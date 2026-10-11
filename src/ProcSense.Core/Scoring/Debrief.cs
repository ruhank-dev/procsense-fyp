using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Observation;

namespace ProcSense.Core.Scoring
{
    /// <summary>One debrief line: the item it states, the sentence made from a template (placeholders filled only from the item's own facts) and the log entries behind it.</summary>
    public sealed record DebriefLine(string Item, string Behaviour, string Label, string Severity, string Status, int Points, string Sentence, IReadOnlyList<int> Evidence, string RuleId, string Source);

    public sealed record TimelineEntry(long Step, string Time, string Kind, string Text, int Seq);

    /// <summary>
    /// The structured facts of a session (schema debrief_facts.v1, content/schema/debrief_facts.schema.json): the result, every item with the sentence that states it, the score per behaviour, the timeline and the events.
    /// Templates cannot invent a mistake: a sentence exists only for an item the engine recorded, and the same facts are what an AI debrief will be allowed to rephrase in P1 (and nothing else).
    /// </summary>
    public sealed class DebriefFacts
    {
        public const string Schema = "debrief_facts.v1";

        public DebriefFacts(string caseId, string mode, string endState, string result, IReadOnlyList<DebriefLine> lines, IReadOnlyList<CategoryScore> categories, IReadOnlyList<TimelineEntry> timeline,
            IReadOnlyList<Lateness> lateness, IReadOnlyList<string> events, IReadOnlyList<string> notShown, IReadOnlyList<string> sources)
        {
            Case = caseId; Mode = mode; EndState = endState; Result = result; Lines = lines; Categories = categories; Timeline = timeline; Lateness = lateness; Events = events; NotShown = notShown; Sources = sources;
        }

        public string Case { get; }
        public string Mode { get; }
        public string EndState { get; }
        public string Result { get; }
        public IReadOnlyList<DebriefLine> Lines { get; }
        public IReadOnlyList<CategoryScore> Categories { get; }
        public IReadOnlyList<TimelineEntry> Timeline { get; }
        public IReadOnlyList<Lateness> Lateness { get; }
        public IReadOnlyList<string> Events { get; }
        public IReadOnlyList<string> NotShown { get; }
        public IReadOnlyList<string> Sources { get; }

        public static DebriefFacts Build(Session s, ContentPackage content)
        {
            var report = Scorer.Score(s, content);
            var tables = content.Scoring;
            var lines = new List<DebriefLine>();
            foreach (var i in report.Items)
            {
                string label = i.Id.Substring(i.Id.IndexOf('.') + 1).Replace('_', ' ');
                string template = tables.Sentences.TryGetValue(i.Severity, out var t) ? t : "{label}. Rule {rule_id} [{source}].";
                string sentence = template.Replace("{label}", label).Replace("{rule_id}", i.RuleId).Replace("{source}", i.Source);
                if (i.Status == "teaching") sentence += " This is a teaching moment: it carries no penalty.";
                lines.Add(new DebriefLine(i.Id, i.Category, label, i.Severity, i.Status, i.Points, sentence, i.Evidence, i.RuleId, i.Source));
            }
            var store = new EvidenceStore(tables);
            store.FromLog(s.Log);
            var timeline = new List<TimelineEntry>();
            foreach (var e in s.Log.Entries.Where(e => e.Accepted && tables.Behaviours.Any(b => b.Actions.Contains(e.Verb))))
                timeline.Add(new TimelineEntry(e.Step, Clock(s, e.Step), "action", e.Verb, e.Seq));
            for (int k = 0; k < lines.Count; k++) timeline.Add(new TimelineEntry(report.Items[k].Step, Clock(s, report.Items[k].Step), "item", lines[k].Item, lines[k].Evidence.Count > 0 ? lines[k].Evidence[0] : -1));
            if (report.EndState.Length > 0) timeline.Add(new TimelineEntry(s.Clock.Steps, Clock(s, s.Clock.Steps), "end_state", report.EndState, s.Log.Count - 1));
            var ordered = timeline.OrderBy(x => x.Step).ThenBy(x => x.Kind == "action" ? 0 : x.Kind == "item" ? 1 : 2).ToList();
            var events = s.Director?.History.Where(h => h.Kind == "event_fired").Select(h => h.Id).ToList() ?? new List<string>();
            var sources = lines.Select(l => l.Source).Where(x => x.Length > 0).Concat(report.Lateness.Select(l => l.Source)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            return new DebriefFacts(report.Case, report.Mode, report.EndState, report.Result, lines, report.Categories, ordered, report.Lateness, events, store.NotShown(), sources);
        }

        private static string Clock(Session s, long step) => ObservableBuilder.Hms(s.Clock.Start + TimeSpan.FromSeconds(step * Time.SimClock.Step));

        // ---------------------------------------------------------------- JSON

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"schema\":"); JsonText.Quote(sb, Schema);
            sb.Append(",\"case\":"); JsonText.Quote(sb, Case);
            sb.Append(",\"mode\":"); JsonText.Quote(sb, Mode);
            sb.Append(",\"end_state\":"); JsonText.Quote(sb, EndState);
            sb.Append(",\"result\":"); JsonText.Quote(sb, Result);
            sb.Append(",\"items\":[");
            for (int i = 0; i < Lines.Count; i++)
            {
                var l = Lines[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":"); JsonText.Quote(sb, l.Item);
                sb.Append(",\"behaviour\":"); JsonText.Quote(sb, l.Behaviour);
                sb.Append(",\"severity\":"); JsonText.Quote(sb, l.Severity);
                sb.Append(",\"status\":"); JsonText.Quote(sb, l.Status);
                sb.Append(",\"points\":").Append(l.Points.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"evidence\":["); sb.Append(string.Join(",", l.Evidence.Select(e => e.ToString(CultureInfo.InvariantCulture)))); sb.Append(']');
                sb.Append(",\"rule_id\":"); JsonText.Quote(sb, l.RuleId);
                sb.Append(",\"source\":"); JsonText.Quote(sb, l.Source);
                sb.Append(",\"sentence\":"); JsonText.Quote(sb, l.Sentence);
                sb.Append('}');
            }
            sb.Append("],\"competencies\":[");
            for (int i = 0; i < Categories.Count; i++)
            {
                var c = Categories[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"behaviour\":"); JsonText.Quote(sb, c.Behaviour);
                sb.Append(",\"label\":"); JsonText.Quote(sb, c.Label);
                sb.Append(",\"credits\":").Append(c.Credits.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"penalties\":").Append(c.Penalties.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"net\":").Append(c.Net.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"weight\":").Append(c.Weight.ToString("0.##", CultureInfo.InvariantCulture));
                sb.Append(",\"items\":").Append(c.Items.ToString(CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append("],\"timeline\":[");
            for (int i = 0; i < Timeline.Count; i++)
            {
                var t = Timeline[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"step\":").Append(t.Step.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"time\":"); JsonText.Quote(sb, t.Time);
                sb.Append(",\"kind\":"); JsonText.Quote(sb, t.Kind);
                sb.Append(",\"text\":"); JsonText.Quote(sb, t.Text);
                sb.Append(",\"seq\":").Append(t.Seq.ToString(CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append("],\"lateness\":[");
            for (int i = 0; i < Lateness.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"what\":"); JsonText.Quote(sb, Lateness[i].What);
                sb.Append(",\"minutes\":").Append(Lateness[i].Minutes.ToString("0.#", CultureInfo.InvariantCulture));
                sb.Append(",\"source\":"); JsonText.Quote(sb, Lateness[i].Source);
                sb.Append('}');
            }
            sb.Append("],\"events\":["); sb.Append(string.Join(",", Events.Select(e => Quoted(e))));
            sb.Append("],\"not_shown\":["); sb.Append(string.Join(",", NotShown.Select(e => Quoted(e))));
            sb.Append("],\"sources\":["); sb.Append(string.Join(",", Sources.Select(e => Quoted(e))));
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Quoted(string s) { var sb = new StringBuilder(); JsonText.Quote(sb, s); return sb.ToString(); }

        // ---------------------------------------------------------------- HTML (one file, inline CSS, no scripts, no real names)

        public string ToHtml()
        {
            string E(string x) => System.Net.WebUtility.HtmlEncode(x);
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>ProcSense session report</title>");
            sb.Append("<style>body{font-family:system-ui,sans-serif;margin:16px;max-width:60rem;line-height:1.4}h1{font-size:1.3rem}h2{font-size:1.1rem;margin-top:1.5rem}table{border-collapse:collapse;width:100%}td,th{border:1px solid #bbb;padding:4px 6px;text-align:left;vertical-align:top;font-size:.9rem}.crit{background:#fde2e2}.ok{background:#e4f5e4}.warn{background:#fff3d6}.small{font-size:.8rem;color:#555}</style></head><body>");
            sb.Append("<h1>ProcSense session report</h1>");
            sb.Append("<p>Case <b>").Append(E(Case)).Append("</b> (").Append(E(Mode)).Append(" mode). Educational simulation, not clinical advice. Synthetic patient.</p>");
            sb.Append("<h2>Summary</h2><p>End state: <b>").Append(E(EndState.Length == 0 ? "not reached" : EndState)).Append("</b>. Result: <b>").Append(E(Result)).Append("</b>.</p>");
            sb.Append("<h2>Critical items</h2>");
            var crit = Lines.Where(l => l.Status == "critical").ToList();
            if (crit.Count == 0) sb.Append("<p>None.</p>");
            else { sb.Append("<table>"); foreach (var l in crit) sb.Append("<tr class=\"crit\"><td>").Append(E(l.Sentence)).Append("</td><td class=\"small\">log ").Append(E(string.Join(", ", l.Evidence))).Append("</td></tr>"); sb.Append("</table>"); }
            sb.Append("<h2>All items</h2><table><tr><th>Statement</th><th>Points</th><th>Log</th></tr>");
            foreach (var l in Lines) sb.Append("<tr class=\"").Append(l.Status == "critical" ? "crit" : l.Status == "credit" ? "ok" : l.Status == "penalty" ? "warn" : "").Append("\"><td>").Append(E(l.Sentence)).Append("</td><td>").Append(l.Points.ToString(CultureInfo.InvariantCulture)).Append("</td><td class=\"small\">").Append(E(string.Join(", ", l.Evidence))).Append("</td></tr>");
            sb.Append("</table><h2>Behaviours</h2><table><tr><th>Behaviour</th><th>Credits</th><th>Penalties</th><th>Net</th></tr>");
            foreach (var c in Categories.Where(c => c.Items > 0)) sb.Append("<tr><td>").Append(E(c.Label)).Append("</td><td>").Append(c.Credits).Append("</td><td>").Append(c.Penalties).Append("</td><td>").Append(c.Net).Append("</td></tr>");
            sb.Append("</table><p class=\"small\">Weights and points are provisional design values; the verdict above does not depend on them.</p>");
            if (NotShown.Count > 0) sb.Append("<p class=\"small\">No evidence yet for: ").Append(E(string.Join(", ", NotShown))).Append(".</p>");
            if (Lateness.Count > 0) { sb.Append("<h2>Lateness</h2><ul>"); foreach (var l in Lateness) sb.Append("<li>").Append(E(l.What)).Append(l.Minutes > 0 ? ": " + l.Minutes.ToString("0.#", CultureInfo.InvariantCulture) + " minutes late" : ": recorded as a delay").Append(" [").Append(E(l.Source)).Append("]</li>"); sb.Append("</ul>"); }
            sb.Append("<h2>Timeline</h2><table><tr><th>Time</th><th>What</th><th>Log</th></tr>");
            foreach (var t in Timeline) sb.Append("<tr><td>").Append(E(t.Time)).Append("</td><td>").Append(E(t.Kind)).Append(": ").Append(E(t.Text)).Append("</td><td class=\"small\">").Append(t.Seq).Append("</td></tr>");
            sb.Append("</table><h2>Sources</h2><p class=\"small\">").Append(E(string.Join(", ", Sources))).Append(" (see docs/research/source_register.csv)</p></body></html>");
            return sb.ToString();
        }
    }
}
