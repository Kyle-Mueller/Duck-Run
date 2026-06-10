using System.Data.Entity;
using DuckRun.Core;
using DuckRun.Core.Runs;
using DuckRun.EfCore.Database;

namespace DuckRun.EfCore.Ef6;

/// <summary>EF6-backed <see cref="IJobRunStore"/> for net48. A fresh context (and connection) per operation.</summary>
internal sealed class Ef6JobRunStore(Func<Ef6DuckRunDbContext> contextFactory) : IJobRunStore
{
    public async Task AddAsync(JobRun run, CancellationToken ct)
    {
        using var ctx = contextFactory();
        ctx.JobRuns.Add(ToRecord(run));
        await ctx.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(JobRun run, CancellationToken ct)
    {
        using var ctx = contextFactory();
        var existing = await ctx.JobRuns.FindAsync(ct, run.Id);
        if (existing is null) ctx.JobRuns.Add(ToRecord(run));
        else ApplyUpdate(existing, run);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<JobRun?> GetAsync(Guid runId, CancellationToken ct)
    {
        using var ctx = contextFactory();
        var rec = await ctx.JobRuns.FindAsync(ct, runId);
        return rec is null ? null : FromRecord(rec);
    }

    public async Task<IReadOnlyList<JobRun>> GetRecentForJobAsync(string jobName, int take, CancellationToken ct)
    {
        using var ctx = contextFactory();
        var records = await ctx.JobRuns
            .Where(r => r.JobName == jobName)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
        return records.ConvertAll(FromRecord);
    }

    public async Task<int> CountInFlightAsync(string jobName, CancellationToken ct)
    {
        using var ctx = contextFactory();
        var runningName = JobRunState.Running.ToString();
        return await ctx.JobRuns.CountAsync(r => r.JobName == jobName && r.State == runningName, ct);
    }

    public async Task<IReadOnlyList<JobRun>> GetRunsSinceAsync(DateTimeOffset since, int max, CancellationToken ct)
    {
        using var ctx = contextFactory();
        var sinceUtc = since.UtcDateTime;
        var records = await ctx.JobRuns.Where(r => r.CreatedAt >= sinceUtc)
                                       .OrderByDescending(r => r.CreatedAt)
                                       .Take(max)
                                       .ToListAsync(ct);
        return records.ConvertAll(FromRecord);
    }

    private static JobRunRecord ToRecord(JobRun run) => new()
    {
        Id = run.Id,
        JobName = run.JobName,
        CreatedAt = run.CreatedAt.UtcDateTime,
        StartedAt = run.StartedAt?.UtcDateTime,
        FinishedAt = run.FinishedAt?.UtcDateTime,
        State = run.State.ToString(),
        TriggerSource = run.TriggerSource,
        ErrorMessage = run.ErrorMessage,
        ErrorStackTrace = run.ErrorStackTrace,
    };

    private static void ApplyUpdate(JobRunRecord rec, JobRun run)
    {
        rec.JobName = run.JobName;
        rec.CreatedAt = run.CreatedAt.UtcDateTime;
        rec.StartedAt = run.StartedAt?.UtcDateTime;
        rec.FinishedAt = run.FinishedAt?.UtcDateTime;
        rec.State = run.State.ToString();
        rec.TriggerSource = run.TriggerSource;
        rec.ErrorMessage = run.ErrorMessage;
        rec.ErrorStackTrace = run.ErrorStackTrace;
    }

    private static JobRun FromRecord(JobRunRecord rec) => new()
    {
        Id = rec.Id,
        JobName = rec.JobName,
        CreatedAt = ToOffset(rec.CreatedAt),
        StartedAt = rec.StartedAt is { } s ? ToOffset(s) : null,
        FinishedAt = rec.FinishedAt is { } f ? ToOffset(f) : null,
        State = Enum.TryParse<JobRunState>(rec.State, out var st) ? st : JobRunState.Pending,
        TriggerSource = rec.TriggerSource,
        ErrorMessage = rec.ErrorMessage,
        ErrorStackTrace = rec.ErrorStackTrace,
    };

    private static DateTimeOffset ToOffset(DateTime dt) => new(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
