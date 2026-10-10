using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    public sealed record ReadinessOption(string Id, string Label, bool Delay);
    public sealed record MissingPrompt(string Test, string Text);

    /// <summary>
    /// Step 5 content: how far off an ASA proposal may be, the anxiety a delay nobody explained causes, the five readiness decisions, the clot and bleeding risk items, the scores the tablet can
    /// calculate, which test answers each missing score item, and the anaesthetist's approved lines. Loaded from content/risk/risk.json and content/npc/anaesthetist_intents.json.
    /// </summary>
    public sealed class RiskTables
    {
        public static readonly RiskTables Empty = new RiskTables(0, 1, 0, new List<ReadinessOption>(), new List<string>(), new List<string>(), new List<string>(),
            new Dictionary<string, MissingPrompt>(), new Dictionary<string, string>());

        public RiskTables(int creditDifference, int minorDifference, int delayAnxiety, IReadOnlyList<ReadinessOption> options, IReadOnlyList<string> vteRiskFactors, IReadOnlyList<string> vteBleedingFactors,
            IReadOnlyList<string> scores, IReadOnlyDictionary<string, MissingPrompt> missingPrompts, IReadOnlyDictionary<string, string> intents)
        {
            CreditDifference = creditDifference; MinorDifference = minorDifference; DelayAnxiety = delayAnxiety; Options = options; VteRiskFactors = vteRiskFactors; VteBleedingFactors = vteBleedingFactors;
            Scores = scores; MissingPrompts = missingPrompts; Intents = intents;
        }

        /// <summary>A proposal this far (or less) from the case's range is credited.</summary>
        public int CreditDifference { get; }
        /// <summary>A proposal this far (or less) is a minor teaching point; further is major.</summary>
        public int MinorDifference { get; }
        public int DelayAnxiety { get; }
        public IReadOnlyList<ReadinessOption> Options { get; }
        public IReadOnlyList<string> VteRiskFactors { get; }
        public IReadOnlyList<string> VteBleedingFactors { get; }
        public IReadOnlyList<string> Scores { get; }
        public IReadOnlyDictionary<string, MissingPrompt> MissingPrompts { get; }
        public IReadOnlyDictionary<string, string> Intents { get; }

        public static RiskTables Parse(string riskJson, string intentsJson)
        {
            var root = new JsonObj(MiniJson.Parse(riskJson), "risk");
            var diff = root.Obj("asa_difference");
            int credit = (int)diff.Int("credit"), minor = (int)diff.Int("minor");
            if (credit < 0 || minor < credit) throw new ContentException(diff.Sub("minor") + ": must not be less than 'credit', which must not be negative");
            diff.Str("note"); diff.Str("ref"); diff.Finish();
            var delay = root.Obj("delay_unexplained");
            int anxiety = (int)delay.Int("anxiety");
            delay.Str("note"); delay.Str("ref"); delay.Finish();

            var options = new List<ReadinessOption>();
            var rawOptions = root.Arr("readiness_options");
            for (int i = 0; i < rawOptions.Count; i++)
            {
                var o = new JsonObj(rawOptions[i], JsonObj.Index(root.Sub("readiness_options"), i));
                string id = o.Str("id");
                if (options.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate option '" + id + "'");
                options.Add(new ReadinessOption(id, o.Str("label"), o.Bool("delay")));
                o.Finish();
            }
            if (options.Count > 0 && !options.Any(o => o.Id == "proceed" && !o.Delay)) throw new ContentException(root.Sub("readiness_options") + ": needs a 'proceed' option that is not a delay");

            var vte = root.Obj("vte");
            var risk = Items(vte, "risk_factors");
            var bleeding = Items(vte, "bleeding_factors");
            if (risk.Intersect(bleeding).Any()) throw new ContentException(vte.Sub("bleeding_factors") + ": an item cannot be both a clot and a bleeding factor");
            Refs(vte); vte.Finish();

            var scores = new List<string>();
            foreach (var s in root.Arr("scores")) scores.Add(s as string ?? throw new ContentException(root.Sub("scores") + ": expected score names"));

            var prompts = new SortedDictionary<string, MissingPrompt>(StringComparer.Ordinal);
            var rawPrompts = root.Any("missing_prompts") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("missing_prompts") + ": expected an object");
            foreach (var kv in rawPrompts.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var m = new JsonObj(kv.Value, root.Sub("missing_prompts") + "." + kv.Key);
                prompts[kv.Key] = new MissingPrompt(m.Str("test"), m.Str("text"));
                m.Finish();
            }
            root.Str("note");
            Refs(root);
            root.Finish();

            var intents = ParseIntents(intentsJson);
            return new RiskTables(credit, minor, anxiety, options, risk, bleeding, scores, prompts, intents);
        }

        private static List<string> Items(JsonObj o, string key)
        {
            var raw = o.Arr(key);
            var ids = new List<string>();
            for (int i = 0; i < raw.Count; i++)
            {
                var it = new JsonObj(raw[i], JsonObj.Index(o.Sub(key), i));
                string id = it.Str("id");
                if (ids.Contains(id)) throw new ContentException(it.Sub("id") + ": duplicate item '" + id + "'");
                it.Str("label"); it.Finish();
                ids.Add(id);
            }
            return ids;
        }

        private static void Refs(JsonObj o)
        {
            var refs = o.Arr("refs");
            if (refs.Count == 0) throw new ContentException(o.Sub("refs") + ": needs at least one register id");
            foreach (var r in refs) if (!(r is string)) throw new ContentException(o.Sub("refs") + ": expected register ids");
        }

        private static IReadOnlyDictionary<string, string> ParseIntents(string json)
        {
            var root = new JsonObj(MiniJson.Parse(json), "anaesthetist_intents");
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var raw = root.Arr("intents");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("intents"), i));
                string id = o.Str("id");
                if (map.ContainsKey(id)) throw new ContentException(o.Sub("id") + ": duplicate intent '" + id + "'");
                string line = o.Str("line");
                if (line.Trim().Length == 0) throw new ContentException(o.Sub("line") + ": a line cannot be empty");
                o.Str("ref"); o.Finish();
                map[id] = line;
            }
            root.Str("note"); Refs(root); root.Finish();
            return map;
        }
    }
}
