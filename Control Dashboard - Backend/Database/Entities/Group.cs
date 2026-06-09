namespace DuckRun.Dashboard.Database;

internal sealed class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public Guid? ParentGroupId { get; set; }
    public string FullPath { get; set; } = "";
    public int Depth { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}
