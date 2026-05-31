using Microsoft.AspNetCore.Builder;

namespace DuckRun.Core.Hosting;

/// <summary>
/// Fans an endpoint convention out to several underlying builders. Lets the dashboard map its routes
/// individually (so it works on net6, which has no <c>MapGroup</c>) while still returning a single
/// <see cref="IEndpointConventionBuilder"/> the caller can apply conventions to.
/// </summary>
internal sealed class CompositeConventionBuilder(IReadOnlyList<IEndpointConventionBuilder> inner) : IEndpointConventionBuilder
{
    public void Add(Action<EndpointBuilder> convention)
    {
        foreach (var b in inner) b.Add(convention);
    }

#if NET7_0_OR_GREATER
    // IEndpointConventionBuilder.Finally was added in .NET 7; net6's interface has only Add.
    public void Finally(Action<EndpointBuilder> finalConvention)
    {
        foreach (var b in inner) b.Finally(finalConvention);
    }
#endif
}
