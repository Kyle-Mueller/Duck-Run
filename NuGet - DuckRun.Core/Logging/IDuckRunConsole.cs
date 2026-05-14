namespace DuckRun.Core;

/// <summary>
/// In-job logging surface. Inject into a job method (or any service it calls — it is scoped to the running job)
/// and write log lines that are visible in the dashboard and persisted by DuckRun.EfCore when configured.
/// </summary>
public interface IDuckRunConsole
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Log(DuckRunLogLevel level, string message);
}
