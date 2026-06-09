namespace DuckRun.Dashboard.Database;

internal sealed class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public Guid? GroupId { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}
