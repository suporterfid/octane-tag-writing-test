using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Sinks;

namespace Logging.Tests;

public class CsvSinkTests
{
    private static LoggingService CreateService(params ILogSink[] sinks)
    {
        var service = (LoggingService)Activator.CreateInstance(typeof(LoggingService), nonPublic: true)!;
        service.Start(new LoggingConfiguration(sinks));
        return service;
    }

    [Test]
    public async Task CsvSink_Writes_Header_Once_And_Batches_Lines()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "log.csv");
        var sink = new CsvSink();
        var svc = CreateService(sink);
        for (var i = 0; i < 60; i++)
        {
            svc.LogCsv(file, "col1,col2", $"{i},data{i}");
        }
        await svc.FlushAsync();
        var lines = await File.ReadAllLinesAsync(file);
        Assert.That(lines[0], Is.EqualTo("col1,col2"));
        var expected = Enumerable.Range(0, 60).Select(i => $"{i},data{i}");
        Assert.That(lines.Skip(1), Is.EqualTo(expected));
        Assert.That(lines.Count(l => l == "col1,col2"), Is.EqualTo(1));
    }
}
