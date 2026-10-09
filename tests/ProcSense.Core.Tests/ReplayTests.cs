using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Replay;

namespace ProcSense.Core.Tests
{
    public class ReplayTests
    {
        /// <summary>Test-only stand-in for the real workflow verbs: "ask" reveals a history topic, "read_band" shows the wristband.</summary>
        private sealed class Asker : ISubsystem
        {
            private ProcSense.Core.Random.SimRandom _rng = null!;
            private Session _s = null!;
            public int Order => PipelineOrder.Clinical;

            public void Init(Session s)
            {
                _s = s;
                s.Verbs.Register("ask");
                s.Verbs.Register("read_band");
                _rng = s.Random.Stream("events");
            }

            public void OnAction(SimAction a, ActionOutcome o)
            {
                if (a.Verb == "read_band") { _s.Patient.Clinical.WristbandRead = true; return; }
                if (!_s.Patient.History.Any(h => h.Topic == a.Target)) { o.Reject("NO_SUCH_TOPIC"); return; }
                _s.Patient.Clinical.Reveal(a.Target);
                o.Effects.Add("roll:" + _rng.NextInt(100));
            }

            public void OnTick() { }
        }

        private static IEnumerable<ISubsystem> Subs() => new ISubsystem[] { new Asker() };

        private static Session Play(string scriptJson)
        {
            var s = new Session(TestCase.Card(), 42, SessionMode.Practice, Subs());
            SessionFile.Play(s, ActionScript.Parse(scriptJson));
            return s;
        }

        private const string Script =
            "[{\"t\":0,\"verb\":\"read_band\"},{\"t\":3.3,\"verb\":\"ask\",\"target\":\"last_food\"}," +
            "{\"t\":9,\"verb\":\"ask\",\"target\":\"nonsense\"},{\"t\":9.2,\"verb\":\"dance\"}]";

        [Fact]
        public void Replaying_a_saved_session_gives_the_same_hash()
        {
            var original = Play(Script);
            var saved = SessionFile.Write(original);

            var r = SessionFile.Replay(saved, TestCase.Package(), Subs);

            Assert.True(r.Match);
            Assert.Equal(original.Log.Hash(), r.ActualHash);
            Assert.Equal(original.Observable.Hash(), r.Session.Observable.Hash());
        }

        [Fact]
        public void A_tampered_log_is_detected()
        {
            var saved = SessionFile.Write(Play(Script));

            var r = SessionFile.Replay(saved.Replace("\"target\":\"last_food\"", "\"target\":\"smoking\""), TestCase.Package(), Subs);

            Assert.False(r.Match);
        }

        [Fact]
        public void Observable_shows_only_what_was_disclosed()
        {
            var s = Play(Script);
            var json = s.Observable.ToJson();

            Assert.Contains("Dinner at 21:00", json);
            foreach (var fact in s.Patient.History.Where(h => h.Topic != "last_food" && h.Answer.Length > 10))
                Assert.DoesNotContain(fact.Answer, json);
        }

        [Fact]
        public void Wristband_is_hidden_until_read()
        {
            var s = new Session(TestCase.Card(), 42, SessionMode.Practice, Subs());
            Assert.Null(s.Observable.Wristband);
            Assert.Null(s.Observable.Notes);

            s.Apply(new SimAction("read_band"));

            Assert.Equal("MR 102938", s.Observable.Wristband!.MrnText);
        }

        [Fact]
        public void Observable_reports_clock_and_last_rejection()
        {
            var s = Play(Script);

            Assert.Equal("08:00:09.5000000", s.Observable.ClockTime); // leftover 0.3 s from earlier + 0.2 s = one more whole step, so the t=9.2 action lands at 9.5 s
            Assert.Equal("UNKNOWN_VERB", s.Observable.LastRejection);
        }

        [Fact]
        public void Script_errors_are_clear()
        {
            var unknown = Assert.Throws<ContentException>(() => ActionScript.Parse("[{\"t\":1,\"verb\":\"x\",\"colour\":\"red\"}]"));
            Assert.Contains("actions[0].colour: unknown field", unknown.Message);

            var notList = Assert.Throws<ContentException>(() => ActionScript.Parse("{}"));
            Assert.Contains("expected a list", notList.Message);
        }

        [Fact]
        public void Actions_out_of_time_order_are_rejected()
        {
            var s = new Session(TestCase.Card(), 42, SessionMode.Practice, Subs());
            var script = ActionScript.Parse("[{\"t\":5,\"verb\":\"read_band\"},{\"t\":2,\"verb\":\"read_band\"}]");

            var e = Assert.Throws<ContentException>(() => SessionFile.Play(s, script));

            Assert.Contains("time order", e.Message);
        }
    }
}
