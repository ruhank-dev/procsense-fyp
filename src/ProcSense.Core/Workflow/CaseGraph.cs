using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Workflow
{
    /// <summary>A thing the trainee must get done (for example "identity confirmed"). Complete when the rule says so; missed when its deadline passes first.</summary>
    public sealed class GoalDef
    {
        public GoalDef(string id, int step, string label, string hint, Logic appliesIf, Logic completeIf, IReadOnlyList<string> after, double? deadlineMinutes,
            IReadOnlyList<string> onMissedFlags, IReadOnlyList<string> onMissedEvents, IReadOnlyList<string> scoreItems, IReadOnlyList<string> refs, Logic? activeIf = null)
        {
            ActiveIf = activeIf ?? Logic.Compile(true, "active_if");
            Id = id; Step = step; Label = label; Hint = hint; AppliesIf = appliesIf; CompleteIf = completeIf; After = after; DeadlineMinutes = deadlineMinutes;
            OnMissedFlags = onMissedFlags; OnMissedEvents = onMissedEvents; ScoreItems = scoreItems; Refs = refs;
        }

        public string Id { get; }
        public int Step { get; }
        public string Label { get; }
        /// <summary>Shown as the practice-mode hint while this is the next goal.</summary>
        public string Hint { get; }
        public Logic AppliesIf { get; }
        /// <summary>The goal only exists for the trainee once this holds (for example "call the senior now" appears when a rigid abdomen is found). Defaults to always.</summary>
        public Logic ActiveIf { get; }
        public Logic CompleteIf { get; }
        /// <summary>Goals that must be complete before this one can complete (a hard order). Cycles are a load error.</summary>
        public IReadOnlyList<string> After { get; }
        /// <summary>Minutes of simulated time from the start of the session, or null for no deadline.</summary>
        public double? DeadlineMinutes { get; }
        public IReadOnlyList<string> OnMissedFlags { get; }
        public IReadOnlyList<string> OnMissedEvents { get; }
        public IReadOnlyList<string> ScoreItems { get; }
        public IReadOnlyList<string> Refs { get; }
    }

    public sealed class OptionDef
    {
        public OptionDef(string id, string label, IReadOnlyList<string> setFlags, IReadOnlyList<string> eventsEnable, string scoreItem)
        {
            Id = id; Label = label; SetFlags = setFlags; EventsEnable = eventsEnable; ScoreItem = scoreItem;
        }

        public string Id { get; }
        public string Label { get; }
        public IReadOnlyList<string> SetFlags { get; }
        public IReadOnlyList<string> EventsEnable { get; }
        public string ScoreItem { get; }
    }

    /// <summary>A moment where the trainee must choose. The engine knows which options are expected; the trainee is never shown that.</summary>
    public sealed class DecisionPointDef
    {
        public DecisionPointDef(string id, int step, string label, Logic trigger, IReadOnlyList<OptionDef> options, IReadOnlyList<string> expected, IReadOnlyList<string> refs)
        {
            Id = id; Step = step; Label = label; Trigger = trigger; Options = options; Expected = expected; Refs = refs;
        }

        public string Id { get; }
        public int Step { get; }
        public string Label { get; }
        public Logic Trigger { get; }
        public IReadOnlyList<OptionDef> Options { get; }
        public IReadOnlyList<string> Expected { get; }
        public IReadOnlyList<string> Refs { get; }
    }

    /// <summary>A safety check that must pass before certain verbs (for example transfer, Sign In, Time Out). The message always says why.</summary>
    public sealed class BlockerDef
    {
        public BlockerDef(string id, IReadOnlyList<string> checkAt, Logic blockedIf, string message, IReadOnlyList<string> refs)
        {
            Id = id; CheckAt = checkAt; BlockedIf = blockedIf; Message = message; Refs = refs;
        }

        public string Id { get; }
        public IReadOnlyList<string> CheckAt { get; }
        public Logic BlockedIf { get; }
        public string Message { get; }
        public IReadOnlyList<string> Refs { get; }
    }

    /// <summary>Everything a case's workflow needs, loaded from content/workflow.</summary>
    public sealed class CaseGraph
    {
        public static readonly CaseGraph Empty = new CaseGraph(new List<GoalDef>(), new List<DecisionPointDef>(), new List<BlockerDef>());

        public CaseGraph(IReadOnlyList<GoalDef> goals, IReadOnlyList<DecisionPointDef> decisionPoints, IReadOnlyList<BlockerDef> blockers)
        {
            Goals = goals; DecisionPoints = decisionPoints; Blockers = blockers;
        }

        public IReadOnlyList<GoalDef> Goals { get; }
        public IReadOnlyList<DecisionPointDef> DecisionPoints { get; }
        public IReadOnlyList<BlockerDef> Blockers { get; }

        private static readonly Regex Id = new Regex("^[A-Z][A-Z0-9-]*$", RegexOptions.CultureInvariant);
        private static readonly Regex OptionId = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex Ref = new Regex("^SR-[0-9]{4}$", RegexOptions.CultureInvariant);
        private static readonly Regex Name = new Regex("^[a-z][a-z0-9_.]*$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses {"goals":[...],"decision_points":[...],"blockers":[...]}. Every problem is a ContentException that names the place:
        /// unknown fields, duplicate ids, goals that wait on a goal that does not exist or on each other (the message lists the cycle),
        /// decision points whose expected option does not exist, blockers without a message, and anything without a source-register id.
        /// </summary>
        public static CaseGraph Parse(string json, string source = "workflow")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var goals = ParseList(root, "goals", Goal);
            var points = ParseList(root, "decision_points", DecisionPoint);
            var blockers = ParseList(root, "blockers", Blocker);
            root.Finish();

            RequireUnique(goals.Select(g => g.Id), source + ".goals");
            RequireUnique(points.Select(p => p.Id), source + ".decision_points");
            RequireUnique(blockers.Select(b => b.Id), source + ".blockers");

            var ids = new HashSet<string>(goals.Select(g => g.Id), StringComparer.Ordinal);
            foreach (var g in goals)
                foreach (var a in g.After)
                    if (!ids.Contains(a)) throw new ContentException(source + ".goals." + g.Id + ".after: goal '" + a + "' does not exist");
            var cycle = FindCycle(goals);
            if (cycle != null) throw new ContentException(source + ".goals: goals wait on each other in a circle: " + string.Join(" -> ", cycle));
            return new CaseGraph(goals, points, blockers);
        }

        private static List<T> ParseList<T>(JsonObj root, string key, Func<JsonObj, T> read)
        {
            var raw = root.Arr(key);
            var list = new List<T>();
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub(key), i));
                list.Add(read(o));
                o.Finish();
            }
            return list;
        }

        private static void RequireUnique(IEnumerable<string> ids, string path)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids) if (!seen.Add(id)) throw new ContentException(path + ": duplicate id '" + id + "'");
        }

        private static string IdOf(JsonObj o)
        {
            string id = o.Str("id");
            if (!Id.IsMatch(id)) throw new ContentException(o.Sub("id") + ": '" + id + "' must be upper-case letters, digits and dashes");
            return id;
        }

        private static List<string> Strings(JsonObj o, string key, Regex? pattern = null, string expected = "text", bool atLeastOne = false)
        {
            var raw = o.Arr(key);
            if (atLeastOne && raw.Count == 0) throw new ContentException(o.Sub(key) + ": needs at least one entry");
            var list = new List<string>();
            for (int i = 0; i < raw.Count; i++)
                list.Add(raw[i] is string s && (pattern == null || pattern.IsMatch(s)) ? s : throw new ContentException(JsonObj.Index(o.Sub(key), i) + ": expected " + expected));
            return list;
        }

        private static List<string> Refs(JsonObj o) => Strings(o, "refs", Ref, "a register id like SR-0001", atLeastOne: true);

        private static GoalDef Goal(JsonObj o)
        {
            string id = IdOf(o);
            int step = (int)o.Int("step");
            if (step < 1 || step > 15) throw new ContentException(o.Sub("step") + ": must be 1 to 15 (the 15 steps of the walkthrough)");
            string label = o.Str("label"), hint = o.Str("hint");
            var applies = Logic.Compile(o.Any("applies_if"), o.Sub("applies_if"));
            var active = o.Has("active_if") ? Logic.Compile(o.Any("active_if"), o.Sub("active_if")) : null;
            var complete = Logic.Compile(o.Any("complete_if"), o.Sub("complete_if"));
            var after = Strings(o, "after", Id, "a goal id");
            double? deadline = null;
            if (o.Has("deadline_min"))
            {
                double d = o.Num("deadline_min");
                if (d <= 0) throw new ContentException(o.Sub("deadline_min") + ": must be more than 0");
                deadline = d;
            }
            var missed = o.Obj("on_missed");
            var flags = Strings(missed, "flags", Name, "a flag name like missed.allergy");
            var events = Strings(missed, "events", Id, "an event id like E-ALLERGY-MISSED");
            missed.Finish();
            return new GoalDef(id, step, label, hint, applies, complete, after, deadline, flags, events, Strings(o, "score_items"), Refs(o), active);
        }

        private static DecisionPointDef DecisionPoint(JsonObj o)
        {
            string id = IdOf(o);
            int step = (int)o.Int("step");
            if (step < 1 || step > 15) throw new ContentException(o.Sub("step") + ": must be 1 to 15");
            var trigger = Logic.Compile(o.Any("trigger"), o.Sub("trigger"));
            var rawOptions = o.Arr("options");
            if (rawOptions.Count < 2) throw new ContentException(o.Sub("options") + ": a decision needs at least two options");
            var options = new List<OptionDef>();
            for (int i = 0; i < rawOptions.Count; i++)
            {
                var oo = new JsonObj(rawOptions[i], JsonObj.Index(o.Sub("options"), i));
                string optionId = oo.Str("id");
                if (!OptionId.IsMatch(optionId)) throw new ContentException(oo.Sub("id") + ": '" + optionId + "' must be lower-case letters, digits and underscores");
                if (options.Any(x => x.Id == optionId)) throw new ContentException(oo.Sub("id") + ": duplicate option '" + optionId + "'");
                options.Add(new OptionDef(optionId, oo.Str("label"), Strings(oo, "set_flags", Name, "a flag name"), Strings(oo, "events_enable", Id, "an event id like E-OT-DELAY"), oo.Str("score_item")));
                oo.Finish();
            }
            var expected = Strings(o, "expected", atLeastOne: true);
            foreach (var e in expected)
                if (options.All(x => x.Id != e)) throw new ContentException(o.Sub("expected") + ": '" + e + "' is not one of the options");
            return new DecisionPointDef(id, step, o.Str("label"), trigger, options, expected, Refs(o));
        }

        private static BlockerDef Blocker(JsonObj o)
        {
            string id = IdOf(o);
            var verbs = Strings(o, "check_at", Name, "a verb name", atLeastOne: true);
            var blockedIf = Logic.Compile(o.Any("blocked_if"), o.Sub("blocked_if"));
            string message = o.Str("message");
            if (message.Trim().Length == 0) throw new ContentException(o.Sub("message") + ": a blocker must say why it blocks");
            return new BlockerDef(id, verbs, blockedIf, message, Refs(o));
        }

        /// <summary>The first circle in the "after" edges as a path that returns to where it started (A -> B -> A), or null.</summary>
        private static List<string>? FindCycle(IReadOnlyList<GoalDef> goals)
        {
            var after = goals.ToDictionary(g => g.Id, g => g.After, StringComparer.Ordinal);
            var state = new Dictionary<string, int>(StringComparer.Ordinal);   // 1 = on the current path, 2 = done
            var path = new List<string>();

            List<string>? Visit(string id)
            {
                state[id] = 1;
                path.Add(id);
                foreach (var next in after[id])
                {
                    if (state.TryGetValue(next, out var s))
                    {
                        if (s == 1) { var cycle = path.Skip(path.IndexOf(next)).ToList(); cycle.Add(next); return cycle; }
                        continue;
                    }
                    var found = Visit(next);
                    if (found != null) return found;
                }
                path.RemoveAt(path.Count - 1);
                state[id] = 2;
                return null;
            }

            foreach (var g in goals.OrderBy(g => g.Id, StringComparer.Ordinal))
                if (!state.ContainsKey(g.Id)) { var found = Visit(g.Id); if (found != null) return found; }
            return null;
        }
    }
}
