using DuckRun.EfCore.Ef6;
using DuckRun.EfCore.Hosting;
using DuckRun.Framework;
using Microsoft.Extensions.Logging.Abstractions;

namespace DuckRun.EfCore;

/// <summary>
/// net48 (.NET Framework / EF6) counterpart to the EF Core <c>UseEfCore</c>. Same surface: persists
/// DuckRun job history and console logs to a relational database via Entity Framework 6.
/// </summary>
public static class Ef6BuilderExtensions
{
    /// <summary>Persists DuckRun job history and console logs via Entity Framework 6.</summary>
    public static DuckRunOptionsBuilder UseEfCore(this DuckRunOptionsBuilder builder, string connectionString, DuckRunProvider provider)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required.", nameof(connectionString));
        return UseEfCore(builder, new DuckRunEfCoreOptions { Provider = provider, ConnectionString = connectionString });
    }

    /// <summary>Persists DuckRun data via Entity Framework 6 using the supplied options.</summary>
    public static DuckRunOptionsBuilder UseEfCore(this DuckRunOptionsBuilder builder, DuckRunEfCoreOptions options)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (options is null) throw new ArgumentNullException(nameof(options));

        builder.AddModuleSetup(ctx =>
        {
            var useSchema = Ef6ConnectionFactory.SupportsSchema(options.Provider);
            Func<Ef6DuckRunDbContext> factory = () => new Ef6DuckRunDbContext(Ef6ConnectionFactory.Create(options.Provider, options.ConnectionString), useSchema);

            var console = new Ef6ConsoleStore(factory, options, NullLogger.Instance);

            ctx.RunStoreOverride = new Ef6JobRunStore(factory);
            ctx.ConsoleStoreOverride = console;
            // Bootstrap first so the tables exist before the console flush loop or any job run touches them.
            ctx.AddHostedService(new Ef6SchemaBootstrap(factory, options.Provider, useSchema, NullLogger.Instance));
            ctx.AddHostedService(console);
        });
        return builder;
    }
}
