namespace DuckRun.EfCore;

/// <summary>
/// Database engine used by DuckRun.EfCore.
/// </summary>
public enum DuckRunProvider
{
    /// <summary>Microsoft SQL Server 2017+ or Azure SQL.</summary>
    SqlServer,

    /// <summary>PostgreSQL 13+. Uses the Npgsql provider.</summary>
    Postgres,

    /// <summary>CockroachDB 23.1+. Uses the Npgsql provider (CockroachDB is wire-compatible).</summary>
    CockroachDb,

    /// <summary>SQLite. Recommended for local development and single-instance deployments.</summary>
    Sqlite,
}
