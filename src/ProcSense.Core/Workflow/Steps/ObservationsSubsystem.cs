using System;
using System.Globalization;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Random;
using ProcSense.Core.State;
using ProcSense.Core.Time;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Walkthrough Step 3: sensors and vital signs. The monitor is blank until a sensor is attached; every other value is a reading the
    /// trainee takes. Values come from the patient's current vitals (the case baseline until physiology exists). Not gated on identity.
    /// shortcut: temperature, respiratory-rate count, ACVPU and pain score are instant readings with no duration; add durations when a
    /// content value exists for them.
    /// </summary>
    public sealed class ObservationsSubsystem : ISubsystem
    {
        public const string Spo2Probe = "spo2";
        public const string NibpCuff = "nibp_cuff";

        public const string MissingSensor = "MISSING_SENSOR";
        public const string UnknownSensor = "UNKNOWN_SENSOR";
        public const string ProbeNotAttached = "PROBE_NOT_ATTACHED";
        public const string CuffNotAttached = "CUFF_NOT_ATTACHED";
        public const string CycleInProgress = "CYCLE_IN_PROGRESS";
        public const string PulseSecondsOutOfRange = "PULSE_SECONDS_OUT_OF_RANGE";
        public const string PulseInProgress = "PULSE_IN_PROGRESS";
        public const string PalpatePulseVerb = "palpate_pulse";
        /// <summary>The walkthrough (Part E, heart rate): feel the radial pulse for 30 to 60 seconds; irregularity needs palpation or an ECG.</summary>
        public const int PulseMinSeconds = 30, PulseMaxSeconds = 60;

        private readonly ContentPackage _content;
        private Session _s = null!;
        private SimRandom _devices = null!;
        private long? _nibpEndStep; // sim step at which the running cuff cycle finishes
        private long? _pulseEndStep; // sim step at which the pulse being felt is read

        public ObservationsSubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Clinical;

        private PhysiologicalLayer Physio => _s.Patient.Physiological;

        public void Init(Session session)
        {
            _s = session;
            _devices = session.Random.Stream("devices");
            foreach (var v in new[] { "attach_sensor", "detach_sensor", "measure_nibp", "measure_temperature", "count_rr", "assess_acvpu", "ask_pain_score", "resite_probe", PalpatePulseVerb, "wait" })
                session.Verbs.Register(v, requiresPatient: v != "wait");
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case "attach_sensor": Attach(a, o); break;
                case "detach_sensor": Detach(a, o); break;
                case "resite_probe": Resite(o); break;
                case "measure_nibp": MeasureNibp(o); break;
                case PalpatePulseVerb: PalpatePulse(a, o); break;
                case "measure_temperature": Record("temperature_c", ObservableBuilder.Num(Physio.Vitals.TemperatureC), o); break;
                case "count_rr": Record("respiratory_rate", ObservableBuilder.Num(Physio.Vitals.RespiratoryRate), o); break;
                case "assess_acvpu": Record("acvpu", Physio.Vitals.Acvpu, o); break;
                case "ask_pain_score": Record("pain_score", Physio.Vitals.PainScore.ToString(CultureInfo.InvariantCulture), o); break;
                case "wait": o.Effects.Add("waited"); break; // time passes before the action runs; nothing else happens
            }
        }

        // The cuff cycle finishes on the sim clock, so a replayed session finishes it at exactly the same step.
        public void OnTick()
        {
            if (_pulseEndStep is long pulseEnd && _s.Clock.Steps >= pulseEnd)
            {
                Physio.Record("heart_rate", ObservableBuilder.Num(Physio.Vitals.HeartRate), pulseEnd);
                Physio.Record("heart_rhythm", Physio.Vitals.HeartRhythm, pulseEnd);
                _pulseEndStep = null;
            }
            if (_nibpEndStep is long end && _s.Clock.Steps >= end)
            {
                Physio.Record("systolic_bp", ObservableBuilder.Num(Physio.Vitals.SystolicBp), end);
                Physio.Record("diastolic_bp", ObservableBuilder.Num(Physio.Vitals.DiastolicBp), end);
                _nibpEndStep = null;
            }
        }

        private bool TryGetSensor(SimAction a, ActionOutcome o, out string sensor)
        {
            if (!a.Params.TryGetValue("sensor", out sensor!)) { o.Reject(MissingSensor); return false; }
            if (sensor != Spo2Probe && sensor != NibpCuff) { o.Reject(UnknownSensor); return false; }
            return true;
        }

        private void Attach(SimAction a, ActionOutcome o)
        {
            if (!TryGetSensor(a, o, out var sensor)) return;
            o.Effects.Add(Physio.Attach(sensor) ? "attached:" + sensor : "already_attached:" + sensor);
        }

        // Checking the probe (warm finger, no nail polish, no movement) and re-siting it clears a poor signal; with nothing wrong it just moves the probe.
        private void Resite(ActionOutcome o)
        {
            if (!Physio.IsAttached(Spo2Probe)) { o.Reject(ProbeNotAttached); return; }
            bool fixedSignal = Physio.ProbeProblem.Length > 0;
            Physio.ProbeProblem = "";
            o.Effects.Add(fixedSignal ? "probe_resited:signal_restored" : "probe_resited");
        }

        private void Detach(SimAction a, ActionOutcome o)
        {
            if (!TryGetSensor(a, o, out var sensor)) return;
            if (!Physio.Detach(sensor)) { o.Effects.Add("not_attached:" + sensor); return; }
            if (sensor == NibpCuff) _nibpEndStep = null; // taking the cuff off cancels a running cycle
            o.Effects.Add("detached:" + sensor);
        }

        // Cycle length is drawn once per cycle from the content range (walkthrough Part E, issue ISS-0001 closed) on the seeded "devices" stream.
        private void MeasureNibp(ActionOutcome o)
        {
            if (!Physio.IsAttached(NibpCuff)) { o.Reject(CuffNotAttached); return; }
            if (_nibpEndStep != null) { o.Reject(CycleInProgress); return; }
            var n = _content.Nibp;
            double seconds = n.CycleSecondsMin + _devices.NextDouble() * (n.CycleSecondsMax - n.CycleSecondsMin);
            long steps = (long)Math.Round(seconds / SimClock.Step);
            _nibpEndStep = _s.Clock.Steps + steps;
            o.Effects.Add("nibp_cycle_started:" + steps.ToString(CultureInfo.InvariantCulture) + "_steps");
        }

        // Feeling the wrist pulse takes the seconds the trainee chooses (30 to 60, the walkthrough's range); it shows the rate and, unlike the finger clip, whether the rhythm is regular (DT-040).
        private void PalpatePulse(SimAction a, ActionOutcome o)
        {
            int seconds = PulseMinSeconds;
            if (a.Params.TryGetValue("seconds", out var text) && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)) { o.Reject(PulseSecondsOutOfRange); return; }
            if (seconds < PulseMinSeconds || seconds > PulseMaxSeconds) { o.Reject(PulseSecondsOutOfRange); return; }
            if (_pulseEndStep != null) { o.Reject(PulseInProgress); return; }
            _pulseEndStep = _s.Clock.Steps + (long)Math.Round(seconds / SimClock.Step);
            o.Effects.Add("pulse_started:" + seconds.ToString(CultureInfo.InvariantCulture) + "_s");
        }

        private void Record(string name, string value, ActionOutcome o)
        {
            Physio.Record(name, value, _s.Clock.Steps);
            o.Effects.Add("recorded:" + name);
        }
    }
}
