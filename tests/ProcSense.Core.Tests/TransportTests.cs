using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Location;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Places, transfers and the escort rule (p0-simulation-workflow-s13, plan tests T-L1 to T-L7 and T06-4).</summary>
    public class TransportTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        private static Session NewSession(ulong seed = 42, CaseCard? card = null, bool confirmed = true)
        {
            var s = new Session(card ?? Content.Cases["VAR-S1-A"], seed, SessionMode.Practice, Subsystems.Default(Content));
            if (confirmed) foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            Do(s, "ask", ("topic", "pregnancy"));   // the pregnancy question is asked first, so the pregnancy gate (imaging with radiation, theatre) is open
            return s;
        }

        private static void Unstable(Session s)
        {
            // breathing 26 (3) + SpO2 94 (1) + BP 92 (2) + pulse 128 (2) + temperature 39.2 (2) = NEWS2 10
            var v = s.Patient.Physiological.Vitals;
            s.Patient.Physiological.Vitals = v with { RespiratoryRate = 26, Spo2 = 94, SystolicBp = 92, HeartRate = 128, TemperatureC = 39.2 };
        }

        private static string StateOf(Session s) => s.Patient.Clinical.Transport?.State ?? "";

        private static void Until(Session s, string state, int maxSeconds = 4000)
        {
            for (int i = 0; i < maxSeconds && StateOf(s) != state; i++) s.Tick(1);
            Assert.Equal(state, StateOf(s));
        }

        private static (string, string)[] Us(string escort = "porter,nurse", string monitoring = "none", string accompany = "false", string mode = "trolley", string oxygen = "false") =>
            new[] { ("to", "ultrasound"), ("mode", mode), ("escort", escort), ("monitoring", monitoring), ("accompany", accompany), ("oxygen", oxygen) };

        private static List<(string state, long step)> Timeline(Session s) =>
            s.TransportHistory.Where(h => h.Kind.StartsWith("transport:")).Select(h => (h.Kind.Substring(10), h.Step)).ToList();

        // ---------------------------------------------------------------- T-L1 an ultrasound round trip

        [Fact]
        public void An_ultrasound_round_trip_goes_through_every_state_in_order()
        {
            var s = NewSession();
            var o = Do(s, "request_transport", Us());
            Assert.Contains("transport:REQUESTED", o.Effects);
            Until(s, "BACK");
            Assert.Equal(new[] { "REQUESTED", "PREPARING", "IN_TRANSIT", "ARRIVED", "DONE", "RETURNING", "BACK" }, Timeline(s).Select(t => t.state));
            Assert.Equal("patient_bay", s.Patient.Clinical.Location);
            Assert.Equal("patient_bay", s.Patient.Clinical.TraineeLocation);
        }

        [Fact]
        public void The_times_follow_the_content_three_minutes_each_way_and_ten_in_the_scan_room()
        {
            var s = NewSession();
            Do(s, "request_transport", Us());
            Until(s, "BACK");
            var t = Timeline(s).ToDictionary(x => x.state, x => x.step);
            Assert.Equal(180 * 2, t["ARRIVED"] - t["IN_TRANSIT"]);          // bay to corridor 1 minute + corridor to ultrasound 2 minutes = 3 minutes = 360 half-second steps
            Assert.Equal(10 * 60 * 2, t["DONE"] - t["ARRIVED"]);
            Assert.Equal(2 * 60 * 2, t["RETURNING"] - t["DONE"]);            // the porter comes back for the patient: the 2 preparation minutes
            Assert.Equal(180 * 2, t["BACK"] - t["RETURNING"]);
            Assert.Equal(2 * 60 * 2, t["IN_TRANSIT"] - t["PREPARING"]);
            long wait = t["PREPARING"] - t["REQUESTED"];
            Assert.InRange(wait, 2 * 60 * 2, 4 * 60 * 2);                    // the porter arrives after 2 to 4 minutes
        }

        [Fact]
        public void The_patient_is_in_the_corridor_on_the_way_and_in_the_scan_room_during_the_scan()
        {
            var s = NewSession();
            Do(s, "request_transport", Us());
            Until(s, "IN_TRANSIT");
            Assert.Equal("corridor", s.Patient.Clinical.Location);
            Until(s, "ARRIVED");
            Assert.Equal("ultrasound", s.Patient.Clinical.Location);
            Until(s, "RETURNING");
            Assert.Equal("corridor", s.Patient.Clinical.Location);
        }

        [Fact]
        public void The_tablet_shows_the_state_the_destination_the_escort_and_the_expected_arrival()
        {
            var s = NewSession();
            Do(s, "request_transport", Us(escort: "nurse,porter", monitoring: "portable"));
            var requested = s.Observable.Transport!;
            Assert.Equal("REQUESTED", requested.State);
            Assert.Equal("", requested.Arrives);
            Until(s, "PREPARING");
            Assert.Matches("^[0-9]{2}:[0-9]{2}$", s.Observable.Transport!.Arrives);
            Assert.Equal(new[] { "nurse", "porter" }, s.Observable.Transport!.Escort);
            Assert.Equal("portable", s.Observable.Transport!.Monitoring);
            Assert.Contains("\"transport\":{\"state\":\"PREPARING\"", s.Observable.ToJson());
        }

        // ---------------------------------------------------------------- T06-4 and the patient being elsewhere

        [Fact]
        public void An_action_aimed_at_the_patient_is_refused_while_the_patient_is_in_another_place_and_the_trainee_stayed()
        {
            var s = NewSession();
            Do(s, "request_transport", Us());
            Until(s, "ARRIVED");
            var o = Do(s, "examine", ("region", "eyes"));
            Assert.Equal(TransportSubsystem.PatientElsewhere, Reject(o));
            Assert.Contains("patient_in:ultrasound", o.Effects);
            foreach (var verb in new[] { "ask", "attach_sensor", "measure_temperature", "count_rr", "assess_acvpu", "ask_identity", "introduce_self", "ask_exam_permission", "airway_check", "murphy_test", "summarise", "explain_delay" })
                Assert.Equal(TransportSubsystem.PatientElsewhere, Reject(Do(s, verb)));
            Until(s, "BACK");
            Assert.True(Do(s, "measure_temperature").Accepted);
        }

        [Fact]
        public void The_trainees_own_judgements_and_the_notes_work_while_the_patient_is_away()
        {
            var s = NewSession();
            Do(s, "request_transport", Us());
            Until(s, "ARRIVED");
            Assert.True(Do(s, "wait").Accepted);
            Assert.True(Do(s, "open_notes").Accepted);
            Assert.NotEqual(TransportSubsystem.PatientElsewhere, Do(s, "assess_vte").RejectReason);
        }

        [Fact]
        public void A_trainee_who_accompanies_stays_with_the_patient_and_can_still_act()
        {
            var s = NewSession();
            Do(s, "request_transport", Us(accompany: "true"));
            Until(s, "IN_TRANSIT");
            Assert.Equal("corridor", s.Patient.Clinical.TraineeLocation);
            Assert.True(Do(s, "count_rr").Accepted);
            Until(s, "ARRIVED");
            Assert.Equal("ultrasound", s.Patient.Clinical.TraineeLocation);
            Assert.True(Do(s, "measure_temperature").Accepted);
            Until(s, "BACK");
            Assert.Equal("patient_bay", s.Patient.Clinical.TraineeLocation);
            Assert.Equal("patient_bay", s.Observable.TraineeLocation);
        }

        // ---------------------------------------------------------------- T-L2 cancelling

        [Fact]
        public void Cancelling_before_the_patient_leaves_cancels_the_transfer_and_the_patient_never_moves()
        {
            var s = NewSession();
            Do(s, "request_transport", Us());
            var o = Do(s, "cancel_transport");
            Assert.Contains("transport:CANCELLED", o.Effects);
            Assert.Equal("CANCELLED", StateOf(s));
            Assert.Equal("patient_bay", s.Patient.Clinical.Location);
            s.Tick(600);
            Assert.Equal("CANCELLED", StateOf(s));
            Assert.True(Do(s, "request_transport", Us()).Accepted);              // a new transfer can be asked for
        }

        [Fact]
        public void Cancelling_in_the_corridor_brings_the_patient_back_the_way_it_came()
        {
            var s = NewSession();
            Do(s, "request_transport", Us(accompany: "true"));
            Until(s, "IN_TRANSIT");
            s.Tick(60);
            var o = Do(s, "cancel_transport");
            Assert.Contains("transport:RETURNING", o.Effects);
            Assert.Contains("transport_cancelled", o.Effects);
            Until(s, "BACK");
            Assert.Equal("patient_bay", s.Patient.Clinical.Location);
            Assert.Equal("patient_bay", s.Patient.Clinical.TraineeLocation);
            Assert.DoesNotContain("ARRIVED", Timeline(s).Select(t => t.state));
        }

        [Fact]
        public void Nothing_can_be_cancelled_once_the_patient_has_arrived_or_when_there_is_no_transfer()
        {
            var s = NewSession();
            Assert.Equal(TransportSubsystem.NothingToCancel, Reject(Do(s, "cancel_transport")));
            Do(s, "request_transport", Us());
            Until(s, "ARRIVED");
            Assert.Equal(TransportSubsystem.CannotCancel, Reject(Do(s, "cancel_transport")));
        }

        // ---------------------------------------------------------------- T-L3 the escort rule at PREPARING

        private static IEnumerable<string> Findings(Session s) => s.TransportHistory.Where(h => h.Kind == "transport_inadequate_escort" || h.Kind == "transport_unsafe_mode").Select(h => h.Kind);

        [Fact]
        public void A_stable_patient_with_a_porter_and_a_nurse_is_adequately_escorted()
        {
            var s = NewSession();
            Do(s, "request_transport", Us("porter,nurse"));
            Until(s, "IN_TRANSIT");
            Assert.Empty(Findings(s));
            Assert.False(s.Patient.Clinical.HasFlag(TransportSubsystem.InadequateFlag));
        }

        [Fact]
        public void A_porter_alone_is_not_enough_even_for_a_stable_patient()
        {
            var s = NewSession();
            Do(s, "request_transport", Us("porter"));
            Until(s, "PREPARING");
            Assert.Contains("transport_inadequate_escort", Findings(s));
            Assert.Contains(s.TransportHistory, h => h.ScoreItems.Contains("major.transfer_escort_inadequate"));
        }

        [Fact]
        public void An_unstable_patient_with_no_monitoring_is_a_major_item()
        {
            var s = NewSession();
            Unstable(s);
            Do(s, "request_transport", Us("porter,nurse", "none"));
            Until(s, "PREPARING");
            Assert.Contains("transport_inadequate_escort", Findings(s));
            Assert.True(s.Patient.Clinical.HasFlag(TransportSubsystem.InadequateFlag));
        }

        [Fact]
        public void NEWS2_10_needs_a_doctor_a_nurse_full_monitoring_and_oxygen()
        {
            var good = NewSession();
            Unstable(good);
            Do(good, "request_transport", Us("doctor,nurse", "full", oxygen: "true"));
            Until(good, "PREPARING");
            Assert.Empty(Findings(good));

            foreach (var (escort, monitoring, oxygen) in new[] { ("nurse", "full", "true"), ("doctor,nurse", "portable", "true"), ("doctor,nurse", "full", "false"), ("porter,nurse", "full", "true") })
            {
                var s = NewSession();
                Unstable(s);
                Do(s, "request_transport", Us(escort, monitoring, oxygen: oxygen));
                Until(s, "PREPARING");
                Assert.Contains("transport_inadequate_escort", Findings(s));
            }
        }

        [Fact]
        public void NEWS2_5_to_6_needs_a_nurse_and_a_portable_monitor()
        {
            // oxygen 2 + breathing 22 (2) + pulse 95 (1) = 5
            NewSession_(out var ok);
            Do(ok, "request_transport", Us("nurse", "portable"));
            Until(ok, "PREPARING");
            Assert.Empty(Findings(ok));
            NewSession_(out var bad);
            Do(bad, "request_transport", Us("nurse", "none"));
            Until(bad, "PREPARING");
            Assert.Contains("transport_inadequate_escort", Findings(bad));

            static void NewSession_(out Session session)
            {
                session = NewSession();
                var v = session.Patient.Physiological.Vitals;
                session.Patient.Physiological.Vitals = v with { Oxygen = "supplemental", RespiratoryRate = 22, HeartRate = 95 };
            }
        }

        [Fact]
        public void The_check_uses_the_patients_true_state_not_what_the_trainee_has_measured()
        {
            var s = NewSession();                                 // nothing measured, no NEWS2 on the chart
            Unstable(s);
            Assert.Empty(s.Patient.Clinical.News2);
            Do(s, "request_transport", Us("porter,nurse", "none"));
            Until(s, "PREPARING");
            Assert.Contains("transport_inadequate_escort", Findings(s));
        }

        [Fact]
        public void A_walk_or_a_wheelchair_is_only_for_a_stable_S1_patient()
        {
            var stable = NewSession();
            Do(stable, "request_transport", Us(mode: "wheelchair"));
            Until(stable, "PREPARING");
            Assert.DoesNotContain("transport_unsafe_mode", Findings(stable));

            var unstable = NewSession();
            Unstable(unstable);
            Do(unstable, "request_transport", Us("doctor,nurse", "full", mode: "wheelchair", oxygen: "true"));
            Until(unstable, "PREPARING");
            Assert.Contains("transport_unsafe_mode", Findings(unstable));
            Assert.Contains(unstable.TransportHistory, h => h.ScoreItems.Contains("major.unsafe_transport_mode"));

            var emergency = NewSession(card: Content.Cases["VAR-S1-A"] with { Scenario = "S2" });
            Do(emergency, "request_transport", Us(mode: "walk"));
            Until(emergency, "PREPARING");
            Assert.Contains("transport_unsafe_mode", Findings(emergency));

            var trolley = NewSession();
            Unstable(trolley);
            Do(trolley, "request_transport", Us("doctor,nurse", "full", mode: "trolley", oxygen: "true"));
            Until(trolley, "PREPARING");
            Assert.DoesNotContain("transport_unsafe_mode", Findings(trolley));
        }

        // ---------------------------------------------------------------- T-L5 and T-L6 delays and determinism

        [Fact]
        public void An_unavailable_porter_delays_the_transfer_and_says_so()
        {
            var normal = NewSession();
            Do(normal, "request_transport", Us());
            Until(normal, "PREPARING");
            long normalWait = Timeline(normal).Single(t => t.state == "PREPARING").step - Timeline(normal).Single(t => t.state == "REQUESTED").step;

            var s = NewSession();
            s.Patient.Clinical.SetFlag(TransportSubsystem.PorterUnavailableFlag);
            var o = Do(s, "request_transport", Us());
            Assert.Contains("transport_delayed", o.Effects);
            Assert.Equal("DELAYED", StateOf(s));
            Assert.Equal("The porter is not available; the transfer is delayed by about 8 minutes.", s.Observable.Transport!.Message);
            Until(s, "PREPARING");
            long wait = Timeline(s).Single(t => t.state == "PREPARING").step - Timeline(s).Single(t => t.state == "DELAYED").step;
            Assert.True(wait >= 8 * 60 * 2 + 2 * 60 * 2 && wait <= 8 * 60 * 2 + 4 * 60 * 2, wait.ToString());
            Assert.False(s.Patient.Clinical.HasFlag(TransportSubsystem.PorterUnavailableFlag));   // used up by this request
            Assert.True(normalWait <= 4 * 60 * 2);
        }

        [Fact]
        public void The_same_seed_gives_the_same_delays_and_other_seeds_give_other_ones()
        {
            List<long> Steps(ulong seed)
            {
                var s = NewSession(seed);
                Do(s, "request_transport", Us());
                Until(s, "BACK");
                return Timeline(s).Select(t => t.step).ToList();
            }
            Assert.Equal(Steps(7), Steps(7));
            Assert.True(Enumerable.Range(1, 8).Select(i => string.Join(",", Steps((ulong)i))).Distinct().Count() > 1);
        }

        // ---------------------------------------------------------------- T-L7 the corridor event

        [Fact]
        public void An_unstable_patient_with_a_porter_only_has_oxygen_fall_to_89_in_the_corridor()
        {
            var s = NewSession();
            Unstable(s);
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Do(s, "request_transport", Us("porter", "none"));
            Until(s, "IN_TRANSIT");
            Assert.Equal(94, s.Patient.Physiological.Vitals.Spo2);
            Until(s, "ARRIVED");
            Assert.Equal(89, s.Patient.Physiological.Vitals.Spo2);
            Assert.Contains(events, e => e.Name == "E-CORRIDOR-DETERIORATION");
            Assert.True(s.Patient.Clinical.HasFlag(TransportSubsystem.CorridorEventFlag));
            Assert.Contains(s.TransportHistory, h => h.Kind == "corridor_event");
        }

        [Fact]
        public void The_corridor_event_does_not_fire_for_an_adequate_escort_a_stable_patient_or_a_cancelled_transfer()
        {
            var adequate = NewSession();
            Unstable(adequate);
            Do(adequate, "request_transport", Us("doctor,nurse", "full", oxygen: "true"));
            Until(adequate, "BACK");
            Assert.False(adequate.Patient.Clinical.HasFlag(TransportSubsystem.CorridorEventFlag));

            var stable = NewSession();
            Do(stable, "request_transport", Us("porter"));                      // inadequate, but she is stable
            Until(stable, "BACK");
            Assert.False(stable.Patient.Clinical.HasFlag(TransportSubsystem.CorridorEventFlag));
            Assert.Equal(98, stable.Patient.Physiological.Vitals.Spo2);

            var cancelled = NewSession();
            Unstable(cancelled);
            Do(cancelled, "request_transport", Us("porter", "none"));
            Until(cancelled, "PREPARING");
            Do(cancelled, "cancel_transport");
            s_tick(cancelled);
            Assert.False(cancelled.Patient.Clinical.HasFlag(TransportSubsystem.CorridorEventFlag));

            static void s_tick(Session s) => s.Tick(1200);
        }

        // ---------------------------------------------------------------- transfer to theatre, requests refused

        [Fact]
        public void A_transfer_to_theatre_ends_at_theatre_reception_and_the_patient_does_not_come_back()
        {
            var s = NewSession();
            Assert.True(Do(s, "transfer_to_theatre", ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true")).Accepted);
            Until(s, "DONE");
            Assert.Equal("theatre_reception", s.Patient.Clinical.Location);
            Assert.Equal("theatre_reception", s.Patient.Clinical.TraineeLocation);
            s.Tick(3600);
            Assert.Equal("DONE", StateOf(s));
            Assert.Equal("theatre_reception", s.Patient.Clinical.Location);
            var travel = Timeline(s).ToDictionary(t => t.state, t => t.step);
            Assert.Equal(4 * 60 * 2, travel["ARRIVED"] - travel["IN_TRANSIT"]);   // bay to corridor 1 + corridor to theatre reception 3
        }

        [Fact]
        public void Transfer_to_theatre_is_blocked_while_a_fasting_breach_is_unresolved()
        {
            var s = NewSession();
            s.Patient.Clinical.SetFlag("readiness.blocked");
            var o = Do(s, "transfer_to_theatre", ("mode", "trolley"), ("escort", "porter,nurse"));
            Assert.Equal("BLOCKED:B-READINESS", Reject(o));
            Assert.Equal("", StateOf(s));
        }

        [Fact]
        public void Bad_requests_are_refused_and_start_nothing()
        {
            var s = NewSession();
            Assert.Equal(TransportSubsystem.MissingDestination, Reject(Do(s, "request_transport", ("mode", "trolley"))));
            Assert.Equal(TransportSubsystem.UnknownLocation, Reject(Do(s, "request_transport", ("to", "moon"), ("mode", "trolley"))));
            Assert.Equal(TransportSubsystem.AlreadyThere, Reject(Do(s, "request_transport", ("to", "patient_bay"), ("mode", "trolley"))));
            Assert.Equal(TransportSubsystem.MissingMode, Reject(Do(s, "request_transport", ("to", "ct"))));
            Assert.Equal(TransportSubsystem.UnknownMode, Reject(Do(s, "request_transport", ("to", "ct"), ("mode", "hover"))));
            Assert.Equal(TransportSubsystem.UnknownEscort, Reject(Do(s, "request_transport", ("to", "ct"), ("mode", "trolley"), ("escort", "porter,clown"))));
            Assert.Equal(TransportSubsystem.UnknownMonitoring, Reject(Do(s, "request_transport", ("to", "ct"), ("mode", "trolley"), ("monitoring", "huge"))));
            Assert.Equal("", StateOf(s));
        }

        [Fact]
        public void A_second_request_is_refused_while_one_is_in_progress_and_nothing_can_be_requested_before_identity_is_confirmed()
        {
            var s = NewSession();
            Do(s, "request_transport", Us());
            Assert.Equal(TransportSubsystem.InProgress, Reject(Do(s, "request_transport", ("to", "ct"), ("mode", "trolley"))));
            var fresh = NewSession(confirmed: false);
            Assert.Equal("IDENTITY_NOT_CONFIRMED", Reject(Do(fresh, "request_transport", Us())));
            Assert.Equal("IDENTITY_NOT_CONFIRMED", Reject(Do(fresh, "transfer_to_theatre", ("mode", "trolley"))));
        }

        // carried over from p0-simulation-workflow-s18: the nurse is outside until she enters the bay
        [Fact]
        public void The_nurse_is_outside_when_called_and_with_the_patient_once_she_has_come_in()
        {
            var s = new Session(Content.Cases["TEST-ID-MISMATCH"], 42, SessionMode.Practice, Subsystems.Default(Content));
            Assert.Equal("", s.Patient.Clinical.NurseLocation);
            var seen = new List<string>();
            s.Events.Subscribe<ProcSense.Core.Workflow.Steps.NurseTaskChanged>(e => seen.Add(e.State + "@" + e.Location));
            Do(s, "ask_identity"); Do(s, "read_wristband"); Do(s, "open_notes");
            Assert.True(Do(s, "report_mismatch").Accepted);
            Assert.Equal(new[] { "CALLED@corridor", "AT_BEDSIDE@patient_bay", "REPLACING@patient_bay", "DONE@patient_bay" }, seen);
            Assert.Equal("patient_bay", s.Patient.Clinical.NurseLocation);
        }

        // ---------------------------------------------------------------- the graph and the content

        [Fact]
        public void The_shortest_route_goes_through_the_corridor_and_the_minutes_add_up()
        {
            var g = new LocationGraph(Content.Locations.Locations);
            var us = g.Shortest("patient_bay", "ultrasound")!;
            Assert.Equal(new[] { "patient_bay", "corridor", "ultrasound" }, us.Places);
            Assert.Equal(3, us.Minutes);
            Assert.Equal(5, g.Shortest("patient_bay", "theatre")!.Minutes);
            Assert.Equal(g.Shortest("ct", "patient_bay")!.Minutes, g.Shortest("patient_bay", "ct")!.Minutes);     // links work both ways
            Assert.Equal(0, g.Shortest("ct", "ct")!.Minutes);
            Assert.Null(g.Shortest("patient_bay", "nowhere"));
        }

        [Fact]
        public void A_place_nothing_links_to_has_no_route()
        {
            var json = File.ReadAllText(Path.Combine(TestCase.ContentRoot, "locations", "locations.json")).Replace("{\"to\": \"theatre\", \"minutes\": 1}", "");
            var edited = Edit(n => n["locations"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("{\"id\":\"island\",\"label\":\"Island\",\"adjacent\":[],\"capabilities\":[],\"returns\":true,\"stay_minutes\":0}")));
            var g = new LocationGraph(LocationTables.Parse(edited).Locations);
            Assert.Null(g.Shortest("patient_bay", "island"));
            Assert.NotNull(json);
        }

        [Fact]
        public void The_content_loads_with_eight_places_three_modes_and_three_escort_tiers()
        {
            Assert.Equal(8, Content.Locations.Locations.Count);
            Assert.Equal(new[] { "walk", "wheelchair", "trolley" }, Content.Locations.Modes.Select(m => m.Id));
            Assert.Equal(new[] { 0, 5, 7 }, Content.Locations.Tiers.Select(t => t.MinNews2));
            Assert.Equal(89, Content.Locations.Corridor.Spo2);
            Assert.False(Content.Locations.Find("theatre_reception")!.Returns);
        }

        private static string Edit(Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "locations", "locations.json")))!;
            change(node);
            return node.ToJsonString();
        }

        [Fact]
        public void Broken_location_content_is_refused_with_its_place()
        {
            Assert.Contains("is not a location", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["locations"]![0]!["adjacent"]![0]!["to"] = "nowhere"))).Message);
            Assert.Contains("duplicate location", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["locations"]![1]!["id"] = "patient_bay"))).Message);
            Assert.Contains("patient_bay", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["locations"]![0]!["id"] = "bay"))).Message);
            Assert.Contains("gap or an overlap", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["transport"]!["escort_tiers"]![1]!["min_news2"] = 6))).Message);
            Assert.Contains("no upper limit", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["transport"]!["escort_tiers"]![2]!["max_news2"] = 20))).Message);
            Assert.Contains("between 0 and 1", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["corridor_event"]!["at_fraction"] = 1.5))).Message);
            Assert.Contains("more than 0", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["locations"]![0]!["adjacent"]![0]!["minutes"] = 0))).Message);
            Assert.Contains("not an escort role", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["transport"]!["escort_tiers"]![0]!["escort"]![0] = "clown"))).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => LocationTables.Parse(Edit(n => n["colour"] = 1))).Message);
        }
    }
}
