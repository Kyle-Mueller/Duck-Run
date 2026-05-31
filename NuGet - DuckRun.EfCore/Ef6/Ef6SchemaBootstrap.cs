using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using DuckRun.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.EfCore.Ef6;

/// <summary>
/// EF6 equivalent of the EF Core schema bootstrap: idempotently creates DuckRun's tables (even in an
/// existing, shared database) and replays runs interrupted by a previous process. Runs once on startup.
/// </summary>
internal sealed class Ef6SchemaBootstrap(
    Func<Ef6DuckRunDbContext> contextFactory,
    DuckRunProvider provider,
    bool useSchema,
    ILogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var ctx = contextFactory();

            if (!ctx.Database.Exists())
            {
                ctx.Database.Create();
                logger.LogInformation("DuckRun.EfCore (EF6) database and tables created.");
            }
            else if (!await TablesPresentAsync(ctx, cancellationToken))
            {
                await PreCreateSchemaAsync(ctx, cancellationToken);
                var script = ((IObjectContextAdapter)ctx).ObjectContext.CreateDatabaseScript();
                await ctx.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, script, cancellationToken);
                logger.LogInformation("DuckRun.EfCore (EF6) tables created in existing database.");
            }

            await ReplayInterruptedRunsAsync(ctx, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DuckRun.EfCore (EF6) schema bootstrap failed. Persistence will not work for this run.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> TablesPresentAsync(Ef6DuckRunDbContext ctx, CancellationToken ct)
    {
        var table = useSchema ? "JobRun" : "DuckRun_JobRun";
        var schema = useSchema ? Ef6DuckRunDbContext.SchemaName : null;

        var conn = ctx.Database.Connection;
        var openedHere = conn.State != ConnectionState.Open;
        if (openedHere) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = schema is null
                ? "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @t"
                : "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @t AND table_schema = @s";
            AddParam(cmd, "@t", table);
            if (schema is not null) AddParam(cmd, "@s", schema);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is not null && result is not DBNull && Convert.ToInt64(result) > 0;
        }
        finally
        {
            if (openedHere) conn.Close();
        }
    }

    private async Task PreCreateSchemaAsync(Ef6DuckRunDbContext ctx, CancellationToken ct)
    {
        if (!useSchema) return;
        var sql = provider switch
        {
            DuckRunProvider.SqlServer => $"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{Ef6DuckRunDbContext.SchemaName}') EXEC('CREATE SCHEMA [{Ef6DuckRunDbContext.SchemaName}]')",
            DuckRunProvider.Postgres or DuckRunProvider.CockroachDb => $"CREATE SCHEMA IF NOT EXISTS \"{Ef6DuckRunDbContext.SchemaName}\"",
            _ => null
        };
        if (sql is not null)
            await ctx.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, sql, ct);
    }

    private static async Task ReplayInterruptedRunsAsync(Ef6DuckRunDbContext ctx, CancellationToken ct)
    {
        var running = JobRunState.Running.ToString();
        var pending = JobRunState.Pending.ToString();
        var failed = JobRunState.Failed.ToString();

        var stuck = await ctx.JobRuns.Where(r => r.State == running || r.State == pending).ToListAsync(ct);
        if (stuck.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var r in stuck)
        {
            r.State = failed;
            r.ErrorMessage ??= "Run was interrupted by a host restart and marked failed on boot.";
            r.FinishedAt ??= now;
        }
        await ctx.SaveChangesAsync(ct);
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
