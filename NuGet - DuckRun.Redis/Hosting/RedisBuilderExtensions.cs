using DuckRun.Core.Cluster;
using DuckRun.Redis.Cluster;
using DuckRun.Redis.Hosting;

#if NETFRAMEWORK
using DuckRun.Framework;
using Microsoft.Extensions.Logging.Abstractions;
#else
using DuckRun.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#endif

namespace DuckRun.Redis;

public static class RedisBuilderExtensions
{
    /// <summary>
    /// Enables Redis-backed cluster coordination: leader election (only the leader fires cron ticks) and
    /// distributed concurrency limits (cluster-wide enforcement of <c>[DuckRunJob(MaxConcurrency = N)]</c>).
    /// </summary>
    /// <param name="builder">The DuckRun options builder.</param>
    /// <param name="connectionString">StackExchange.Redis connection string.</param>
    /// <param name="projectId">Optional project id. Distinct deployments sharing the same Redis must use distinct ids.</param>
    /// <param name="environment">Optional environment tag mixed into Redis keys to isolate dev/staging/prod
    /// of the same project. Defaults to <c>ASPNETCORE_ENVIRONMENT</c>/<c>DOTNET_ENVIRONMENT</c> or <c>"Production"</c>.</param>
    public static DuckRunOptionsBuilder UseRedis(this DuckRunOptionsBuilder builder, string connectionString, string? projectId = null, string? environment = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Redis connection string is required.", nameof(connectionString));

        var defaults = new DuckRunRedisOptions { ConnectionString = "_" };
        var options = new DuckRunRedisOptions
        {
            ConnectionString = connectionString,
            ProjectId = projectId ?? defaults.ProjectId,
            Environment = environment ?? defaults.Environment,
        };
        return UseRedis(builder, options);
    }

    /// <summary>Enables Redis-backed cluster coordination with the supplied options.</summary>
    public static DuckRunOptionsBuilder UseRedis(this DuckRunOptionsBuilder builder, DuckRunRedisOptions options)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (options is null) throw new ArgumentNullException(nameof(options));

#if NETFRAMEWORK
        builder.AddModuleSetup(ctx =>
        {
            var coordinator = new RedisClusterCoordinator(options, NullLogger<RedisClusterCoordinator>.Instance);
            ctx.Coordinator = coordinator;
            ctx.AddHostedService(coordinator);
        });
#else
        builder.AddModuleSetup(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<RedisClusterCoordinator>();
            services.Replace(ServiceDescriptor.Singleton<IClusterCoordinator>(sp => sp.GetRequiredService<RedisClusterCoordinator>()));
            services.AddHostedService(sp => sp.GetRequiredService<RedisClusterCoordinator>());
        });
#endif
        return builder;
    }
}
