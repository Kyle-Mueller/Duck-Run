namespace DuckRun.Dashboard.Database;

internal sealed class NodeHeartbeat
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string NodeId { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsLeader { get; set; }
    public string Runtime { get; set; } = "";
    public string ClientVersion { get; set; } = "";
}
