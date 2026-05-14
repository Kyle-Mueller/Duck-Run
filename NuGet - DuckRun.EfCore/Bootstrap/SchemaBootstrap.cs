using DuckRun.EfCore.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckRun.EfCore.Bootstrap;

/// <summary>
/// Hosted service that ensures the DuckRun schema/tables exist on startup. Uses EF Core's
/// <c>EnsureCreatedAsync</c>, which creates missing tables but does not migrate an existing schema.
/// </summary>
internal sealed class SchemaBootstrap(IDbContextFactory<DuckRunDbContext> contextFactory, ILogger<SchemaBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
            var created = await ctx.Database.EnsureCreatedAsync(cancellationToken);
            if (created) logger.LogInformation("DuckRun.EfCore schema created (provider: {Provider}).", ctx.Database.ProviderName);
            else logger.LogInformation("DuckRun.EfCore schema already present (provider: {Provider}).", ctx.Database.ProviderName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DuckRun.EfCore schema bootstrap failed. Persistence will not work for this run.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
