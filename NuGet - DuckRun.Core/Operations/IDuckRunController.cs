namespace DuckRun.Core;

/// <summary>
/// Public surface for triggering and cancelling jobs by name, and querying recent run state.
/// Resolve from DI to drive jobs from your own controllers or background services.
/// </summary>
public interface IDuckRunController
{
    /// <summary>Returns metadata for every registered job.</summary>
    IReadOnlyList<JobDescriptor> ListJobs();

    /// <summary>Returns the descriptor for the named job, or null if not registered.</summary>
    JobDescriptor? GetJob(string name);

    /// <summary>Starts a manual run for the named job. Returns the new run id.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the job is unknown or manual triggering is disabled for it.</exception>
    Task<Guid> TriggerAsync(string jobName, CancellationToken ct = default);

    /// <summary>Signals cancellation to an in-flight run. No-op if the run is already finished or unknown.</summary>
    Task CancelAsync(Guid runId, CancellationToken ct = default);

    /// <summary>Returns the most recent runs for a job, newest first.</summary>
    Task<IReadOnlyList<JobRun>> GetRecentRunsAsync(string jobName, int take = 50, CancellationToken ct = default);

    /// <summary>Returns a single run by id, or null if not known.</summary>
    Task<JobRun?> GetRunAsync(Guid runId, CancellationToken ct = default);

    /// <summary>Returns the console log lines captured for a run, in order.</summary>
    Task<IReadOnlyList<ConsoleLogEntry>> GetConsoleAsync(Guid runId, CancellationToken ct = default);
}
