using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical.Scores
{
    /// <summary>One row of a point score: which item and option it is for, the points it gives, and (for a measured value) the range it covers.</summary>
    public sealed record ScoreItemRow(string Id, string Item, string Option, int Points, double? Min, bool MinInclusive, double? MaxExclusive, string Ref)
    {
        /// <summary>True when the row is for a measured number (it has a range), false when it is a yes/no or a named option.</summary>
        public bool Numeric => Min.HasValue;

        public bool Covers(double value) =>
            Min.HasValue && (MinInclusive ? value >= Min.Value : value > Min.Value) && (!MaxExclusive.HasValue || value < MaxExclusive.Value);
    }

    public sealed record ScoreBand(string Band, int Min, int? Max, string Ref)
    {
        public bool Covers(int total) => total >= Min && (!Max.HasValue || total <= Max.Value);
    }

    /// <summary>A score that adds up points (AIR, AAS, Alvarado): its rows, what a total means, and the inputs it needs.</summary>
    public sealed class PointScore
    {
        public PointScore(string id, string title, int max, IReadOnlyList<IReadOnlyList<string>> required, IReadOnlyList<ScoreItemRow> items, IReadOnlyList<ScoreBand> bands)
        {
            Id = id; Title = title; Max = max; Required = required; Items = items; Bands = bands;
        }

        public string Id { get; }
        public string Title { get; }
        public int Max { get; }
        /// <summary>Inputs the score needs. A group with several items is exclusive: one answer among them (such as light, medium or strong rebound).</summary>
        public IReadOnlyList<IReadOnlyList<string>> Required { get; }
        public IReadOnlyList<ScoreItemRow> Items { get; }
        public IReadOnlyList<ScoreBand> Bands { get; }
    }

    /// <summary>Everything the calculators read, loaded from content/scores/scores.json. Numbers come from the verified score tables, not from code.</summary>
    public sealed class ScoreTables
    {
        public ScoreTables(IReadOnlyDictionary<string, PointScore> points) { Points = points; }

        public IReadOnlyDictionary<string, PointScore> Points { get; }

        public double Tg18CreatinineUmol { get; private set; }
        public double Tg18Inr { get; private set; }
        public double Tg18Platelets { get; private set; }
        public double Tg18Wbc { get; private set; }
        public double Tg18SymptomHours { get; private set; }
        public double CholangitisTemperature { get; private set; }
        public double CholangitisWbcLow { get; private set; }
        public double CholangitisWbcHigh { get; private set; }
        public double CholangitisCrp { get; private set; }
        public double CholangitisBilirubin { get; private set; }
        public double CholangitisLiverTests { get; private set; }
        public double AsgeBilirubin { get; private set; }
        public double AsgeDuctMm { get; private set; }
        public double AsgeAge { get; private set; }
        public IReadOnlyList<string> StopBangItems { get; private set; } = new List<string>();
        public IReadOnlyList<ScoreBand> StopBangBands { get; private set; } = new List<ScoreBand>();
        public double AkiStage1 { get; private set; }
        public double AkiStage2 { get; private set; }
        public double AkiStage3 { get; private set; }
        public double AkiStage3Absolute { get; private set; }
        public double AkiRise48h { get; private set; }

        public static readonly ScoreTables Empty = new ScoreTables(new Dictionary<string, PointScore>());

        /// <summary>Parses scores.json. Unknown fields, a missing number and a required item with no row are errors that name the place.</summary>
        public static ScoreTables Parse(string json, string source = "scores")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var points = new Dictionary<string, PointScore>(StringComparer.Ordinal);
            var rawPoints = root.Arr("point_scores");
            for (int i = 0; i < rawPoints.Count; i++)
            {
                var o = new JsonObj(rawPoints[i], JsonObj.Index(root.Sub("point_scores"), i));
                var score = ParsePoint(o);
                o.Finish();
                if (points.ContainsKey(score.Id)) throw new ContentException(o.Sub("id") + ": duplicate score '" + score.Id + "'");
                points[score.Id] = score;
            }

            var t = new ScoreTables(points);
            var c = root.Obj("tg18_cholecystitis");
            var c3 = c.Obj("grade_iii"); t.Tg18CreatinineUmol = c3.Num("creatinine_umol"); t.Tg18Inr = c3.Num("inr"); t.Tg18Platelets = c3.Num("platelets"); Ref(c3); c3.Finish();
            var c2 = c.Obj("grade_ii"); t.Tg18Wbc = c2.Num("wbc"); t.Tg18SymptomHours = c2.Num("symptom_hours"); Ref(c2); c2.Finish();
            Refs(c); c.Finish();
            var g = root.Obj("tg18_cholangitis");
            var inf = g.Obj("inflammation"); t.CholangitisTemperature = inf.Num("temperature_c"); t.CholangitisWbcLow = inf.Num("wbc_low"); t.CholangitisWbcHigh = inf.Num("wbc_high"); t.CholangitisCrp = inf.Num("crp"); Ref(inf); inf.Finish();
            var bb = g.Obj("bile_blocked"); t.CholangitisBilirubin = bb.Num("bilirubin_umol"); t.CholangitisLiverTests = bb.Num("liver_tests_x_uln"); Ref(bb); bb.Finish();
            Refs(g); g.Finish();
            var a = root.Obj("asge");
            var hi = a.Obj("high"); t.AsgeBilirubin = hi.Num("bilirubin_umol"); t.AsgeDuctMm = hi.Num("duct_mm"); Ref(hi); hi.Finish();
            var im = a.Obj("intermediate"); t.AsgeAge = im.Num("age"); Ref(im); im.Finish();
            Refs(a); a.Finish();
            var s = root.Obj("stopbang");
            t.StopBangItems = Strings(s, "items");
            if (t.StopBangItems.Count != 8) throw new ContentException(s.Sub("items") + ": STOP-Bang has 8 questions");
            t.StopBangBands = Bands(s, "bands");
            s.Str("ref"); s.Finish();
            var k = root.Obj("aki");
            t.AkiStage1 = k.Num("stage1_ratio"); t.AkiStage2 = k.Num("stage2_ratio"); t.AkiStage3 = k.Num("stage3_ratio"); t.AkiStage3Absolute = k.Num("stage3_absolute_umol"); t.AkiRise48h = k.Num("rise_48h_umol");
            Refs(k); k.Finish();
            root.Finish();
            return t;
        }

        private static void Ref(JsonObj o) => o.Str("ref");

        private static void Refs(JsonObj o) { foreach (var r in o.Arr("refs")) if (!(r is string)) throw new ContentException(o.Sub("refs") + ": expected register ids"); }

        private static List<string> Strings(JsonObj o, string key)
        {
            var list = new List<string>();
            foreach (var x in o.Arr(key)) list.Add(x as string ?? throw new ContentException(o.Sub(key) + ": expected text"));
            return list;
        }

        private static List<ScoreBand> Bands(JsonObj o, string key)
        {
            var raw = o.Arr(key);
            var list = new List<ScoreBand>();
            for (int i = 0; i < raw.Count; i++)
            {
                var b = new JsonObj(raw[i], JsonObj.Index(o.Sub(key), i));
                list.Add(new ScoreBand(b.Str("band"), (int)b.Int("min"), b.Has("max") ? (int)b.Int("max") : (int?)null, b.Has("ref") ? b.Str("ref") : ""));
                b.Finish();
            }
            for (int i = 1; i < list.Count; i++)
                if (list[i].Min != (list[i - 1].Max ?? int.MaxValue) + 1) throw new ContentException(o.Sub(key) + ": band '" + list[i].Band + "' does not follow the one before it without a gap or overlap");
            return list;
        }

        private static PointScore ParsePoint(JsonObj o)
        {
            string id = o.Str("id");
            string title = o.Str("title");
            int max = (int)o.Int("max");
            var required = new List<IReadOnlyList<string>>();
            foreach (var g in o.Arr("required"))
            {
                var group = new List<string>();
                if (!(g is List<object?> items) || items.Count == 0) throw new ContentException(o.Sub("required") + ": each group needs at least one item");
                foreach (var x in items) group.Add(x as string ?? throw new ContentException(o.Sub("required") + ": expected item names"));
                required.Add(group);
            }
            var rows = new List<ScoreItemRow>();
            var rawRows = o.Arr("items");
            for (int i = 0; i < rawRows.Count; i++)
            {
                var r = new JsonObj(rawRows[i], JsonObj.Index(o.Sub("items"), i));
                string rid = r.Str("id"), item = r.Str("item"), option = r.Str("option");
                int points = (int)r.Int("points");
                string rf = r.Str("ref");
                double? min = r.Has("min") ? r.Num("min") : (double?)null;
                bool inclusive = r.Has("min_inclusive") ? r.Bool("min_inclusive") : true;
                double? maxEx = r.Has("max_exclusive") ? r.Num("max_exclusive") : (double?)null;
                r.Finish();
                if (points < 0) throw new ContentException(r.Sub("points") + ": points cannot be negative");
                rows.Add(new ScoreItemRow(rid, item, option, points, min, inclusive, maxEx, rf));
            }
            var known = new HashSet<string>(rows.Select(r => r.Item), StringComparer.Ordinal);
            foreach (var group in required)
                foreach (var item in group)
                    if (!known.Contains(item)) throw new ContentException(o.Sub("required") + ": '" + item + "' has no row in the score");
            var bands = Bands(o, "bands");
            return new PointScore(id, title, max, required, rows, bands);
        }
    }
}
