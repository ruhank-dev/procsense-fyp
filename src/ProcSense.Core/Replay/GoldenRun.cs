using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Replay
{
    /// <summary>
    /// A golden run is an actions script plus an expected file: the key facts a correct run must end with, plus the log hash and
    /// the final-state hash for the recorded seed. The same check backs the xUnit tests and the CLI "golden" command.
    /// </summary>
    public static class GoldenRun
    {
        /// <summary>Runs the script and returns every difference from the expected file. An empty list means the golden run passes.</summary>
        public static List<string> Check(string scriptJson, string expectedJson, ContentPackage content)
        {
            var expected = MiniJson.Parse(expectedJson);
            var header = new JsonObj(expected, "expected file");
            string caseId = header.Str("case");
            ulong seed = ulong.Parse(header.Str("seed"), CultureInfo.InvariantCulture);
            var mode = (SessionMode)Enum.Parse(typeof(SessionMode), header.Str("mode"), true);

            var diffs = new List<string>();
            Diff("", expected, MiniJson.Parse(Describe(Run(scriptJson, caseId, seed, mode, content, Options(header)), content)), diffs);
            return diffs;
        }

        /// <summary>The expected file for a script as it runs today (used by "golden --update"; review the change before committing it).</summary>
        public static string Expected(string scriptJson, string caseId, ulong seed, SessionMode mode, ContentPackage content, SessionOptions? options = null) =>
            Format(MiniJson.Parse(Describe(Run(scriptJson, caseId, seed, mode, content, options), content)), 0) + "\n";

        /// <summary>The optional "variant" and "level" keys of an expected file (DT-093). A file without them plays the case's base path at the beginner level, as before.</summary>
        public static SessionOptions Options(string expectedJson) => Options(new JsonObj(MiniJson.Parse(expectedJson), "expected file"));

        private static SessionOptions Options(JsonObj header) => new SessionOptions(
            header.Has("level") ? (TrainingLevel)Enum.Parse(typeof(TrainingLevel), header.Str("level"), true) : TrainingLevel.Beginner,
            header.Has("variant") ? header.Str("variant") : "");

        private static Session Run(string scriptJson, string caseId, ulong seed, SessionMode mode, ContentPackage content, SessionOptions? options)
        {
            if (!content.Cases.TryGetValue(caseId, out var card)) throw new ContentException("case '" + caseId + "' is not loaded");
            var session = new Session(card, seed, mode, Subsystems.Default(content), options);
            SessionFile.Play(session, ActionScript.Parse(scriptJson));
            return session;
        }

        // The whole expected-file shape, in a fixed key order.
        private static string Describe(Session s, ContentPackage content)
        {
            var o = s.Observable;
            var c = s.Patient.Clinical;
            var b = s.Patient.Behavioural;
            var sb = new StringBuilder("{\"case\":");
            JsonText.Quote(sb, s.Case.Id);
            sb.Append(",\"seed\":"); JsonText.Quote(sb, s.Seed.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"mode\":"); JsonText.Quote(sb, s.Mode.ToString().ToLowerInvariant());
            if (s.Variant.Length > 0) { sb.Append(",\"variant\":"); JsonText.Quote(sb, s.Variant); }                                   // only when the run plays a variant or the advanced level, so the earlier golden files keep their shape
            if (s.Level != TrainingLevel.Beginner) { sb.Append(",\"level\":"); JsonText.Quote(sb, s.Level.ToString().ToLowerInvariant()); }
            sb.Append(",\"log_hash\":"); JsonText.Quote(sb, s.Log.Hash());
            sb.Append(",\"state_hash\":"); JsonText.Quote(sb, o.Hash());
            sb.Append(",\"facts\":{\"identity_confirmed\":").Append(o.IdentityConfirmed ? "true" : "false");
            sb.Append(",\"allergies_known\":").Append(o.AllergiesKnown ? "true" : "false");
            sb.Append(",\"nurse\":"); JsonText.Quote(sb, o.Nurse);
            sb.Append(",\"allergy_band\":").Append(o.AllergyBand ? "true" : "false");
            sb.Append(",\"disclosed_topics\":["); Join(sb, o.DisclosedFacts.Select(f => f.Topic));
            sb.Append("],\"vitals\":{");
            sb.Append(string.Join(",", o.Vitals.Select(v => Q(v.Name) + ":" + Q(v.Value))));
            sb.Append("},\"flags\":["); Join(sb, c.Flags);
            sb.Append("],\"rejections\":[");
            sb.Append(string.Join(",", Rejections(s)));
            sb.Append("],\"trust\":").Append(b.Trust.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"anxiety\":").Append(b.Anxiety.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"understanding\":").Append(b.Understanding.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"last_rejection\":"); JsonText.Quote(sb, o.LastRejection);
            string end = s.EndStates?.Resolved ?? "";
            if (end.Length > 0) { sb.Append(",\"end_state\":"); JsonText.Quote(sb, end); }       // only once the case has ended, so the earlier golden runs keep their files
            sb.Append("}");
            // the score the debrief shows (p0-evaluation-performance-s02, plan test T-SCO1): the result, how many credits, and every lost item by severity, so a score change shows in the diff
            var facts = Scoring.DebriefFacts.Build(s, content);
            sb.Append(",\"score\":{\"result\":"); JsonText.Quote(sb, facts.Result);
            sb.Append(",\"credits\":").Append(facts.Lines.Count(l => l.Severity == "credit").ToString(CultureInfo.InvariantCulture));
            foreach (var severity in new[] { "minor", "major", "critical" })
            {
                sb.Append(",\"").Append(severity).Append("\":[");
                Join(sb, facts.Lines.Where(l => l.Severity == severity).Select(l => l.Item).OrderBy(i => i, StringComparer.Ordinal));
                sb.Append(']');
            }
            return sb.Append("}}").ToString();
        }

        private static IEnumerable<string> Rejections(Session s)
        {
            foreach (var line in s.Log.ToJsonl().Split('\n'))
            {
                if (line.Length == 0) continue;
                var e = new JsonObj(MiniJson.Parse(line), "log");
                if (!e.Bool("accepted")) yield return "{\"verb\":" + Q(e.Str("verb")) + ",\"reason\":" + Q(e.Str("reject")) + "}";
            }
        }

        private static string Q(string s) => JsonText.Quote(new StringBuilder(), s).ToString();

        private static void Join(StringBuilder sb, IEnumerable<string> items) => sb.Append(string.Join(",", items.Select(Q)));

        private static void Diff(string path, object? expected, object? actual, List<string> diffs)
        {
            if (expected is Dictionary<string, object?> e && actual is Dictionary<string, object?> a)
            {
                foreach (var k in e.Keys.Concat(a.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
                {
                    string p = path.Length == 0 ? k : path + "." + k;
                    if (!e.ContainsKey(k)) diffs.Add(p + ": not expected, but the run produced it");
                    else if (!a.ContainsKey(k)) diffs.Add(p + ": expected but the run did not produce it");
                    else Diff(p, e[k], a[k], diffs);
                }
            }
            else if (expected is List<object?> le && actual is List<object?> la)
            {
                if (le.Count != la.Count) diffs.Add(path + ": expected " + le.Count.ToString(CultureInfo.InvariantCulture) + " items, got " + la.Count.ToString(CultureInfo.InvariantCulture) + " (expected " + Format(expected, 0) + ", got " + Format(actual, 0) + ")");
                else for (int i = 0; i < le.Count; i++) Diff(path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", le[i], la[i], diffs);
            }
            else if (!Equals(expected, actual))
                diffs.Add(path + ": expected " + Format(expected, 0) + ", got " + Format(actual, 0));
        }

        // Readable layout for the expected file: one key per line, short lists on one line.
        private static string Format(object? node, int indent)
        {
            string pad = new string(' ', indent * 2), inner = new string(' ', (indent + 1) * 2);
            switch (node)
            {
                case Dictionary<string, object?> d:
                    if (d.Count == 0) return "{}";
                    if (d.Values.All(v => !(v is Dictionary<string, object?> || v is List<object?>)))
                        return "{ " + string.Join(", ", d.Select(kv => Q(kv.Key) + ": " + Format(kv.Value, 0))) + " }";
                    return "{\n" + string.Join(",\n", d.Select(kv => inner + Q(kv.Key) + ": " + Format(kv.Value, indent + 1))) + "\n" + pad + "}";
                case List<object?> l:
                    if (l.Count == 0) return "[]";
                    if (l.All(v => !(v is List<object?>)) && l.Count <= 4) return "[" + string.Join(", ", l.Select(v => Format(v, 0))) + "]";
                    return "[\n" + string.Join(",\n", l.Select(v => inner + Format(v, indent + 1))) + "\n" + pad + "]";
                case string s: return Q(s);
                case bool bo: return bo ? "true" : "false";
                case long n: return n.ToString(CultureInfo.InvariantCulture);
                case double x: return x.ToString("R", CultureInfo.InvariantCulture);
                default: return "null";
            }
        }
    }
}
