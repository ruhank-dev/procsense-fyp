using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Physiology
{
    /// <summary>One display band: the zone it colours the monitor, and its range (an open end is null).</summary>
    public sealed record DisplayBand(string Zone, double? Low, double? High, bool HighExclusive, string Ref);

    /// <summary>How a probe goes wrong and what the monitor then shows: the reading drops and the waveform quality is poor until the probe is re-sited.</summary>
    public sealed record ProbeArtefact(IReadOnlyList<string> Problems, double ReadingDrop, string Quality, string Ref);

    /// <summary>
    /// The monitor's display zones (walkthrough Part 17: normal, watch, worry, emergency, and critical for oxygen below 88) and the probe artefact (content/physio/display_zones.json). They are separate from the NEWS2 scoring bands
    /// on purpose (WT-I04: temperature and heart rate bands differ), so NEWS2 never reads this table and this table never scores anything.
    /// </summary>
    public sealed class DisplayZoneTable
    {
        public static readonly DisplayZoneTable Empty = new DisplayZoneTable(new Dictionary<string, (int, IReadOnlyList<DisplayBand>)>(), new Dictionary<string, ProbeArtefact>());

        private readonly IReadOnlyDictionary<string, (int Digits, IReadOnlyList<DisplayBand> Bands)> _parameters;

        public DisplayZoneTable(IReadOnlyDictionary<string, (int Digits, IReadOnlyList<DisplayBand> Bands)> parameters, IReadOnlyDictionary<string, ProbeArtefact> artefacts) { _parameters = parameters; Artefacts = artefacts; }

        public IReadOnlyDictionary<string, ProbeArtefact> Artefacts { get; }

        public IEnumerable<string> Parameters => _parameters.Keys;

        /// <summary>The zone of a reading, or "" when the parameter has no bands or the value falls in none (the book lists no band for it). Readings are rounded to the precision the bands are written in.</summary>
        public string Zone(string parameter, double value)
        {
            if (!_parameters.TryGetValue(parameter, out var p)) return "";
            double v = Math.Round(value, p.Digits, MidpointRounding.AwayFromZero);
            foreach (var b in p.Bands)         // the rows are stored worst zone first
            {
                if (b.Low.HasValue && v < b.Low.Value) continue;
                if (b.High.HasValue && (b.HighExclusive ? v >= b.High.Value : v > b.High.Value)) continue;
                return b.Zone;
            }
            return "";
        }

        public static DisplayZoneTable Parse(string json, string source = "physio/display_zones")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var parameters = new Dictionary<string, (int, IReadOnlyList<DisplayBand>)>(StringComparer.Ordinal);
            var pj = root.Any("parameters") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("parameters") + ": expected an object");
            foreach (var kv in pj)
            {
                var o = new JsonObj(kv.Value, root.Sub("parameters") + "." + kv.Key);
                int digits = (int)o.Int("digits");
                var bands = new List<DisplayBand>();
                var rows = o.Arr("rows");
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = new JsonObj(rows[i], JsonObj.Index(o.Sub("rows"), i));
                    string zone = r.Enum("zone", "critical", "emergency", "worry", "watch", "normal");
                    double? low = Open(r, "low"), high = Open(r, "high");
                    if (low == null && high == null) throw new ContentException(r.Sub("low") + ": a band needs a low or a high end");
                    bands.Add(new DisplayBand(zone, low, high, r.Bool("high_exclusive"), r.Str("ref")));
                    r.Finish();
                }
                o.Finish();
                parameters[kv.Key] = (digits, bands);
            }
            var artefacts = new Dictionary<string, ProbeArtefact>(StringComparer.Ordinal);
            var aj = root.Any("artefacts") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("artefacts") + ": expected an object");
            foreach (var kv in aj)
            {
                var o = new JsonObj(kv.Value, root.Sub("artefacts") + "." + kv.Key);
                var problems = new List<string>();
                foreach (var x in o.Arr("problems")) problems.Add(x as string ?? throw new ContentException(o.Sub("problems") + ": expected text"));
                double drop = o.Num("reading_drop");
                if (drop <= 0) throw new ContentException(o.Sub("reading_drop") + ": must be positive");
                artefacts[kv.Key] = new ProbeArtefact(problems, drop, o.Str("quality"), o.Str("ref"));
                o.Str("note"); o.Finish();
            }
            foreach (var x in root.Arr("left_out")) if (!(x is string)) throw new ContentException(root.Sub("left_out") + ": expected text");
            foreach (var x in root.Arr("refs")) if (!(x is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new DisplayZoneTable(parameters, artefacts);
        }

        private static double? Open(JsonObj o, string key)
        {
            object? v = o.Any(key);
            if (v == null) return null;
            if (v is long l) return l;
            if (v is double d) return d;
            throw new ContentException(o.Sub(key) + ": expected a number or null");
        }
    }

    /// <summary>
    /// What pregnancy changes for a generated patient (content/physio/pregnancy.json): the resting heart rate is higher. Authored case files already include it in their values, so this is applied only
    /// when a patient is generated. NEWS2 is not used in pregnancy (the maternity score is the hospital's choice, a placeholder here). The book gives no white-cell figure, so none is applied.
    /// </summary>
    public sealed class PregnancyModifiers
    {
        public static readonly PregnancyModifiers Empty = new PregnancyModifiers(0, 0, 0);

        public PregnancyModifiers(double hrRiseMin, double hrRiseMax, double hrRiseUsed) { HrRiseMin = hrRiseMin; HrRiseMax = hrRiseMax; HrRiseUsed = hrRiseUsed; }

        public double HrRiseMin { get; }
        public double HrRiseMax { get; }
        public double HrRiseUsed { get; }

        /// <summary>The vital signs of a generated pregnant patient: the heart rate raised by the used amount.</summary>
        public Vitals Apply(Vitals v) => v with { HeartRate = v.HeartRate + HrRiseUsed };

        public static PregnancyModifiers Parse(string json, string source = "physio/pregnancy")
        {
            var o = new JsonObj(MiniJson.Parse(json), source);
            double min = o.Num("hr_rise_min"), max = o.Num("hr_rise_max"), used = o.Num("hr_rise_used");
            if (min < 0 || max < min || used < min || used > max) throw new ContentException(o.Sub("hr_rise_used") + ": needs min <= used <= max");
            if (o.Bool("news2_used")) throw new ContentException(o.Sub("news2_used") + ": NEWS2 is not used in pregnancy");
            o.Str("maternity_score"); o.Any("wbc"); o.Str("note"); o.Str("ref");
            o.Finish();
            return new PregnancyModifiers(min, max, used);
        }
    }
}
