namespace DuckRun.Core.Internal.Logging;

internal sealed class ScopedDuckRunConsole(InMemoryConsoleStore store) : IDuckRunConsole
{
    public Guid RunId { get; set; }

    public void Info(string message) => Log(DuckRunLogLevel.Info, message);
    public void Warning(string message) => Log(DuckRunLogLevel.Warning, message);
    public void Error(string message) => Log(DuckRunLogLevel.Error, message);

    public void Log(DuckRunLogLevel level, string message)
    {
        if (RunId == Guid.Empty) return;
        store.Append(new ConsoleLogEntry(RunId, DateTimeOffset.UtcNow, level, message ?? string.Empty));
    }
}
