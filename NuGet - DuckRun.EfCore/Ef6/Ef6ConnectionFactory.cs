using System.Data.Common;

namespace DuckRun.EfCore.Ef6;

/// <summary>
/// Builds the right ADO.NET connection for an EF6 DuckRun context from the configured provider.
/// EF6 selects its provider from the connection type, so this is what picks SQL Server vs Npgsql vs MySQL.
/// </summary>
internal static class Ef6ConnectionFactory
{
    public static DbConnection Create(DuckRunProvider provider, string connectionString) => provider switch
    {
        DuckRunProvider.SqlServer => new System.Data.SqlClient.SqlConnection(connectionString),
        DuckRunProvider.Postgres or DuckRunProvider.CockroachDb => new global::Npgsql.NpgsqlConnection(connectionString),
        DuckRunProvider.MySql => new global::MySql.Data.MySqlClient.MySqlConnection(connectionString),
        _ => throw new NotSupportedException($"DuckRunProvider.{provider} is not supported by DuckRun.EfCore on .NET Framework.")
    };

    // MySQL has no schemas → DuckRun_-prefixed tables; SQL Server and PostgreSQL/CockroachDB get a "DuckRun" schema.
    public static bool SupportsSchema(DuckRunProvider provider) => provider != DuckRunProvider.MySql;
}
