using DuckRun.Core.Cluster;
using DuckRun.Core.Logging;
using DuckRun.Core.Reporting;
using DuckRun.Core.Runs;
using Microsoft.Extensions.Hosting;

namespace DuckRun.Framework;

/// <summary>
/// Mutable bag passed to companion-package setup callbacks (DuckRun.Redis, DuckRun.EfCore, ...).
/// Each module can swap in its own implementation of the abstractions and register lifecycle services.
/// Internal — only companion packages (with <c>InternalsVisibleTo</c>) and the host itself see this.
/// </summary>
internal sealed class DuckRunFrameworkContext
{
    private readonly List<IHostedService> _hosted = new();

    public IClusterCoordinator Coordinator { get; set; } = new LocalClusterCoordinator();
    public IDashboardReporter Reporter { get; set; } = new NullDashboardReporter();
    public IJobRunStore? RunStoreOverride { get; set; }
    public IConsoleStore? ConsoleStoreOverride { get; set; }

    public void AddHostedService(IHostedService service)
    {
        if (service is null) throw new ArgumentNullException(nameof(service));
        _hosted.Add(service);
    }

    public IReadOnlyList<IHostedService> HostedServices => _hosted;
}
