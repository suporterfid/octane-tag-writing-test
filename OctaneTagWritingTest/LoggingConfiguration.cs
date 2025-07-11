using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace OctaneTagWritingTest
{
    /// <summary>
    /// Centralized logging configuration for the RFID Tag Writing application
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>
        /// Configures Serilog with structured logging for console and file outputs
        /// </summary>
        /// <param name="testDescription">Test description to include in log file names</param>
        /// <param name="logLevel">Minimum log level (default: Information)</param>
        public static void ConfigureLogging(string testDescription = "default", LogEventLevel logLevel = LogEventLevel.Information)
        {
            var logFileName = $"logs/octane-tag-writing-{testDescription}-{DateTime.Now:yyyyMMdd-HHmmss}.log";
            var errorLogFileName = $"logs/octane-tag-writing-{testDescription}-errors-{DateTime.Now:yyyyMMdd-HHmmss}.log";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.WithProperty("Application", "OctaneTagWritingTest")
                .Enrich.WithProperty("TestDescription", testDescription)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(
                    path: logFileName,
                    formatter: new CompactJsonFormatter(),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true)
                .WriteTo.File(
                    path: errorLogFileName,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    formatter: new CompactJsonFormatter(),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true)
                .CreateLogger();

            Log.Information("Logging configured for {Application} with test description: {TestDescription}", 
                "OctaneTagWritingTest", testDescription);
        }

        /// <summary>
        /// Creates a logger with additional context properties
        /// </summary>
        /// <param name="contextProperties">Additional properties to enrich the logger</param>
        /// <returns>Contextual logger instance</returns>
        public static ILogger CreateContextLogger(params (string Key, object Value)[] contextProperties)
        {
            var logger = Log.Logger;
            
            foreach (var (key, value) in contextProperties)
            {
                logger = logger.ForContext(key, value);
            }
            
            return logger;
        }

        /// <summary>
        /// Creates a logger for a specific component/class
        /// </summary>
        /// <typeparam name="T">The type to create logger for</typeparam>
        /// <returns>Logger with source context</returns>
        public static ILogger CreateLogger<T>()
        {
            return Log.ForContext<T>();
        }

        /// <summary>
        /// Creates a logger for tag operations with common properties
        /// </summary>
        /// <param name="tid">Tag ID</param>
        /// <param name="operation">Operation type</param>
        /// <returns>Logger with tag operation context</returns>
        public static ILogger CreateTagOperationLogger(string tid, string operation)
        {
            return Log.ForContext("TID", tid)
                     .ForContext("Operation", operation)
                     .ForContext("Component", "TagOperation");
        }

        /// <summary>
        /// Creates a logger for reader operations
        /// </summary>
        /// <param name="readerName">Reader name (detector, writer, verifier)</param>
        /// <param name="hostname">Reader hostname</param>
        /// <returns>Logger with reader context</returns>
        public static ILogger CreateReaderLogger(string readerName, string hostname)
        {
            return Log.ForContext("ReaderName", readerName)
                     .ForContext("ReaderHostname", hostname)
                     .ForContext("Component", "Reader");
        }

        /// <summary>
        /// Creates a logger for job strategy operations
        /// </summary>
        /// <param name="strategyName">Strategy name</param>
        /// <returns>Logger with strategy context</returns>
        public static ILogger CreateStrategyLogger(string strategyName)
        {
            return Log.ForContext("Strategy", strategyName)
                     .ForContext("Component", "JobStrategy");
        }

        /// <summary>
        /// Closes and flushes the logger
        /// </summary>
        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
