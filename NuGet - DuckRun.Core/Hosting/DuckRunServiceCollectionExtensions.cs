using DuckRun.Core;
using DuckRun.Core.Cluster;
using DuckRun.Core.Jobs;
using DuckRun.Core.Logging;
using DuckRun.Core.Operations;
using DuckRun.Core.Reporting;
using DuckRun.Core.Runs;
using DuckRun.Core.Scheduler;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DuckRunServiceCollectionExtensions
{
    /// <summary>
    /// Registers DuckRun with this service collection. Configure jobs, persistence, transport, and dashboard mode
    /// through the supplied <see cref="DuckRunOptionsBuilder"/>.
    /// </summary>
    public static IServiceCollection AddDuckRun(this IServiceCollection services, Action<DuckRunOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new DuckRunOptionsBuilder();
        configure(builder);
        var options = builder.Build();

        services.AddSingleton(options);
        services.AddSingleton<IConsoleStore>(_ => new InMemoryConsoleStore(options.ConsoleEntriesPerRun));
        services.AddSingleton<IJobRunStore>(_ => new InMemoryJobRunStore(options.RunsRetainedPerJob));
        services.AddSingleton<IJobRegistry>(_ => JobScanner.Build(options.AssembliesToScan, options.ExplicitJobs));
        services.AddSingleton<JobExecutor>();
        services.AddSingleton<LocalConcurrencyTrackers>();
        services.AddSingleton<JobSlotGate>();
        services.AddSingleton<IClusterCoordinator, LocalClusterCoordinator>();
        services.AddSingleton<IDashboardReporter, NullDashboardReporter>();
        services.AddSingleton<DuckRunSchedulerService>();
        services.AddHostedService(sp => sp.GetRequiredService<DuckRunSchedulerService>());
        services.AddScoped<IDuckRunConsole, ScopedDuckRunConsole>();
        services.AddSingleton<IDuckRunController, DuckRunController>();

        // Modules (EfCore, Redis, ...) run after defaults so they can override store registrations.
        foreach (var setup in builder.ModuleSetups)
            setup(services);

        return services;
    }
}
