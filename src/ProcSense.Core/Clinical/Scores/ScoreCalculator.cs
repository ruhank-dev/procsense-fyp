using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProcSense.Core.Clinical.Scores
{
    /// <summary>One item that scored, with the row it came from (for the debrief and the register).</summary>
    public sealed record ScoredItem(string Item, string RowId, int Points, string Ref);

    /// <summary>
    /// What a point score came to. <see cref="Complete"/> is false while a needed input is missing (<see cref="Missing"/> names the first item of each missing group), and then
    /// there is no <see cref="Band"/>: a partial total is shown but never interpreted.
    /// </summary>
    public sealed record ScoreResult(string Score, bool Complete, int Total, string? Band, IReadOnlyList<ScoredItem> Items, IReadOnlyList<string> Missing);

    public sealed record Tg18CholecystitisResult(string Diagnosis, string Grade, IReadOnlyList<string> Missing);
    public sealed record Tg18CholangitisResult(string Diagnosis, bool Emergency, IReadOnlyList<string> Missing);
    public sealed record AsgeResult(string Tier, bool Emergency, IReadOnlyList<string> Missing);
    public sealed record StopBangResult(bool Complete, int Total, string? Band, IReadOnlyList<string> Missing);
    public sealed record AkiResult(bool Complete, int Stage, double Ratio, IReadOnlyList<string> Missing);
    public sealed record AsaCheckResult(int Difference, bool Exact, bool EmergencyMatches);

    /// <summary>
    /// The clinical calculators. Each is a pure function of the facts it is given (name to bool, number or option text); a name that is absent means "not known yet", which is different from
    /// false or zero. None of them decides anything: they return a total, a band or a tier as information for the trainee and the scorer.
    /// </summary>
    public sealed class ScoreCalculator
    {
        private readonly ScoreTables _t;

        public ScoreCalculator(ScoreTables tables) { _t = tables; }

        // ---------------------------------------------------------------- facts

        private static bool Has(IReadOnlyDictionary<string, object?> f, string name) => f.TryGetValue(name, out var v) && v != null;

        private static bool? Bool(IReadOnlyDictionary<string, object?> f, string name) =>
            f.TryGetValue(name, out var v) && v != null ? (bool?)(v is bool b ? b : throw new ArgumentException("fact '" + name + "' must be true or false")) : null;

        private static double? Num(IReadOnlyDictionary<string, object?> f, string name)
        {
            if (!f.TryGetValue(name, out var v) || v == null) return null;
            return v switch
            {
                double d => d,
                int i => i,
                long l => l,
                decimal m => (double)m,
                _ => throw new ArgumentException("fact '" + name + "' must be a number"),
            };
        }

        /// <summary>True if any fact is known and true; false if all are known and false; null if the answer is still open.</summary>
        private static bool? AnyOf(IEnumerable<bool?> parts)
        {
            bool open = false;
            foreach (var p in parts)
            {
                if (p == true) return true;
                if (p == null) open = true;
            }
            return open ? (bool?)null : false;
        }

        /// <summary>True if all are known and true; false if any is known false; null if the answer is still open.</summary>
        private static bool? AllOf(IEnumerable<bool?> parts)
        {
            bool open = false;
            foreach (var p in parts)
            {
                if (p == false) return false;
                if (p == null) open = true;
            }
            return open ? (bool?)null : true;
        }

        private static bool? Above(double? value, double limit) => value.HasValue ? value.Value > limit : (bool?)null;
        private static bool? AtLeast(double? value, double limit) => value.HasValue ? value.Value >= limit : (bool?)null;

        // ---------------------------------------------------------------- AIR, AAS, Alvarado

        /// <summary>
        /// Adds the points of a point score. A yes/no item gives the points of its only row when true; a measured item gives the points of the row whose range holds the value (no row, no points);
        /// an item with several named options takes the option as text ("none" gives nothing). A group of exclusive items accepts at most one that scores.
        /// </summary>
        public ScoreResult Point(string score, IReadOnlyDictionary<string, object?> facts)
        {
            if (!_t.Points.TryGetValue(score, out var def)) throw new ArgumentException("unknown score '" + score + "'");
            var items = new List<ScoredItem>();
            var missing = new List<string>();
            foreach (var group in def.Required)
            {
                var scored = new List<ScoredItem>();
                bool any = false;
                foreach (var item in group)
                {
                    if (!Has(facts, item)) continue;
                    any = true;
                    var got = ScoreItem(def, item, facts[item]!);
                    if (got != null) scored.Add(got);
                }
                if (!any) { missing.Add(group[0]); continue; }
                if (scored.Count > 1) throw new ArgumentException("only one of " + string.Join(", ", group) + " can apply");
                items.AddRange(scored);
            }
            int total = items.Sum(i => i.Points);
            bool complete = missing.Count == 0;
            string? band = complete ? def.Bands.First(b => b.Covers(total)).Band : null;
            return new ScoreResult(score, complete, total, band, items, missing);
        }

        private static ScoredItem? ScoreItem(PointScore def, string item, object value)
        {
            var rows = def.Items.Where(r => r.Item == item).ToList();
            if (rows[0].Numeric)
            {
                double v = value switch { double d => d, int i => i, long l => l, decimal m => (double)m, _ => throw new ArgumentException("fact '" + item + "' must be a number") };
                var hit = rows.FirstOrDefault(r => r.Covers(v));
                return hit == null ? null : new ScoredItem(item, hit.Id, hit.Points, hit.Ref);
            }
            if (value is bool b)
            {
                if (!b) return null;
                if (rows.Count != 1) throw new ArgumentException("fact '" + item + "' needs one of: " + string.Join(", ", rows.Select(r => r.Option)));
                return new ScoredItem(item, rows[0].Id, rows[0].Points, rows[0].Ref);
            }
            if (value is string s)
            {
                if (s == "none") return null;
                var hit = rows.FirstOrDefault(r => r.Option == s) ?? throw new ArgumentException("fact '" + item + "' must be one of: none, " + string.Join(", ", rows.Select(r => r.Option)));
                return new ScoredItem(item, hit.Id, hit.Points, hit.Ref);
            }
            throw new ArgumentException("fact '" + item + "' must be true/false, a number or an option name");
        }

        // ---------------------------------------------------------------- TG18 acute cholecystitis (diagnosis and grade)

        /// <summary>
        /// Suspected = one local sign (A) and one whole-body sign (B); definite = A, B and the scan (C). Grade III if any organ is failing, II if any moderate sign, otherwise I.
        /// Facts (yes/no unless noted): murphy, ruq_lump_pain_tenderness, fever, raised_crp, raised_wbc, imaging_inflamed; for the grade vasopressor, reduced_consciousness, poor_oxygen, low_urine,
        /// creatinine_umol, inr, platelets (numbers), wbc (number), symptom_hours (number), tender_lump, severe_local_inflammation. A grade is only given once it is certain.
        /// </summary>
        public Tg18CholecystitisResult Tg18Cholecystitis(IReadOnlyDictionary<string, object?> f)
        {
            var missing = new List<string>();
            bool? a = AnyOf(new[] { Bool(f, "murphy"), Bool(f, "ruq_lump_pain_tenderness") });
            bool? b = AnyOf(new[] { Bool(f, "fever"), Bool(f, "raised_crp"), Bool(f, "raised_wbc") });
            bool? c = Bool(f, "imaging_inflamed");
            if (a == null) missing.Add("murphy");
            if (b == null) missing.Add("fever");
            string diagnosis = a == true && b == true ? (c == true ? "definite" : "suspected") : (a == null || b == null ? "unknown" : "none");
            if (diagnosis == "suspected" && c == null) missing.Add("imaging_inflamed");   // not needed for "suspected", asked for so the diagnosis can become definite

            string grade = "";
            if (diagnosis == "suspected" || diagnosis == "definite")
            {
                var organ = new[]
                {
                    Bool(f, "vasopressor"), Bool(f, "reduced_consciousness"), Bool(f, "poor_oxygen"), Bool(f, "low_urine"),
                    Above(Num(f, "creatinine_umol"), _t.Tg18CreatinineUmol), Above(Num(f, "inr"), _t.Tg18Inr), Num(f, "platelets") is double p ? p < _t.Tg18Platelets : (bool?)null,
                };
                var moderate = new[] { Above(Num(f, "wbc"), _t.Tg18Wbc), Bool(f, "tender_lump"), Above(Num(f, "symptom_hours"), _t.Tg18SymptomHours), Bool(f, "severe_local_inflammation") };
                var iii = AnyOf(organ);
                var ii = AnyOf(moderate);
                if (iii == true) grade = "III";
                else if (iii == null) AddOpen(missing, new[] { "vasopressor", "reduced_consciousness", "poor_oxygen", "low_urine", "creatinine_umol", "inr", "platelets" }, f);
                else if (ii == true) grade = "II";
                else if (ii == null) AddOpen(missing, new[] { "wbc", "tender_lump", "symptom_hours", "severe_local_inflammation" }, f);
                else grade = "I";
            }
            return new Tg18CholecystitisResult(diagnosis, grade, missing);
        }

        private static void AddOpen(List<string> missing, string[] names, IReadOnlyDictionary<string, object?> f)
        {
            foreach (var n in names) if (!Has(f, n)) missing.Add(n);
        }

        // ---------------------------------------------------------------- TG18 acute cholangitis

        /// <summary>
        /// Definite = A (inflammation), B (bile blocked) and C (scan); suspected = A and (B or C). Either is an emergency. Facts: temperature_c, chills, wbc, crp (A); bilirubin_umol,
        /// liver_tests_x_uln (B); duct_widened, cause_seen (C).
        /// </summary>
        public Tg18CholangitisResult Tg18Cholangitis(IReadOnlyDictionary<string, object?> f)
        {
            var missing = new List<string>();
            double? wbc = Num(f, "wbc");
            bool? a = AnyOf(new[]
            {
                Above(Num(f, "temperature_c"), _t.CholangitisTemperature), Bool(f, "chills"),
                wbc.HasValue ? wbc.Value < _t.CholangitisWbcLow || wbc.Value > _t.CholangitisWbcHigh : (bool?)null, AtLeast(Num(f, "crp"), _t.CholangitisCrp),
            });
            bool? b = AnyOf(new[] { AtLeast(Num(f, "bilirubin_umol"), _t.CholangitisBilirubin), Above(Num(f, "liver_tests_x_uln"), _t.CholangitisLiverTests) });
            bool? c = AnyOf(new[] { Bool(f, "duct_widened"), Bool(f, "cause_seen") });
            string diagnosis;
            if (a == true && b == true && c == true) diagnosis = "definite";
            else if (a == true && (b == true || c == true)) diagnosis = "suspected";
            else if (a == false || (b == false && c == false)) diagnosis = "none";
            else diagnosis = "unknown";
            if (diagnosis == "unknown")
            {
                if (a == null) AddOpen(missing, new[] { "temperature_c", "chills", "wbc", "crp" }, f);
                if (b == null) AddOpen(missing, new[] { "bilirubin_umol", "liver_tests_x_uln" }, f);
                if (c == null) AddOpen(missing, new[] { "duct_widened", "cause_seen" }, f);
            }
            return new Tg18CholangitisResult(diagnosis, diagnosis == "definite" || diagnosis == "suspected", missing);
        }

        // ---------------------------------------------------------------- ASGE 2019 duct-stone risk

        /// <summary>
        /// Cholangitis first (fever, jaundice and right upper pain together is an emergency); then high (stone seen, or bilirubin above the limit with a duct wider than the limit),
        /// intermediate (abnormal liver tests, age over the limit or a wide duct), otherwise low. Facts: fever, jaundice, ruq_pain, stone_on_scan, bilirubin_umol, duct_mm, liver_tests_abnormal, age.
        /// </summary>
        public AsgeResult Asge(IReadOnlyDictionary<string, object?> f)
        {
            var missing = new List<string>();
            bool? cholangitis = AllOf(new[] { Bool(f, "fever"), Bool(f, "jaundice"), Bool(f, "ruq_pain") });
            if (cholangitis == true) return new AsgeResult("cholangitis", true, missing);
            if (cholangitis == null)
            {
                AddOpen(missing, new[] { "fever", "jaundice", "ruq_pain" }, f);
                return new AsgeResult("", false, missing);
            }

            double? duct = Num(f, "duct_mm");
            bool? high = AnyOf(new[] { Bool(f, "stone_on_scan"), AllOf(new[] { Above(Num(f, "bilirubin_umol"), _t.AsgeBilirubin), Above(duct, _t.AsgeDuctMm) }) });
            if (high == true) return new AsgeResult("high", false, missing);
            if (high == null)
            {
                AddOpen(missing, new[] { "stone_on_scan", "bilirubin_umol", "duct_mm" }, f);
                return new AsgeResult("", false, missing);
            }
            bool? intermediate = AnyOf(new[] { Bool(f, "liver_tests_abnormal"), Above(Num(f, "age"), _t.AsgeAge), Above(duct, _t.AsgeDuctMm) });
            if (intermediate == true) return new AsgeResult("intermediate", false, missing);
            if (intermediate == null)
            {
                AddOpen(missing, new[] { "liver_tests_abnormal", "age", "duct_mm" }, f);
                return new AsgeResult("", false, missing.Distinct().ToList());
            }
            return new AsgeResult("low", false, missing);
        }

        // ---------------------------------------------------------------- STOP-Bang

        /// <summary>One point for each of the 8 questions answered yes (the table's item names); 0-2 low, 3-4 intermediate, 5-8 high.</summary>
        public StopBangResult StopBang(IReadOnlyDictionary<string, object?> f)
        {
            int total = 0;
            var missing = new List<string>();
            foreach (var item in _t.StopBangItems)
            {
                var v = Bool(f, item);
                if (v == null) missing.Add(item);
                else if (v == true) total++;
            }
            string? band = missing.Count == 0 ? _t.StopBangBands.First(b => b.Covers(total)).Band : null;
            return new StopBangResult(missing.Count == 0, total, band, missing);
        }

        // ---------------------------------------------------------------- AKI stage (creatinine against the usual value, or a rise within 48 hours)

        /// <summary>
        /// Stage 1 = 1.5 to 1.9 times the usual creatinine, 2 = 2.0 to 2.9 times, 3 = 3 times or 354 micromol/L or more; 0 = no injury by these criteria. Facts: creatinine_umol, baseline_umol.
        /// An absolute rise of 26.5 micromol/L or more within 48 hours (fact creatinine_48h_ago_umol, optional) is stage 1 when the ratio alone would give none; without that fact the ratio decides.
        /// </summary>
        public AkiResult Aki(IReadOnlyDictionary<string, object?> f)
        {
            double? nowOrNull = Num(f, "creatinine_umol"), usualOrNull = Num(f, "baseline_umol");
            var missing = new List<string>();
            if (nowOrNull == null) missing.Add("creatinine_umol");
            if (usualOrNull == null) missing.Add("baseline_umol");
            if (!(nowOrNull is double now) || !(usualOrNull is double usual)) return new AkiResult(false, 0, 0, missing);
            if (usual <= 0) throw new ArgumentException("baseline_umol must be more than 0");
            double ratio = now / usual;
            int stage = ratio >= _t.AkiStage3 || now >= _t.AkiStage3Absolute ? 3 : ratio >= _t.AkiStage2 ? 2 : ratio >= _t.AkiStage1 ? 1 : 0;
            if (stage == 0 && Num(f, "creatinine_48h_ago_umol") is double before && now - before >= _t.AkiRise48h) stage = 1;
            return new AkiResult(true, stage, Math.Round(ratio, 2), missing);
        }

        // ---------------------------------------------------------------- ASA proposal

        /// <summary>How far the trainee's proposed ASA grade is from the grade the case expects (the case file stores the expected grade; it is never worked out here).</summary>
        public static AsaCheckResult AsaCheck(int proposed, bool proposedEmergency, int expected, bool expectedEmergency)
        {
            if (proposed < 1 || proposed > 6 || expected < 1 || expected > 6) throw new ArgumentException("an ASA grade is 1 to 6");
            return new AsaCheckResult(Math.Abs(proposed - expected), proposed == expected, proposedEmergency == expectedEmergency);
        }

        // ---------------------------------------------------------------- supporting numbers (plain formulas, no bands)

        /// <summary>Mean arterial pressure, (systolic + 2 x diastolic) / 3.</summary>
        public static double Map(double systolic, double diastolic) => Math.Round((systolic + 2 * diastolic) / 3, 1);

        /// <summary>Shock index, heart rate divided by systolic pressure.</summary>
        public static double ShockIndex(double heartRate, double systolic)
        {
            if (systolic <= 0) throw new ArgumentException("systolic pressure must be more than 0");
            return Math.Round(heartRate / systolic, 2);
        }

        /// <summary>R ratio of liver tests, (ALT / its upper limit) divided by (ALP / its upper limit).</summary>
        public static double RRatio(double alt, double altUpperLimit, double alp, double alpUpperLimit)
        {
            if (altUpperLimit <= 0 || alpUpperLimit <= 0 || alp <= 0) throw new ArgumentException("limits and ALP must be more than 0");
            return Math.Round((alt / altUpperLimit) / (alp / alpUpperLimit), 2);
        }
    }
}
