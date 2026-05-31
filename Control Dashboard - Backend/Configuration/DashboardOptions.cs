using DuckRun.EfCore;

namespace DuckRun.Dashboard.Configuration;

/// <summary>
/// Top-level dashboard configuration, populated from <c>appsettings.json</c> + env vars
/// under the <c>DuckRun</c> section.
/// </summary>
public sealed class DashboardOptions
{
    public const string SectionName = "DuckRun";

    public DbOptions Db { get; set; } = new();
    public AuthOptions Auth { get; set; } = new();
    public string DashboardSecret { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public RetentionOptions Retention { get; set; } = new();
}

public sealed class DbOptions
{
    public DuckRunProvider Provider { get; set; } = DuckRunProvider.Postgres;
    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=duckrun_dashboard;Username=postgres;Password=postgres";
}

public sealed class AuthOptions
{
    public string? InitialAdminEmail { get; set; }
    public string? InitialAdminPassword { get; set; }
}

public sealed class RetentionOptions
{
    public int RunDays { get; set; } = 30;
    public int ConsoleDays { get; set; } = 7;
}
