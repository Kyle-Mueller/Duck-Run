using System.Data;
using DuckRun.Core;
using DuckRun.EfCore.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.EfCore.Bootstrap;

/// <summary>
/// Hosted service that, on startup, idempotently creates the DuckRun tables (even when the target
/// database already exists and is shared with the host app) and replays interrupted runs.
/// Unlike <c>EnsureCreated</c> — which no-ops the moment the database exists — this creates the
/// DuckRun tables specifically, so adding DuckRun.EfCore to an app with an existing database works.
/// </summary>
internal sealed class SchemaBootstrap(IDbContextFactory<DuckRunDbContext> contextFactory, ILogger<SchemaBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
            var creator = ctx.GetService<IRelationalDatabaseCreator>();

            if (!await creator.ExistsAsync(cancellationToken))
            {
                try { await creator.CreateAsync(cancellationToken); }
                catch (Exception ex) when (IsAlreadyExists(ex)) { /* raced with another node */ }
            }

            if (!await DuckRunTablesPresentAsync(ctx, cancellationToken))
            {
                try
                {
                    await creator.CreateTablesAsync(cancellationToken);
                    logger.LogInformation("DuckRun.EfCore tables created (provider: {Provider}).", ctx.Database.ProviderName);
                }
                catch (Exception ex) when (IsAlreadyExists(ex)) { /* raced with another node */ }
            }
            else
            {
                logger.LogInformation("DuckRun.EfCore tables already present (provider: {Provider}).", ctx.Database.ProviderName);
            }

            await ReplayInterruptedRunsAsync(ctx, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DuckRun.EfCore schema bootstrap failed. Persistence will not work for this run.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> DuckRunTablesPresentAsync(DuckRunDbContext ctx, CancellationToken ct)
    {
        var entity = ctx.Model.FindEntityType(typeof(JobRunRecord));
        var table = entity?.GetTableName();
        if (string.IsNullOrEmpty(table)) return false;
        var schema = entity!.GetSchema();

        var conn = ctx.Database.GetDbConnection();
        var openedHere = conn.State != ConnectionState.Open;
        if (openedHere) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = schema is null
                ? "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @t"
                : "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @t AND table_schema = @s";
            AddParam(cmd, "@t", table!);
            if (schema is not null) AddParam(cmd, "@s", schema);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is not null && result is not DBNull && Convert.ToInt64(result) > 0;
        }
        finally
        {
            if (openedHere) await conn.CloseAsync();
        }
    }

    private static async Task ReplayInterruptedRunsAsync(DuckRunDbContext ctx, CancellationToken ct)
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

    // Duplicate-table / duplicate-schema / duplicate-database errors are benign here: another node
    // created it first, or it already existed. We key off the well-known provider error codes.
    private static bool IsAlreadyExists(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            var name = e.GetType().Name;
            var msg = e.Message;

            // SQL Server: 2714 (object exists), 1801 (db exists), 2759 (schema exists).
            if (name == "SqlException" && (msg.Contains("2714") || msg.Contains("already exists") || msg.Contains("There is already")))
                return true;
            // PostgreSQL / CockroachDB: 42P07 duplicate_table, 42P06 duplicate_schema, 42710 duplicate_object.
            if (name == "PostgresException" && (msg.Contains("42P07") || msg.Contains("42P06") || msg.Contains("42710") || msg.Contains("already exists")))
                return true;
            // MySQL: 1050 table exists, 1007 database exists.
            if (name == "MySqlException" && (msg.Contains("1050") || msg.Contains("1007") || msg.Contains("already exists")))
                return true;
            if (msg.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
