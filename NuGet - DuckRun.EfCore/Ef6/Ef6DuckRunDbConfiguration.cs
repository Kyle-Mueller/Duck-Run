using MySql.Data.EntityFramework;

namespace DuckRun.EfCore.Ef6;

/// <summary>
/// EF6 provider registration for DuckRun's net48 persistence. Inherits the MySQL provider services
/// from <see cref="MySqlEFConfiguration"/> and adds the Npgsql (PostgreSQL/CockroachDB) provider.
/// SQL Server is EF6's built-in default and needs no registration. Applied to the context via
/// <c>[DbConfigurationType]</c>.
/// </summary>
internal sealed class Ef6DuckRunDbConfiguration : MySqlEFConfiguration
{
    public Ef6DuckRunDbConfiguration()
    {
        SetProviderServices("Npgsql", global::Npgsql.NpgsqlServices.Instance);
        SetProviderFactory("Npgsql", global::Npgsql.NpgsqlFactory.Instance);
    }
}
