using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Location;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Diagnostics
{
    /// <summary>One test's trip through the diagnostics loop: the sim step of each stage, -1 while it has not happened.</summary>
    public sealed class DiagnosticTrip
    {
        public const string Requested = "REQUESTED";
        public const string SampleWaiting = "SAMPLE_WAITING";
        public const string PatientAway = "PATIENT_AWAY";
        public const string SampleSent = "SAMPLE_SENT";
        public const string AtBedside = "AT_BEDSIDE";
        public const string ResultReady = "RESULT_READY";
        public const string PatientBack = "PATIENT_BACK";
        public const string ReportDelivered = "REPORT_DELIVERED";
        public const string Rejected = "REJECTED";

        public int OrderId { get; internal set; }
        public string Test { get; internal set; } = "";
        public string Kind { get; internal set; } = "";

        /// <summary>Where the test is done (a scan room for imaging; "" when the patient does not move).</summary>
        public string Destination { get; internal set; } = "";

        /// <summary>How the sample went to the laboratory ("" for imaging and bedside tests).</summary>
        public string Via { get; internal set; } = "";

        public string Stage { get; internal set; } = Requested;
        public long RequestedStep { get; internal set; } = -1;

        /// <summary>When a sample kept for the tube station or the porter was taken (-1 when it went at once).</summary>
        public long TakenStep { get; internal set; } = -1;

        /// <summary>True once the nurse has pointed out that this sample is still at the bedside.</summary>
        public bool Prompted { get; internal set; }
        public long DepartedStep { get; internal set; } = -1;
        public long SentStep { get; internal set; } = -1;
        public long ResultStep { get; internal set; } = -1;
        public long ReturnedStep { get; internal set; } = -1;
        public long DeliveredStep { get; internal set; } = -1;

        /// <summary>True while the patient must travel for this test and has not come back yet.</summary>
        public bool PatientTravels => Destination.Length > 0;

        internal bool Finished => Stage == ReportDelivered || Stage == Rejected;
    }

    /// <summary>
    /// The diagnostics loop in Core (p0-diagnostic-transitions-s01): InvestigationRequested, then PatientDeparted (imaging: the patient goes to the scan room) or SampleSent (a sample goes to the laboratory),
    /// time passes on the sim clock while physiology and events keep running, ResultReady, then PatientReturned and ReportDelivered. It only observes the investigations and transport steps and publishes
    /// these events for the views; it changes no state and no score, so the log and every hash stay as they were. While time is skipped it asks the session for attention when something needs the trainee
    /// now (the laboratory phones with a critical result, an event fires, the patient deteriorates in the corridor), so a skip stops before the trainee misses it.
    /// </summary>
    public sealed class DiagnosticsLoop : ISubsystem
    {
        public const string Porter = "porter";
        public const string WithPatient = "patient";
        public const string FromLaboratory = "laboratory";
        public const string AtTheBedside = "bedside";

        private readonly List<DiagnosticTrip> _trips = new List<DiagnosticTrip>();
        private readonly Dictionary<int, string> _seenState = new Dictionary<int, string>();
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<(DiagnosticTrip Trip, string With)> _toDeliver = new List<(DiagnosticTrip, string)>();
        private int _callsSeen;
        private bool _corridorSeen;
        private readonly DiagnosticsTables _tables;
        private Session _s = null!;

        public DiagnosticsLoop(ContentPackage? content = null) { _tables = content?.Diagnostics ?? DiagnosticsTables.Empty; }

        public int Order => PipelineOrder.Events + 5;

        /// <summary>Every test's trip, in the order the tests were ordered.</summary>
        public IReadOnlyList<DiagnosticTrip> Trips => _trips;

        /// <summary>The loop's stages with their sim steps (for tests and the debrief timeline; nothing here carries a score item).</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private ClinicalLayer Clinical => _s.Patient.Clinical;

        public void Init(Session session)
        {
            _s = session;
            session.DiagnosticsStep = this;
            session.Events.Subscribe<ResultReady>(OnResultReady);
            session.Events.Subscribe<TransportChanged>(OnTransport);
            session.Events.Subscribe<Events.EventFired>(e => _s.RequestAttention("event:" + e.Event));
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (o.Accepted) Scan();
            Flush();
        }

        public void OnTick()
        {
            Scan();
            Flush();
            if (Clinical.CriticalCalls.Count > _callsSeen)
            {
                for (int i = _callsSeen; i < Clinical.CriticalCalls.Count; i++) _s.RequestAttention("critical_call:" + Clinical.CriticalCalls[i].Test);
                _callsSeen = Clinical.CriticalCalls.Count;
            }
            if (!_corridorSeen && Clinical.HasFlag(TransportSubsystem.CorridorEventFlag))
            {
                _corridorSeen = true;
                _s.RequestAttention("corridor_deterioration");
            }
            Prompt();
        }

        // The beginner safety net (walkthrough Step 6): tubes left on the bed table are pointed out by the nurse after a while, in practice mode only, never in assessment.
        private void Prompt()
        {
            if (_tables.PromptAfterMinutes <= 0 || _s.Mode != SessionMode.Practice || (_tables.PromptBeginnerOnly && _s.Level != TrainingLevel.Beginner)) return;
            long wait = (long)Math.Round(_tables.PromptAfterMinutes * 60 / Time.SimClock.Step);
            for (int i = 0; i < _trips.Count; i++)
            {
                var trip = _trips[i];
                if (trip.Prompted || trip.Stage != DiagnosticTrip.SampleWaiting || _s.Clock.Steps - trip.TakenStep < wait) continue;
                trip.Prompted = true;
                _history.Add(new GraphEvent("diagnostics:" + trip.Test + ":NURSE_PROMPT", trip.Test, _s.Clock.Steps, Array.Empty<string>()));
                _s.Events.Publish(new SamplesWaiting(trip.OrderId, trip.Test, _tables.PromptText, _s.Clock.Steps));
                _s.RequestAttention("samples_waiting:" + trip.Test);
            }
        }

        // New orders and state changes of the investigations: a request, a sample leaving for the laboratory, a bedside test being done, a rejected sample.
        private void Scan()
        {
            var orders = Clinical.Investigations;
            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (!_seenState.TryGetValue(order.Id, out var before))
                {
                    _seenState[order.Id] = order.State;
                    Requested(order);
                    before = "REQUESTED";
                }
                if (before == order.State) continue;
                _seenState[order.Id] = order.State;
                var trip = Trip(order.Id);
                if (trip == null) continue;
                if (order.State == "PROCESSING" && before == "REQUESTED" && !trip.PatientTravels)
                {
                    if (order.Kind == "blood") Sent(trip, Porter);
                    else if (order.Kind == "bedside") Stage(trip, DiagnosticTrip.AtBedside);
                }
                else if (order.State == "SAMPLE_TAKEN")
                {
                    trip.TakenStep = _s.Clock.Steps;
                    Stage(trip, DiagnosticTrip.SampleWaiting);
                }
                else if (order.State == "SENT")
                {
                    Sent(trip, order.Sample.TryGetValue("via", out var via) ? via : Porter);
                }
                else if (order.State == "REJECTED")
                {
                    Stage(trip, DiagnosticTrip.Rejected);
                }
            }
        }

        private void Requested(InvestigationOrder order)
        {
            var trip = new DiagnosticTrip
            {
                OrderId = order.Id,
                Test = order.Test,
                Kind = order.Kind,
                Destination = order.Kind == "imaging" && Clinical.Location != order.Location ? order.Location : "",
                RequestedStep = _s.Clock.Steps,
            };
            _trips.Add(trip);
            _history.Add(new GraphEvent("diagnostics:" + order.Test + ":" + DiagnosticTrip.Requested, order.Test, trip.RequestedStep, Array.Empty<string>()));
            _s.Events.Publish(new InvestigationRequested(order.Id, order.Test, order.Kind, trip.RequestedStep));
        }

        private void Sent(DiagnosticTrip trip, string via)
        {
            trip.Via = via;
            trip.SentStep = _s.Clock.Steps;
            Stage(trip, DiagnosticTrip.SampleSent);
            _s.Events.Publish(new SampleSent(trip.OrderId, trip.Test, trip.Via, trip.SentStep));
        }

        // The transport step moves the patient; the trip waiting for that destination follows it.
        private void OnTransport(TransportChanged e)
        {
            if (e.State == "IN_TRANSIT")
            {
                var trip = _trips.FirstOrDefault(t => t.PatientTravels && t.Destination == e.To && t.DepartedStep < 0 && !t.Finished);
                if (trip == null) return;
                trip.DepartedStep = _s.Clock.Steps;
                Stage(trip, DiagnosticTrip.PatientAway);
                _s.Events.Publish(new PatientDeparted(trip.OrderId, trip.Test, trip.Destination, trip.DepartedStep));
            }
            else if (e.State == "BACK")
            {
                foreach (var trip in _trips.Where(t => t.PatientTravels && t.Destination == e.To && t.DepartedStep >= 0 && t.ReturnedStep < 0))
                {
                    trip.ReturnedStep = _s.Clock.Steps;
                    if (trip.Stage != DiagnosticTrip.Rejected) Stage(trip, DiagnosticTrip.PatientBack);
                    _s.Events.Publish(new PatientReturned(trip.OrderId, trip.Test, trip.ReturnedStep));
                    if (trip.ResultStep >= 0) Deliver(trip, WithPatient);
                }
            }
        }

        private void OnResultReady(ResultReady e)
        {
            var order = Clinical.Investigations.LastOrDefault(o => o.Test == e.Test && o.Instance == e.Instance);
            var trip = order == null ? null : Trip(order.Id);
            if (trip == null || trip.ResultStep >= 0) return;     // a test with several results publishes one ResultReady per result
            trip.ResultStep = _s.Clock.Steps;
            Stage(trip, DiagnosticTrip.ResultReady);
            if (!trip.PatientTravels) Deliver(trip, trip.Kind == "bedside" ? AtTheBedside : trip.Kind == "imaging" ? WithPatient : FromLaboratory);
            else if (trip.ReturnedStep >= 0) Deliver(trip, WithPatient);      // otherwise the report comes back with the patient
        }

        // A delivery found while another event is being handed out (ResultReady, TransportChanged) waits until the end of the step, so every view sees the result arrive before the report.
        private void Deliver(DiagnosticTrip trip, string with)
        {
            if (trip.DeliveredStep < 0 && !_toDeliver.Any(d => d.Trip == trip)) _toDeliver.Add((trip, with));
        }

        private void Flush()
        {
            for (int i = 0; i < _toDeliver.Count; i++) Delivered(_toDeliver[i].Trip, _toDeliver[i].With);
            _toDeliver.Clear();
        }

        private void Delivered(DiagnosticTrip trip, string with)
        {
            if (trip.DeliveredStep >= 0) return;
            trip.DeliveredStep = _s.Clock.Steps;
            Stage(trip, DiagnosticTrip.ReportDelivered);
            _s.Events.Publish(new ReportDelivered(trip.OrderId, trip.Test, with, trip.DeliveredStep));
        }

        private void Stage(DiagnosticTrip trip, string stage)
        {
            trip.Stage = stage;
            _history.Add(new GraphEvent("diagnostics:" + trip.Test + ":" + stage, trip.Test, _s.Clock.Steps, Array.Empty<string>()));
        }

        private DiagnosticTrip? Trip(int orderId) => _trips.FirstOrDefault(t => t.OrderId == orderId);
    }
}
