using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Physiology
{
    /// <summary>
    /// Runs the LitePhysio engine for the session: it starts from the case's vital signs (and the capillary glucose the case states, if any), moves forward with the sim clock and, while an effect or noise is acting, writes
    /// the result to the patient's vital signs; with nothing acting the vitals are left exactly as the case gives them. The flags the behaviour model reads (confusion from low oxygen or low sugar) are kept as session flags
    /// physio.&lt;flag&gt;, worked out from the live vital signs and glucose so they follow the deterioration clock as well.
    /// </summary>
    public sealed class PhysiologySubsystem : ISubsystem
    {
        public const string FlagPrefix = "physio.";
        private const long UpdateEverySteps = 60;      // flags and written vitals are refreshed every 30 sim seconds

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private LitePhysio _engine = null!;
        private CaseFile? _file;
        private Session _s = null!;
        private double _lastSpo2;
        private bool _seen;
        private string _lastAcvpu = "";
        private readonly HashSet<string> _on = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public PhysiologySubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        public int Order => PipelineOrder.Clinical + 5;

        public LitePhysio Engine => _engine;

        /// <summary>The glucose the case states (mmol/L), or null.</summary>
        public double? Glucose { get; private set; }

        public void Init(Session session)
        {
            _s = session;
            session.PhysiologyStep = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
            var v = session.Patient.Physiological.Vitals;
            var baseline = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["HR"] = v.HeartRate,
                ["SBP"] = v.SystolicBp,
                ["DBP"] = v.DiastolicBp,
                ["RR"] = v.RespiratoryRate,
                ["SpO2"] = v.Spo2,
                ["Temp"] = v.TemperatureC,
                ["pain"] = v.PainScore,
            };
            var glucose = _file?.Results.FirstOrDefault(r => r.Test.StartsWith("Capillary glucose", StringComparison.Ordinal) && !r.Test.Contains("repeat"));
            if (glucose != null && double.TryParse(glucose.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double g)) { Glucose = g; baseline["glucose"] = g; }
            _engine = new LitePhysio(_content.Physio);
            _engine.Initialise(baseline, session.Random.Stream("physio"));
            Refresh();
        }

        public void OnAction(SimAction a, ActionOutcome o) { }

        public void OnTick()
        {
            if (_engine.Active)
            {
                _engine.Advance(Time.SimClock.Step);
                if (_s.Clock.Steps % UpdateEverySteps == 0) WriteVitals();
            }
            if (_s.Clock.Steps % UpdateEverySteps == 0) Refresh();
        }

        /// <summary>A treatment or event nudges the patient (the effects of treatments come with the treatment-effects step).</summary>
        public void Apply(PhysioEffect effect) => _engine.Apply(effect);

        private void WriteVitals()
        {
            var s = _engine.Read().Values;
            var phys = _s.Patient.Physiological;
            phys.Vitals = phys.Vitals with { HeartRate = Math.Round(s["HR"]), SystolicBp = Math.Round(s["SBP"]), DiastolicBp = Math.Round(s["DBP"]), RespiratoryRate = Math.Round(s["RR"]), Spo2 = Math.Round(s["SpO2"]), TemperatureC = Math.Round(s["Temp"], 1) };
        }

        // The flags are only worked out again when oxygen, sugar or consciousness has changed, so a quiet session costs nothing.
        private void Refresh()
        {
            var v = _s.Patient.Physiological.Vitals;
            if (_seen && v.Spo2 == _lastSpo2 && v.Acvpu == _lastAcvpu) return;                   // the glucose is fixed for the session, so only oxygen and consciousness can change
            _seen = true;
            _lastSpo2 = v.Spo2; _lastAcvpu = v.Acvpu;
            Recompute(v);
        }

        // kept apart so the lambda below (and the object that holds what it captures) is only made when something changed
        private void Recompute(Content.Vitals v)
        {
            var data = new Dictionary<string, object?>(StringComparer.Ordinal) { ["SpO2"] = v.Spo2 };
            if (Glucose.HasValue) data["glucose"] = Glucose.Value;
            var now = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in _content.Physio.Flags) if (rule.When.Test(data)) now.Add(rule.Flag);
            if (v.Acvpu != "A") now.Add("confusion");                                          // the case's own consciousness level (new confusion, drowsiness or worse)
            foreach (var f in now) if (_on.Add(f)) _s.Patient.Clinical.SetFlag(FlagPrefix + f);
            foreach (var f in _on.Where(x => !now.Contains(x)).ToList()) { _on.Remove(f); _s.Patient.Clinical.ClearFlag(FlagPrefix + f); }
        }
    }
}
