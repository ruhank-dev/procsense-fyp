using System;
using System.Collections.Generic;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;
using Xunit.Abstractions;

namespace ProcSense.Core.Tests
{
    public class SessionTests
    {
        private readonly ITestOutputHelper _out;

        public SessionTests(ITestOutputHelper output) { _out = output; }

        /// <summary>Test subsystem: "ping" echoes a random draw; "block" is always rejected.</summary>
        private sealed class Echo : ISubsystem
        {
            private ProcSense.Core.Random.SimRandom _rng = null!;
            public List<string> Trace { get; } = new List<string>();
            public Echo(int order = PipelineOrder.Clinical) { Order = order; }
            public int Order { get; }

            public void Init(Session s)
            {
                s.Verbs.Register("ping");
                s.Verbs.Register("block");
                _rng = s.Random.Stream("events");
            }

            public void OnAction(SimAction a, ActionOutcome o)
            {
                if (a.Verb == "block") o.Reject("NOT_ALLOWED");
                else o.Effects.Add("pong:" + _rng.NextInt(1000));
            }

            public void OnTick() => Trace.Add("tick");
        }

        private static Session NewSession(params ISubsystem[] subs) =>
            new Session(TestCase.Card(), 42, SessionMode.Practice, subs);

        [Fact] // T-K2
        public void Unknown_verb_is_rejected_and_logged()
        {
            var s = NewSession(new Echo());

            var outcome = s.Apply(new SimAction("dance"));

            Assert.False(outcome.Accepted);
            Assert.Equal("UNKNOWN_VERB", outcome.RejectReason);
            Assert.Contains("\"reject\":\"UNKNOWN_VERB\"", s.Log.ToJsonl());
        }

        [Fact]
        public void Rejected_action_is_logged_with_its_reason()
        {
            var s = NewSession(new Echo());

            s.Apply(new SimAction("block"));

            Assert.Equal(1, s.Log.Count);
            Assert.Contains("\"accepted\":false,\"reject\":\"NOT_ALLOWED\"", s.Log.ToJsonl());
        }

        [Fact] // T-K1 (initial version)
        public void Same_seed_and_actions_give_identical_log_hash()
        {
            string Run()
            {
                var s = NewSession(new Echo());
                for (int i = 0; i < 100; i++)
                {
                    s.Tick(0.73);
                    s.Apply(new SimAction(i % 10 == 9 ? "block" : "ping", "t" + i));
                }
                return s.Log.Hash();
            }

            string a = Run(), b = Run();
            _out.WriteLine("log hash: " + a);

            Assert.Equal(a, b);
        }

        [Fact]
        public void Log_hash_matches_independently_computed_value()
        {
            var s = NewSession(new Echo());
            s.Apply(new SimAction("ping", "x", Channel.Cli, new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" }));
            s.Apply(new SimAction("bogus"));

            // Same JSONL hashed with Python's hashlib: proves the format is canonical, not just self-consistent.
            Assert.Equal("4b9885398986f345084e6efc1d13dfb49f0be1ebba461d0b57e6de6eb542ae82", s.Log.Hash());
        }

        [Fact]
        public void Pipeline_runs_in_fixed_order_not_registration_order()
        {
            var order = new List<int>();
            var late = new Probe(PipelineOrder.Scorer, order);
            var early = new Probe(PipelineOrder.Clinical, order);
            var s = NewSession(late, early);

            s.Apply(new SimAction("probe"));

            Assert.Equal(new[] { PipelineOrder.Clinical, PipelineOrder.Scorer }, order);
        }

        [Fact]
        public void Rejecting_subsystem_stops_the_rest_of_the_pipeline()
        {
            var order = new List<int>();
            var s = NewSession(new Echo(PipelineOrder.Clinical), new Probe(PipelineOrder.Scorer, order));

            s.Apply(new SimAction("block"));

            Assert.Empty(order);
        }

        [Fact]
        public void Tick_runs_subsystems_once_per_whole_step()
        {
            var echo = new Echo();
            var s = NewSession(echo);

            s.Tick(1.2); // 2 whole steps, 0.2 s carried

            Assert.Equal(2, echo.Trace.Count);
        }

        [Fact]
        public void EventBus_delivers_in_subscription_order_to_matching_type_only()
        {
            var bus = new EventBus();
            var seen = new List<string>();
            bus.Subscribe<int>(i => seen.Add("a" + i));
            bus.Subscribe<int>(i => seen.Add("b" + i));
            bus.Subscribe<string>(x => seen.Add("s" + x));

            bus.Publish(7);

            Assert.Equal(new[] { "a7", "b7" }, seen);
        }

        private sealed class Probe : ISubsystem
        {
            private readonly List<int> _order;
            public Probe(int order, List<int> sink) { Order = order; _order = sink; }
            public int Order { get; }
            public void Init(Session s) => s.Verbs.Register("probe");
            public void OnAction(SimAction a, ActionOutcome o) { if (a.Verb == "probe") _order.Add(Order); }
            public void OnTick() { }
        }
    }
}
