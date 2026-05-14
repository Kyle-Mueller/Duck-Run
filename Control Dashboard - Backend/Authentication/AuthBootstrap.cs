using DuckRun.Dashboard.Configuration;
using DuckRun.Dashboard.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DuckRun.Dashboard.Authentication;

/// <summary>
/// Hosted service. If no users exist and InitialAdminEmail+InitialAdminPassword are set, creates the bootstrap admin.
/// Idempotent; only fires on the very first boot of a fresh database.
/// </summary>
internal sealed class AuthBootstrap(
    IDbContextFactory<DashboardDbContext> contextFactory,
    IOptions<DashboardOptions> options,
    ILogger<AuthBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var auth = options.Value.Auth;
        if (string.IsNullOrWhiteSpace(auth.InitialAdminEmail) || string.IsNullOrWhiteSpace(auth.InitialAdminPassword))
        {
            logger.LogDebug("No initial admin configured. Skipping bootstrap.");
            return;
        }

        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        var anyUsers = await ctx.Users.AnyAsync(cancellationToken);
        if (anyUsers)
        {
            logger.LogDebug("Users already exist. Skipping initial-admin bootstrap.");
            return;
        }

        var email = auth.InitialAdminEmail.Trim().ToLowerInvariant();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(auth.InitialAdminPassword),
            DisplayName = email,
            Role = "Admin",
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Users.Add(admin);
        await ctx.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created initial admin '{Email}'. Sign in and change the password.", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
