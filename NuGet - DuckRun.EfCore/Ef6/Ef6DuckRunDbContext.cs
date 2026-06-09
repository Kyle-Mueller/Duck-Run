using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using DuckRun.EfCore.Database;

namespace DuckRun.EfCore.Ef6;

/// <summary>
/// EF6 context backing DuckRun persistence on .NET Framework. Mirrors the EF Core model: a "DuckRun"
/// schema where the provider supports it, DuckRun_-prefixed tables otherwise (MySQL).
/// </summary>
[DbConfigurationType(typeof(Ef6DuckRunDbConfiguration))]
internal sealed class Ef6DuckRunDbContext : DbContext
{
    public const string SchemaName = "DuckRun";
    private readonly bool _useSchema;

    static Ef6DuckRunDbContext()
    {
        // DuckRun creates its tables explicitly in Ef6SchemaBootstrap; disable EF6's automatic initializer.
        System.Data.Entity.Database.SetInitializer<Ef6DuckRunDbContext>(null);
    }

    public Ef6DuckRunDbContext(DbConnection connection, bool useSchema) : base(connection, contextOwnsConnection: true)
    {
        _useSchema = useSchema;
    }

    public DbSet<JobRunRecord> JobRuns { get; set; } = null!;
    public DbSet<ConsoleLogRecord> ConsoleLogs { get; set; } = null!;

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

        if (_useSchema)
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

        var run = modelBuilder.Entity<JobRunRecord>();
        run.HasKey(x => x.Id);
        run.Property(x => x.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
        run.Property(x => x.JobName).HasMaxLength(200).IsRequired();
        run.Property(x => x.State).HasMaxLength(40).IsRequired();
        run.Property(x => x.TriggerSource).HasMaxLength(40).IsRequired();

        var log = modelBuilder.Entity<ConsoleLogRecord>();
        log.HasKey(x => x.Id);
        log.Property(x => x.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
        log.Property(x => x.Level).HasMaxLength(20).IsRequired();
    }
}
