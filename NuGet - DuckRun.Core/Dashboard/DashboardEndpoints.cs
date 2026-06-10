using System.Reflection;
using System.Text;
using DuckRun.Core.Hosting;
using DuckRun.Core.Jobs;
using DuckRun.Core.Runs;
using DuckRun.Core.Scheduler;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DuckRun.Core.Dashboard;

internal static class DashboardEndpoints
{
    private static readonly Assembly _asm = typeof(DashboardEndpoints).Assembly;
    private const string ResourceRoot = "DuckRun.Core.Dashboard.Assets.";

    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder endpoints, string prefix)
    {
        // net6 has no MapGroup, so map full-path routes individually and return one composite builder.
        // The index is mapped ONCE: ASP.NET Core routing collapses a trailing slash, so the "{prefix}"
        // route already serves both "/prefix" and "/prefix/" — also mapping "{prefix}/" makes both match
        // the same request and throws AmbiguousMatchException. ServeIndex injects a <base href="{prefix}/">
        // so the page's relative asset/api URLs resolve under the prefix regardless of the trailing slash.
        var builders = new List<IEndpointConventionBuilder>
        {
            endpoints.MapGet(prefix, () => ServeIndex(prefix)),
            endpoints.MapGet($"{prefix}/assets/{{**path}}", ServeAsset),
            endpoints.MapGet($"{prefix}/api/overview", GetOverview),
            endpoints.MapGet($"{prefix}/api/jobs", ListJobs),
            endpoints.MapGet($"{prefix}/api/jobs/{{name}}", GetJob),
            endpoints.MapGet($"{prefix}/api/jobs/{{name}}/runs", GetRuns),
            endpoints.MapGet($"{prefix}/api/runs/{{id:guid}}", GetRun),
            endpoints.MapGet($"{prefix}/api/runs/{{id:guid}}/console", GetConsole),
            endpoints.MapPost($"{prefix}/api/jobs/{{name}}/trigger", TriggerJob),
            endpoints.MapPost($"{prefix}/api/runs/{{id:guid}}/cancel", CancelRun),
        };

        return new CompositeConventionBuilder(builders);
    }

    private static IResult ServeIndex(string prefix)
    {
        var bytes = ReadEmbeddedBytes("index.html");
        if (bytes is null) return Results.NotFound();
        // Inject <base> so the relative "assets/..." and "api/..." URLs in index.html resolve under the
        // prefix whether the page was reached at "/prefix" or "/prefix/".
        var html = Encoding.UTF8.GetString(bytes).Replace("<head>", $"<head>\n  <base href=\"{prefix}/\">");
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static IResult ServeAsset(string path)
    {
        if (path.Contains("..")) return Results.NotFound();
        var key = path.Replace('/', '.').Replace('\\', '.');
        var bytes = ReadEmbeddedBytes(key);
        return bytes is null ? Results.NotFound() : Results.File(bytes, MimeFor(path));
    }

    private static IResult ListJobs(IDuckRunController controller, IJobRunStore runs)
    {
        var now = DateTimeOffset.UtcNow;
        var jobs = controller.ListJobs();
        var payload = new List<object>(jobs.Count);
        foreach (var j in jobs)
        {
            DateTimeOffset? next = null;
            try { next = CronUtil.GetNextOccurrence(j.Cron, now); } catch { /* ignore */ }
            payload.Add(new
            {
                name = j.Name,
                cron = j.Cron,
                maxConcurrency = j.MaxConcurrency == int.MaxValue ? (int?)null : j.MaxConcurrency,
                timeoutSeconds = j.Timeout?.TotalSeconds,
                allowManualTrigger = j.AllowManualTrigger,
                enabled = j.Enabled,
                declaringType = j.DeclaringType.FullName,
                method = j.Method.Name,
                nextRunUtc = next,
            });
        }
        return Results.Json(payload);
    }

    // Aggregate KPIs + a time-bucketed run series over a window (minutes). Reads whatever IJobRunStore
    // is registered, so it reflects the in-memory runtime window in standalone mode and real history
    // when DuckRun.EfCore is configured (its GetRunsSinceAsync is a DB query, not an in-memory scan).
    private static async Task<IResult> GetOverview(IJobRunStore runs, int window = 1440, int buckets = 24, CancellationToken ct = default)
    {
        window = Math.Clamp(window, 5, 60 * 24 * 31);   // 5 minutes .. 31 days
        buckets = Math.Clamp(buckets, 6, 96);
        const int maxRuns = 20000;

        var now = DateTimeOffset.UtcNow;
        var since = now.AddMinutes(-window);
        var all = await runs.GetRunsSinceAsync(since, maxRuns, ct);

        int succeeded = 0, failed = 0, cancelled = 0, timedOut = 0, running = 0, pending = 0;
        foreach (var r in all)
        {
            switch (r.State)
            {
                case JobRunState.Succeeded: succeeded++; break;
                case JobRunState.Failed:    failed++;    break;
                case JobRunState.Cancelled: cancelled++; break;
                case JobRunState.TimedOut:  timedOut++;  break;
                case JobRunState.Running:   running++;   break;
                default:                    pending++;   break;
            }
        }
        var finished = succeeded + failed + cancelled + timedOut;

        var sinceMs = since.ToUnixTimeMilliseconds();
        var bucketMs = window * 60_000.0 / buckets;
        var series = new BucketAcc[buckets];
        for (var i = 0; i < buckets; i++) series[i].Start = (long)(sinceMs + i * bucketMs);
        foreach (var r in all)
        {
            var t = (r.StartedAt ?? r.CreatedAt).ToUnixTimeMilliseconds();
            var idx = Math.Clamp((int)((t - sinceMs) / bucketMs), 0, buckets - 1);
            if (r.State is JobRunState.Succeeded) series[idx].Succeeded++;
            else if (r.State is JobRunState.Failed or JobRunState.TimedOut) series[idx].Failed++;
            else series[idx].Other++;
        }

        return Results.Json(new
        {
            windowMinutes = window,
            generatedAt = now,
            totals = new
            {
                total = all.Count,
                succeeded, failed, cancelled, timedOut, running, pending, finished,
                exceptions = failed + timedOut,
                successRate = finished == 0 ? (double?)null : Math.Round(100.0 * succeeded / finished, 1),
            },
            buckets = series.Select(s => new { start = s.Start, succeeded = s.Succeeded, failed = s.Failed, other = s.Other }).ToArray(),
            truncated = all.Count >= maxRuns,
        });
    }

    private struct BucketAcc { public long Start; public int Succeeded; public int Failed; public int Other; }

    private static async Task<IResult> GetJob(string name, IDuckRunController controller)
    {
        var job = controller.GetJob(name);
        if (job is null) return Results.NotFound();

        DateTimeOffset? next = null;
        try { next = CronUtil.GetNextOccurrence(job.Cron, DateTimeOffset.UtcNow); } catch { /* ignore */ }

        var recent = await controller.GetRecentRunsAsync(name, take: 1);
        return Results.Json(new
        {
            name = job.Name,
            cron = job.Cron,
            maxConcurrency = job.MaxConcurrency == int.MaxValue ? (int?)null : job.MaxConcurrency,
            timeoutSeconds = job.Timeout?.TotalSeconds,
            allowManualTrigger = job.AllowManualTrigger,
            enabled = job.Enabled,
            declaringType = job.DeclaringType.FullName,
            method = job.Method.Name,
            nextRunUtc = next,
            lastRun = recent.Count == 0 ? null : MapRunSummary(recent[0]),
        });
    }

    private static async Task<IResult> GetRuns(string name, IDuckRunController controller, int take = 50)
    {
        var runs = await controller.GetRecentRunsAsync(name, take);
        return Results.Json(runs.Select(MapRunSummary).ToArray());
    }

    private static async Task<IResult> GetRun(Guid id, IDuckRunController controller)
    {
        var run = await controller.GetRunAsync(id);
        return run is null ? Results.NotFound() : Results.Json(MapRunDetail(run));
    }

    private static async Task<IResult> GetConsole(Guid id, IDuckRunController controller)
    {
        var entries = await controller.GetConsoleAsync(id);
        return Results.Json(entries.Select(e => new
        {
            timestamp = e.Timestamp,
            level = e.Level.ToString(),
            message = e.Message,
        }).ToArray());
    }

    private static async Task<IResult> TriggerJob(string name, IDuckRunController controller)
    {
        try
        {
            var id = await controller.TriggerAsync(name);
            return Results.Json(new { runId = id });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: 409);
        }
    }

    private static async Task<IResult> CancelRun(Guid id, IDuckRunController controller)
    {
        await controller.CancelAsync(id);
        return Results.NoContent();
    }

    private static object MapRunSummary(JobRun r) => new
    {
        id = r.Id,
        jobName = r.JobName,
        state = r.State.ToString(),
        triggerSource = r.TriggerSource,
        startedAt = r.StartedAt,
        finishedAt = r.FinishedAt,
        durationMs = r.Duration?.TotalMilliseconds,
        hasError = r.ErrorMessage is not null,
    };

    private static object MapRunDetail(JobRun r) => new
    {
        id = r.Id,
        jobName = r.JobName,
        state = r.State.ToString(),
        triggerSource = r.TriggerSource,
        createdAt = r.CreatedAt,
        startedAt = r.StartedAt,
        finishedAt = r.FinishedAt,
        durationMs = r.Duration?.TotalMilliseconds,
        errorMessage = r.ErrorMessage,
        errorStackTrace = r.ErrorStackTrace,
    };

    private static byte[]? ReadEmbeddedBytes(string assetKey)
    {
        using var stream = _asm.GetManifestResourceStream(ResourceRoot + assetKey);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string MimeFor(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "application/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream",
    };
}
