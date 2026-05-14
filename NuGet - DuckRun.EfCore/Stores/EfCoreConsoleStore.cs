using System.Threading.Channels;
using DuckRun.Core;
using DuckRun.Core.Logging;
using DuckRun.EfCore.Database;
using DuckRun.EfCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DuckRun.EfCore.Stores;

/// <summary>
/// EF Core-backed console store. <see cref="Append"/> writes to an unbounded in-memory channel;
/// a background loop drains the channel and inserts batches into the database. Reads always hit the database,
/// so the last batch-interval worth of entries (default 250 ms) may be slightly delayed in queries.
/// </summary>
internal sealed class EfCoreConsoleStore : IConsoleStore, IDisposable
{
    private readonly IDbContextFactory<DuckRunDbContext> _contextFactory;
    private readonly DuckRunEfCoreOptions _options;
    private readonly ILogger<EfCoreConsoleStore> _logger;
    private readonly Channel<ConsoleLogEntry> _channel;
    private readonly Task _flushTask;
    private readonly CancellationTokenSource _stopCts = new();

    public EfCoreConsoleStore(
        IDbContextFactory<DuckRunDbContext> contextFactory,
        DuckRunEfCoreOptions options,
        ILogger<EfCoreConsoleStore> logger)
    {
        _contextFactory = contextFactory;
        _options = options;
        _logger = logger;
        _channel = Channel.CreateUnbounded<ConsoleLogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _flushTask = Task.Run(FlushLoopAsync);
    }

    public void Append(ConsoleLogEntry entry)
    {
        if (!_channel.Writer.TryWrite(entry))
            _logger.LogWarning("DuckRun console buffer write failed for run {RunId}.", entry.RunId);
    }

    public async Task<IReadOnlyList<ConsoleLogEntry>> GetForRunAsync(Guid runId, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var records = await ctx.ConsoleLogs
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        return records.Select(r => new ConsoleLogEntry(
            r.RunId,
            new DateTimeOffset(DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Utc)),
            Enum.TryParse<DuckRunLogLevel>(r.Level, out var lvl) ? lvl : DuckRunLogLevel.Info,
            r.Message)).ToArray();
    }

    public void Drop(Guid runId)
    {
        // No-op: durable rows stay in the DB. Retention is handled separately.
    }

    private async Task FlushLoopAsync()
    {
        var batch = new List<ConsoleLogEntry>(_options.ConsoleFlushBatchSize);
        try
        {
            while (!_stopCts.IsCancellationRequested)
            {
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(_stopCts.Token);
                waitCts.CancelAfter(_options.ConsoleFlushInterval);

                try
                {
                    if (await _channel.Reader.WaitToReadAsync(waitCts.Token))
                        DrainAvailable(batch);
                }
                catch (OperationCanceledException) when (waitCts.IsCancellationRequested && !_stopCts.IsCancellationRequested)
                {
                    // Interval elapsed without new entries.
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            DrainAvailable(batch);
            if (batch.Count > 0)
            {
                try { await FlushBatchAsync(batch); }
                catch (Exception ex) { _logger.LogError(ex, "Final DuckRun console flush failed."); }
            }
        }
    }

    private void DrainAvailable(List<ConsoleLogEntry> batch)
    {
        while (batch.Count < _options.ConsoleFlushBatchSize && _channel.Reader.TryRead(out var entry))
            batch.Add(entry);
    }

    private async Task FlushBatchAsync(IReadOnlyList<ConsoleLogEntry> batch)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            foreach (var entry in batch)
            {
                ctx.ConsoleLogs.Add(new ConsoleLogRecord
                {
                    RunId = entry.RunId,
                    Timestamp = entry.Timestamp.UtcDateTime,
                    Level = entry.Level.ToString(),
                    Message = entry.Message,
                });
            }
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flushing {Count} DuckRun console entries failed; entries dropped.", batch.Count);
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _stopCts.Cancel();
        try { _flushTask.Wait(TimeSpan.FromSeconds(10)); }
        catch (Exception ex) { _logger.LogWarning(ex, "DuckRun console flush task did not complete cleanly during dispose."); }
        _stopCts.Dispose();
    }
}
