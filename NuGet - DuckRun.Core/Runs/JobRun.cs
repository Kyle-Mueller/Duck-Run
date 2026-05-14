namespace DuckRun.Core;

/// <summary>
/// One invocation of a job. Mutable while the run is in flight; treated as immutable once <see cref="FinishedAt"/> is set.
/// </summary>
public sealed class JobRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string JobName { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public JobRunState State { get; set; } = JobRunState.Pending;
    public string TriggerSource { get; init; } = "Cron";
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }

    public TimeSpan? Duration => StartedAt is { } s && FinishedAt is { } f ? f - s : null;
}
