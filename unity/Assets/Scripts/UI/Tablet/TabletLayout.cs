using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.VR.UI.Tablet
{
    /// <summary>One choice of a field: the value sent to Core and the words on the button.</summary>
    public sealed class TabletOption
    {
        public string Value = "", Label = "";
    }

    /// <summary>
    /// One input of a form. Kinds (content/ui/tablet.json, tools/content/build_tablet.py): choice (one option; an optional one may stay unset and is not sent),
    /// text (free text), flags (each item yes or no, sent as one parameter "id:yes,id:no"), booleans (each item a parameter of its own, "true" or "false"),
    /// multi (several options sent as "a,b"), state (the options come from what the session shows, for example the investigations).
    /// </summary>
    public sealed class TabletField
    {
        public string Key = "", Label = "", Kind = "", Source = "";
        public bool Optional;
        public List<TabletOption> Options = new List<TabletOption>();
        public List<TabletOption> Items = new List<TabletOption>();
    }

    /// <summary>A button (no fields) or a form (fields filled by the trainee, then sent as one action).</summary>
    public sealed class TabletEntry
    {
        public string Label = "", Verb = "", Enabled = "always";
        public SortedDictionary<string, string> Params = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public List<TabletField> Fields = new List<TabletField>();
        public bool IsForm => Fields.Count > 0;
    }

    /// <summary>A tab: its entries in walkthrough order, and the session list shown beside them (known, investigations, orders, goals) if any.</summary>
    public sealed class TabletTab
    {
        public string Id = "", Label = "", List = "";
        public int Step;
        public List<TabletEntry> Entries = new List<TabletEntry>();
    }

    /// <summary>
    /// The tablet as content describes it (content/ui/tablet.json). Unity holds no clinical text: every label and every choice comes from this file,
    /// which tools/content/build_tablet.py builds from the content tables and checks against the P0 correct paths.
    /// </summary>
    public sealed class TabletLayout
    {
        public const string File = "ui/tablet.json";

        public List<TabletTab> Tabs = new List<TabletTab>();
        /// <summary>The identity banner's words (p0-vr-interaction-ui-s12), by key.</summary>
        public Dictionary<string, string> IdentityStatus = new Dictionary<string, string>();

        public IEnumerable<TabletEntry> Entries => Tabs.SelectMany(t => t.Entries);

        public static TabletLayout Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object> ?? throw new ContentException(File + ": expected an object");
            var layout = new TabletLayout();
            foreach (var t in List(root, "tabs"))
            {
                var tab = (Dictionary<string, object>)t;
                var parsed = new TabletTab { Id = Str(tab, "id"), Label = Str(tab, "label"), List = Opt(tab, "list"), Step = (int)(long)tab["step"] };
                foreach (var e in List(tab, "entries"))
                {
                    var entry = (Dictionary<string, object>)e;
                    var te = new TabletEntry { Label = Str(entry, "label"), Verb = Str(entry, "verb"), Enabled = Opt(entry, "enabled", "always") };
                    foreach (var kv in (Dictionary<string, object>)entry["params"]) te.Params[kv.Key] = (string)kv.Value;
                    if (entry.TryGetValue("fields", out var fields))
                        foreach (var f in (List<object>)fields) te.Fields.Add(Field((Dictionary<string, object>)f));
                    parsed.Entries.Add(te);
                }
                layout.Tabs.Add(parsed);
            }
            if (layout.Tabs.Count == 0) throw new ContentException(File + ": no tabs");
            if (root.TryGetValue("identity_status", out var words) && words is Dictionary<string, object> status)
                foreach (var kv in status) layout.IdentityStatus[kv.Key] = (string)kv.Value;
            return layout;
        }

        private static TabletField Field(Dictionary<string, object> f)
        {
            var field = new TabletField { Key = Str(f, "key"), Label = Str(f, "label"), Kind = Str(f, "kind"), Source = Opt(f, "source"), Optional = f.TryGetValue("optional", out var o) && o is bool b && b };
            if (f.TryGetValue("options", out var options)) field.Options = Options(options);
            if (f.TryGetValue("items", out var items)) field.Items = Options(items);
            return field;
        }

        private static List<TabletOption> Options(object list) =>
            ((List<object>)list).Select(x => (Dictionary<string, object>)x).Select(x => new TabletOption { Value = Str(x, "value"), Label = Str(x, "label") }).ToList();

        private static IEnumerable<object> List(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is List<object> l ? l : throw new ContentException(File + ": '" + key + "' must be a list");

        private static string Str(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is string s ? s : throw new ContentException(File + ": '" + key + "' must be text");

        private static string Opt(Dictionary<string, object> d, string key, string fallback = "") => d.TryGetValue(key, out var v) && v is string s ? s : fallback;

        /// <summary>
        /// The field values that make exactly this action from this entry, or null when the entry cannot make it (the same rule as the content check in
        /// build_tablet.py). State fields accept a value only if the session shows it now.
        /// </summary>
        public static Dictionary<string, string> Fill(TabletEntry entry, string verb, IReadOnlyDictionary<string, string> parameters, Func<TabletField, IEnumerable<TabletOption>> stateOptions)
        {
            if (entry.Verb != verb) return null;
            var rest = new Dictionary<string, string>(parameters.ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal);
            foreach (var kv in entry.Params)
            {
                if (!rest.TryGetValue(kv.Key, out var v) || v != kv.Value) return null;
                rest.Remove(kv.Key);
            }
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var f in entry.Fields)
            {
                if (f.Kind == "booleans")
                {
                    foreach (var item in f.Items)
                    {
                        if (!rest.TryGetValue(item.Value, out var b) || (b != "true" && b != "false")) return null;
                        values[item.Value] = b;
                        rest.Remove(item.Value);
                    }
                    continue;
                }
                if (!rest.TryGetValue(f.Key, out var value))
                {
                    if (f.Optional) continue;
                    return null;
                }
                rest.Remove(f.Key);
                switch (f.Kind)
                {
                    case "choice":
                        if (f.Options.All(o => o.Value != value)) return null;
                        break;
                    case "multi":
                        if (value.Split(',').Any(p => f.Options.All(o => o.Value != p))) return null;
                        break;
                    case "flags":
                        var pairs = value.Split(',').Select(p => p.Split(':')).ToList();
                        if (pairs.Count != f.Items.Count || pairs.Where((p, i) => p.Length != 2 || p[0] != f.Items[i].Value || (p[1] != "yes" && p[1] != "no")).Any()) return null;
                        break;
                    case "state":
                        if (stateOptions(f).All(o => o.Value != value)) return null;
                        break;
                    case "text":
                        break;
                    default:
                        return null;
                }
                values[f.Key] = value;
            }
            return rest.Count == 0 ? values : null;
        }
    }
}
