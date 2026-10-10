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
    /// <summary>Imaging report categories and the 'not visualised is not negative' rule (p0-clinical-logic-s09, plan test T-IV7).</summary>
    public class ImagingTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly ImagingTables Imaging = Content.Imaging;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_every_finding_with_a_category_and_an_approved_report_for_each()
        {
            Assert.Equal(new[] { "normal", "positive", "complicated", "other_diagnosis", "non_diagnostic" }, Imaging.Categories);
            Assert.Equal(51, Imaging.Findings.Count);
            Assert.Equal(204, Imaging.Reports.Count);
            Assert.All(Imaging.Findings, f => Assert.Contains(Imaging.Reports, r => r.Code == f.Code));
            Assert.All(Imaging.Categories, c => Assert.Contains(Imaging.Findings, f => f.Category == c));
            Assert.Equal(35, Imaging.CaseStudies.Count);
            Assert.Equal(new[] { "chest_xray", "ecg" }, Imaging.Clean.Keys);
        }

        [Fact]
        public void Every_picture_with_a_report_is_approved_for_training()
        {
            var approvals = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "image_approvals.json")))!;
            string text = approvals.ToJsonString();
            Assert.All(Imaging.Reports, r => Assert.Contains(r.ImageId, text));
        }

        [Theory]
        [InlineData("US-GB-01", "positive")]
        [InlineData("US-GB-02", "positive")]
        [InlineData("US-GB-03", "other_diagnosis")]
        [InlineData("US-GB-04", "positive")]
        [InlineData("US-GB-05", "complicated")]
        [InlineData("US-GB-06", "other_diagnosis")]
        [InlineData("US-GB-07", "non_diagnostic")]        // the seven gallbladder ultrasound report types (6.3)
        [InlineData("US-AP-01", "positive")]
        [InlineData("US-AP-02", "complicated")]
        [InlineData("US-AP-03", "other_diagnosis")]
        [InlineData("US-AP-04", "non_diagnostic")]
        [InlineData("US-AP-05", "normal")]   // the appendix categories (6.4)
        [InlineData("CT-AP-00", "normal")]
        [InlineData("CT-AP-02", "complicated")]
        [InlineData("MRCP-01", "positive")]
        [InlineData("CXR-01", "normal")]
        [InlineData("CXR-08", "complicated")]
        [InlineData("ECG-01", "normal")]
        [InlineData("ECG-07", "complicated")]
        public void Each_finding_belongs_to_the_category_the_walkthrough_wording_gives_it(string code, string category)
        {
            Assert.Equal(category, Imaging.FindingOf(code)!.Category);
        }

        [Fact]
        public void The_equivocal_findings_are_all_non_diagnostic_and_none_of_them_is_called_negative()
        {
            Assert.Equal(new[] { "US-AP-04", "US-GB-07", "US-GB-08" }, Imaging.Findings.Where(f => f.Category == "non_diagnostic").Select(f => f.Code).OrderBy(c => c));
            Assert.Contains("NOT the same as negative", Imaging.FindingOf("US-AP-04")!.Plain);
        }

        // ---------------------------------------------------------------- which report a case shows

        [Fact]
        public void The_clinic_ultrasound_of_VAR_S1_A_is_simple_gallstones()
        {
            var r = Imaging.Resolve("VAR-S1-A", "ultrasound")!;
            Assert.Equal(("ultrasound_gallbladder", "US-GB-01", "positive", "case"), (r.Study, r.Code, r.Category, r.Origin));
            Assert.Equal("Simple gallstones", r.Plain);
            Assert.StartsWith("rep-us-gb-01", r.ReportId);
            Assert.Contains("stone", r.Text, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual("", r.ImageId);
        }

        [Theory]
        [InlineData("SC-14", "ultrasound", "ultrasound_appendix", "US-AP-04")]
        [InlineData("SC-14", "ct", "ct_appendix_level", "CT-AP-02")]
        [InlineData("SC-3", "mri", "mri_appendix_level", "MR-AP-01")]
        [InlineData("VAR-S1-PB", "mrcp", "mrcp", "MRCP-01")]
        [InlineData("VAR-S1-PB", "ultrasound", "ultrasound_gallbladder", "US-GB-03")]
        [InlineData("SC-2", "ultrasound", "ultrasound_pelvis", "US-PV-01")]
        [InlineData("SC-7", "chest_xray", "chest_xray", "CXR-02")]
        [InlineData("VAR-S1-L", "ecg", "ecg", "ECG-10")]
        public void Each_imaging_test_shows_the_finding_the_case_states_for_its_study(string caseId, string test, string study, string code)
        {
            var r = Imaging.Resolve(caseId, test)!;
            Assert.Equal((study, code, "case"), (r.Study, r.Code, r.Origin));
        }

        [Fact]
        public void A_film_or_strip_with_no_abnormality_is_shown_only_for_a_chest_X_ray_or_ECG_the_case_states_nothing_for()
        {
            var xray = Imaging.Resolve("VAR-S1-A", "chest_xray")!;
            Assert.Equal(("CXR-01", "normal", "clean"), (xray.Code, xray.Category, xray.Origin));
            var ecg = Imaging.Resolve("VAR-S1-A", "ecg")!;
            Assert.Equal(("ECG-01", "clean"), (ecg.Code, ecg.Origin));
            Assert.Null(Imaging.Resolve("VAR-S1-A", "ct"));                     // nothing is invented for a scan the case does not state
            Assert.Null(Imaging.Resolve("VAR-S1-A", "mri"));
            Assert.Null(Imaging.Resolve("SC-7", "ultrasound"));
            Assert.Null(Imaging.Resolve("VAR-S1-A", "lft"));                     // not an imaging test
            Assert.False(Imaging.IsImagingTest("lft"));
        }

        [Fact]
        public void The_same_case_always_shows_the_same_report_and_different_cases_can_show_different_variants()
        {
            Assert.Equal(Imaging.Resolve("VAR-S1-B", "ultrasound")!.ReportId, Imaging.Resolve("VAR-S1-B", "ultrasound")!.ReportId);
            var variants = Imaging.CaseStudies.Where(kv => kv.Value.Values.Contains("US-GB-01")).Select(kv => Imaging.Resolve(kv.Key, "ultrasound")!.ReportId).Distinct().Count();
            Assert.True(variants > 1);
        }

        [Fact]
        public void Every_study_every_case_states_resolves_to_a_report_in_the_right_category()
        {
            int n = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                foreach (var test in Imaging.Tests.Keys)
                {
                    var r = Imaging.Resolve(id, test);
                    if (r == null) continue;
                    n++;
                    Assert.Equal(Imaging.FindingOf(r.Code)!.Category, r.Category);
                    Assert.Contains(r.Study, Imaging.Tests[test]);
                }
            }
            Assert.True(n > 60);
        }

        // ---------------------------------------------------------------- in a session

        private static Session NewSession(string caseFile, string scenario = "S2")
        {
            var file = File_(caseFile);
            var chain = Subsystems.Default(Content).Where(x => !(x is InvestigationsSubsystem) && !(x is ResultReviewSubsystem))
                .Concat(new ISubsystem[] { new InvestigationsSubsystem(Content, file), new ResultReviewSubsystem(Content, file) });
            var card = scenario == "S1" ? Content.Cases["VAR-S1-A"] : Content.Cases["VAR-S1-A"] with { Scenario = scenario };
            var s = new Session(card, 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static void Scan(Session s, string test)
        {
            Assert.True(Do(s, "order_investigation", ("test", test), ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true")).Accepted);
            for (int i = 0; i < 20000 && s.Patient.Clinical.Investigations.Last().State != "RESULT_AVAILABLE"; i++) s.Tick(1);
            Assert.Equal("RESULT_AVAILABLE", s.Patient.Clinical.Investigations.Last().State);
            Assert.True(Do(s, "view_result", ("test", test)).Accepted);
        }

        [Fact]
        public void Viewing_the_scan_shows_the_report_text_and_the_approved_picture_but_never_the_engines_code_or_category()
        {
            var s = NewSession("SC-14");
            Scan(s, "ultrasound");
            var rep = s.Observable.Investigations.Last().Report!;
            Assert.StartsWith("rep-", Imaging.Resolve("SC-14", "ultrasound")!.ReportId);
            Assert.Equal(Imaging.Resolve("SC-14", "ultrasound")!.Text, rep.Text);
            Assert.Equal(Imaging.Resolve("SC-14", "ultrasound")!.ImageId, rep.ImageId);
            string json = s.Observable.ToJson();
            Assert.DoesNotContain("US-AP-04", json);
            Assert.DoesNotContain("non_diagnostic", json);
            Assert.Contains("\"report\":{\"text\":", json);
        }

        [Fact]
        public void An_ECG_the_case_states_shows_its_report_after_the_bedside_test()
        {
            var s = NewSession("VAR-S1-L", "S1");
            Do(s, "order_investigation", ("test", "ecg")); Do(s, "perform_bedside_test", ("test", "ecg"));
            s.Tick(25 * 60);
            Do(s, "view_result", ("test", "ecg"));
            Assert.Equal("ECG-10", s.Patient.Clinical.Investigations.Single().Report!.Code);
            Assert.Contains("atrial", s.Observable.Investigations.Single().Report!.Text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_chest_X_ray_the_case_states_nothing_for_shows_a_film_with_no_abnormality()
        {
            var s = NewSession("VAR-S1-A", "S1");
            Scan(s, "chest_xray");
            var order = s.Patient.Clinical.Investigations.Single();
            Assert.Equal(("CXR-01", "clean"), (order.Report!.Code, order.Report.Origin));
            Assert.Empty(order.Results);
        }

        // ---------------------------------------------------------------- T-IV7

        [Fact]
        public void Calling_the_appendix_not_visualised_normal_is_the_classic_error_and_a_major_item()
        {
            var s = NewSession("SC-14");
            Scan(s, "ultrasound");
            var o = Do(s, "interpret_imaging", ("test", "ultrasound"), ("category", "normal"));
            Assert.Contains("imaging_interpretation:wrong", o.Effects);
            Assert.Contains("equivocal_taken_as_negative", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("imaging.equivocal_taken_as_negative"));
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "not_visualised_taken_as_negative" && h.ScoreItems.Contains("major.not_visualised_taken_as_negative"));
        }

        [Fact]
        public void Calling_it_non_diagnostic_is_correct_and_credited()
        {
            var s = NewSession("SC-14");
            Scan(s, "ultrasound");
            var o = Do(s, "interpret_imaging", ("test", "ultrasound"), ("category", "non_diagnostic"));
            Assert.Contains("imaging_interpretation:correct", o.Effects);
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "imaging_interpretation_correct" && h.ScoreItems.Contains("credit.imaging_interpretation"));
            Assert.False(s.Patient.Clinical.HasFlag("imaging.equivocal_taken_as_negative"));
        }

        [Fact]
        public void Any_other_wrong_category_is_a_major_item_without_the_special_flag()
        {
            var s = NewSession("SC-14");
            Scan(s, "ultrasound");
            var o = Do(s, "interpret_imaging", ("test", "ultrasound"), ("category", "positive"));
            Assert.Contains("imaging_interpretation:wrong", o.Effects);
            Assert.DoesNotContain("equivocal_taken_as_negative", o.Effects);
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "imaging_interpretation_wrong" && h.ScoreItems.Contains("major.interpretation_wrong"));
            Assert.Equal("positive", s.Patient.Clinical.Investigations.Single().Interpretation);
        }

        [Fact]
        public void The_CT_of_SC_14_is_complicated_appendicitis()
        {
            var s = NewSession("SC-14");
            Scan(s, "ct");
            Assert.Contains("imaging_interpretation:correct", Do(s, "interpret_imaging", ("test", "ct"), ("category", "complicated")).Effects);
        }

        [Fact]
        public void Interpreting_a_scan_needs_it_opened_a_report_and_one_of_the_five_categories()
        {
            var s = NewSession("SC-14");
            Assert.Equal(ResultReviewSubsystem.MissingTest, Reject(Do(s, "interpret_imaging", ("category", "normal"))));
            Assert.Equal(ResultReviewSubsystem.UnknownTest, Reject(Do(s, "interpret_imaging", ("test", "scan"), ("category", "normal"))));
            Assert.Equal(ResultReviewSubsystem.NotOrdered, Reject(Do(s, "interpret_imaging", ("test", "ct"), ("category", "normal"))));
            Do(s, "order_investigation", ("test", "ct"), ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true"));
            Assert.Equal(ResultReviewSubsystem.NotViewed, Reject(Do(s, "interpret_imaging", ("test", "ct"), ("category", "normal"))));
            for (int i = 0; i < 20000 && s.Patient.Clinical.Investigations.Last().State != "RESULT_AVAILABLE"; i++) s.Tick(1);
            Do(s, "view_result", ("test", "ct"));
            Assert.Equal(ResultReviewSubsystem.UnknownInterpretation, Reject(Do(s, "interpret_imaging", ("test", "ct"), ("category", "negative"))));
            Assert.Equal(ResultReviewSubsystem.UnknownInterpretation, Reject(Do(s, "interpret_imaging", ("test", "ct"))));
            Assert.Equal(ResultReviewSubsystem.UseImagingVerb, Reject(Do(s, "interpret_result", ("test", "ct"), ("interpretation", "abnormal"))));       // a scan is interpreted with the imaging verb
        }

        [Fact]
        public void A_blood_test_has_no_imaging_interpretation()
        {
            var s = NewSession("VAR-S1-A", "S1");
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft")); s.Tick(50 * 60); Do(s, "view_result", ("test", "lft"));
            Assert.Equal(ResultReviewSubsystem.NothingToInterpret, Reject(Do(s, "interpret_imaging", ("test", "lft"), ("category", "normal"))));
        }

        // ---------------------------------------------------------------- broken content is refused

        private static string Edit(Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "imaging", "imaging.json")))!;
            change(node);
            return node.ToJsonString();
        }

        [Fact]
        public void Broken_imaging_content_is_refused_with_its_place()
        {
            Assert.Contains("is not one of", Assert.Throws<ContentException>(() => ImagingTables.Parse(Edit(n => n["findings"]![0]!["category"] = "scary"))).Message);
            Assert.Contains("duplicate finding", Assert.Throws<ContentException>(() => ImagingTables.Parse(Edit(n => n["findings"]![1]!["code"] = n["findings"]![0]!["code"]!.GetValue<string>()))).Message);
            Assert.Contains("which is not a finding", Assert.Throws<ContentException>(() => ImagingTables.Parse(Edit(n => n["reports"]![0]!["code"] = "XX-00"))).Message);
            Assert.Contains("has no finding or no approved report", Assert.Throws<ContentException>(() => ImagingTables.Parse(Edit(n => n["case_studies"]!["VAR-S1-A"]!["ultrasound_gallbladder"] = "US-GB-99"))).Message);
            Assert.Contains("at least two categories", Assert.Throws<ContentException>(() => ImagingTables.Parse(Edit(n => n["categories"] = new System.Text.Json.Nodes.JsonArray("normal")))).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => ImagingTables.Parse(Edit(n => n["colour"] = 1))).Message);
        }
    }
}
