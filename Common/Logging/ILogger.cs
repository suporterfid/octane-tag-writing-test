namespace Common.Logging;

public interface ILogger
{
    void Debug(string messageTemplate, params object[] args);
    void Information(string messageTemplate, params object[] args);
    void Warning(string messageTemplate, params object[] args);
    void Warning(System.Exception exception, string messageTemplate, params object[] args);
    void Error(string messageTemplate, params object[] args);
    void Error(System.Exception exception, string messageTemplate, params object[] args);
}
