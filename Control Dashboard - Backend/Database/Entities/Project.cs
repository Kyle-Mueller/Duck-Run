namespace DuckRun.Dashboard.Database;

internal sealed class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}
