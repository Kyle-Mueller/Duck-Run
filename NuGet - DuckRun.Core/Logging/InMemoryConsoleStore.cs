using System.Collections.Concurrent;

namespace DuckRun.Core.Logging;

internal sealed class InMemoryConsoleStore(int maxPerRun) : IConsoleStore
{
    private readonly ConcurrentDictionary<Guid, ConsoleRunLog> _byRun = new();
    private readonly int _maxPerRun = maxPerRun;

    public void Append(ConsoleLogEntry entry)
    {
        var bucket = _byRun.GetOrAdd(entry.RunId, _ => new ConsoleRunLog(_maxPerRun));
        bucket.Append(entry);
    }

    public Task<IReadOnlyList<ConsoleLogEntry>> GetForRunAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<ConsoleLogEntry> entries = _byRun.TryGetValue(runId, out var bucket) ? bucket.Snapshot() : [];
        return Task.FromResult(entries);
    }

    public void Drop(Guid runId) => _byRun.TryRemove(runId, out _);
}
