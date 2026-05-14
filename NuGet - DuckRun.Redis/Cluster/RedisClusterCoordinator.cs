using MessagePack;
using DuckRun.Core;
using DuckRun.Core.Cluster;
using DuckRun.Redis.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DuckRun.Redis.Cluster;

internal sealed class RedisClusterCoordinator( DuckRunRedisOptions options, ILogger<RedisClusterCoordinator> logger) : IClusterCoordinator, IHostedService, IAsyncDisposable
{

    private static readonly string AssemblyVersion = typeof(RedisClusterCoordinator).Assembly.GetName().Version?.ToString() ?? "0";

    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly CancellationTokenSource _stopCts = new();

    private IConnectionMultiplexer? _multiplexer;
    private IDatabase? _db;
    private Task? _maintenanceLoop;
    private volatile bool _isLeader;
    private int _stopped;
    private int _disposed;

    public string ProjectId => options.ProjectId;
    public string Environment => options.Environment;
    public string NodeId { get; } = Guid.NewGuid().ToString("N");

    private string KeyPrefix => $"duckrun:{ProjectId}:{Environment}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("DuckRun.Redis connecting (project '{Project}', environment '{Env}', node '{Node}')...", ProjectId, Environment, NodeId);
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(options.ConnectionString);
            _db = _multiplexer.GetDatabase();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DuckRun.Redis failed to connect. Cluster coordination will not work for this run.");
            throw;
        }

        // Do an initial tick so leader status is known before the scheduler starts.
        await DoMaintenanceTickAsync(cancellationToken);

        _maintenanceLoop = Task.Run(() => RunMaintenanceLoopAsync(_stopCts.Token));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;

        try { _stopCts.Cancel(); }
        catch (ObjectDisposedException) { return; }

        if (_maintenanceLoop is not null)
        {
            try
            {
#if NETFRAMEWORK
                await _maintenanceLoop;
#else
                await _maintenanceLoop.WaitAsync(cancellationToken);
#endif
            }
            catch (Exception ex) { logger.LogWarning(ex, "DuckRun.Redis maintenance loop did not exit cleanly."); }
        }

        if (_db is not null && _isLeader)
        {
            try
            {
                await _db.ScriptEvaluateAsync(
                    RedisLuaScripts.ReleaseLeader,
                    [LeaderKey],
                    [NodeId]);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Releasing DuckRun leader key failed."); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await StopAsync(CancellationToken.None);
        if (_multiplexer is not null)
        {
            try { await _multiplexer.DisposeAsync(); }
            catch { /* ignore */ }
        }

        try { _stopCts.Dispose(); }
        catch (ObjectDisposedException) { /* already disposed */ }
    }

    public Task<bool> IsLeaderAsync(CancellationToken ct = default) => Task.FromResult(_isLeader);

    public async Task<IAsyncDisposable?> TryAcquireSlotAsync(string jobName, int maxConcurrency, TimeSpan ttl, CancellationToken ct = default)
    {
        if (_db is null) return null;

        var key = SlotKey(jobName);
        var slotId = new RedisValue($"{NodeId}:{Guid.NewGuid():N}");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ttlSeconds = (int)Math.Max(1, ttl.TotalSeconds);

        var result = await _db.ScriptEvaluateAsync(RedisLuaScripts.AcquireSlot, [key], [now, maxConcurrency, ttlSeconds, slotId]);

        return (int)result == 1 ? new RedisSlot(_db, key, slotId, logger) : null;
    }

    public async Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken ct = default)
    {
        if (_multiplexer is null || _db is null) return [];

        var pattern = $"{KeyPrefix}:nodes:*";
        var endpoints = _multiplexer.GetEndPoints();
        var nodes = new List<NodeInfo>();

        foreach (var endpoint in endpoints)
        {
            var server = _multiplexer.GetServer(endpoint);
            if (!server.IsConnected) continue;

            await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(ct))
            {
                var nodeId = key.ToString().Split(':').Last();
                var value = await _db.StringGetAsync(key);
                if (value.IsNullOrEmpty) continue;

                try
                {
                    var meta = MessagePackSerializer.Deserialize<NodeMeta>((byte[])value!);
                    if (meta is null) continue;
                    nodes.Add(new NodeInfo(NodeId: nodeId,
                                           StartedAt: meta.StartedAt,
                                           LastSeen: DateTimeOffset.UtcNow,
                                           IsLeader: nodeId == await GetLeaderNodeIdAsync(),
                                           Version: meta.Version));
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Skipping malformed node entry '{Key}'.", key);
                }
            }
            break; // single endpoint is enough for standalone; multi-shard handled later
        }

        return nodes;
    }

    private async Task<string?> GetLeaderNodeIdAsync()
    {
        if (_db is null) return null;
        var owner = await _db.StringGetAsync(LeaderKey);
        return owner.IsNullOrEmpty ? null : owner.ToString();
    }

    private async Task RunMaintenanceLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await DoMaintenanceTickAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DuckRun.Redis maintenance tick failed.");
                if (_isLeader)
                {
                    _isLeader = false;
                    logger.LogWarning("Demoted from leader due to Redis errors.");
                }
            }

            try { await Task.Delay(options.LeaderRefreshInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DoMaintenanceTickAsync(CancellationToken ct)
    {
        if (_db is null) return;

        // Heartbeat first — even followers want to be visible in the cluster view.
        await UpdateHeartbeatAsync();

        // Then try acquire / refresh leadership.
        var leaderTtl = (int)Math.Max(1, options.LeaderLeaseDuration.TotalSeconds);
        var result = await _db.ScriptEvaluateAsync(RedisLuaScripts.AcquireOrRefreshLeader, [LeaderKey], [NodeId, leaderTtl]);

        var wasLeader = _isLeader;
        _isLeader = (int)result == 1;

        if (_isLeader && !wasLeader) logger.LogInformation("Acquired DuckRun leadership for project '{Project}' env '{Env}' (node '{Node}').", ProjectId, Environment, NodeId);
        else if (!_isLeader && wasLeader) logger.LogInformation("Lost DuckRun leadership for project '{Project}' env '{Env}' (node '{Node}').", ProjectId, Environment, NodeId);
    }

    private async Task UpdateHeartbeatAsync()
    {
        if (_db is null) return;
        var nodeKey = (RedisKey)$"{KeyPrefix}:nodes:{NodeId}";
        var meta = MessagePackSerializer.Serialize(new NodeMeta(_startedAt, AssemblyVersion));
        await _db.StringSetAsync(nodeKey, meta, options.HeartbeatTtl);
    }

    private RedisKey LeaderKey => $"{KeyPrefix}:leader";
    private RedisKey SlotKey(string jobName) => $"{KeyPrefix}:slots:{jobName}";

    [MessagePackObject]
    internal sealed record NodeMeta([property: Key(0)] DateTimeOffset StartedAt, [property: Key(1)] string Version);
}
