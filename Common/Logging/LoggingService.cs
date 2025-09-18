using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Common.Logging.Sinks;

namespace Common.Logging;

/// <summary>
/// Asynchronous logging service backed by a <see cref="Channel{T}"/>.
/// </summary>
public sealed class LoggingService : IDisposable, IAsyncDisposable
{
    private static readonly Lazy<LoggingService> _lazy = new(() => new LoggingService());

    /// <summary>
    /// Singleton instance of the logging service.
    /// </summary>
    public static LoggingService Instance => _lazy.Value;

    private Channel<LogEvent>? _channel;
    private Task? _consumerTask;
    private IReadOnlyList<ILogSink>? _sinks;
    private CancellationTokenSource? _cts;

    private LoggingService() { }

    /// <summary>
    /// Starts the logging service with the provided configuration.
    /// </summary>
    /// <param name="cfg">Logging configuration containing sinks and capacity.</param>
    public void Start(LoggingConfiguration cfg)
    {
        if (_channel != null)
        {
            throw new InvalidOperationException("LoggingService already started.");
        }

        _sinks = cfg.Sinks;
        _cts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<LogEvent>(cfg.ChannelCapacity);
        _consumerTask = Task.Run(async () =>
        {
            await foreach (var logEvent in _channel.Reader.ReadAllAsync(_cts.Token))
              {
                  foreach (var sink in _sinks)
                  {
                      sink.Write(logEvent);
                  }
              }
        }, _cts.Token);
    }

    public ILogger CreateContextLogger(params (string Key, object Value)[] contextProperties)
        => new ContextLogger(this, contextProperties);

    public ILogger CreateLogger<T>()
        => CreateContextLogger(("SourceContext", typeof(T).FullName ?? typeof(T).Name));

    /// <summary>
    /// Enqueues a log event to be processed by the background consumer.
    /// </summary>
    public void Log(LogLevel level, string template, object[] args, Exception? exception = null, IReadOnlyList<(string Key, object Value)>? context = null)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException("LoggingService has not been started.");
        }

        var logEvent = new LogEvent(DateTime.UtcNow, level, template, args, null, context, exception);
        _channel.Writer.TryWrite(logEvent);
    }

    /// <summary>
    /// Enqueues a CSV log event for asynchronous processing.
    /// </summary>
    /// <param name="file">Destination CSV file path.</param>
    /// <param name="header">CSV header to ensure is written once per file.</param>
    /// <param name="line">CSV data line to append.</param>
    public void LogCsv(string file, string header, string line)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException("LoggingService has not been started.");
        }

        var logEvent = new LogEvent(DateTime.UtcNow, LogLevel.Information, string.Empty, null,
            new CsvPayload(file, header, line));
        _channel.Writer.TryWrite(logEvent);
    }

    public void LogTrace(string template, params object[] args) => Log(LogLevel.Trace, template, args);
    public void LogDebug(string template, params object[] args) => Log(LogLevel.Debug, template, args);
    public void LogInfo(string template, params object[] args) => Log(LogLevel.Information, template, args);
    public void LogWarning(string template, params object[] args) => Log(LogLevel.Warning, template, args);
    public void LogError(string template, params object[] args) => Log(LogLevel.Error, template, args);
    public void LogCritical(string template, params object[] args) => Log(LogLevel.Critical, template, args);

      /// <summary>
      /// Flushes all queued log events and waits for the consumer to finish.
      /// </summary>
      public async Task FlushAsync()
      {
          var channel = _channel;
          if (channel is null)
          {
              return;
          }

          var consumerTask = _consumerTask;
          var sinks = _sinks;
          var cts = _cts;

          channel.Writer.Complete();

          if (consumerTask is not null)
          {
              await consumerTask.ConfigureAwait(false);
          }

          if (sinks is not null)
          {
              foreach (var sink in sinks)
              {
                  switch (sink)
                  {
                      case IAsyncDisposable asyncDisposable:
                          await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                          break;
                      case IDisposable disposable:
                          disposable.Dispose();
                          break;
                  }
              }
          }

          cts?.Cancel();
          cts?.Dispose();

          _channel = null;
          _consumerTask = null;
          _sinks = null;
          _cts = null;
      }

    /// <summary>
    /// Stops the logging service and flushes any remaining events.
    /// </summary>
    public void Stop() => FlushAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await FlushAsync().ConfigureAwait(false);
        _cts?.Dispose();
    }
}

/// <summary>
/// Configuration for <see cref="LoggingService"/>.
/// </summary>
/// <param name="Sinks">Collection of sinks to receive log events.</param>
/// <param name="ChannelCapacity">Bounded channel capacity.</param>
public record LoggingConfiguration(IReadOnlyList<ILogSink> Sinks, int ChannelCapacity = 1024);

