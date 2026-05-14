namespace DuckRun.Core.Runs;

/// <summary>
/// Combined local + distributed concurrency slot for a single job run. Disposing releases both halves
/// in the correct order: distributed first (so the cluster sees the slot freed) then local.
/// </summary>
internal sealed class JobSlot(ConcurrencyTracker tracker, IAsyncDisposable? remote) : IAsyncDisposable
{
    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            if (remote is not null) await remote.DisposeAsync();
        }
        finally
        {
            tracker.Release();
        }
    }
}
