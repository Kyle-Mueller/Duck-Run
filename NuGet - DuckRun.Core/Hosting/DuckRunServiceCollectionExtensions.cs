using DuckRun.Core;
using DuckRun.Core.Internal.Control;
using DuckRun.Core.Internal.Discovery;
using DuckRun.Core.Internal.Execution;
using DuckRun.Core.Internal.Logging;
using DuckRun.Core.Internal.Scheduling;
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
        services.AddSingleton<InMemoryConsoleStore>();
        services.AddSingleton<IJobRunStore, InMemoryJobRunStore>();
        services.AddSingleton<IJobRegistry>(_ => JobScanner.Build(options));
        services.AddSingleton<JobExecutor>();
        services.AddSingleton<DuckRunSchedulerService>();
        services.AddHostedService(sp => sp.GetRequiredService<DuckRunSchedulerService>());
        services.AddScoped<IDuckRunConsole, ScopedDuckRunConsole>();
        services.AddSingleton<IDuckRunController, DuckRunController>();

        return services;
    }
}
