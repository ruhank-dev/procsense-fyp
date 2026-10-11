using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.AIContext
{
    /// <summary>The fixed vocabulary around AI-voiced lines: the intents each character may use (content/ai/intents.json) and the lexicon of drugs, tests, conditions, allergens and equipment (content/ai/lexicon.json).</summary>
    public sealed class AiTables
    {
        public static readonly AiTables Empty = new AiTables(new Dictionary<string, IReadOnlyList<string>>(), new Lexicon(new Dictionary<string, IReadOnlyList<string>>(), new Dictionary<string, IReadOnlyList<string>>()));

        public AiTables(IReadOnlyDictionary<string, IReadOnlyList<string>> roles, Lexicon lexicon) { Roles = roles; Lexicon = lexicon; }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Roles { get; }
        public Lexicon Lexicon { get; }

        public static AiTables Parse(string intentsJson, string lexiconJson)
        {
            var root = new JsonObj(MiniJson.Parse(intentsJson), "ai/intents");
            var roles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var kv in root.Any("roles") as Dictionary<string, object?> ?? throw new ContentException("ai/intents.roles: expected an object"))
            {
                var list = (kv.Value as List<object?> ?? throw new ContentException("ai/intents.roles." + kv.Key + ": expected a list")).Select(x => x as string ?? throw new ContentException("ai/intents.roles." + kv.Key + ": expected text")).ToList();
                if (list.Count == 0 || list.Distinct().Count() != list.Count) throw new ContentException("ai/intents.roles." + kv.Key + ": needs at least one intent, each once");
                roles[kv.Key] = list;
            }
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException("ai/intents.refs: expected register ids");
            root.Finish();
            return new AiTables(roles, Lexicon.Parse(lexiconJson));
        }
    }
}
