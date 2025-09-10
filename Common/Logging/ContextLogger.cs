using System;
using System.Collections.Generic;

namespace Common.Logging;

internal sealed class ContextLogger : ILogger
{
    private readonly LoggingService _service;
    private readonly IReadOnlyList<(string Key, object Value)> _context;

    public ContextLogger(LoggingService service, IReadOnlyList<(string Key, object Value)> context)
    {
        _service = service;
        _context = context;
    }

    private void Log(LogLevel level, string messageTemplate, Exception? exception, object[] args)
    {
        _service.Log(level, messageTemplate, args, exception, _context);
    }

    public void Debug(string messageTemplate, params object[] args) => Log(LogLevel.Debug, messageTemplate, null, args);
    public void Information(string messageTemplate, params object[] args) => Log(LogLevel.Information, messageTemplate, null, args);
    public void Warning(string messageTemplate, params object[] args) => Log(LogLevel.Warning, messageTemplate, null, args);
    public void Warning(Exception exception, string messageTemplate, params object[] args) => Log(LogLevel.Warning, messageTemplate, exception, args);
    public void Error(string messageTemplate, params object[] args) => Log(LogLevel.Error, messageTemplate, null, args);
    public void Error(Exception exception, string messageTemplate, params object[] args) => Log(LogLevel.Error, messageTemplate, exception, args);
}
