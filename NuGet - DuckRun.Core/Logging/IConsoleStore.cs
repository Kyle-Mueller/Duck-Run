namespace DuckRun.Core.Logging;

/// <summary>
/// Abstraction over the in-job console log store. The default implementation is in-memory;
/// DuckRun.EfCore swaps in a database-backed implementation when configured.
/// </summary>
internal interface IConsoleStore
{
    /// <summary>Append a single entry. Called synchronously from job code; implementations buffer if persistence is async.</summary>
    void Append(ConsoleLogEntry entry);

    /// <summary>Read the log entries for a run, ordered by timestamp ascending.</summary>
    Task<IReadOnlyList<ConsoleLogEntry>> GetForRunAsync(Guid runId, CancellationToken ct = default);
}
