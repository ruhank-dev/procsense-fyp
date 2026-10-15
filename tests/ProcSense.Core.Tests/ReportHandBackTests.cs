using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Diagnostics;
using ProcSense.Core.Records;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Report hand-back (p0-diagnostic-transitions-s05, Core half): the EMR has the result when it is ready, the printout arrives with whoever brings it and lies on the desk, and filing it puts the same document in the folder.</summary>
    public class ReportHandBackTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            Do(s, "ask", ("topic", "pregnancy"));
            return s;
        }

        // The step's verification, headless: a scan trip, the report comes back with the patient, it is filed in the folder, and the EMR has the very same report.
        [Fact]
        public void A_scan_trip_brings_the_printed_report_back_and_filing_puts_the_same_report_in_the_folder()
        {
            var s = Start();
            Assert.True(Do(s, "order_investigation", ("test", "ultrasound"), ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "false")).Accepted);
            var trip = s.Diagnostics!.Trips.Single();
            for (int i = 0; i < 6 * 3600 && trip.ResultStep < 0; i++) s.Tick(1);
            var doc = s.Records!.Documents.Single(d => d.OrderId == trip.OrderId);
            Assert.Contains(s.Records.Emr, d => ReferenceEquals(d, doc));                              // the EMR updated when the result was ready
            if (trip.ReturnedStep < 0) Assert.Equal(RecordsSubsystem.NotDelivered, Do(s, "file_document", ("id", doc.Id)).RejectReason);   // no paper to file while the patient is away
            for (int i = 0; i < 6 * 3600 && trip.DeliveredStep < 0; i++) s.Tick(1);
            Assert.Equal((true, false, "patient"), (doc.OnDesk, doc.InFolder, doc.DeliveredWith));
            Assert.Contains("filed:" + doc.Id, Do(s, "file_document", ("id", doc.Id)).Effects);
            Assert.Equal((false, true), (doc.OnDesk, doc.InFolder));
            Assert.Contains(s.Records.Folder, d => ReferenceEquals(d, doc));
            Assert.Equal(s.Patient.Clinical.Investigations.Single(o => o.Id == trip.OrderId).Report!.Text, doc.Body);   // the printout's text is the result's text: one source
            Assert.Contains(s.Records.History, h => h.Kind == "document_filed:imaging_report");
            Assert.Contains("file_document", s.Log.ToJsonl());
        }

        [Fact]
        public void A_printout_never_filed_stays_on_the_desk_and_nothing_is_lost()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"));
            s.Tick(120 * 60);
            var doc = s.Records!.Documents.Single(d => d.OrderId > 0);
            Assert.Equal(("laboratory", true), (doc.DeliveredWith, doc.OnDesk));
            Assert.DoesNotContain(s.Records.Folder, d => ReferenceEquals(d, doc));
            Assert.Contains(s.Records.Emr, d => ReferenceEquals(d, doc));
            Assert.True(Do(s, "open_document", ("id", doc.Id), ("view", "folder")).Accepted);      // the paper on the desk can be read before it is filed
        }

        [Fact]
        public void Filing_is_refused_for_what_is_not_a_delivered_printout()
        {
            var s = Start();
            Assert.Equal(RecordsSubsystem.NotPrintout, Do(s, "file_document", ("id", "clerking")).RejectReason);
            Assert.Equal(RecordsSubsystem.UnknownDocument, Do(s, "file_document", ("id", "nope")).RejectReason);
            Assert.Equal(RecordsSubsystem.MissingDocument, Do(s, "file_document").RejectReason);
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"));
            s.Tick(120 * 60);
            var doc = s.Records!.Desk.Single();
            Assert.True(Do(s, "file_document", ("id", doc.Id)).Accepted);
            Assert.Equal(RecordsSubsystem.AlreadyFiled, Do(s, "file_document", ("id", doc.Id)).RejectReason);
        }
    }
}
