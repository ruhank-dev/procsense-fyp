using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProcSense.Core.Content
{
    /// <summary>
    /// Reads and strictly checks a playable case file (content/case_files/*.json). Same rules as the card loader: an unknown field, a wrong type or a value outside its list is an error that names the path.
    /// It also repeats the checks that can be made without any table, so a hand-edited or broken file is refused here as well as in CI:
    /// the age against the date of birth, one volunteered complaint, hidden facts with a trigger, ranks 1 to n, every decision with a right and a wrong option, a first 'initial' observation, unique ids.
    /// What needs the content tables (NEWS2 recomputed, lab flags against ranges, decision-rule ids, verification ids) stays with tools/content/case_file_checks.py.
    /// </summary>
    public static class CaseFileLoader
    {
        private static readonly string[] Origins = { "book", "authored" };
        private static readonly string[] Tiers = { "P0", "P1", "P2" };
        private static readonly string[] Statuses = { "DRAFT", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT" };
        private static readonly string[] Conditions = { "initial", "after_correct_action", "if_no_action", "variant" };
        private static readonly string[] EndStates = { "ready", "postponed", "redirected", "delayed", "failure" };
        private static readonly string[] Flags = { "normal", "high", "low", "critical_high", "critical_low", "abnormal", "positive", "negative" };
        private static readonly string[] Qualities = { "haemolysed", "drip_arm", "wrong_tube_edta", "clotted", "lipaemic" };
        private static readonly string[] ProblemTypes = { "life_threat", "safety_blocker", "time_critical", "pathway_changing", "optimisable", "information_only" };
        private static readonly string[] Settings = { "ward", "pre-assessment", "emergency department" };
        private static readonly Regex CaseId = new Regex("^((VAR|STK)-S[12]-[A-Z]+|SC-[0-9]+)$");
        private static readonly Regex Clock = new Regex("^([01][0-9]|2[0-3]):[0-5][0-9]$");
        private static readonly Regex Verification = new Regex("^V-[0-9]{2}$");
        private static readonly Regex RefId = new Regex("^SR-[0-9]{4}$");
        private static readonly Regex IsoDate = new Regex("^([0-9]{4})-([0-9]{2})-([0-9]{2})$");

        public static CaseFile Load(string json, string name)
        {
            try
            {
                var file = Parse(MiniJson.Parse(json));
                Check(file);
                return file;
            }
            catch (ContentException e) { throw new ContentException(name + ": " + e.Message); }
        }

        private static CaseFile Parse(object? node)
        {
            var c = new JsonObj(node, "");
            if (c.Int("schema") != 1) throw new ContentException("schema: only version 1 is supported");
            var id = c.Str("id");
            if (!CaseId.IsMatch(id)) throw new ContentException("id: '" + id + "' is not a case id like VAR-S1-A, STK-S2-B or SC-4");
            var label = c.Str("walkthrough_label");
            var scenario = c.Enum("scenario", "S1", "S2");
            var tier = c.Enum("tier", Tiers);
            if (!c.Bool("synthetic")) throw new ContentException("synthetic: every case patient is synthetic (must be true)");
            var status = c.Enum("status", Statuses);

            CaseFileApproval? approval = null;
            if (c.Has("approval"))
            {
                var a = c.Obj("approval");
                approval = new CaseFileApproval(a.Str("by"), a.Str("date"), a.Str("note"));
                a.Finish();
            }
            if (status != "DRAFT" && approval == null) throw new ContentException("approval: a case with status " + status + " must say who approved it");

            var referenceDate = c.Str("reference_date");
            string? baseCard = c.Has("base_card") ? c.Any("base_card") as string : null;

            var i = c.Obj("identity");
            var identity = new CaseFileIdentity(i.Str("title"), i.Str("full_name"), i.Enum("sex", "F", "M"), i.Int("age"), i.Str("occupation"), i.Str("dob"), i.Str("mrn"));
            i.Finish();

            var spoken = c.Str("spoken_identity_answer");
            var w = c.ObjOrNull("wristband");
            Wristband? wristband = null;
            if (w != null)
            {
                wristband = new Wristband(w.Str("name_text"), w.Str("dob_text"), w.Str("mrn_text"));
                w.Finish();
            }

            var allergies = new List<Allergy>();
            var allergyList = c.Arr("allergies");
            for (int k = 0; k < allergyList.Count; k++)
            {
                var a = new JsonObj(allergyList[k], JsonObj.Index("allergies", k));
                allergies.Add(new Allergy(a.Str("substance"), a.Str("reaction")));
                a.Finish();
            }
            var allergyAnswer = c.Str("allergy_answer");
            var reason = c.Str("reason_answer");

            var p = c.Obj("presentation");
            var setting = p.Enum("setting", Settings);
            var timeNow = ClockTime(p, "time_now");
            TimeSpan? surgery = p.Has("surgery_time") ? ClockTime(p, "surgery_time") : (TimeSpan?)null;
            var presentation = new CaseFilePresentation(setting, timeNow, surgery, p.Str("summary"));
            p.Finish();

            var history = new List<CaseFileHistory>();
            var historyList = c.Arr("history");
            for (int k = 0; k < historyList.Count; k++)
            {
                var h = new JsonObj(historyList[k], JsonObj.Index("history", k));
                history.Add(new CaseFileHistory(h.Str("id"), h.Str("topic"), h.Str("answer"), h.Bool("volunteered"), Strings(h, "disclose_on"), h.Enum("origin", Origins), Optional(h, "variant")));
                h.Finish();
            }

            var observations = new List<CaseFileObservation>();
            var obsList = c.Arr("observations");
            for (int k = 0; k < obsList.Count; k++)
            {
                var o = new JsonObj(obsList[k], JsonObj.Index("observations", k));
                long pain = o.Int("pain_score");
                if (pain < 0 || pain > 10) throw new ContentException(o.Sub("pain_score") + ": must be 0 to 10");
                long news2 = o.Int("news2");
                if (news2 < 0) throw new ContentException(o.Sub("news2") + ": must not be negative");
                observations.Add(new CaseFileObservation(
                    ClockTime(o, "time"), o.Enum("condition", Conditions), o.Num("heart_rate"), o.Enum("heart_rhythm", "regular", "irregular"), o.Num("systolic_bp"), o.Num("diastolic_bp"),
                    o.Num("respiratory_rate"), o.Num("spo2"), o.Enum("oxygen", "room_air", "supplemental"), o.Num("temperature_c"), o.Enum("acvpu", "A", "C", "V", "P", "U"), pain, news2,
                    o.Str("note"), o.Enum("origin", Origins), Optional(o, "variant")));
                o.Finish();
            }

            var examination = new List<CaseFileExamination>();
            var exList = c.Arr("examination");
            for (int k = 0; k < exList.Count; k++)
            {
                var e = new JsonObj(exList[k], JsonObj.Index("examination", k));
                examination.Add(new CaseFileExamination(e.Str("id"), e.Str("system"), e.Str("finding"), e.Bool("abnormal"), Strings(e, "disclose_on"), e.Enum("origin", Origins),
                    e.Has("painful") && e.Bool("painful"), e.Has("flags") ? Strings(e, "flags") : new List<string>()));
                e.Finish();
            }

            var results = new List<CaseFileResult>();
            var resList = c.Arr("results");
            for (int k = 0; k < resList.Count; k++)
            {
                var r = new JsonObj(resList[k], JsonObj.Index("results", k));
                string? quality = null;
                if (r.Has("quality") && r.Any("quality") != null) quality = r.Enum("quality", Qualities);
                PreviousResult? previous = null;
                var pv = r.ObjOrNull("previous");
                if (pv != null)
                {
                    previous = new PreviousResult(pv.Str("value"), pv.Str("when"));
                    pv.Finish();
                }
                string? sourceRow = null;
                if (r.Has("source_row"))
                {
                    sourceRow = r.Str("source_row");
                    if (!RefId.IsMatch(sourceRow)) throw new ContentException(r.Sub("source_row") + ": expected a register ID like SR-0001");
                }
                long minutes = r.Int("minutes_to_result");
                if (minutes < 0) throw new ContentException(r.Sub("minutes_to_result") + ": must not be negative");
                results.Add(new CaseFileResult(r.Str("id"), r.Str("test"), r.Str("value"), r.Str("unit"), r.Str("normal_range"), r.Enum("flag", Flags), r.Str("comment"), quality, previous, minutes,
                    r.Enum("origin", Origins), sourceRow, Optional(r, "variant")));
                // 'quality' may be present as null: it was marked used by Has/Any above, so Finish() accepts it
                r.Finish();
            }

            var scores = new List<CaseFileScore>();
            var scoreList = c.Arr("scores");
            for (int k = 0; k < scoreList.Count; k++)
            {
                var s = new JsonObj(scoreList[k], JsonObj.Index("scores", k));
                scores.Add(new CaseFileScore(s.Str("name"), s.Str("value"), s.Str("note"), s.Enum("origin", Origins)));
                s.Finish();
            }

            var problems = new List<CaseFileProblem>();
            var probList = c.Arr("problems");
            for (int k = 0; k < probList.Count; k++)
            {
                var q = new JsonObj(probList[k], JsonObj.Index("problems", k));
                problems.Add(new CaseFileProblem(q.Str("id"), q.Enum("type", ProblemTypes), q.Str("label"), q.Int("rank"), q.Str("why"), Strings(q, "expected_actions")));
                q.Finish();
            }

            var decisions = new List<CaseFileDecision>();
            var decList = c.Arr("decisions");
            for (int k = 0; k < decList.Count; k++)
            {
                var d = new JsonObj(decList[k], JsonObj.Index("decisions", k));
                string? rule = d.Any("rule") as string;
                if (rule == null && d.Any("rule") != null) throw new ContentException(d.Sub("rule") + ": expected text or null");
                var options = new List<CaseFileOption>();
                var optList = d.Arr("options");
                for (int m = 0; m < optList.Count; m++)
                {
                    var op = new JsonObj(optList[m], JsonObj.Index(d.Sub("options"), m));
                    options.Add(new CaseFileOption(op.Str("id"), op.Str("text"), op.Bool("correct"), op.Str("consequence")));
                    op.Finish();
                }
                decisions.Add(new CaseFileDecision(d.Str("id"), d.Str("prompt"), rule, options, Optional(d, "variant")));
                d.Finish();
            }

            var endState = c.Enum("expected_end_state", EndStates);
            var variantEnds = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (c.Has("variant_end_states"))
                foreach (var kv in c.StringMap("variant_end_states"))
                {
                    if (!EndStates.Contains(kv.Value)) throw new ContentException("variant_end_states." + kv.Key + ": '" + kv.Value + "' is not one of " + string.Join(", ", EndStates));
                    variantEnds[kv.Key] = kv.Value;
                }

            var pa = c.Obj("paths");
            var incorrect = new List<IncorrectPath>();
            var incList = pa.Arr("incorrect");
            for (int k = 0; k < incList.Count; k++)
            {
                var ip = new JsonObj(incList[k], JsonObj.Index("paths.incorrect", k));
                incorrect.Add(new IncorrectPath(ip.Str("mistake"), ip.Str("shows"), ip.Str("risk"), ip.Str("recovery")));
                ip.Finish();
            }
            var paths = new CaseFilePaths(Strings(pa, "correct"), incorrect, Strings(pa, "missed"), Strings(pa, "recovery"));
            pa.Finish();

            var db = c.Obj("debrief");
            var debrief = new CaseFileDebrief(Strings(db, "recognised"), Strings(db, "missed"), db.Str("priority"), db.Str("why_priority"), db.Str("related"), db.Str("independent"),
                db.Str("trigger_escalation"), db.Str("if_ignored"), db.Str("remember"));
            db.Finish();

            var verification = Strings(c, "verification");
            foreach (var v in verification)
                if (!Verification.IsMatch(v)) throw new ContentException("verification: '" + v + "' is not a claim id like V-01");

            var so = c.Obj("sources");
            var story = so.Str("story");
            var authored = so.Str("authored");
            foreach (var r in new[] { story, authored })
                if (!RefId.IsMatch(r)) throw new ContentException("sources: '" + r + "' is not a register ID like SR-0001");
            var measurements = so.Has("measurements") ? Strings(so, "measurements") : new List<string>();
            foreach (var r in measurements)
                if (!RefId.IsMatch(r)) throw new ContentException("sources.measurements: '" + r + "' is not a register ID like SR-0001");
            so.Finish();

            c.Finish();
            return new CaseFile(id, label, scenario, tier, status, approval, referenceDate, baseCard, identity, spoken, wristband, allergies, allergyAnswer, reason, presentation, history, observations,
                examination, results, scores, problems, decisions, endState, variantEnds, paths, debrief, verification, new CaseFileSources(story, authored, measurements));
        }

        // The checks that need no table: they hold for any case file.
        private static void Check(CaseFile f)
        {
            if (f.Scenario == "S1" && f.Presentation.SurgeryTime == null) throw new ContentException("presentation: an S1 case has a booked surgery time");

            // age against the date of birth on the reference date (whole years)
            var (ry, rm, rd) = Date("reference_date", f.ReferenceDate);
            var (by, bm, bd) = Date("identity.dob", f.Identity.Dob);
            long age = ry - by - ((rm, rd).CompareTo((bm, bd)) < 0 ? 1 : 0);
            if (age != f.Identity.Age) throw new ContentException("identity.age: " + f.Identity.Age.ToString(CultureInfo.InvariantCulture) + " does not match the date of birth " + f.Identity.Dob + " on " + f.ReferenceDate);

            if (f.Wristband != null)
            {
                var wb = f.Wristband;
                string dobText = bd.ToString("00", CultureInfo.InvariantCulture) + "/" + bm.ToString("00", CultureInfo.InvariantCulture) + "/" + by.ToString("0000", CultureInfo.InvariantCulture);
                if (wb.NameText != f.Identity.FullName.ToUpperInvariant() || wb.DobText != dobText || wb.MrnText != "MR " + f.Identity.Mrn)
                    throw new ContentException("wristband: the text does not match the identity");
            }
            if (!f.SpokenIdentityAnswer.Contains(f.Identity.FullName)) throw new ContentException("spoken_identity_answer: must contain the full name");

            Unique("history", f.History.Select(h => h.Id));
            var volunteered = f.History.Where(h => h.Volunteered).ToList();
            if (volunteered.Count != 1 || volunteered[0].Topic != "complaint") throw new ContentException("history: exactly one fact is volunteered and it is the complaint");
            foreach (var h in f.History)
                if (!h.Volunteered && h.DiscloseOn.Count == 0) throw new ContentException("history." + h.Id + ": a hidden fact needs at least one disclose_on trigger");

            if (f.Observations[0].Condition != "initial") throw new ContentException("observations: the first one must be the initial one");
            Unique("examination", f.Examination.Select(e => e.Id));
            Unique("results", f.Results.Select(r => r.Id));
            Unique("problems", f.Problems.Select(p => p.Id));
            Unique("decisions", f.Decisions.Select(d => d.Id));

            var ranks = f.Problems.Select(p => p.Rank).OrderBy(x => x).ToList();
            for (int k = 0; k < ranks.Count; k++)
                if (ranks[k] != k + 1) throw new ContentException("problems: ranks must be 1 to " + ranks.Count.ToString(CultureInfo.InvariantCulture) + " without gaps");

            foreach (var d in f.Decisions)
            {
                if (!d.Options.Any(o => o.Correct) || d.Options.All(o => o.Correct)) throw new ContentException("decisions." + d.Id + ": needs at least one correct and one wrong option");
                Unique("decisions." + d.Id + ".options", d.Options.Select(o => o.Id));
            }
            if (f.Status == "APPROVED_FOR_ASSESSMENT" && f.Tier != "P0") throw new ContentException("status: only a P0 case can be approved for assessment");
        }

        private static (int, int, int) Date(string where, string text)
        {
            var m = IsoDate.Match(text);
            if (!m.Success) throw new ContentException(where + ": '" + text + "' is not a date like 2026-10-10");
            int y = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), mo = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), d = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (mo < 1 || mo > 12 || d < 1 || d > 31) throw new ContentException(where + ": '" + text + "' is not a real date");
            return (y, mo, d);
        }

        private static void Unique(string where, IEnumerable<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (!seen.Add(id)) throw new ContentException(where + ": the id '" + id + "' is used twice");
        }

        private static string? Optional(JsonObj o, string key) => o.Has(key) ? o.Str(key) : null;

        private static List<string> Strings(JsonObj o, string key)
        {
            var result = new List<string>();
            foreach (var item in o.Arr(key))
                result.Add(item as string ?? throw new ContentException(o.Sub(key) + ": expected a list of text"));
            return result;
        }

        private static string ClockText(JsonObj o, string key)
        {
            string s = o.Str(key);
            if (!Clock.IsMatch(s)) throw new ContentException(o.Sub(key) + ": '" + s + "' is not a HH:MM time");
            return s;
        }

        private static TimeSpan ClockTime(JsonObj o, string key)
        {
            string s = ClockText(o, key);
            return new TimeSpan(int.Parse(s.Substring(0, 2), CultureInfo.InvariantCulture), int.Parse(s.Substring(3, 2), CultureInfo.InvariantCulture), 0);
        }
    }
}
