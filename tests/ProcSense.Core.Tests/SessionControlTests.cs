using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Pause / resume and time-skip (p0-core-engine-s06). Time-skip must be exactly the same as playing the minutes out.</summary>
    public class SessionControlTests
    {
        private sealed class Counter : ISubsystem
        {
            public int Ticks;
            public int AttentionAtTick = -1;
            public Session? Session;
            public int Order => PipelineOrder.Physiology;
            public void Init(Session session) => Session = session;
            public void OnAction(SimAction action, ActionOutcome outcome) { }
            public void OnTick()
            {
                Ticks++;
                if (Ticks == AttentionAtTick) Session!.RequestAttention("critical result");
            }
        }

        private static (Session session, Counter counter) New(SessionMode mode = SessionMode.Practice, int attentionAtTick = -1)
        {
            var content = TestCase.Package();
            var counter = new Counter { AttentionAtTick = attentionAtTick };
            var subsystems = Subsystems.Default(content).Concat(new ISubsystem[] { counter });
            return (new Session(content.Cases["VAR-S1-A"], 42, mode, subsystems), counter);
        }

        private static SimAction Act(string verb, params (string k, string v)[] p) =>
            new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        [Fact]
        public void The_mode_is_kept_for_the_whole_session()
        {
            Assert.Equal(SessionMode.Practice, New(SessionMode.Practice).session.Mode);
            Assert.Equal(SessionMode.Assessment, New(SessionMode.Assessment).session.Mode);
        }

        [Fact]
        public void Pause_stops_time_and_the_subsystems()
        {
            var (s, counter) = New();
            s.Tick(5);
            long steps = s.Clock.Steps;
            int ticks = counter.Ticks;

            Assert.True(s.Pause().Accepted);
            s.Tick(60);

            Assert.True(s.Paused);
            Assert.Equal(steps, s.Clock.Steps);
            Assert.Equal(ticks, counter.Ticks);
        }

        [Fact]
        public void An_action_while_paused_is_rejected_and_logged_and_changes_nothing()
        {
            var (s, _) = New();
            s.Pause();
            var before = s.Observable;

            var outcome = s.Apply(Act("introduce_self"));

            Assert.False(outcome.Accepted);
            Assert.Equal("SESSION_PAUSED", outcome.RejectReason);
            var after = s.Observable;
            Assert.Equal("SESSION_PAUSED", after.LastRejection);   // the trainee is told why; nothing else is different
            Assert.Equal(before.DisclosedFacts.Count, after.DisclosedFacts.Count);
            Assert.Equal(before.ClockTime, after.ClockTime);
            Assert.Equal(before.IdentityConfirmed, after.IdentityConfirmed);
            Assert.Contains("\"verb\":\"introduce_self\"", s.Log.ToJsonl());
            Assert.Contains("\"reject\":\"SESSION_PAUSED\"", s.Log.ToJsonl());
        }

        [Fact]
        public void Resume_continues_where_it_stopped()
        {
            var (s, counter) = New();
            s.Tick(2);
            s.Pause();
            s.Tick(100);
            Assert.True(s.Resume().Accepted);
            s.Tick(1);

            Assert.False(s.Paused);
            Assert.Equal(6, s.Clock.Steps);   // 2 s + 1 s = 6 half-second steps; the paused 100 s did not count
            Assert.Equal(6, counter.Ticks);
            Assert.True(s.Apply(Act("introduce_self")).Accepted);
        }

        [Fact]
        public void Pause_and_resume_are_logged_as_system_actions()
        {
            var (s, _) = New();
            s.Pause();
            s.Resume();

            string log = s.Log.ToJsonl();
            Assert.Contains("\"verb\":\"pause\"", log);
            Assert.Contains("\"verb\":\"resume\"", log);
            Assert.Contains("\"channel\":\"system\"", log);
        }

        [Fact]
        public void Resume_when_not_paused_and_a_second_pause_are_rejected()
        {
            var (s, _) = New();
            Assert.Equal("NOT_PAUSED", s.Resume().RejectReason);

            s.Pause();
            Assert.Equal("SESSION_PAUSED", s.Pause().RejectReason);
        }

        // The plan's test: skipping N minutes = playing N minutes.
        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(37.5)]
        public void A_time_skip_gives_the_same_state_as_the_same_minutes_of_manual_ticks(double minutes)
        {
            var (skipped, skippedCounter) = New();
            var (played, playedCounter) = New();

            Assert.True(skipped.TimeSkip(minutes).Accepted);
            for (int i = 0; i < (int)(minutes * 120); i++) played.Tick(0.5);

            Assert.Equal(played.Clock.Steps, skipped.Clock.Steps);
            Assert.Equal(playedCounter.Ticks, skippedCounter.Ticks);
            Assert.Equal(played.Observable.Hash(), skipped.Observable.Hash());
            Assert.Equal(played.Observable.ClockTime, skipped.Observable.ClockTime);
        }

        [Fact]
        public void A_time_skip_is_logged_with_how_many_steps_it_ran()
        {
            var (s, _) = New();

            var outcome = s.TimeSkip(10);

            Assert.Contains("skipped_steps:1200", outcome.Effects);
            Assert.Contains("\"verb\":\"time_skip\"", s.Log.ToJsonl());
            Assert.Contains("\"minutes\":\"10\"", s.Log.ToJsonl());
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("1441")]
        [InlineData("soon")]
        [InlineData("NaN")]
        public void A_bad_skip_length_is_rejected_and_time_does_not_move(string minutes)
        {
            var (s, _) = New();

            var outcome = s.Apply(Act("time_skip", ("minutes", minutes)));

            Assert.Equal("BAD_MINUTES", outcome.RejectReason);
            Assert.Equal(0, s.Clock.Steps);
        }

        [Fact]
        public void A_skip_without_minutes_is_rejected()
        {
            var (s, _) = New();

            Assert.Equal("BAD_MINUTES", s.Apply(Act("time_skip")).RejectReason);
        }

        [Fact]
        public void A_time_skip_stops_early_when_something_needs_attention_and_says_why()
        {
            var (s, counter) = New(attentionAtTick: 100);

            var outcome = s.TimeSkip(10);

            Assert.Equal(100, s.Clock.Steps);
            Assert.Equal(100, counter.Ticks);
            Assert.Contains("skipped_steps:100", outcome.Effects);
            Assert.Contains("stopped:critical result", outcome.Effects);
        }

        [Fact]
        public void A_second_skip_after_an_early_stop_carries_on_normally()
        {
            var (s, _) = New(attentionAtTick: 100);
            s.TimeSkip(10);

            var outcome = s.TimeSkip(1);

            Assert.Equal(220, s.Clock.Steps);
            Assert.DoesNotContain(outcome.Effects, e => e.StartsWith("stopped:"));
        }

        [Fact]
        public void A_time_skip_while_paused_is_rejected()
        {
            var (s, _) = New();
            s.Pause();

            Assert.Equal("SESSION_PAUSED", s.TimeSkip(5).RejectReason);
            Assert.Equal(0, s.Clock.Steps);
        }

        // A session with pauses and skips must still replay to the identical log.
        [Fact]
        public void A_session_with_pause_resume_and_skip_replays_to_the_same_hash()
        {
            var content = TestCase.Package();
            var original = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            original.Tick(3);
            original.Apply(Act("introduce_self"));
            original.Pause();
            original.Tick(50);
            original.Apply(Act("ask_identity"));
            original.Resume();
            original.TimeSkip(5);
            original.Apply(Act("ask_identity"));

            var result = SessionFile.Replay(SessionFile.Write(original), content, () => Subsystems.Default(content));

            Assert.True(result.Match, result.ExpectedHash + " vs " + result.ActualHash);
            Assert.Equal(original.Clock.Steps, result.Session.Clock.Steps);
            Assert.Equal(original.Observable.Hash(), result.Session.Observable.Hash());
        }

        [Fact]
        public void The_command_line_script_format_can_pause_and_skip()
        {
            var script = ActionScript.Parse("[{\"t\":0,\"verb\":\"time_skip\",\"target\":\"\",\"params\":{\"minutes\":\"2\"}}]");
            var (s, _) = New();

            SessionFile.Play(s, script);

            Assert.Equal(240, s.Clock.Steps);
        }
    }
}
