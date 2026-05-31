using System.Security.Claims;
using System.Text;
using DuckRun.Dashboard.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.WebApi;

internal static class GroupEndpoints
{
    public const int MaxDepth = 10;

    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = app.MapGroup("/api/groups").RequireAuthorization();

        groups.MapGet("", ListFlatAsync);
        groups.MapGet("tree", GetTreeAsync);
        groups.MapPost("", CreateAsync);
        groups.MapGet("{id:guid}", GetAsync);
        groups.MapPatch("{id:guid}", UpdateAsync);
        groups.MapDelete("{id:guid}", DeleteAsync);
        groups.MapPost("{id:guid}/move", MoveAsync);

        groups.MapGet("{id:guid}/members", ListMembersAsync);
        groups.MapPost("{id:guid}/members", AddMemberAsync);
        groups.MapPatch("{id:guid}/members/{memberId:guid}", UpdateMemberAsync);
        groups.MapDelete("{id:guid}/members/{memberId:guid}", RemoveMemberAsync);

        return app;
    }

    private static async Task<IResult> ListFlatAsync(IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var list = await ctx.Groups.OrderBy(g => g.FullPath).Select(g => new
        {
            id = g.Id,
            name = g.Name,
            slug = g.Slug,
            description = g.Description,
            parentGroupId = g.ParentGroupId,
            fullPath = g.FullPath,
            depth = g.Depth,
            createdAt = g.CreatedAt,
        }).ToListAsync();
        return Results.Json(list);
    }

    private static async Task<IResult> GetTreeAsync(IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();

        var groups = await ctx.Groups.AsNoTracking().OrderBy(g => g.FullPath).ToListAsync();
        var projectCountsByGroup = await ctx.Projects.Where(p => p.GroupId != null).GroupBy(p => p.GroupId!.Value)
                                                     .Select(g => new { GroupId = g.Key, Count = g.Count() })
                                                     .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        var rootProjectCount = await ctx.Projects.CountAsync(p => p.GroupId == null);

        var byParent = groups.GroupBy(g => g.ParentGroupId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());

        object Node(Group g) => new
        {
            id = g.Id,
            name = g.Name,
            slug = g.Slug,
            description = g.Description,
            fullPath = g.FullPath,
            depth = g.Depth,
            projectCount = projectCountsByGroup.GetValueOrDefault(g.Id, 0),
            subgroups = byParent.TryGetValue(g.Id, out var kids) ? kids.ConvertAll(Node) : [],
        };

        var roots = byParent.TryGetValue(Guid.Empty, out var top) ? top.ConvertAll(Node) : [];

        return Results.Json(new { roots, rootProjectCount });
    }

    private static async Task<IResult> CreateAsync(HttpContext http, CreateGroupRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return Results.BadRequest(new { error = "Group name is required." });

        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(body.Slug) ? body.Name : body.Slug);
        if (slug.Length == 0) return Results.BadRequest(new { error = "Slug must contain at least one letter, digit, hyphen, or underscore." });

        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await using var ctx = await contextFactory.CreateDbContextAsync();

        Group? parent = null;
        if (body.ParentGroupId is { } parentId)
        {
            parent = await ctx.Groups.FindAsync(parentId);
            if (parent is null) return Results.BadRequest(new { error = $"Parent group {parentId} not found." });
            if (parent.Depth + 1 > MaxDepth) return Results.BadRequest(new { error = $"Group nesting cannot exceed {MaxDepth} levels." });
        }

        var siblingExists = await ctx.Groups.AnyAsync(g => g.ParentGroupId == body.ParentGroupId && g.Slug == slug);
        if (siblingExists) return Results.Conflict(new { error = $"A sibling group with slug '{slug}' already exists here." });

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = body.Name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
            ParentGroupId = body.ParentGroupId,
            FullPath = parent is null ? slug : $"{parent.FullPath}/{slug}",
            Depth = parent is null ? 0 : parent.Depth + 1,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Groups.Add(group);
        ctx.GroupMembers.Add(new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            UserId = userId,
            Role = "Admin",
            AddedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        return Results.Created($"/api/groups/{group.Id}", Serialize(group));
    }

    private static async Task<IResult> GetAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();

        var group = await ctx.Groups.FindAsync(id);
        if (group is null) return Results.NotFound();

        var subgroups = await ctx.Groups.Where(g => g.ParentGroupId == id).OrderBy(g => g.Slug).Select(g => new
        {
            id = g.Id,
            name = g.Name,
            slug = g.Slug,
            description = g.Description,
            fullPath = g.FullPath,
            depth = g.Depth,
            createdAt = g.CreatedAt,
        }).ToListAsync();

        var projects = await ctx.Projects.Where(p => p.GroupId == id).OrderBy(p => p.Slug).Select(p => new
        {
            id = p.Id,
            name = p.Name,
            slug = p.Slug,
            createdAt = p.CreatedAt,
        }).ToListAsync();

        var ancestors = new List<object>();
        var cursor = group.ParentGroupId;
        while (cursor is { } cur)
        {
            var anc = await ctx.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == cur);
            if (anc is null) break;
            ancestors.Insert(0, new { id = anc.Id, name = anc.Name, slug = anc.Slug, fullPath = anc.FullPath });
            cursor = anc.ParentGroupId;
        }

        return Results.Json(new
        {
            id = group.Id,
            name = group.Name,
            slug = group.Slug,
            description = group.Description,
            parentGroupId = group.ParentGroupId,
            fullPath = group.FullPath,
            depth = group.Depth,
            createdAt = group.CreatedAt,
            ancestors,
            subgroups,
            projects,
        });
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateGroupRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();

        var group = await ctx.Groups.FindAsync(id);
        if (group is null) return Results.NotFound();

        if (body.Name is { } newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return Results.BadRequest(new { error = "Group name cannot be empty." });
            group.Name = newName.Trim();
        }

        if (body.Description is not null) group.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();

        if (body.Slug is { } slugIn)
        {
            var newSlug = NormalizeSlug(slugIn);
            if (newSlug.Length == 0) return Results.BadRequest(new { error = "Slug must contain at least one letter, digit, hyphen, or underscore." });

            if (!string.Equals(newSlug, group.Slug, StringComparison.Ordinal))
            {
                var collision = await ctx.Groups.AnyAsync(g => g.ParentGroupId == group.ParentGroupId && g.Slug == newSlug && g.Id != group.Id);
                if (collision) return Results.Conflict(new { error = $"A sibling group with slug '{newSlug}' already exists here." });
                await RewritePathsAsync(ctx, group, group.ParentGroupId, newSlug);
            }
        }

        await ctx.SaveChangesAsync();
        return Results.Json(Serialize(group));
    }

    private static async Task<IResult> DeleteAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();

        var group = await ctx.Groups.FindAsync(id);
        if (group is null) return Results.NotFound();

        var hasSubgroups = await ctx.Groups.AnyAsync(g => g.ParentGroupId == id);
        var hasProjects = await ctx.Projects.AnyAsync(p => p.GroupId == id);
        if (hasSubgroups || hasProjects) return Results.Conflict(new { error = "Group is not empty. Move or delete its subgroups and projects first." });

        ctx.GroupMembers.RemoveRange(ctx.GroupMembers.Where(m => m.GroupId == id));
        ctx.Groups.Remove(group);
        await ctx.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> MoveAsync(Guid id, MoveGroupRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();

        var group = await ctx.Groups.FindAsync(id);
        if (group is null) return Results.NotFound();

        if (body.NewParentGroupId == id) return Results.BadRequest(new { error = "A group cannot be its own parent." });

        Group? newParent = null;
        var newDepth = 0;
        if (body.NewParentGroupId is { } np)
        {
            newParent = await ctx.Groups.FindAsync(np);
            if (newParent is null) return Results.BadRequest(new { error = $"Parent group {np} not found." });

            if (await IsDescendantAsync(ctx, ancestor: id, candidate: np)) return Results.BadRequest(new { error = "Cannot move a group under one of its own descendants." });

            newDepth = newParent.Depth + 1;
        }

        var deepest = await ctx.Groups.AsNoTracking().Where(g => g.FullPath == group.FullPath || g.FullPath.StartsWith(group.FullPath + "/")).Select(g => g.Depth).MaxAsync();
        var addedDepth = newDepth - group.Depth;
        if (deepest + addedDepth > MaxDepth) return Results.BadRequest(new { error = $"Move would exceed the maximum nesting depth of {MaxDepth}." });

        var collision = await ctx.Groups.AnyAsync(g => g.ParentGroupId == body.NewParentGroupId && g.Slug == group.Slug && g.Id != group.Id);
        if (collision) return Results.Conflict(new { error = $"A sibling group with slug '{group.Slug}' already exists at the destination." });

        await RewritePathsAsync(ctx, group, body.NewParentGroupId, group.Slug);
        await ctx.SaveChangesAsync();
        return Results.Json(Serialize(group));
    }

    private static async Task<IResult> ListMembersAsync(Guid id, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var members = await ctx.GroupMembers.Where(m => m.GroupId == id).Join(ctx.Users, m => m.UserId, u => u.Id, (m, u) => new
        {
            id = m.Id,
            userId = u.Id,
            email = u.Email,
            displayName = u.DisplayName,
            role = m.Role,
            addedAt = m.AddedAt,
        }).OrderBy(m => m.email).ToListAsync();
        return Results.Json(members);
    }

    private static async Task<IResult> AddMemberAsync(Guid id, AddGroupMemberRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        if (string.IsNullOrWhiteSpace(body.Email)) return Results.BadRequest(new { error = "Email is required." });

        await using var ctx = await contextFactory.CreateDbContextAsync();
        var group = await ctx.Groups.FindAsync(id);
        if (group is null) return Results.NotFound();

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == body.Email.Trim());
        if (user is null) return Results.BadRequest(new { error = $"No user with email '{body.Email}'." });

        var existing = await ctx.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == user.Id);
        if (existing is not null) return Results.Conflict(new { error = "User is already a member of this group." });

        var role = NormalizeRole(body.Role);
        var member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = id,
            UserId = user.Id,
            Role = role,
            AddedAt = DateTime.UtcNow,
        };
        ctx.GroupMembers.Add(member);
        await ctx.SaveChangesAsync();

        return Results.Created($"/api/groups/{id}/members/{member.Id}", new
        {
            id = member.Id,
            userId = user.Id,
            email = user.Email,
            displayName = user.DisplayName,
            role = member.Role,
            addedAt = member.AddedAt,
        });
    }

    private static async Task<IResult> UpdateMemberAsync(Guid id, Guid memberId, UpdateGroupMemberRequest body, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var member = await ctx.GroupMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.GroupId == id);
        if (member is null) return Results.NotFound();

        member.Role = NormalizeRole(body.Role);
        await ctx.SaveChangesAsync();
        return Results.Json(new { id = member.Id, role = member.Role });
    }

    private static async Task<IResult> RemoveMemberAsync(Guid id, Guid memberId, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var member = await ctx.GroupMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.GroupId == id);
        if (member is null) return Results.NotFound();
        ctx.GroupMembers.Remove(member);
        await ctx.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<bool> IsDescendantAsync(DashboardDbContext ctx, Guid ancestor, Guid candidate)
    {
        var anc = await ctx.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == ancestor);
        if (anc is null) return false;
        var cand = await ctx.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == candidate);
        if (cand is null) return false;
        return cand.FullPath == anc.FullPath || cand.FullPath.StartsWith(anc.FullPath + "/");
    }

    private static async Task RewritePathsAsync(DashboardDbContext ctx, Group group, Guid? newParentGroupId, string newSlug)
    {
        Group? newParent = null;
        if (newParentGroupId is { } np) newParent = await ctx.Groups.FindAsync(np) ?? throw new InvalidOperationException($"Parent group {np} not found.");

        var oldPath = group.FullPath;
        var newPath = newParent is null ? newSlug : $"{newParent.FullPath}/{newSlug}";
        var newDepth = newParent is null ? 0 : newParent.Depth + 1;
        var depthDelta = newDepth - group.Depth;

        var descendants = await ctx.Groups.Where(g => g.FullPath.StartsWith(oldPath + "/")).ToListAsync();

        group.ParentGroupId = newParentGroupId;
        group.Slug = newSlug;
        group.FullPath = newPath;
        group.Depth = newDepth;

        foreach (var d in descendants)
        {
            d.FullPath = newPath + d.FullPath[oldPath.Length..];
            d.Depth += depthDelta;
        }
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

    private static string NormalizeRole(string? role)
    {
        var r = (role ?? "").Trim();
        return r switch
        {
            "Admin" or "Maintainer" or "Developer" or "Viewer" => r,
            _ => "Viewer",
        };
    }

    private static object Serialize(Group g) => new
    {
        id = g.Id,
        name = g.Name,
        slug = g.Slug,
        description = g.Description,
        parentGroupId = g.ParentGroupId,
        fullPath = g.FullPath,
        depth = g.Depth,
        createdAt = g.CreatedAt,
    };

    public sealed record CreateGroupRequest(string Name, string? Slug, string? Description, Guid? ParentGroupId);
    public sealed record UpdateGroupRequest(string? Name, string? Slug, string? Description);
    public sealed record MoveGroupRequest(Guid? NewParentGroupId);
    public sealed record AddGroupMemberRequest(string Email, string? Role);
    public sealed record UpdateGroupMemberRequest(string? Role);
}
