using DuckRun.Core;
using DuckRun.Core.Jobs;
using DuckRun.Core.Logging;
using DuckRun.Core.Runs;
using DuckRun.Framework.Operations;
using DuckRun.Framework.Runs;
using DuckRun.Framework.Scheduler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DuckRun.Framework;

/// <summary>
/// Process-wide entry point for DuckRun on classic .NET Framework. Call <see cref="Start"/> from
/// <c>Global.asax</c>'s <c>Application_Start</c> and <see cref="Stop"/> from <c>Application_End</c>.
/// </summary>
public static class DuckRunHost
{
    private static readonly object _lock = new();
    private static DuckRunInstance? _current;

    /// <summary>Loggerfactory used by the host. Set from your app's logging setup if you have one.</summary>
    public static ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

    public static void Start(Action<DuckRunOptionsBuilder> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        lock (_lock)
        {
            if (_current is not null) throw new InvalidOperationException("DuckRun has already been started in this process.");

            var builder = new DuckRunOptionsBuilder();
            configure(builder);
            var options = builder.Build();
            _current = DuckRunInstance.Build(options, LoggerFactory);
            _current.Start();
        }
    }

    public static void Stop()
    {
        lock (_lock)
        {
            _current?.Stop();
            _current = null;
        }
    }

    /// <summary>Manual trigger/cancel + read access for jobs. Null until <see cref="Start"/> is called.</summary>
    public static IDuckRunController? Controller => _current?.Controller;
}

internal sealed class DuckRunInstance
{
    private readonly SchedulerLoop _scheduler;
    private readonly IReadOnlyList<global::Microsoft.Extensions.Hosting.IHostedService> _hosted;
    public IDuckRunController Controller { get; }

    private DuckRunInstance(SchedulerLoop scheduler, IDuckRunController controller, IReadOnlyList<global::Microsoft.Extensions.Hosting.IHostedService> hosted)
    {
        _scheduler = scheduler;
        Controller = controller;
        _hosted = hosted;
    }

    public static DuckRunInstance Build(DuckRunOptions options, ILoggerFactory loggers)
    {
        var registry = JobScanner.Build(options.AssembliesToScan, options.ExplicitJobs);
        var runStore = new InMemoryJobRunStore(options.RunsRetainedPerJob);
        var consoleStore = new InMemoryConsoleStore(options.ConsoleEntriesPerRun);

        var ctx = new DuckRunFrameworkContext();
        foreach (var setup in options.ModuleSetups) setup(ctx);

        IJobRunStore runs = ctx.RunStoreOverride ?? runStore;
        IConsoleStore console = ctx.ConsoleStoreOverride ?? consoleStore;

        var trackers = new LocalConcurrencyTrackers();
        var slotGate = new JobSlotGate(trackers, ctx.Coordinator);

        var executor = new JobExecutor(runs, console, ctx.Reporter, options.JobFactory, loggers.CreateLogger<JobExecutor>());
        var scheduler = new SchedulerLoop(registry, executor, slotGate, ctx.Coordinator, loggers.CreateLogger<SchedulerLoop>());
        var controller = new DuckRunController(registry, executor, runs, console, slotGate, scheduler, loggers.CreateLogger<DuckRunController>());

        return new DuckRunInstance(scheduler, controller, ctx.HostedServices);
    }

    public void Start()
    {
        foreach (var svc in _hosted) svc.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        _scheduler.Start();
    }

    public void Stop()
    {
        _scheduler.Stop();
        foreach (var svc in _hosted.Reverse())
        {
            try { svc.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
            catch { /* best effort */ }
        }
    }
}
