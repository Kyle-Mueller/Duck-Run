namespace DuckRun.EfCore.Hosting;

/// <summary>
/// Resolved configuration for the DuckRun.EfCore module.
/// </summary>
public sealed class DuckRunEfCoreOptions
{
    public required DuckRunProvider Provider { get; init; }
    public required string ConnectionString { get; init; }
    public TimeSpan ConsoleFlushInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public int ConsoleFlushBatchSize { get; init; } = 100;
}
