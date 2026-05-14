using Microsoft.AspNetCore.Builder;

namespace DuckRun.Core.Hosting;

internal sealed class NoOpConventionBuilder : IEndpointConventionBuilder
{
    public void Add(Action<EndpointBuilder> convention) { }
    public void Finally(Action<EndpointBuilder> finalConvention) { }
}
