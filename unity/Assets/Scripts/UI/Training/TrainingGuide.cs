using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.VR.UI.Training
{
    /// <summary>One step the board can explain: a goal, a scenario stage or a ward checklist line, with its section, order and how-to per input mode.</summary>
    public sealed class GuideEntry
    {
        public string Id = "", Kind = "", Section = "", Label = "", HowMenu = "", HowVoice = "", VoiceExample = "", Explanation = "";
        public int Order;
        public IReadOnlyList<string> Scenarios = new string[0];
    }

    /// <summary>
    /// The training guide (content/ui/training_guide.json, built by tools/content/build_training_guide.py; P3-TRN-01): sections, the explanation and how-to of every goal, stage and ward line,
    /// and the wording of each "not needed" reason id the engine publishes (P3-TRN-02). It only explains: completion, states and reasons come from Core.
    /// </summary>
    public sealed class TrainingGuide
    {
        public const string File = "ui/training_guide.json";

        public IReadOnlyList<(string Id, string Title)> Sections { get; private set; } = new (string, string)[0];
        public IReadOnlyList<GuideEntry> Entries { get; private set; } = new GuideEntry[0];
        private readonly Dictionary<string, GuideEntry> _byId = new Dictionary<string, GuideEntry>();
        private readonly Dictionary<string, string> _reasonText = new Dictionary<string, string>();

        public GuideEntry Find(string id) => id != null && _byId.TryGetValue(id, out var e) ? e : null;

        /// <summary>The words for a reason id; the two generic reasons of Core have fixed wording.</summary>
        public string ReasonText(string reasonId)
        {
            if (reasonId != null && _reasonText.TryGetValue(reasonId, out var t)) return t;
            if (reasonId == ProcSense.Core.Observation.Availability.NotRequired) return "Optional: only if this patient needs it.";
            return "Does not apply to this case.";
        }

        public static TrainingGuide Load(IContentReader reader) => Parse(reader.ReadText(File));

        public static TrainingGuide Parse(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var g = new TrainingGuide();
            g.Sections = ((List<object>)root["sections"]).Cast<Dictionary<string, object>>()
                .OrderBy(s => System.Convert.ToInt32(s["order"])).Select(s => ((string)s["id"], (string)s["title"])).ToList();
            var entries = new List<GuideEntry>();
            foreach (Dictionary<string, object> e in (List<object>)root["entries"])
            {
                var how = (Dictionary<string, object>)e["how_to"];
                var voice = how["voice"] as Dictionary<string, object>;
                var entry = new GuideEntry
                {
                    Id = (string)e["id"], Kind = (string)e["kind"], Section = (string)e["section"], Label = (string)e["label"],
                    HowMenu = how["menu"] as string ?? "", HowVoice = voice != null ? voice["text"] as string ?? "" : "", VoiceExample = voice != null ? voice["example"] as string ?? "" : "",
                    Explanation = e.TryGetValue("explanation", out var x) ? x as string ?? "" : "", Order = System.Convert.ToInt32(e["order"]),
                    Scenarios = e.TryGetValue("scenarios", out var sc) && sc is List<object> l ? l.Cast<string>().ToList() : new List<string>(),
                };
                entries.Add(entry);
                g._byId[entry.Id] = entry;
            }
            g.Entries = entries;
            foreach (Dictionary<string, object> r in (List<object>)root["inactive_reasons"]) g._reasonText[(string)r["reason_id"]] = (string)r["text"];
            return g;
        }
    }
}
