using System;
using System.Collections.Generic;
using ProcSense.Core.Random;

namespace ProcSense.Core.Physiology
{
    /// <summary>What the session's physiology subsystem needs from an engine beyond IPhysiologyEngine: whether anything is moving the patient, and the baselines the deterioration drivers move.</summary>
    public interface ISessionPhysiology : IPhysiologyEngine
    {
        bool Active { get; }
        double GetBaseline(string id);
        void SetBaseline(string id, double value);
    }

    /// <summary>
    /// The adapter for the Pulse Physiology Engine behind IPhysiologyEngine (p1-pulse-physiology-s02). It replays a RECORDED Pulse run (PulseTrace), not a live one, so a session stays deterministic and a saved session
    /// verifies on any machine. How it behaves:
    ///  * Until the effect that starts the recording is applied (the first action of the trace, for example EFF-ADRENALINE), the patient stays at the case's baseline vital signs.
    ///  * When that effect is applied, the recording starts: the vital signs are the case's baseline plus how far Pulse's values have moved from where its own run started, so each case keeps its own starting point
    ///    and only the CHANGE comes from Pulse. A variable the trace does not carry is left alone.
    ///  * Effects other than the starting one are not re-timed: the recording already contains the later actions at the times they were recorded. Every effect that arrives and is not the starting one is counted in
    ///    Ignored, so a test or a log can say what Pulse did not see. It is a recorded scenario, not an interactive patient; an interactive Pulse would have to run beside the session (a server-side bridge) and log its
    ///    values, which is future work and not needed for replay.
    ///  * Variables the Core carries and Pulse does not (pain, nausea, sedation, glucose, volume_deficit) stay at their baselines.
    /// </summary>
    public sealed class PulseTraceEngine : ISessionPhysiology
    {
        private readonly PhysioTables _tables;
        private readonly PulseTrace _trace;
        private readonly Dictionary<string, double> _baseline = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<string> _ignored = new List<string>();
        private double _time;
        private double _startedAt = double.NaN;

        public PulseTraceEngine(PhysioTables tables, PulseTrace trace) { _tables = tables; _trace = trace; }

        public PulseTrace Trace => _trace;

        /// <summary>Ids of effects that were applied and are not the one that starts the recording.</summary>
        public IReadOnlyList<string> Ignored => _ignored;

        public bool Started => !double.IsNaN(_startedAt);

        public bool Active => Started && _time - _startedAt < _trace.Duration + 1;

        public void Initialise(IReadOnlyDictionary<string, double> baseline, SimRandom random)
        {
            foreach (var v in _tables.Variables) _baseline[v.Id] = baseline.TryGetValue(v.Id, out var b) ? b : 0;
        }

        public double GetBaseline(string id) => _baseline[id];

        public void SetBaseline(string id, double value) { _baseline[id] = value; }          // the deterioration drivers move baselines; the recording does not follow them

        public void Apply(PhysioEffect effect)
        {
            if (!Started && effect.Id == _trace.Anchor.Key) _startedAt = _time - _trace.Anchor.T;
            else _ignored.Add(effect.Id);
        }

        public void Advance(double seconds) { if (seconds > 0) _time += seconds; }

        public PhysioSnapshot Read()
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            double t = Started ? _time - _startedAt : 0;
            foreach (var v in _tables.Variables)
            {
                double x = _baseline[v.Id];
                if (Started && Array.IndexOf(PulseTrace.Variables, v.Id) >= 0) x += _trace.At(v.Id, t) - _trace.At(v.Id, 0);
                values[v.Id] = Math.Max(v.Min, Math.Min(v.Max, x));
            }
            if (values.TryGetValue("SBP", out var sbp) && values.TryGetValue("DBP", out var dbp)) values["MAP"] = (sbp + 2 * dbp) / 3;
            var data = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in values) data[kv.Key] = kv.Value;
            var flags = new List<string>();
            foreach (var rule in _tables.Flags) if (!flags.Contains(rule.Flag) && rule.When.Test(data)) flags.Add(rule.Flag);
            return new PhysioSnapshot(values, flags);
        }
    }
}
