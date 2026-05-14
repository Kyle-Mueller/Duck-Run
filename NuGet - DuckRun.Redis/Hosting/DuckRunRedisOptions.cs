namespace DuckRun.Redis.Hosting;

/// <summary>
/// Configuration for the DuckRun.Redis module.
/// </summary>
public sealed class DuckRunRedisOptions
{
    /// <summary>StackExchange.Redis connection string. Supports standalone, Sentinel, and Cluster modes.</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Project identifier. Distinct DuckRun deployments using the same Redis must use distinct ids,
    /// or they'll fight each other for leadership. Defaults to the entry assembly name.</summary>
    public string ProjectId { get; init; } = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "default";

    /// <summary>
    /// Free-text environment tag mixed into every Redis key prefix. Use this to isolate the same project
    /// running in dev/staging/prod against a shared Redis. Defaults to the <c>ASPNETCORE_ENVIRONMENT</c>
    /// or <c>DOTNET_ENVIRONMENT</c> env var, falling back to <c>"Production"</c> — matches ASP.NET Core's
    /// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.EnvironmentName"/> convention.
    /// </summary>
    public string Environment { get; init; } = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                                                                                                                   ?? "Production";

    /// <summary>How long a leader claim lasts before another node may steal it. Refreshed every <see cref="LeaderRefreshInterval"/>.</summary>
    public TimeSpan LeaderLeaseDuration { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>How often the leader refreshes its claim and followers try to acquire.</summary>
    public TimeSpan LeaderRefreshInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a node's heartbeat lasts. The dashboard treats a node as alive while its key is in Redis.</summary>
    public TimeSpan HeartbeatTtl { get; init; } = TimeSpan.FromSeconds(30);
}
