using Cronos;
using DuckRun.Core.Cluster;
using DuckRun.Core.Jobs;
using DuckRun.Core.Runs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.Core.Scheduler;

internal sealed class DuckRunSchedulerService(
    IJobRegistry registry,
    JobExecutor executor,
    JobSlotGate slotGate,
    IClusterCoordinator coordinator,
    ILogger<DuckRunSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan FollowerPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaderTickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SlotTtl = TimeSpan.FromHours(1);

    private readonly TimeZoneInfo _timezone = TimeZoneInfo.Local;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DuckRun scheduler started with {Count} job(s) in timezone '{Tz}' (project '{Project}', node '{Node}').",
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
                    logger.LogWarning(ex, "Leader check failed; treating this node as follower for this tick.");
                    isLeader = false;
                }

                if (isLeader && !wasLeader)
                {
                    nextRuns.Clear();
                    var bootNow = DateTimeOffset.UtcNow;
                    foreach (var job in registry.All)
                        nextRuns[job.Name] = ComputeNext(job, bootNow);
                    logger.LogInformation("This node is now the DuckRun leader; will fire crons.");
                }
                else if (!isLeader && wasLeader)
                {
                    logger.LogInformation("This node is no longer the DuckRun leader; holding crons.");
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
                        logger.LogDebug("Skipping tick for '{Job}': at concurrency cap.", job.Name);
                    }
                    else
                    {
                        _ = Task.Run(async () =>
                        {
                            try { await executor.ExecuteAsync(job, "Cron", stoppingToken); }
                            catch (Exception ex) { logger.LogError(ex, "Unhandled exception running DuckRun job '{Job}'.", job.Name); }
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
            logger.LogInformation("DuckRun scheduler stopping.");
        }
    }

    private DateTimeOffset? ComputeNext(JobDescriptor job, DateTimeOffset after)
    {
        try
        {
            var fieldCount = job.Cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
            var expr = CronExpression.Parse(job.Cron, format);
            var next = expr.GetNextOccurrence(after.UtcDateTime, _timezone);
            return next.HasValue ? new DateTimeOffset(next.Value, TimeSpan.Zero) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compute next run for '{Job}'.", job.Name);
            return null;
        }
    }
}
