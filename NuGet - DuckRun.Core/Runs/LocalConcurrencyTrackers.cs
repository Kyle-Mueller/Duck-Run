using System.Collections.Concurrent;

namespace DuckRun.Core.Runs;

internal sealed class LocalConcurrencyTrackers
{
    private readonly ConcurrentDictionary<string, ConcurrencyTracker> _byJob = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrencyTracker GetTracker(JobDescriptor job) => _byJob.GetOrAdd(job.Name, _ => new ConcurrencyTracker(job.MaxConcurrency));
}
