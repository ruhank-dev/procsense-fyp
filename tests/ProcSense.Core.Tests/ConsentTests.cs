using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Behaviour;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Workflow Step 9, consent (p0-simulation-workflow-s11, plan tests T09-1 to T09-3).</summary>
    public class ConsentTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static ConsentTables Tables => Content.Consent;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // The VAR-S1-A patient by default; a case file can drive the behaviour model (low sugar, for example) and the scenario can be forced.
        private static Session Start(string? caseFile = null, string? scenario = null, SessionMode mode = SessionMode.Practice, CaseCard? card = null)
        {
            var file = File_(caseFile ?? "VAR-S1-A");
            var chain = Subsystems.Default(Content).Where(x => !(x is BehaviourSubsystem) && !(x is ProcSense.Core.Physiology.PhysiologySubsystem))
                .Concat(new ISubsystem[] { new ProcSense.Core.Physiology.PhysiologySubsystem(Content, file), new BehaviourSubsystem(Content, file) });
            var basis = card ?? Content.Cases["VAR-S1-A"];
            var s = new Session(scenario == null ? basis : basis with { Scenario = scenario }, 42, mode, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static void Discuss(Session s, params string[] topics) { foreach (var t in topics) Assert.True(Do(s, "consent_discussion", ("topic", t)).Accepted, t); }

        private static void ExplainAll(Session s)
        {
            Discuss(s, "diagnosis", "operation", "benefits", "risks", "alternatives", "no_treatment", "matters");
        }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_covers_every_topic_for_both_scenarios_with_the_verified_figures()
        {
            Assert.Equal(new[] { "diagnosis", "operation", "benefits", "risks", "alternatives", "no_treatment", "matters" }, Tables.Topics);
            var s1 = Tables.Procedures["S1"]; var s2 = Tables.Procedures["S2"];
            Assert.All(new[] { s1, s2 }, p => Assert.Equal(Tables.Topics, p.Select(e => e.Topic)));
            Assert.Contains("0.46%", s1.Single(e => e.Topic == "risks").Text);
            Assert.Contains("0.28%", s1.Single(e => e.Topic == "risks").Text);
            Assert.Contains("29%", s2.Single(e => e.Topic == "alternatives").Text);
            Assert.Contains("39.1%", s2.Single(e => e.Topic == "alternatives").Text);
            Assert.Equal(6, Tables.Questions.Count);
            Assert.All(Tables.Questions, q => Assert.Single(q.Answers, a => a.Kind == "approved"));
            Assert.Equal(6.0, Tables.UnderstandingNeeded);
        }

        // ---------------------------------------------------------------- explaining (L9-01)

        [Fact]
        public void An_explanation_needs_a_known_topic_and_each_element_counts_once_and_builds_understanding()
        {
            var s = Start();
            Assert.Equal(ConsentSubsystem.MissingTopic, Reject(Do(s, "consent_discussion")));
            Assert.Equal(ConsentSubsystem.UnknownTopic, Reject(Do(s, "consent_discussion", ("topic", "weather"))));
            double u = s.Behaviour!.State.Get("understanding");
            var o = Do(s, "consent_discussion", ("topic", "diagnosis"));
            Assert.Contains("explained:diagnosis", o.Effects);
            Assert.Equal(u + 1, s.Behaviour.State.Get("understanding"));
            Assert.Contains("already_explained:diagnosis", Do(s, "consent_discussion", ("topic", "diagnosis")).Effects);
            Assert.Equal(u + 1, s.Behaviour.State.Get("understanding"));
            Assert.Contains(s.Consent!.History, h => h.ScoreItems.Contains("credit.consent_element"));
        }

        // ---------------------------------------------------------------- the patient's questions (L9-03, E9-B)

        [Fact]
        public void After_the_benefits_the_patient_asks_a_question_and_an_approved_answer_raises_trust()
        {
            var s = Start();
            Discuss(s, "diagnosis", "operation");
            var o = Do(s, "consent_discussion", ("topic", "benefits"));
            Assert.Contains("patient_asks:s1.home", o.Effects);
            Assert.Equal(new[] { "s1.home" }, s.Consent!.Pending);
            double trust = s.Behaviour!.State.Get("trust");
            Assert.Contains("answered:s1.home", Do(s, "answer_question", ("q", "s1.home"), ("a", "approved")).Effects);
            Assert.Equal(trust + 1, s.Behaviour.State.Get("trust"));
            Assert.Empty(s.Consent.Pending);
        }

        [Fact]
        public void False_reassurance_relaxes_the_patient_but_is_a_communication_item_and_a_dismissive_answer_costs_trust()
        {
            var s = Start();
            Discuss(s, "benefits");
            double anxiety = s.Behaviour!.State.Get("anxiety");
            Assert.Contains("patient_relaxes", Do(s, "answer_question", ("q", "s1.home"), ("a", "false_reassurance")).Effects);
            Assert.True(s.Behaviour.State.Get("anxiety") < anxiety);
            Assert.Contains(s.Consent!.History, h => h.ScoreItems.Contains("minor.false_reassurance"));
            Discuss(s, "alternatives");
            double trust = s.Behaviour.State.Get("trust");
            Do(s, "answer_question", ("q", "s1.scars"), ("a", "dismissive"));
            Assert.Equal(trust - 2, s.Behaviour.State.Get("trust"));
            Assert.Contains(s.Consent.History, h => h.ScoreItems.Contains("minor.question_dismissed"));
        }

        [Fact]
        public void An_answer_needs_a_pending_question_and_a_known_answer()
        {
            var s = Start();
            Assert.Equal(ConsentSubsystem.MissingQuestion, Reject(Do(s, "answer_question", ("a", "approved"))));
            Assert.Equal(ConsentSubsystem.NoQuestionPending, Reject(Do(s, "answer_question", ("q", "s1.home"), ("a", "approved"))));
            Discuss(s, "benefits");
            Assert.Equal(ConsentSubsystem.MissingAnswer, Reject(Do(s, "answer_question", ("q", "s1.home"))));
            Assert.Equal(ConsentSubsystem.UnknownAnswer, Reject(Do(s, "answer_question", ("q", "s1.home"), ("a", "shrug"))));
        }

        // ---------------------------------------------------------------- teach-back

        [Fact]
        public void Teach_back_needs_something_explained_and_fails_until_understanding_is_enough()
        {
            var s = Start();
            Assert.Equal(ConsentSubsystem.NothingToCheck, Reject(Do(s, "teach_back")));
            Discuss(s, "diagnosis");
            Assert.Contains("teach_back_failed", Do(s, "teach_back").Effects);                      // understanding is still too low
            Discuss(s, "operation", "benefits", "risks");
            var ok = Do(s, "teach_back");
            Assert.Contains("teach_back_ok", ok.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("consent.teach_back_ok"));
        }

        // ---------------------------------------------------------------- signing (T09-1)

        [Fact]
        public void T09_1_a_complete_conversation_with_capacity_checked_and_teach_back_gives_a_valid_signed_consent()
        {
            var s = Start();
            Assert.Contains("capacity:yes", Do(s, "check_capacity").Effects);
            ExplainAll(s);
            Do(s, "answer_question", ("q", "s1.home"), ("a", "approved")); Do(s, "answer_question", ("q", "s1.scars"), ("a", "approved"));
            Do(s, "teach_back");
            var o = Do(s, "sign_consent");
            Assert.True(o.Accepted, o.RejectReason);
            Assert.Contains("consent_signed", o.Effects);
            Assert.True(s.Consent!.Signed);
            Assert.True(s.Patient.Clinical.HasFlag("consent.signed"));
            var h = s.Consent.History.Last();
            Assert.Equal("valid", h.Id);
            Assert.Contains("credit.consent_valid", h.ScoreItems);
            Assert.Equal(ConsentSubsystem.AlreadySigned, Reject(Do(s, "sign_consent")));
        }

        // T09-2: a confused patient cannot give valid consent
        [Fact]
        public void T09_2_signing_a_confused_patient_is_critical_and_the_consent_is_not_valid()
        {
            var s = Start("STK-S1-A");                                                              // low sugar: confused, no capacity
            Assert.Contains("capacity:no", Do(s, "check_capacity").Effects);
            Assert.Contains("patient_cannot_follow:diagnosis", Do(s, "consent_discussion", ("topic", "diagnosis")).Effects);
            Assert.Empty(s.Consent!.Explained);
            var o = Do(s, "sign_consent");
            Assert.Contains("consent_invalid:no_capacity", o.Effects);
            Assert.False(s.Consent.Signed);
            Assert.True(s.Patient.Clinical.HasFlag("consent.invalid"));
            Assert.Contains(s.Consent.History, h => h.ScoreItems.Contains("critical.consent_signed_without_capacity"));
        }

        [Fact]
        public void The_capacity_check_names_the_causes_when_it_is_lost_and_not_checking_is_a_major_item()
        {
            var o = Do(Start("STK-S1-A"), "check_capacity");
            Assert.Contains("cause:confusion", o.Effects);
            var s = Start();
            ExplainAll(s); Do(s, "teach_back");
            Do(s, "sign_consent");
            Assert.Contains("major.consent_capacity_not_checked", s.Consent!.History.Last().ScoreItems);
        }

        [Fact]
        public void In_practice_a_patient_who_has_not_understood_cannot_sign_and_in_assessment_it_is_a_major_item()
        {
            var practice = Start();
            Discuss(practice, "diagnosis");
            Assert.Equal(ConsentSubsystem.UnderstandingTooLow, Reject(Do(practice, "sign_consent")));
            Assert.False(practice.Consent!.Signed);

            var assessment = Start(mode: SessionMode.Assessment);
            Discuss(assessment, "diagnosis");
            var o = Do(assessment, "sign_consent");
            Assert.True(o.Accepted);
            Assert.Contains("major.consent_signed_low_understanding", assessment.Consent!.History.Last().ScoreItems);
        }

        [Fact]
        public void E9_A_skipped_risks_and_alternatives_are_major_items_when_the_patient_signs_quickly()
        {
            var s = Start();
            Discuss(s, "diagnosis", "operation", "benefits", "no_treatment", "matters");              // understanding is high enough but the risks and alternatives were never explained
            Do(s, "check_capacity"); Do(s, "teach_back");
            var o = Do(s, "sign_consent");
            Assert.True(o.Accepted, o.RejectReason);
            var items = s.Consent!.History.Last().ScoreItems;
            Assert.Contains("major.consent_risks_skipped", items);
            Assert.Contains("major.consent_alternatives_skipped", items);
            Assert.Contains("missing:risks", o.Effects);
        }

        // ---------------------------------------------------------------- refusal, pressure, interpreters, minors

        [Fact]
        public void A_patient_who_has_been_pressed_refuses_and_pressing_is_a_professionalism_item()
        {
            var s = Start();
            ExplainAll(s); Do(s, "teach_back");
            Do(s, "press_to_sign"); Do(s, "press_to_sign");                                           // cooperation falls below the limit
            var o = Do(s, "sign_consent");
            Assert.Equal(ConsentSubsystem.PatientRefuses, Reject(o));
            Assert.Contains("call:senior", o.Effects);
            Assert.Equal(2, s.Consent!.History.Count(h => h.ScoreItems.Contains("major.consent_pressure")));
            Assert.False(s.Consent.Signed);
        }

        [Fact]
        public void Only_a_trained_interpreter_counts_and_a_patient_who_needs_one_cannot_sign_without()
        {
            var s = Start();
            Assert.Equal(ConsentSubsystem.MissingWho, Reject(Do(s, "request_interpreter")));
            Assert.Equal(ConsentSubsystem.UnknownInterpreter, Reject(Do(s, "request_interpreter", ("who", "neighbour"))));
            Assert.Contains("not_a_trained_interpreter", Do(s, "request_interpreter", ("who", "child")).Effects);
            Assert.Contains(s.Consent!.History, h => h.ScoreItems.Contains("major.consent_untrained_interpreter"));

            var lang = Start();
            lang.Behaviour!.State.Persona["communication_difficulty"] = "language";
            ExplainAll(lang); Do(lang, "teach_back"); Do(lang, "check_capacity");
            Assert.Equal(ConsentSubsystem.InterpreterNeeded, Reject(Do(lang, "sign_consent")));
            Assert.Contains("interpreter_arrives", Do(lang, "request_interpreter", ("who", "trained")).Effects);
            Assert.True(Do(lang, "sign_consent").Accepted);
        }

        [Fact]
        public void A_patient_under_18_signing_without_the_senior_involved_is_a_major_item()
        {
            var card = Content.Cases["VAR-S1-A"] with { Identity = Content.Cases["VAR-S1-A"].Identity with { Age = 16 } };
            var s = Start(card: card);
            ExplainAll(s); Do(s, "teach_back"); Do(s, "check_capacity");
            Do(s, "sign_consent");
            Assert.Contains("major.consent_minor_without_senior", s.Consent!.History.Last().ScoreItems);

            var withSenior = Start(card: card);
            ExplainAll(withSenior); Do(withSenior, "teach_back"); Do(withSenior, "check_capacity");
            Do(withSenior, "escalate", ("role", "senior"), ("s", "a"), ("b", "b"), ("a", "c"), ("r", "d"));
            Do(withSenior, "sign_consent");
            Assert.DoesNotContain("major.consent_minor_without_senior", withSenior.Consent!.History.Last().ScoreItems);
        }

        // ---------------------------------------------------------------- emergency without capacity

        [Fact]
        public void In_an_emergency_without_capacity_the_senior_routes_best_interests_and_not_otherwise()
        {
            var s = Start("STK-S1-A", scenario: "S2");
            Assert.Equal(ConsentSubsystem.NotAllowed, Reject(Do(s, "use_emergency_route")));        // the senior has not been called
            Do(s, "escalate", ("role", "senior"), ("s", "a"), ("b", "b"), ("a", "c"), ("r", "d"));
            Assert.Contains("consent_emergency_route", Do(s, "use_emergency_route").Effects);
            Assert.True(s.Patient.Clinical.HasFlag("consent.signed"));
            Assert.Contains(s.Consent!.History, h => h.ScoreItems.Contains("credit.consent_emergency_route"));
            Assert.Equal(ConsentSubsystem.NotAllowed, Reject(Do(Start("STK-S1-A"), "use_emergency_route")));      // a planned case does not use it
        }

        // ---------------------------------------------------------------- T09-3: no transfer without valid consent

        [Fact]
        public void T09_3_the_transfer_to_theatre_is_blocked_until_valid_consent_is_signed()
        {
            var s = Start();
            Do(s, "ask", ("topic", "pregnancy"));
            s.Patient.Clinical.SetFlag("ward.checklist_signed");
            var transfer = new[] { ("mode", "trolley"), ("escort", "porter,nurse") };
            Assert.Equal("BLOCKED:B-CONSENT", Reject(Do(s, "transfer_to_theatre", transfer)));
            ExplainAll(s); Do(s, "check_capacity"); Do(s, "teach_back"); Do(s, "sign_consent");
            Assert.NotEqual("BLOCKED:B-CONSENT", Do(s, "transfer_to_theatre", transfer).RejectReason);
        }

        [Fact]
        public void Capacity_is_never_shown_to_the_trainee_until_they_check_it()
        {
            var s = Start("STK-S1-A");
            Assert.DoesNotContain("capacity", s.Observable.ToJson());
            Assert.DoesNotContain("cause", s.Observable.ToJson());
        }
    }
}
