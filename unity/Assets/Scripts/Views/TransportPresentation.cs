using System;
using System.Linq;
using ProcSense.Core.Observation;

namespace ProcSense.VR.Views
{
    public enum TransportVisualPhase { Bedside, Outbound, Inside, Returning, Unavailable }

    /// <summary>Read-only projection of Core transport. No clock, commands or clinical decisions.</summary>
    public sealed class TransportPresentation
    {
        public TransportVisualPhase Phase { get; private set; }
        public string Destination { get; private set; } = "";
        public string Mode { get; private set; } = "";
        public string Stage { get; private set; } = "";
        public bool Porter { get; private set; }
        public bool Nurse { get; private set; }
        public bool Doctor { get; private set; }
        public string MonitoringLevel { get; private set; }="";
        public bool Monitor { get; private set; }
        public bool Oxygen { get; private set; }
        public bool BedsideVisible { get; private set; } = true;
        public bool ActorVisible => Phase == TransportVisualPhase.Outbound || Phase == TransportVisualPhase.Returning;
        public string TraineeLocation { get; private set; } = "";
        public string Behaviour { get; private set; } = "";
        private object _session;

        public void Observe(ObservableState state, object session)
        {
            if (!ReferenceEquals(_session, session)) { Clear(); _session = session; }
            if (session == null || state == null) { Clear(); return; }
            var trip = state.Transport;
            Destination = trip?.To ?? "";
            Mode = trip?.Mode ?? "";
            Stage = trip?.State ?? "";
            Porter = trip?.Escort?.Contains("porter") == true;
            Nurse = trip?.Escort?.Contains("nurse") == true;
            Monitor = trip != null && trip.Monitoring != "none" && !string.IsNullOrEmpty(trip.Monitoring);
            Oxygen = trip?.Oxygen == true;
            Doctor = trip?.Escort?.Contains("doctor") == true; MonitoringLevel=trip?.Monitoring??"";
            TraineeLocation = state.TraineeLocation ?? "";
            Behaviour = state.BehaviourMode ?? "";
            var phase = Stage switch
            {
                "" or "REQUESTED" or "DELAYED" or "PREPARING" or "BACK" or "CANCELLED" => TransportVisualPhase.Bedside,
                "IN_TRANSIT" => TransportVisualPhase.Outbound,
                "ARRIVED" or "DONE" => TransportVisualPhase.Inside,
                "RETURNING" => TransportVisualPhase.Returning,
                _ => TransportVisualPhase.Unavailable
            };
            if (trip != null && Mode != "walk" && Mode != "wheelchair" && Mode != "trolley")
                phase = TransportVisualPhase.Unavailable;
            Phase = phase;
            BedsideVisible = state.Location == "patient_bay" && phase != TransportVisualPhase.Outbound && phase != TransportVisualPhase.Returning;
        }

        public void Clear()
        {
            Phase = TransportVisualPhase.Bedside; Destination = Mode = Stage = TraineeLocation = "";
            Porter = Nurse = Doctor = Monitor = Oxygen = false; MonitoringLevel=""; BedsideVisible = true; Behaviour = "";
            _session = null;
        }
    }
}
