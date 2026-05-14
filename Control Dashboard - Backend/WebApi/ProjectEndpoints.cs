using System.Security.Claims;
using DuckRun.Dashboard.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.WebApi;

internal static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("/api/projects").RequireAuthorization();

        projects.MapGet("", ListProjectsAsync);
        projects.MapPost("", CreateProjectAsync);
        projects.MapGet("{id:guid}", GetProjectAsync);
        projects.MapGet("{id:guid}/jobs", GetJobsAsync);
        projects.MapGet("{id:guid}/runs", GetRunsAsync);
        projects.MapGet("{id:guid}/runs/{runId:guid}", GetRunAsync);
        projects.MapGet("{id:guid}/runs/{runId:guid}/console", GetConsoleAsync);
        projects.MapGet("{id:guid}/nodes", GetNodesAsync);

        return app;
    }

    private static async Task<IResult> ListProjectsAsync(IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var list = await ctx.Projects.OrderBy(p => p.Name).Select(p => new
        {
            id = p.Id,
            name = p.Name,
            createdAt = p.CreatedAt,
        }).ToListAsync();
        return Results.Json(list);
    }

    private static async Task<IResult> CreateProjectAsync(
        HttpContext http,
        CreateProjectRequest body,
        IDbContextFactory<DashboardDbContext> contextFactory)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "Project name is required." });

        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await using var ctx = await contextFactory.CreateDbContextAsync();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = body.Name.Trim(),
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Projects.Add(project);
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = userId,
            Role = "Admin",
            AddedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        return Results.Created($"/api/projects/{project.Id}", new
        {
            id = project.Id,
            name = project.Name,
            createdAt = project.CreatedAt,
        });
    }

    private static async Task<IResult> GetProjectAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var p = await ctx.Projects.FindAsync(id);
        return p is null ? Results.NotFound() : Results.Json(new
        {
            id = p.Id,
            name = p.Name,
            createdAt = p.CreatedAt,
        });
    }

    private static async Task<IResult> GetJobsAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var jobs = await ctx.JobDefinitions
            .Where(j => j.ProjectId == id)
            .OrderBy(j => j.Name)
            .Select(j => new
            {
                name = j.Name,
                cron = j.Cron,
                maxConcurrency = j.MaxConcurrency,
                timeoutSeconds = j.TimeoutSeconds,
                allowManualTrigger = j.AllowManualTrigger,
                enabled = j.Enabled,
                firstSeen = j.FirstSeen,
                lastSeen = j.LastSeen,
            })
            .ToListAsync();
        return Results.Json(jobs);
    }

    private static async Task<IResult> GetRunsAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory,
        string? job = null, string? state = null, int take = 50)
    {
        take = Math.Clamp(take, 1, 500);

        await using var ctx = await contextFactory.CreateDbContextAsync();
        var query = ctx.JobRuns.Where(r => r.ProjectId == id);
        if (!string.IsNullOrWhiteSpace(job)) query = query.Where(r => r.JobName == job);
        if (!string.IsNullOrWhiteSpace(state)) query = query.Where(r => r.State == state);

        var runs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => new
            {
                id = r.Id,
                jobName = r.JobName,
                nodeId = r.NodeId,
                state = r.State,
                triggerSource = r.TriggerSource,
                createdAt = r.CreatedAt,
                startedAt = r.StartedAt,
                finishedAt = r.FinishedAt,
                hasError = r.ErrorMessage != null,
            })
            .ToListAsync();
        return Results.Json(runs);
    }

    private static async Task<IResult> GetRunAsync(Guid id, Guid runId, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var r = await ctx.JobRuns.SingleOrDefaultAsync(x => x.ProjectId == id && x.Id == runId);
        return r is null ? Results.NotFound() : Results.Json(new
        {
            id = r.Id,
            jobName = r.JobName,
            nodeId = r.NodeId,
            state = r.State,
            triggerSource = r.TriggerSource,
            createdAt = r.CreatedAt,
            startedAt = r.StartedAt,
            finishedAt = r.FinishedAt,
            errorMessage = r.ErrorMessage,
            errorStackTrace = r.ErrorStackTrace,
        });
    }

    private static async Task<IResult> GetConsoleAsync(Guid id, Guid runId, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var logs = await ctx.ConsoleLogs
            .Where(c => c.ProjectId == id && c.RunId == runId)
            .OrderBy(c => c.Id)
            .Select(c => new { timestamp = c.Timestamp, level = c.Level, message = c.Message })
            .ToListAsync();
        return Results.Json(logs);
    }

    private static async Task<IResult> GetNodesAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var nodes = await ctx.NodeHeartbeats
            .Where(n => n.ProjectId == id)
            .OrderByDescending(n => n.LastSeen)
            .Select(n => new
            {
                nodeId = n.NodeId,
                runtime = n.Runtime,
                clientVersion = n.ClientVersion,
                startedAt = n.StartedAt,
                lastSeen = n.LastSeen,
                isLeader = n.IsLeader,
            })
            .ToListAsync();
        return Results.Json(nodes);
    }

    public sealed record CreateProjectRequest(string Name);
}
