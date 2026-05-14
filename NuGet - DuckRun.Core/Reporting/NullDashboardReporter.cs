namespace DuckRun.Core.Reporting;

internal sealed class NullDashboardReporter : IDashboardReporter
{
    public void ReportRun(JobRun run) { /* no-op */ }
    public void ReportLog(ConsoleLogEntry entry) { /* no-op */ }
}
