using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Diagnostics;
using ProcSense.Core.Time;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Lab samples by the tube station or the porter (p0-diagnostic-transitions-s04): tubes kept at the bedside until sent, the turnaround starting when they arrive, the laboratory's rejection, and the nurse's beginner prompt.</summary>
    public class SampleDispatchTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start(SessionMode mode = SessionMode.Practice, TrainingLevel level = TrainingLevel.Beginner)
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(Content), new SessionOptions(level));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static long Steps(double minutes) => (long)System.Math.Round(minutes * 60 / SimClock.Step);

        private static State.InvestigationOrder Order(Session s, string test) => s.Patient.Clinical.Investigations.Last(o => o.Test == test);

        [Fact]
        public void The_content_has_the_tube_station_and_the_porter_and_the_beginner_prompt()
        {
            var t = Content.Diagnostics;
            Assert.Equal(new[] { "tube_station", "porter" }, t.Dispatch.Select(d => d.Id));
            Assert.Equal((2.0, false), (t.Way("tube_station")!.TransitMinutes, t.Way("tube_station")!.Porter));
            Assert.True(t.Way("porter")!.Porter);
            Assert.Equal((10.0, true), (t.PromptAfterMinutes, t.PromptBeginnerOnly));
        }

        [Fact]
        public void A_sample_taken_without_send_goes_at_once_as_before()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"));
            Assert.Equal("PROCESSING", Order(s, "lft").State);
            Assert.Equal("porter", s.Diagnostics!.Trips.Single().Via);
        }

        [Fact]
        public void A_kept_sample_waits_at_the_bedside_and_the_turnaround_starts_when_the_tube_station_delivers_it()
        {
            var s = Start();
            var seen = new List<string>();
            s.Events.Subscribe<SampleSent>(e => seen.Add("sent:" + e.Via));
            Do(s, "order_investigation", ("test", "lft"));
            Assert.Contains("sample_waiting:lft", Do(s, "take_sample", ("test", "lft"), ("send", "later")).Effects);
            s.Tick(60 * 5);
            var order = Order(s, "lft");
            Assert.Equal("SAMPLE_TAKEN", order.State);                                     // nothing happens to tubes left on the bed table
            Assert.Equal(DiagnosticTrip.SampleWaiting, s.Diagnostics!.Trips.Single().Stage);
            long sentAt = s.Clock.Steps;
            Assert.Contains("sent:lft:tube_station", Do(s, "send_samples", ("via", "tube_station")).Effects);
            Assert.Equal(("SENT", sentAt + Steps(2)), (order.State, order.ReadyStep));
            Assert.Equal(new[] { "sent:tube_station" }, seen);
            s.Tick(2 * 60);
            Assert.Equal("PROCESSING", order.State);                                       // arrived: the laboratory starts its turnaround now
            for (int i = 0; i < 6 * 3600 && order.State == "PROCESSING"; i++) s.Tick(1);
            Assert.Equal("RESULT_AVAILABLE", order.State);
            Assert.Equal(DiagnosticTrip.ReportDelivered, s.Diagnostics.Trips.Single().Stage);
        }

        [Fact]
        public void The_porter_comes_first_then_walks_the_sample_to_the_laboratory()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("send", "later"));
            Do(s, "order_investigation", ("test", "u_and_e")); Do(s, "take_sample", ("test", "u_and_e"), ("send", "later"));
            long now = s.Clock.Steps;
            var o = Do(s, "send_samples", ("via", "porter"));
            Assert.Contains("sent:lft:porter", o.Effects);
            Assert.Contains("sent:u_and_e:porter", o.Effects);                             // one porter takes every waiting tube
            long arrives = Order(s, "lft").ReadyStep;
            Assert.Equal(arrives, Order(s, "u_and_e").ReadyStep);
            var loc = Content.Locations;
            Assert.InRange(arrives - now, Steps(5 + loc.PorterMin), Steps(5 + loc.PorterMax));
            Assert.Equal("porter", Order(s, "lft").Sample["via"]);
        }

        [Fact]
        public void Only_the_named_tubes_are_sent_and_wrong_requests_are_refused()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("send", "later"));
            Do(s, "order_investigation", ("test", "fbc")); Do(s, "take_sample", ("test", "fbc"), ("send", "later"));
            Assert.Equal(InvestigationsSubsystem.UnknownDispatch, Do(s, "send_samples", ("via", "drone")).RejectReason);
            Assert.Equal(InvestigationsSubsystem.NotWaiting, Do(s, "send_samples", ("via", "porter"), ("tests", "crp")).RejectReason);
            Assert.True(Do(s, "send_samples", ("via", "tube_station"), ("tests", "fbc")).Accepted);
            Assert.Equal(("SAMPLE_TAKEN", "SENT"), (Order(s, "lft").State, Order(s, "fbc").State));
            Do(s, "send_samples", ("via", "tube_station"));
            Assert.Equal(InvestigationsSubsystem.NothingToSend, Do(s, "send_samples", ("via", "tube_station")).RejectReason);
            Do(s, "order_investigation", ("test", "glucose_fingerprick"));
            Assert.Equal(InvestigationsSubsystem.BadSend, Do(s, "perform_bedside_test", ("test", "glucose_fingerprick"), ("send", "later")).RejectReason);   // a bedside test has nothing to send
            Do(s, "order_investigation", ("test", "crp"));
            Assert.Equal(InvestigationsSubsystem.BadSend, Do(s, "take_sample", ("test", "crp"), ("send", "tomorrow")).RejectReason);
        }

        [Fact]
        public void An_unlabelled_tube_is_still_rejected_by_the_laboratory_and_must_be_taken_again()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"), ("label", "unlabelled"), ("send", "later"));
            Do(s, "send_samples", ("via", "tube_station"));
            var order = Order(s, "lft");
            for (int i = 0; i < 6 * 3600 && order.State != "REJECTED"; i++) s.Tick(1);
            Assert.Equal("REJECTED", order.State);
            Assert.Contains("not labelled", order.Note);
            Assert.Equal(DiagnosticTrip.Rejected, s.Diagnostics!.Trips.Single(t => t.OrderId == order.Id).Stage);
            Assert.True(Do(s, "order_investigation", ("test", "lft")).Accepted);           // re-take: a new order and a new sample
            Assert.True(Do(s, "take_sample", ("test", "lft"), ("send", "later")).Accepted);
        }

        [Fact]
        public void The_nurse_points_out_tubes_left_at_the_bedside_for_a_beginner_only()
        {
            int Prompts(SessionMode mode, TrainingLevel level, out Session s)
            {
                s = Start(mode, level);
                int n = 0;
                s.Events.Subscribe<SamplesWaiting>(_ => n++);
                Do(s, "order_investigation", ("test", "lft"));
                Do(s, "take_sample", ("test", "lft"), ("send", "later"));
                s.Tick(30 * 60);
                return n;
            }
            Assert.Equal(1, Prompts(SessionMode.Practice, TrainingLevel.Beginner, out var beginner));      // once, not every step
            Assert.True(beginner.Diagnostics!.Trips.Single().Prompted);
            Assert.Equal(0, Prompts(SessionMode.Practice, TrainingLevel.Advanced, out _));
            Assert.Equal(0, Prompts(SessionMode.Assessment, TrainingLevel.Beginner, out _));
        }

        [Fact]
        public void A_skip_stops_when_the_nurse_mentions_the_waiting_tubes()
        {
            var s = Start();
            Do(s, "order_investigation", ("test", "lft"));
            Do(s, "take_sample", ("test", "lft"), ("send", "later"));
            var o = s.TimeSkip(60);
            Assert.Contains("stopped:samples_waiting:lft", o.Effects);
            Assert.Contains("skipped_steps:" + Steps(10), o.Effects);
        }
    }
}
