namespace DuckRun.Redis.Cluster;

internal static class RedisLuaScripts
{
    /// <summary>
    /// Acquire or refresh leadership for the calling node, atomically.
    /// KEYS[1] = leader key, ARGV[1] = nodeId, ARGV[2] = ttl seconds.
    /// Returns 1 if this node now holds (or still holds) leadership, 0 otherwise.
    /// </summary>
    public const string AcquireOrRefreshLeader = @"
local owner = redis.call('GET', KEYS[1])
if owner == ARGV[1] then
    redis.call('EXPIRE', KEYS[1], ARGV[2])
    return 1
end
if owner == false then
    local ok = redis.call('SET', KEYS[1], ARGV[1], 'NX', 'EX', ARGV[2])
    if ok then return 1 end
end
return 0";

    /// <summary>
    /// Try to reserve a cluster-wide concurrency slot for a job.
    /// KEYS[1] = slot sorted-set key, ARGV[1] = now epoch seconds, ARGV[2] = max concurrency,
    /// ARGV[3] = slot ttl seconds, ARGV[4] = slot id.
    /// Returns 1 if reserved, 0 if at capacity.
    /// </summary>
    public const string AcquireSlot = @"
redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[1])
local current = redis.call('ZCARD', KEYS[1])
if tonumber(current) < tonumber(ARGV[2]) then
    redis.call('ZADD', KEYS[1], tonumber(ARGV[1]) + tonumber(ARGV[3]), ARGV[4])
    redis.call('EXPIRE', KEYS[1], tonumber(ARGV[3]) + 60)
    return 1
end
return 0";

    /// <summary>
    /// Best-effort release of leadership: only deletes if we still own it.
    /// KEYS[1] = leader key, ARGV[1] = nodeId. Returns 1 if released, 0 otherwise.
    /// </summary>
    public const string ReleaseLeader = @"
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
end
return 0";
}
