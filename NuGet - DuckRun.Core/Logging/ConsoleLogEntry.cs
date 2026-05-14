namespace DuckRun.Core;

public sealed record ConsoleLogEntry(Guid RunId, DateTimeOffset Timestamp, DuckRunLogLevel Level, string Message);
