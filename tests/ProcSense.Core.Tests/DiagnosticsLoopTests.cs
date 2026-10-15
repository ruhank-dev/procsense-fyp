using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Diagnostics;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The diagnostics loop in Core (p0-diagnostic-transitions-s01, experiment EXP-DT-2): the events in order for a sample, a scan and a bedside test, time skipped through the loop, and a skip that stops when something needs the trainee.</summary>
    public class DiagnosticsLoopTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private sealed class Recorder
        {
            public readonly List<string> Seen = new List<string>();

            public Recorder(Session s)
            {
                s.Events.Subscribe<InvestigationRequested>(e => Seen.Add("requested:" + e.Test));
                s.Events.Subscribe<SampleSent>(e => Seen.Add("sent:" + e.Test + ":" + e.Via));
                s.Events.Subscribe<PatientDeparted>(e => Seen.Add("departed:" + e.Test + ":" + e.To));
                s.Events.Subscribe<ResultReady>(e => { if (!Seen.Contains("ready:" + e.Test)) Seen.Add("ready:" + e.Test); });
                s.Events.Subscribe<PatientReturned>(e => Seen.Add("returned:" + e.Test));
                s.Events.Subscribe<ReportDelivered>(e => Seen.Add("delivered:" + e.Test + ":" + e.With));
            }
        }

        private static Session Start(string caseId)
        {
            var s = new Session(Content.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            Do(s, "ask", ("topic", "pregnancy"));   // opens the pregnancy gate for imaging where the case needs it
            return s;
        }

        private static void Until(Session s, System.Func<bool> done, int maxSeconds = 6 * 3600)
        {
            for (int i = 0; i < maxSeconds && !done(); i++) s.Tick(1);
            Assert.True(done(), "the loop did not get there in time");
        }

        [Fact]
        public void A_blood_test_is_requested_sent_to_the_laboratory_by_the_porter_and_its_report_comes_back_from_the_laboratory()
        {
            var s = Start("VAR-S1-A");
            var rec = new Recorder(s);
            Assert.True(Do(s, "order_investigation", ("test", "lft")).Accepted);
            Assert.True(Do(s, "take_sample", ("test", "lft")).Accepted);
            var trip = s.Diagnostics!.Trips.Single();
            Until(s, () => trip.Stage == DiagnosticTrip.ReportDelivered);
            Assert.Equal(new[] { "requested:lft", "sent:lft:porter", "ready:lft", "delivered:lft:laboratory" }, rec.Seen);
            Assert.Equal(("blood", "", "porter"), (trip.Kind, trip.Destination, trip.Via));
            Assert.True(trip.RequestedStep <= trip.SentStep && trip.SentStep < trip.ResultStep && trip.ResultStep == trip.DeliveredStep);
            Assert.Equal(-1, trip.DepartedStep);                                      // the patient never left
            Assert.Equal(s.Patient.Clinical.TraineeLocation, s.Patient.Clinical.Location);
        }

        [Fact]
        public void A_scan_takes_the_patient_away_and_the_report_comes_back_with_the_patient()
        {
            var s = Start("VAR-S1-A");
            var rec = new Recorder(s);
            string bay = s.Patient.Clinical.Location;
            Assert.True(Do(s, "order_investigation", ("test", "ultrasound"), ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "false")).Accepted);
            var trip = s.Diagnostics!.Trips.Single();
            Assert.Equal("ultrasound", trip.Destination);
            Until(s, () => trip.Stage == DiagnosticTrip.PatientAway);
            Assert.NotEqual(bay, s.Patient.Clinical.Location);                       // away (in the corridor or the scan room) ...
            Assert.Equal(PatientElsewhere(), Do(s, "examine", ("region", "abdomen")).RejectReason);   // ... so nothing can be done to the patient
            Until(s, () => trip.Stage == DiagnosticTrip.ReportDelivered);
            Assert.Equal(new[] { "requested:ultrasound", "departed:ultrasound:ultrasound", "ready:ultrasound", "returned:ultrasound", "delivered:ultrasound:patient" },
                rec.Seen.OrderBy(x => Rank(x)).ToArray());
            Assert.Equal(bay, s.Patient.Clinical.Location);
            Assert.Equal(System.Math.Max(trip.ResultStep, trip.ReturnedStep), trip.DeliveredStep);   // the report travels with the patient
        }

        private static string PatientElsewhere() => Location.TransportSubsystem.PatientElsewhere;

        private static int Rank(string e) => new[] { "requested", "departed", "sent", "ready", "returned", "delivered" }.ToList().IndexOf(e.Split(':')[0]);

        [Fact]
        public void A_bedside_test_is_done_where_the_patient_is()
        {
            var s = Start("VAR-S1-A");
            var rec = new Recorder(s);
            Do(s, "order_investigation", ("test", "glucose_fingerprick"));
            Assert.True(Do(s, "perform_bedside_test", ("test", "glucose_fingerprick")).Accepted);
            var trip = s.Diagnostics!.Trips.Single();
            Until(s, () => trip.Stage == DiagnosticTrip.ReportDelivered);
            Assert.Equal(new[] { "requested:glucose_fingerprick", "ready:glucose_fingerprick", "delivered:glucose_fingerprick:bedside" }, rec.Seen);
            Assert.Contains(s.Diagnostics.History, h => h.Kind == "diagnostics:glucose_fingerprick:AT_BEDSIDE");
        }

        [Fact]
        public void Skipping_time_runs_the_whole_loop_and_is_the_same_as_playing_it()
        {
            Session Run(bool skip)
            {
                var s = Start("VAR-S1-A");
                Do(s, "order_investigation", ("test", "lft"));
                Do(s, "take_sample", ("test", "lft"));
                if (skip) Assert.True(s.TimeSkip(120).Accepted); else s.Tick(120 * 60);
                return s;
            }
            var skipped = Run(true).Diagnostics!.Trips.Single();
            var played = Run(false).Diagnostics!.Trips.Single();
            Assert.Equal(DiagnosticTrip.ReportDelivered, skipped.Stage);
            Assert.Equal((played.SentStep, played.ResultStep, played.DeliveredStep), (skipped.SentStep, skipped.ResultStep, skipped.DeliveredStep));
        }

        // EXP-DT-2: the untreated septic patient (Mr Usman, VAR-S2-PD) keeps deteriorating on the clock while away for a scan.
        [Fact]
        public void EXP_DT_2_an_untreated_S2_patient_sent_to_CT_deteriorates_during_the_trip()
        {
            // the Usman timeline drives the vital signs (the same set-up as the deterioration tests); the rest of the case is the S1 card played as S2
            var usman = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), "VAR-S2-PD");
            var chain = Subsystems.Default(Content).Where(x => !(x is Physiology.DeteriorationSubsystem)).Concat(new Pipeline.ISubsystem[] { new Physiology.DeteriorationSubsystem(Content, usman) });
            var s = new Session(Content.Cases["VAR-S1-A"] with { Scenario = usman.Scenario }, 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            Do(s, "ask", ("topic", "pregnancy"));
            Assert.True(Do(s, "order_investigation", ("test", "ct"), ("mode", "trolley"), ("escort", "porter,nurse,doctor"), ("monitoring", "full"), ("oxygen", "true"), ("accompany", "false")).Accepted,
                s.LastRejection);
            var trip = s.Diagnostics!.Trips.Single();
            Until(s, () => trip.Stage == DiagnosticTrip.PatientAway);
            double hrLeaving = s.Patient.Physiological.Vitals.HeartRate;
            Until(s, () => trip.ReturnedStep >= 0);
            double hrBack = s.Patient.Physiological.Vitals.HeartRate;
            Assert.True(hrBack > hrLeaving, $"heart rate {hrLeaving} when leaving, {hrBack} when back: the deterioration clock must keep running during the trip");
        }

        [Fact]
        public void A_skip_stops_when_the_laboratory_phones_with_a_critical_result()
        {
            var s = Start("VAR-S1-A");
            s.Patient.Clinical.CriticalCalls.Add(new State.CriticalCall { Id = 1, Test = "u_and_e", State = "RINGING" });   // the laboratory rings while time is skipped
            var o = s.TimeSkip(60);
            Assert.Contains("stopped:critical_call:u_and_e", o.Effects);
            Assert.Contains("skipped_steps:1", o.Effects);                                  // stopped after the step in which it rang
            Assert.DoesNotContain(s.TimeSkip(1).Effects, e => e.StartsWith("stopped:"));    // a call is only announced once
        }

        [Fact]
        public void The_loop_changes_nothing_the_log_or_the_score_sees()
        {
            Pipeline.ISubsystem[] Without() => Subsystems.Default(Content).Where(x => !(x is DiagnosticsLoop)).ToArray();
            Session Play(IEnumerable<Pipeline.ISubsystem> chain)
            {
                var s = new Session(Content.Cases["VAR-S1-A"], 7, SessionMode.Practice, chain);
                foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Do(s, verb);
                Do(s, "order_investigation", ("test", "fbc")); Do(s, "take_sample", ("test", "fbc"));
                s.Tick(90 * 60);
                Do(s, "view_result", ("test", "fbc"));
                return s;
            }
            var with = Play(Subsystems.Default(Content));
            var without = Play(Without());
            Assert.Equal(without.Log.Hash(), with.Log.Hash());
            Assert.Null(without.Diagnostics);
        }
    }
}
