using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Sinks;

namespace Logging.Tests;

public class LoggingServiceTests
{
    private static LoggingService CreateService(params ILogSink[] sinks)
    {
        var service = (LoggingService)Activator.CreateInstance(typeof(LoggingService), nonPublic: true)!;
        service.Start(new LoggingConfiguration(sinks));
        return service;
    }

    private sealed class TestSink : ILogSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Write(LogEvent evt) => Events.Add(evt);
    }

    private sealed class SlowSink : ILogSink
    {
        private readonly int _delayMs;
        public List<LogEvent> Events { get; } = new();
        public SlowSink(int delayMs) => _delayMs = delayMs;
        public void Write(LogEvent evt)
        {
            Thread.Sleep(_delayMs);
            Events.Add(evt);
        }
    }

    [Test]
    public async Task Events_Are_Processed_In_FIFO_Order()
    {
        var sink = new TestSink();
        var svc = CreateService(sink);
        svc.LogInfo("first");
        svc.LogInfo("second");
        svc.LogInfo("third");
        await svc.FlushAsync();
        var templates = sink.Events.Select(e => e.MessageTemplate).ToList();
        Assert.That(templates, Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public async Task FlushAsync_Drains_Remaining_Events()
    {
        var sink = new SlowSink(10);
        var svc = CreateService(sink);
        for (var i = 0; i < 20; i++)
        {
            svc.LogInfo("msg {0}", i);
        }
        await svc.FlushAsync();
        Assert.That(sink.Events.Count, Is.EqualTo(20));
    }
}
