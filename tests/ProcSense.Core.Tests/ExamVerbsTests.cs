using System;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>P3-EXM-01 (request_hint), P3-EXM-05 (submit_patient) and P3-SES-02 (Session.Check): deterministic, logged, replayable, and they never change what they should not.</summary>
    public class ExamVerbsTests
    {
        private const string Case = "VAR-S1-A";
        private static ContentPackage Content => ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { Case }, new[] { Case });

        private static Session New(SessionMode mode, ContentPackage? content = null)
        {
            content ??= Content;
            return new Session(content.Cases[Case], 42, mode, Subsystems.Default(content)) { Checker = new Session.CheckSource(content, () => Subsystems.Default(content)) };
        }

        private static SimAction Hint() => new SimAction(ExamControl.RequestHint);
        private static SimAction Submit(string? outcome = null, bool abandoned = false)
        {
            var p = new System.Collections.Generic.Dictionary<string, string>();
            if (outcome != null) p[ExamControl.OutcomeParam] = outcome;
            if (abandoned) p[ExamControl.AbandonedParam] = "true";
            return new SimAction(ExamControl.SubmitPatient, "", Channel.Vr, p);
        }

        [Fact]
        public void Hints_have_two_tiers_a_limit_of_three_and_the_same_log_gives_the_same_hints()
        {
            var a = New(SessionMode.Practice);
            for (int i = 0; i < 3; i++) Assert.True(a.Apply(Hint()).Accepted);
            Assert.Equal(new[] { 1, 2, 2 }, a.Hints.Select(h => h.Tier));                       // first request for a goal: what to think about; later requests for the same goal: what to do
            Assert.Single(a.Hints.Select(h => h.GoalId).Distinct());
            Assert.All(a.Hints, h => Assert.NotEmpty(h.Text));
            var fourth = a.Apply(Hint());
            Assert.False(fourth.Accepted);
            Assert.Equal(ExamControl.HintLimitReached, fourth.RejectReason);

            var b = New(SessionMode.Practice);
            for (int i = 0; i < 4; i++) b.Apply(Hint());
            Assert.Equal(a.Hints, b.Hints);
            Assert.Equal(SessionFile.Write(a), SessionFile.Write(b));
            var replayed = SessionFile.Replay(SessionFile.Write(a), Content, () => Subsystems.Default(Content));
            Assert.True(replayed.Match);
            Assert.Equal(a.Hints, replayed.Session.Hints);
        }

        [Fact]
        public void The_hint_limit_comes_from_grades_json_and_a_changed_limit_is_saved_and_replayed()
        {
            Assert.Equal(ExamControl.DefaultHintLimit, Flow.SessionStarter.HintLimitOf(new FileContentReader(TestCase.ContentRoot)));   // content/scoring/grades.json "hint_limit": 3, the same as before
            Assert.Equal(3, Flow.GradeTable.Parse("{\"bands\":{\"A\":90},\"pass_mark\":60,\"hint_penalty\":5}").HintLimit);              // an older grades file without the key keeps 3
            Assert.Equal(5, Flow.GradeTable.Parse("{\"bands\":{\"A\":90},\"pass_mark\":60,\"hint_penalty\":5,\"hint_limit\":5}").HintLimit);
            Assert.Equal(3, New(SessionMode.Practice).HintLimit);

            var content = Content;
            var two = new Session(content.Cases[Case], 42, SessionMode.Practice, Subsystems.Default(content), new SessionOptions(HintLimit: 2));
            Assert.True(two.Apply(Hint()).Accepted);
            Assert.True(two.Apply(Hint()).Accepted);
            Assert.Equal(ExamControl.HintLimitReached, two.Apply(Hint()).RejectReason);
            string text = SessionFile.Write(two);
            Assert.Contains("\"hint_limit\":2", text.Split('\n')[0]);
            var replayed = SessionFile.Replay(text, content, () => Subsystems.Default(content));
            Assert.True(replayed.Match);
            Assert.Equal(2, replayed.Session.HintLimit);
        }

        [Fact]
        public void A_hint_never_changes_the_clinical_state_and_costs_only_in_the_examination()
        {
            var p = New(SessionMode.Practice);
            var before = p.Observable.Hash();
            p.Apply(Hint());
            Assert.Equal(before, p.Observable.Hash());
            Assert.DoesNotContain(p.Log.Entries.Last().Effects, e => e.StartsWith("score:", StringComparison.Ordinal));

            var x = New(SessionMode.Assessment);
            Assert.True(x.Apply(Hint()).Accepted);
            Assert.Contains("score:" + ExamControl.HintScoreItem, x.Log.Entries.Last().Effects);
            Assert.Contains(Scorer.Collect(x, Content), i => i.Id == ExamControl.HintScoreItem);
        }

        [Fact]
        public void Submitting_closes_the_case_and_misses_what_is_still_open()
        {
            var s = New(SessionMode.Practice);
            var content = Content;
            var outcome = s.Apply(Submit());
            Assert.True(outcome.Accepted);
            Assert.True(s.Submitted);
            Assert.Equal(ExamControl.OutcomeProceed, s.SubmitOutcome);
            Assert.True(s.MissedAtSubmit > 0);
            Assert.Contains(s.GraphHistory, e => e.Kind == "goal_missed");
            long steps = s.Clock.Steps;
            s.Tick(60);
            Assert.Equal(steps, s.Clock.Steps);
            var later = s.Apply(new SimAction("ask_identity"));
            Assert.Equal(ExamControl.SessionSubmitted, later.RejectReason);
            Assert.Equal(ExamControl.SessionSubmitted, s.Apply(Submit()).RejectReason);          // one submission per session
            Assert.Equal(ExamControl.SessionSubmitted, s.Apply(Hint()).RejectReason);
            var report = Scorer.Score(s, content);
            Assert.Equal(Scorer.ResultFail, report.Result);                                       // sent on before the case could end: a failure, with the missed items listed
            Assert.Equal("SAFETY_FAILURE", s.EndStates!.Resolved);
        }

        [Fact]
        public void Submit_has_a_default_outcome_a_transfer_request_and_an_abandoned_flag()
        {
            Assert.Equal(ExamControl.BadOutcome, New(SessionMode.Practice).Apply(Submit("sideways")).RejectReason);
            var t = New(SessionMode.Practice);
            t.Apply(Submit(ExamControl.OutcomeTransfer));
            Assert.Equal(ExamControl.OutcomeTransfer, t.SubmitOutcome);
            Assert.False(t.Abandoned);
            var a = New(SessionMode.Assessment);
            a.Apply(Submit(null, true));
            Assert.True(a.Abandoned);
            var replayed = SessionFile.Replay(SessionFile.Write(a), Content, () => Subsystems.Default(Content));
            Assert.True(replayed.Match);
            Assert.True(replayed.Session.Abandoned && replayed.Session.Submitted);
        }

        [Fact]
        public void Check_agrees_with_Apply_and_never_changes_the_session()
        {
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", Case + "_correct.actions.json"))).Take(14).ToList();
            var s = New(SessionMode.Practice);
            foreach (var timed in script)
            {
                s.Tick(timed.T - s.Clock.SimSeconds);
                string hashBefore = s.Log.Hash(), stateBefore = s.Observable.Hash();
                long stepsBefore = s.Clock.Steps;
                var check = s.Check(timed.Action);
                Assert.Equal(hashBefore, s.Log.Hash());
                Assert.Equal(stateBefore, s.Observable.Hash());
                Assert.Equal(stepsBefore, s.Clock.Steps);
                var applied = s.Apply(timed.Action);
                Assert.Equal((applied.Accepted, applied.RejectReason), (check.Accepted, check.Reason));
            }
            Assert.False(s.Check(new SimAction("no_such_verb")).Accepted);
            Assert.Equal(VerbRegistry.UnknownVerb, s.Check(new SimAction("no_such_verb")).Reason);
            Assert.True(s.Check(Hint()).Accepted);
            Assert.Empty(s.Hints);                                                                // checking a hint did not give one
            Assert.Throws<InvalidOperationException>(() => new Session(Content.Cases[Case], 1, SessionMode.Practice, Subsystems.Default(Content)).Check(Hint()));
        }
    }
}
