using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.IO;
using Common.Logging;

namespace Common.Logging.Sinks;

/// <summary>
/// Batches CSV log events by file and writes them asynchronously.
/// </summary>
public sealed class CsvSink : ILogSink, IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, Channel<CsvPayload>> _channels = new();
    private readonly ConcurrentDictionary<string, Task> _tasks = new();

    public void Write(LogEvent evt)
    {
        if (evt.CsvPayload is not { } payload)
        {
            return;
        }

        var channel = _channels.GetOrAdd(payload.FilePath, path =>
        {
            var ch = Channel.CreateUnbounded<CsvPayload>();
            var task = Task.Run(() => ProcessChannel(path, ch));
            _tasks[path] = task;
            return ch;
        });

        channel.Writer.TryWrite(payload);
    }

    private static async Task ProcessChannel(string path, Channel<CsvPayload> channel)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var exists = File.Exists(path);
        await using var stream = new StreamWriter(path, append: true, Encoding.UTF8);
        var batch = new List<string>(64);

        await foreach (var payload in channel.Reader.ReadAllAsync())
        {
            if (!exists)
            {
                await stream.WriteLineAsync(payload.Header);
                exists = true;
            }

            batch.Add(payload.Line);
            if (batch.Count >= 50)
            {
                foreach (var line in batch)
                {
                    await stream.WriteLineAsync(line);
                }
                await stream.FlushAsync();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            foreach (var line in batch)
            {
                await stream.WriteLineAsync(line);
            }
            await stream.FlushAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Writer.Complete();
        }

        if (_tasks.Count > 0)
        {
            await Task.WhenAll(_tasks.Values).ConfigureAwait(false);
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
