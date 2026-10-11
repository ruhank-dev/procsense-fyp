using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProcSense.Core.Content
{
    /// <summary>Where raw content text comes from. Core ships a file reader; Unity supplies its own (StreamingAssets / Addressables).</summary>
    public interface IContentReader
    {
        /// <summary>Text of a file under content/, e.g. "cases/VAR-S1-A.json". Throws if it does not exist.</summary>
        string ReadText(string relativePath);
    }

    public sealed class FileContentReader : IContentReader
    {
        private readonly string _root;

        public FileContentReader(string contentRoot) { _root = contentRoot; }

        public string ReadText(string relativePath) => File.ReadAllText(Path.Combine(_root, relativePath));
    }

    /// <summary>Looks in several content folders in order (for example content/ and the test fixtures).</summary>
    public sealed class CompositeContentReader : IContentReader
    {
        private readonly IContentReader[] _readers;

        public CompositeContentReader(params IContentReader[] readers) { _readers = readers; }

        public string ReadText(string relativePath)
        {
            foreach (var r in _readers)
            {
                try { return r.ReadText(relativePath); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
            throw new FileNotFoundException("not found in any content folder: " + relativePath);
        }
    }

    /// <summary>Reads and strictly checks content files. Unknown fields, wrong types and clinical values without a source ref are errors.</summary>
    public static class ContentLoader
    {
        private static readonly string[] Topics =
            { "complaint", "red_flags", "past_history", "medicines", "herbal", "previous_anaesthetic", "family_anaesthetic", "smoking", "alcohol", "last_food", "last_drink", "pregnancy" };
        private static readonly string[] Wordings = { "quote", "narration" };
        private static readonly Regex Clock = new Regex("^([01][0-9]|2[0-3]):[0-5][0-9]$");
        private static readonly Regex RefId = new Regex("^SR-[0-9]{4}$");

        // Keys that describe structure, not clinical facts, so they need no source ref (same list as tools/content/checks.py).
        private static readonly HashSet<string> NonClinical = new HashSet<string> { "id", "scenario", "refs", "topic", "wording", "volunteered" };

        /// <summary>Loads the cards for <paramref name="caseIds"/> and, where one exists, the playable case file of the same id (the examination findings come from it).</summary>
        public static ContentPackage Load(IContentReader reader, params string[] caseIds) =>
            Load(reader, caseIds, caseIds.Where(id => HasCaseFile(reader, id)).ToList());

        private static bool HasCaseFile(IContentReader reader, string id)
        {
            try { reader.ReadText("case_files/" + id + ".json"); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        /// <summary>Loads the cards for <paramref name="caseIds"/> and, next to them, the playable case files for <paramref name="caseFileIds"/> (content/case_files/&lt;id&gt;.json).</summary>
        public static ContentPackage Load(IContentReader reader, IReadOnlyList<string> caseIds, IReadOnlyList<string> caseFileIds)
        {
            var cases = new SortedDictionary<string, CaseCard>(StringComparer.Ordinal);
            foreach (var id in caseIds) cases[id] = LoadCase(reader.ReadText("cases/" + id + ".json"), id);
            var files = new SortedDictionary<string, CaseFile>(StringComparer.Ordinal);
            foreach (var id in caseFileIds) files[id] = LoadCaseFile(reader, id);
            var behaviour = LoadBehaviour(reader.ReadText("behaviour.json"));
            return new ContentPackage(cases, LoadNibp(reader.ReadText("devices.json")), behaviour.Item1, behaviour.Item2, LoadHistoryTopics(reader.ReadText("history_topics.json")),
                Workflow.CaseGraph.Parse(reader.ReadText("workflow/workflow.json"), "workflow"), files,
                Workflow.FlowMaps.Parse(reader.ReadText("flow/flow_maps.json"), "flow"), behaviour.Item3,
                Clinical.Scores.ScoreTables.Parse(reader.ReadText("scores/scores.json"), "scores"),
                Clinical.FastingRules.Parse(reader.ReadText("fasting/fasting.json"), "fasting"),
                Clinical.RiskTables.Parse(reader.ReadText("risk/risk.json"), reader.ReadText("npc/anaesthetist_intents.json")),
                Clinical.News2Table.Parse(reader.ReadText("news2/news2.json"), "news2"),
                Location.LocationTables.Parse(reader.ReadText("locations/locations.json"), "locations"),
                Clinical.InvestigationTables.Parse(reader.ReadText("investigations/investigations.json"), reader.ReadText("investigations/indications.json"), reader.ReadText("investigations/normal_results.json"), reader.ReadText("investigations/critical.json")),
                Clinical.ImagingTables.Parse(reader.ReadText("imaging/imaging.json")),
                Clinical.ProblemTables.Parse(reader.ReadText("problems/problems.json")),
                Clinical.TreatmentTables.Parse(reader.ReadText("treatments.json"), "treatments"),
                Clinical.SepsisTables.Parse(reader.ReadText("sepsis/sepsis.json")),
                Clinical.ClashTables.Parse(reader.ReadText("clashes/clashes.json")),
                Clinical.DecisionTables.Parse(reader.ReadText("decision/decision.json")),
                Physiology.DeteriorationTables.Parse(reader.ReadText("physiology/deterioration.json")),
                Workflow.Steps.WardTables.Parse(reader.ReadText("ward/ward_checklist.json")),
                Workflow.OrTables.Parse(reader.ReadText("or/or_requirements.json")),
                Workflow.Steps.HandoverTables.Parse(reader.ReadText("handover/handover_required.json")),
                Workflow.Steps.SafetyChecklistTables.Parse(reader.ReadText("checklist/safety_checklist.json")),
                Workflow.EndStateTables.Parse(reader.ReadText("endstate/end_states.json")),
                Physiology.PhysioTables.Parse(reader.ReadText("physio/model.json"), reader.ReadText("physio/couplings.json"), reader.ReadText("physio/noise.json")),
                Behaviour.BehaviourTables.Parse(reader.ReadText("behaviour/transitions.json"), reader.ReadText("behaviour/modes.json"), reader.ReadText("behaviour/disclosure.json")));
        }

        /// <summary>One playable case file; the file name and the id inside must agree.</summary>
        public static CaseFile LoadCaseFile(IContentReader reader, string id)
        {
            var file = CaseFileLoader.Load(reader.ReadText("case_files/" + id + ".json"), "case_files/" + id);
            if (file.Id != id) throw new ContentException("case_files/" + id + ": the file holds the case " + file.Id);
            return file;
        }

        public static CaseCard LoadCase(string json, string name)
        {
            try { return ParseCase(MiniJson.Parse(json)); }
            catch (ContentException e) { throw new ContentException(name + ": " + e.Message); }
        }

        private static CaseCard ParseCase(object? node)
        {
            var c = new JsonObj(node, "");

            var id = c.Str("id");
            var label = c.Str("walkthrough_label");
            var scenario = c.Enum("scenario", "S1", "S2");

            var i = c.Obj("identity");
            var identity = new Identity(i.Str("title"), i.Str("full_name"), i.Int("age"), i.Str("occupation"), i.Str("dob"), i.Str("mrn"));
            i.Finish();

            var spoken = c.Str("spoken_identity_answer");

            var w = c.ObjOrNull("wristband"); // null = no band on the patient's wrist
            Wristband? wristband = null;
            if (w != null)
            {
                wristband = new Wristband(w.Str("name_text"), w.Str("dob_text"), w.Str("mrn_text"));
                w.Finish();
            }

            var n = c.Obj("notes");
            var notes = new NotesIdentifiers(n.Str("full_name"), n.Str("dob"), n.Str("mrn"));
            n.Finish();

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

            var r = c.Obj("responsible_adult");
            var adult = new ResponsibleAdult(r.Str("who"), r.Bool("takes_home"), r.Bool("stays_overnight"), r.Str("text"), r.Enum("wording", Wordings));
            r.Finish();

            var t = c.Obj("timing");
            var timing = new Timing(ClockTime(t, "time_now"), ClockTime(t, "surgery_time"));
            t.Finish();

            var history = new List<HistoryFact>();
            var historyList = c.Arr("history");
            for (int k = 0; k < historyList.Count; k++)
            {
                var h = new JsonObj(historyList[k], JsonObj.Index("history", k));
                string? clock = h.Has("clock_time") ? ClockText(h, "clock_time") : null;
                history.Add(new HistoryFact(h.Enum("topic", Topics), h.Str("answer"), h.Enum("wording", Wordings), h.Bool("volunteered"), clock));
                h.Finish();
            }

            var v = c.Obj("baseline_vitals");
            var vitals = new Vitals(
                v.Num("heart_rate"), v.Enum("heart_rhythm", "regular", "irregular"), v.Num("systolic_bp"), v.Num("diastolic_bp"),
                v.Num("respiratory_rate"), v.Num("spo2"), v.Enum("oxygen", "room_air", "supplemental"), v.Num("temperature_c"),
                v.Enum("acvpu", "A", "C", "V", "P", "U"), PainScore(v));
            v.Finish();

            var refs = c.StringMap("refs", RefId, "a register ID like SR-0001");
            c.Finish();
            CheckRefCoverage(node); // after the structure checks so an extra field reports as "unknown field" first

            return new CaseCard(id, label, scenario, identity, spoken, wristband, notes, allergies, allergyAnswer, reason, adult, timing, history, vitals, refs);
        }

        private static long PainScore(JsonObj v)
        {
            long p = v.Int("pain_score");
            if (p < 0 || p > 10) throw new ContentException(v.Sub("pain_score") + ": must be 0 to 10");
            return p;
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

        private static (BehaviourEffect, SummariseEffect, ExamEffects) LoadBehaviour(string json)
        {
            try
            {
                var d = new JsonObj(MiniJson.Parse(json), "");
                var b = d.Obj("introduce_self");
                var intro = new BehaviourEffect(b.Int("trust"), b.Int("anxiety"), b.Str("ref"));
                b.Str("note");
                b.Finish();
                var m = d.Obj("summarise");
                var summarise = new SummariseEffect(m.Int("min_topics"), m.Int("trust"), m.Int("understanding"), m.Str("ref"));
                m.Str("note");
                m.Finish();
                var np = d.Obj("exam_without_permission");
                var pf = d.Obj("exam_painful_first");
                var mp = d.Obj("murphy_positive");
                var exam = new ExamEffects(np.Int("trust"), np.Str("reaction"), pf.Int("pain"), pf.Int("trust"), pf.Str("reaction"), mp.Str("reaction"));
                foreach (var o in new[] { np, pf, mp }) { o.Str("note"); o.Str("ref"); o.Finish(); }
                d.Finish();
                return (intro, summarise, exam);
            }
            catch (ContentException e) { throw new ContentException("behaviour.json: " + e.Message); }
        }

        private static HistoryTopics LoadHistoryTopics(string json)
        {
            try
            {
                var d = new JsonObj(MiniJson.Parse(json), "");
                var t = d.Obj("topics");
                var ids = Strings(t, "ids");
                var sensitive = Strings(t, "sensitive");
                foreach (var s in sensitive)
                    if (!ids.Contains(s)) throw new ContentException("topics.sensitive: '" + s + "' is not in topics.ids");
                var topics = new HistoryTopics(ids, sensitive, t.Str("ref"));
                t.Str("note");
                t.Finish();
                d.Finish();
                return topics;
            }
            catch (ContentException e) { throw new ContentException("history_topics.json: " + e.Message); }
        }

        private static List<string> Strings(JsonObj o, string key)
        {
            var result = new List<string>();
            foreach (var item in o.Arr(key))
                result.Add(item as string ?? throw new ContentException(o.Sub(key) + ": expected a list of text"));
            return result;
        }

        private static NibpTiming LoadNibp(string json)
        {
            try
            {
                var d = new JsonObj(MiniJson.Parse(json), "");
                var n = d.Obj("nibp");
                var nibp = new NibpTiming(n.Num("cycle_seconds_min"), n.Num("cycle_seconds_max"), n.Str("ref"));
                n.Str("note"); // free text, kept in the file for reviewers
                n.Finish();
                d.Finish();
                return nibp;
            }
            catch (ContentException e) { throw new ContentException("devices.json: " + e.Message); }
        }

        // Every clinical value needs a source ref on its own path or an ancestor (mirrors tools/content/checks.py).
        private static void CheckRefCoverage(object? node)
        {
            var root = node as Dictionary<string, object?> ?? throw new ContentException("<root>: expected an object");
            var refs = root.TryGetValue("refs", out var rn) ? rn as Dictionary<string, object?> : null;
            if (refs == null) throw new ContentException("refs: missing field");
            var keys = refs.Keys.ToList();
            foreach (var path in LeafPaths(root, "").OrderBy(p => p, StringComparer.Ordinal))
                if (!keys.Any(r => path == r || path.StartsWith(r + ".", StringComparison.Ordinal) || path.StartsWith(r + "[", StringComparison.Ordinal)))
                    throw new ContentException("no source reference for '" + path + "'");
        }

        private static IEnumerable<string> LeafPaths(object? node, string prefix)
        {
            if (node is Dictionary<string, object?> d && d.Count > 0)
            {
                foreach (var kv in d.Where(kv => !NonClinical.Contains(kv.Key)))
                    foreach (var p in LeafPaths(kv.Value, prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key)) yield return p;
            }
            else if (node is List<object?> l && l.Count > 0)
            {
                for (int k = 0; k < l.Count; k++)
                    foreach (var p in LeafPaths(l[k], JsonObj.Index(prefix, k))) yield return p;
            }
            else yield return prefix;
        }
    }
}
