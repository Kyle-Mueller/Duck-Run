namespace DuckRun.Core.Cluster;

/// <summary>
/// Cluster coordination surface. The default implementation is single-node (always leader, no distributed slots);
/// DuckRun.Redis swaps in an implementation that talks to Redis for leader election and cluster-wide concurrency.
/// </summary>
internal interface IClusterCoordinator
{
    /// <summary>Stable id for the project this node belongs to. Distinct DuckRun deployments must use distinct project ids.</summary>
    string ProjectId { get; }

    /// <summary>Stable id for this node within the project. Generated per process; new id on restart.</summary>
    string NodeId { get; }

    /// <summary>True if this node currently holds cluster leadership. Followers must not fire cron-driven runs.</summary>
    Task<bool> IsLeaderAsync(CancellationToken ct = default);

    /// <summary>Reserve a cluster-wide concurrency slot for a job. Returns a disposable that releases the slot,
    /// or null if the cluster is already at the configured cap.</summary>
    Task<IAsyncDisposable?> TryAcquireSlotAsync(string jobName, int maxConcurrency, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Snapshot of currently-alive nodes for the project, leader marked.</summary>
    Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken ct = default);
}
