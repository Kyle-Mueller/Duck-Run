namespace DuckRun.Dashboard.Database;

internal sealed class GroupMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Viewer";
    public DateTime AddedAt { get; set; }
}
