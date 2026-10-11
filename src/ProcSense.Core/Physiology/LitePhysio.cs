using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Random;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Physiology
{
    /// <summary>One variable of the model: its guard bounds (simulation guards, not clinical limits) and how fast it relaxes toward its target.</summary>
    public sealed record PhysioVariable(string Id, string Unit, double Min, double Max, double TauSeconds, string Source, string Ref);

    /// <summary>One coupling: the target moves by gain times how far the source is from its baseline.</summary>
    public sealed record PhysioCoupling(string Id, string Source, string Target, double Gain);

    /// <summary>A flag the behaviour model reads, switched on while its rule over the variables is true.</summary>
    public sealed record PhysioFlagRule(string Flag, Logic When, string Why, string Ref);

    /// <summary>Noise for one variable: AR(1) with this sigma and phi (from the VitalDB aggregates), never larger than the cap (below the uncaused-change band).</summary>
    public sealed record PhysioNoise(string Variable, double Sigma, double Phi, double UncausedBand, double Cap);

    /// <summary>How an effect builds and fades: nothing until Onset, a rise over Peak seconds, a plateau, then a fall over Offset seconds. All times in seconds.</summary>
    public sealed record PhysioProfile(double OnsetSeconds, double PeakSeconds, double PlateauSeconds, double OffsetSeconds)
    {
        public double Length => OnsetSeconds + PeakSeconds + PlateauSeconds + OffsetSeconds;

        /// <summary>0 to 1: how much of the effect is acting this long after it was applied.</summary>
        public double Factor(double age)
        {
            if (age < OnsetSeconds || age >= Length) return 0;
            double a = age - OnsetSeconds;
            if (a < PeakSeconds) return a / PeakSeconds;
            a -= PeakSeconds;
            if (a < PlateauSeconds) return 1;
            a -= PlateauSeconds;
            return OffsetSeconds <= 0 ? 0 : 1 - a / OffsetSeconds;
        }
    }

    /// <summary>A treatment or event nudging variables: the change each variable is pulled toward at full effect, and the time profile.</summary>
    public sealed record PhysioEffect(string Id, IReadOnlyDictionary<string, double> Targets, PhysioProfile Profile, string Source);

    /// <summary>What the engine shows at one moment: each variable's value, MAP worked out from them, and the flags that are on.</summary>
    public sealed record PhysioSnapshot(IReadOnlyDictionary<string, double> Values, IReadOnlyList<string> Flags);

    public interface IPhysiologyEngine
    {
        void Initialise(IReadOnlyDictionary<string, double> baseline, SimRandom random);
        void Apply(PhysioEffect effect);
        void Advance(double seconds);
        PhysioSnapshot Read();
    }

    /// <summary>The LitePhysio model (content/physio/model.json, couplings.json and noise.json).</summary>
    public sealed class PhysioTables
    {
        public static readonly PhysioTables Empty = new PhysioTables(new List<PhysioVariable>(), false, 1, new List<PhysioCoupling>(), new List<PhysioFlagRule>(), new List<PhysioNoise>());

        public PhysioTables(IReadOnlyList<PhysioVariable> variables, bool noiseEnabled, int noiseStepSeconds, IReadOnlyList<PhysioCoupling> couplings, IReadOnlyList<PhysioFlagRule> flags, IReadOnlyList<PhysioNoise> noise)
        {
            Variables = variables; NoiseEnabled = noiseEnabled; NoiseStepSeconds = noiseStepSeconds; Couplings = couplings; Flags = flags; Noise = noise;
        }

        public IReadOnlyList<PhysioVariable> Variables { get; }
        public bool NoiseEnabled { get; }
        public int NoiseStepSeconds { get; }
        public IReadOnlyList<PhysioCoupling> Couplings { get; }
        public IReadOnlyList<PhysioFlagRule> Flags { get; }
        public IReadOnlyList<PhysioNoise> Noise { get; }

        public PhysioVariable? Find(string id) => Variables.FirstOrDefault(v => v.Id == id);

        public static PhysioTables Parse(string modelJson, string couplingsJson, string noiseJson)
        {
            var model = new JsonObj(MiniJson.Parse(modelJson), "physio/model");
            var vars = new List<PhysioVariable>();
            var rawVars = model.Arr("variables");
            for (int i = 0; i < rawVars.Count; i++)
            {
                var o = new JsonObj(rawVars[i], JsonObj.Index(model.Sub("variables"), i));
                var v = new PhysioVariable(o.Str("id"), o.Str("unit"), o.Num("min"), o.Num("max"), o.Num("tau_seconds"), o.Str("source"), o.Str("ref"));
                if (vars.Any(x => x.Id == v.Id)) throw new ContentException(o.Sub("id") + ": duplicate variable '" + v.Id + "'");
                if (v.Max <= v.Min || v.TauSeconds <= 0) throw new ContentException(o.Sub("min") + ": needs min < max and a positive tau");
                vars.Add(v);
                o.Finish();
            }
            bool noiseOn = model.Bool("noise_enabled");
            int step = (int)model.Int("noise_step_seconds");
            if (step <= 0) throw new ContentException(model.Sub("noise_step_seconds") + ": must be positive");
            foreach (var r in model.Arr("refs")) if (!(r is string)) throw new ContentException(model.Sub("refs") + ": expected register ids");
            model.Finish();

            var cj = new JsonObj(MiniJson.Parse(couplingsJson), "physio/couplings");
            var couplings = new List<PhysioCoupling>();
            var rawC = cj.Arr("couplings");
            for (int i = 0; i < rawC.Count; i++)
            {
                var o = new JsonObj(rawC[i], JsonObj.Index(cj.Sub("couplings"), i));
                string src = o.Str("source"), tgt = o.Str("target");
                if (vars.All(v => v.Id != src) || vars.All(v => v.Id != tgt)) throw new ContentException(o.Sub("source") + ": a coupling names a variable the model does not have");
                couplings.Add(new PhysioCoupling(o.Str("id"), src, tgt, o.Num("gain")));
                o.Str("note"); o.Finish();
            }
            var flags = new List<PhysioFlagRule>();
            var rawF = cj.Arr("flags");
            for (int i = 0; i < rawF.Count; i++)
            {
                var o = new JsonObj(rawF[i], JsonObj.Index(cj.Sub("flags"), i));
                flags.Add(new PhysioFlagRule(o.Str("id"), Logic.Compile(o.Any("when"), o.Sub("when")), o.Str("why"), o.Str("ref")));
                o.Finish();
            }
            foreach (var r in cj.Arr("refs")) if (!(r is string)) throw new ContentException(cj.Sub("refs") + ": expected register ids");
            cj.Finish();

            var nj = new JsonObj(MiniJson.Parse(noiseJson), "physio/noise");
            var noise = new List<PhysioNoise>();
            var rawN = nj.Arr("noise");
            for (int i = 0; i < rawN.Count; i++)
            {
                var o = new JsonObj(rawN[i], JsonObj.Index(nj.Sub("noise"), i));
                string variable = o.Str("variable");
                if (vars.All(v => v.Id != variable)) throw new ContentException(o.Sub("variable") + ": '" + variable + "' is not a variable");
                double phi = o.Num("phi");
                if (phi < 0 || phi >= 1) throw new ContentException(o.Sub("phi") + ": needs 0 <= phi < 1");
                noise.Add(new PhysioNoise(variable, o.Num("sigma"), phi, o.Num("uncaused_band"), o.Num("cap")));
                o.Str("ref"); o.Str("cap_ref"); o.Finish();
            }
            foreach (var r in nj.Arr("refs")) if (!(r is string)) throw new ContentException(nj.Sub("refs") + ": expected register ids");
            nj.Finish();
            return new PhysioTables(vars, noiseOn, step, couplings, flags, noise);
        }
    }

    /// <summary>
    /// LitePhysio: each variable relaxes toward its target (baseline plus every active effect times its profile, plus the couplings) by x += (target - x) * dt / tau, and is held inside its guard bounds.
    /// Noise is optional (off by default): a seeded AR(1) per variable, never larger than its cap, added to what is read but never to the state. Everything is deterministic for a seed.
    /// </summary>
    public sealed class LitePhysio : IPhysiologyEngine
    {
        private readonly PhysioTables _t;
        private readonly Dictionary<string, double> _baseline = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _state = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _noise = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<(PhysioEffect Effect, double Start)> _effects = new List<(PhysioEffect, double)>();
        private SimRandom _rng = null!;
        private double _time;
        private double _noiseDue;
        private bool _moved;

        public LitePhysio(PhysioTables tables) { _t = tables; NoiseEnabled = tables.NoiseEnabled; }

        /// <summary>Whether Read adds noise (the content default is off; a session switches it on when the goldens and tests are ready for flicker).</summary>
        public bool NoiseEnabled { get; set; }

        /// <summary>True while anything is moving the patient: an effect, noise, or a variable away from its baseline.</summary>
        public bool Active => _effects.Count > 0 || NoiseEnabled || _moved;

        public void Initialise(IReadOnlyDictionary<string, double> baseline, SimRandom random)
        {
            _rng = random;
            foreach (var v in _t.Variables)
            {
                double b = baseline.TryGetValue(v.Id, out var given) ? given : 0;
                _baseline[v.Id] = _state[v.Id] = Clamp(v, b);
                _noise[v.Id] = 0;
            }
        }

        /// <summary>Changes a variable's baseline (what it would settle at with nothing acting on it).</summary>
        public void SetBaseline(string id, double value)
        {
            var v = _t.Find(id) ?? throw new ArgumentException("unknown variable '" + id + "'", nameof(id));
            _baseline[id] = Clamp(v, value);
            _moved = true;
        }

        public void Apply(PhysioEffect effect)
        {
            foreach (var key in effect.Targets.Keys)
                if (_t.Find(key) == null) throw new ArgumentException("effect " + effect.Id + " names the variable '" + key + "', which the model does not have", nameof(effect));
            _effects.Add((effect, _time));
        }

        public void Advance(double seconds)
        {
            if (seconds <= 0) return;
            _time += seconds;
            _effects.RemoveAll(e => _time - e.Start >= e.Effect.Profile.Length);
            var targets = new Dictionary<string, double>(_baseline, StringComparer.Ordinal);
            foreach (var (effect, start) in _effects)
            {
                double f = effect.Profile.Factor(_time - start);
                foreach (var kv in effect.Targets) targets[kv.Key] += kv.Value * f;
            }
            foreach (var c in _t.Couplings) targets[c.Target] += c.Gain * (_state[c.Source] - _baseline[c.Source]);
            foreach (var v in _t.Variables)
            {
                double step = Math.Min(1, seconds / v.TauSeconds);
                _state[v.Id] = Clamp(v, _state[v.Id] + (Clamp(v, targets[v.Id]) - _state[v.Id]) * step);
            }
            _moved = false;
            foreach (var v in _t.Variables) if (Math.Abs(_state[v.Id] - _baseline[v.Id]) > 1e-9) { _moved = true; break; }
            if (NoiseEnabled) StepNoise();
        }

        private void StepNoise()
        {
            while (_noiseDue <= _time)
            {
                _noiseDue += _t.NoiseStepSeconds;
                foreach (var n in _t.Noise)
                {
                    double e = n.Sigma * Math.Sqrt(1 - n.Phi * n.Phi) * Gaussian();
                    _noise[n.Variable] = Math.Max(-n.Cap, Math.Min(n.Cap, n.Phi * _noise[n.Variable] + e));
                }
            }
        }

        // Box-Muller from the seeded stream
        private double Gaussian()
        {
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        public PhysioSnapshot Read()
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var v in _t.Variables) values[v.Id] = Clamp(v, _state[v.Id] + (NoiseEnabled ? _noise[v.Id] : 0));
            if (values.TryGetValue("SBP", out var sbp) && values.TryGetValue("DBP", out var dbp)) values["MAP"] = (sbp + 2 * dbp) / 3;
            // flags read the clean state, so noise never flips them
            var data = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var v in _t.Variables) data[v.Id] = _state[v.Id];
            var flags = new List<string>();
            foreach (var rule in _t.Flags) if (!flags.Contains(rule.Flag) && rule.When.Test(data)) flags.Add(rule.Flag);
            return new PhysioSnapshot(values, flags);
        }

        private static double Clamp(PhysioVariable v, double x) => Math.Max(v.Min, Math.Min(v.Max, x));
    }
}
