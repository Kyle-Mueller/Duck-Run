using Microsoft.EntityFrameworkCore;

namespace DuckRun.EfCore.Providers;

public static class ProviderConfigurator
{
    public static void Configure(DbContextOptionsBuilder dbOpts, DuckRunProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DuckRunProvider.SqlServer:
                dbOpts.UseSqlServer(connectionString);
                break;
            case DuckRunProvider.Postgres:
            case DuckRunProvider.CockroachDb:
                dbOpts.UseNpgsql(connectionString);
                break;
            case DuckRunProvider.MySql:
                dbOpts.UseMySQL(connectionString);
                break;
            default:
                throw new NotSupportedException($"DuckRunProvider.{provider} is not supported by DuckRun.EfCore in this release. Supported: SqlServer, Postgres, CockroachDb, MySql.");
        }
    }

    // MySQL has no schemas, so DuckRun falls back to DuckRun_-prefixed tables there. SQL Server and
    // PostgreSQL/CockroachDB (Npgsql) both support a dedicated "DuckRun" schema.
    public static bool SupportsSchema(string? providerName) => !string.IsNullOrEmpty(providerName) && !providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase);
}
