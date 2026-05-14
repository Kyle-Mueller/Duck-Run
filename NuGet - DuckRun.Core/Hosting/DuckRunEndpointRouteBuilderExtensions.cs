using DuckRun.Core;
using DuckRun.Core.Dashboard;
using DuckRun.Core.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

public static class DuckRunEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Mounts the embedded standalone dashboard at the configured path (default <c>/duckrun</c>).
    /// No-op if the standalone dashboard was not enabled via <c>UseStandaloneDashboard()</c>.
    /// </summary>
    public static IEndpointConventionBuilder MapDuckRunDashboard(this IEndpointRouteBuilder endpoints, string? pathPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<DuckRunOptions>();
        if (!options.StandaloneDashboardEnabled) return new NoOpConventionBuilder();

        var prefix = pathPrefix ?? options.StandaloneDashboardPath;
        return DashboardEndpoints.Map(endpoints, prefix);
    }
}
