using System.Reflection;

namespace DuckRun.Core;

/// <summary>
/// Resolved metadata for a single job. Built from <see cref="DuckRunJobAttribute"/> on discovery.
/// </summary>
public sealed class JobDescriptor
{
    public required string Name { get; init; }
    public required string Cron { get; init; }
    public required Type DeclaringType { get; init; }
    public required MethodInfo Method { get; init; }
    public int MaxConcurrency { get; init; } = 1;
    public TimeSpan? Timeout { get; init; }
    public bool AllowManualTrigger { get; init; } = true;
    public bool Enabled { get; init; } = true;
}
