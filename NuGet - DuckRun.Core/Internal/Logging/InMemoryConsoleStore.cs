using System.Collections.Concurrent;

namespace DuckRun.Core.Internal.Logging;

internal sealed class InMemoryConsoleStore(DuckRunOptions options)
{

    private readonly ConcurrentDictionary<Guid, ConsoleRunLog> _byRun = new();
    private readonly int _maxPerRun = options.ConsoleEntriesPerRun;

    public void Append(ConsoleLogEntry entry)
    {
        var bucket = _byRun.GetOrAdd(entry.RunId, _ => new ConsoleRunLog(_maxPerRun));
        bucket.Append(entry);
    }

    public IReadOnlyList<ConsoleLogEntry> GetForRun(Guid runId) => _byRun.TryGetValue(runId, out var bucket) ? bucket.Snapshot() : [];

    public void Drop(Guid runId) => _byRun.TryRemove(runId, out _);

    private sealed class ConsoleRunLog(int max)
    {
        private readonly Queue<ConsoleLogEntry> _entries = new(Math.Min(max, 64));
        private readonly object _lock = new();

        public void Append(ConsoleLogEntry entry)
        {
            lock (_lock)
            {
                _entries.Enqueue(entry);
                while (_entries.Count > max) _entries.Dequeue();
            }
        }

        public ConsoleLogEntry[] Snapshot()
        {
            lock (_lock) { return [.. _entries]; }
        }
    }
}
