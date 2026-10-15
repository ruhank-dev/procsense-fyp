using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Records;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The records data model (p0-patient-records-emr-s01): one copy of each document in two views, a result appearing in both, the hospital record in the EMR only, opening logged, search with near-miss patients.</summary>
    public class RecordsTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start(string caseId = "VAR-S1-A")
        {
            var s = new Session(Content.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        [Fact]
        public void The_content_has_the_document_types_every_cases_record_and_the_distractors()
        {
            var t = Content.Records;
            Assert.Contains(t.Types, x => x.Id == "clerking" && x.Episode == "current");
            Assert.Contains(t.Types, x => x.Id == "previous_imaging" && x.Episode == "record");
            Assert.Equal(new[] { "imaging_report", "lab_report", "bedside_result" }.OrderBy(x => x), new[] { t.ResultTypes["imaging"], t.ResultTypes["blood"], t.ResultTypes["bedside"] }.OrderBy(x => x));
            Assert.Equal(3, t.Records["VAR-S1-A"].Count);
            Assert.All(t.Records.Values.SelectMany(x => x), r => Assert.StartsWith("SR-", r.Ref));
            Assert.Equal(6, t.Distractors.Count);
        }

        [Fact]
        public void A_session_starts_with_the_paperwork_in_the_folder_and_the_hospital_record_only_in_the_EMR()
        {
            var r = Start().Records!;
            Assert.Equal(new[] { "clerking", "obs_chart" }, r.Folder.Select(d => d.Id));
            Assert.Equal(new[] { "clerking", "obs_chart", "VAR-S1-A.preassessment", "VAR-S1-A.clinic_ultrasound", "VAR-S1-A.previous_anaesthetic" }, r.Emr.Select(d => d.Id));
            var us = r.Documents.Single(d => d.Id == "VAR-S1-A.clinic_ultrasound");
            Assert.Equal(("previous_imaging", "record", false), (us.Type, us.Episode, us.InFolder));
            Assert.Equal("before the booking", us.DateOffset);
        }

        [Fact]
        public void A_result_becomes_one_document_seen_in_the_EMR_and_once_filed_in_the_folder()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"));
            s.Tick(90 * 60);
            var r = s.Records!;
            var doc = r.Documents.Single(d => d.OrderId > 0);
            Assert.Equal("lab_report", doc.Type);
            Assert.Contains(r.Emr, d => ReferenceEquals(d, doc));
            Assert.Contains(r.Desk, d => ReferenceEquals(d, doc));               // the printout has come back from the laboratory ...
            Assert.True(Do(s, "file_document", ("id", doc.Id)).Accepted);
            Assert.Contains(r.Folder, d => ReferenceEquals(d, doc));             // ... and once filed it is the same object in the folder: the views cannot disagree
            Assert.Contains("Bilirubin", doc.Body);
            Assert.True(Do(s, "open_document", ("id", doc.Id), ("view", "folder")).Accepted);
            Assert.True(Do(s, "open_document", ("id", doc.Id)).Accepted);
            Assert.Equal(2, doc.TimesOpened);
        }

        [Fact]
        public void A_scan_report_is_in_the_EMR_when_ready_and_comes_back_on_paper_only_with_the_patient()
        {
            var s = Start();
            Do(s, "ask", ("topic", "pregnancy"));
            Assert.True(Do(s, "order_investigation", ("test", "ultrasound"), ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "false")).Accepted);
            var trip = s.Diagnostics!.Trips.Single();
            for (int i = 0; i < 6 * 3600 && trip.ResultStep < 0; i++) s.Tick(1);
            var doc = s.Records!.Documents.Single(d => d.OrderId == trip.OrderId);
            Assert.Equal("imaging_report", doc.Type);
            if (trip.ReturnedStep < 0)
            {
                Assert.True(doc.InEmr);
                Assert.False(doc.InFolder);
                Assert.Equal(RecordsSubsystem.NotInFolder, Do(s, "open_document", ("id", doc.Id), ("view", "folder")).RejectReason);
            }
            for (int i = 0; i < 6 * 3600 && trip.DeliveredStep < 0; i++) s.Tick(1);
            Assert.Equal((true, "patient"), (doc.OnDesk, doc.DeliveredWith));
            if (doc.ImageId.Length > 0) Assert.Contains("image:" + doc.ImageId, Do(s, "view_image", ("id", doc.Id)).Effects);
        }

        [Fact]
        public void Opening_the_previous_record_is_logged_so_the_score_can_see_it()
        {
            var s = Start();
            var o = Do(s, "open_document", ("id", "VAR-S1-A.previous_anaesthetic"));
            Assert.Contains("opened:VAR-S1-A.previous_anaesthetic", o.Effects);
            Assert.Contains("open_document", s.Log.ToJsonl());
            Assert.Contains(s.Records!.History, h => h.Kind == "document_opened:anaesthetic_history");
            Assert.Equal(RecordsSubsystem.NotInFolder, Do(s, "open_document", ("id", "VAR-S1-A.previous_anaesthetic"), ("view", "folder")).RejectReason);   // the old record is not paper in the folder
            Assert.Equal(RecordsSubsystem.UnknownDocument, Do(s, "open_document", ("id", "nope")).RejectReason);
            Assert.Equal(RecordsSubsystem.MissingDocument, Do(s, "open_document").RejectReason);
            Assert.Equal(RecordsSubsystem.NoImage, Do(s, "view_image", ("id", "clerking")).RejectReason);
        }

        [Fact]
        public void Signing_the_consent_puts_the_consent_form_in_the_folder()
        {
            var s = Start();
            Assert.DoesNotContain(s.Records!.Folder, d => d.Id == "consent");
            s.Patient.Clinical.SetFlag("consent.signed");
            s.Tick(1);
            Assert.Contains(s.Records.Folder, d => d.Id == "consent" && d.Type == "consent");
        }

        [Fact]
        public void Searching_finds_the_patient_and_the_near_misses_so_the_trainee_must_check()
        {
            var s = Start();
            var byName = Do(s, "search_patient", ("query", "ayesha"));
            Assert.Equal(new[] { "match:DIST-03", "match:DIST-04", "match:VAR-S1-A" }, byName.Effects.Where(e => e.StartsWith("match:")).OrderBy(e => e));
            Assert.Equal(new[] { "match:VAR-S1-A" }, Do(s, "search_patient", ("query", "102938")).Effects.Where(e => e.StartsWith("match:")));
            Assert.Contains("matches:0", Do(s, "search_patient", ("query", "nobody")).Effects);
            Assert.Equal(RecordsSubsystem.MissingQuery, Do(s, "search_patient", ("query", " ")).RejectReason);
            Assert.Equal(7, s.Records!.Patients.Count);
        }
    }
}
