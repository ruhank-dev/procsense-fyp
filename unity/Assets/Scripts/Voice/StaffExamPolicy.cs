using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;
using ProcSense.VR.Content;
using ProcSense.VR.Views;

namespace ProcSense.VR.Voice
{
    /// <summary>
    /// The examination policy for the staff voices (owner decision D3, content/ai/staff_exam_policy.json, validated by tools/content/validate_staff_exam_policy.py). In the examination a staff member speaks only
    /// when the trainee speaks to them (the interpreter's addressee, or one of the role's address words), and then only the examination line of the intent the service chose: a role-appropriate answer, a read-back
    /// or a fact the trainee asked for; practice lines that could hint were replaced by neutral ones. The server applies the same file; this is the second, independent gate. A missing or unreadable file means no
    /// staff speak in the examination (the safe fallback). Nothing here changes the simulation or credits anything.
    /// </summary>
    public sealed class StaffExamPolicy
    {
        public const string File = "ai/staff_exam_policy.json";

        private readonly Dictionary<string, string[]> _words = new Dictionary<string, string[]>();
        private readonly Dictionary<string, Dictionary<string, string>> _lines = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>The staff roles that may speak in the examination at all.</summary>
        public IReadOnlyList<string> Roles => _lines.Keys.ToList();

        private static StaffExamPolicy _loaded;

        /// <summary>The policy (read once); tests may replace it. Unreadable: an empty policy, so no staff speak in the examination.</summary>
        public static StaffExamPolicy Loaded
        {
            get
            {
                if (_loaded != null) return _loaded;
                try { _loaded = Parse(UnityContentReader.ForThisPlatform().ReadText(File)); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[ProcSense] no staff examination policy, staff stay silent in the examination: " + e.Message); _loaded = new StaffExamPolicy(); }
                return _loaded;
            }
            set => _loaded = value;
        }

        public static StaffExamPolicy Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object> ?? throw new FormatException("staff examination policy is not an object");
            var p = new StaffExamPolicy();
            foreach (var kv in (Dictionary<string, object>)root["roles"])
            {
                var spec = (Dictionary<string, object>)kv.Value;
                p._words[kv.Key] = ((List<object>)spec["address_words"]).Select(w => ((string)w).ToLowerInvariant()).ToArray();
                p._lines[kv.Key] = ((Dictionary<string, object>)spec["lines"]).ToDictionary(l => l.Key, l => (string)l.Value);
            }
            return p;
        }

        /// <summary>The trainee spoke to this role: the interpreter said so, or one of its address words is in the sentence as a whole word.</summary>
        public bool Addressed(string role, string question, string addressee)
        {
            if (!_words.TryGetValue(role ?? "", out var words)) return false;
            if (role == addressee) return true;
            string q = " " + Regex.Replace((question ?? "").ToLowerInvariant(), "[^a-z ]+", " ") + " ";
            return words.Any(w => q.Contains(" " + w + " "));
        }

        /// <summary>The examination line of the intent, or null when the role or intent has none (the reply is then dropped).</summary>
        public string Line(string role, string intent) =>
            _lines.TryGetValue(role ?? "", out var lines) && lines.TryGetValue(intent ?? "", out var line) ? line : null;

        /// <summary>
        /// Applies the policy to one examination turn: the patient's reply is kept; a staff reply stays only if the trainee spoke to that role and the intent has an examination line, and its words become that line.
        /// Returns the number of staff replies removed.
        /// </summary>
        public int Apply(List<TeamReply> replies, string question, string addressee)
        {
            int removed = 0;
            for (int i = replies.Count - 1; i >= 0; i--)
            {
                var r = replies[i];
                if (r.Role == "patient") continue;
                string role = r.Role == "anaesthetist" ? "anaesthesia" : r.Role;
                string line = Addressed(role, question, addressee) ? Line(role, r.Intent) : null;
                if (line == null) { replies.RemoveAt(i); removed++; continue; }
                r.Role = role;
                r.Text = line;
            }
            return removed;
        }
    }
}
