using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Scoring;

namespace ProcSense.VR.UI.Debrief
{
    /// <summary>A test the case did not need: what it was, when it was ordered, and what it cost in waiting (the content's result time).</summary>
    public sealed class UnneededTest
    {
        public string Test = "", Label = "", At = "";
        public int Minutes;
        public int Seq = -1;
    }

    /// <summary>
    /// The debrief screen's words (p0-vr-interaction-ui-s09), from Core's debrief facts (<see cref="DebriefFacts"/>: every line is an item the engine recorded, with its sentence, rule and source, and the log
    /// entries behind it). Nothing is judged here: the order is severity (critical first, then major, then minor, then credits), minor items are collapsed by default, and the evidence of an item is its log
    /// entries as they were recorded.
    /// </summary>
    public static class DebriefText
    {
        private static readonly string[] Order = { "critical", "major", "missed", "unnecessary", "minor", "teaching", "credit" };

        public static int Rank(string severity)
        {
            int i = System.Array.IndexOf(Order, severity);
            return i < 0 ? Order.Length : i;
        }

        public static bool Collapsed(DebriefLine l) => l.Severity == "minor" || l.Severity == "teaching";

        /// <summary>The lines in reading order: the most serious first, credits last; the same severity keeps the engine's order.</summary>
        public static IReadOnlyList<DebriefLine> Ordered(DebriefFacts f) =>
            f.Lines.Select((l, i) => (l, i)).OrderBy(x => Rank(x.l.Severity)).ThenBy(x => x.i).Select(x => x.l).ToList();

        /// <summary>The headline: how the case ended, in the engine's words.</summary>
        public static string Headline(DebriefFacts f) =>
            (f.EndState.Length > 0 ? f.EndState.Replace("_", " ") : "Session not finished") + (f.Result.Length > 0 ? "   " + f.Result : "") + "   (" + f.Mode.ToLowerInvariant() + ")";

        /// <summary>One log entry as evidence: the time, what the trainee did and with what, and whether the engine took it.</summary>
        public static string Evidence(Session s, LogEntry e) =>
            Clock(s, e.Step) + "  #" + e.Seq + "  " + e.Verb.Replace("_", " ") + (e.Params.Count > 0 ? " (" + string.Join(", ", e.Params.OrderBy(kv => kv.Key).Select(kv => kv.Key + ": " + kv.Value)) + ")" : "") + (e.Accepted ? "" : "  - refused: " + e.Reject);

        /// <summary>The drawer for one item: its sentence, its log entries, its rule and its source.</summary>
        public static string Drawer(Session s, DebriefLine l)
        {
            var entries = l.Evidence.Select(n => Entry(s, n)).Where(e => e != null).Select(e => Evidence(s, e)).ToList();
            return l.Sentence + "\n\n" + (entries.Count > 0 ? string.Join("\n", entries) : "No log entry is attached to this item.") +
                   "\n\nRule: " + (l.RuleId.Length > 0 ? l.RuleId : "-") + "\nSource: " + (l.Source.Length > 0 ? Citations.For(l.Source) : "-");   // the citation, from content (DT-261)
        }

        /// <summary>
        /// Tests ordered that the case did not need: the orders Core judged not indicated when they were placed, with the waiting time the content gives for each. Read from the orders, not from the items'
        /// evidence, so one test is listed once.
        /// </summary>
        public static IReadOnlyList<UnneededTest> Unneeded(Session s, ContentPackage content, DebriefFacts f)
        {
            var list = new List<UnneededTest>();
            foreach (var o in s.Patient.Clinical.Investigations.Where(o => o.Indication == "NOT_INDICATED"))
            {
                var def = content.Investigations.Find(o.Test);
                int seq = s.Log.Entries.FirstOrDefault(e => e.Verb == "order_investigation" && e.Params.TryGetValue("test", out var t) && t == o.Test && e.Step >= o.OrderedStep)?.Seq ?? -1;
                list.Add(new UnneededTest { Test = o.Test, Label = o.Label, At = Clock(s, o.OrderedStep), Minutes = (int)System.Math.Round(def?.MaxMinutes ?? 0), Seq = seq });
            }
            return list;
        }

        /// <summary>The log entry with this sequence number (the scorer points to entries by Seq), or null.</summary>
        public static LogEntry Entry(Session s, int seq) => seq >= 0 && seq < s.Log.Entries.Count && s.Log.Entries[seq].Seq == seq ? s.Log.Entries[seq] : s.Log.Entries.FirstOrDefault(e => e.Seq == seq);

        public static string Clock(Session s, long step) => ProcSense.Core.Observation.ObservableBuilder.Hms(s.Clock.Start + System.TimeSpan.FromSeconds(step * ProcSense.Core.Time.SimClock.Step)).Substring(0, 5);

        public static string Minutes(double m) => m.ToString("0", CultureInfo.InvariantCulture) + " min";
    }
}
