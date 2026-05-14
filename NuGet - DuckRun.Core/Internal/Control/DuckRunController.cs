using DuckRun.Core.Internal.Discovery;
using DuckRun.Core.Internal.Execution;
using DuckRun.Core.Internal.Logging;
using DuckRun.Core.Internal.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.Core.Internal.Control;

internal sealed class DuckRunController : IDuckRunController
{
    private readonly IJobRegistry _registry;
    private readonly JobExecutor _executor;
    private readonly IJobRunStore _runs;
    private readonly InMemoryConsoleStore _console;
    private readonly DuckRunSchedulerService _scheduler;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DuckRunController> _logger;

    public DuckRunController(
        IJobRegistry registry,
        JobExecutor executor,
        IJobRunStore runs,
        InMemoryConsoleStore console,
        DuckRunSchedulerService scheduler,
        IHostApplicationLifetime lifetime,
        ILogger<DuckRunController> logger)
    {
        _registry = registry;
        _executor = executor;
        _runs = runs;
        _console = console;
        _scheduler = scheduler;
        _lifetime = lifetime;
        _logger = logger;
    }

    public IReadOnlyList<JobDescriptor> ListJobs() => _registry.All;

    public JobDescriptor? GetJob(string name) => _registry.FindByName(name);

    public async Task<Guid> TriggerAsync(string jobName, CancellationToken ct = default)
    {
        var job = _registry.FindByName(jobName)
            ?? throw new InvalidOperationException($"Unknown job '{jobName}'.");
        if (!job.AllowManualTrigger)
            throw new InvalidOperationException($"Job '{jobName}' does not allow manual triggering.");

        var tracker = _scheduler.GetTracker(job);
        if (!tracker.TryAcquire())
            throw new InvalidOperationException($"Job '{jobName}' is at MaxConcurrency ({tracker.Max}); refusing manual trigger.");

        var run = await _executor.CreateAndStoreRunAsync(job, "Manual", ct);

        _ = Task.Run(async () =>
        {
            try { await _executor.ContinueRunAsync(run, job, _lifetime.ApplicationStopping); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during manual run of '{Job}'.", job.Name);
            }
            finally { tracker.Release(); }
        });

        return run.Id;
    }

    public Task CancelAsync(Guid runId, CancellationToken ct = default)
    {
        _executor.RequestCancel(runId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JobRun>> GetRecentRunsAsync(string jobName, int take = 50, CancellationToken ct = default) =>
        _runs.GetRecentForJobAsync(jobName, take, ct);

    public Task<JobRun?> GetRunAsync(Guid runId, CancellationToken ct = default) =>
        _runs.GetAsync(runId, ct);

    public Task<IReadOnlyList<ConsoleLogEntry>> GetConsoleAsync(Guid runId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ConsoleLogEntry>>(_console.GetForRun(runId));
}
