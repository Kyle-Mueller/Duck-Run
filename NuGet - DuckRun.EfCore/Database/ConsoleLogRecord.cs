namespace DuckRun.EfCore.Database;

internal sealed class ConsoleLogRecord
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = "";
}
