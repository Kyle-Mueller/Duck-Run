using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DuckRun.Redis.Cluster;

internal sealed class RedisSlot(IDatabase db, RedisKey key, RedisValue slotId, ILogger logger) : IAsyncDisposable
{
    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            await db.SortedSetRemoveAsync(key, slotId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Releasing DuckRun Redis slot {Slot} failed; relying on TTL expiry.", slotId);
        }
    }
}
