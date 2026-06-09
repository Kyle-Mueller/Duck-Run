using DuckRun.Core;

namespace DuckRun.Tests;

// Discovered by JobScannerTests via assembly scan. Every [DuckRunJob] name in this
// assembly must stay unique — a duplicate would make JobScanner.Build throw for every
// test that scans the assembly.
internal sealed class SampleJobs
{

    [DuckRunJob("sample-heartbeat", "*/5 * * * *", MaxConcurrency = 3, TimeoutSeconds = 30)]
    public void Heartbeat() { }

    [DuckRunJob("sample-import", "0 0 * * *", MaxConcurrency = 1)]
    public Task Import(CancellationToken ct) => Task.CompletedTask;

    [DuckRunJob("sample-unlimited", "* * * * *", MaxConcurrency = 0)]
    public void Unlimited() { }
}
