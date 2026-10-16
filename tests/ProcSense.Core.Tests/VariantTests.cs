using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>A session plays one path of a case: the base case or one variant (DT-193), and a golden file can name the variant and the level it plays (DT-093).</summary>
    public class VariantTests
    {
        private static readonly ContentPackage All = ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), "VAR-S1-B", "VAR-S1-C", "VAR-S1-D");

        private static Session Start(string caseId, string variant) =>
            new Session(All.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(All), new SessionOptions(TrainingLevel.Beginner, variant));

        [Fact]
        public void The_base_case_sees_nothing_of_a_variant()
        {
            var c = All.CaseFiles["VAR-S1-C"].ForVariant("");
            Assert.DoesNotContain(c.Results, r => r.Variant == "C2");
            Assert.Single(c.Results, r => r.Test == "HbA1c");
            Assert.Equal("58", c.Results.Single(r => r.Test == "HbA1c").Value);
            Assert.DoesNotContain(c.Decisions, d => d.Variant == "C2");
            Assert.DoesNotContain(All.CaseFiles["VAR-S1-B"].ForVariant("").Observations, o => o.Condition == "variant");
            Assert.DoesNotContain(All.CaseFiles["VAR-S1-D"].ForVariant("").History, h => h.Variant != null);
        }

        [Fact]
        public void A_variant_result_replaces_the_base_result_of_the_same_test_and_the_other_results_stay()
        {
            var c = All.CaseFiles["VAR-S1-C"].ForVariant("C2");
            Assert.Equal("82", c.Results.Single(r => r.Test == "HbA1c").Value);
            Assert.Equal("13.4", c.Results.Single(r => r.Test == "Finger-prick glucose").Value);
            Assert.Contains(c.Results, r => r.Test == "Ketones");
            Assert.Contains(c.Decisions, d => d.Variant == "C2");
        }

        [Fact]
        public void A_variant_with_its_own_readings_starts_with_them_and_the_base_case_does_not()
        {
            Assert.Equal((172, 104), (Start("VAR-S1-B", "").Patient.Physiological.Vitals.SystolicBp, Start("VAR-S1-B", "").Patient.Physiological.Vitals.DiastolicBp));
            var b2 = Start("VAR-S1-B", "B2").Patient.Physiological.Vitals;
            Assert.Equal((188, 114, 82), (b2.SystolicBp, b2.DiastolicBp, b2.HeartRate));
        }

        [Fact]
        public void A_variant_without_readings_of_its_own_keeps_the_base_vitals()
        {
            Assert.Equal(Start("VAR-S1-C", "").Patient.Physiological.Vitals, Start("VAR-S1-C", "C2").Patient.Physiological.Vitals);
        }

        [Fact]
        public void Measuring_the_pressure_of_the_B2_patient_registers_the_B2_problem_and_not_the_base_one()
        {
            var s = Start("VAR-S1-B", "B2");
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(new SimAction(v, "patient"));
            s.Apply(new SimAction("attach_sensor", "patient", Channel.Cli, new System.Collections.Generic.Dictionary<string, string> { ["sensor"] = "nibp_cuff" }));
            s.Apply(new SimAction("measure_nibp", "patient"));
            s.Tick(60);                                                              // the cuff takes its cycle before the reading is shown
            Assert.Contains(s.Problems!.Open, p => p.Id == "b.bp2");
            Assert.DoesNotContain(s.Problems.Open, p => p.Id == "b.bp");
        }

        [Fact]
        public void A_golden_file_names_the_variant_and_level_it_plays_and_a_file_without_them_plays_the_base_case()
        {
            var o = GoldenRun.Options("{\"case\":\"VAR-S1-B\",\"seed\":\"42\",\"mode\":\"practice\",\"variant\":\"B2\",\"level\":\"advanced\"}");
            Assert.Equal(("B2", TrainingLevel.Advanced), (o.Variant, o.Level));
            var none = GoldenRun.Options("{\"case\":\"VAR-S1-B\",\"seed\":\"42\",\"mode\":\"practice\"}");
            Assert.Equal(("", TrainingLevel.Beginner), (none.Variant, none.Level));
        }

        [Fact]
        public void A_wrong_variant_in_a_golden_file_shows_in_the_diff()
        {
            string dir = System.IO.Path.Combine(TestCase.Repo, "tests", "golden");
            string script = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "VAR-S1-B_variant-B2.actions.json"));
            string expected = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "VAR-S1-B_variant-B2.expected.json"));
            Assert.Empty(GoldenRun.Check(script, expected, All));
            Assert.NotEmpty(GoldenRun.Check(script, expected.Replace("\"variant\": \"B2\"", "\"variant\": \"B9\""), All));
        }
    }
}
