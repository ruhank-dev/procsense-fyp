using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Rules;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>A problem joins the registry when the trainee has seen the evidence for it (DT-073, content/problems/discovery.json).</summary>
    public class DiscoveryTests
    {
        private static readonly string[] Ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).ToArray()!;
        private static readonly ContentPackage All = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), Ids, Ids);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start(string caseId, string variant = "")
        {
            var s = new Session(All.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(All), new SessionOptions(TrainingLevel.Beginner, variant));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static bool Found(Session s, string problem) => s.Problems!.Ordered.Any(p => p.Id == problem);

        private static void OpenBlood(Session s, string test, string tube)
        {
            Do(s, "order_investigation", ("test", test));
            Do(s, "take_sample", ("test", test), ("tube", tube));
        }

        [Fact]
        public void The_rules_name_only_problems_of_their_case_and_cover_every_problem_of_every_case_but_the_two_framing_ones()
        {
            // 29 rules written one by one for the 17 P0 cases and, since DT-091, the rules derived from the case content for the other 36 cases (127 problems): every problem of every case has one
            int total = All.CaseFiles.Values.Sum(c => c.Problems.Count);
            Assert.Equal(total - 2, All.Problems.Discovery.Count);
            Assert.Equal(29 + 127, All.Problems.Discovery.Count);
            foreach (var d in All.Problems.Discovery)
            {
                Assert.Contains(All.CaseFiles[d.Case].Problems, p => p.Id == d.Problem);
                Assert.StartsWith("SR-", d.Ref);
                Assert.False(string.IsNullOrWhiteSpace(d.How));
            }
            Assert.Equal(127, All.Problems.Discovery.Count(d => d.How.EndsWith("[derived from the case content]")));
        }

        [Fact]
        public void Each_rule_is_false_on_an_empty_state_and_true_once_its_evidence_is_there()
        {
            var empty = new Dictionary<string, object?>
            {
                ["viewed_tests"] = new List<object?>(),
                ["topics"] = new List<object?>(),
                ["measured"] = new List<object?>(),
                ["findings"] = new List<object?>(),
                ["flags"] = new List<object?>(),
                ["sepsis_tier"] = "none",
                ["allergies_known"] = false,
            };
            foreach (var d in All.Problems.Discovery) Assert.False(d.Trigger.Test(empty), d.Case + ":" + d.Problem);
            var full = new Dictionary<string, object?>
            {
                ["viewed_tests"] = new List<object?> { "ultrasound", "pregnancy", "u_and_e", "lft", "fbc", "crp", "lactate_cultures", "clotting", "glucose_fingerprick", "ecg", "ct", "mri", "hba1c", "ferritin", "tryptase", "stopbang", "bmi", "urine_dipstick", "chest_xray", "blood_gas", "tsh" },
                ["topics"] = new List<object?> { "medicines", "last_food", "complaint" },
                ["measured"] = new List<object?> { "systolic_bp", "pain_score" },
                ["findings"] = new List<object?> { "ex.abdomen", "ex.airway", "ex.chest", "ex.pulse", "ex.skin", "ex.eyes", "ex.general" },
                ["flags"] = new List<object?> { "fasting.breach" },
                ["sepsis_tier"] = "high",
                ["allergies_known"] = true,
            };
            foreach (var d in All.Problems.Discovery) Assert.True(d.Trigger.Test(full), d.Case + ":" + d.Problem);
        }

        [Fact]
        public void A_positive_pregnancy_test_opened_registers_the_problem_and_not_before()
        {
            var s = Start("VAR-S1-H");
            Assert.False(Found(s, "h.preg"));
            Do(s, "ask", ("topic", "pregnancy"));
            Do(s, "order_investigation", ("test", "pregnancy"));
            var bed = Do(s, "perform_bedside_test", ("test", "pregnancy"));
            Assert.True(bed.Accepted, bed.RejectReason);
            s.Tick(3600);
            Assert.False(Found(s, "h.preg"));                            // done but not yet opened
            Do(s, "view_result", ("test", "pregnancy"));
            Assert.True(Found(s, "h.preg"));
            Assert.Equal("safety_blocker", s.Problems!.Ordered.Single(p => p.Id == "h.preg").Type);
        }

        [Fact]
        public void The_liver_tests_register_the_stone_and_the_watch_for_cholangitis_in_the_bile_duct_case()
        {
            var s = Start("VAR-S1-PB");
            OpenBlood(s, "lft", "serum_gold");
            s.Tick(3600);
            Do(s, "view_result", ("test", "lft"));
            Assert.True(Found(s, "pb.stone"));
            Assert.True(Found(s, "pb.cholangitis"));
            Assert.NotEqual("PROCEED", s.Decision!.Current.Outcome);
        }

        [Fact]
        public void Asking_about_the_last_meal_registers_the_full_stomach_in_the_emergency_case()
        {
            var s = Start("VAR-S2-F");
            Assert.False(Found(s, "sf.stomach"));
            Do(s, "ask", ("topic", "last_food"));
            Assert.True(Found(s, "sf.stomach"));
        }

        [Fact]
        public void The_fasting_rule_found_broken_registers_the_fasting_problem()
        {
            var s = Start("VAR-S1-G");
            Do(s, "ask", ("topic", "last_food")); Do(s, "ask", ("topic", "last_drink"));
            Assert.True(s.Patient.Clinical.HasFlag("fasting.breach"));
            Assert.True(Found(s, "g.fasting"));
        }

        [Fact]
        public void Asking_for_the_pain_score_registers_the_severe_pain_that_must_not_wait()
        {
            var s = Start("VAR-S2-PC");
            Assert.False(Found(s, "pc.pain"));
            Do(s, "ask_pain_score");
            Assert.True(Found(s, "pc.pain"));
            Assert.Equal("time_critical", s.Problems!.Ordered.Single(p => p.Id == "pc.pain").Type);
        }

        [Fact]
        public void The_blood_pressure_problem_depends_on_the_variant_being_played()
        {
            var baseCase = Start("VAR-S1-B");
            Do(baseCase, "attach_sensor", ("sensor", "nibp_cuff")); Do(baseCase, "measure_nibp"); baseCase.Tick(60);
            Assert.True(Found(baseCase, "b.bp"));
            Assert.False(Found(baseCase, "b.bp2"));                      // not the variant being played
            var b2 = Start("VAR-S1-B", "B2");
            Do(b2, "attach_sensor", ("sensor", "nibp_cuff")); Do(b2, "measure_nibp"); b2.Tick(60);
            Assert.True(Found(b2, "b.bp2"));
            Assert.False(Found(b2, "b.bp"));                             // "base" belongs to the case only when no variant is played
        }

        [Fact]
        public void Asking_about_medicines_registers_the_diabetes_and_the_blood_thinner_problems()
        {
            var c = Start("VAR-S1-C");
            Do(c, "ask", ("topic", "medicines"));
            Assert.True(Found(c, "c.diabetes"));
            var d = Start("VAR-S1-D");
            Do(d, "ask", ("topic", "medicines"));
            Assert.True(Found(d, "d.anticoag"));
        }

        [Fact]
        public void The_allergy_history_and_the_rigid_abdomen_register_their_problems_in_the_sepsis_case()
        {
            var s = Start("STK-S2-B");
            Assert.False(Found(s, "s2b.allergy"));
            Do(s, "ask_allergies");
            Assert.True(Found(s, "s2b.allergy"));
            Do(s, "ask_exam_permission");
            Do(s, "examine", ("region", "abdomen"));
            Assert.True(Found(s, "s2b.source"));
        }

        [Fact]
        public void A_life_threat_found_by_the_rule_switches_emergency_mode_on()
        {
            var s = Start("VAR-S2-B");
            Assert.False(s.Patient.Clinical.HasFlag("mode.emergency"));
            Do(s, "ask", ("topic", "pregnancy"));
            Do(s, "order_investigation", ("test", "pregnancy"));
            Do(s, "perform_bedside_test", ("test", "pregnancy"));
            s.Tick(3600);
            Do(s, "view_result", ("test", "pregnancy"));
            Assert.True(Found(s, "sb.bleed"));
            Assert.True(s.Patient.Clinical.HasFlag("mode.emergency"));
            Assert.Equal("EMERGENCY", s.Decision!.Current.Outcome);
        }

        [Fact]
        public void A_case_with_no_discovery_rules_costs_nothing_and_finds_nothing()
        {
            var s = Start("VAR-S1-A");
            Do(s, "ask", ("topic", "complaint"));
            s.Tick(300);
            Assert.Empty(s.Problems!.Ordered);
        }
    }
}
