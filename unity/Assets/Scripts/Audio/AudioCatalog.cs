using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Observation;

namespace ProcSense.VR.Audio
{
    /// <summary>One spoken line of the catalogue: who says it, the words, and what makes it be said.</summary>
    public sealed class VoiceLine
    {
        public string Id = "", Speaker = "", Case = "", Text = "";
        public Dictionary<string, object> Event = new Dictionary<string, object>();
        public bool Template;

        /// <summary>A line that pre-empts the queue: a Core event the trainee must hear now (the lab's critical call, a sample waiting, the porter delayed).</summary>
        public bool Urgent => Event.ContainsKey("core_event") || Event.ContainsKey("transport_state") || Speaker == "laboratory";
    }

    /// <summary>
    /// The voice line catalogue (content/voice/catalogue.json, p0-voice-audio-s01) as the player needs it (p0-voice-audio-s03): the lines of this case and the shared staff lines, and which lines an action or a state
    /// change calls for. A line is said when its event matches: the action's verb (and its topic), whether Core took it or refused it with a given reason, an effect Core reported, the transport state, or the lab
    /// ringing. A template line ("ASA {asa}") needs a value only a session knows and is not played here yet (DT-195).
    /// </summary>
    public sealed class AudioCatalog
    {
        public const string File = "voice/catalogue.json";
        public readonly List<VoiceLine> Lines = new List<VoiceLine>();

        public static AudioCatalog Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object> ?? throw new ContentException(File + ": expected an object");
            var c = new AudioCatalog();
            foreach (var o in (List<object>)root["lines"])
            {
                var d = (Dictionary<string, object>)o;
                c.Lines.Add(new VoiceLine
                {
                    Id = (string)d["id"], Speaker = (string)d["speaker"], Text = (string)d["text"],
                    Case = d.TryGetValue("case", out var cs) && cs is string s ? s : "",
                    Event = d.TryGetValue("event", out var e) && e is Dictionary<string, object> ev ? ev : new Dictionary<string, object>(),
                    Template = d.ContainsKey("template"),
                });
            }
            return c;
        }

        /// <summary>The lines this case can say: its own and the shared ones (no case).</summary>
        public IEnumerable<VoiceLine> ForCase(string caseId) => Lines.Where(l => l.Case.Length == 0 || l.Case == caseId);

        private static string Str(Dictionary<string, object> e, string key) => e.TryGetValue(key, out var v) && v is string s ? s : null;

        /// <summary>The lines an action calls for, in catalogue order.</summary>
        public IReadOnlyList<VoiceLine> For(string caseId, SimAction a, ActionOutcome o)
        {
            var found = new List<VoiceLine>();
            foreach (var l in ForCase(caseId))
            {
                if (l.Template) continue;
                var e = l.Event;
                string verb = Str(e, "verb"), effect = Str(e, "effect"), rejection = Str(e, "rejection");
                if (verb == null && effect == null) continue;
                if (verb != null && verb != a.Verb) continue;
                if (Str(e, "topic") is { } topic && (!a.Params.TryGetValue("topic", out var t) || t != topic)) continue;
                if (rejection != null) { if (o.Accepted || o.RejectReason != rejection) continue; }
                else if (e.TryGetValue("accepted", out var acc) && acc is bool want) { if (o.Accepted != want) continue; }
                else if (!o.Accepted) continue;
                if (effect != null && !o.Effects.Contains(effect)) continue;
                if (Str(e, "nurse_state") != null || Str(e, "medicine_line") != null) continue;   // said by the nurse's and the medicine round's own views
                found.Add(l);
            }
            return found;
        }

        /// <summary>The lines a change of state calls for: the porter's state, and the lab ringing with a critical result.</summary>
        public IReadOnlyList<VoiceLine> ForState(string caseId, ObservableState before, ObservableState now)
        {
            var found = new List<VoiceLine>();
            string was = before?.Transport?.State ?? "", tr = now?.Transport?.State ?? "";
            bool rang = (before?.Phone.State ?? "") != "RINGING" && (now?.Phone.State ?? "") == "RINGING";
            foreach (var l in ForCase(caseId).Where(l => !l.Template))
            {
                if (Str(l.Event, "transport_state") is { } state && state == tr && tr != was) found.Add(l);
                if (Str(l.Event, "core_event") == "critical_call_rings" && rang) found.Add(l);
            }
            return found;
        }
    }
}
