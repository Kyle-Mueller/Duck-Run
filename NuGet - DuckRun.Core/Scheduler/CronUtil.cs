using Cronos;

namespace DuckRun.Core.Scheduler;

internal static class CronUtil
{
    public static DateTimeOffset? GetNextOccurrence(string cron, DateTimeOffset after, TimeZoneInfo? timezone = null)
    {
        var tz = timezone ?? TimeZoneInfo.Local;
        var fieldCount = cron.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
        var expr = CronExpression.Parse(cron, format);
        var next = expr.GetNextOccurrence(after.UtcDateTime, tz);
        return next.HasValue ? new DateTimeOffset(next.Value, TimeSpan.Zero) : null;
    }
}
