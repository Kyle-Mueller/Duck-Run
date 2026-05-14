using DuckRun.EfCore.Providers;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.EfCore.Database;

internal sealed class DuckRunDbContext(DbContextOptions<DuckRunDbContext> options) : DbContext(options)
{
    public const string SchemaName = "DuckRun";

    public DbSet<JobRunRecord> JobRuns => Set<JobRunRecord>();
    public DbSet<ConsoleLogRecord> ConsoleLogs => Set<ConsoleLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var supportsSchema = ProviderConfigurator.SupportsSchema(Database.ProviderName);

        if (supportsSchema)
        {
            modelBuilder.HasDefaultSchema(SchemaName);
            modelBuilder.Entity<JobRunRecord>().ToTable("JobRun");
            modelBuilder.Entity<ConsoleLogRecord>().ToTable("ConsoleLog");
        }
        else
        {
            modelBuilder.Entity<JobRunRecord>().ToTable("DuckRun_JobRun");
            modelBuilder.Entity<ConsoleLogRecord>().ToTable("DuckRun_ConsoleLog");
        }

        modelBuilder.Entity<JobRunRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.JobName).HasMaxLength(200).IsRequired();
            e.Property(x => x.State).HasMaxLength(40).IsRequired();
            e.Property(x => x.TriggerSource).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.JobName);
            e.HasIndex(x => new { x.JobName, x.CreatedAt });
        });

        modelBuilder.Entity<ConsoleLogRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Level).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.RunId);
        });
    }
}
