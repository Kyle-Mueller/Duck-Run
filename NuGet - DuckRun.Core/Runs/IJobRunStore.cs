namespace DuckRun.Core.Runs;

internal interface IJobRunStore
{
    Task AddAsync(JobRun run, CancellationToken ct);
    Task UpdateAsync(JobRun run, CancellationToken ct);
    Task<JobRun?> GetAsync(Guid runId, CancellationToken ct);
    Task<IReadOnlyList<JobRun>> GetRecentForJobAsync(string jobName, int take, CancellationToken ct);
    Task<int> CountInFlightAsync(string jobName, CancellationToken ct);

    /// <summary>Runs created at or after <paramref name="since"/>, across all jobs, newest first, capped at <paramref name="max"/>.</summary>
    Task<IReadOnlyList<JobRun>> GetRunsSinceAsync(DateTimeOffset since, int max, CancellationToken ct);
}
