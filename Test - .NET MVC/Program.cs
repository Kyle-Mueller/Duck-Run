using DuckRun.EfCore;
using DuckRun.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDuckRun(o =>
{
    o.AddJobsFromAssembly(typeof(Program).Assembly);
    o.UseStandaloneDashboard("/duckrun");
    o.UseEfCore("Host=localhost;Port=5432;Database=duckrun;Username=duckrun;Password=duckrun", DuckRunProvider.Postgres);
    o.UseRedis("127.0.0.1:6379,abortConnect=false", projectId: "duckrun-test");
    o.UseDashboard("http://gPtCgBPO7CfkZBdqsGMYhreSgAnZ5HY4Tao9jfcjEuw@localhost:8091/88cd0322-5731-4015-a20e-1e1b0907dccb");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapDuckRunDashboard();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
