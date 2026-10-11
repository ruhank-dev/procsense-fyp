using System;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Random;

namespace ProcSense.Core.Physiology
{
    /// <summary>
    /// Runs the deterioration clock on the patient's vital signs for an emergency (S2) case that has a problem still untreated: a case with a timeline follows it, any other drifts by the table's rule.
    /// The first treatment that stops the clock (a fluid bolus or an antibiotic given by the medicine workflow) is noted; the patient then recovers to the treated values. Only the engine's truth changes: what the
    /// trainee sees still comes from what they measure.
    /// </summary>
    public sealed class DeteriorationSubsystem : ISubsystem
    {
        public const string TreatedFlag = "deterioration.treated";

        private const long UpdateEverySteps = 60;      // the readings are recomputed every 30 sim seconds

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly DeteriorationClock _clock;
        private CaseFile? _file;
        private CaseTimeline? _timeline;
        private bool _generic;
        private Session _s = null!;
        private SimRandom _rng = null!;
        private double? _treatedAtMinutes;
        private int _intervals;
        private double _hr;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public DeteriorationSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; _clock = new DeteriorationClock(content.Deterioration); }

        public int Order => PipelineOrder.Events - 3;

        /// <summary>True once a treatment that stops the clock has been given.</summary>
        public bool Treated => _treatedAtMinutes.HasValue;

        public void Init(Session session)
        {
            _s = session;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
            if (_file == null || _file.Scenario != "S2") return;
            if (_content.Deterioration.Timelines.TryGetValue(_file.Id, out var line)) _timeline = line;
            else _generic = _file.Problems.Any(p => p.Type == "life_threat" || p.Type == "time_critical");
            if (_timeline == null && !_generic) return;
            _rng = session.Random.Stream("deterioration");
            _hr = session.Patient.Physiological.Vitals.HeartRate;
            session.Events.Subscribe<TreatmentGiven>(OnGiven);
        }

        public void OnAction(SimAction a, ActionOutcome o) { }

        public void OnTick()
        {
            if (_timeline == null && !_generic) return;
            if (_s.Clock.Steps % UpdateEverySteps != 0) return;
            double minutes = _s.Clock.SimSeconds / 60.0;
            var phys = _s.Patient.Physiological;
            if (_timeline != null)
            {
                var v = _clock.At(_timeline, minutes, _treatedAtMinutes);
                phys.Vitals = phys.Vitals with { HeartRate = Math.Round(v.HeartRate), SystolicBp = Math.Round(v.Sbp), DiastolicBp = Math.Round(v.Dbp), RespiratoryRate = Math.Round(v.RespiratoryRate), Spo2 = Math.Round(v.Spo2), TemperatureC = Math.Round(v.TemperatureC, 1), Acvpu = v.Acvpu };
                return;
            }
            if (_treatedAtMinutes.HasValue) return;
            int due = (int)(minutes / _content.Deterioration.IntervalMinutes);
            while (_intervals < due)
            {
                _intervals++;
                _hr += _clock.HeartRateStep(_rng.NextInt);
                phys.Vitals = phys.Vitals with { HeartRate = _hr };
            }
        }

        private void OnGiven(TreatmentGiven e)
        {
            if (_treatedAtMinutes.HasValue) return;
            if (!e.Effects.Any(effect => _content.Deterioration.TreatedBy.Contains(effect))) return;
            _treatedAtMinutes = _s.Clock.SimSeconds / 60.0;
            _s.Patient.Clinical.SetFlag(TreatedFlag);
        }
    }
}
