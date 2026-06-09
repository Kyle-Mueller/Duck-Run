using System.Reflection;
using System.Threading.Channels;
using DuckRun.Core.Cluster;
using DuckRun.Core.Jobs;
using DuckRun.Protocol.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.Core.Reporting;

internal sealed class GrpcDashboardReporter(
    Dsn dsn,
    IJobRegistry registry,
    IClusterCoordinator coordinator,
    ILogger<GrpcDashboardReporter> logger) : IDashboardReporter, IHostedService, IAsyncDisposable
{
    private static readonly string ClientVersion =
        typeof(GrpcDashboardReporter).Assembly.GetName().Version?.ToString() ?? "0";

    private readonly Channel<RunRecord> _runs = Channel.CreateUnbounded<RunRecord>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<LogEntry> _logs = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();

    private GrpcChannel? _channel;
    private IngestService.IngestServiceClient? _client;
    private Task? _handshake;
    private Task? _runFlush;
    private Task? _logFlush;
    private Task? _heartbeat;
    private int _stopped;
    private int _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Allow HTTP/2 over plaintext for dev (http:// DSN).
        if (dsn.Scheme == "http")
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        _channel = GrpcChannel.ForAddress(dsn.EndpointUrl);
        var interceptor = new ApiKeyClientInterceptor(dsn.PublicKey);
        var invoker = _channel.Intercept(interceptor);
        _client = new IngestService.IngestServiceClient(invoker);

        // Handshake (which registers job definitions) retries in the background until it succeeds,
        // so a dashboard that's briefly unreachable at startup doesn't leave the project's jobs unregistered.
        _handshake = Task.Run(() => HandshakeLoopAsync(_stop.Token));
        _runFlush = Task.Run(() => FlushRunsAsync(_stop.Token));
        _logFlush = Task.Run(() => FlushLogsAsync(_stop.Token));
        _heartbeat = Task.Run(() => HeartbeatLoopAsync(_stop.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;

        try { _stop.Cancel(); } catch (ObjectDisposedException) { return; }

        _runs.Writer.TryComplete();
        _logs.Writer.TryComplete();

        async Task SafeWait(Task? t) { if (t is null) return; try { await t.WaitAsync(cancellationToken); } catch { } }
        await SafeWait(_handshake);
        await SafeWait(_runFlush);
        await SafeWait(_logFlush);
        await SafeWait(_heartbeat);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await StopAsync(CancellationToken.None);
        _channel?.Dispose();
        try { _stop.Dispose(); } catch (ObjectDisposedException) { }
    }

    public void ReportRun(JobRun run) => _runs.Writer.TryWrite(ToProto(run, coordinator.NodeId));

    public void ReportLog(ConsoleLogEntry entry) => _logs.Writer.TryWrite(ToProto(entry));

    private async Task HandshakeLoopAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(2);
        var maxDelay = TimeSpan.FromSeconds(30);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SendHandshakeAsync(ct);
                return; // success — job definitions are now registered with the dashboard
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dashboard handshake failed; retrying in {Delay}s.", (int)delay.TotalSeconds);
            }

            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { return; }
            delay = TimeSpan.FromSeconds(Math.Min(maxDelay.TotalSeconds, delay.TotalSeconds * 2));
        }
    }

    private async Task SendHandshakeAsync(CancellationToken ct)
    {
        if (_client is null) return;

        var req = new HandshakeRequest
        {
            NodeId = coordinator.NodeId,
            ClientVersion = ClientVersion,
            ProtocolVersion = "v1",
            Runtime = "net10.0",
            StartedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        };
        foreach (var job in registry.All)
        {
            req.Jobs.Add(new Protocol.V1.JobDefinition
            {
                Name = job.Name,
                Cron = job.Cron,
                MaxConcurrency = job.MaxConcurrency == int.MaxValue ? 0 : job.MaxConcurrency,
                TimeoutSeconds = (int)(job.Timeout?.TotalSeconds ?? 0),
                AllowManualTrigger = job.AllowManualTrigger,
                Enabled = job.Enabled,
            });
        }

        var resp = await _client.HandshakeAsync(req, cancellationToken: ct);
        logger.LogInformation("Connected to DuckRun dashboard project '{Project}' (protocol '{Proto}').",
            resp.ProjectId, resp.AcceptedProtocolVersion);
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(10);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_client is not null)
                {
                    var isLeader = await coordinator.IsLeaderAsync(ct);
                    await _client.SendHeartbeatAsync(new HeartbeatRequest
                    {
                        NodeId = coordinator.NodeId,
                        At = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                        IsLeader = isLeader,
                    }, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogDebug(ex, "Dashboard heartbeat failed; continuing."); }

            try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task FlushRunsAsync(CancellationToken ct)
    {
        var batch = new List<RunRecord>(50);
        try
        {
            while (await _runs.Reader.WaitToReadAsync(ct))
            {
                batch.Clear();
                while (batch.Count < 100 && _runs.Reader.TryRead(out var r)) batch.Add(r);

                if (_client is null || batch.Count == 0) continue;

                var msg = new RunsBatch();
                msg.Runs.AddRange(batch);

                try { await _client.SendRunsAsync(msg, cancellationToken: ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.LogWarning(ex, "Sending {Count} run records to dashboard failed.", batch.Count); }

                try { await Task.Delay(TimeSpan.FromMilliseconds(500), ct); } catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private async Task FlushLogsAsync(CancellationToken ct)
    {
        var batch = new List<LogEntry>(100);
        try
        {
            while (await _logs.Reader.WaitToReadAsync(ct))
            {
                batch.Clear();
                while (batch.Count < 200 && _logs.Reader.TryRead(out var l)) batch.Add(l);

                if (_client is null || batch.Count == 0) continue;

                var msg = new LogsBatch();
                msg.Logs.AddRange(batch);

                try { await _client.SendLogsAsync(msg, cancellationToken: ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.LogWarning(ex, "Sending {Count} log entries to dashboard failed.", batch.Count); }

                try { await Task.Delay(TimeSpan.FromMilliseconds(250), ct); } catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private static RunRecord ToProto(JobRun run, string nodeId)
    {
        var r = new RunRecord
        {
            Id = run.Id.ToString(),
            JobName = run.JobName,
            NodeId = nodeId,
            State = run.State.ToString(),
            TriggerSource = run.TriggerSource,
            ErrorMessage = run.ErrorMessage ?? "",
            ErrorStackTrace = run.ErrorStackTrace ?? "",
            CreatedAt = Timestamp.FromDateTimeOffset(run.CreatedAt),
        };
        if (run.StartedAt is { } s) r.StartedAt = Timestamp.FromDateTimeOffset(s);
        if (run.FinishedAt is { } f) r.FinishedAt = Timestamp.FromDateTimeOffset(f);
        return r;
    }

    private static LogEntry ToProto(ConsoleLogEntry entry) => new()
    {
        RunId = entry.RunId.ToString(),
        Timestamp = Timestamp.FromDateTimeOffset(entry.Timestamp),
        Level = entry.Level.ToString(),
        Message = entry.Message,
    };
}
