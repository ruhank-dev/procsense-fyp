using System.Collections.Generic;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;
using ProcSense.VR.Content;

namespace ProcSense.VR.UI.Debrief
{
    /// <summary>
    /// The short citation of a source register ID (DT-261), from content/sources/citations.json (built by tools/content/build_citations.py from the source register, which does not ship in the APK).
    /// The debrief names an item's source by its ID; this turns "SR-2859" into "SR-2859: NICE NG253 Suspected sepsis ...". An ID without a citation is shown as the ID alone.
    /// </summary>
    public static class Citations
    {
        public const string File = "sources/citations.json";
        private static readonly Regex Id = new Regex(@"\bSR-\d{4}\b");
        private static Dictionary<string, string> _table;

        /// <summary>The table (read once); tests may replace it.</summary>
        public static IReadOnlyDictionary<string, string> Table
        {
            get
            {
                if (_table != null) return _table;
                _table = new Dictionary<string, string>();
                try
                {
                    var root = MiniJson.Parse(UnityContentReader.ForThisPlatform().ReadText(File)) as Dictionary<string, object>;
                    if (root != null && root.TryGetValue("citations", out var c) && c is Dictionary<string, object> cites)
                        foreach (var kv in cites) if (kv.Value is string s) _table[kv.Key] = s;
                }
                catch (System.Exception e) { UnityEngine.Debug.LogWarning("[ProcSense] no source citations: " + e.Message); }
                return _table;
            }
        }

        /// <summary>Every register ID in the text with its citation, one per line ("" for none).</summary>
        public static string For(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            var lines = new List<string>();
            foreach (Match m in Id.Matches(source))
                lines.Add(Table.TryGetValue(m.Value, out var c) ? m.Value + ": " + c : m.Value);
            return lines.Count == 0 ? source : string.Join("\n", lines);
        }
    }
}
