using DuckRun.Core.Reporting;

namespace DuckRun.Core.Logging;

internal sealed class ScopedDuckRunConsole(IConsoleStore store, IDashboardReporter reporter) : IDuckRunConsole
{
    public Guid RunId { get; set; }

    public void Info(string message) => Log(DuckRunLogLevel.Info, message);
    public void Warning(string message) => Log(DuckRunLogLevel.Warning, message);
    public void Error(string message) => Log(DuckRunLogLevel.Error, message);

    public void Log(DuckRunLogLevel level, string message)
    {
        if (RunId == Guid.Empty) return;
        var entry = new ConsoleLogEntry(RunId, DateTimeOffset.UtcNow, level, message ?? string.Empty);
        store.Append(entry);
        reporter.ReportLog(entry);
    }
}
