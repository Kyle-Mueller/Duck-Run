namespace DuckRun.Core.Logging;

internal sealed class ConsoleRunLog(int max)
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
