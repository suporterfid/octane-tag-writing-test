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
    public System.Exception? Exception { get; init; }
    public System.Collections.Generic.IReadOnlyList<(string Key, object Value)>? Context { get; init; }

    public LogEvent(
        DateTime timestamp,
        LogLevel level,
        string messageTemplate,
        object[]? parameters = null,
        CsvPayload? csvPayload = null,
        System.Collections.Generic.IReadOnlyList<(string Key, object Value)>? context = null,
        System.Exception? exception = null)
    {
        Timestamp = timestamp;
        Level = level;
        MessageTemplate = messageTemplate;
        Parameters = parameters is null ? null : (object[])parameters.Clone();
        CsvPayload = csvPayload;
        Context = context;
        Exception = exception;
    }
}
