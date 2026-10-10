using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Investigations: states, indications and turnaround (p0-clinical-logic-s06, plan tests T-IV2, T-IV4 and T-IV5).</summary>
    public class InvestigationsTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // The VAR-S1-A card; the case file (results, minutes, scenario's needed tests) comes from `caseFile`, and the scenario can be forced.
        private static Session NewSession(string? caseFile = null, string scenario = "S1", SessionMode mode = SessionMode.Practice, bool confirmed = true, ulong seed = 42)
        {
            var chain = caseFile == null ? Subsystems.Default(Content)
                : Subsystems.Default(Content).Where(x => !(x is InvestigationsSubsystem)).Concat(new ISubsystem[] { new InvestigationsSubsystem(Content, File_(caseFile)) });
            var card = scenario == "S1" ? Content.Cases["VAR-S1-A"] : Content.Cases["VAR-S1-A"] with { Scenario = scenario };
            var s = new Session(card, seed, mode, chain);
            if (confirmed) foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static string StateOf(Session s, string test) => s.Patient.Clinical.Investigations.Last(x => x.Test == test).State;

        private static ActionOutcome Order(Session s, string test, params (string, string)[] more) => Do(s, "order_investigation", new[] { ("test", test) }.Concat(more).ToArray());

        private static void Minutes(Session s, double minutes) => s.Tick(minutes * 60);

        private static List<string> Kinds(Session s, string test) =>
            s.InvestigationHistory.Where(h => h.Kind.StartsWith("investigation:" + test + ":")).Select(h => h.Kind.Substring(("investigation:" + test + ":").Length)).ToList();

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_defines_every_test_in_the_walkthrough_table_with_its_kind_tube_and_place()
        {
            var t = Content.Investigations;
            Assert.Equal(23, t.Tests.Count);
            Assert.Equal(17, t.Indications.Count);
            Assert.All(t.Tests.Where(x => x.Kind == "blood"), x => Assert.Contains(x.Tube, t.Tubes));
            Assert.All(t.Tests.Where(x => x.Kind == "imaging"), x => Assert.Contains(x.Location, Content.Locations.Locations.Select(l => l.Id)));
            Assert.Equal("serum_gold", t.Find("lft")!.Tube);
            Assert.Equal("citrate_blue", t.Find("clotting")!.Tube);
            Assert.Equal(45, t.Find("fbc")!.MinMinutes);
            Assert.Equal((60, 120), ((int)t.Find("ct")!.MinMinutes, (int)t.Find("ct")!.MaxMinutes));
        }

        [Fact]
        public void Every_result_in_the_53_case_files_belongs_to_a_test()
        {
            int n = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
                foreach (var r in File_(Path.GetFileNameWithoutExtension(path)).Results)
                {
                    Assert.NotNull(Content.Investigations.TestOfAnalyte(r.Test));
                    n++;
                }
            Assert.True(n > 150);
        }

        [Theory]
        [InlineData("Potassium (repeat)", "Potassium", true)]
        [InlineData("Lactate (repeat after fluids)", "Lactate", true)]
        [InlineData("Capillary glucose (repeat in 30 minutes)", "Capillary glucose", true)]
        [InlineData("WBC", "WBC", false)]
        public void A_repeat_result_is_recognised_and_belongs_to_the_same_analyte(string name, string analyte, bool repeat)
        {
            Assert.Equal(analyte, InvestigationTables.BaseName(name));
            Assert.Equal(repeat, InvestigationTables.IsRepeat(name));
        }

        // ---------------------------------------------------------------- the state machine (T-IV2)

        [Fact]
        public void The_liver_tests_and_the_pregnancy_test_reach_RESULT_AVAILABLE_after_their_turnaround()
        {
            var s = NewSession();
            Assert.Contains("ordered:lft", Order(s, "lft").Effects);
            Assert.Equal("REQUESTED", StateOf(s, "lft"));
            Assert.Contains("sample_taken:lft", Do(s, "take_sample", ("test", "lft"), ("tube", "serum_gold")).Effects);
            Assert.Equal("PROCESSING", StateOf(s, "lft"));
            Order(s, "pregnancy"); Do(s, "perform_bedside_test", ("test", "pregnancy"));

            var ready = new List<string>();
            s.Events.Subscribe<ResultReady>(e => ready.Add(e.Test + ":" + e.ResultId));
            Minutes(s, 44);
            Assert.Equal("PROCESSING", StateOf(s, "lft"));
            Minutes(s, 1.1);
            Assert.Equal("RESULT_AVAILABLE", StateOf(s, "lft"));
            Assert.Equal("RESULT_AVAILABLE", StateOf(s, "pregnancy"));
            Assert.Equal(new[] { "lft:lab.bilirubin", "lft:lab.alt", "lft:lab.alp", "lft:lab.ggt", "lft:lab.albumin", "pregnancy:lab.upt" }.OrderBy(x => x), ready.OrderBy(x => x));
        }

        [Fact]
        public void A_test_goes_through_every_state_in_order_and_the_result_is_only_the_cases()
        {
            var s = NewSession();
            Order(s, "lft"); Do(s, "take_sample", ("test", "lft"));
            Minutes(s, 46);
            var view = Do(s, "view_result", ("test", "lft"));
            Assert.Equal(new[] { "result:lab.bilirubin", "result:lab.alt", "result:lab.alp", "result:lab.ggt", "result:lab.albumin" }, view.Effects.Where(e => e.StartsWith("result:")));
            Assert.Equal("VIEWED", StateOf(s, "lft"));
            Assert.Contains("acknowledged:lft", Do(s, "acknowledge_result", ("test", "lft")).Effects);
            Assert.Equal("ACKNOWLEDGED", StateOf(s, "lft"));
            Assert.Equal(new[] { "REQUESTED", "SAMPLE_TAKEN", "PROCESSING", "RESULT_AVAILABLE", "VIEWED", "ACKNOWLEDGED" }, Kinds(s, "lft"));
        }

        [Fact]
        public void A_bedside_test_is_done_at_the_bedside_and_a_blood_test_is_not()
        {
            var s = NewSession();
            Order(s, "pregnancy"); Order(s, "lft");
            Assert.Equal(InvestigationsSubsystem.WrongKind, Reject(Do(s, "take_sample", ("test", "pregnancy"))));
            Assert.Equal(InvestigationsSubsystem.WrongKind, Reject(Do(s, "perform_bedside_test", ("test", "lft"))));
            Assert.True(Do(s, "perform_bedside_test", ("test", "pregnancy")).Accepted);
            Assert.True(Do(s, "take_sample", ("test", "lft")).Accepted);
        }

        [Fact]
        public void How_the_sample_was_taken_is_kept_for_the_result_generator()
        {
            var s = NewSession();
            Order(s, "u_and_e");
            Do(s, "take_sample", ("test", "u_and_e"), ("tube", "serum_gold"), ("arm", "drip_arm"), ("label", "bedside"));
            var sample = s.Patient.Clinical.Investigations.Single().Sample;
            Assert.Equal("serum_gold", sample["tube"]);
            Assert.Equal("drip_arm", sample["arm"]);
            Assert.Equal("bedside", sample["label"]);
        }

        // ---------------------------------------------------------------- imaging needs the patient in the scan room

        [Fact]
        public void An_imaging_test_waits_until_the_patient_reaches_the_scan_room_then_takes_the_cases_minutes()
        {
            var s = NewSession("SC-14", "S2");
            var ordered = Order(s, "ultrasound");
            Assert.Equal("REQUESTED", StateOf(s, "ultrasound"));
            Minutes(s, 30);
            Assert.Equal("REQUESTED", StateOf(s, "ultrasound"));                 // nothing happens while she is in the bay
            Do(s, "request_transport", ("to", "ultrasound"), ("mode", "trolley"), ("escort", "porter,nurse"), ("monitoring", "none"));
            for (int i = 0; i < 3000 && StateOf(s, "ultrasound") == "REQUESTED"; i++) s.Tick(1);
            Assert.Equal("PROCESSING", StateOf(s, "ultrasound"));
            Assert.Equal("ultrasound", s.Patient.Clinical.Location);
            Assert.Equal(new[] { "REQUESTED", "TRANSPORTED", "PROCESSING" }, Kinds(s, "ultrasound"));
            long start = s.InvestigationHistory.Single(h => h.Kind == "investigation:ultrasound:PROCESSING").Step;
            for (int i = 0; i < 4000 && StateOf(s, "ultrasound") == "PROCESSING"; i++) s.Tick(1);
            long ready = s.InvestigationHistory.Single(h => h.Kind == "investigation:ultrasound:RESULT_AVAILABLE").Step;
            Assert.InRange(ready - start, 45 * 60 * 2, 45 * 60 * 2 + 4);          // the case states 45 minutes
            Assert.NotNull(ordered);
        }

        [Fact]
        public void An_imaging_order_can_carry_the_transfer_so_the_patient_goes_to_the_scan_room()
        {
            var s = NewSession("SC-14", "S2");
            var o = Order(s, "ultrasound", ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true"));
            Assert.True(o.Accepted);
            Assert.Contains("transport_to:ultrasound", o.Effects);
            Assert.Equal("REQUESTED", s.Patient.Clinical.Transport!.State);
            for (int i = 0; i < 3000 && StateOf(s, "ultrasound") == "REQUESTED"; i++) s.Tick(1);
            Assert.Equal("PROCESSING", StateOf(s, "ultrasound"));
        }

        [Fact]
        public void A_refused_transfer_means_the_imaging_order_is_not_made()
        {
            var s = NewSession("SC-14", "S2");
            var o = Order(s, "ultrasound", ("mode", "hover"));
            Assert.Equal("UNKNOWN_MODE", Reject(o));
            Assert.Empty(s.Patient.Clinical.Investigations);
        }

        [Fact]
        public void Imaging_the_case_states_no_result_for_takes_a_time_in_the_books_default_range_and_returns_nothing()
        {
            var s = NewSession();                                                // VAR-S1-A has no chest X-ray result
            var ready = new List<ResultReady>();
            s.Events.Subscribe<ResultReady>(ready.Add);
            Order(s, "chest_xray", ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true"));
            for (int i = 0; i < 3000 && StateOf(s, "chest_xray") == "REQUESTED"; i++) s.Tick(1);
            long start = s.InvestigationHistory.Single(h => h.Kind == "investigation:chest_xray:PROCESSING").Step;
            for (int i = 0; i < 8000 && StateOf(s, "chest_xray") == "PROCESSING"; i++) s.Tick(1);
            long done = s.InvestigationHistory.Single(h => h.Kind == "investigation:chest_xray:RESULT_AVAILABLE").Step;
            Assert.InRange(done - start, 60 * 60 * 2, 120 * 60 * 2 + 2);
            Assert.Equal(new[] { "" }, ready.Select(r => r.ResultId));
            Assert.Empty(s.Patient.Clinical.Investigations.Single().ResultIds);
        }

        // ---------------------------------------------------------------- repeats

        [Fact]
        public void A_second_order_returns_the_cases_repeat_results()
        {
            var s = NewSession("VAR-S1-J");
            Order(s, "u_and_e"); Do(s, "take_sample", ("test", "u_and_e"));
            Minutes(s, 50);
            Assert.Equal("RESULT_AVAILABLE", StateOf(s, "u_and_e"));
            Assert.Equal(new[] { "lab.k1", "lab.creat" }, s.Patient.Clinical.Investigations.Single().ResultIds);
            Assert.True(Order(s, "u_and_e").Accepted);                             // a repeat after the first result
            var second = s.Patient.Clinical.Investigations.Last();
            Assert.Equal(2, second.Instance);
            Do(s, "take_sample", ("test", "u_and_e"));
            Assert.Equal(new[] { "lab.k2" }, second.ResultIds);
            Minutes(s, 80);
            Assert.Equal("PROCESSING", StateOf(s, "u_and_e"));                      // the repeat takes the 90 minutes the case states
            Minutes(s, 11);
            Assert.Equal("RESULT_AVAILABLE", StateOf(s, "u_and_e"));
        }

        [Fact]
        public void A_repeat_with_no_repeat_result_in_the_case_returns_the_same_results()
        {
            var s = NewSession();
            Order(s, "lft"); Do(s, "take_sample", ("test", "lft")); Minutes(s, 46);
            Order(s, "lft"); Do(s, "take_sample", ("test", "lft"));
            var all = s.Patient.Clinical.Investigations;
            Assert.Equal(all[0].ResultIds, all[1].ResultIds);
        }

        // ---------------------------------------------------------------- indications

        [Fact]
        public void For_a_planned_patient_the_walkthroughs_right_set_is_appropriate_and_the_rest_is_not_indicated()
        {
            var s = NewSession();                                                // VAR-S1-A: liver tests and a pregnancy test, nothing else (decision a.d1)
            Assert.Contains("indication:appropriate", Order(s, "lft").Effects);
            Assert.Contains("indication:appropriate", Order(s, "pregnancy").Effects);
            foreach (var test in new[] { "chest_xray", "ecg", "clotting", "u_and_e", "fbc", "crp", "urine_dipstick", "hba1c", "mrcp" })
                Assert.Contains("indication:not_indicated", Order(s, test).Effects);
            Assert.Contains("indication:appropriate", Order(s, "group_and_save").Effects);                     // per hospital rule: never penalised
            Assert.Equal(9, s.InvestigationHistory.Count(h => h.Kind == "test_not_indicated"));
            Assert.All(s.InvestigationHistory.Where(h => h.Kind == "test_not_indicated"), h => Assert.Contains("unnecessary.test_ordered", h.ScoreItems));
        }

        [Fact]
        public void For_an_emergency_patient_the_blood_set_and_a_dipstick_are_always_appropriate()
        {
            var s = NewSession("VAR-S2-PC", "S2");
            foreach (var test in new[] { "fbc", "crp", "u_and_e", "urine_dipstick" })
                Assert.Contains("indication:appropriate", Order(s, test).Effects);
            Assert.Contains("indication:not_indicated", Order(s, "hba1c").Effects);          // not used for S2
            Assert.Contains("indication:not_indicated", Order(s, "mrcp").Effects);
        }

        [Fact]
        public void A_conditional_test_is_appropriate_when_the_cases_story_states_a_result_for_it()
        {
            var s = NewSession("STK-S2-C", "S2");                               // morphine, slow breathing: the case states a chest X-ray
            Assert.Contains("indication:appropriate", Order(s, "chest_xray").Effects);
            var plain = NewSession("VAR-S2-PC", "S2");
            Assert.Contains("indication:not_indicated", Order(plain, "chest_xray").Effects);
        }

        [Fact]
        public void The_indication_chip_is_shown_in_practice_mode_only()
        {
            var practice = NewSession();
            Order(practice, "ecg");
            Assert.Equal("not_indicated", practice.Observable.Investigations.Single().Indication);
            var assessment = NewSession(mode: SessionMode.Assessment);
            Order(assessment, "ecg");
            Assert.Equal("", assessment.Observable.Investigations.Single().Indication);
            Assert.Equal("NOT_INDICATED", assessment.Patient.Clinical.Investigations.Single().Indication);   // the engine still knows
        }

        [Fact]
        public void At_readiness_the_tests_the_case_needed_and_the_trainee_never_ordered_are_missed_once()
        {
            var s = NewSession();
            var first = Do(s, "decide_ready", ("option", "postpone"));
            Assert.Equal(new[] { "missed_test:lft", "missed_test:pregnancy" }, first.Effects.Where(e => e.StartsWith("missed_test:")).OrderBy(e => e));
            Assert.Contains(s.InvestigationHistory, h => h.Kind == "test_missed" && h.ScoreItems.Contains("missed.test_not_ordered"));
            Assert.DoesNotContain(Do(s, "decide_ready", ("option", "postpone")).Effects, e => e.StartsWith("missed_test:"));    // reported once

            var good = NewSession();
            Order(good, "lft"); Order(good, "pregnancy");
            Assert.DoesNotContain(Do(good, "decide_ready", ("option", "proceed")).Effects, e => e.StartsWith("missed_test:"));
        }

        [Fact]
        public void The_tests_a_case_needs_are_the_always_set_plus_the_ones_its_story_states()
        {
            Assert.Equal(new[] { "lft", "pregnancy" }, NeededOf(NewSession()).OrderBy(x => x));
            var emergency = NewSession("VAR-S2-PC", "S2");
            Assert.Subset(new HashSet<string>(NeededOf(emergency)), new HashSet<string> { "fbc", "crp", "u_and_e", "urine_dipstick" });
        }

        private static IReadOnlyList<string> NeededOf(Session s) => ((InvestigationsSubsystem)typeof(Session).GetProperty("Investigations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(s)!).NeededTests();

        // ---------------------------------------------------------------- delays (T-IV5)

        [Fact]
        public void A_lab_delay_pushes_back_every_blood_test_still_processing_and_not_imaging_or_bedside_tests()
        {
            var s = NewSession();
            Order(s, "lft"); Do(s, "take_sample", ("test", "lft"));
            Order(s, "pregnancy"); Do(s, "perform_bedside_test", ("test", "pregnancy"));
            Minutes(s, 30);
            s.Events.Publish(new WorkflowEvent("E-LAB-DELAY", "test"));
            Assert.True(s.Patient.Clinical.Investigations.Single(x => x.Test == "lft").Delayed);
            Assert.False(s.Patient.Clinical.Investigations.Single(x => x.Test == "pregnancy").Delayed);
            Minutes(s, 16);
            Assert.Equal("PROCESSING", StateOf(s, "lft"));                           // 46 minutes: no longer ready at 45
            Assert.Equal("RESULT_AVAILABLE", StateOf(s, "pregnancy"));
            Minutes(s, 30);
            Assert.Equal("RESULT_AVAILABLE", StateOf(s, "lft"));                     // ready at 45 + 30 = 75 minutes
        }

        [Fact]
        public void The_same_seed_gives_the_same_imaging_time()
        {
            long Run(ulong seed)
            {
                var s = NewSession(seed: seed);
                Order(s, "chest_xray", ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true"));
                for (int i = 0; i < 12000 && StateOf(s, "chest_xray") != "RESULT_AVAILABLE"; i++) s.Tick(1);
                return s.InvestigationHistory.Single(h => h.Kind == "investigation:chest_xray:RESULT_AVAILABLE").Step;
            }
            Assert.Equal(Run(5), Run(5));
            Assert.True(Enumerable.Range(1, 6).Select(i => Run((ulong)i)).Distinct().Count() > 1);
        }

        // ---------------------------------------------------------------- refusals and visibility

        [Fact]
        public void Bad_requests_are_refused_with_a_reason()
        {
            var s = NewSession();
            Assert.Equal(InvestigationsSubsystem.MissingTest, Reject(Do(s, "order_investigation")));
            Assert.Equal(InvestigationsSubsystem.UnknownTest, Reject(Order(s, "mri_head")));
            Assert.Equal(InvestigationsSubsystem.NotOrdered, Reject(Do(s, "take_sample", ("test", "lft"))));
            Assert.Equal(InvestigationsSubsystem.NotOrdered, Reject(Do(s, "view_result", ("test", "lft"))));
            Order(s, "lft");
            Assert.Equal(InvestigationsSubsystem.OrderPending, Reject(Order(s, "lft")));
            Assert.Equal(InvestigationsSubsystem.UnknownTube, Reject(Do(s, "take_sample", ("test", "lft"), ("tube", "rainbow"))));
            Do(s, "take_sample", ("test", "lft"));
            Assert.Equal(InvestigationsSubsystem.AlreadyTaken, Reject(Do(s, "take_sample", ("test", "lft"))));
            Assert.Equal(InvestigationsSubsystem.NotReady, Reject(Do(s, "view_result", ("test", "lft"))));
            Assert.Equal(InvestigationsSubsystem.NotReady, Reject(Do(s, "acknowledge_result", ("test", "lft"))));
            Minutes(s, 46);
            Assert.Equal(InvestigationsSubsystem.ViewFirst, Reject(Do(s, "acknowledge_result", ("test", "lft"))));
        }

        [Fact]
        public void Nothing_can_be_ordered_before_identity_is_confirmed_and_the_sample_needs_the_patient_present()
        {
            var fresh = NewSession(confirmed: false);
            Assert.Equal("IDENTITY_NOT_CONFIRMED", Reject(Order(fresh, "lft")));
            var s = NewSession();
            Order(s, "lft");
            Do(s, "request_transport", ("to", "ultrasound"), ("mode", "trolley"), ("escort", "porter,nurse"));
            for (int i = 0; i < 3000 && s.Patient.Clinical.Location != "ultrasound"; i++) s.Tick(1);
            Assert.Equal("PATIENT_ELSEWHERE", Reject(Do(s, "take_sample", ("test", "lft"))));
            Assert.True(Order(s, "pregnancy").Accepted);                              // ordering from the notes is fine
        }

        [Fact]
        public void The_tablet_shows_the_state_when_it_is_expected_and_the_results_only_once_viewed()
        {
            var s = NewSession();
            Order(s, "lft");
            var requested = s.Observable.Investigations.Single();
            Assert.Equal("REQUESTED", requested.State); Assert.Equal("", requested.Ready); Assert.Equal("Liver function tests", requested.Label);
            Do(s, "take_sample", ("test", "lft"));
            Assert.Matches("^[0-9]{2}:[0-9]{2}$", s.Observable.Investigations.Single().Ready);
            Minutes(s, 46);
            Assert.Empty(s.Observable.Investigations.Single().Results);
            Do(s, "view_result", ("test", "lft"));
            Assert.Equal(5, s.Observable.Investigations.Single().Results.Count);
            Assert.Contains("\"investigations\":[{\"test\":\"lft\"", s.Observable.ToJson());
        }

        // ---------------------------------------------------------------- broken content is refused

        private static string Edit(string file, Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", file)))!;
            change(node);
            return node.ToJsonString();
        }

        private static string Tests() => File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", "investigations.json"));
        private static string Indications() => File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", "indications.json"));

        [Fact]
        public void Broken_investigation_content_is_refused_with_its_place()
        {
            Assert.Contains("needs one of the tubes", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["tests"]![0]!["tube"] = "rainbow"), Indications())).Message);
            Assert.Contains("only a blood test", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["tests"]![12]!["tube"] = "serum_gold"), Indications())).Message);
            Assert.Contains("needs the place", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["tests"]![18]!.AsObject().Remove("location")), Indications())).Message);
            Assert.Contains("belongs to more than one test", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["tests"]![1]!["analytes"]!.AsArray().Add("WBC")), Indications())).Message);
            Assert.Contains("duplicate test", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["tests"]![1]!["id"] = "fbc"), Indications())).Message);
            Assert.Contains("is not a test", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Tests(), Edit("indications.json", n => n["indications"]![0]!["test"] = "mri_head"))).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["colour"] = 1), Indications())).Message);
        }
    }
}
