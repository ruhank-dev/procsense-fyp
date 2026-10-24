using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The result generator and sample quality (p0-clinical-logic-s07, plan tests T-IV1, T-IV3 and T-IV6).</summary>
    public class ResultGeneratorTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly ResultGenerator Gen = new ResultGenerator(Content.Investigations);
        private static readonly IReadOnlyDictionary<string, string> NoSample = new Dictionary<string, string>();

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static InvestigationDef Def(string test) => Content.Investigations.Find(test)!;

        private static IReadOnlyDictionary<string, string> Sample(params (string k, string v)[] p) => p.ToDictionary(x => x.k, x => x.v);

        private static GeneratedResults Make(string caseId, string test, int validBefore = 0, params (string k, string v)[] sample) =>
            Gen.Generate(File_(caseId), Def(test), validBefore, Sample(sample), File_(caseId).Scenario);

        private static ResultRecord Rec(GeneratedResults g, string analyte) => g.Records.Single(r => r.Analyte == analyte);

        // ---------------------------------------------------------------- T-IV1 only what the case states

        [Fact]
        public void No_case_ever_returns_a_result_it_does_not_state_apart_from_the_supplied_normal_liver_panel()
        {
            int cases = 0, generated = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                var f = File_(Path.GetFileNameWithoutExtension(path));
                cases++;
                var stated = new HashSet<string>(f.Results.Select(r => r.Id));
                foreach (var def in Content.Investigations.Tests)
                    foreach (int before in new[] { 0, 1 })
                    {
                        var g = Gen.Generate(f, def, before, NoSample, f.Scenario);
                        Assert.Equal("ok", g.Outcome);
                        foreach (var r in g.Records)
                        {
                            if (r.Origin == "case") Assert.Contains(r.Id, stated);
                            else
                            {
                                generated++;
                                Assert.Equal("generated", r.Origin);
                                Assert.Equal("lft", def.Id);
                                Assert.Equal("S1", f.Scenario);
                                Assert.StartsWith("gen.lft.", r.Id);
                                Assert.Equal("normal", r.Flag);
                            }
                        }
                    }
            }
            Assert.Equal(TestCase.AllCases, cases);
            Assert.True(generated > 0);
        }

        [Fact]
        public void A_planned_case_that_states_no_liver_tests_gets_a_normal_panel_inside_the_normal_ranges_labelled_generated()
        {
            var g = Make("VAR-S1-B", "lft");                                  // a planned case that states no liver tests
            Assert.Equal(new[] { "Bilirubin", "ALT", "ALP", "GGT", "Albumin" }, g.Records.Select(r => r.Analyte));
            Assert.All(g.Records, r =>
            {
                Assert.Equal("generated", r.Origin);
                Assert.Equal("normal", ResultGenerator.FlagFor(double.Parse(r.Value, System.Globalization.CultureInfo.InvariantCulture), r.Range, "?"));
                Assert.Contains("the case states no result", r.Comment);
            });
            Assert.Equal("below 60", Rec(g, "GGT").Range);                       // a man's range
            var woman = Gen.Generate(File_("VAR-S1-A"), Def("lft"), 0, NoSample, "S1");
            Assert.Equal("case", woman.Records[0].Origin);                       // the case that states them keeps its own
        }

        [Fact]
        public void A_woman_gets_her_own_GGT_range()
        {
            var f = File_("VAR-S1-I");                                          // Mrs Amna Hashmi: states no liver tests
            Assert.Equal("F", f.Identity.Sex);
            var g = Gen.Generate(f, Def("lft"), 0, NoSample, "S1");
            Assert.Equal("below 40", Rec(g, "GGT").Range);
        }

        [Fact]
        public void Emergency_blood_results_are_never_invented()
        {
            var g = Make("VAR-S2-E", "u_and_e");                               // the case states no urea and electrolytes
            Assert.Empty(g.Records);
            Assert.Equal("The case states no result for this test.", g.Note);
            Assert.Empty(Make("VAR-S2-E", "lft").Records);                      // the liver panel is only supplied for planned cases
            Assert.Empty(Make("VAR-S1-B", "crp").Records);                       // and only the liver panel
        }

        [Fact]
        public void The_results_a_case_states_come_back_as_written()
        {
            var g = Make("VAR-S1-J", "u_and_e");
            var k = Rec(g, "Potassium");
            Assert.Equal(("6.4", "mmol/L", "3.5-5.3", "high"), (k.Value, k.Unit, k.Range, k.Flag));
            Assert.Equal("case", k.Origin);
            Assert.Equal("84", Rec(g, "Creatinine").Value);
        }

        // ---------------------------------------------------------------- T-IV6 the haemolysed sample and the repeat

        [Fact]
        public void VAR_S1_J_first_potassium_6_4_is_haemolysed_and_the_repeat_is_4_6()
        {
            var first = Rec(Make("VAR-S1-J", "u_and_e"), "Potassium");
            Assert.Equal("6.4", first.Value);
            Assert.Equal("haemolysed", first.Quality);
            Assert.Equal("high", first.Flag);
            Assert.Contains("falsely high", first.Comment);
            var repeat = Rec(Make("VAR-S1-J", "u_and_e", validBefore: 1), "Potassium (repeat)");
            Assert.Equal("4.6", repeat.Value);
            Assert.Equal("", repeat.Quality);
            Assert.Equal("normal", repeat.Flag);
        }

        [Fact]
        public void In_a_session_the_trainee_reads_the_haemolysed_result_then_repeats_it()
        {
            var s = Session("VAR-S1-J");
            Order(s, "u_and_e"); Do(s, "take_sample", ("test", "u_and_e"), ("tube", "serum_gold"));
            s.Tick(46 * 60);
            Do(s, "view_result", ("test", "u_and_e"));
            var k = s.Observable.Investigations.Single().Results.Single(r => r.Analyte == "Potassium");
            Assert.Equal(("6.4", "haemolysed"), (k.Value, k.Quality));
            Assert.Contains(s.InvestigationHistory, h => h.Kind == "sample_artefact:haemolysed");

            Order(s, "u_and_e"); Do(s, "take_sample", ("test", "u_and_e"));
            s.Tick(91 * 60);
            Do(s, "view_result", ("test", "u_and_e"));
            Assert.Equal("4.6", s.Observable.Investigations.Last().Results.Single().Value);
        }

        // ---------------------------------------------------------------- T-IV3 labelling

        [Fact]
        public void An_unlabelled_sample_is_rejected_by_the_lab_with_no_results()
        {
            var g = Make("VAR-S1-A", "lft", 0, ("label", "unlabelled"));
            Assert.Equal("rejected", g.Outcome);
            Assert.False(g.Valid);
            Assert.Empty(g.Records);
            Assert.Contains("not labelled", g.Note);
        }

        [Fact]
        public void A_rejected_sample_in_a_session_sets_the_incident_the_event_and_allows_a_re_take_that_gives_the_first_results()
        {
            var s = Session("VAR-S1-J");
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Order(s, "u_and_e"); Do(s, "take_sample", ("test", "u_and_e"), ("label", "unlabelled"));
            s.Tick(46 * 60);
            Assert.Equal("REJECTED", s.Patient.Clinical.Investigations.Single().State);
            Assert.Contains(events, e => e.Name == "E-SAMPLE-REJECTED");
            Assert.True(s.Patient.Clinical.HasFlag("incident.sample_rejected"));
            Assert.Contains(s.InvestigationHistory, h => h.Kind == "investigation:u_and_e:REJECTED" && h.ScoreItems.Contains("incident.sample_rejected"));
            Assert.Contains("not labelled", s.Observable.Investigations.Single().Note);

            Assert.True(Order(s, "u_and_e").Accepted);                                    // a rejected test can be asked for again
            var retake = s.Patient.Clinical.Investigations.Last();
            Assert.Equal(2, retake.Instance);
            Do(s, "take_sample", ("test", "u_and_e"));
            Assert.Contains("lab.k1", retake.ResultIds);                                  // the first set, because the rejected sample gave nothing
            Assert.DoesNotContain("lab.k2", retake.ResultIds);
        }

        [Fact]
        public void A_pre_labelled_tube_gives_another_patients_result_and_a_wrong_blood_incident()
        {
            var g = Make("VAR-S1-A", "lft", 0, ("label", "prelabelled"));
            Assert.Equal("mislabelled", g.Outcome);
            Assert.All(g.Records, r => { Assert.Equal("", r.Value); Assert.Equal("mislabelled", r.Quality); });

            var s = Session("VAR-S1-A");
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Order(s, "lft"); Do(s, "take_sample", ("test", "lft"), ("label", "prelabelled"));
            s.Tick(46 * 60);
            Assert.Equal("RESULT_AVAILABLE", s.Patient.Clinical.Investigations.Single().State);
            Assert.True(s.Patient.Clinical.HasFlag("incident.wrong_blood"));
            Assert.Contains(events, e => e.Name == "E-SAMPLE-MISLABELLED");
            Assert.Contains(s.InvestigationHistory, h => h.Kind == "sample_mislabelled" && h.ScoreItems.Contains("incident.wrong_blood"));
            Order(s, "lft"); Do(s, "take_sample", ("test", "lft"), ("label", "bedside"));
            Assert.Equal(s.Patient.Clinical.Investigations[1].ResultIds, new[] { "lab.bilirubin", "lab.alt", "lab.alp", "lab.ggt", "lab.albumin" });   // first set again
        }

        [Fact]
        public void A_sample_labelled_at_the_bedside_or_with_no_label_stated_is_valid()
        {
            Assert.Equal("ok", Make("VAR-S1-A", "lft", 0, ("label", "bedside")).Outcome);
            Assert.Equal("ok", Make("VAR-S1-A", "lft").Outcome);
            var s = Session("VAR-S1-A");
            Order(s, "lft");
            Assert.Equal(InvestigationsSubsystem.UnknownLabel, Reject(Do(s, "take_sample", ("test", "lft"), ("label", "scribble"))));
        }

        // ---------------------------------------------------------------- how the sample was taken

        [Fact]
        public void A_drip_arm_sample_is_diluted_and_says_so()
        {
            var g = Make("VAR-S1-J", "u_and_e", 0, ("arm", "drip_arm"));
            var creat = Rec(g, "Creatinine");
            Assert.Equal("50", creat.Value);                                   // 84 x 0.6
            Assert.Equal("low", creat.Flag);                                   // against 60-110
            Assert.Equal("drip_arm", creat.Quality);
            Assert.Equal("artefact", creat.Origin);
            Assert.Contains("Repeat from the other arm", creat.Comment);
            Assert.Equal("3.8", Rec(g, "Potassium").Value);                     // 6.4 x 0.6 = 3.84, kept to one decimal
        }

        [Fact]
        public void A_gold_tube_test_in_an_EDTA_tube_has_a_falsely_high_potassium_and_nothing_else_changes()
        {
            var g = Make("VAR-S1-J", "u_and_e", 0, ("tube", "edta_purple"));
            var k = Rec(g, "Potassium");
            Assert.Equal("8.9", k.Value);                                      // 6.4 + 2.5
            Assert.Equal(("high", "wrong_tube_edta"), (k.Flag, k.Quality));
            Assert.Equal("84", Rec(g, "Creatinine").Value);
            var repeat = Rec(Make("VAR-S1-J", "u_and_e", 1, ("tube", "edta_purple")), "Potassium (repeat)");
            Assert.Equal("7.1", repeat.Value);                                 // 4.6 + 2.5: a normal potassium made to look high
            Assert.Equal("high", repeat.Flag);
        }

        [Fact]
        public void A_tube_that_is_wrong_for_any_other_reason_is_rejected_and_the_right_tube_is_fine()
        {
            Assert.Equal("rejected", Make("VAR-S1-A", "lft", 0, ("tube", "edta_purple")).Outcome);          // no potassium to go false, so the lab simply cannot use it
            Assert.Equal("rejected", Make("VAR-S1-J", "u_and_e", 0, ("tube", "citrate_blue")).Outcome);
            Assert.Contains("serum gold", Make("VAR-S1-A", "lft", 0, ("tube", "citrate_blue")).Note);
            Assert.Equal("ok", Make("VAR-S1-A", "lft", 0, ("tube", "serum_gold")).Outcome);
        }

        [Fact]
        public void Bedside_tests_have_no_tube_or_label_artefacts()
        {
            var g = Make("VAR-S1-A", "pregnancy", 0, ("label", "unlabelled"), ("arm", "drip_arm"));
            Assert.Equal("ok", g.Outcome);
            Assert.Equal("case", g.Records.Single().Origin);
        }

        [Fact]
        public void The_same_inputs_always_give_the_same_results()
        {
            var a = Make("VAR-S1-J", "u_and_e", 0, ("arm", "drip_arm"), ("tube", "edta_purple"));
            var b = Make("VAR-S1-J", "u_and_e", 0, ("arm", "drip_arm"), ("tube", "edta_purple"));
            Assert.Equal(a.Records, b.Records);
            Assert.Equal(a.Outcome, b.Outcome);
        }

        // ---------------------------------------------------------------- the flag

        [Theory]
        [InlineData(3.4, "3.5-5.3", "x", "low")]
        [InlineData(3.5, "3.5-5.3", "x", "normal")]
        [InlineData(5.3, "3.5-5.3", "x", "normal")]
        [InlineData(5.4, "3.5-5.3", "x", "high")]
        [InlineData(1.9, "below 2.0", "x", "normal")]
        [InlineData(2.0, "below 2.0", "x", "high")]
        [InlineData(10.0, "below 42 (below 69 is acceptable for planned surgery)", "x", "normal")]
        [InlineData(50.0, "negative", "abnormal", "abnormal")]                  // other wording keeps the case's flag
        public void The_flag_is_worked_out_from_the_range_the_case_gives(double value, string range, string original, string expected)
        {
            Assert.Equal(expected, ResultGenerator.FlagFor(value, range, original));
        }

        // ---------------------------------------------------------------- in a session and the content

        [Fact]
        public void Results_are_only_readable_after_they_are_viewed_and_the_tablet_shows_every_field()
        {
            var s = Session("VAR-S1-J");
            Order(s, "u_and_e"); Do(s, "take_sample", ("test", "u_and_e"));
            s.Tick(46 * 60);
            Assert.Empty(s.Observable.Investigations.Single().Results);
            Do(s, "view_result", ("test", "u_and_e"));
            var json = s.Observable.ToJson();
            Assert.Contains("\"analyte\":\"Potassium\",\"value\":\"6.4\",\"unit\":\"mmol/L\",\"range\":\"3.5-5.3\",\"flag\":\"high\"", json);
            Assert.Contains("\"quality\":\"haemolysed\",\"origin\":\"case\"", json);
        }

        [Fact]
        public void An_ordered_test_the_case_states_nothing_for_returns_no_results_and_says_so()
        {
            var s = Session("VAR-S2-E", "S2");
            Order(s, "u_and_e"); Do(s, "take_sample", ("test", "u_and_e"));
            s.Tick(50 * 60);
            Do(s, "view_result", ("test", "u_and_e"));
            var seen = s.Observable.Investigations.Single();
            Assert.Empty(seen.Results);
            Assert.Equal("The case states no result for this test.", seen.Note);
        }

        private static string Edit(string file, Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", file)))!;
            change(node);
            return node.ToJsonString();
        }

        private static string Tests() => File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", "investigations.json"));
        private static string Indications() => File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", "indications.json"));
        private static string Normals() => File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", "normal_results.json"));

        [Fact]
        public void The_artefact_numbers_labels_and_normal_results_come_from_content_and_broken_content_is_refused()
        {
            Assert.Equal(0.6, Content.Investigations.DripDilution);
            Assert.Equal(2.5, Content.Investigations.EdtaPotassiumRise);
            Assert.Equal(new[] { "lft" }, Content.Investigations.Normals.Keys);
            Assert.Equal(10, Content.Investigations.Normals["lft"].Results.Count);
            Assert.Contains("between 0 and 1", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["artefacts"]!["drip_dilution_factor"] = 1.5), Indications(), Normals())).Message);
            Assert.Contains("more than 0", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["artefacts"]!["edta_potassium_rise"] = 0), Indications(), Normals())).Message);
            Assert.Contains("is missing", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Edit("investigations.json", n => n["labels"]!.AsArray().RemoveAt(2)), Indications(), Normals())).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => InvestigationTables.Parse(Tests(), Indications(), Edit("normal_results.json", n => n["colour"] = 1))).Message);
        }

        // ---------------------------------------------------------------- helpers

        private static Session Session(string caseFile, string scenario = "S1")
        {
            var chain = Subsystems.Default(Content).Where(x => !(x is InvestigationsSubsystem)).Concat(new ISubsystem[] { new InvestigationsSubsystem(Content, File_(caseFile)) });
            var card = scenario == "S1" ? Content.Cases["VAR-S1-A"] : Content.Cases["VAR-S1-A"] with { Scenario = scenario };
            var s = new Session(card, 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static ActionOutcome Order(Session s, string test) => Do(s, "order_investigation", ("test", test));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }
    }
}
