using Cronos;
using DuckRun.Core;
using DuckRun.Core.Cluster;
using DuckRun.Core.Jobs;
using DuckRun.Core.Runs;
using DuckRun.Framework.Runs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DuckRun.Framework.Scheduler;

/// <summary>
/// Task-based cron loop. Equivalent to <c>DuckRunSchedulerService</c> on .Core but runs on a single
/// long-lived Task rather than via <c>BackgroundService</c>. Started/stopped explicitly by <c>DuckRunHost</c>.
/// </summary>
internal sealed class SchedulerLoop(IJobRegistry registry, JobExecutor executor, JobSlotGate slotGate, IClusterCoordinator coordinator, ILogger? logger = null)
{

    private static readonly TimeSpan FollowerPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaderTickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SlotTtl = TimeSpan.FromHours(1);
    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly TimeZoneInfo _timezone = TimeZoneInfo.Local;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public CancellationToken StoppingToken => _cts?.Token ?? new CancellationToken(canceled: true);

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _loop?.Wait(TimeSpan.FromSeconds(5)); } catch { /* swallow */ }
        try { _cts?.Dispose(); } catch (ObjectDisposedException) { }
        _cts = null;
        _loop = null;
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DuckRun.Framework scheduler started with {Count} job(s) in timezone '{Tz}' (project '{Project}', node '{Node}').", 
                               registry.All.Count, _timezone.Id, coordinator.ProjectId, coordinator.NodeId);

        var nextRuns = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        var wasLeader = false;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool isLeader;
                try { isLeader = await coordinator.IsLeaderAsync(stoppingToken); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Leader check failed; treating as follower this tick.");
                    isLeader = false;
                }

                if (isLeader && !wasLeader)
                {
                    nextRuns.Clear();
                    var bootNow = DateTimeOffset.UtcNow;
                    foreach (var job in registry.All)
                        nextRuns[job.Name] = ComputeNext(job, bootNow);
                    _logger.LogInformation("This node is now the DuckRun.Framework leader; will fire crons.");
                }
                else if (!isLeader && wasLeader)
                {
                    _logger.LogInformation("This node is no longer the DuckRun.Framework leader.");
                }
                wasLeader = isLeader;

                if (!isLeader)
                {
                    try { await Task.Delay(FollowerPollInterval, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                foreach (var job in registry.All)
                {
                    if (!job.Enabled) continue;
                    if (!nextRuns.TryGetValue(job.Name, out var next) || next is null) continue;
                    if (now < next.Value) continue;

                    var slot = await slotGate.TryAcquireAsync(job, SlotTtl, stoppingToken);
                    if (slot is null)
                    {
                        _logger.LogDebug("Skipping tick for '{Job}': at concurrency cap.", job.Name);
                    }
                    else
                    {
                        _ = Task.Run(async () =>
                        {
                            try { await executor.ExecuteAsync(job, "Cron", stoppingToken); }
                            catch (Exception ex) { _logger.LogError(ex, "Unhandled exception in cron run of '{Job}'.", job.Name); }
                            finally { await slot.DisposeAsync(); }
                        }, stoppingToken);
                    }

                    nextRuns[job.Name] = ComputeNext(job, now);
                }

                try { await Task.Delay(LeaderTickInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            _logger.LogInformation("DuckRun.Framework scheduler stopping.");
        }
    }

    private DateTimeOffset? ComputeNext(JobDescriptor job, DateTimeOffset after)
    {
        try
        {
            var fieldCount = job.Cron.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
            var expr = CronExpression.Parse(job.Cron, format);
            var next = expr.GetNextOccurrence(after.UtcDateTime, _timezone);
            return next.HasValue ? new DateTimeOffset(next.Value, TimeSpan.Zero) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute next run for '{Job}'.", job.Name);
            return null;
        }
    }
}
