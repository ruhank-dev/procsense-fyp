using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>One test the trainee can order: what it is (blood, bedside or imaging), which tube a blood test goes in, where imaging is done, the analytes of the case's results that belong to it, and its default turnaround.</summary>
    public sealed record InvestigationDef(string Id, string Label, string Kind, string Tube, string Location, IReadOnlyList<string> Analytes, double MinMinutes, double MaxMinutes, string Ref);

    /// <summary>The pregnancy gate: who is eligible (sex and age band) and which tests it blocks until the pregnancy status is known.</summary>
    public sealed record PregnancyGate(string Sex, int MinAge, int MaxAge, IReadOnlyList<string> Blocks);

    /// <summary>A normal result the engine may supply when the case states none (supplied for the scenario named, for the patient's sex).</summary>
    public sealed record NormalResult(string Id, string Analyte, string Sex, string Value, string Unit, string Range, string Ref);

    /// <summary>The normal results for one test and the scenario they may be supplied for (S1 only for the liver tests).</summary>
    public sealed record NormalSet(string Scenario, IReadOnlyList<NormalResult> Results);

    /// <summary>When a test is indicated: its kind for a planned (S1) and an emergency (S2) patient, and why (always, conditional, not_routine, per_policy or not_used).</summary>
    public sealed record IndicationRow(string Test, string S1, string S2, string Why, string WhenS1, string WhenS2, string Ref);

    /// <summary>The tests, tubes, default turnaround and indication kinds, loaded from content/investigations (investigations.json by hand, indications.json built from the verified table).</summary>
    public sealed class InvestigationTables
    {
        public static readonly InvestigationTables Empty = new InvestigationTables(new List<InvestigationDef>(), new List<string>(), new List<IndicationRow>(), "", 0);


        private static readonly Regex Parenthetical = new Regex(@"\s*\(.*\)\s*$", RegexOptions.CultureInvariant);

        public InvestigationTables(IReadOnlyList<InvestigationDef> tests, IReadOnlyList<string> tubes, IReadOnlyList<IndicationRow> indications, string delayEvent, double delayMinutes,
            IReadOnlyList<string>? labels = null, double dripDilution = 1, double edtaPotassiumRise = 0, IReadOnlyDictionary<string, NormalSet>? normals = null, CriticalTable? critical = null, PregnancyGate? gate = null)
        {
            Gate = gate ?? new PregnancyGate("F", 12, 55, new List<string>());
            Critical = critical ?? CriticalTable.Empty;
            Tests = tests; Tubes = tubes; Indications = indications; DelayEvent = delayEvent; DelayMinutes = delayMinutes;
            Labels = labels ?? new List<string> { "bedside", "prelabelled", "unlabelled" }; DripDilution = dripDilution; EdtaPotassiumRise = edtaPotassiumRise;
            Normals = normals ?? new Dictionary<string, NormalSet>();
        }

        public IReadOnlyList<InvestigationDef> Tests { get; }
        public IReadOnlyList<string> Tubes { get; }
        public IReadOnlyList<IndicationRow> Indications { get; }
        /// <summary>The event name that extends every test still processing, and by how many minutes.</summary>
        public string DelayEvent { get; }
        public double DelayMinutes { get; }
        /// <summary>How a sample can be labelled: bedside, prelabelled or unlabelled.</summary>
        public IReadOnlyList<string> Labels { get; }
        /// <summary>A drip-arm sample is this fraction of the true value.</summary>
        public double DripDilution { get; }
        /// <summary>Potassium rises by this much (mmol/L) when a gold-tube test is taken into an EDTA tube.</summary>
        public double EdtaPotassiumRise { get; }
        public IReadOnlyDictionary<string, NormalSet> Normals { get; }
        /// <summary>The limits the lab phones about (content/investigations/critical.json).</summary>
        public CriticalTable Critical { get; }
        /// <summary>Who must have a known pregnancy status before radiation imaging or theatre.</summary>
        public PregnancyGate Gate { get; }

        public InvestigationDef? Find(string id) => Tests.FirstOrDefault(t => t.Id == id);

        public IndicationRow? IndicationOf(string test) => Indications.FirstOrDefault(i => i.Test == test);

        /// <summary>The test a result belongs to, from the analyte named in the case ("Potassium (repeat)" is the analyte Potassium), or null.</summary>
        public InvestigationDef? TestOfAnalyte(string resultName)
        {
            string analyte = BaseName(resultName);
            return Tests.FirstOrDefault(t => t.Analytes.Contains(analyte));
        }

        /// <summary>"Lactate (repeat after fluids)" gives "Lactate"; names without a bracket are unchanged.</summary>
        public static string BaseName(string resultName) => Parenthetical.Replace(resultName, "").Trim();

        /// <summary>True for the results a case states for a second look ("Potassium (repeat)", "Capillary glucose (repeat in 30 minutes)").</summary>
        public static bool IsRepeat(string resultName) => resultName.IndexOf("(repeat", StringComparison.OrdinalIgnoreCase) >= 0;

        public static InvestigationTables Parse(string investigationsJson, string indicationsJson, string? normalsJson = null, string? criticalJson = null)
        {
            var root = new JsonObj(MiniJson.Parse(investigationsJson), "investigations");
            var tubes = new List<string>();
            foreach (var t in root.Arr("tubes")) tubes.Add(t as string ?? throw new ContentException(root.Sub("tubes") + ": expected text"));
            var tests = new List<InvestigationDef>();
            var raw = root.Arr("tests");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("tests"), i));
                string id = o.Str("id");
                if (tests.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate test '" + id + "'");
                string kind = o.Enum("kind", "blood", "bedside", "imaging");
                string tube = o.Has("tube") ? o.Str("tube") : "", loc = o.Has("location") ? o.Str("location") : "";
                if (kind == "blood" && !tubes.Contains(tube)) throw new ContentException(o.Sub("tube") + ": a blood test needs one of the tubes " + string.Join(", ", tubes));
                if (kind != "blood" && tube.Length > 0) throw new ContentException(o.Sub("tube") + ": only a blood test goes in a tube");
                if (kind == "imaging" && loc.Length == 0) throw new ContentException(o.Sub("location") + ": imaging needs the place it is done");
                if (kind != "imaging" && loc.Length > 0) throw new ContentException(o.Sub("location") + ": only imaging has a place");
                var analytes = new List<string>();
                foreach (var a in o.Arr("analytes")) analytes.Add(a as string ?? throw new ContentException(o.Sub("analytes") + ": expected text"));
                var m = o.Obj("minutes");
                double lo = m.Num("min"), hi = m.Num("max");
                if (lo < 0 || hi < lo) throw new ContentException(m.Sub("min") + ": needs 0 <= min <= max");
                m.Finish();
                tests.Add(new InvestigationDef(id, o.Str("label"), kind, tube, loc, analytes, lo, hi, o.Str("ref")));
                o.Finish();
            }
            var dup = tests.SelectMany(t => t.Analytes.Select(a => (a, t.Id))).GroupBy(x => x.a).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new ContentException(root.Sub("tests") + ": the analyte '" + dup.Key + "' belongs to more than one test");
            var gateObj = root.Obj("pregnancy_gate");
            var blocks = new List<string>();
            foreach (var b in gateObj.Arr("blocks")) blocks.Add(b as string ?? throw new ContentException(gateObj.Sub("blocks") + ": expected test ids"));
            foreach (var b in blocks) if (tests.All(t => t.Id != b)) throw new ContentException(gateObj.Sub("blocks") + ": '" + b + "' is not a test");
            int minAge = (int)gateObj.Int("min_age"), maxAge = (int)gateObj.Int("max_age");
            if (minAge < 0 || maxAge < minAge) throw new ContentException(gateObj.Sub("min_age") + ": needs 0 <= min_age <= max_age");
            var gate = new PregnancyGate(gateObj.Enum("sex", "F", "M"), minAge, maxAge, blocks);
            gateObj.Str("ref"); gateObj.Finish();
            var delay = root.Obj("delay");
            string delayEvent = delay.Str("event");
            double delayMinutes = delay.Num("minutes");
            if (delayMinutes < 0) throw new ContentException(delay.Sub("minutes") + ": cannot be negative");
            delay.Str("ref"); delay.Finish();
            var labels = new List<string>();
            foreach (var l in root.Arr("labels")) labels.Add(l as string ?? throw new ContentException(root.Sub("labels") + ": expected text"));
            foreach (var need in new[] { "bedside", "prelabelled", "unlabelled" })
                if (!labels.Contains(need)) throw new ContentException(root.Sub("labels") + ": the label '" + need + "' is missing");
            var art = root.Obj("artefacts");
            double dilution = art.Num("drip_dilution_factor"), rise = art.Num("edta_potassium_rise");
            if (dilution <= 0 || dilution >= 1) throw new ContentException(art.Sub("drip_dilution_factor") + ": must be between 0 and 1");
            if (rise <= 0) throw new ContentException(art.Sub("edta_potassium_rise") + ": must be more than 0");
            foreach (var r in art.Arr("refs")) if (!(r is string)) throw new ContentException(art.Sub("refs") + ": expected register ids");
            art.Finish();
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Str("note");
            root.Finish();

            var normals = new SortedDictionary<string, NormalSet>(StringComparer.Ordinal);
            if (normalsJson != null)
            {
                var nroot = new JsonObj(MiniJson.Parse(normalsJson), "normal_results");
                var ntests = nroot.Obj("tests");
                foreach (var test in tests.Select(t => t.Id))
                {
                    if (!ntests.Has(test)) continue;
                    var set = ntests.Obj(test);
                    string scenario = set.Enum("scenario", "S1", "S2");
                    var list = new List<NormalResult>();
                    var rawRows = set.Arr("results");
                    for (int i = 0; i < rawRows.Count; i++)
                    {
                        var r = new JsonObj(rawRows[i], JsonObj.Index(set.Sub("results"), i));
                        list.Add(new NormalResult(r.Str("id"), r.Str("analyte"), r.Enum("sex", "F", "M"), r.Str("value"), r.Str("unit"), r.Str("range"), r.Str("ref")));
                        r.Finish();
                    }
                    set.Finish();
                    normals[test] = new NormalSet(scenario, list);
                }
                ntests.Finish();
                nroot.Finish();
            }

            var ind = new JsonObj(MiniJson.Parse(indicationsJson), "indications");
            var rows = new List<IndicationRow>();
            var rawInd = ind.Arr("indications");
            string[] kinds = { "always", "conditional", "not_routine", "per_policy", "not_used" };
            for (int i = 0; i < rawInd.Count; i++)
            {
                var o = new JsonObj(rawInd[i], JsonObj.Index(ind.Sub("indications"), i));
                string test = o.Str("test");
                if (tests.All(t => t.Id != test)) throw new ContentException(o.Sub("test") + ": '" + test + "' is not a test in investigations.json");
                if (rows.Any(r => r.Test == test)) throw new ContentException(o.Sub("test") + ": duplicate indication for '" + test + "'");
                rows.Add(new IndicationRow(test, o.Enum("s1", kinds), o.Enum("s2", kinds), o.Str("why"), o.Str("when_s1"), o.Str("when_s2"), o.Str("ref")));
                o.Finish();
            }
            ind.Finish();
            return new InvestigationTables(tests, tubes, rows, delayEvent, delayMinutes, labels, dilution, rise, normals, criticalJson == null ? null : CriticalTable.Parse(criticalJson), gate);
        }
    }

    /// <summary>What the indication rule says about one test for this case: appropriate or not indicated, why (the table's words), whether the case needs it, and the register id.</summary>
    public sealed record IndicationResult(string Test, string Verdict, bool Needed, string Why, string Ref);

    /// <summary>
    /// Rule R-INDICATION: for the case's scenario a test the table calls "always" is appropriate; "not used" is not indicated; a "conditional" or "not routine" test is appropriate when the case states a result for it
    /// (the story calls for it); a "per policy" test (group and save) is appropriate and never missed. A needed test that is never ordered is missed at readiness.
    /// </summary>
    public sealed class IndicationEvaluator
    {
        private readonly InvestigationTables _t;

        public IndicationEvaluator(InvestigationTables tables) { _t = tables; }

        public IndicationResult Evaluate(string test, string scenario, ISet<string> testsWithCaseResults)
        {
            var row = _t.IndicationOf(test);
            if (row == null) return new IndicationResult(test, "NOT_INDICATED", false, "Not on the walkthrough's list of tests.", "");
            string kind = scenario == "S1" ? row.S1 : row.S2;
            bool stated = testsWithCaseResults.Contains(test);
            switch (kind)
            {
                case "always": return new IndicationResult(test, "APPROPRIATE", true, row.Why, row.Ref);
                case "per_policy": return new IndicationResult(test, "APPROPRIATE", false, row.Why, row.Ref);
                case "conditional":
                case "not_routine":
                    return new IndicationResult(test, stated ? "APPROPRIATE" : "NOT_INDICATED", stated, row.Why, row.Ref);
                default: return new IndicationResult(test, "NOT_INDICATED", false, row.Why, row.Ref);
            }
        }

        /// <summary>Every test this case needs: the "always" tests for its scenario and the conditional ones its story states a result for.</summary>
        public IReadOnlyList<string> Needed(string scenario, ISet<string> testsWithCaseResults) =>
            _t.Tests.Where(t => Evaluate(t.Id, scenario, testsWithCaseResults).Needed).Select(t => t.Id).ToList();
    }
}
