using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.Database;

/// <summary>
/// Hosted service that ensures dashboard tables exist on startup.
/// </summary>
internal sealed class SchemaBootstrap(
    IDbContextFactory<DashboardDbContext> contextFactory,
    ILogger<SchemaBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
            var created = await ctx.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation(
                created
                    ? "Dashboard schema created (provider: {Provider})."
                    : "Dashboard schema already present (provider: {Provider}).",
                ctx.Database.ProviderName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard schema bootstrap failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
