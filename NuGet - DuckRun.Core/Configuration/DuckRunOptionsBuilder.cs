using System.Reflection;

namespace DuckRun.Core;

/// <summary>
/// Fluent builder used by <c>AddDuckRun(...)</c>.
/// </summary>
public sealed class DuckRunOptionsBuilder
{
    private readonly List<Assembly> _assemblies = new();
    private readonly List<JobDescriptor> _explicitJobs = new();

    public bool StandaloneDashboardEnabled { get; private set; }
    public string StandaloneDashboardPath { get; private set; } = "/duckrun";
    public int ConsoleEntriesPerRun { get; private set; } = 1000;
    public int RunsRetainedPerJob { get; private set; } = 200;
    public string? DashboardDsn { get; private set; }

    /// <summary>Adds all types from the assembly to the discovery scan.</summary>
    public DuckRunOptionsBuilder AddJobsFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!_assemblies.Contains(assembly)) _assemblies.Add(assembly);
        return this;
    }

    /// <summary>Adds the assembly that declares <typeparamref name="T"/> to the discovery scan.</summary>
    public DuckRunOptionsBuilder AddJobsFromAssemblyContaining<T>() =>
        AddJobsFromAssembly(typeof(T).Assembly);

    /// <summary>Registers a job explicitly. Use when reflection-based discovery is not wanted.</summary>
    public DuckRunOptionsBuilder AddJob(JobDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _explicitJobs.Add(descriptor);
        return this;
    }

    /// <summary>Enables the embedded standalone dashboard. Call <c>app.MapDuckRunDashboard()</c> to mount it.</summary>
    public DuckRunOptionsBuilder UseStandaloneDashboard(string path = "/duckrun")
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
            throw new ArgumentException("Path must start with '/'.", nameof(path));
        StandaloneDashboardEnabled = true;
        StandaloneDashboardPath = path.TrimEnd('/');
        if (StandaloneDashboardPath.Length == 0) StandaloneDashboardPath = "/duckrun";
        return this;
    }

    /// <summary>Connects this runtime to a centralized Control Dashboard via DSN. Implemented in a later phase.</summary>
    public DuckRunOptionsBuilder UseDashboard(string dsn)
    {
        if (string.IsNullOrWhiteSpace(dsn)) throw new ArgumentException("DSN is required.", nameof(dsn));
        DashboardDsn = dsn;
        return this;
    }

    public DuckRunOptionsBuilder ConsoleBufferSize(int entriesPerRun)
    {
        if (entriesPerRun < 1) throw new ArgumentOutOfRangeException(nameof(entriesPerRun));
        ConsoleEntriesPerRun = entriesPerRun;
        return this;
    }

    public DuckRunOptionsBuilder RunHistorySize(int runsPerJob)
    {
        if (runsPerJob < 1) throw new ArgumentOutOfRangeException(nameof(runsPerJob));
        RunsRetainedPerJob = runsPerJob;
        return this;
    }

    internal DuckRunOptions Build() => new()
    {
        AssembliesToScan = _assemblies.ToArray(),
        ExplicitJobs = _explicitJobs.ToArray(),
        StandaloneDashboardEnabled = StandaloneDashboardEnabled,
        StandaloneDashboardPath = StandaloneDashboardPath,
        ConsoleEntriesPerRun = ConsoleEntriesPerRun,
        RunsRetainedPerJob = RunsRetainedPerJob,
        DashboardDsn = DashboardDsn,
    };
}
