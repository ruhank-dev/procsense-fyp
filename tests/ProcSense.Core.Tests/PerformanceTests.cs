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

        // DT-022: the exact zero above holds for a quiet session only. With every subsystem running and the monitor noise on, a tick costs about 380 bytes (measured 17 Oct 2026, Release):
        // the problem-discovery rules build their state every 20 ticks (about 190 bytes a tick on average) and the noise model builds a little each step. At the real-time rate of 2 ticks a second
        // that is under 1 KB a second, so the test holds a budget of 600 bytes and not zero; it fails if a subsystem starts allocating much more.
        [Theory]
        [InlineData("VAR-S2-PD")]
        [InlineData("STK-S2-B")]
        [InlineData("VAR-S1-J")]
        public void A_tick_allocates_little_in_the_busiest_cases_with_every_subsystem_running(string caseId)
        {
            var all = ProcSense.Core.Content.ContentLoader.Load(new ProcSense.Core.Content.FileContentReader(TestCase.ContentRoot), caseId);
            var session = new Session(all.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(all), new SessionOptions(Noise: true));
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) session.Apply(new SimAction(v, "patient", Channel.Cli));
            for (int i = 0; i < 400; i++) session.Tick(0.5);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Ticks; i++) session.Tick(0.5);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            _out.WriteLine($"{caseId}: allocated {allocated} bytes over {Ticks} ticks");
            Assert.True(allocated < 600L * Ticks, $"{caseId}: {allocated} bytes allocated over {Ticks} ticks ({allocated / (double)Ticks:F1} per tick, budget 600)");
        }

        // The observable is built when the view asks (not every tick). Measured so the cost of asking once per frame is known.
        [Fact]
        public void Building_the_observable_once_per_frame_fits_in_a_frame()
        {
            var session = NewSession();
            for (int i = 0; i < 200; i++) session.Tick(0.5);
            _ = session.Observable;
            var clock = Stopwatch.StartNew();
            const int Builds = 200;
            for (int i = 0; i < Builds; i++) _ = session.Observable;
            clock.Stop();
            double ms = clock.Elapsed.TotalMilliseconds / Builds;
            _out.WriteLine($"{ms:F4} ms per observable");
            Assert.True(ms < 5.0, $"{ms:F3} ms per observable build");     // far inside a 13.9 ms frame at 72 fps, with room for a slow runner
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

            // Best of five runs: other tests and the CI tools run alongside, and one noisy run says nothing about the engine.
            double best = double.MaxValue;
            for (int run = 0; run < 5; run++)
            {
                var clock = Stopwatch.StartNew();
                for (int i = 0; i < Ticks; i++) session.Apply(new SimAction("wait", "patient", Channel.Cli));
                clock.Stop();
                best = Math.Min(best, clock.Elapsed.TotalMilliseconds / Ticks);
            }

            _out.WriteLine($"{best:F4} ms per action");
            Assert.True(best < 1.0, $"{best:F3} ms per action");
        }
    }
}
