namespace DuckRun.Core.Cluster;

/// <summary>
/// Single-node coordinator: this process is always the leader and there is no distributed concurrency.
/// Used when DuckRun.Redis is not configured.
/// </summary>
internal sealed class LocalClusterCoordinator : IClusterCoordinator
{
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public string ProjectId => "local";
    public string NodeId { get; } = Guid.NewGuid().ToString("N");

    public Task<bool> IsLeaderAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<IAsyncDisposable?> TryAcquireSlotAsync(string jobName, int maxConcurrency, TimeSpan ttl, CancellationToken ct = default)
        => Task.FromResult<IAsyncDisposable?>(NoOpSlot.Instance);

    public Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<NodeInfo>>([new NodeInfo(NodeId, _startedAt, DateTimeOffset.UtcNow, IsLeader: true)]);

    private sealed class NoOpSlot : IAsyncDisposable
    {
        public static readonly NoOpSlot Instance = new();
        public ValueTask DisposeAsync() => default;
    }
}
