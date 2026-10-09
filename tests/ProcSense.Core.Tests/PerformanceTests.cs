using System;
using System.Diagnostics;
using ProcSense.Core.Actions;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// Quest budget guards (experiment EXP-CORE-3). The time budget is deliberately generous: shared CI machines are slow and noisy,
    /// so it only catches something badly wrong. The allocation check is exact and is the one that matters on a headset.
    /// </summary>
    public class PerformanceTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public PerformanceTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

        private const int Ticks = 1000;

        private static Session NewSession()
        {
            var content = TestCase.Package();
            return new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
        }

        [Fact]
        public void A_tick_allocates_no_memory_after_warm_up()
        {
            var session = NewSession();
            for (int i = 0; i < 200; i++) session.Tick(0.5);   // warm-up: JIT, caches

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Ticks; i++) session.Tick(0.5);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            _out.WriteLine($"allocated {allocated} bytes over {Ticks} ticks");
            Assert.True(allocated == 0, $"{allocated} bytes allocated over {Ticks} ticks ({allocated / (double)Ticks:F1} per tick)");
        }

        [Fact]
        public void A_thousand_ticks_take_far_less_than_a_frame_budget()
        {
            var session = NewSession();
            for (int i = 0; i < 200; i++) session.Tick(0.5);

            var clock = Stopwatch.StartNew();   // test code only: Core itself never reads real time
            for (int i = 0; i < Ticks; i++) session.Tick(0.5);
            clock.Stop();

            _out.WriteLine($"{clock.Elapsed.TotalMilliseconds / Ticks:F4} ms per tick");
            // One tick must fit easily inside a 72 fps frame (13.9 ms); require an average under 1 ms even on a slow runner.
            Assert.True(clock.Elapsed.TotalMilliseconds / Ticks < 1.0, $"{clock.Elapsed.TotalMilliseconds / Ticks:F3} ms per tick");
        }

        [Fact]
        public void Applying_an_action_stays_fast()
        {
            var session = NewSession();
            for (int i = 0; i < 50; i++) session.Apply(new SimAction("wait", "patient", Channel.Cli));

            var clock = Stopwatch.StartNew();
            for (int i = 0; i < Ticks; i++) session.Apply(new SimAction("wait", "patient", Channel.Cli));
            clock.Stop();

            _out.WriteLine($"{clock.Elapsed.TotalMilliseconds / Ticks:F4} ms per action");
            Assert.True(clock.Elapsed.TotalMilliseconds / Ticks < 1.0, $"{clock.Elapsed.TotalMilliseconds / Ticks:F3} ms per action");
        }
    }
}
