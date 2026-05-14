using DuckRun.Core.Jobs;
using DuckRun.Core.Logging;
using DuckRun.Core.Runs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.Core.Operations;

internal sealed class DuckRunController(IJobRegistry registry,
                                        JobExecutor executor,
                                        IJobRunStore runs,
                                        IConsoleStore console,
                                        JobSlotGate slotGate,
                                        IHostApplicationLifetime lifetime,
                                        ILogger<DuckRunController> logger) : IDuckRunController
{
    private static readonly TimeSpan SlotTtl = TimeSpan.FromHours(1);

    public IReadOnlyList<JobDescriptor> ListJobs() => registry.All;

    public JobDescriptor? GetJob(string name) => registry.FindByName(name);

    public async Task<Guid> TriggerAsync(string jobName, CancellationToken ct = default)
    {
        var job = registry.FindByName(jobName) ?? throw new InvalidOperationException($"Unknown job '{jobName}'.");
        if (!job.AllowManualTrigger) throw new InvalidOperationException($"Job '{jobName}' does not allow manual triggering.");

        var slot = await slotGate.TryAcquireAsync(job, SlotTtl, ct)
            ?? throw new InvalidOperationException($"Job '{jobName}' is at MaxConcurrency; refusing manual trigger.");

        var run = await executor.CreateAndStoreRunAsync(job, "Manual", ct);

        _ = Task.Run(async () =>
        {
            try { await executor.ContinueRunAsync(run, job, lifetime.ApplicationStopping); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception during manual run of '{Job}'.", job.Name);
            }
            finally { await slot.DisposeAsync(); }
        }, ct);

        return run.Id;
    }

    public Task CancelAsync(Guid runId, CancellationToken ct = default)
    {
        executor.RequestCancel(runId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JobRun>> GetRecentRunsAsync(string jobName, int take = 50, CancellationToken ct = default) => runs.GetRecentForJobAsync(jobName, take, ct);

    public Task<JobRun?> GetRunAsync(Guid runId, CancellationToken ct = default) => runs.GetAsync(runId, ct);

    public Task<IReadOnlyList<ConsoleLogEntry>> GetConsoleAsync(Guid runId, CancellationToken ct = default) => console.GetForRunAsync(runId, ct);
}
