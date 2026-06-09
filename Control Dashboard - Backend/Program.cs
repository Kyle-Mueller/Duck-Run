using DuckRun.Dashboard.Authentication;
using DuckRun.Dashboard.Configuration;
using DuckRun.Dashboard.Database;
using DuckRun.Dashboard.Ingest;
using DuckRun.Dashboard.WebApi;
using DuckRun.EfCore.Providers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddEnvironmentVariables();

        builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection(DashboardOptions.SectionName));

        var dashboardOpts = builder.Configuration.GetSection(DashboardOptions.SectionName).Get<DashboardOptions>() ?? new DashboardOptions();

        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
        });

        builder.Services.AddDbContextFactory<DashboardDbContext>(opts => ProviderConfigurator.Configure(opts, dashboardOpts.Db.Provider, dashboardOpts.Db.ConnectionString));

        builder.Services.AddHostedService<SchemaBootstrap>();
        builder.Services.AddHostedService<AuthBootstrap>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(opts =>
            {
                opts.Cookie.Name = "duckrun.sid";
                opts.Cookie.HttpOnly = true;
                opts.Cookie.SameSite = SameSiteMode.Lax;
                opts.SlidingExpiration = true;
                opts.ExpireTimeSpan = TimeSpan.FromDays(30);
                opts.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                opts.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();

        builder.Services.AddScoped<ApiKeyAuthInterceptor>();
        builder.Services.AddGrpc(o =>
        {
            o.Interceptors.Add<ApiKeyAuthInterceptor>();
            o.EnableDetailedErrors = builder.Environment.IsDevelopment();
        });

        builder.Services.AddSignalR();
        builder.Services.AddRouting();

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", async (IDbContextFactory<DashboardDbContext> f) =>
        {
            try { await using var ctx = await f.CreateDbContextAsync(); await ctx.Database.CanConnectAsync(); return Results.Ok(new { status = "ready" }); }
            catch { return Results.StatusCode(503); }
        });

        app.MapAuthEndpoints();
        app.MapGroupEndpoints();
        app.MapProjectEndpoints();
        app.MapDsnEndpoints();
        app.MapKpiEndpoints();

        app.MapHub<ControlHub>("/hubs/control");

        app.MapGrpcService<IngestServiceImpl>();

        app.MapFallbackToFile("index.html");

        app.Run();
    }
}
