namespace DuckRun.Dashboard.Database;

internal sealed class ProjectMember
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Viewer";
    public DateTime AddedAt { get; set; }
}
