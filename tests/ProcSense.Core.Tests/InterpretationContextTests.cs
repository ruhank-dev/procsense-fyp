using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.Core.Scoring;
using ProcSense.Core.Speech;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The interpretation context, state-based eligibility and judged speech evidence in the log (p1-ai-language-s10, ADR-0022; judgement and graded credit p1-ai-language-s22, ADR-0025).</summary>
    public class InterpretationContextTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static ScoreTables Tables => Content.Scoring;
        private static readonly IReadOnlyList<ContextTurn> NoTurns = new List<ContextTurn>();

        private static Session Start() => new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));

        private static ActionOutcome Say(Session s, string act, string utterance, string assertion = "performed", string actor = "trainee", int r = 3, int c = 3, int k = 3, string form = "statement") =>
            s.Apply(new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Cli, new Dictionary<string, string>
            {
                [SpeechEvidenceSubsystem.ActParam] = act,
                [SpeechEvidenceSubsystem.SpanParam] = "words from the sentence",
                [SpeechEvidenceSubsystem.UtteranceParam] = utterance,
                [SpeechEvidenceSubsystem.AssertionParam] = assertion,
                [SpeechEvidenceSubsystem.ActorParam] = actor,
                [SpeechEvidenceSubsystem.FormParam] = form,
                [SpeechEvidenceSubsystem.RelevanceParam] = r.ToString(CultureInfo.InvariantCulture),
                [SpeechEvidenceSubsystem.CorrectnessParam] = c.ToString(CultureInfo.InvariantCulture),
                [SpeechEvidenceSubsystem.CompletenessParam] = k.ToString(CultureInfo.InvariantCulture),
                [SpeechEvidenceSubsystem.ClaimsPhysicalParam] = "false"
            }));

        private static EvidenceStore Evidence(Session s) { var st = new EvidenceStore(Tables); st.FromLog(s.Log); return st; }

        /// <summary>An ordinary act that is eligible in the session's current state, or null when the state allows none.</summary>
        private static EligibleAct? AnyOrdinary(Session s) => InterpretationContextBuilder.Build(s, Content, NoTurns, "patient").Eligible.FirstOrDefault(e => !e.Critical && e.Act != "ANSWER_QUESTION" && e.Act != "CLOSED_LOOP");

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
            var turns = new[] { new ContextTurn("patient", "Will it hurt?"), new ContextTurn("trainee", "A little.") };
            var ctx = InterpretationContextBuilder.Build(s, Content, turns, "patient");
            Assert.Equal(s.RunningSteps, ctx.Steps);
            Assert.Equal("VAR-S1-A", ctx.Case);
            Assert.Equal("practice", ctx.Mode);
            Assert.Equal("trainee", ctx.Speaker);
            Assert.All(ctx.Eligible, e => Assert.Contains(Tables.Behaviours, b => b.Id == e.Behaviour && b.AiActs.Contains(e.Act) && b.Steps.Any(ctx.Steps.Contains)));
            var json = ctx.ToJson();
            foreach (var key in new[] { "\"case\"", "\"scenario\"", "\"mode\"", "\"steps\"", "\"eligible\"", "\"accepted\"", "\"patient_question_pending\"", "\"readback_pending\"", "\"last_turns\"", "\"speaker\"", "\"addressee\"", "\"verified_actions\"" }) Assert.Contains(key, json);
            foreach (var banned in new[] { "hidden", "score", "expected", "hint", "goal" }) Assert.DoesNotContain(banned, json);
            Assert.Equal(json, InterpretationContextBuilder.Build(s, Content, turns, "patient").ToJson());   // deterministic
        }

        [Fact]
        public void The_context_names_the_actions_the_simulator_itself_accepted_and_nothing_a_speaker_claimed()
        {
            var s = Start();
            Assert.Empty(InterpretationContextBuilder.Build(s, Content, NoTurns, "patient").VerifiedActions);
            Say(s, "CONFIRM_IDENTITY", "u1");                                                                  // a spoken claim of the identity check
            Assert.DoesNotContain("confirm_identity", InterpretationContextBuilder.Build(s, Content, NoTurns, "patient").VerifiedActions);
            var real = s.Apply(new SimAction("open_notes", "", Channel.Cli, new Dictionary<string, string>()));
            Assert.True(real.Accepted, real.RejectReason);
            Assert.Contains("open_notes", InterpretationContextBuilder.Build(s, Content, NoTurns, "patient").VerifiedActions);        // the engine accepted a real action
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
        public void An_evidence_span_up_to_the_longest_utterance_is_accepted_and_a_longer_one_is_refused()
        {
            var s = Start();
            ActionOutcome Span(string utterance, int length) => s.Apply(new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Cli, new Dictionary<string, string>
            {
                [SpeechEvidenceSubsystem.ActParam] = "ASK_TOPIC",
                [SpeechEvidenceSubsystem.SpanParam] = new string('a', length),
                [SpeechEvidenceSubsystem.UtteranceParam] = utterance,
                [SpeechEvidenceSubsystem.AssertionParam] = "performed",
                [SpeechEvidenceSubsystem.ActorParam] = "trainee",
                [SpeechEvidenceSubsystem.FormParam] = "question",
                [SpeechEvidenceSubsystem.RelevanceParam] = "3",
                [SpeechEvidenceSubsystem.CorrectnessParam] = "3",
                [SpeechEvidenceSubsystem.CompletenessParam] = "3",
                [SpeechEvidenceSubsystem.ClaimsPhysicalParam] = "false"
            }));
            Assert.Equal(400, SpeechEvidenceSubsystem.MaxSpan);
            Assert.True(Span("long-1", 400).Accepted);
            Assert.Equal(SpeechEvidenceSubsystem.MissingSpan, Span("long-2", 401).RejectReason);
            Assert.True(Span("short-1", 12).Accepted);
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
            Assert.True(InterpretationContextBuilder.ReadbackPending(s));
            Assert.Equal(SpeechEvidenceSubsystem.NoPendingQuestion, Say(s, "ANSWER_QUESTION", "u4").RejectReason);                      // still no question
            Assert.True(Say(s, "ASK_TOPIC", "u5").Accepted);                                                                         // other acts need no waiting state
        }

        [Fact]
        public void A_malformed_judgement_is_rejected_and_logged_as_rejected()
        {
            var s = Start();
            Assert.Equal(SpeechEvidenceSubsystem.BadJudgement, Say(s, "ASK_TOPIC", "u1", r: 4).RejectReason);                           // a grade out of range
            Assert.Equal(SpeechEvidenceSubsystem.BadJudgement, Say(s, "ASK_TOPIC", "u2", assertion: "definitely").RejectReason);          // outside the closed vocabulary
            Assert.Equal(SpeechEvidenceSubsystem.BadJudgement, Say(s, "ASK_TOPIC", "u3", form: "shout").RejectReason);
            Assert.Equal(SpeechEvidenceSubsystem.MissingAct, s.Apply(new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Cli, new Dictionary<string, string> { ["utterance_id"] = "u4" })).RejectReason);
            Assert.Equal(SpeechEvidenceSubsystem.MissingUtterance, s.Apply(new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Cli, new Dictionary<string, string> { ["act"] = "ASK_TOPIC", ["span"] = "x" })).RejectReason);
            Assert.Empty(Evidence(s).Records);
        }

        [Fact]
        public void An_eligible_act_is_graded_a_better_utterance_counts_and_an_out_of_window_act_is_refused()
        {
            var s = Start();
            var eligible = AnyOrdinary(s);
            Assert.NotNull(eligible);
            Assert.True(Say(s, eligible!.Act, "u1", k: 1).Accepted);                                          // correct but incomplete
            var rec = Assert.Single(Evidence(s).Records);
            Assert.True(rec.Accepted, rec.Reason);
            Assert.Equal(eligible.Behaviour, rec.Behaviour);
            Assert.Equal("ai", rec.Source);
            Assert.InRange(rec.Grade!.Value, 0.5, 0.9);                                                       // substantial partial credit

            var ctxAfter = InterpretationContextBuilder.Build(s, Content, NoTurns, "patient");
            Assert.Contains(eligible.Behaviour, ctxAfter.Accepted);

            Say(s, eligible.Act, "u2");                                                                      // the same behaviour again, a complete and correct utterance
            var comm = Scorer.Score(s, Content).Communication;
            Assert.Equal(1.0, Assert.Single(comm, c => c.Behaviour == eligible.Behaviour).Grade);               // the best performance counts once, repeating cannot add up

            var outside = Tables.Behaviours.SelectMany(b => b.AiActs.Select(a => (a, b))).First(x => !Tables.CriticalActs.Contains(x.a) && !x.b.Steps.Any(s.RunningSteps.Contains));
            Say(s, outside.a, "u3");
            Assert.StartsWith(EvidenceStore.ContextMismatch, Evidence(s).Records.Last().Reason);
            Assert.False(Evidence(s).Records.Last().Accepted);
        }

        [Theory]
        [InlineData("negated", "trainee")]
        [InlineData("intended_later", "trainee")]
        [InlineData("hypothetical", "trainee")]
        [InlineData("reported", "other")]
        [InlineData("other_actor", "other")]
        [InlineData("earlier_claim", "trainee")]
        [InlineData("uncertain", "unclear")]
        [InlineData("incomplete", "trainee")]
        [InlineData("performed", "other")]
        public void Speech_that_the_trainee_does_not_perform_now_earns_nothing(string assertion, string actor)
        {
            var s = Start();
            var a = AnyOrdinary(s)!;
            Assert.True(Say(s, a.Act, "u1", assertion, actor).Accepted);                                      // the action is logged: the record shows what was said
            var rec = Assert.Single(Evidence(s).Records);
            Assert.False(rec.Accepted);
            Assert.StartsWith(EvidenceStore.NotPerformed, rec.Reason);
            Assert.Equal(0, rec.Grade);
            Assert.Empty(Scorer.Score(s, Content).Communication);
        }

        [Fact]
        public void Evidence_with_no_running_step_is_refused_for_that_reason_not_as_a_window_mismatch()
        {
            var act = Tables.Behaviours.SelectMany(b => b.AiActs).First(a => !Tables.CriticalActs.Contains(a));
            var rec = new EvidenceStore(Tables).Submit(act, "", "words", new SpeechJudgement("performed", "trainee", "statement", 3, 3, 3, false), new int[0], new[] { 0 });
            Assert.False(rec.Accepted);
            Assert.Equal(EvidenceStore.NoRunningStep, rec.Reason);
        }

        [Fact]
        public void The_same_utterance_and_objective_cannot_be_credited_twice_but_two_objectives_of_one_utterance_both_count()
        {
            var s = Start();
            var acts = InterpretationContextBuilder.Build(s, Content, NoTurns, "patient").Eligible.Where(e => !e.Critical && e.Act != "ANSWER_QUESTION" && e.Act != "CLOSED_LOOP").Select(e => e.Act).Distinct().Take(2).ToList();
            Assert.Equal(2, acts.Count);
            Say(s, acts[0], "same");
            Say(s, acts[0], "same");
            Assert.Equal(EvidenceStore.DuplicateUtterance, Evidence(s).Records.Last().Reason);
            Say(s, acts[1], "same");                                                                         // a second job in the same utterance
            Assert.Equal(2, Evidence(s).Records.Count(r => r.Accepted));
        }

        [Fact]
        public void A_critical_act_is_never_accepted_from_speech_at_any_grade_in_any_state_and_never_physically_complete()
        {
            foreach (var act in Tables.CriticalActs)
            {
                var s = Start();
                Say(s, act, "u1");
                Assert.All(Evidence(s).Records, r => Assert.False(r.Accepted));
                string why = Evidence(s).Records.Last().Reason;
                Assert.True(why == EvidenceStore.CriticalNeedsAction || why.StartsWith(EvidenceStore.ContextMismatch) || why == EvidenceStore.NoRunningStep, why);
                Assert.All(Scorer.Score(s, Content).Communication.Where(c => c.Critical), c => Assert.False(c.PhysicalVerified));     // a spoken claim never makes the physical part true
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
            Assert.Equal(scoreBefore.ProcessTotal, scoreAfter.ProcessTotal);                                    // the process score is untouched; speech adds only its separate communication credit
            Assert.True(scoreAfter.CommunicationTotal > 0);
        }

        [Fact]
        public void Replaying_the_same_actions_gives_the_same_log_and_the_same_evidence()
        {
            string Run()
            {
                var s = Start();
                var a = AnyOrdinary(s)!;
                Say(s, a.Act, "u1"); Say(s, "CONFIRM_IDENTITY", "u2"); Say(s, a.Act, "u3");
                return string.Join("|", s.Log.Entries.Select(e => e.Verb + e.Accepted + string.Join(",", e.Effects))) + "#" + string.Join("|", Evidence(s).Records.Select(r => r.Act + r.Accepted + r.Reason + r.Grade));
            }
            Assert.Equal(Run(), Run());
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
