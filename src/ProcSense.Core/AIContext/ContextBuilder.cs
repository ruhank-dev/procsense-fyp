using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;

namespace ProcSense.Core.AIContext
{
    /// <summary>One earlier line of the conversation.</summary>
    public sealed record ContextTurn(string Speaker, string Text);

    /// <summary>A fact the model may use now: already told to the trainee, or earned by what the trainee is asking this turn. A fact the character may not tell is not here at all.</summary>
    public sealed record ContextFact(string Id, string Topic, string Value, bool Disclosed, bool DisclosableNow);

    /// <summary>
    /// What an AI-voiced character is told (p1-ai-language s03, guide section 82): an immutable, read-only projection of the simulation for ONE character. It has no way back into the session: it is a plain record of text and numbers.
    /// The model never receives a hidden fact, an expected answer, a score or a hint; what the validator also knows (the hidden facts, to catch a leak) lives only in the <see cref="ValidationContext"/> beside it.
    /// </summary>
    public sealed record ContextDto(
        string Role, string Case, string Mode, string Level, string Stage, IReadOnlyList<string> CompletedActions, IReadOnlyList<string> MissingGoals, IReadOnlyList<ContextFact> Facts, IReadOnlyList<string> ToldToPatient,
        string BehaviourMode, IReadOnlyList<string> Signs, IReadOnlyList<string> ActiveEvents, IReadOnlyList<ContextTurn> LastTurns, IReadOnlyList<string> Results, IReadOnlyList<string> AllowedIntents)
    {
        /// <summary>The DTO as JSON (what is sent to the model), in a fixed key order.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder("{");
            Str(sb, "role", Role); sb.Append(',');
            Str(sb, "case", Case); sb.Append(',');
            Str(sb, "mode", Mode); sb.Append(',');
            Str(sb, "level", Level); sb.Append(',');
            Str(sb, "stage", Stage); sb.Append(',');
            List(sb, "completed_actions", CompletedActions); sb.Append(',');
            List(sb, "missing_goals", MissingGoals); sb.Append(",\"facts\":[");
            for (int i = 0; i < Facts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{'); Str(sb, "id", Facts[i].Id); sb.Append(','); Str(sb, "topic", Facts[i].Topic); sb.Append(','); Str(sb, "value", Facts[i].Value);
                sb.Append(",\"disclosed\":").Append(Facts[i].Disclosed ? "true" : "false").Append(",\"disclosable_now\":").Append(Facts[i].DisclosableNow ? "true" : "false").Append('}');
            }
            sb.Append("],"); List(sb, "told_to_patient", ToldToPatient); sb.Append(',');
            Str(sb, "behaviour", BehaviourMode); sb.Append(',');
            List(sb, "signs", Signs); sb.Append(',');
            List(sb, "active_events", ActiveEvents); sb.Append(",\"last_turns\":[");
            for (int i = 0; i < LastTurns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{'); Str(sb, "speaker", LastTurns[i].Speaker); sb.Append(','); Str(sb, "text", LastTurns[i].Text); sb.Append('}');
            }
            sb.Append("],"); List(sb, "results", Results); sb.Append(',');
            List(sb, "allowed_intents", AllowedIntents);
            return sb.Append('}').ToString();
        }

        /// <summary>A cheap token estimate (four characters a token); the real count is measured with the model's tokenizer on the server.</summary>
        public int ApproxTokens => (ToJson().Length + 3) / 4;

        private static void Str(StringBuilder sb, string key, string value) { JsonText.Quote(sb, key); sb.Append(':'); JsonText.Quote(sb, value); }

        private static void List(StringBuilder sb, string key, IReadOnlyList<string> items)
        {
            JsonText.Quote(sb, key); sb.Append(":[");
            for (int i = 0; i < items.Count; i++) { if (i > 0) sb.Append(','); JsonText.Quote(sb, items[i]); }
            sb.Append(']');
        }
    }

    /// <summary>What a built context holds: the DTO for the model and the context for the validator (which also knows the hidden facts).</summary>
    public sealed record BuiltContext(ContextDto Dto, ValidationContext Validation);

    /// <summary>
    /// Builds the per-character context from the session's truth (p1-ai-language s03). Rules: a patient may use the facts the trainee has already learned, the main complaint, and the fact asked about this turn; staff know only what the trainee
    /// has learned (and the chart), never the patient's hidden facts; nobody is given a score, an expected answer or a hint. When the context is too large the oldest turns go first, then older actions and results.
    /// </summary>
    public static class ContextBuilder
    {
        public const int TokenBudget = 900;

        public static BuiltContext Build(Session s, ContentPackage content, string role, string askedTopic, IReadOnlyList<ContextTurn> turns, int tokenBudget = TokenBudget)
        {
            if (!content.Ai.Roles.TryGetValue(role, out var intents)) throw new ArgumentException("unknown role '" + role + "'", nameof(role));
            var file = content.CaseFiles.TryGetValue(s.Case.Id, out var f) ? f : null;
            var obs = s.Observable;
            var lexicon = content.Ai.Lexicon;
            bool patient = role == "patient";

            var facts = new List<ContextFact>();
            var hidden = new List<ValidationFact>();
            foreach (var h in obs.DisclosedFacts) facts.Add(new ContextFact("h." + h.Topic, h.Topic, h.Answer, true, false));
            if (file != null)
                foreach (var h in file.History.Where(x => x.Variant == null || x.Variant == s.Variant))
                {
                    if (obs.DisclosedFacts.Any(d => d.Topic == h.Topic)) continue;
                    bool earned = patient && (h.Volunteered || h.Topic == askedTopic);
                    if (earned) facts.Add(new ContextFact("h." + h.Topic, h.Topic, h.Answer, false, true));
                    else hidden.Add(new ValidationFact("h." + h.Topic, h.Answer, false, false));         // known to the validator only
                }

            var completed = s.Log.Entries.Where(e => e.Accepted).Select(e => e.Verb).Distinct().Take(40).ToList();
            var missing = patient || s.Tracker == null ? new List<string>() : s.Tracker.GoalStatuses().Where(g => g.State == "pending").Select(g => g.Id).Take(10).ToList();
            var told = s.Consent?.Explained.ToList() ?? new List<string>();
            var signs = new List<string>();
            if (patient)
            {
                if (s.Patient.Physiological.Vitals.Acvpu != "A") signs.Add("drowsy or confused");
                if (s.Patient.Physiological.Vitals.PainScore >= 7) signs.Add("in obvious pain");
            }
            var events = s.Director?.Cues.Select(c => c.Text).Distinct().ToList() ?? new List<string>();
            var results = patient ? new List<string>() : obs.Investigations.SelectMany(i => i.Results.Select(r => r.Analyte + " " + r.Value + " " + r.Unit)).ToList();
            string stage = s.Flow?.Running().FirstOrDefault()?.Id ?? "";
            string mode = s.Mode.ToString().ToLowerInvariant(), level = s.Level.ToString().ToLowerInvariant();
            string behaviour = patient ? s.BehaviourStep?.Mode ?? "" : "";

            var keptTurns = turns.ToList();
            ContextDto Make() => new ContextDto(role, s.Case.Id, mode, level, stage, completed, missing, facts, told, behaviour, signs, events, keptTurns, results, intents);
            var dto = Make();
            while (dto.ApproxTokens > tokenBudget && (keptTurns.Count > 0 || completed.Count > 5 || results.Count > 0))      // trim: oldest turns, then older actions, then results
            {
                if (keptTurns.Count > 0) keptTurns.RemoveAt(0);
                else if (completed.Count > 5) completed.RemoveAt(0);
                else results.RemoveAt(0);
                dto = Make();
            }

            // the validator's view: the DTO's own words are the allowed entities and numbers, plus the case's allergies and the approved dose example text
            var allowedText = new StringBuilder();
            foreach (var x in facts) allowedText.Append(x.Value).Append(' ');
            foreach (var r in results) allowedText.Append(r).Append(' ');
            foreach (var e in events) allowedText.Append(e).Append(' ');
            foreach (var a in s.Case.Allergies) allowedText.Append(a.Substance).Append(' ');
            foreach (var t in keptTurns) allowedText.Append(t.Text).Append(' ');
            string allowedNormal = Validator.Norm(allowedText.ToString());
            var entities = lexicon.Categories.Values.SelectMany(v => v).Where(term => (" " + allowedNormal + " ").Contains(" " + Validator.Norm(term) + " ") || (" " + allowedNormal + " ").Contains(" " + Validator.Norm(term) + "s ")).Distinct().ToList();
            var numbers = System.Text.RegularExpressions.Regex.Matches(allowedText.ToString(), @"(?<![\w.])\d+(?:\.\d+)?(?![\w])").Select(m => m.Value).Distinct().ToList();
            var validation = new ValidationContext
            {
                AllowedIntents = intents,
                Facts = facts.Select(x => new ValidationFact(x.Id, x.Value, x.Disclosed, x.DisclosableNow)).Concat(hidden).ToList(),
                AllowedEntities = entities,
                Numbers = numbers,
            };
            return new BuiltContext(dto, validation);
        }
    }

    /// <summary>Why a character may speak: something happened (an event), the trainee asked (a question) or the character volunteers a worry (spontaneous). Higher comes first.</summary>
    public enum ReactionKind { Spontaneous = 0, Question = 1, Event = 2 }

    /// <summary>A reason to speak: the kind, a key (the same key within the debounce window counts once) and the sim second it arose.</summary>
    public sealed record Reaction(ReactionKind Kind, string Key, double AtSeconds);

    /// <summary>
    /// Decides who speaks next (p1-ai-language s03): an event outranks a question, which outranks a spontaneous remark; the same trigger within the debounce window counts once, and a patient makes at most one spontaneous remark
    /// every 8 sim seconds. Pure bookkeeping on sim seconds, so it replays the same.
    /// </summary>
    public sealed class ReactionQueue
    {
        public const double SpontaneousGapSeconds = 8;
        public const double DebounceSeconds = 4;

        private readonly List<Reaction> _queue = new List<Reaction>();
        private readonly Dictionary<string, double> _lastKey = new Dictionary<string, double>(StringComparer.Ordinal);
        private double _lastSpontaneous = double.NegativeInfinity;

        public int Count => _queue.Count;

        public bool Enqueue(Reaction r)
        {
            if (_lastKey.TryGetValue(r.Key, out var seen) && r.AtSeconds - seen < DebounceSeconds) return false;
            if (r.Kind == ReactionKind.Spontaneous && r.AtSeconds - _lastSpontaneous < SpontaneousGapSeconds) return false;
            _lastKey[r.Key] = r.AtSeconds;
            _queue.Add(r);
            return true;
        }

        /// <summary>The next reason to speak (highest kind first, then the oldest), or null.</summary>
        public Reaction? Next()
        {
            if (_queue.Count == 0) return null;
            var r = _queue.OrderByDescending(x => x.Kind).ThenBy(x => x.AtSeconds).ThenBy(x => x.Key, StringComparer.Ordinal).First();
            _queue.Remove(r);
            if (r.Kind == ReactionKind.Spontaneous) _lastSpontaneous = r.AtSeconds;
            return r;
        }
    }
}
