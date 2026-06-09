using System.Data.Entity;
using System.Threading.Channels;
using DuckRun.Core;
using DuckRun.Core.Logging;
using DuckRun.EfCore.Database;
using DuckRun.EfCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.EfCore.Ef6;

/// <summary>
/// EF6-backed console store for net48. <see cref="Append"/> buffers to an in-memory channel; a background
/// loop (started/stopped via the host lifecycle) drains it into the database in batches. Reads hit the DB.
/// </summary>
internal sealed class Ef6ConsoleStore(
    Func<Ef6DuckRunDbContext> contextFactory,
    DuckRunEfCoreOptions options,
    ILogger logger) : IConsoleStore, IHostedService
{
    private readonly Channel<ConsoleLogEntry> _channel = Channel.CreateUnbounded<ConsoleLogEntry>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource _stopCts = new();
    private Task? _flushTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _flushTask = Task.Run(FlushLoopAsync);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        try { _stopCts.Cancel(); } catch (ObjectDisposedException) { }
        if (_flushTask is not null)
        {
            try { await _flushTask; }
            catch (Exception ex) { logger.LogWarning(ex, "DuckRun console flush task did not complete cleanly on stop."); }
        }
        try { _stopCts.Dispose(); } catch (ObjectDisposedException) { }
    }

    public void Append(ConsoleLogEntry entry)
    {
        if (!_channel.Writer.TryWrite(entry))
            logger.LogWarning("DuckRun console buffer write failed for run {RunId}.", entry.RunId);
    }

    public async Task<IReadOnlyList<ConsoleLogEntry>> GetForRunAsync(Guid runId, CancellationToken ct = default)
    {
        using var ctx = contextFactory();
        var records = await ctx.ConsoleLogs
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        return records.ConvertAll(r => new ConsoleLogEntry(
            r.RunId,
            new DateTimeOffset(DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Utc)),
            Enum.TryParse<DuckRunLogLevel>(r.Level, out var lvl) ? lvl : DuckRunLogLevel.Info,
            r.Message));
    }

    private async Task FlushLoopAsync()
    {
        var batch = new List<ConsoleLogEntry>(options.ConsoleFlushBatchSize);
        try
        {
            while (!_stopCts.IsCancellationRequested)
            {
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(_stopCts.Token);
                waitCts.CancelAfter(options.ConsoleFlushInterval);

                try
                {
                    if (await _channel.Reader.WaitToReadAsync(waitCts.Token))
                        DrainAvailable(batch);
                }
                catch (OperationCanceledException) when (waitCts.IsCancellationRequested && !_stopCts.IsCancellationRequested)
                {
                    // interval elapsed without new entries
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
                catch (Exception ex) { logger.LogError(ex, "Final DuckRun console flush failed."); }
            }
        }
    }

    private void DrainAvailable(List<ConsoleLogEntry> batch)
    {
        while (batch.Count < options.ConsoleFlushBatchSize && _channel.Reader.TryRead(out var entry))
            batch.Add(entry);
    }

    private async Task FlushBatchAsync(IReadOnlyList<ConsoleLogEntry> batch)
    {
        try
        {
            using var ctx = contextFactory();
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
            logger.LogError(ex, "Flushing {Count} DuckRun console entries failed; entries dropped.", batch.Count);
        }
    }
}
