using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.VR.Interaction
{
    /// <summary>What starts a transition: a grab, an item dropped into a socket, an activate press while held, or a panel/UI press.</summary>
    public enum Trigger { Grab, Socket, Activate, Ui }

    /// <summary>One step of an item's life. A transition with a verb sends exactly one Core action; a visual-only transition has no verb (Core has no matching action) and always succeeds.</summary>
    public sealed class Transition
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public Trigger Trigger { get; set; }
        public string Socket { get; set; } = "";
        public string Verb { get; set; } = "";
        public string Target { get; set; } = "patient";
        public string Label { get; set; } = "";
        public bool VisualOnly => string.IsNullOrEmpty(Verb);
        public SortedDictionary<string, string> Params { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>A button on an item's poke panel (a pump start, a BP cycle). It sends one action and exists only in the listed states.</summary>
    public sealed class PanelEntry
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Verb { get; set; } = "";
        public string Target { get; set; } = "patient";
        public List<string> OnlyIn { get; } = new List<string>();
        public SortedDictionary<string, string> Params { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>The states, transitions and panel of one equipment item, read from content/equipment/equipment_states.json.</summary>
    public sealed class EquipmentDefinition
    {
        public string Id { get; set; } = "";
        public int SpecRow { get; set; }
        public string Initial { get; set; } = "";
        public List<string> States { get; } = new List<string>();
        public List<Transition> Transitions { get; } = new List<Transition>();
        public List<PanelEntry> Panel { get; } = new List<PanelEntry>();

        public IEnumerable<Transition> From(string state) => Transitions.Where(t => t.From == state);

        /// <summary>Every state must be named, every transition must join two named states, and the initial state must exist. Throws with the item and the reason.</summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(Id)) throw new ContentException("equipment item without an id");
            if (States.Count == 0) throw new ContentException(Id + ": no states");
            if (!States.Contains(Initial)) throw new ContentException(Id + ": initial state " + Initial + " is not one of its states");
            foreach (var t in Transitions)
            {
                if (!States.Contains(t.From) || !States.Contains(t.To)) throw new ContentException(Id + ": transition " + t.From + " to " + t.To + " names an unknown state");
                if (t.Trigger == Trigger.Socket && string.IsNullOrEmpty(t.Socket)) throw new ContentException(Id + ": a socket transition needs a socket (" + t.From + " to " + t.To + ")");
            }
            foreach (var p in Panel)
            {
                if (string.IsNullOrEmpty(p.Verb)) throw new ContentException(Id + ": panel entry " + p.Id + " has no verb");
                foreach (var s in p.OnlyIn) if (!States.Contains(s)) throw new ContentException(Id + ": panel entry " + p.Id + " names the unknown state " + s);
            }
        }

        /// <summary>Reads content/equipment/equipment_states.json.</summary>
        public static IReadOnlyDictionary<string, EquipmentDefinition> Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("items", out var itemsNode) || !(itemsNode is List<object> items))
                throw new ContentException("equipment_states.json has no items list");
            var result = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
            foreach (var node in items)
            {
                var o = node as Dictionary<string, object> ?? throw new ContentException("an equipment item is not an object");
                var d = new EquipmentDefinition { Id = Str(o, "id"), Initial = Str(o, "initial"), SpecRow = (int)Num(o, "spec_row") };
                foreach (var s in List(o, "states")) d.States.Add((string)s);
                foreach (var tn in List(o, "transitions"))
                {
                    var t = (Dictionary<string, object>)tn;
                    var tr = new Transition
                    {
                        From = Str(t, "from"), To = Str(t, "to"), Socket = Str(t, "socket"), Verb = Str(t, "verb"),
                        Label = Str(t, "label"), Trigger = ParseTrigger(Str(t, "trigger"), d.Id)
                    };
                    if (t.TryGetValue("target", out var tg) && tg is string ts && ts.Length > 0) tr.Target = ts;
                    Fill(tr.Params, t);
                    d.Transitions.Add(tr);
                }
                foreach (var pn in List(o, "panel"))
                {
                    var p = (Dictionary<string, object>)pn;
                    var pe = new PanelEntry { Id = Str(p, "id"), Label = Str(p, "label"), Verb = Str(p, "verb") };
                    if (p.TryGetValue("target", out var tg) && tg is string ts && ts.Length > 0) pe.Target = ts;
                    foreach (var s in List(p, "only_in")) pe.OnlyIn.Add((string)s);
                    Fill(pe.Params, p);
                    d.Panel.Add(pe);
                }
                d.Validate();
                if (result.ContainsKey(d.Id)) throw new ContentException("equipment item " + d.Id + " is listed twice");
                result[d.Id] = d;
            }
            return result;
        }

        private static Trigger ParseTrigger(string s, string item)
        {
            switch (s)
            {
                case "grab": return Trigger.Grab;
                case "socket": return Trigger.Socket;
                case "activate": return Trigger.Activate;
                case "ui": return Trigger.Ui;
                default: throw new ContentException(item + ": unknown trigger '" + s + "'");
            }
        }

        private static string Str(Dictionary<string, object> o, string key) => o.TryGetValue(key, out var v) && v is string s ? s : "";
        private static double Num(Dictionary<string, object> o, string key) => o.TryGetValue(key, out var v) && v is long l ? l : v is double d ? d : 0;
        private static IEnumerable<object> List(Dictionary<string, object> o, string key) => o.TryGetValue(key, out var v) && v is List<object> l ? l : Enumerable.Empty<object>();

        private static void Fill(IDictionary<string, string> into, Dictionary<string, object> o)
        {
            if (o.TryGetValue("params", out var v) && v is Dictionary<string, object> ps)
                foreach (var kv in ps) into[kv.Key] = kv.Value as string ?? "";
        }
    }
}
