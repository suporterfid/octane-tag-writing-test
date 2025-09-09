using System;

namespace Common.Logging;

[Serializable]
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

[Serializable]
public readonly record struct CsvPayload(string FilePath, string Header, string Line);

[Serializable]
public record LogEvent
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string MessageTemplate { get; init; }
    public object[]? Parameters { get; init; }
    public CsvPayload? CsvPayload { get; init; }

    public LogEvent(DateTime timestamp, LogLevel level, string messageTemplate, object[]? parameters = null, CsvPayload? csvPayload = null)
    {
        Timestamp = timestamp;
        Level = level;
        MessageTemplate = messageTemplate;
        Parameters = parameters is null ? null : (object[])parameters.Clone();
        CsvPayload = csvPayload;
    }
}
