using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Records;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The EMR sections (p0-patient-records-emr-s04, Core half): every P0 case shows every section from content; labs with previous values and trends; a report-only notice where no image exists.</summary>
    public class EmrSectionsTests
    {
        private static readonly string[] P0 = Directory.GetFiles(Path.Combine(TestCase.Repo, "tests", "golden"), "*_correct.actions.json")
            .Select(f => Path.GetFileName(f).Replace("_correct.actions.json", "")).Where(n => n.Contains('-') && char.IsUpper(n[0])).OrderBy(n => n).ToArray();

        private static readonly ContentPackage Content = ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), P0);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start(string caseId)
        {
            var s = new Session(Content.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity", "ask_allergies" }) Do(s, verb);
            return s;
        }

        [Fact]
        public void Every_P0_case_shows_every_section_from_content()
        {
            Assert.Equal(17, P0.Length);
            foreach (var id in P0)
            {
                var e = Start(id).Records!.Sections();
                var file = Content.CaseFiles[id];
                Assert.True(e.Profile.Name.Contains(file.Identity.FullName), id);
                Assert.Equal((file.Identity.Mrn, file.Identity.Dob), (e.Profile.Mrn, e.Profile.Dob));
                Assert.Equal(file.Allergies.Count, e.Profile.Alerts.Count);
                Assert.Equal(file.Presentation.Summary, e.Episode.Reason);
                Assert.Contains("clerking", e.Episode.Documents);
                Assert.NotEmpty(e.Reports);
                Assert.NotEmpty(e.Previous);                                                      // every case has its record (at least the summary letter)
                Assert.All(e.Previous, d => Assert.Contains(d, e.Reports));
                Assert.All(e.Imaging, im => Assert.True(im.ImageId.Length > 0 || im.Notice == EmrSections.ReportOnly, id + " " + im.DocumentId));
                Assert.NotEmpty(e.Medications.RegularNote);
            }
        }

        [Fact]
        public void An_allergy_is_an_alert_on_the_profile()
        {
            var e = Start("STK-S2-B").Records!.Sections();
            Assert.Contains(e.Profile.Alerts, a => a.StartsWith("Allergy: Penicillin"));
        }

        [Fact]
        public void A_previous_scan_without_a_picture_shows_the_report_only_notice()
        {
            var e = Start("VAR-S1-A").Records!.Sections();
            var us = e.Imaging.Single(i => i.DocumentId == "VAR-S1-A.clinic_ultrasound");
            Assert.Equal(("", EmrSections.ReportOnly), (us.ImageId, us.Notice));
            Assert.Contains("Gallstones", us.Report);
        }

        [Fact]
        public void Labs_show_results_with_the_previous_value_and_a_trend_once_an_analyte_has_two_values()
        {
            var s = Start("VAR-S1-PB");                                                            // the book gives earlier bilirubin and ALP for this patient
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"));
            s.Tick(120 * 60);
            var labs = s.Records!.Sections().Labs;
            var bili = labs.Rows.Single(r => r.Analyte == "Bilirubin");
            Assert.Equal("14", bili.Previous.Split(' ')[0]);
            Assert.True(labs.Trends.ContainsKey("Bilirubin"));
            Assert.Equal("previous", labs.Trends["Bilirubin"][0].At);
            Assert.Equal(2, labs.Trends["Bilirubin"].Count);
        }

        [Fact]
        public void The_NEWS2_trend_and_the_confirmed_prescriptions_come_from_what_happened()
        {
            var s = Start("VAR-S1-A");
            Assert.Empty(s.Records!.Sections().Episode.News2Trend);
            Do(s, "request_medication", ("class", "TR-PARACETAMOL"), ("indication", "pain"));
            s.Tick(150);
            var meds = s.Records.Sections().Medications;
            Assert.Contains(meds.Prescriptions, p => p.Treatment == "TR-PARACETAMOL");
        }
    }
}
