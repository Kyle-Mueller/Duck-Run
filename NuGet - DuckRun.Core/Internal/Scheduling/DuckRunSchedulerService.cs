using System.Collections.Concurrent;
using Cronos;
using DuckRun.Core.Internal.Discovery;
using DuckRun.Core.Internal.Execution;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.Core.Internal.Scheduling;

internal sealed class DuckRunSchedulerService : BackgroundService
{
    private readonly IJobRegistry _registry;
    private readonly JobExecutor _executor;
    private readonly ILogger<DuckRunSchedulerService> _logger;
    private readonly ConcurrentDictionary<string, ConcurrencyTracker> _concurrency = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeZoneInfo _timezone = TimeZoneInfo.Utc;

    public DuckRunSchedulerService(
        IJobRegistry registry,
        JobExecutor executor,
        ILogger<DuckRunSchedulerService> logger)
    {
        _registry = registry;
        _executor = executor;
        _logger = logger;
    }

    public ConcurrencyTracker GetTracker(JobDescriptor job) =>
        _concurrency.GetOrAdd(job.Name, _ => new ConcurrencyTracker(job.MaxConcurrency));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DuckRun scheduler started with {Count} job(s).", _registry.All.Count);

        var nextRuns = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in _registry.All)
        {
            nextRuns[job.Name] = ComputeNext(job, DateTimeOffset.UtcNow);
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;

                foreach (var job in _registry.All)
                {
                    if (!job.Enabled) continue;
                    if (!nextRuns.TryGetValue(job.Name, out var next) || next is null) continue;
                    if (now < next.Value) continue;

                    var tracker = GetTracker(job);
                    if (tracker.TryAcquire())
                    {
                        _ = Task.Run(async () =>
                        {
                            try { await _executor.ExecuteAsync(job, "Cron", stoppingToken); }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unhandled exception running DuckRun job '{Job}'.", job.Name);
                            }
                            finally { tracker.Release(); }
                        }, stoppingToken);
                    }
                    else
                    {
                        _logger.LogDebug("Skipping tick for '{Job}': MaxConcurrency={Max} reached.", job.Name, tracker.Max);
                    }

                    nextRuns[job.Name] = ComputeNext(job, now);
                }

                try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            _logger.LogInformation("DuckRun scheduler stopping.");
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
            _logger.LogError(ex, "Failed to compute next run for '{Job}'.", job.Name);
            return null;
        }
    }
}
