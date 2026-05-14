using DuckRun.Core;
using DuckRun.Core.Logging;
using DuckRun.Core.Runs;
using DuckRun.EfCore.Bootstrap;
using DuckRun.EfCore.Database;
using DuckRun.EfCore.Hosting;
using DuckRun.EfCore.Providers;
using DuckRun.EfCore.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DuckRun.EfCore;

public static class EfCoreBuilderExtensions
{
    /// <summary>
    /// Persists DuckRun job history, console logs, and errors to a relational database.
    /// </summary>
    /// <param name="builder">The DuckRun options builder.</param>
    /// <param name="connectionString">Provider-specific ADO.NET connection string.</param>
    /// <param name="provider">Database engine. See <see cref="DuckRunProvider"/>.</param>
    public static DuckRunOptionsBuilder UseEfCore(this DuckRunOptionsBuilder builder, string connectionString, DuckRunProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required.", nameof(connectionString));

        var options = new DuckRunEfCoreOptions
        {
            Provider = provider,
            ConnectionString = connectionString,
        };

        return UseEfCore(builder, options);
    }

    /// <summary>
    /// Persists DuckRun data via EF Core using the supplied options.
    /// </summary>
    public static DuckRunOptionsBuilder UseEfCore(this DuckRunOptionsBuilder builder, DuckRunEfCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.AddModuleSetup(services => RegisterEfCore(services, options));
        return builder;
    }

    private static void RegisterEfCore(IServiceCollection services, DuckRunEfCoreOptions options)
    {
        services.AddSingleton(options);

        services.AddDbContextFactory<DuckRunDbContext>(dbOpts => ProviderConfigurator.Configure(dbOpts, options.Provider, options.ConnectionString));

        services.Replace(ServiceDescriptor.Singleton<IJobRunStore, EfCoreJobRunStore>());

        services.AddSingleton<EfCoreConsoleStore>();
        services.Replace(ServiceDescriptor.Singleton<IConsoleStore>(sp => sp.GetRequiredService<EfCoreConsoleStore>()));

        services.AddHostedService<SchemaBootstrap>();
    }
}
