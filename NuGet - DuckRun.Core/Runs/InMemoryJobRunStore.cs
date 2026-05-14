using System.Collections.Concurrent;

namespace DuckRun.Core.Runs;

internal sealed class InMemoryJobRunStore(int maxPerJob) : IJobRunStore
{
    private readonly ConcurrentDictionary<Guid, JobRun> _byId = new();
    private readonly ConcurrentDictionary<string, LinkedList<Guid>> _byJob = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxPerJob = maxPerJob;

    public Task AddAsync(JobRun run, CancellationToken ct)
    {
        _byId[run.Id] = run;
        var list = _byJob.GetOrAdd(run.JobName, _ => new LinkedList<Guid>());
        lock (list)
        {
            list.AddFirst(run.Id);
            while (list.Count > _maxPerJob)
            {
                var drop = list.Last!.Value;
                list.RemoveLast();
                _byId.TryRemove(drop, out _);
            }
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(JobRun run, CancellationToken ct)
    {
        _byId[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task<JobRun?> GetAsync(Guid runId, CancellationToken ct) => Task.FromResult(_byId.TryGetValue(runId, out var run) ? run : null);

    public Task<IReadOnlyList<JobRun>> GetRecentForJobAsync(string jobName, int take, CancellationToken ct)
    {
        if (!_byJob.TryGetValue(jobName, out var list)) return Task.FromResult<IReadOnlyList<JobRun>>([]);

        Guid[] snapshot;
        lock (list) { snapshot = list.Take(take).ToArray(); }

        var runs = new List<JobRun>(snapshot.Length);
        foreach (var id in snapshot) if (_byId.TryGetValue(id, out var r)) runs.Add(r);

        return Task.FromResult<IReadOnlyList<JobRun>>(runs);
    }

    public Task<int> CountInFlightAsync(string jobName, CancellationToken ct)
    {
        if (!_byJob.TryGetValue(jobName, out var list)) return Task.FromResult(0);

        Guid[] snapshot;
        lock (list) { snapshot = [.. list]; }

        var count = 0;
        foreach (var id in snapshot)
            if (_byId.TryGetValue(id, out var r) && r.State == JobRunState.Running)
                count++;

        return Task.FromResult(count);
    }
}
