using Serilog;
using Serilog.Events;
using LogEvent = Common.Logging.LogEvent;
using LogLevel = Common.Logging.LogLevel;

namespace Common.Logging.Sinks;

public sealed class SerilogSink : ILogSink
{
    public void Write(LogEvent evt)
    {
        var level = evt.Level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };

        ILogger logger = Log.Logger;
        if (evt.Context is { } ctx)
        {
            foreach (var (key, value) in ctx)
            {
                logger = logger.ForContext(key, value);
            }
        }

        if (evt.Parameters is { Length: > 0 })
        {
            if (evt.Exception is not null)
            {
                logger.Write(level, evt.Exception, evt.MessageTemplate, evt.Parameters);
            }
            else
            {
                logger.Write(level, evt.MessageTemplate, evt.Parameters);
            }
        }
        else
        {
            if (evt.Exception is not null)
            {
                logger.Write(level, evt.Exception, evt.MessageTemplate);
            }
            else
            {
                logger.Write(level, evt.MessageTemplate);
            }
        }
    }
}
