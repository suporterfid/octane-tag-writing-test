using Common.Logging;

namespace Common.Logging.Sinks;

public interface ILogSink
{
    void Write(LogEvent evt);
}
