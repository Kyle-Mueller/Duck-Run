namespace DuckRun.Dashboard.Database;

internal sealed class JobRun
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string JobName { get; set; } = "";
    public string NodeId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string State { get; set; } = "Pending";
    public string TriggerSource { get; set; } = "Cron";
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }

    /// <summary>When this dashboard observed/received the latest update for this run.</summary>
    public DateTime ReceivedAt { get; set; }
}
