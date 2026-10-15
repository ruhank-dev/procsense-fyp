using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Records;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The EMR's automatic update and new-result badges (p0-patient-records-emr-s05, Core half): a result badges its tab at once, opening it anywhere clears it, and a critical result still rings.</summary>
    public class BadgesTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        [Fact]
        public void Every_document_type_shows_under_one_of_the_EMR_tabs()
        {
            var tabs = Content.Records.Tabs.Select(t => t.Id).ToList();
            Assert.Equal(new[] { "profile", "current_episode", "labs", "imaging", "reports", "history", "medications" }, tabs);
            Assert.All(Content.Records.Types, t => Assert.Contains(t.Tab, tabs));
        }

        [Fact]
        public void A_session_starts_with_no_badges()
        {
            var b = Start().Records!.Badges;
            Assert.All(b.Counts.Values, n => Assert.Equal(0, n));                       // the starting paperwork and the old record are not news
        }

        // The step's own check, headless: VAR-S1-A's liver tests; after the turnaround the Labs tab shows 1; opening the result clears it; the log has open_document.
        [Fact]
        public void A_result_badges_the_labs_tab_and_opening_it_clears_the_badge()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"));
            var r = s.Records!;
            Assert.Equal(0, r.Badges.Count("labs"));
            s.Tick(120 * 60);
            Assert.Equal(1, r.Badges.Count("labs"));
            var doc = r.Documents.Single(d => d.OrderId > 0);
            Assert.Equal(new[] { doc.Id }, r.Badges.Unseen("labs"));
            Assert.Contains("badge_cleared:labs", Do(s, "open_document", ("id", doc.Id)).Effects);
            Assert.Equal(0, r.Badges.Count("labs"));
            Assert.Contains("open_document", s.Log.ToJsonl());
            Assert.DoesNotContain("badge_cleared:labs", Do(s, "open_document", ("id", doc.Id)).Effects);   // only once
        }

        [Fact]
        public void Reading_the_printout_on_the_desk_clears_the_EMR_badge_too()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"));
            s.Tick(120 * 60);
            var doc = s.Records!.Desk.Single();
            Do(s, "open_document", ("id", doc.Id), ("view", "folder"));                  // seen anywhere is seen
            Assert.Equal(0, s.Records.Badges.Count("labs"));
        }

        [Fact]
        public void A_scan_report_badges_imaging_and_a_signed_consent_badges_the_current_episode()
        {
            var s = Start();
            Do(s, "ask", ("topic", "pregnancy"));
            Do(s, "order_investigation", ("test", "ultrasound"), ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "false"));
            for (int i = 0; i < 6 * 3600 && s.Records!.Badges.Count("imaging") == 0; i++) s.Tick(1);
            Assert.Equal(1, s.Records!.Badges.Count("imaging"));
            s.Patient.Clinical.SetFlag("consent.signed");
            s.Tick(1);
            Assert.Equal(new[] { "consent" }, s.Records.Badges.Unseen("current_episode"));
        }

        [Fact]
        public void A_critical_result_still_rings_the_phone_the_badge_never_replaces_the_call()
        {
            var file = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), "SC-8");
            var chain = Subsystems.Default(Content).Where(x => !(x is InvestigationsSubsystem) && !(x is ResultReviewSubsystem) && !(x is RecordsSubsystem))
                .Concat(new ISubsystem[] { new InvestigationsSubsystem(Content, file), new ResultReviewSubsystem(Content, file), new RecordsSubsystem(Content, "SC-8") });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Do(s, verb);
            Do(s, "order_investigation", ("test", "u_and_e"));
            Do(s, "take_sample", ("test", "u_and_e"));
            s.Tick(100 * 60);
            Assert.Equal(1, s.Records!.Badges.Count("labs"));
            Assert.Contains(s.Patient.Clinical.CriticalCalls, c => c.Test == "u_and_e");   // the laboratory phones as well
        }
    }
}
