namespace DuckRun.Core.Internal.Execution;

internal interface IJobRunStore
{
    Task AddAsync(JobRun run, CancellationToken ct);
    Task UpdateAsync(JobRun run, CancellationToken ct);
    Task<JobRun?> GetAsync(Guid runId, CancellationToken ct);
    Task<IReadOnlyList<JobRun>> GetRecentForJobAsync(string jobName, int take, CancellationToken ct);
    Task<int> CountInFlightAsync(string jobName, CancellationToken ct);
}
