using System.Collections.Concurrent;

namespace DuckRun.Core.Logging;

/// <summary>
/// In-memory console store. Bounds both the entries kept per run and the number of runs tracked,
/// evicting the oldest run's log when the run cap is exceeded — so a long-lived process doesn't leak
/// console buffers for runs that have long since aged out of the run history.
/// </summary>
internal sealed class InMemoryConsoleStore : IConsoleStore
{
    private readonly ConcurrentDictionary<Guid, ConsoleRunLog> _byRun = new();
    private readonly LinkedList<Guid> _order = new();
    private readonly object _orderLock = new();
    private readonly int _maxPerRun;
    private readonly int _maxRuns;

    public InMemoryConsoleStore(int maxPerRun, int maxRuns)
    {
        _maxPerRun = maxPerRun;
        _maxRuns = Math.Max(1, maxRuns);
    }

    public void Append(ConsoleLogEntry entry)
    {
        if (_byRun.TryGetValue(entry.RunId, out var existing))
        {
            existing.Append(entry);
            return;
        }

        var created = new ConsoleRunLog(_maxPerRun);
        var bucket = _byRun.GetOrAdd(entry.RunId, created);
        if (ReferenceEquals(bucket, created)) TrackNewRun(entry.RunId);
        bucket.Append(entry);
    }

    public Task<IReadOnlyList<ConsoleLogEntry>> GetForRunAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<ConsoleLogEntry> entries = _byRun.TryGetValue(runId, out var bucket) ? bucket.Snapshot() : [];
        return Task.FromResult(entries);
    }

    private void TrackNewRun(Guid runId)
    {
        lock (_orderLock)
        {
            _order.AddFirst(runId);
            while (_order.Count > _maxRuns)
            {
                var evict = _order.Last!.Value;
                _order.RemoveLast();
                _byRun.TryRemove(evict, out _);
            }
        }
    }
}
