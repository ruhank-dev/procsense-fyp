using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;

namespace ProcSense.Core.AIContext
{
    /// <summary>The words an AI-written line may not use unless the case allows them (content/ai/lexicon.json).</summary>
    public sealed class Lexicon
    {
        public Lexicon(IReadOnlyDictionary<string, IReadOnlyList<string>> categories, IReadOnlyDictionary<string, IReadOnlyList<string>> events) { Categories = categories; Events = events; }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Categories { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Events { get; }

        public static Lexicon Parse(string json)
        {
            var root = (Dictionary<string, object?>)MiniJson.Parse(json)!;
            var cats = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var kv in (Dictionary<string, object?>)root["categories"]!) cats[kv.Key] = Strings(kv.Value);
            var events = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var kv in (Dictionary<string, object?>)root["events"]!) events[kv.Key] = Strings(kv.Value);
            return new Lexicon(cats, events);
        }

        internal static List<string> Strings(object? o) => ((List<object?>)o!).Select(x => (string)x!).ToList();
    }

    /// <summary>One fact the model was given. Disclosed and DisclosableNow decide whether its value may appear in what the model writes.</summary>
    public sealed record ValidationFact(string Id, string Value, bool Disclosed, bool DisclosableNow);

    /// <summary>One fact of the debrief: its verdict (credit, penalty, critical) and the numbers it states.</summary>
    public sealed record ValidationDebriefFact(string Id, string Verdict, IReadOnlyList<string> Numbers);

    /// <summary>Everything the validator needs to judge one output (the same fields as the Python twin).</summary>
    public sealed class ValidationContext
    {
        public IReadOnlyList<string> AllowedIntents { get; set; } = new List<string>();
        public IReadOnlyList<ValidationFact> Facts { get; set; } = new List<ValidationFact>();
        public IReadOnlyList<ValidationDebriefFact> DebriefFacts { get; set; } = new List<ValidationDebriefFact>();
        public IReadOnlyList<string> AllowedEntities { get; set; } = new List<string>();
        public IReadOnlyList<string> Numbers { get; set; } = new List<string>();
        public IReadOnlyList<string> DoseText { get; set; } = new List<string>();
        public IReadOnlyList<string> LogEvents { get; set; } = new List<string>();

        public static ValidationContext Parse(object? json)
        {
            var d = (Dictionary<string, object?>)json!;
            var c = new ValidationContext();
            if (d.TryGetValue("allowed_intents", out var ai)) c.AllowedIntents = Lexicon.Strings(ai);
            if (d.TryGetValue("facts", out var f))
                c.Facts = ((List<object?>)f!).Select(x => (Dictionary<string, object?>)x!).Select(x => new ValidationFact((string)x["id"]!, (string)x["value"]!, x.TryGetValue("disclosed", out var a) && a is true, x.TryGetValue("disclosable_now", out var b) && b is true)).ToList();
            if (d.TryGetValue("debrief_facts", out var df))
                c.DebriefFacts = ((List<object?>)df!).Select(x => (Dictionary<string, object?>)x!).Select(x => new ValidationDebriefFact((string)x["id"]!, (string)x["verdict"]!, x.TryGetValue("numbers", out var n) ? Lexicon.Strings(n) : new List<string>())).ToList();
            if (d.TryGetValue("allowed_entities", out var ae)) c.AllowedEntities = Lexicon.Strings(ae);
            if (d.TryGetValue("numbers", out var nu)) c.Numbers = Lexicon.Strings(nu);
            if (d.TryGetValue("dose_text", out var dt)) c.DoseText = Lexicon.Strings(dt);
            if (d.TryGetValue("log_events", out var le)) c.LogEvents = Lexicon.Strings(le);
            return c;
        }
    }

    /// <summary>
    /// The grounding validator (p1-ai-language s01): nothing an AI writes reaches a trainee unchecked. This is the C# twin of server/validation/validator.py; both run tests/vectors/grounding_vectors.json and must give
    /// the same codes (plan test T-GV5). It reads no model, no network and no clock. See the Python module for what each code means.
    /// </summary>
    public static class Validator
    {
        private static readonly string[] Categories = { "drug", "test", "condition", "allergen", "equipment" };
        private static readonly HashSet<string> Filler = new HashSet<string>(StringComparer.Ordinal) { "the", "a", "an", "and", "or", "of", "to", "in", "on", "is", "was", "were", "it", "i", "my", "me", "you", "your", "for", "with", "at", "by", "that", "this", "have", "has", "had", "not", "no", "yes", "be", "been", "are", "am" };
        private static readonly string[] Negative = { "missed", "failed", "omitted", "did not", "didn't", "wrong", "error", "mistake", "unsafe", "forgot", "skipped" };
        private static readonly string[] Positive = { "well done", "correct", "good", "excellent", "right", "great", "successfully" };
        private static readonly Regex Number = new Regex(@"(?<![\w.])\d+(?:\.\d+)?(?![\w])", RegexOptions.CultureInvariant);
        private static readonly Regex Dose = new Regex(@"(?<![\w.])\d+(?:\.\d+)?\s?(?:mg|mcg|micrograms?|ml|units?|iu|g)(?![\w/])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Prescribe = new Regex(@"\b(?:you should|i recommend|i suggest|please|let's|lets)\s+(?:give|prescribe|administer|start|giving|prescribing)\b|\bprescrib(?:e|ing)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Runs = new Regex(" +", RegexOptions.CultureInvariant);
        private static readonly Regex Other = new Regex("[^a-z0-9\\-\u0001]+", RegexOptions.CultureInvariant);
        private static readonly Regex Point = new Regex(@"(?<=\d)\.(?=\d)", RegexOptions.CultureInvariant);

        public static string Norm(string text)
        {
            string t = text.ToLowerInvariant().Replace('’', '\'');
            t = Point.Replace(t, "\u0001");
            t = Other.Replace(t, " ");
            return Runs.Replace(t.Replace('\u0001', '.'), " ").Trim();
        }

        private static bool HasTerm(string normalised, string term)
        {
            string padded = " " + normalised + " ", t = Norm(term);
            return padded.Contains(" " + t + " ") || padded.Contains(" " + t + "s ");
        }

        private static List<string> Numbers(string text) => Number.Matches(text).Select(m => m.Value.EndsWith(".0", StringComparison.Ordinal) ? m.Value.Substring(0, m.Value.Length - 2) : m.Value).ToList();

        private static List<string> ContentWords(string value) => Norm(value).Split(' ').Where(w => w.Length > 2 && !Filler.Contains(w) && !w.All(char.IsDigit)).ToList();

        /// <summary>The sorted unique codes of everything wrong with the output (empty: it may be shown). <paramref name="output"/> is the parsed JSON the model wrote.</summary>
        public static IReadOnlyList<string> Validate(object? output, ValidationContext context, Lexicon lexicon)
        {
            var codes = new SortedSet<string>(StringComparer.Ordinal);
            var o = Schema(output);
            if (o == null) return new[] { "SCHEMA" };
            bool utterance = (string)o["kind"]! == "utterance";
            var sections = utterance ? null : ((List<object?>)o["sections"]!).Select(x => (Dictionary<string, object?>)x!).ToList();
            var texts = utterance ? new List<string> { (string)o["text"]! } : sections!.Select(s => (string)s["text"]!).ToList();
            var factIds = utterance ? Lexicon.Strings(o["fact_ids"]) : sections!.SelectMany(s => Lexicon.Strings(s["fact_ids"])).ToList();
            string whole = string.Join(" ", texts), normal = Norm(whole);

            HashSet<string> known;
            if (utterance)
            {
                if (!context.AllowedIntents.Contains((string)o["intent"]!)) codes.Add("UNKNOWN_INTENT");
                known = new HashSet<string>(context.Facts.Select(f => f.Id), StringComparer.Ordinal);
            }
            else known = new HashSet<string>(context.DebriefFacts.Select(f => f.Id), StringComparer.Ordinal);
            if (factIds.Any(f => !known.Contains(f))) codes.Add("UNKNOWN_FACT");

            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in context.AllowedEntities)
            {
                string n = Norm(a);
                allowed.Add(n); allowed.Add(n + "s");                                    // singular and plural of an allowed word are the same word
                if (n.EndsWith("s", StringComparison.Ordinal)) allowed.Add(n.Substring(0, n.Length - 1));
            }
            foreach (var cat in Categories)
                foreach (var term in lexicon.Categories[cat])
                    if (!allowed.Contains(Norm(term)) && HasTerm(normal, term)) codes.Add("OFF_CARD_ENTITY");

            var allowedNumbers = new HashSet<string>(context.Numbers.Select(n => n.EndsWith(".0", StringComparison.Ordinal) ? n.Substring(0, n.Length - 2) : n), StringComparer.Ordinal);
            if (!utterance) foreach (var f in context.DebriefFacts) foreach (var n in f.Numbers) allowedNumbers.Add(n);
            var wholeNumbers = Numbers(whole);
            if (wholeNumbers.Any(n => !allowedNumbers.Contains(n))) codes.Add("UNGROUNDED_NUMBER");

            if (utterance)
            {
                var tokens = new HashSet<string>(normal.Split(' '), StringComparer.Ordinal);
                foreach (var f in context.Facts)
                {
                    if (f.Disclosed || f.DisclosableNow) continue;
                    var nums = Numbers(f.Value);
                    var words = ContentWords(f.Value);
                    if (nums.Count > 0 && nums.All(n => wholeNumbers.Contains(n))) codes.Add("DISCLOSURE_LEAK");
                    else if (words.Count >= 2 && (double)words.Count(w => tokens.Contains(w)) / words.Count >= 0.8) codes.Add("DISCLOSURE_LEAK");
                }
            }

            if (Dose.IsMatch(whole) && !context.DoseText.Any(e => whole.Contains(e))) codes.Add("DOSE_STATEMENT");
            if (Prescribe.IsMatch(whole)) codes.Add("PRESCRIBE_PHRASING");

            if (!utterance)
            {
                var facts = context.DebriefFacts.ToDictionary(f => f.Id, StringComparer.Ordinal);
                var covered = new HashSet<string>(factIds, StringComparer.Ordinal);
                if (facts.Keys.Any(f => !covered.Contains(f))) codes.Add("DEBRIEF_MISSING_FACT");
                foreach (var s in sections!)
                {
                    string text = (string)s["text"]!, sn = Norm(text);
                    var used = Lexicon.Strings(s["fact_ids"]).Where(facts.ContainsKey).Select(f => facts[f]).ToList();
                    var textNumbers = Numbers(text);
                    foreach (var f in used) if (f.Numbers.Any(n => !textNumbers.Contains(n))) codes.Add("DEBRIEF_NUMBER_CHANGED");
                    var polarity = new HashSet<string>(used.Select(f => f.Verdict), StringComparer.Ordinal);
                    if (polarity.Count == 1 && polarity.Contains("credit") && Negative.Any(w => HasTerm(sn, w))) codes.Add("DEBRIEF_VERDICT_CHANGED");
                    if (polarity.Count > 0 && polarity.All(p => p == "penalty" || p == "critical") && Positive.Any(w => HasTerm(sn, w))) codes.Add("DEBRIEF_VERDICT_CHANGED");
                }
                var logged = new HashSet<string>(context.LogEvents, StringComparer.Ordinal);
                foreach (var kv in lexicon.Events)
                    if (!logged.Contains(kv.Key) && kv.Value.Any(w => HasTerm(normal, w))) codes.Add("DEBRIEF_NEW_EVENT");
            }
            return codes.ToList();
        }

        // the output has exactly the keys, types and sizes of its kind, or null
        private static Dictionary<string, object?>? Schema(object? output)
        {
            if (!(output is Dictionary<string, object?> d) || !d.TryGetValue("kind", out var kind)) return null;
            if ("utterance".Equals(kind))
            {
                if (!d.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "fact_ids", "intent", "kind", "role", "text" })) return null;
                if (!(d["role"] is string) || !(d["intent"] is string) || !(d["text"] is string t) || t.Length == 0 || t.Length > 400) return null;
                return IsStrings(d["fact_ids"]) ? d : null;
            }
            if ("debrief".Equals(kind))
            {
                if (!d.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "kind", "sections" })) return null;
                if (!(d["sections"] is List<object?> s) || s.Count == 0) return null;
                foreach (var x in s)
                {
                    if (!(x is Dictionary<string, object?> sec) || !sec.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "fact_ids", "text", "title" })) return null;
                    if (!(sec["title"] is string) || !(sec["text"] is string tx) || tx.Length == 0 || !IsStrings(sec["fact_ids"])) return null;
                }
                return d;
            }
            return null;
        }

        private static bool IsStrings(object? o) => o is List<object?> l && l.All(x => x is string);
    }
}
