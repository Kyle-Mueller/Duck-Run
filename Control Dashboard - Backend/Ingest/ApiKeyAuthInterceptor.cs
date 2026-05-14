using DuckRun.Dashboard.Database;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.Ingest;

/// <summary>
/// gRPC interceptor that authenticates incoming ingest calls by their DSN public key.
/// Clients send the key in the <c>x-duckrun-key</c> metadata header. The resolved <c>ProjectId</c>
/// is placed into <c>ServerCallContext.UserState</c> under the key <see cref="ProjectIdKey"/>.
/// </summary>
internal sealed class ApiKeyAuthInterceptor(IDbContextFactory<DashboardDbContext> contextFactory) : Interceptor
{
    public const string MetadataKey = "x-duckrun-key";
    public const string ProjectIdKey = "duckrun.projectId";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var apiKey = await ResolveAsync(context);
        context.UserState[ProjectIdKey] = apiKey.ProjectId;
        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var apiKey = await ResolveAsync(context);
        context.UserState[ProjectIdKey] = apiKey.ProjectId;
        return await continuation(requestStream, context);
    }

    private async Task<ApiKey> ResolveAsync(ServerCallContext context)
    {
        var key = context.RequestHeaders.GetValue(MetadataKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new RpcException(new Status(StatusCode.Unauthenticated, $"Missing '{MetadataKey}' metadata."));

        await using var ctx = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var apiKey = await ctx.ApiKeys.SingleOrDefaultAsync(k => k.PublicKey == key && k.RevokedAt == null);
        if (apiKey is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or revoked DSN."));

        return apiKey;
    }
}
