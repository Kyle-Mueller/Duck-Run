using System.Reflection;
using DuckRun.Core;

namespace DuckRun.Framework;

public sealed class DuckRunOptionsBuilder
{
    private readonly List<Assembly> _assemblies = new();
    private readonly List<JobDescriptor> _explicitJobs = new();
    private readonly List<Action<DuckRunFrameworkContext>> _moduleSetups = new();

    public int ConsoleEntriesPerRun { get; private set; } = 1000;
    public int RunsRetainedPerJob { get; private set; } = 200;
    public Func<Type, object>? JobFactory { get; private set; }
    public string? DashboardDsn { get; private set; }

    /// <summary>Companion packages (DuckRun.Redis, ...) call this to install setup callbacks.
    /// Internal: only visible via <c>InternalsVisibleTo</c>.</summary>
    internal DuckRunOptionsBuilder AddModuleSetup(Action<DuckRunFrameworkContext> setup)
    {
        if (setup is null) throw new ArgumentNullException(nameof(setup));
        _moduleSetups.Add(setup);
        return this;
    }

    public DuckRunOptionsBuilder AddJobsFromAssembly(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
        if (!_assemblies.Contains(assembly)) _assemblies.Add(assembly);
        return this;
    }

    public DuckRunOptionsBuilder AddJobsFromAssemblyContaining<T>() => AddJobsFromAssembly(typeof(T).Assembly);

    public DuckRunOptionsBuilder AddJob(JobDescriptor descriptor)
    {
        if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));
        _explicitJobs.Add(descriptor);
        return this;
    }

    /// <summary>
    /// Tell DuckRun how to construct job classes. Use this to plug in your container of choice.
    /// </summary>
    public DuckRunOptionsBuilder UseJobFactory(Func<Type, object> factory)
    {
        JobFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>Connects this runtime to a centralized Control Dashboard via DSN. (Reporting on net48 is a follow-up phase.)</summary>
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
        ConsoleEntriesPerRun = ConsoleEntriesPerRun,
        RunsRetainedPerJob = RunsRetainedPerJob,
        JobFactory = JobFactory ?? (static t => Activator.CreateInstance(t) ?? throw new InvalidOperationException($"Activator.CreateInstance returned null for {t.FullName}.")),
        DashboardDsn = DashboardDsn,
        ModuleSetups = _moduleSetups.ToArray(),
    };
}
