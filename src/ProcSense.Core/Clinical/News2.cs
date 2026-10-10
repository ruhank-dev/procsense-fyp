using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>One cell of the NEWS2 chart: the points for a reading that falls in [Min, Max] (a missing limit is open). Condition says when it applies (SpO2 scale 2 only): any, on_air or on_oxygen.</summary>
    public sealed record News2Cell(double? Min, double? Max, int Points, string Condition, string Ref)
    {
        public bool Covers(double v) => (!Min.HasValue || v >= Min.Value) && (!Max.HasValue || v <= Max.Value);
    }

    /// <summary>One row of "what to do for this total": the total range (Single3 has none: it is judged from the parts), the risk, how often to check and who responds.</summary>
    public sealed record News2Band(string Id, int? Min, int? Max, string Risk, string Frequency, string Responder, string Ref);

    /// <summary>The NEWS2 chart and response table, loaded from content/news2/news2.json (built from the verified tables); no number is in code.</summary>
    public sealed class News2Table
    {
        public static readonly string[] Order = { "respiratory_rate", "spo2", "supplemental_oxygen", "systolic_bp", "heart_rate", "acvpu", "temperature_c" };

        public News2Table(IReadOnlyDictionary<string, IReadOnlyList<News2Cell>> cells, IReadOnlyList<News2Cell> spo2Scale2, int oxygenAir, int oxygenYes, int acvpuAlert, int acvpuOther,
            IReadOnlyDictionary<string, int> precision, IReadOnlyList<News2Band> response)
        {
            Cells = cells; Spo2Scale2 = spo2Scale2; OxygenAir = oxygenAir; OxygenYes = oxygenYes; AcvpuAlert = acvpuAlert; AcvpuOther = acvpuOther; Precision = precision; Response = response;
        }

        public static readonly News2Table Empty = new News2Table(new Dictionary<string, IReadOnlyList<News2Cell>>(), new List<News2Cell>(), 0, 0, 0, 0, new Dictionary<string, int>(), new List<News2Band>());

        public IReadOnlyDictionary<string, IReadOnlyList<News2Cell>> Cells { get; }
        public IReadOnlyList<News2Cell> Spo2Scale2 { get; }
        public int OxygenAir { get; }
        public int OxygenYes { get; }
        public int AcvpuAlert { get; }
        public int AcvpuOther { get; }
        /// <summary>Decimals of each parameter in the table; a reading is rounded to this before it is scored.</summary>
        public IReadOnlyDictionary<string, int> Precision { get; }
        public IReadOnlyList<News2Band> Response { get; }

        public static News2Table Parse(string json, string source = "news2")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var parameters = root.Obj("parameters");
            var cells = new Dictionary<string, IReadOnlyList<News2Cell>>(StringComparer.Ordinal);
            foreach (var p in new[] { "respiratory_rate", "spo2", "systolic_bp", "heart_rate", "temperature_c" })
                cells[p] = Cells_(parameters, p, false);
            parameters.Finish();
            var scale2 = Cells_(root, "spo2_scale2", true);
            var ox = root.Obj("supplemental_oxygen");
            int air = (int)ox.Int("air"), yes = (int)ox.Int("oxygen"); Refs(ox); ox.Finish();
            var ac = root.Obj("acvpu");
            int alert = (int)ac.Int("alert"), other = (int)ac.Int("other"); Refs(ac); ac.Finish();
            var pr = root.Obj("precision");
            var precision = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var p in cells.Keys) precision[p] = (int)pr.Int(p);
            pr.Finish();
            var response = new List<News2Band>();
            var raw = root.Arr("response");
            for (int i = 0; i < raw.Count; i++)
            {
                var b = new JsonObj(raw[i], JsonObj.Index(root.Sub("response"), i));
                response.Add(new News2Band(b.Str("id"), b.Has("min") ? (int)b.Int("min") : (int?)null, b.Has("max") ? (int)b.Int("max") : (int?)null, b.Str("risk"), b.Str("frequency"), b.Str("responder"), b.Str("ref")));
                b.Finish();
            }
            root.Finish();
            foreach (var id in new[] { "0", "1-4", "single3", "5-6", "7+" })
                if (response.All(b => b.Id != id)) throw new ContentException(root.Sub("response") + ": the band '" + id + "' is missing");
            return new News2Table(cells, scale2, air, yes, alert, other, precision, response);
        }

        private static void Refs(JsonObj o) { foreach (var r in o.Arr("refs")) if (!(r is string)) throw new ContentException(o.Sub("refs") + ": expected register ids"); }

        private static List<News2Cell> Cells_(JsonObj o, string key, bool withCondition)
        {
            var raw = o.Arr(key);
            if (raw.Count == 0) throw new ContentException(o.Sub(key) + ": needs at least one cell");
            var list = new List<News2Cell>();
            for (int i = 0; i < raw.Count; i++)
            {
                var c = new JsonObj(raw[i], JsonObj.Index(o.Sub(key), i));
                double? min = c.Has("min") ? c.Num("min") : (double?)null, max = c.Has("max") ? c.Num("max") : (double?)null;
                if (min.HasValue && max.HasValue && min > max) throw new ContentException(c.Sub("min") + ": is above max");
                int points = (int)c.Int("points");
                if (points < 0 || points > 3) throw new ContentException(c.Sub("points") + ": a NEWS2 cell scores 0 to 3");
                string condition = withCondition ? c.Enum("condition", "any", "on_air", "on_oxygen") : "any";
                list.Add(new News2Cell(min, max, points, condition, c.Str("ref")));
                c.Finish();
            }
            return list;
        }
    }

    /// <summary>The readings that make a NEWS2. A null reading is "not taken yet", which makes the score incomplete.</summary>
    public sealed record News2Input(double? RespiratoryRate, double? Spo2, double? SystolicBp, double? HeartRate, double? TemperatureC, string? Acvpu, bool? SupplementalOxygen);

    public sealed record News2Part(string Parameter, double Value, int Points, string Ref);

    /// <summary>
    /// Complete is false while a reading is missing (Missing names them; the total shown is what has been scored so far and carries no tier). Disabled is true for a pregnant patient: NEWS2 is not
    /// the tool for pregnancy (a maternity early warning score is, which is the hospital's choice), so no score or tier is given.
    /// </summary>
    public sealed record News2Result(bool Complete, bool Disabled, int Total, bool Single3, string Tier, string Frequency, string Responder, IReadOnlyList<News2Part> Parts, IReadOnlyList<string> Missing);

    /// <summary>NEWS2 (Royal College of Physicians 2017) from the content table: scale 1 by default, scale 2 for the clinician-set oxygen target of 88 to 92% in hypercapnic failure.</summary>
    public sealed class News2
    {
        private readonly News2Table _t;

        public News2(News2Table table) { _t = table; }

        public News2Result Score(News2Input obs, int scale = 1, bool pregnant = false)
        {
            if (scale != 1 && scale != 2) throw new ArgumentException("the SpO2 scale is 1 or 2");
            if (pregnant) return new News2Result(false, true, 0, false, "", "", "", new List<News2Part>(), new List<string>());

            var parts = new List<News2Part>();
            var missing = new List<string>();
            double? Rounded(string p, double? v) => v.HasValue ? Math.Round(v.Value, _t.Precision[p], MidpointRounding.AwayFromZero) : (double?)null;

            void Measured(string p, double? v)
            {
                var r = Rounded(p, v);
                if (!r.HasValue) { missing.Add(p); return; }
                parts.Add(Part(p, r.Value, _t.Cells[p]));
            }

            Measured("respiratory_rate", obs.RespiratoryRate);
            double? spo2 = Rounded("spo2", obs.Spo2);
            if (!spo2.HasValue) missing.Add("spo2");
            else if (scale == 1) parts.Add(Part("spo2", spo2.Value, _t.Cells["spo2"]));
            else if (!obs.SupplementalOxygen.HasValue) missing.Add("supplemental_oxygen");
            else
            {
                string state = obs.SupplementalOxygen.Value ? "on_oxygen" : "on_air";
                parts.Add(Part("spo2", spo2.Value, _t.Spo2Scale2.Where(c => c.Condition == "any" || c.Condition == state).ToList()));
            }
            if (!obs.SupplementalOxygen.HasValue) { if (!missing.Contains("supplemental_oxygen")) missing.Add("supplemental_oxygen"); }
            else parts.Add(new News2Part("supplemental_oxygen", obs.SupplementalOxygen.Value ? 1 : 0, obs.SupplementalOxygen.Value ? _t.OxygenYes : _t.OxygenAir, ""));
            Measured("systolic_bp", obs.SystolicBp);
            Measured("heart_rate", obs.HeartRate);
            if (obs.Acvpu == null) missing.Add("acvpu");
            else if (obs.Acvpu == "A") parts.Add(new News2Part("acvpu", 0, _t.AcvpuAlert, ""));
            else if (obs.Acvpu == "C" || obs.Acvpu == "V" || obs.Acvpu == "P" || obs.Acvpu == "U") parts.Add(new News2Part("acvpu", 1, _t.AcvpuOther, ""));
            else throw new ArgumentException("ACVPU must be A, C, V, P or U");
            Measured("temperature_c", obs.TemperatureC);

            int total = parts.Sum(p => p.Points);
            bool single3 = parts.Any(p => p.Points == 3);
            if (missing.Count > 0) return new News2Result(false, false, total, single3, "", "", "", parts, missing.OrderBy(m => Array.IndexOf(News2Table.Order, m)).ToList());

            var band = Band(total, single3);
            return new News2Result(true, false, total, single3, band.Id, band.Frequency, band.Responder, parts, missing);
        }

        // A total of 5 or more is judged by the total; a lower total with any single reading scoring 3 needs an urgent ward review; otherwise by the total.
        private News2Band Band(int total, bool single3)
        {
            News2Band By(string id) => _t.Response.First(b => b.Id == id);
            if (total >= By("7+").Min!.Value) return By("7+");
            if (total >= By("5-6").Min!.Value) return By("5-6");
            if (single3) return By("single3");
            return total >= By("1-4").Min!.Value ? By("1-4") : By("0");
        }

        private static News2Part Part(string parameter, double value, IReadOnlyList<News2Cell> cells)
        {
            var hits = cells.Where(c => c.Covers(value)).ToList();
            if (hits.Count != 1) throw new InvalidOperationException("NEWS2 " + parameter + " = " + value + " matches " + hits.Count + " cells");
            return new News2Part(parameter, value, hits[0].Points, hits[0].Ref);
        }
    }
}
