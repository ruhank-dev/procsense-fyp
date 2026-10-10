using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>One test the trainee can order: what it is (blood, bedside or imaging), which tube a blood test goes in, where imaging is done, the analytes of the case's results that belong to it, and its default turnaround.</summary>
    public sealed record InvestigationDef(string Id, string Label, string Kind, string Tube, string Location, IReadOnlyList<string> Analytes, double MinMinutes, double MaxMinutes, string Ref);

    /// <summary>When a test is indicated: its kind for a planned (S1) and an emergency (S2) patient, and why (always, conditional, not_routine, per_policy or not_used).</summary>
    public sealed record IndicationRow(string Test, string S1, string S2, string Why, string WhenS1, string WhenS2, string Ref);

    /// <summary>The tests, tubes, default turnaround and indication kinds, loaded from content/investigations (investigations.json by hand, indications.json built from the verified table).</summary>
    public sealed class InvestigationTables
    {
        public static readonly InvestigationTables Empty = new InvestigationTables(new List<InvestigationDef>(), new List<string>(), new List<IndicationRow>(), "", 0);

        private static readonly Regex Parenthetical = new Regex(@"\s*\(.*\)\s*$", RegexOptions.CultureInvariant);

        public InvestigationTables(IReadOnlyList<InvestigationDef> tests, IReadOnlyList<string> tubes, IReadOnlyList<IndicationRow> indications, string delayEvent, double delayMinutes)
        {
            Tests = tests; Tubes = tubes; Indications = indications; DelayEvent = delayEvent; DelayMinutes = delayMinutes;
        }

        public IReadOnlyList<InvestigationDef> Tests { get; }
        public IReadOnlyList<string> Tubes { get; }
        public IReadOnlyList<IndicationRow> Indications { get; }
        /// <summary>The event name that extends every test still processing, and by how many minutes.</summary>
        public string DelayEvent { get; }
        public double DelayMinutes { get; }

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

        public static InvestigationTables Parse(string investigationsJson, string indicationsJson)
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
            var delay = root.Obj("delay");
            string delayEvent = delay.Str("event");
            double delayMinutes = delay.Num("minutes");
            if (delayMinutes < 0) throw new ContentException(delay.Sub("minutes") + ": cannot be negative");
            delay.Str("ref"); delay.Finish();
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Str("note");
            root.Finish();

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
            return new InvestigationTables(tests, tubes, rows, delayEvent, delayMinutes);
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
