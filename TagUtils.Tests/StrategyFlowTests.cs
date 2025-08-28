using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Serilog;
using Serilog.Sinks.InMemory;
using Snapshooter.NUnit;

namespace TagUtils.Tests;

public class StrategyFlowTests
{
    private class InstrumentedStrategy
    {
        private readonly ILogger _logger = Log.ForContext("Strategy", "Test");
        public List<string> Steps { get; } = new();

        public void Start()
        {
            _logger.Information("[FLOW] Start");
            Steps.Add("start");
        }

        public void Configure()
        {
            _logger.Information("[FLOW] Configure");
            Steps.Add("configure");
        }

        public void Run()
        {
            _logger.Information("[FLOW] Run");
            Steps.Add("run");
        }

        public void Stop()
        {
            _logger.Information("[FLOW] Stop");
            Steps.Add("stop");
        }
    }

    [Test]
    public void StrategyFlow_LogsInExpectedOrder()
    {
        var logger = new LoggerConfiguration().WriteTo.InMemory().CreateLogger();
        Log.Logger = logger;

        var strategy = new InstrumentedStrategy();
        strategy.Start();
        strategy.Configure();
        strategy.Run();
        strategy.Stop();

        var logMessages = InMemorySink.Instance.LogEvents.Select(e => e.RenderMessage()).ToList();
        Snapshot.Match(logMessages);

        CollectionAssert.AreEqual(new[] { "start", "configure", "run", "stop" }, strategy.Steps);
    }
}
