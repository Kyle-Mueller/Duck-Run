namespace DuckRun.Core.Reporting;

/// <summary>
/// Outgoing telemetry channel to the centralized Control Dashboard.
/// Default implementation is a no-op; <c>UseDashboard(dsn)</c> swaps in the gRPC implementation.
/// </summary>
internal interface IDashboardReporter
{
    /// <summary>Enqueue a run create-or-update for the dashboard. Non-blocking; batched and sent in the background.</summary>
    void ReportRun(JobRun run);

    /// <summary>Enqueue a console line for the dashboard. Non-blocking; batched and sent in the background.</summary>
    void ReportLog(ConsoleLogEntry entry);
}
