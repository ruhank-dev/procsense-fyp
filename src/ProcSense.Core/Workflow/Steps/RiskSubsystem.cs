using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Clinical.Scores;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Walkthrough Step 5: risk and anaesthetic assessment. The trainee proposes an ASA grade, completes the clot and bleeding form, calculates scores from the components they have, calls the
    /// anaesthetist and decides whether to proceed. The case states the grade it expects (never worked out here); the anaesthetist is always involved, agrees or corrects the grade as a
    /// teaching moment and agrees the plan, from approved lines only. Proceeding with an unresolved fasting breach is allowed on the tablet but is critical and blocks readiness.
    /// </summary>
    public sealed class RiskSubsystem : ISubsystem
    {
        public const string AssessRiskVerb = "assess_risk";
        public const string AssessVteVerb = "assess_vte";
        public const string ComputeScoreVerb = "compute_score";
        public const string ReviewVerb = "request_anaesthetic_review";
        public const string DecideReadyVerb = "decide_ready";
        public const string ExplainDelayVerb = "explain_delay";

        public const string MissingAsa = "MISSING_ASA";
        public const string BadAsa = "BAD_ASA";
        public const string HistoryAndExamFirst = "HISTORY_AND_EXAM_FIRST";
        public const string VteItemsMissing = "VTE_ITEMS_MISSING";
        public const string VteUnknownItem = "VTE_UNKNOWN_ITEM";
        public const string MissingScore = "MISSING_SCORE";
        public const string UnknownScore = "UNKNOWN_SCORE";
        public const string BadScoreInput = "BAD_SCORE_INPUT";
        public const string MissingOption = "MISSING_OPTION";
        public const string UnknownOption = "UNKNOWN_OPTION";

        public const string AsaProposedFlag = "risk.asa_proposed";
        public const string VteDoneFlag = "vte.done";
        public const string VtePlanFlag = "vte.plan_required";
        public const string AnaesthetistCalledFlag = "anaesthetist.called";
        public const string PlanAgreedFlag = "anaesthetic.plan_agreed";
        public const string ReadyDecidedFlag = "ready.decided";
        public const string BlockedFlag = "readiness.blocked";
        public const string DelayExplainedFlag = "delay.explained";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly ScoreCalculator _calc;
        private CaseFile? _file;
        private Session _s = null!;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public RiskSubsystem(ContentPackage content, CaseFile? caseFile = null)
        {
            _content = content; _fixed = caseFile; _calc = new ScoreCalculator(content.Scores);
        }

        public int Order => PipelineOrder.Clinical + 2;

        private ClinicalLayer Clinical => _s.Patient.Clinical;
        private RiskState Risk => Clinical.Risk;
        private RiskTables Tables => _content.Risk;

        public void Init(Session session)
        {
            _s = session;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
            foreach (var verb in new[] { AssessRiskVerb, AssessVteVerb, ComputeScoreVerb, ReviewVerb, DecideReadyVerb, ExplainDelayVerb }) session.Verbs.Register(verb, requiresIdentity: true, requiresPatient: verb == ExplainDelayVerb);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case AssessRiskVerb: AssessRisk(a, o); break;
                case AssessVteVerb: AssessVte(a, o); break;
                case ComputeScoreVerb: ComputeScore(a, o); break;
                case ReviewVerb: Review(o); break;
                case DecideReadyVerb: DecideReady(a, o); break;
                case ExplainDelayVerb: Clinical.SetFlag(DelayExplainedFlag); o.Effects.Add("delay_explained"); break;
            }
        }

        public void OnTick() { }

        private static string Grade(int n, bool emergency) => n.ToString(CultureInfo.InvariantCulture) + (emergency ? "E" : "");

        // ---------------------------------------------------------------- ASA (L5-01)

        private void AssessRisk(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("asa", out var text) || text.Length == 0) { o.Reject(MissingAsa); return; }
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int grade) || grade < 1 || grade > 6) { o.Reject(BadAsa); return; }
            bool emergency = a.Params.TryGetValue("emergency", out var e) && e == "true";
            // history and examination come first: the grade is a judgement about what has been found
            bool examined = Clinical.ExamTotal == 0 || Clinical.Findings.Any(f => !f.Region.StartsWith(RuleState.AirwayRegion, StringComparison.Ordinal));
            if (Clinical.RevealedTopics.Count == 0 || !examined) { o.Reject(HistoryAndExamFirst); return; }

            Risk.ProposedAsa = grade;
            Risk.ProposedEmergency = emergency;
            Clinical.SetFlag(AsaProposedFlag);
            var expected = _file?.Risk?.Asa;
            if (expected == null) { Risk.AsaClass = ""; o.Effects.Add("asa_proposed:" + Grade(grade, emergency)); o.Effects.Add("asa:no_expected"); return; }

            int diff = grade < expected.Min ? expected.Min - grade : grade > expected.Max ? grade - expected.Max : 0;
            string cls = diff <= Tables.CreditDifference ? "credit" : diff <= Tables.MinorDifference ? "minor" : "major";
            if (emergency != expected.Emergency && cls == "credit") cls = "minor";      // the E is part of the answer
            Risk.AsaClass = cls;
            o.Effects.Add("asa_proposed:" + Grade(grade, emergency));
            o.Effects.Add("asa:" + cls);
            o.Effects.Add("score:" + (cls == "credit" ? "credit.asa_correct" : cls == "minor" ? "minor.asa_close" : "major.asa_far"));      // a teaching moment: the anaesthetist corrects it, so the rubric gives it no penalty
        }

        // ---------------------------------------------------------------- clot and bleeding form (L5-02)

        private void AssessVte(SimAction a, ActionOutcome o)
        {
            a.Params.TryGetValue("items", out var text);
            var answers = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var part in (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || (kv[1] != "yes" && kv[1] != "no")) { o.Reject(VteUnknownItem); return; }
                if (!Tables.VteRiskFactors.Contains(kv[0]) && !Tables.VteBleedingFactors.Contains(kv[0])) { o.Reject(VteUnknownItem); return; }
                answers[kv[0]] = kv[1] == "yes";
            }
            if (Tables.VteRiskFactors.Concat(Tables.VteBleedingFactors).Any(i => !answers.ContainsKey(i))) { o.Reject(VteItemsMissing); return; }

            bool plan = Tables.VteRiskFactors.Any(i => answers[i]);
            Risk.BleedingRisk = Tables.VteBleedingFactors.Any(i => answers[i]);
            Risk.Vte = plan ? "plan_required" : "no_plan";
            Clinical.SetFlag(VteDoneFlag);
            if (plan) Clinical.SetFlag(VtePlanFlag);
            if (Risk.BleedingRisk) Clinical.SetFlag("vte.bleeding_risk");
            o.Effects.Add("vte:" + Risk.Vte);
            o.Effects.Add("vte_bleeding:" + (Risk.BleedingRisk ? "yes" : "no"));
        }

        // ---------------------------------------------------------------- scores (L5-03)

        private void ComputeScore(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("score", out var score) || score.Length == 0) { o.Reject(MissingScore); return; }
            if (!Tables.Scores.Contains(score)) { o.Reject(UnknownScore); return; }
            var facts = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in a.Params)
            {
                if (kv.Key == "score") continue;
                if (kv.Value == "true") facts[kv.Key] = true;
                else if (kv.Value == "false") facts[kv.Key] = false;
                else if (double.TryParse(kv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) facts[kv.Key] = d;
                else facts[kv.Key] = kv.Value;
            }

            string summary;
            IReadOnlyList<string> missing;
            try { (summary, missing) = Calculate(score, facts); }
            catch (ArgumentException) { o.Reject(BadScoreInput); return; }

            Risk.Scores[score] = summary;
            o.Effects.Add("score_calc:" + score + " " + summary);
            if (missing.Count == 0) { Clinical.SetFlag("score." + score + ".computed"); return; }
            foreach (var m in missing)
            {
                o.Effects.Add("missing:" + m);
                if (Tables.MissingPrompts.TryGetValue(m, out var prompt))
                {
                    o.Effects.Add("prompt_order:" + prompt.Test);
                    if (!Risk.Prompts.Contains(prompt.Text)) Risk.Prompts.Add(prompt.Text);
                }
            }
        }

        private (string summary, IReadOnlyList<string> missing) Calculate(string score, IReadOnlyDictionary<string, object?> f)
        {
            switch (score)
            {
                case "air":
                case "aas":
                case "alvarado":
                    {
                        var r = _calc.Point(score, f);
                        return (r.Complete ? r.Total.ToString(CultureInfo.InvariantCulture) + " " + r.Band : "incomplete: " + r.Total.ToString(CultureInfo.InvariantCulture) + " so far", r.Missing);
                    }
                case "stopbang":
                    {
                        var r = _calc.StopBang(f);
                        return (r.Complete ? r.Total.ToString(CultureInfo.InvariantCulture) + " " + r.Band : "incomplete", r.Missing);
                    }
                case "tg18_cholecystitis":
                    {
                        var r = _calc.Tg18Cholecystitis(f);
                        return ("diagnosis " + r.Diagnosis + (r.Grade.Length > 0 ? " grade " + r.Grade : ""), r.Missing);
                    }
                case "tg18_cholangitis":
                    {
                        var r = _calc.Tg18Cholangitis(f);
                        return ("diagnosis " + r.Diagnosis, r.Missing);
                    }
                case "asge":
                    {
                        var r = _calc.Asge(f);
                        return ("tier " + (r.Tier.Length > 0 ? r.Tier : "unknown"), r.Missing);
                    }
                default:
                    throw new ArgumentException("unknown score");
            }
        }

        // ---------------------------------------------------------------- the anaesthetist (always involved)

        private string Say(string intent, params (string key, string value)[] fill)
        {
            if (!Tables.Intents.TryGetValue(intent, out var line)) return "";
            foreach (var (key, value) in fill) line = line.Replace("{" + key + "}", value);
            return line;
        }

        private void Review(ActionOutcome o)
        {
            Clinical.SetFlag(AnaesthetistCalledFlag);
            var lines = new List<string>();
            void Add(string intent, string text) { if (text.Length > 0) { lines.Add(text); o.Effects.Add("npc:" + intent); } }

            if (!Risk.ProposedAsa.HasValue) Add("ask_asa", Say("ask_asa"));
            else
            {
                var expected = _file?.Risk?.Asa;
                if (expected == null) Add("asa_no_expected", Say("asa_no_expected"));
                else if (Risk.AsaClass == "credit") Add("asa_agreed", Say("asa_agreed", ("asa", Grade(Risk.ProposedAsa.Value, Risk.ProposedEmergency))));
                else
                {
                    string range = expected.Min == expected.Max ? expected.Min.ToString(CultureInfo.InvariantCulture) : expected.Min.ToString(CultureInfo.InvariantCulture) + " to " + expected.Max.ToString(CultureInfo.InvariantCulture);
                    Add("asa_corrected", Say("asa_corrected", ("asa", range + (expected.Emergency ? "E" : "")), ("why", expected.Why)));
                }

                var plan = _file?.Risk?.Plan;
                if (plan != null)
                {
                    Risk.Plan = plan;
                    Clinical.SetFlag(PlanAgreedFlag);
                    Add("plan_" + plan, Say("plan_" + plan));
                    o.Effects.Add("plan:" + plan);
                }
            }
            if (Clinical.HasFlag(FastingSubsystem.BreachFlag) && !Clinical.HasFlag("fasting.anaesthetist_informed"))
                Add("fasting_delay", Say("fasting_delay", ("time", Clinical.FastingSafeFrom)));
            if (!Clinical.HasFlag(VteDoneFlag) && Risk.ProposedAsa.HasValue) Add("vte_incomplete", Say("vte_incomplete"));
            Risk.AnaesthetistLines.Clear();
            Risk.AnaesthetistLines.AddRange(lines);
        }

        // ---------------------------------------------------------------- the readiness decision (L5-04, E5-A, E5-B)

        private void DecideReady(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("option", out var id) || id.Length == 0) { o.Reject(MissingOption); return; }
            var option = Tables.Options.FirstOrDefault(x => x.Id == id);
            if (option == null) { o.Reject(UnknownOption); return; }

            Risk.Decision = id;
            Clinical.SetFlag(ReadyDecidedFlag);
            o.Effects.Add("ready:" + id);
            if (!Clinical.HasFlag(AnaesthetistCalledFlag)) { Clinical.SetFlag("readiness.no_anaesthetist"); o.Effects.Add("major:anaesthetist_not_called"); o.Effects.Add("score:major.anaesthetist_not_called"); }
            if (!Clinical.HasFlag(VteDoneFlag)) { Clinical.SetFlag("readiness.vte_missing"); o.Effects.Add("major:vte_not_done"); o.Effects.Add("score:major.vte_not_assessed"); }

            bool breach = Clinical.HasFlag(FastingSubsystem.BreachFlag) && !Clinical.HasFlag("fasting.anaesthetist_informed");
            if (id == "proceed" && breach)
            {
                if (!Clinical.HasFlag("fasting.proceeded_despite_breach")) { Clinical.SetFlag("fasting.proceeded_despite_breach"); o.Effects.Add("score:critical.proceed_despite_fasting_breach"); }
                Clinical.SetFlag(BlockedFlag);
                Risk.Blocked = true;
                o.Effects.Add("critical:proceed_despite_fasting_breach");
                o.Effects.Add("blocked:fasting_breach_unresolved");
                var line = Say("fasting_unresolved");
                if (line.Length > 0) { Risk.AnaesthetistLines.Clear(); Risk.AnaesthetistLines.Add(line); o.Effects.Add("npc:fasting_unresolved"); }
            }
            else
            {
                Clinical.ClearFlag(BlockedFlag);
                Risk.Blocked = false;
            }

            if (option.Delay && !Clinical.HasFlag(DelayExplainedFlag) && !Clinical.HasFlag("delay.anxiety_applied"))
            {
                Clinical.SetFlag("delay.anxiety_applied");
                _s.Patient.Behavioural.Anxiety += Tables.DelayAnxiety;
                o.Effects.Add("anxiety:delay_not_explained");
            }
        }
    }
}
