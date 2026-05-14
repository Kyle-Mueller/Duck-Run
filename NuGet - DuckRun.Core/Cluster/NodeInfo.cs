namespace DuckRun.Core;

/// <summary>
/// A single DuckRun process participating in a cluster (or a single-node deployment).
/// </summary>
public sealed record NodeInfo(
    string NodeId,
    DateTimeOffset StartedAt,
    DateTimeOffset LastSeen,
    bool IsLeader,
    string? Version = null);
