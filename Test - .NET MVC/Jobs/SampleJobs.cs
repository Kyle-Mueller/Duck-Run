using DuckRun.Core;

namespace Test___.NET_MVC.Jobs;

public sealed class SampleJobs
{
    private readonly ILogger<SampleJobs> _logger;

    public SampleJobs(ILogger<SampleJobs> logger)
    {
        _logger = logger;
    }

    [DuckRunJob("heartbeat", "*/30 * * * * *")]
    public Task Heartbeat(IDuckRunConsole console, CancellationToken ct)
    {
        console.Info("heartbeat tick");
        return Task.CompletedTask;
    }

    [DuckRunJob("slow-import", "*/2 * * * *", MaxConcurrency = 1, TimeoutSeconds = 30)]
    public async Task SlowImport(IDuckRunConsole console, CancellationToken ct)
    {
        console.Info("starting slow-import");
        for (var i = 1; i <= 10; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            console.Info($"processed batch {i}/10");
        }
        console.Info("slow-import complete");
        _logger.LogInformation("slow-import finished");
    }

    [DuckRunJob("flaky-report", "*/3 * * * *")]
    public Task FlakyReport(IDuckRunConsole console)
    {
        console.Info("generating report");
        if (Random.Shared.Next(0, 3) == 0)
        {
            console.Warning("data source is degraded");
            throw new InvalidOperationException("upstream timeout while assembling report");
        }
        console.Info("report ok");
        return Task.CompletedTask;
    }
}
