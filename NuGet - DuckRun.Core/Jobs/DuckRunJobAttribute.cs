namespace DuckRun.Core;

/// <summary>
/// Marks a method as a DuckRun background job. The method is invoked by the scheduler when the cron condition matches,
/// and can also be triggered manually from the dashboard or via <see cref="IDuckRunController"/>.
/// </summary>
/// <remarks>
/// The declaring type is resolved from the DI container per run, so constructor dependencies work as in any scoped service.
/// Method parameters are resolved from the same per-run scope; a parameter of type <see cref="System.Threading.CancellationToken"/>
/// receives a token that is signalled on host shutdown, manual cancel, or timeout.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DuckRunJobAttribute : Attribute
{
    /// <summary>Unique name for the job. Must be unique across all registered assemblies.</summary>
    public string Name { get; }

    /// <summary>Cron expression. 5-field (minute precision) or 6-field (with seconds). Parsed by Cronos.</summary>
    public string Cron { get; }

    /// <summary>Maximum simultaneous in-flight runs. Defaults to 1. Enforced locally; enforced cluster-wide when DuckRun.Redis is configured.</summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>Hard timeout in seconds. 0 disables the timeout. When exceeded the run's cancellation token is signalled and the run is marked TimedOut.</summary>
    public int TimeoutSeconds { get; init; } = 0;

    /// <summary>If false, the dashboard and IDuckRunController will refuse manual trigger requests for this job.</summary>
    public bool AllowManualTrigger { get; init; } = true;

    /// <summary>If false, the scheduler does not fire this job on cron. Manual triggers (when allowed) still work.</summary>
    public bool Enabled { get; init; } = true;

    public DuckRunJobAttribute(string name, string cron)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Job name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(cron)) throw new ArgumentException("Cron expression is required.", nameof(cron));
        Name = name;
        Cron = cron;
    }
}
