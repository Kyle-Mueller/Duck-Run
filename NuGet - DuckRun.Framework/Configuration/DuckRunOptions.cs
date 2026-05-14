using System.Reflection;
using DuckRun.Core;

namespace DuckRun.Framework;

/// <summary>
/// Resolved configuration for a DuckRun.Framework host. Built by <see cref="DuckRunOptionsBuilder"/>.
/// </summary>
public sealed class DuckRunOptions
{
    public IReadOnlyList<Assembly> AssembliesToScan { get; init; } = Array.Empty<Assembly>();
    public IReadOnlyList<JobDescriptor> ExplicitJobs { get; init; } = Array.Empty<JobDescriptor>();

    /// <summary>Maximum console log entries retained per run in the in-memory store.</summary>
    public int ConsoleEntriesPerRun { get; init; } = 1000;

    /// <summary>Maximum runs retained per job in the in-memory run store.</summary>
    public int RunsRetainedPerJob { get; init; } = 200;

    /// <summary>
    /// Factory invoked to construct each job's declaring type per run. Defaults to <see cref="Activator.CreateInstance(Type)"/>,
    /// which requires a parameterless constructor. Override with <see cref="DuckRunOptionsBuilder.UseJobFactory"/>
    /// to plug in Autofac / Unity / Ninject etc.
    /// </summary>
    public Func<Type, object> JobFactory { get; init; } = static t => Activator.CreateInstance(t) ?? throw new InvalidOperationException($"Activator.CreateInstance returned null for {t.FullName}.");

    /// <summary>DSN for a centralized Control Dashboard. Wiring is a follow-up phase on net48.</summary>
    public string? DashboardDsn { get; init; }

    internal IReadOnlyList<Action<DuckRunFrameworkContext>> ModuleSetups { get; init; } = Array.Empty<Action<DuckRunFrameworkContext>>();
}
