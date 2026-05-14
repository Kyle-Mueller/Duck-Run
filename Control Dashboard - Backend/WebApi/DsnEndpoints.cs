using DuckRun.Dashboard.Authentication;
using DuckRun.Dashboard.Configuration;
using DuckRun.Dashboard.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DuckRun.Dashboard.WebApi;

internal static class DsnEndpoints
{
    public static IEndpointRouteBuilder MapDsnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/dsns").RequireAuthorization();

        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapDelete("{keyId:guid}", RevokeAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(Guid projectId,
        IDbContextFactory<DashboardDbContext> contextFactory,
        IOptions<DashboardOptions> options)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var keys = await ctx.ApiKeys
            .Where(k => k.ProjectId == projectId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new
            {
                id = k.Id,
                publicKey = k.PublicKey,
                label = k.Label,
                createdAt = k.CreatedAt,
                revokedAt = k.RevokedAt,
                dsn = BuildDsn(options.Value.PublicBaseUrl, k.PublicKey, projectId),
            })
            .ToListAsync();
        return Results.Json(keys);
    }

    private static async Task<IResult> CreateAsync(Guid projectId,
        CreateDsnRequest body,
        IDbContextFactory<DashboardDbContext> contextFactory,
        IOptions<DashboardOptions> options)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var project = await ctx.Projects.FindAsync(projectId);
        if (project is null) return Results.NotFound();

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PublicKey = ApiKeyGenerator.NewPublicKey(),
            Label = body.Label?.Trim() ?? "",
            CreatedAt = DateTime.UtcNow,
        };
        ctx.ApiKeys.Add(apiKey);
        await ctx.SaveChangesAsync();

        return Results.Created($"/api/projects/{projectId}/dsns/{apiKey.Id}", new
        {
            id = apiKey.Id,
            publicKey = apiKey.PublicKey,
            label = apiKey.Label,
            createdAt = apiKey.CreatedAt,
            dsn = BuildDsn(options.Value.PublicBaseUrl, apiKey.PublicKey, projectId),
        });
    }

    private static async Task<IResult> RevokeAsync(Guid projectId, Guid keyId,
        IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var key = await ctx.ApiKeys.SingleOrDefaultAsync(k => k.Id == keyId && k.ProjectId == projectId);
        if (key is null) return Results.NotFound();
        if (key.RevokedAt is not null) return Results.NoContent();

        key.RevokedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return Results.NoContent();
    }

    private static string BuildDsn(string publicBaseUrl, string publicKey, Guid projectId)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl)) return "";
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri)) return "";
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        return $"{uri.Scheme}://{publicKey}@{uri.Host}{port}/{projectId}";
    }

    public sealed record CreateDsnRequest(string? Label);
}
