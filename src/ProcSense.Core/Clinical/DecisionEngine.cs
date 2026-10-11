using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    public sealed record DecisionOutcomeDef(string Id, string Code, string Label, string Meaning, string Ref);

    public sealed record DecisionCheckDef(string Id, string Outcome, string Text, string Ref);

    /// <summary>The master decision's emergency signs (walkthrough Step 8 diagram).</summary>
    public sealed record EmergencySigns(int News2Min, int SbpMax, int Spo2Max, bool NotAlert, IReadOnlyList<string> Flags, bool PositivePregnancyWithShock, string Ref);

    /// <summary>What the engine looks at: the scenario, the problems still open, and the signs of the patient now.</summary>
    public sealed record DecisionInput(string Scenario, IReadOnlyList<Problem> OpenProblems, int? News2Total, double SystolicBp, double Spo2, string Acvpu, IReadOnlyCollection<string> Flags,
        bool PregnancyPositive, bool InformationMissing, bool FastingBreach, int AsaMax);

    /// <summary>The expected outcome with the check that gave it, the plain reason and the evidence (problem ids and signs). Pace is the golden rule for a fixable problem: postpone (planned) or parallel (emergency).</summary>
    public sealed record DecisionResult(string Outcome, string Check, string Reason, IReadOnlyList<string> Evidence, string Pace, string Ref);

    /// <summary>The five outcomes and the ordered checks (content/decision/decision.json, built from the verified outcome table and the walkthrough's Step 8 diagram).</summary>
    public sealed class DecisionTables
    {
        public static readonly DecisionTables Empty = new DecisionTables(new List<DecisionOutcomeDef>(), new List<DecisionCheckDef>(), new EmergencySigns(0, 0, 0, false, new List<string>(), false, ""), new Dictionary<string, string>());

        public DecisionTables(IReadOnlyList<DecisionOutcomeDef> outcomes, IReadOnlyList<DecisionCheckDef> checks, EmergencySigns signs, IReadOnlyDictionary<string, string> pace)
        {
            Outcomes = outcomes; Checks = checks; Signs = signs; Pace = pace;
        }

        public IReadOnlyList<DecisionOutcomeDef> Outcomes { get; }
        public IReadOnlyList<DecisionCheckDef> Checks { get; }
        public EmergencySigns Signs { get; }
        public IReadOnlyDictionary<string, string> Pace { get; }

        public DecisionCheckDef Check(string id) => Checks.First(c => c.Id == id);

        public static DecisionTables Parse(string json, string source = "decision")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var outcomes = new List<DecisionOutcomeDef>();
            var rawOutcomes = root.Arr("outcomes");
            for (int i = 0; i < rawOutcomes.Count; i++)
            {
                var o = new JsonObj(rawOutcomes[i], JsonObj.Index(root.Sub("outcomes"), i));
                outcomes.Add(new DecisionOutcomeDef(o.Str("id"), o.Str("code"), o.Str("label"), o.Str("meaning"), o.Str("ref")));
                o.Finish();
            }
            var codes = new HashSet<string>(outcomes.Select(o => o.Code), StringComparer.Ordinal);
            foreach (var need in new[] { "SAFE", "INVESTIGATE", "OPTIMISE", "REVIEW", "EMERGENCY" })
                if (!codes.Contains(need)) throw new ContentException(root.Sub("outcomes") + ": the outcome " + need + " is missing");
            var checks = new List<DecisionCheckDef>();
            var rawChecks = root.Arr("checks");
            for (int i = 0; i < rawChecks.Count; i++)
            {
                var o = new JsonObj(rawChecks[i], JsonObj.Index(root.Sub("checks"), i));
                string outcome = o.Str("outcome");
                if (!codes.Contains(outcome)) throw new ContentException(o.Sub("outcome") + ": '" + outcome + "' is not an outcome");
                checks.Add(new DecisionCheckDef(o.Str("id"), outcome, o.Str("text"), o.Str("ref")));
                o.Finish();
            }
            foreach (var need in new[] { "emergency", "information_missing", "diagnosis_changed", "fixable_problem", "affects_anaesthesia", "safe" })
                if (checks.All(c => c.Id != need)) throw new ContentException(root.Sub("checks") + ": the check '" + need + "' is missing");
            var s = root.Obj("emergency_signs");
            var flags = new List<string>();
            foreach (var f in s.Arr("flags")) flags.Add(f as string ?? throw new ContentException(s.Sub("flags") + ": expected text"));
            var signs = new EmergencySigns((int)s.Int("news2_min"), (int)s.Int("sbp_max"), (int)s.Int("spo2_max"), s.Bool("not_alert"), flags, s.Bool("positive_pregnancy_with_shock"), s.Str("ref"));
            s.Finish();
            var pace = new SortedDictionary<string, string>(root.StringMap("pace"), StringComparer.Ordinal);
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new DecisionTables(outcomes, checks, signs, pace);
        }
    }

    /// <summary>
    /// The master decision, 'Is it safe to continue?' (walkthrough Step 8): the checks run in order and the first that fires gives the expected outcome. This only works out what the right answer is, for the score;
    /// it never decides for the trainee and is never shown in assessment. Run it again whenever information changes.
    /// </summary>
    public sealed class DecisionEngine
    {
        private readonly DecisionTables _t;

        public DecisionEngine(DecisionTables tables) { _t = tables; }

        public DecisionResult Evaluate(DecisionInput i)
        {
            var sign = _t.Signs;
            var evidence = new List<string>();
            if (i.News2Total.HasValue && i.News2Total.Value >= sign.News2Min) evidence.Add("news2>=" + sign.News2Min.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (i.SystolicBp <= sign.SbpMax) evidence.Add("top_bp<=" + sign.SbpMax.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (i.Spo2 <= sign.Spo2Max) evidence.Add("oxygen<=" + sign.Spo2Max.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (sign.NotAlert && i.Acvpu != "A") evidence.Add("not_alert");
            foreach (var f in sign.Flags) if (i.Flags.Contains(f)) evidence.Add(f);
            if (sign.PositivePregnancyWithShock && i.PregnancyPositive && i.SystolicBp <= sign.SbpMax) evidence.Add("positive_pregnancy_with_shock");
            evidence.AddRange(i.OpenProblems.Where(p => p.Type == "life_threat").Select(p => p.Id));
            if (evidence.Count > 0) return Result("emergency", "An emergency sign or a life-threatening problem is open: call for help, ABCDE and treat first.", evidence, "");

            if (i.InformationMissing) return Result("information_missing", "A result the case needs has not been opened: more information is needed before deciding.", new[] { "needed_results_unopened" }, "");

            var pathway = i.OpenProblems.Where(p => p.Type == "pathway_changing").Select(p => p.Id).ToList();
            if (pathway.Count > 0) return Result("diagnosis_changed", "The diagnosis looks different or complicated: senior review, change the pathway or investigate further.", pathway, "");

            var fixable = i.OpenProblems.Where(p => p.Type == "safety_blocker" || p.Type == "time_critical" || p.Type == "optimisable").Select(p => p.Id).ToList();
            if (fixable.Count > 0) return Result("fixable_problem", i.Scenario == "S1" ? "A fixable problem is open: optimise first (a planned case is usually deferred or re-timed)." : "A fixable problem is open: correct it in parallel while the team moves toward surgery.", fixable, _t.Pace.TryGetValue(i.Scenario, out var pace) ? pace : "");

            var anaesthesia = new List<string>();
            if (i.FastingBreach) anaesthesia.Add("fasting_breach");
            if (i.AsaMax >= 3) anaesthesia.Add("asa>=3");
            if (anaesthesia.Count > 0) return Result("affects_anaesthesia", "Something affects the anaesthetic: the anaesthetist reviews and plans.", anaesthesia, "");

            return Result("safe", "Nothing is left open: safe to continue to consent and preparation.", Array.Empty<string>(), "");
        }

        private DecisionResult Result(string check, string reason, IReadOnlyList<string> evidence, string pace)
        {
            var c = _t.Check(check);
            return new DecisionResult(c.Outcome, check, reason, evidence, pace, c.Ref);
        }
    }
}
