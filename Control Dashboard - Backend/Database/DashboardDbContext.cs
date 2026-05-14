using DuckRun.EfCore.Providers;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.Database;

internal sealed class DashboardDbContext(DbContextOptions<DashboardDbContext> options) : DbContext(options)
{
    public const string SchemaName = "DuckRunDash";

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<JobDefinition> JobDefinitions => Set<JobDefinition>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<ConsoleLog> ConsoleLogs => Set<ConsoleLog>();
    public DbSet<NodeHeartbeat> NodeHeartbeats => Set<NodeHeartbeat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var supportsSchema = ProviderConfigurator.SupportsSchema(Database.ProviderName);

        if (supportsSchema) modelBuilder.HasDefaultSchema(SchemaName);

        Table<User>(modelBuilder, supportsSchema, "User", "DuckRunDash_User", e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        Table<Project>(modelBuilder, supportsSchema, "Project", "DuckRunDash_Project", e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.OwnerId);
        });

        Table<ProjectMember>(modelBuilder, supportsSchema, "ProjectMember", "DuckRunDash_ProjectMember", e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
            e.Property(x => x.Role).HasMaxLength(40).IsRequired();
        });

        Table<ApiKey>(modelBuilder, supportsSchema, "ApiKey", "DuckRunDash_ApiKey", e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublicKey).IsUnique();
            e.HasIndex(x => x.ProjectId);
            e.Property(x => x.PublicKey).HasMaxLength(120).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200);
        });

        Table<JobDefinition>(modelBuilder, supportsSchema, "JobDefinition", "DuckRunDash_JobDefinition", e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Cron).HasMaxLength(120).IsRequired();
        });

        Table<JobRun>(modelBuilder, supportsSchema, "JobRun", "DuckRunDash_JobRun", e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProjectId, x.JobName, x.CreatedAt });
            e.HasIndex(x => x.ProjectId);
            e.Property(x => x.JobName).HasMaxLength(200).IsRequired();
            e.Property(x => x.State).HasMaxLength(40).IsRequired();
            e.Property(x => x.TriggerSource).HasMaxLength(40).IsRequired();
            e.Property(x => x.NodeId).HasMaxLength(100);
        });

        Table<ConsoleLog>(modelBuilder, supportsSchema, "ConsoleLog", "DuckRunDash_ConsoleLog", e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.ProjectId, x.RunId });
            e.Property(x => x.Level).HasMaxLength(20).IsRequired();
        });

        Table<NodeHeartbeat>(modelBuilder, supportsSchema, "NodeHeartbeat", "DuckRunDash_NodeHeartbeat", e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProjectId, x.NodeId }).IsUnique();
            e.Property(x => x.NodeId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Runtime).HasMaxLength(40);
            e.Property(x => x.ClientVersion).HasMaxLength(40);
        });
    }

    private static void Table<T>(ModelBuilder mb, bool supportsSchema, string schemaName, string prefixedName,
        Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T>> configure) where T : class
    {
        mb.Entity<T>(e =>
        {
            e.ToTable(supportsSchema ? schemaName : prefixedName);
            configure(e);
        });
    }
}
