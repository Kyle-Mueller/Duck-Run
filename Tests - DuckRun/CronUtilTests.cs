using DuckRun.Core.Scheduler;

namespace DuckRun.Tests;

public class CronUtilTests
{

    [Fact]
    public void FiveFieldEveryMinute_ReturnsNextMinute()
    {
        var after = new DateTimeOffset(2026, 1, 1, 12, 0, 30, TimeSpan.Zero);
        var next = CronUtil.GetNextOccurrence("* * * * *", after, TimeZoneInfo.Utc);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void SixFieldEverySecond_IsParsedWithSeconds()
    {
        var after = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var next = CronUtil.GetNextOccurrence("* * * * * *", after, TimeZoneInfo.Utc);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 1, TimeSpan.Zero), next);
    }

    [Fact]
    public void DailyAtMidnight_ReturnsNextMidnight()
    {
        var after = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var next = CronUtil.GetNextOccurrence("0 0 * * *", after, TimeZoneInfo.Utc);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void InvalidExpression_Throws() => Assert.ThrowsAny<Exception>(() => CronUtil.GetNextOccurrence("not a cron", DateTimeOffset.UtcNow, TimeZoneInfo.Utc));
}
