using DuckRun.Dashboard.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.WebApi;

internal static class KpiEndpoints
{
    public static IEndpointRouteBuilder MapKpiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kpis").RequireAuthorization();
        group.MapGet("global", GlobalAsync);
        group.MapGet("projects/{id:guid}", ProjectAsync);
        return app;
    }

    private static async Task<IResult> GlobalAsync(IDbContextFactory<DashboardDbContext> contextFactory)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var nodeAliveCutoff = DateTime.UtcNow.AddMinutes(-1);

        await using var ctx = await contextFactory.CreateDbContextAsync();

        var totalProjects = await ctx.Projects.CountAsync();
        var totalJobs = await ctx.JobDefinitions.CountAsync();
        var activeNodes = await ctx.NodeHeartbeats.CountAsync(n => n.LastSeen >= nodeAliveCutoff);
        var running = await ctx.JobRuns.CountAsync(r => r.State == "Running");

        var byState = await ctx.JobRuns
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.State)
            .Select(g => new { state = g.Key, count = g.Count() })
            .ToListAsync();

        return Results.Json(new
        {
            totalProjects,
            totalJobs,
            activeNodes,
            running,
            runs24h = byState,
        });
    }

    private static async Task<IResult> ProjectAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var nodeAliveCutoff = DateTime.UtcNow.AddMinutes(-1);

        await using var ctx = await contextFactory.CreateDbContextAsync();

        var jobs = await ctx.JobDefinitions.CountAsync(j => j.ProjectId == id);
        var allNodes = await ctx.NodeHeartbeats.CountAsync(n => n.ProjectId == id);
        var aliveNodes = await ctx.NodeHeartbeats.CountAsync(n => n.ProjectId == id && n.LastSeen >= nodeAliveCutoff);
        var leader = await ctx.NodeHeartbeats
            .Where(n => n.ProjectId == id && n.IsLeader && n.LastSeen >= nodeAliveCutoff)
            .Select(n => n.NodeId)
            .FirstOrDefaultAsync();
        var running = await ctx.JobRuns.CountAsync(r => r.ProjectId == id && r.State == "Running");

        var byState = await ctx.JobRuns
            .Where(r => r.ProjectId == id && r.CreatedAt >= since)
            .GroupBy(r => r.State)
            .Select(g => new { state = g.Key, count = g.Count() })
            .ToListAsync();

        return Results.Json(new
        {
            jobs,
            nodesAlive = aliveNodes,
            nodesTotal = allNodes,
            leader,
            running,
            runs24h = byState,
        });
    }
}
