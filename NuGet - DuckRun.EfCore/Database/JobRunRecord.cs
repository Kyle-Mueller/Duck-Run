namespace DuckRun.EfCore.Database;

internal sealed class JobRunRecord
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string State { get; set; } = "Pending";
    public string TriggerSource { get; set; } = "Cron";
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
}
