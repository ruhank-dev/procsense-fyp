using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    public class WristbandTests
    {
        private static Session New(string caseId = "VAR-S1-A") =>
            new Session(TestCase.Package().Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));

        private static ActionOutcome Do(Session s, string verb) => s.Apply(new SimAction(verb));

        private static void AskAndOpenNotes(Session s) { Do(s, "introduce_self"); Do(s, "ask_identity"); Do(s, "open_notes"); }

        [Fact] // T01-3b
        public void A_reported_mismatch_is_fixed_by_the_nurse_then_the_trainee_rereads_and_confirms()
        {
            var s = New("TEST-ID-MISMATCH");
            AskAndOpenNotes(s); Do(s, "read_wristband");
            Assert.Equal("ST1_MISMATCH", Do(s, "confirm_identity").RejectReason);

            var steps = new List<string>();
            s.Events.Subscribe<NurseTaskChanged>(e => steps.Add(e.State));
            var report = Do(s, "report_mismatch");

            Assert.True(report.Accepted);
            Assert.Equal(new[] { "CALLED", "AT_BEDSIDE", "REPLACING", "DONE" }, steps);
            Assert.Contains("wristband_replaced", report.Effects);
            Assert.Equal("DONE", s.Observable.Nurse);
            Assert.Null(s.Observable.Wristband); // must be read again
            Assert.False(s.Patient.Clinical.IdentityVerified);

            Do(s, "read_wristband");
            Assert.Equal("14/03/1984", s.Observable.Wristband!.DobText);
            Assert.True(Do(s, "confirm_identity").Accepted);
            Assert.True(s.Patient.Clinical.IdentityVerified);
        }

        [Fact]
        public void The_nurse_takes_no_simulated_time()
        {
            var s = New("TEST-ID-MISMATCH");
            AskAndOpenNotes(s); Do(s, "read_wristband");
            double before = s.Clock.SimSeconds;

            Do(s, "report_mismatch");

            Assert.Equal(before, s.Clock.SimSeconds);
        }

        [Fact]
        public void Reporting_when_nothing_is_wrong_does_not_call_the_nurse()
        {
            var s = New();
            var steps = new List<string>();
            s.Events.Subscribe<NurseTaskChanged>(e => steps.Add(e.State));

            var o = Do(s, "report_mismatch");

            Assert.Contains("no_mismatch_found", o.Effects);
            Assert.Empty(steps);
            Assert.Equal("", s.Observable.Nurse);
        }

        [Fact]
        public void A_problem_in_the_notes_goes_to_admissions_the_nurse_cannot_fix_it()
        {
            var json = File.ReadAllText(Path.Combine(TestCase.ContentRoot, "cases", "VAR-S1-A.json"))
                .Replace("\"mrn\": \"102938\"\n  },\n  \"allergies\"", "\"mrn\": \"999999\"\n  },\n  \"allergies\"");
            var card = ContentLoader.LoadCase(json.Replace("\r\n", "\n"), "notes test");
            var s = new Session(card, 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));
            AskAndOpenNotes(s); Do(s, "read_wristband");

            var o = Do(s, "report_mismatch");

            Assert.Contains(o.Effects, e => e.StartsWith("escalate_to_admissions:notes MRN"));
            Assert.Equal("ST1_MISMATCH", Do(s, "confirm_identity").RejectReason);
        }

        [Fact] // T01-4
        public void A_missing_band_cannot_be_read_or_confirmed()
        {
            var s = New("TEST-ID-MISSING-BAND");
            AskAndOpenNotes(s);

            Assert.Equal("WRISTBAND_MISSING", Do(s, "read_wristband").RejectReason);
            Assert.Equal("WRISTBAND_MISSING", Do(s, "confirm_identity").RejectReason);
        }

        [Fact]
        public void The_nurse_applies_a_missing_band_only_after_identity_is_verified_another_way()
        {
            var s = New("TEST-ID-MISSING-BAND");
            Do(s, "ask_identity");
            Assert.Equal("VERIFY_IDENTITY_FIRST", Do(s, "request_wristband").RejectReason); // notes not opened yet
            Assert.Null(s.Patient.Clinical.CurrentWristband);

            Do(s, "open_notes");
            var o = Do(s, "request_wristband");

            Assert.True(o.Accepted);
            Assert.Contains("wristband_applied", o.Effects);
            Assert.NotNull(s.Patient.Clinical.CurrentWristband);
            Do(s, "read_wristband");
            Assert.True(Do(s, "confirm_identity").Accepted);
        }

        [Fact]
        public void Asking_for_a_band_without_the_open_question_is_rejected()
        {
            var s = New("TEST-ID-MISSING-BAND");
            Do(s, "open_notes");

            Assert.Equal("VERIFY_IDENTITY_FIRST", Do(s, "request_wristband").RejectReason);
        }

        [Fact]
        public void A_reported_allergy_also_gets_its_band_but_the_reaction_must_still_be_asked()
        {
            var s = New("TEST-ID-MISSING-BAND-ALLERGY");
            Assert.False(s.Observable.AllergyBand);
            AskAndOpenNotes(s);

            var o = Do(s, "request_wristband");

            Assert.Contains("allergy_band_applied", o.Effects);
            Assert.True(s.Observable.AllergyBand);
            Assert.DoesNotContain(s.Observable.DisclosedFacts, f => f.Topic == "allergy_reaction");
            Do(s, "ask_allergy_reaction");
            Assert.Contains(s.Observable.DisclosedFacts, f => f.Topic == "allergy_reaction" && f.Answer.Contains("TEST-REACTION"));
        }

        [Fact]
        public void Requesting_a_band_when_one_is_on_does_nothing()
        {
            var s = New();

            var o = Do(s, "request_wristband");

            Assert.Contains("nothing_to_resolve", o.Effects);
            Assert.Equal("", s.Observable.Nurse);
        }

        [Fact]
        public void The_corrected_band_uses_the_walkthrough_format_for_the_real_identity()
        {
            var id = TestCase.Card().Identity;
            var real = TestCase.Card().Wristband!;

            Assert.Equal(real, WristbandResolution.CorrectBand(id)); // reproduces VAR-S1-A's own band exactly
        }

        [Fact]
        public void Mismatch_and_missing_band_sessions_replay_with_the_same_hash()
        {
            foreach (var (caseId, verbs) in new[]
            {
                ("TEST-ID-MISMATCH", new[] { "ask_identity", "open_notes", "read_wristband", "confirm_identity", "report_mismatch", "read_wristband", "confirm_identity" }),
                ("TEST-ID-MISSING-BAND-ALLERGY", new[] { "ask_identity", "open_notes", "read_wristband", "request_wristband", "read_wristband", "confirm_identity", "ask_allergy_reaction" }),
            })
            {
                var s = New(caseId);
                foreach (var v in verbs) Do(s, v);

                var r = SessionFile.Replay(SessionFile.Write(s), TestCase.Package(), () => Subsystems.Default(TestCase.Package()));

                Assert.True(r.Match, caseId);
                Assert.Equal(s.Observable.Hash(), r.Session.Observable.Hash());
            }
        }
    }
}
