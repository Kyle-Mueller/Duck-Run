namespace DuckRun.Dashboard.Database;

internal sealed class ApiKey
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>The token sent by the runtime as the bearer. Shown in DSNs. Long random string.</summary>
    public string PublicKey { get; set; } = "";

    public string Label { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
