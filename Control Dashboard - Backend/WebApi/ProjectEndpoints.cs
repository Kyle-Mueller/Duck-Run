using System.Security.Claims;
using System.Text;
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
        projects.MapPatch("{id:guid}", UpdateProjectAsync);
        projects.MapPost("{id:guid}/move", MoveProjectAsync);
        projects.MapGet("{id:guid}/jobs", GetJobsAsync);
        projects.MapGet("{id:guid}/runs", GetRunsAsync);
        projects.MapGet("{id:guid}/runs/{runId:guid}", GetRunAsync);
        projects.MapGet("{id:guid}/runs/{runId:guid}/console", GetConsoleAsync);
        projects.MapGet("{id:guid}/nodes", GetNodesAsync);

        return app;
    }

    private static async Task<IResult> ListProjectsAsync(IDbContextFactory<DashboardDbContext> contextFactory, Guid? groupId = null, bool rootOnly = false)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var query = ctx.Projects.AsQueryable();
        if (rootOnly) query = query.Where(p => p.GroupId == null);
        else if (groupId is { } gid) query = query.Where(p => p.GroupId == gid);

        var rows = await query.OrderBy(p => p.Name).Select(p => new
        {
            id = p.Id,
            name = p.Name,
            slug = p.Slug,
            groupId = p.GroupId,
            createdAt = p.CreatedAt,
        }).ToListAsync();
        return Results.Json(rows);
    }

    private static async Task<IResult> CreateProjectAsync(HttpContext http, CreateProjectRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return Results.BadRequest(new { error = "Project name is required." });

        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(body.Slug) ? body.Name : body.Slug);
        if (slug.Length == 0) return Results.BadRequest(new { error = "Slug must contain at least one letter, digit, hyphen, or underscore." });

        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await using var ctx = await contextFactory.CreateDbContextAsync();

        if (body.GroupId is { } gid)
        {
            var groupExists = await ctx.Groups.AnyAsync(g => g.Id == gid);
            if (!groupExists) return Results.BadRequest(new { error = $"Group {gid} not found." });
        }

        var collision = await ctx.Projects.AnyAsync(p => p.GroupId == body.GroupId && p.Slug == slug);
        if (collision) return Results.Conflict(new { error = $"A sibling project with slug '{slug}' already exists in this namespace." });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = body.Name.Trim(),
            Slug = slug,
            GroupId = body.GroupId,
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

        return Results.Created($"/api/projects/{project.Id}", Serialize(project));
    }

    private static async Task<IResult> GetProjectAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var p = await ctx.Projects.FindAsync(id);
        if (p is null) return Results.NotFound();

        string? groupFullPath = null;
        if (p.GroupId is { } gid)
        {
            groupFullPath = await ctx.Groups.AsNoTracking().Where(g => g.Id == gid).Select(g => g.FullPath).FirstOrDefaultAsync();
        }

        return Results.Json(new
        {
            id = p.Id,
            name = p.Name,
            slug = p.Slug,
            groupId = p.GroupId,
            groupFullPath,
            createdAt = p.CreatedAt,
        });
    }

    private static async Task<IResult> UpdateProjectAsync(Guid id, UpdateProjectRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var p = await ctx.Projects.FindAsync(id);
        if (p is null) return Results.NotFound();

        if (body.Name is { } newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return Results.BadRequest(new { error = "Project name cannot be empty." });
            p.Name = newName.Trim();
        }

        if (body.Slug is { } slugIn)
        {
            var newSlug = NormalizeSlug(slugIn);
            if (newSlug.Length == 0) return Results.BadRequest(new { error = "Slug must contain at least one letter, digit, hyphen, or underscore." });
            if (!string.Equals(newSlug, p.Slug, StringComparison.Ordinal))
            {
                var collision = await ctx.Projects.AnyAsync(x => x.GroupId == p.GroupId && x.Slug == newSlug && x.Id != p.Id);
                if (collision) return Results.Conflict(new { error = $"A sibling project with slug '{newSlug}' already exists here." });
                p.Slug = newSlug;
            }
        }

        await ctx.SaveChangesAsync();
        return Results.Json(Serialize(p));
    }

    private static async Task<IResult> MoveProjectAsync(Guid id, MoveProjectRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var p = await ctx.Projects.FindAsync(id);
        if (p is null) return Results.NotFound();

        if (body.NewGroupId is { } gid)
        {
            var exists = await ctx.Groups.AnyAsync(g => g.Id == gid);
            if (!exists) return Results.BadRequest(new { error = $"Group {gid} not found." });
        }

        var collision = await ctx.Projects.AnyAsync(x => x.GroupId == body.NewGroupId && x.Slug == p.Slug && x.Id != p.Id);
        if (collision) return Results.Conflict(new { error = $"A sibling project with slug '{p.Slug}' already exists at the destination." });

        p.GroupId = body.NewGroupId;
        await ctx.SaveChangesAsync();
        return Results.Json(Serialize(p));
    }

    private static async Task<IResult> GetJobsAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var jobs = await ctx.JobDefinitions.Where(j => j.ProjectId == id).OrderBy(j => j.Name).Select(j => new
        {
            name = j.Name,
            cron = j.Cron,
            maxConcurrency = j.MaxConcurrency,
            timeoutSeconds = j.TimeoutSeconds,
            allowManualTrigger = j.AllowManualTrigger,
            enabled = j.Enabled,
            firstSeen = j.FirstSeen,
            lastSeen = j.LastSeen,
        }).ToListAsync();
        return Results.Json(jobs);
    }

    private static async Task<IResult> GetRunsAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory, string? job = null, string? state = null, int take = 50)
    {
        take = Math.Clamp(take, 1, 500);

        await using var ctx = await contextFactory.CreateDbContextAsync();
        var query = ctx.JobRuns.Where(r => r.ProjectId == id);
        if (!string.IsNullOrWhiteSpace(job)) query = query.Where(r => r.JobName == job);
        if (!string.IsNullOrWhiteSpace(state)) query = query.Where(r => r.State == state);

        var runs = await query.OrderByDescending(r => r.CreatedAt).Take(take).Select(r => new
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
        }).ToListAsync();
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
        var logs = await ctx.ConsoleLogs.Where(c => c.ProjectId == id && c.RunId == runId).OrderBy(c => c.Id)
                                        .Select(c => new { timestamp = c.Timestamp, level = c.Level, message = c.Message })
                                        .ToListAsync();
        return Results.Json(logs);
    }

    private static async Task<IResult> GetNodesAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var nodes = await ctx.NodeHeartbeats.Where(n => n.ProjectId == id).OrderByDescending(n => n.LastSeen).Select(n => new
        {
            nodeId = n.NodeId,
            runtime = n.Runtime,
            clientVersion = n.ClientVersion,
            startedAt = n.StartedAt,
            lastSeen = n.LastSeen,
            isLeader = n.IsLeader,
        }).ToListAsync();
        return Results.Json(nodes);
    }

    private static string NormalizeSlug(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        var lastWasSeparator = false;
        foreach (var ch in raw.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasSeparator = false;
            }
            else if (ch is '-' or '_' or ' ' or '.' or '/')
            {
                if (sb.Length > 0 && !lastWasSeparator)
                {
                    sb.Append('-');
                    lastWasSeparator = true;
                }
            }
        }
        return sb.ToString().Trim('-');
    }

    private static object Serialize(Project p) => new
    {
        id = p.Id,
        name = p.Name,
        slug = p.Slug,
        groupId = p.GroupId,
        createdAt = p.CreatedAt,
    };

    public sealed record CreateProjectRequest(string Name, string? Slug, Guid? GroupId);
    public sealed record UpdateProjectRequest(string? Name, string? Slug);
    public sealed record MoveProjectRequest(Guid? NewGroupId);
}
