using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.Core.Scoring;
using ProcSense.Core.Speech;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The interpretation context, state-based eligibility and speech evidence in the log (p1-ai-language-s10, ADR-0022).</summary>
    public class InterpretationContextTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static ScoreTables Tables => Content.Scoring;
        private static readonly IReadOnlyList<ContextTurn> NoTurns = new List<ContextTurn>();

        private static Session Start() => new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));

        private static ActionOutcome Say(Session s, string act, string utterance, double conf = 0.99, string status = "actual") =>
            s.Apply(new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Cli, new Dictionary<string, string>
            {
                [SpeechEvidenceSubsystem.ActParam] = act,
                [SpeechEvidenceSubsystem.SpanParam] = "words from the sentence",
                [SpeechEvidenceSubsystem.ConfidenceParam] = conf.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [SpeechEvidenceSubsystem.UtteranceParam] = utterance,
                [SpeechEvidenceSubsystem.StatusParam] = status
            }));

        private static EvidenceStore Evidence(Session s) { var st = new EvidenceStore(Tables); st.FromLog(s.Log); return st; }

        /// <summary>An ordinary act that is eligible in the session's current state, or null when the state allows none.</summary>
        private static EligibleAct? AnyOrdinary(Session s) => InterpretationContextBuilder.Build(s, Content, NoTurns, "patient").Eligible.FirstOrDefault(e => !e.Critical && e.Act != "ANSWER_QUESTION" && e.Act != "CLOSED_LOOP");   // those two need a waiting question or read-back in the session

        [Fact]
        public void Eligibility_follows_the_behaviour_windows_of_the_mapping_step_by_step()
        {
            for (int step = 1; step <= 15; step++)
            {
                var got = InterpretationContextBuilder.EligibleFor(Tables, new[] { step });
                foreach (var b in Tables.Behaviours)
                    foreach (var act in b.AiActs)
                        Assert.Equal(b.Steps.Contains(step), got.Any(e => e.Act == act && e.Behaviour == b.Id));
            }
            Assert.Empty(InterpretationContextBuilder.EligibleFor(Tables, new int[0]));                          // no running step: nothing is eligible, never everything
            var s9 = InterpretationContextBuilder.EligibleFor(Tables, new[] { 9 });
            Assert.Contains(s9, e => e.Act == "CHECK_UNDERSTANDING");
            Assert.DoesNotContain(InterpretationContextBuilder.EligibleFor(Tables, new[] { 3 }), e => e.Act == "CHECK_UNDERSTANDING");
            Assert.All(InterpretationContextBuilder.EligibleFor(Tables, Enumerable.Range(1, 15).ToList()).Where(e => Tables.CriticalActs.Contains(e.Act)), e => Assert.True(e.Critical));
        }

        [Fact]
        public void The_context_is_made_from_the_session_and_holds_no_hidden_or_scoring_field()
        {
            var s = Start();
            var ctx = InterpretationContextBuilder.Build(s, Content, new[] { new ContextTurn("patient", "Will it hurt?"), new ContextTurn("trainee", "A little.") }, "patient");
            Assert.Equal(s.RunningSteps, ctx.Steps);
            Assert.Equal("VAR-S1-A", ctx.Case);
            Assert.Equal("practice", ctx.Mode);
            Assert.Equal("trainee", ctx.Speaker);
            Assert.All(ctx.Eligible, e => Assert.Contains(Tables.Behaviours, b => b.Id == e.Behaviour && b.AiActs.Contains(e.Act) && b.Steps.Any(ctx.Steps.Contains)));
            var json = ctx.ToJson();
            foreach (var key in new[] { "\"case\"", "\"scenario\"", "\"mode\"", "\"steps\"", "\"eligible\"", "\"accepted\"", "\"patient_question_pending\"", "\"readback_pending\"", "\"last_turns\"", "\"speaker\"", "\"addressee\"" }) Assert.Contains(key, json);
            foreach (var banned in new[] { "hidden", "score", "expected", "hint", "goal" }) Assert.DoesNotContain(banned, json);
            Assert.Equal(json, InterpretationContextBuilder.Build(s, Content, new[] { new ContextTurn("patient", "Will it hurt?"), new ContextTurn("trainee", "A little.") }, "patient").ToJson());   // deterministic
        }

        [Fact]
        public void Only_the_last_four_turns_are_kept_and_long_text_is_cut()
        {
            var turns = Enumerable.Range(0, 7).Select(i => new ContextTurn(i % 2 == 0 ? "patient" : "trainee", new string('x', 500) + i)).ToList();
            var ctx = InterpretationContextBuilder.Build(Start(), Content, turns, "patient");
            Assert.Equal(InterpretationContextBuilder.MaxTurns, ctx.LastTurns.Count);
            Assert.All(ctx.LastTurns, t => Assert.Equal(InterpretationContextBuilder.MaxTurnChars, t.Text.Length));
        }

        [Fact]
        public void Speech_evidence_is_logged_with_the_steps_the_engine_saw_and_changes_no_state()
        {
            var s = Start();
            var before = s.Observable.ToJson();
            var o = Say(s, "ASK_TOPIC", "u1");
            Assert.True(o.Accepted);
            Assert.Equal(SpeechEvidenceSubsystem.StepsEffect + string.Join(",", s.RunningSteps), Assert.Single(o.Effects));
            Assert.Equal(before, s.Observable.ToJson());
            Assert.Contains(s.Log.Entries, e => e.Verb == SpeechEvidenceSubsystem.Verb && e.Accepted);
        }

        [Fact]
        public void An_answer_needs_a_waiting_question_and_a_read_back_a_waiting_result_in_the_session_itself()
        {
            var s = Start();
            Assert.Equal(SpeechEvidenceSubsystem.NoPendingQuestion, Say(s, "ANSWER_QUESTION", "u1").RejectReason);                       // a client claiming a waiting question it was not given
            Assert.Equal(SpeechEvidenceSubsystem.NoReadbackPending, Say(s, "CLOSED_LOOP", "u2").RejectReason);
            Assert.DoesNotContain(s.Log.Entries, e => e.Verb == SpeechEvidenceSubsystem.Verb && e.Accepted);
            Assert.Empty(Evidence(s).Records);
            s.Patient.Clinical.SetFlag(ProcSense.Core.Workflow.Steps.ResultReviewSubsystem.CriticalFlag);
            Assert.True(InterpretationContextBuilder.ReadbackPending(s));                                                             // the definition the check shares with the context (the result-review step owns the flag in play, so the logged path is exercised there)
            Assert.Equal(SpeechEvidenceSubsystem.NoPendingQuestion, Say(s, "ANSWER_QUESTION", "u4").RejectReason);                      // still no question
            Assert.True(Say(s, "ASK_TOPIC", "u5").Accepted);                                                                         // other acts need no waiting state
        }

        [Fact]
        public void Malformed_speech_evidence_is_rejected_and_logged_as_rejected()
        {
            var s = Start();
            Assert.Equal(SpeechEvidenceSubsystem.BadConfidence, Say(s, "ASK_TOPIC", "u1", 1.5).RejectReason);
            Assert.Equal(SpeechEvidenceSubsystem.BadStatus, Say(s, "ASK_TOPIC", "u2", 0.9, "denied").RejectReason);
            Assert.Equal(SpeechEvidenceSubsystem.MissingAct, s.Apply(new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Cli, new Dictionary<string, string> { ["utterance_id"] = "u3" })).RejectReason);
            Assert.Equal(SpeechEvidenceSubsystem.MissingUtterance, s.Apply(new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Cli, new Dictionary<string, string> { ["act"] = "ASK_TOPIC", ["span"] = "x", ["confidence"] = "0.9", ["status"] = "actual" })).RejectReason);
            Assert.Empty(Evidence(s).Records);
        }

        [Fact]
        public void An_eligible_act_is_accepted_once_and_an_out_of_window_act_is_refused()
        {
            var s = Start();
            var eligible = AnyOrdinary(s);
            Assert.NotNull(eligible);
            Assert.True(Say(s, eligible!.Act, "u1").Accepted);
            var rec = Assert.Single(Evidence(s).Records);
            Assert.True(rec.Accepted, rec.Reason);
            Assert.Equal(eligible.Behaviour, rec.Behaviour);
            Assert.Equal("ai", rec.Source);

            var ctxAfter = InterpretationContextBuilder.Build(s, Content, NoTurns, "patient");
            Assert.Contains(eligible.Behaviour, ctxAfter.Accepted);                                          // the context now says it was credited

            Say(s, eligible.Act, "u2");                                                                      // the same behaviour again, a new utterance
            Assert.Equal(EvidenceStore.DuplicateBehaviour, Evidence(s).Records.Last().Reason);

            var outside = Tables.Behaviours.SelectMany(b => b.AiActs.Select(a => (a, b))).First(x => !Tables.CriticalActs.Contains(x.a) && !x.b.Steps.Any(s.RunningSteps.Contains));
            Say(s, outside.a, "u3");
            Assert.StartsWith(EvidenceStore.ContextMismatch, Evidence(s).Records.Last().Reason);
            Assert.False(Evidence(s).Records.Last().Accepted);
        }

        [Fact]
        public void The_same_utterance_cannot_be_credited_twice()
        {
            var s = Start();
            var a = AnyOrdinary(s)!;
            Say(s, a.Act, "same");
            Say(s, a.Act, "same");
            Assert.Equal(EvidenceStore.DuplicateUtterance, Evidence(s).Records.Last().Reason);
            Assert.Equal(1, Evidence(s).Records.Count(r => r.Accepted));
        }

        [Fact]
        public void A_critical_act_is_never_accepted_from_speech_at_any_confidence_in_any_state()
        {
            foreach (var act in Tables.CriticalActs)
            {
                var s = Start();
                Say(s, act, "u1", 1.0);
                Assert.All(Evidence(s).Records, r => Assert.False(r.Accepted));
                Assert.Contains(Evidence(s).Records.Last().Reason, new[] { EvidenceStore.CriticalNeedsAction, EvidenceStore.UnknownAct });          // a critical act that no behaviour lists is an unknown AI act: refused either way
            }
        }

        [Fact]
        public void Speech_evidence_can_never_satisfy_a_behaviours_actions_or_the_scored_items()
        {
            Assert.DoesNotContain(Tables.Behaviours, b => b.Actions.Contains(SpeechEvidenceSubsystem.Verb));
            var s = Start();
            var scoreBefore = Scorer.Score(s, Content);
            Say(s, AnyOrdinary(s)!.Act, "u1");
            var store = Evidence(s);
            Assert.DoesNotContain(store.Records, r => r.Source == "deterministic");
            var scoreAfter = Scorer.Score(s, Content);
            Assert.Equal(scoreBefore.Items.Select(i => i.Id + i.Status), scoreAfter.Items.Select(i => i.Id + i.Status));
        }

        [Fact]
        public void Replaying_the_same_actions_gives_the_same_log_and_the_same_evidence()
        {
            string Run()
            {
                var s = Start();
                var a = AnyOrdinary(s)!;
                Say(s, a.Act, "u1"); Say(s, "CONFIRM_IDENTITY", "u2"); Say(s, a.Act, "u3");
                return string.Join("|", s.Log.Entries.Select(e => e.Verb + e.Accepted + string.Join(",", e.Effects))) + "#" + string.Join("|", Evidence(s).Records.Select(r => r.Act + r.Accepted + r.Reason));
            }
            Assert.Equal(Run(), Run());
        }

        [Fact]
        public void The_engine_evidence_thresholds_are_the_classifiers_calibrated_ones_a_critical_act_has_none_and_the_boundary_is_exact()
        {
            Assert.Equal(14, Tables.Thresholds.Count);                                                                // every act the classifier head can credit
            foreach (var kv in Tables.Thresholds)
            {
                Assert.InRange(kv.Value, 0.1, Tables.DefaultThreshold);                                               // calibrated, never above the old 0.8 placeholder
                Assert.DoesNotContain(kv.Key, Tables.CriticalActs);                                                   // a critical act has no threshold: it is refused whatever its confidence
                var behaviour = Tables.Behaviours.First(b => b.AiActs.Contains(kv.Key));
                int step = behaviour.Steps[0];
                var at = new EvidenceStore(Tables).Submit(kv.Key, "", "", kv.Value, new[] { step }, new[] { 1 });
                Assert.True(at.Accepted, kv.Key + " at its threshold: " + at.Reason);                                 // evidence the gate credited at its own threshold is accepted
                var below = new EvidenceStore(Tables).Submit(kv.Key, "", "", kv.Value - 0.001, new[] { step }, new[] { 1 });
                Assert.Equal(EvidenceStore.LowConfidence, below.Reason);                                              // and one notch below is refused
            }
            Assert.Equal(0.8, Tables.DefaultThreshold);                                                               // the default still applies to an act the head does not credit
        }

        [Fact]
        public void A_critical_result_waiting_for_acknowledgement_and_a_pending_question_show_in_the_context()
        {
            var s = Start();
            var ctx = InterpretationContextBuilder.Build(s, Content, NoTurns, "patient");
            Assert.False(ctx.ReadbackPending);
            Assert.False(ctx.PatientQuestionPending);
            s.Patient.Clinical.SetFlag(ProcSense.Core.Workflow.Steps.ResultReviewSubsystem.CriticalFlag);
            Assert.True(InterpretationContextBuilder.Build(s, Content, NoTurns, "patient").ReadbackPending);
        }
    }
}
