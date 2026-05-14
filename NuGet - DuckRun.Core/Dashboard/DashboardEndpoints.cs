using System.Reflection;
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
        var group = endpoints.MapGroup(prefix);

        group.MapGet("/", ServeIndex);
        group.MapGet("assets/{**path}", ServeAsset);

        var api = group.MapGroup("api");
        api.MapGet("jobs", ListJobs);
        api.MapGet("jobs/{name}", GetJob);
        api.MapGet("jobs/{name}/runs", GetRuns);
        api.MapGet("runs/{id:guid}", GetRun);
        api.MapGet("runs/{id:guid}/console", GetConsole);
        api.MapPost("jobs/{name}/trigger", TriggerJob);
        api.MapPost("runs/{id:guid}/cancel", CancelRun);

        return group;
    }

    private static IResult ServeIndex()
    {
        var bytes = ReadEmbeddedBytes("index.html");
        return bytes is null ? Results.NotFound() : Results.Bytes(bytes, "text/html; charset=utf-8");
    }

    private static IResult ServeAsset(string path)
    {
        if (path.Contains("..")) return Results.NotFound();
        var key = path.Replace('/', '.').Replace('\\', '.');
        var bytes = ReadEmbeddedBytes(key);
        return bytes is null ? Results.NotFound() : Results.Bytes(bytes, MimeFor(path));
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
