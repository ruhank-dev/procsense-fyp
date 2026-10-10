using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Step 4: examination findings, permission, the order of examining and the airway checks (p0-simulation-workflow-s07, plan tests T04-1 to T04-5).</summary>
    public class ExaminationTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        // A session on the VAR-S1-A card whose examination findings come from the given case file (VAR-S1-A's own file when none is given).
        private static Session NewSession(string? caseFile = null, SessionMode mode = SessionMode.Practice, bool confirmed = true, CaseCard? card = null)
        {
            var chain = caseFile == null ? Subsystems.Default(Content)
                : Subsystems.Default(Content).Where(x => !(x is ExaminationSubsystem)).Concat(new ISubsystem[] { new ExaminationSubsystem(Content, File_(caseFile)) });
            var s = new Session(card ?? Content.Cases["VAR-S1-A"], 42, mode, chain);
            if (confirmed) foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(s.Apply(Act(verb)).Accepted, verb);
            return s;
        }

        private static SimAction Act(string verb, params (string k, string v)[] p) =>
            new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p)
        {
            var o = s.Apply(Act(verb, p));
            return o;
        }

        private static ActionOutcome Examine(Session s, string region) => Do(s, "examine", ("region", region));
        private static ActionOutcome Airway(Session s, string item) => Do(s, "airway_check", ("item", item));
        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // ---------------------------------------------------------------- T04-1 the correct examination (walkthrough Part 12, Scene 4)

        [Fact]
        public void Examining_VAR_S1_A_in_order_with_permission_shows_exactly_the_walkthrough_findings_and_costs_no_trust()
        {
            var s = NewSession();
            Assert.Empty(s.Observable.Findings);
            Assert.Empty(s.Observable.Airway);

            Assert.True(Do(s, "ask_exam_permission").Accepted);
            Examine(s, "eyes"); Examine(s, "chest"); Examine(s, "abdomen"); Assert.True(Do(s, "murphy_test").Accepted);
            Airway(s, "mouth_opening"); Airway(s, "mallampati"); Airway(s, "neck");

            Assert.Equal(new[]
            {
                "eyes: Whites of the eyes are white; no jaundice.",
                "chest: Heart and lungs sound normal.",
                "abdomen: Tummy soft; mild tenderness under the right ribs, only with deep pressure.",
                "murphy: Murphy's sign negative.",
            }, s.Observable.Findings.Select(f => f.Region + ": " + f.Text));
            Assert.Equal(new[] { "mallampati: Mallampati class 2.", "mouth_opening: Mouth opens well (3 fingers).", "neck: Neck moves normally." }, s.Observable.Airway.Select(a => a.Item + ": " + a.Text));
            Assert.Equal(0, s.Patient.Behavioural.Trust);
            Assert.Equal(new[] { "G-EXAM-PERMISSION", "G-EXAM-REGIONS", "G-EXAM-AIRWAY" }, s.Observable.Goals.Where(g => g.State == "complete" && g.Id.StartsWith("G-EXAM")).Select(g => g.Id));
        }

        [Fact]
        public void Nothing_is_shown_before_the_trainee_examines_it()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            Examine(s, "eyes");
            Assert.Equal(new[] { "eyes" }, s.Observable.Findings.Select(f => f.Region));
            Assert.DoesNotContain("Murphy", s.Observable.ToJson());
            Assert.DoesNotContain("Mallampati", s.Observable.ToJson());
        }

        [Fact]
        public void Examining_the_same_region_twice_adds_nothing()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            Examine(s, "eyes");
            var again = Examine(s, "eyes");
            Assert.Contains("already_known:ex.eyes", again.Effects);
            Assert.Single(s.Observable.Findings);
        }

        // ---------------------------------------------------------------- T04-2 permission (E4-A)

        [Fact]
        public void Examining_without_asking_permission_costs_one_trust_once_and_is_kept_as_evidence()
        {
            var s = NewSession();
            var first = Examine(s, "eyes");
            Assert.True(first.Accepted);
            Assert.Contains("professionalism:exam_without_permission", first.Effects);
            Assert.Contains("reaction:Oh!", first.Effects);
            Assert.Equal(-1, s.Patient.Behavioural.Trust);
            Assert.True(s.Patient.Clinical.HasFlag(ExaminationSubsystem.NoPermission));
            Assert.Equal(new[] { "eyes" }, s.Observable.Findings.Select(f => f.Region));        // the finding is still found

            Examine(s, "chest");
            Assert.Equal(-1, s.Patient.Behavioural.Trust);                                       // not repeated for every region
        }

        [Fact]
        public void Asking_permission_late_stops_further_penalties_and_completes_the_permission_goal()
        {
            var s = NewSession();
            Examine(s, "eyes");
            Do(s, "ask_exam_permission");
            Examine(s, "chest");
            Assert.Equal(-1, s.Patient.Behavioural.Trust);
            Assert.Equal("complete", s.Observable.Goals.Single(g => g.Id == "G-EXAM-PERMISSION").State);
        }

        [Fact]
        public void Offering_a_chaperone_is_recorded_but_never_required()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            var o = Do(s, "offer_chaperone");
            Assert.Contains("chaperone_offered", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag(ExaminationSubsystem.ChaperoneOffered));
            var s2 = NewSession();
            Do(s2, "ask_exam_permission");
            Assert.True(Examine(s2, "eyes").Accepted);
            Assert.Equal(0, s2.Patient.Behavioural.Trust);
        }

        // ---------------------------------------------------------------- T04-3 order (E4-B)

        [Fact]
        public void Pressing_the_painful_region_first_raises_pain_and_lowers_trust_once()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            long before = s.Patient.Physiological.Vitals.PainScore;
            var o = Examine(s, "abdomen");
            Assert.Equal(before + 1, s.Patient.Physiological.Vitals.PainScore);
            Assert.Equal(-1, s.Patient.Behavioural.Trust);
            Assert.Contains("professionalism:painful_region_first", o.Effects);
            Assert.Contains("reaction:Ow! That hurts.", o.Effects);

            Examine(s, "eyes"); Examine(s, "chest");
            Assert.Equal(before + 1, s.Patient.Physiological.Vitals.PainScore);                  // once
            Assert.Equal(-1, s.Patient.Behavioural.Trust);
        }

        [Fact]
        public void Starting_away_from_the_pain_costs_nothing()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            long before = s.Patient.Physiological.Vitals.PainScore;
            Examine(s, "eyes"); Examine(s, "abdomen");
            Assert.Equal(before, s.Patient.Physiological.Vitals.PainScore);
            Assert.Equal(0, s.Patient.Behavioural.Trust);
            Assert.False(s.Patient.Clinical.HasFlag(ExaminationSubsystem.PainfulFirst));
        }

        [Fact]
        public void Pain_never_goes_past_ten()
        {
            var s = NewSession();
            var v = s.Patient.Physiological.Vitals;
            s.Patient.Physiological.Vitals = v with { PainScore = 10 };
            Do(s, "ask_exam_permission");
            Examine(s, "abdomen");
            Assert.Equal(10, s.Patient.Physiological.Vitals.PainScore);
        }

        // ---------------------------------------------------------------- T04-4 Murphy's sign (L4-02, E4-C)

        [Fact]
        public void A_positive_Murphy_sign_makes_the_patient_cry_out_and_opens_the_acute_branch_flag()
        {
            var s = NewSession("VAR-S1-E");
            Do(s, "ask_exam_permission");
            Examine(s, "eyes");
            var o = Do(s, "murphy_test");
            Assert.Contains("reaction:Ow!", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("finding.murphy_positive"));
            Assert.Contains("murphy: Tender under the right ribs; Murphy's sign positive.", s.Observable.Findings.Select(f => f.Region + ": " + f.Text));
        }

        [Fact]
        public void If_Murphy_is_never_tested_the_finding_stays_hidden_even_after_examining_the_abdomen()
        {
            var s = NewSession("VAR-S1-E");
            Do(s, "ask_exam_permission");
            Examine(s, "eyes"); Examine(s, "abdomen");
            Assert.DoesNotContain(s.Observable.Findings, f => f.Text.Contains("Murphy"));
            Assert.False(s.Patient.Clinical.HasFlag("finding.murphy_positive"));
        }

        [Fact]
        public void Murphys_test_belongs_to_the_S1_cases_only()
        {
            var s = NewSession(card: Content.Cases["VAR-S1-A"] with { Scenario = "S2" });
            Assert.Equal(ExaminationSubsystem.MurphyScenarioOnly, Reject(Do(s, "murphy_test")));
        }

        // ---------------------------------------------------------------- T04-5 rigid abdomen

        [Fact]
        public void A_rigid_abdomen_makes_calling_the_senior_a_goal_that_was_not_there_before()
        {
            var s = NewSession("SC-1");
            Assert.DoesNotContain(s.Observable.Goals, g => g.Id == "G-SENIOR-NOW");
            Do(s, "ask_exam_permission");
            Examine(s, "general");
            Assert.DoesNotContain(s.Observable.Goals, g => g.Id == "G-SENIOR-NOW");

            Examine(s, "abdomen");
            Assert.True(s.Patient.Clinical.HasFlag("finding.rigid_abdomen"));
            Assert.Equal("pending", s.Observable.Goals.Single(g => g.Id == "G-SENIOR-NOW").State);
            Assert.Equal("A rigid, board-like abdomen is an emergency: call the senior now.", s.Graph.Goals.Single(g => g.Id == "G-SENIOR-NOW").Hint);

            s.Patient.Clinical.SetFlag("escalated.senior");                                       // the escalation verb itself is built in step 8
            Do(s, "wait");
            Assert.Equal("complete", s.Observable.Goals.Single(g => g.Id == "G-SENIOR-NOW").State);
        }

        // ---------------------------------------------------------------- airway (L4-04)

        [Fact]
        public void Airway_findings_are_kept_for_the_anaesthetist()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            Airway(s, "mallampati");
            Assert.Equal("Mallampati class 2.", s.Patient.Clinical.Airway["mallampati"]);
            Assert.Equal(new[] { "mallampati" }, s.Patient.Clinical.Airway.Keys);
        }

        [Fact]
        public void An_airway_check_the_case_does_not_describe_is_refused_rather_than_invented()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            Assert.Equal(ExaminationSubsystem.ItemNotInCase, Reject(Airway(s, "thyromental")));
            Assert.Empty(s.Observable.Airway);
        }

        [Fact]
        public void The_airway_goal_waits_until_every_airway_check_the_case_has_is_made()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            Airway(s, "mouth_opening"); Airway(s, "mallampati");
            Assert.Equal("pending", s.Observable.Goals.Single(g => g.Id == "G-EXAM-AIRWAY").State);
            Airway(s, "neck");
            Assert.Equal("complete", s.Observable.Goals.Single(g => g.Id == "G-EXAM-AIRWAY").State);
        }

        // ---------------------------------------------------------------- refusals

        [Fact]
        public void Bad_examination_requests_are_refused_with_a_reason_and_change_nothing()
        {
            var s = NewSession();
            Do(s, "ask_exam_permission");
            Assert.Equal(ExaminationSubsystem.MissingRegion, Reject(Do(s, "examine")));
            Assert.Equal(ExaminationSubsystem.RegionNotInCase, Reject(Examine(s, "testicles")));
            Assert.Equal(ExaminationSubsystem.MissingItem, Reject(Do(s, "airway_check")));
            Assert.Equal(ExaminationSubsystem.UnknownAirwayItem, Reject(Airway(s, "teeth")));
            Assert.Empty(s.Observable.Findings);
            Assert.Equal(0, s.Patient.Behavioural.Trust);
            Assert.False(s.Patient.Clinical.HasFlag(ExaminationSubsystem.Started));
        }

        [Fact]
        public void Nothing_can_be_examined_before_the_identity_is_confirmed()
        {
            var s = NewSession(confirmed: false);
            Assert.Equal(IdentitySubsystem.IdentityNotConfirmed, Reject(Examine(s, "eyes")));
            Assert.Equal(IdentitySubsystem.IdentityNotConfirmed, Reject(Do(s, "ask_exam_permission")));
            Assert.Empty(s.Observable.Findings);
        }

        [Fact]
        public void A_case_without_a_case_file_has_nothing_to_examine()
        {
            var content = ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { "VAR-S1-A" }, new string[0]);
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(Act(verb));
            Assert.Equal(ExaminationSubsystem.RegionNotInCase, Reject(Examine(s, "eyes")));
            Assert.DoesNotContain(s.Observable.Goals, g => g.Id == "G-EXAM-REGIONS");
        }

        [Fact]
        public void In_assessment_mode_the_findings_are_shown_but_the_goals_and_hints_are_not()
        {
            var s = NewSession(mode: SessionMode.Assessment);
            Do(s, "ask_exam_permission");
            Examine(s, "eyes");
            Assert.Single(s.Observable.Findings);
            Assert.Empty(s.Observable.Goals);
            Assert.Equal("", s.Observable.Hint);
        }

        // ---------------------------------------------------------------- the 53 case files

        [Fact]
        public void Every_case_file_marks_its_rigid_and_Murphy_findings_and_keeps_Murphy_and_airway_apart_from_the_abdomen()
        {
            int murphy = 0, rigid = 0, airway = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                var f = File_(Path.GetFileNameWithoutExtension(path));
                foreach (var e in f.Examination)
                {
                    var flags = e.Flags ?? new List<string>();
                    if (System.Text.RegularExpressions.Regex.IsMatch(e.Finding, @"\brigid\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { rigid++; Assert.Contains("finding.rigid_abdomen", flags); }
                    if (e.Finding.IndexOf("Murphy's sign positive", StringComparison.OrdinalIgnoreCase) >= 0) { murphy++; Assert.Contains("finding.murphy_positive", flags); Assert.Equal("murphy", e.System); Assert.Equal("S1", f.Scenario); }
                    if (e.System == "murphy") Assert.Contains("examine:murphy", e.DiscloseOn);
                    if (e.DiscloseOn.Any(d => d.StartsWith("airway_check:", StringComparison.Ordinal)))
                    {
                        airway++;
                        Assert.Single(e.DiscloseOn);
                        Assert.Contains(e.DiscloseOn[0].Substring("airway_check:".Length), ExaminationSubsystem.AirwayItems);
                    }
                }
            }
            Assert.True(murphy >= 2 && rigid >= 4 && airway >= 3, $"murphy {murphy}, rigid {rigid}, airway {airway}");
        }
    }
}
