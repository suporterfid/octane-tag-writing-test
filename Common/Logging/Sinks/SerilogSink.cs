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

        if (evt.Parameters is { Length: > 0 })
        {
            Log.Write(level, evt.MessageTemplate, evt.Parameters);
        }
        else
        {
            Log.Write(level, evt.MessageTemplate);
        }
    }
}
