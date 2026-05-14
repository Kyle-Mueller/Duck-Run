using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DuckRun.Core.Reporting;

/// <summary>
/// gRPC client interceptor that attaches the DSN public key to every outbound call
/// as the <c>x-duckrun-key</c> metadata header.
/// </summary>
internal sealed class ApiKeyClientInterceptor(string publicKey) : Interceptor
{
    public const string MetadataKey = "x-duckrun-key";

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, WithKey(context));
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(WithKey(context));
    }

    private ClientInterceptorContext<TRequest, TResponse> WithKey<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class where TResponse : class
    {
        var headers = context.Options.Headers ?? new Metadata();
        if (headers.GetValue(MetadataKey) is null)
            headers.Add(MetadataKey, publicKey);
        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, context.Options.WithHeaders(headers));
    }
}
