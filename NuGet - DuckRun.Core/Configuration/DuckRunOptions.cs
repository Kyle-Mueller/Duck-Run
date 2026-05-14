using System.Reflection;

namespace DuckRun.Core;

/// <summary>
/// Resolved configuration for a DuckRun host. Built by <see cref="DuckRunOptionsBuilder"/>.
/// </summary>
public sealed class DuckRunOptions
{
    public IReadOnlyList<Assembly> AssembliesToScan { get; init; } = Array.Empty<Assembly>();
    public IReadOnlyList<JobDescriptor> ExplicitJobs { get; init; } = Array.Empty<JobDescriptor>();

    /// <summary>When true, the standalone dashboard is wired into the host pipeline by <c>MapDuckRunDashboard()</c>.</summary>
    public bool StandaloneDashboardEnabled { get; init; }

    /// <summary>Path prefix for the standalone dashboard. Defaults to <c>/duckrun</c>.</summary>
    public string StandaloneDashboardPath { get; init; } = "/duckrun";

    /// <summary>Maximum console log entries retained per run in the in-memory store. Older entries are dropped first.</summary>
    public int ConsoleEntriesPerRun { get; init; } = 1000;

    /// <summary>Maximum runs retained per job in the in-memory run store. Older runs are dropped first.</summary>
    public int RunsRetainedPerJob { get; init; } = 200;

    /// <summary>DSN for the centralized Control Dashboard. When null, the runtime is dashboard-less or uses the embedded UI.</summary>
    public string? DashboardDsn { get; init; }
}
