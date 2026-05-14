namespace DuckRun.Dashboard.Database;

internal sealed class JobDefinition
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string Cron { get; set; } = "";
    public int MaxConcurrency { get; set; } = 1;
    public int TimeoutSeconds { get; set; }
    public bool AllowManualTrigger { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}
