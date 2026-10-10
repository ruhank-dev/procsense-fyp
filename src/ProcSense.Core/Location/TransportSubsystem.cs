using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Random;
using ProcSense.Core.State;
using ProcSense.Core.Time;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Location
{
    /// <summary>Published on the session's EventBus each time a transfer changes state, so views can fade, walk or wheel the patient. Not part of the log.</summary>
    public readonly struct TransportChanged
    {
        public TransportChanged(string state, string to) { State = state; To = to; }
        public string State { get; }
        public string To { get; }
    }

    /// <summary>
    /// Where the patient and the trainee are, and how the patient moves between places: REQUESTED, DELAYED (porter unavailable), PREPARING, IN_TRANSIT, ARRIVED, DONE, RETURNING, BACK or CANCELLED, all on
    /// the simulation clock with delays drawn from the seeded "transport" stream (the same seed gives the same delays). It also refuses every action aimed at the patient while the patient is elsewhere
    /// (PATIENT_ELSEWHERE), checks the escort and monitoring against the patient's true NEWS2 when preparation starts, and arms the corridor event for an unstable patient with an inadequate escort.
    /// </summary>
    public sealed class TransportSubsystem : ISubsystem
    {
        public const string RequestVerb = "request_transport";
        public const string TheatreVerb = "transfer_to_theatre";
        public const string CancelVerb = "cancel_transport";
        public const string TheatreReception = "theatre_reception";
        public const string PatientElsewhere = "PATIENT_ELSEWHERE";
        public const string MissingDestination = "MISSING_DESTINATION";
        public const string UnknownLocation = "UNKNOWN_LOCATION";
        public const string AlreadyThere = "ALREADY_THERE";
        public const string NoRoute = "NO_ROUTE";
        public const string MissingMode = "MISSING_MODE";
        public const string UnknownMode = "UNKNOWN_MODE";
        public const string UnknownEscort = "UNKNOWN_ESCORT";
        public const string UnknownMonitoring = "UNKNOWN_MONITORING";
        public const string InProgress = "TRANSPORT_IN_PROGRESS";
        public const string NothingToCancel = "NO_TRANSPORT";
        public const string CannotCancel = "CANNOT_CANCEL";
        public const string PorterUnavailableFlag = "staff.porter_unavailable";
        public const string InadequateFlag = "transport.inadequate_escort";
        public const string UnsafeModeFlag = "transport.unsafe_mode";
        public const string CorridorEventFlag = "event.corridor_deterioration";

        private sealed class Request
        {
            public string From = "", To = "", Mode = "", Monitoring = "none", State = "";
            public HashSet<string> Escort = new HashSet<string>(StringComparer.Ordinal);
            public bool Oxygen, Accompany, Returns, Inadequate, Unstable, EventFired, Cancelled;
            public long StateEnd, TransitStart, EventStep = -1, TravelSteps, StaySteps, PrepSteps;
            public string Message = "";
        }

        private readonly ContentPackage _content;
        private readonly LocationGraph _graph;
        private readonly News2 _news2;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private Request? _r;
        private Session _s = null!;
        private SimRandom _rng = null!;

        public TransportSubsystem(ContentPackage content)
        {
            _content = content; _graph = new LocationGraph(content.Locations.Locations); _news2 = new News2(content.News2);
        }

        public int Order => PipelineOrder.Gate + 1;

        /// <summary>State changes, safety findings and the corridor event, with the sim step and score items. The scorer reads this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private ClinicalLayer Clinical => _s.Patient.Clinical;
        private LocationTables Tables => _content.Locations;

        public void Init(Session session)
        {
            _s = session;
            session.Transport = this;
            _rng = session.Random.Stream("transport");
            session.Verbs.Register(RequestVerb, requiresIdentity: true);
            session.Verbs.Register(TheatreVerb, requiresIdentity: true);
            session.Verbs.Register(CancelVerb);
        }

        // ---------------------------------------------------------------- actions

        public void OnAction(SimAction a, ActionOutcome o)
        {
            // this runs before the identity step, so a transfer's own identity rule is applied here
            if (_s.Verbs.RequiresIdentity(a.Verb) && !Clinical.IdentityVerified) { o.Reject(Workflow.Steps.IdentitySubsystem.IdentityNotConfirmed); return; }
            if (_s.Verbs.RequiresPatient(a.Verb) && Clinical.Location != Clinical.TraineeLocation)
            {
                o.Effects.Add("patient_in:" + Clinical.Location);
                o.Reject(PatientElsewhere);
                return;
            }
            switch (a.Verb)
            {
                case RequestVerb: Request_(a, o, null); break;
                case TheatreVerb: Request_(a, o, TheatreReception); break;
                case CancelVerb: Cancel(o); break;
            }
        }

        private void Request_(SimAction a, ActionOutcome o, string? fixedTo)
        {
            if (_r != null && _r.State != "BACK" && _r.State != "CANCELLED" && !(_r.State == "DONE" && !_r.Returns)) { o.Reject(InProgress); return; }
            string to = fixedTo ?? "";
            if (fixedTo == null && (!a.Params.TryGetValue("to", out to!) || to.Length == 0)) { o.Reject(MissingDestination); return; }
            if (!_graph.Has(to)) { o.Reject(UnknownLocation); return; }
            string from = Clinical.Location;
            if (from == to) { o.Reject(AlreadyThere); return; }
            var route = _graph.Shortest(from, to);
            if (route == null) { o.Reject(NoRoute); return; }
            if (!a.Params.TryGetValue("mode", out var mode) || mode.Length == 0) { o.Reject(MissingMode); return; }
            if (Tables.Modes.All(m => m.Id != mode)) { o.Reject(UnknownMode); return; }
            var escort = new HashSet<string>(StringComparer.Ordinal);
            if (a.Params.TryGetValue("escort", out var escortText))
                foreach (var part in escortText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Tables.EscortRoles.Contains(part)) { o.Reject(UnknownEscort); return; }
                    escort.Add(part);
                }
            string monitoring = a.Params.TryGetValue("monitoring", out var mon) && mon.Length > 0 ? mon : Tables.MonitoringLevels[0];
            if (!Tables.MonitoringLevels.Contains(monitoring)) { o.Reject(UnknownMonitoring); return; }

            var dest = Tables.Find(to)!;
            var r = new Request
            {
                From = from,
                To = to,
                Mode = mode,
                Escort = escort,
                Monitoring = monitoring,
                Oxygen = a.Params.TryGetValue("oxygen", out var ox) && ox == "true",
                Accompany = a.Params.TryGetValue("accompany", out var acc) && acc == "true",
                Returns = dest.Returns,
                TravelSteps = Steps(route.Minutes),
                StaySteps = Steps(dest.StayMinutes),
                PrepSteps = Steps(Tables.PrepareMinutes),
            };
            double arrival = Tables.PorterMin + _rng.NextDouble() * (Tables.PorterMax - Tables.PorterMin);
            bool unavailable = Clinical.HasFlag(PorterUnavailableFlag);
            if (unavailable)
            {
                Clinical.ClearFlag(PorterUnavailableFlag);
                arrival += Tables.PorterExtra;
                r.Message = Tables.PorterMessage.Replace("{minutes}", ((int)Math.Round(Tables.PorterExtra)).ToString(CultureInfo.InvariantCulture));
            }
            r.StateEnd = _s.Clock.Steps + Steps(arrival);
            _r = r;
            Enter(r, unavailable ? "DELAYED" : "REQUESTED");
            o.Effects.Add("transport:" + r.State);
            o.Effects.Add("transport_to:" + to);
            if (unavailable) o.Effects.Add("transport_delayed");
        }

        private void Cancel(ActionOutcome o)
        {
            var r = _r;
            if (r == null || r.State == "BACK" || r.State == "CANCELLED" || (r.State == "DONE" && !r.Returns)) { o.Reject(NothingToCancel); return; }
            switch (r.State)
            {
                case "REQUESTED":
                case "DELAYED":
                case "PREPARING":
                    Enter(r, "CANCELLED");
                    o.Effects.Add("transport:CANCELLED");
                    break;
                case "IN_TRANSIT":
                    r.Cancelled = true;
                    r.StateEnd = _s.Clock.Steps + Math.Max(1, _s.Clock.Steps - r.TransitStart);   // back the way it came, as long as it has already been travelling
                    Enter(r, "RETURNING");
                    o.Effects.Add("transport:RETURNING");
                    o.Effects.Add("transport_cancelled");
                    break;
                default:
                    o.Reject(CannotCancel);
                    break;
            }
        }

        // ---------------------------------------------------------------- time

        public void OnTick()
        {
            var r = _r;
            if (r == null) return;
            long now = _s.Clock.Steps;
            switch (r.State)
            {
                case "REQUESTED":
                case "DELAYED":
                    if (now >= r.StateEnd) { r.StateEnd = now + r.PrepSteps; Enter(r, "PREPARING"); Check(r); }
                    break;
                case "PREPARING":
                    if (now >= r.StateEnd)
                    {
                        r.TransitStart = now; r.StateEnd = now + r.TravelSteps;
                        if (r.Unstable && r.Inadequate) r.EventStep = now + (long)Math.Max(1, Math.Round(r.TravelSteps * Tables.Corridor.AtFraction));
                        Move("corridor", r.Accompany);
                        Enter(r, "IN_TRANSIT");
                    }
                    break;
                case "IN_TRANSIT":
                    if (!r.EventFired && r.EventStep >= 0 && now >= r.EventStep) Corridor(r);
                    if (now >= r.StateEnd) { r.StateEnd = now + r.StaySteps; Move(r.To, r.Accompany); Enter(r, "ARRIVED"); }
                    break;
                case "ARRIVED":
                    if (now >= r.StateEnd) { r.StateEnd = now + r.PrepSteps; Enter(r, "DONE"); }
                    break;
                case "DONE":
                    if (r.Returns && now >= r.StateEnd) { r.StateEnd = now + r.TravelSteps; Move("corridor", r.Accompany); Enter(r, "RETURNING"); }
                    break;
                case "RETURNING":
                    if (now >= r.StateEnd) { Move(r.From, r.Accompany); Enter(r, "BACK"); }
                    break;
            }
        }

        private void Move(string place, bool trainee)
        {
            Clinical.Location = place;
            if (trainee) Clinical.TraineeLocation = place;
        }

        private static long Steps(double minutes) => (long)Math.Round(minutes * 60 / SimClock.Step);

        // ---------------------------------------------------------------- safety at PREPARING (T-L3) and the corridor event (T-L7)

        private void Check(Request r)
        {
            var v = _s.Patient.Physiological.Vitals;
            var news = _news2.Score(new News2Input(v.RespiratoryRate, v.Spo2, v.SystolicBp, v.HeartRate, v.TemperatureC, v.Acvpu, v.Oxygen == "supplemental"));
            int total = news.Total;                                    // the patient's true state, not what the trainee has been told
            r.Unstable = total >= Tables.UnstableFromNews2;
            var tier = Tables.Tiers.First(t => t.Covers(total));
            int have = Array.IndexOf(Tables.MonitoringLevels.ToArray(), r.Monitoring), need = Array.IndexOf(Tables.MonitoringLevels.ToArray(), tier.Monitoring);
            r.Inadequate = !tier.Escort.All(r.Escort.Contains) || have < need || (tier.Oxygen && !r.Oxygen);
            if (r.Inadequate)
            {
                Clinical.SetFlag(InadequateFlag);
                _history.Add(new GraphEvent("transport_inadequate_escort", r.To, _s.Clock.Steps, new[] { "major.transfer_escort_inadequate" }));
            }
            var mode = Tables.Modes.First(m => m.Id == r.Mode);
            if (mode.NeedsStableS1 && !(_s.Case.Scenario == "S1" && !r.Unstable))
            {
                Clinical.SetFlag(UnsafeModeFlag);
                _history.Add(new GraphEvent("transport_unsafe_mode", r.To, _s.Clock.Steps, new[] { "major.unsafe_transport_mode" }));
            }
        }

        private void Corridor(Request r)
        {
            r.EventFired = true;
            var phys = _s.Patient.Physiological;
            phys.Vitals = phys.Vitals with { Spo2 = Tables.Corridor.Spo2 };
            Clinical.SetFlag(CorridorEventFlag);
            _history.Add(new GraphEvent("corridor_event", r.To, _s.Clock.Steps, Array.Empty<string>()));
            _s.Events.Publish(new WorkflowEvent(Tables.Corridor.Event, r.To));
        }

        // ---------------------------------------------------------------- the state and what the trainee sees

        private void Enter(Request r, string state)
        {
            r.State = state;
            _history.Add(new GraphEvent("transport:" + state, r.To, _s.Clock.Steps, Array.Empty<string>()));
            string arrives = "";
            if (state == "PREPARING") arrives = Clock(r.StateEnd + r.TravelSteps);
            else if (state == "IN_TRANSIT") arrives = Clock(r.StateEnd);
            Clinical.Transport = new TransportView(state, r.To, r.Mode, r.Escort.OrderBy(e => e, StringComparer.Ordinal).ToList(), r.Monitoring, r.Oxygen, arrives, state == "DELAYED" ? r.Message : "");
            _s.Events.Publish(new TransportChanged(state, r.To));
        }

        private string Clock(long step) => Observation.ObservableBuilder.Hms(_s.Clock.Start + TimeSpan.FromSeconds(step * SimClock.Step)).Substring(0, 5);
    }
}
