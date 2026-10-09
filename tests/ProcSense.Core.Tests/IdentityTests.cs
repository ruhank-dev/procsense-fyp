using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    public class IdentityTests
    {
        private static Session New(CaseCard? card = null) =>
            new Session(card ?? TestCase.Card(), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));

        // TEST-ONLY case: VAR-S1-A with a different date of birth on the wristband.
        private static CaseCard WrongBandCard() =>
            ContentLoader.LoadCase(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "cases", "VAR-S1-A.json"))
                .Replace("\"dob_text\": \"14/03/1984\"", "\"dob_text\": \"15/03/1984\""), "VAR-S1-A (test: wrong band)");

        private static ActionOutcome Do(Session s, string verb) => s.Apply(new SimAction(verb));

        private static void FullCheck(Session s)
        {
            Do(s, "introduce_self"); Do(s, "ask_identity"); Do(s, "read_wristband"); Do(s, "open_notes");
        }

        [Fact] // T01-1
        public void Correct_path_verifies_identity()
        {
            var s = New();
            FullCheck(s);

            var confirm = Do(s, "confirm_identity");

            Assert.True(confirm.Accepted);
            Assert.True(s.Patient.Clinical.IdentityVerified);
            Assert.True(s.Observable.IdentityConfirmed);
        }

        [Fact] // T01-2
        public void Blood_test_before_identity_is_rejected_and_works_after()
        {
            var s = New();

            var early = Do(s, "order_blood_test");
            Assert.False(early.Accepted);
            Assert.Equal("IDENTITY_NOT_CONFIRMED", early.RejectReason);
            Assert.Contains("\"reject\":\"IDENTITY_NOT_CONFIRMED\"", s.Log.ToJsonl());

            FullCheck(s);
            Do(s, "confirm_identity");
            Assert.True(Do(s, "order_blood_test").Accepted);
        }

        [Fact] // T01-3
        public void Mismatch_cannot_be_confirmed()
        {
            var s = New(WrongBandCard());
            FullCheck(s);

            var confirm = Do(s, "confirm_identity");

            Assert.False(confirm.Accepted);
            Assert.Equal("ST1_MISMATCH", confirm.RejectReason);
            Assert.Contains(confirm.Effects, e => e.Contains("wristband date of birth '15/03/1984'"));
            Assert.False(s.Patient.Clinical.IdentityVerified);
            Assert.False(Do(s, "order_blood_test").Accepted);
        }

        [Fact]
        public void Report_mismatch_is_right_only_when_there_is_one()
        {
            var wrong = New(WrongBandCard());
            Do(wrong, "report_mismatch");
            Assert.True(wrong.Patient.Clinical.HasFlag(IdentitySubsystem.MismatchReported));

            var fine = New();
            var o = Do(fine, "report_mismatch");
            Assert.Contains("no_mismatch_found", o.Effects);
            Assert.True(fine.Patient.Clinical.HasFlag(IdentitySubsystem.UnnecessaryMismatchReport));
            Assert.False(fine.Patient.Clinical.HasFlag(IdentitySubsystem.MismatchReported));
        }

        [Fact]
        public void Confirm_needs_open_question_wristband_and_notes()
        {
            var s = New();
            Do(s, "read_wristband");
            Assert.Equal("IDENTITY_CHECK_INCOMPLETE", Do(s, "confirm_identity").RejectReason);

            Do(s, "open_notes");
            Assert.Equal("IDENTITY_CHECK_INCOMPLETE", Do(s, "confirm_identity").RejectReason); // never asked

            Do(s, "ask_identity");
            Assert.True(Do(s, "confirm_identity").Accepted);
        }

        [Fact] // L1-WT1
        public void Reading_the_name_aloud_arms_wrong_blood_and_does_not_count_as_asking()
        {
            var s = New();
            s.Apply(new SimAction("read_name_to_patient"));
            Do(s, "read_wristband"); Do(s, "open_notes");

            Assert.True(s.Patient.Clinical.HasFlag(IdentitySubsystem.WrongBloodRisk));
            Assert.Equal("IDENTITY_CHECK_INCOMPLETE", Do(s, "confirm_identity").RejectReason);
            Assert.DoesNotContain(s.Observable.DisclosedFacts, f => f.Topic == "identity");
        }

        [Fact] // L1-03
        public void Introducing_yourself_first_changes_trust_and_anxiety_by_the_content_values()
        {
            var effect = TestCase.Package().IntroduceSelf;
            var s = New();

            Do(s, "introduce_self");

            Assert.Equal((int)effect.Trust, s.Patient.Behavioural.Trust);
            Assert.Equal((int)effect.Anxiety, s.Patient.Behavioural.Anxiety);
        }

        [Fact]
        public void Introducing_yourself_after_another_action_earns_nothing()
        {
            var s = New();
            Do(s, "ask_identity");

            Do(s, "introduce_self");

            Assert.Equal(0, s.Patient.Behavioural.Trust);
            Assert.Equal(0, s.Patient.Behavioural.Anxiety);
        }

        [Fact]
        public void Answers_come_from_the_case_and_stay_hidden_until_asked()
        {
            var s = New();
            Assert.Empty(s.Observable.DisclosedFacts);

            Do(s, "ask_identity"); Do(s, "ask_allergies"); Do(s, "ask_reason_for_admission"); Do(s, "ask_responsible_adult");

            var facts = s.Observable.DisclosedFacts.ToDictionary(f => f.Topic, f => f.Answer);
            Assert.Equal(s.Case.SpokenIdentityAnswer, facts["identity"]);
            Assert.Equal(s.Case.AllergyAnswer, facts["allergies"]);
            Assert.Equal(s.Case.ReasonAnswer, facts["reason_for_admission"]);
            Assert.Equal(s.Case.ResponsibleAdult.Text, facts["responsible_adult"]);
            Assert.True(s.Observable.AllergiesKnown);
        }

        [Fact]
        public void Asking_about_an_allergy_reaction_when_there_are_no_allergies_invents_nothing()
        {
            var s = New();

            var o = Do(s, "ask_allergy_reaction");

            Assert.True(o.Accepted);
            Assert.Contains("nothing_to_describe:allergy_reaction", o.Effects);
            Assert.DoesNotContain(s.Observable.DisclosedFacts, f => f.Topic == "allergy_reaction");
            Assert.True(s.Patient.Clinical.HasFlag(IdentitySubsystem.AllergyReactionAsked));
        }

        [Fact]
        public void Wristband_and_notes_appear_in_the_observable_once_read()
        {
            var s = New();
            Assert.Null(s.Observable.Wristband);

            Do(s, "read_wristband"); Do(s, "open_notes");

            Assert.Equal("14/03/1984", s.Observable.Wristband!.DobText);
            Assert.Equal("102938", s.Observable.Notes!.Mrn);
        }

        [Fact]
        public void A_whole_identity_session_replays_with_the_same_hash()
        {
            var s = New();
            FullCheck(s); Do(s, "confirm_identity"); Do(s, "ask_allergies"); Do(s, "order_blood_test");
            var saved = ProcSense.Core.Replay.SessionFile.Write(s);

            var r = ProcSense.Core.Replay.SessionFile.Replay(saved, TestCase.Package(), () => Subsystems.Default(TestCase.Package()));

            Assert.True(r.Match);
            Assert.Equal(s.Observable.Hash(), r.Session.Observable.Hash());
        }
    }
}
