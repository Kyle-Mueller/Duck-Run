namespace DuckRun.Dashboard.Database;

internal sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSignInAt { get; set; }
}
