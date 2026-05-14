using DuckRun.Core.Cluster;

namespace DuckRun.Core.Runs;

/// <summary>
/// Acquires concurrency permission for a job run. Always checks the local tracker first (fast, free);
/// then, if the job has a finite <see cref="JobDescriptor.MaxConcurrency"/>, asks the cluster coordinator
/// for a distributed slot too. Either failing rolls the other back.
/// </summary>
internal sealed class JobSlotGate(LocalConcurrencyTrackers local, IClusterCoordinator coordinator)
{
    public async Task<JobSlot?> TryAcquireAsync(JobDescriptor job, TimeSpan ttl, CancellationToken ct = default)
    {
        var tracker = local.GetTracker(job);
        if (!tracker.TryAcquire()) return null;

        IAsyncDisposable? remote = null;
        if (job.MaxConcurrency < int.MaxValue)
        {
            try
            {
                remote = await coordinator.TryAcquireSlotAsync(job.Name, job.MaxConcurrency, ttl, ct);
            }
            catch
            {
                tracker.Release();
                throw;
            }

            if (remote is null)
            {
                tracker.Release();
                return null;
            }
        }

        return new JobSlot(tracker, remote);
    }
}
