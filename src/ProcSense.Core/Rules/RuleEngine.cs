using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Rules
{
    /// <summary>What a rule does when it fires. A rule can raise a concern, ask for a review, block a step or add a score item; it never cancels surgery by itself.</summary>
    public enum RuleAction { Concern, RequireReview, Block, ScoreItem }

    public sealed record RuleTest(string Name, object? State, bool Expect);

    /// <summary>One rule with its provenance: where it comes from (register ids), who reviewed it (status) and the cases that prove it.</summary>
    public sealed class RuleRecord
    {
        public RuleRecord(string id, Logic when, RuleAction action, string text, string severity, IReadOnlyList<string> refs, string status, IReadOnlyList<RuleTest> tests)
        {
            Id = id; When = when; Action = action; Text = text; Severity = severity; Refs = refs; Status = status; Tests = tests;
        }

        public string Id { get; }
        public Logic When { get; }
        public RuleAction Action { get; }
        /// <summary>The message for a concern, review or block; the score item id for a score item.</summary>
        public string Text { get; }
        public string Severity { get; }
        public IReadOnlyList<string> Refs { get; }
        public string Status { get; }
        public IReadOnlyList<RuleTest> Tests { get; }
    }

    /// <summary>A rule that fired, with the provenance that goes into the session log.</summary>
    public sealed record RuleHit(string RuleId, RuleAction Action, string Text, string Severity, IReadOnlyList<string> Refs)
    {
        /// <summary>The log text, e.g. "rule:R-FAST-01 ref:SR-0030".</summary>
        public string Describe() => "rule:" + RuleId + " ref:" + string.Join(",", Refs);
    }

    public static class RuleLoader
    {
        private static readonly Regex Ref = new Regex("^SR-[0-9]{4}$", RegexOptions.CultureInvariant);
        private static readonly Regex Id = new Regex("^[A-Z][A-Z0-9-]*$", RegexOptions.CultureInvariant);

        /// <summary>Parses a rules file: {"rules":[ {id, when, then{kind,text}, severity, refs[], status, tests[]} ]}. Anything wrong is a ContentException that names the place.</summary>
        public static List<RuleRecord> Parse(string json, string source = "rules")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var list = root.Arr("rules");
            root.Finish();
            var rules = new List<RuleRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                string path = JsonObj.Index(source + ".rules", i);
                var o = new JsonObj(list[i], path);
                string id = o.Str("id");
                if (!Id.IsMatch(id)) throw new ContentException(o.Sub("id") + ": '" + id + "' must be upper-case letters, digits and dashes");
                if (!seen.Add(id)) throw new ContentException(o.Sub("id") + ": duplicate rule id '" + id + "'");

                var when = Logic.Compile(o.Any("when"), o.Sub("when"));
                var then = o.Obj("then");
                string kind = then.Enum("kind", "concern", "require_review", "block", "score_item");
                string text = then.Str("text");
                then.Finish();
                if (text.Trim().Length == 0) throw new ContentException(then.Sub("text") + ": must not be empty");
                string severity = o.Enum("severity", "low", "medium", "high");

                var refs = new List<string>();
                var rawRefs = o.Arr("refs");
                if (rawRefs.Count == 0) throw new ContentException(o.Sub("refs") + ": a rule needs at least one source-register id");
                for (int r = 0; r < rawRefs.Count; r++)
                    refs.Add(rawRefs[r] is string s && Ref.IsMatch(s) ? s : throw new ContentException(JsonObj.Index(o.Sub("refs"), r) + ": expected a register id like SR-0001"));

                string status = o.Enum("status", "DRAFT", "REVIEWED", "APPROVED");

                var rawTests = o.Arr("tests");
                if (rawTests.Count == 0) throw new ContentException(o.Sub("tests") + ": a rule needs at least one test case");
                var tests = new List<RuleTest>();
                for (int t = 0; t < rawTests.Count; t++)
                {
                    var to = new JsonObj(rawTests[t], JsonObj.Index(o.Sub("tests"), t));
                    tests.Add(new RuleTest(to.Str("name"), to.Any("state"), to.Bool("expect")));
                    to.Finish();
                }
                o.Finish();

                var action = kind switch
                {
                    "concern" => RuleAction.Concern,
                    "require_review" => RuleAction.RequireReview,
                    "block" => RuleAction.Block,
                    _ => RuleAction.ScoreItem,
                };
                rules.Add(new RuleRecord(id, when, action, text, severity, refs, status, tests));
            }
            return rules;
        }

        /// <summary>Runs every rule's own test cases. Returns one line per failure; empty means every rule does what its tests say.</summary>
        public static List<string> RunOwnTests(IEnumerable<RuleRecord> rules)
        {
            var failures = new List<string>();
            foreach (var rule in rules)
                foreach (var test in rule.Tests)
                {
                    bool actual = rule.When.Test(test.State);
                    if (actual != test.Expect)
                        failures.Add(rule.Id + " / " + test.Name + ": expected " + (test.Expect ? "fires" : "does not fire") + ", got " + (actual ? "fires" : "does not fire"));
                }
            return failures;
        }
    }

    /// <summary>Evaluates a set of rules against the current state and reports what fired. Rules run in id order, so results are deterministic.</summary>
    public sealed class RuleEngine
    {
        private readonly List<RuleRecord> _rules;

        public RuleEngine(IEnumerable<RuleRecord> rules)
        {
            _rules = rules.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        }

        public int Count => _rules.Count;

        public List<RuleHit> Evaluate(object? state)
        {
            var hits = new List<RuleHit>();
            foreach (var rule in _rules)
                if (rule.When.Test(state)) hits.Add(new RuleHit(rule.Id, rule.Action, rule.Text, rule.Severity, rule.Refs));
            return hits;
        }

        /// <summary>Evaluates and writes each hit into the action's effects, so the session log shows which rule fired and from which source.</summary>
        public List<RuleHit> Apply(object? state, ActionOutcome outcome)
        {
            var hits = Evaluate(state);
            foreach (var hit in hits) outcome.Effects.Add(hit.Describe());
            return hits;
        }
    }
}
