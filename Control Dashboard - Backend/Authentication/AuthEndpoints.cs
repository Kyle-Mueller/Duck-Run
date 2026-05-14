using System.Security.Claims;
using DuckRun.Dashboard.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.Authentication;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("login", LoginAsync).AllowAnonymous();
        group.MapPost("logout", LogoutAsync);
        group.MapGet("me", MeAsync);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext http,
        LoginRequest body,
        IDbContextFactory<DashboardDbContext> contextFactory)
    {
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
            return Results.BadRequest(new { error = "Email and password are required." });

        var email = body.Email.Trim().ToLowerInvariant();

        await using var ctx = await contextFactory.CreateDbContextAsync();
        var user = await ctx.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null || !PasswordHasher.Verify(body.Password, user.PasswordHash))
        {
            await Task.Delay(Random.Shared.Next(100, 200));
            return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
        }

        user.LastSignInAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("displayName", user.DisplayName),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
        });

        return Results.Json(MapMe(user));
    }

    private static async Task<IResult> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(HttpContext http, IDbContextFactory<DashboardDbContext> contextFactory)
    {
        if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();

        var id = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var ctx = await contextFactory.CreateDbContextAsync();
        var user = await ctx.Users.FindAsync(id);
        return user is null ? Results.Unauthorized() : Results.Json(MapMe(user));
    }

    private static object MapMe(User u) => new
    {
        id = u.Id,
        email = u.Email,
        displayName = u.DisplayName,
        role = u.Role,
        lastSignInAt = u.LastSignInAt,
    };

    public sealed record LoginRequest(string Email, string Password);
}
