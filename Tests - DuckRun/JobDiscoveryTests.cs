using DuckRun.Core;
using DuckRun.Core.Jobs;

namespace DuckRun.Tests;

public class JobDiscoveryTests
{

    private static IJobRegistry Scan() => JobScanner.Build([typeof(SampleJobs).Assembly], []);

    private static JobDescriptor Descriptor(string name) => new() { Name = name, Cron = "* * * * *", DeclaringType = typeof(SampleJobs), Method = typeof(SampleJobs).GetMethod(nameof(SampleJobs.Heartbeat))! };

    [Fact]
    public void Build_DiscoversAttributedMethod_WithResolvedMetadata()
    {
        var job = Scan().FindByName("sample-heartbeat");
        Assert.NotNull(job);
        Assert.Equal("*/5 * * * *", job!.Cron);
        Assert.Equal(3, job.MaxConcurrency);
        Assert.Equal(TimeSpan.FromSeconds(30), job.Timeout);
    }

    [Fact]
    public void Build_ZeroMaxConcurrency_BecomesUnbounded()
    {
        var job = Scan().FindByName("sample-unlimited");
        Assert.NotNull(job);
        Assert.Equal(int.MaxValue, job!.MaxConcurrency);
    }

    [Fact]
    public void Build_NoTimeoutSpecified_LeavesTimeoutNull()
    {
        var job = Scan().FindByName("sample-import");
        Assert.NotNull(job);
        Assert.Null(job!.Timeout);
    }

    [Fact]
    public void FindByName_IsCaseInsensitive() => Assert.NotNull(Scan().FindByName("SAMPLE-HEARTBEAT"));

    [Fact]
    public void Registry_DuplicateName_Throws() => Assert.Throws<InvalidOperationException>(() => new JobRegistry([Descriptor("dup"), Descriptor("dup")]));

    [Fact]
    public void Registry_DuplicateNameDifferingOnlyByCase_Throws() => Assert.Throws<InvalidOperationException>(() => new JobRegistry([Descriptor("dup"), Descriptor("DUP")]));
}
