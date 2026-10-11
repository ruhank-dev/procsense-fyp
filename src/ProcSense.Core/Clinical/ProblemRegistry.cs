using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>One kind of problem: where it sits on the 7-step ladder and its default Part D priority (P1 to P4).</summary>
    public sealed record ProblemType(string Id, int LadderRank, int Priority, string Effect, string Ref);

    public sealed record LadderRung(int Rank, string Question, string Action, string Ref);

    /// <summary>When a problem of a case joins the registry: the rule over what the engine knows (a result opened, a finding, a topic asked, a flag), in words, and the register id (content/problems/discovery.json).</summary>
    public sealed record ProblemDiscovery(string Case, string Problem, Rules.Logic Trigger, string How, string Ref);

    /// <summary>The six problem types, the seven ladder rungs and the four priorities (content/problems/problems.json, built from the verified tables).</summary>
    public sealed class ProblemTables
    {
        public static readonly ProblemTables Empty = new ProblemTables(new List<ProblemType>(), new List<LadderRung>(), new List<ProblemDiscovery>());

        public ProblemTables(IReadOnlyList<ProblemType> types, IReadOnlyList<LadderRung> ladder, IReadOnlyList<ProblemDiscovery> discovery) { Types = types; Ladder = ladder; Discovery = discovery; }

        public IReadOnlyList<ProblemType> Types { get; }
        public IReadOnlyList<LadderRung> Ladder { get; }
        public IReadOnlyList<ProblemDiscovery> Discovery { get; }

        public ProblemType? Find(string type) => Types.FirstOrDefault(t => t.Id == type);

        public static ProblemTables Parse(string json, string source = "problems", string? discoveryJson = null)
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var types = new List<ProblemType>();
            var rawTypes = root.Arr("types");
            for (int i = 0; i < rawTypes.Count; i++)
            {
                var o = new JsonObj(rawTypes[i], JsonObj.Index(root.Sub("types"), i));
                string id = o.Str("id");
                if (types.Any(t => t.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate type '" + id + "'");
                int rung = (int)o.Int("ladder_rank"), priority = (int)o.Int("priority");
                if (rung < 1 || rung > 7) throw new ContentException(o.Sub("ladder_rank") + ": needs 1 to 7");
                if (priority < 1 || priority > 4) throw new ContentException(o.Sub("priority") + ": needs 1 to 4");
                types.Add(new ProblemType(id, rung, priority, o.Str("effect"), o.Str("ref")));
                o.Str("examples"); o.Finish();
            }
            var ladder = new List<LadderRung>();
            var rawLadder = root.Arr("ladder");
            for (int i = 0; i < rawLadder.Count; i++)
            {
                var o = new JsonObj(rawLadder[i], JsonObj.Index(root.Sub("ladder"), i));
                ladder.Add(new LadderRung((int)o.Int("rank"), o.Str("question"), o.Str("action"), o.Str("ref")));
                o.Finish();
            }
            if (ladder.Select(x => x.Rank).SequenceEqual(Enumerable.Range(1, 7)) == false) throw new ContentException(root.Sub("ladder") + ": needs the rungs 1 to 7 in order");
            var rawPriorities = root.Arr("priorities");
            for (int i = 0; i < rawPriorities.Count; i++) { var o = new JsonObj(rawPriorities[i], JsonObj.Index(root.Sub("priorities"), i)); o.Int("priority"); o.Str("meaning"); o.Str("ref"); o.Finish(); }
            if (rawPriorities.Count != 4) throw new ContentException(root.Sub("priorities") + ": needs P1 to P4");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            var discovery = new List<ProblemDiscovery>();
            if (discoveryJson != null)
            {
                var dj = new JsonObj(MiniJson.Parse(discoveryJson), "problems/discovery");
                var rawD = dj.Arr("discovery");
                for (int i = 0; i < rawD.Count; i++)
                {
                    var o = new JsonObj(rawD[i], JsonObj.Index(dj.Sub("discovery"), i));
                    discovery.Add(new ProblemDiscovery(o.Str("case"), o.Str("problem"), Rules.Logic.Compile(o.Any("trigger"), o.Sub("trigger")), o.Str("how"), o.Str("ref")));
                    o.Finish();
                }
                foreach (var r in dj.Arr("refs")) if (!(r is string)) throw new ContentException(dj.Sub("refs") + ": expected register ids");
                dj.Finish();
            }
            return new ProblemTables(types, ladder, discovery);
        }
    }

    /// <summary>How hard the whole case should be run, set by the golden rules (14B.2). Advice for the trainee: it never cancels a case by itself.</summary>
    public enum CasePace { Proceed, Emergency, Postpone, PostponeCertain, Parallel }

    /// <summary>One problem found in the case.</summary>
    public sealed class Problem
    {
        public Problem(string id, string type, string label, int ladderRank, int priority, int order) { Id = id; Type = type; Label = label; LadderRank = ladderRank; Priority = priority; Order = order; }

        public string Id { get; }
        public string Type { get; }
        public string Label { get; }
        /// <summary>The rung of the 7-step ladder the type sits at (1 = most urgent).</summary>
        public int LadderRank { get; }
        /// <summary>Part D priority 1 to 4.</summary>
        public int Priority { get; }
        /// <summary>Order the problem was registered in (the content order breaks ties).</summary>
        public int Order { get; }
        public bool Resolved { get; internal set; }
        /// <summary>Information-only problems are kept for the handover but not shown while the trainee works.</summary>
        public bool Hidden => Type == "information_only";
    }

    /// <summary>
    /// The problems found so far in a case. They are ordered by Part D priority, then the ladder rung of their type, then the order they were found in, so the same case always gives the same order.
    /// The golden rules (14B.2) are applied on top: the most dangerous problem sets the pace, a planned case's problems add up to postponing, an emergency case's to preparing in parallel, and the handover lists them all.
    /// </summary>
    public sealed class ProblemRegistry
    {
        private readonly ProblemTables _tables;
        private readonly string _scenario;
        private readonly List<Problem> _all = new List<Problem>();

        public ProblemRegistry(ProblemTables tables, string scenario) { _tables = tables; _scenario = scenario; }

        /// <summary>Registers a problem (once: asking again for the same id returns the first). The priority is the type's default unless the case states one (Part D).</summary>
        public Problem Register(string id, string type, string label, int? priority = null)
        {
            var existing = _all.FirstOrDefault(p => p.Id == id);
            if (existing != null) return existing;
            var t = _tables.Find(type) ?? throw new ArgumentException("unknown problem type '" + type + "'", nameof(type));
            var p = new Problem(id, type, label, t.LadderRank, Math.Min(4, Math.Max(1, priority ?? t.Priority)), _all.Count);
            _all.Add(p);
            return p;
        }

        /// <summary>Registers every problem a case file lists, with the priority the case states (its rank, at most 4).</summary>
        public static ProblemRegistry FromCase(CaseFile file, ProblemTables tables, IEnumerable<string>? activeVariants = null)
        {
            var reg = new ProblemRegistry(tables, file.Scenario);
            var active = new HashSet<string>(activeVariants ?? Array.Empty<string>(), StringComparer.Ordinal);
            bool playingAVariant = active.Any(v => file.VariantEndStates.ContainsKey(v));
            foreach (var p in file.Problems)
            {
                // a problem tied to a variant exists only there; 'base' belongs to the case when no other variant is being played
                bool applies = p.Variant == null || active.Contains(p.Variant) || (p.Variant == "base" && !playingAVariant);
                if (applies) reg.Register(p.Id, p.Type, p.Label, (int)p.Rank);
            }
            return reg;
        }

        public bool Resolve(string id)
        {
            var p = _all.FirstOrDefault(x => x.Id == id);
            if (p == null || p.Resolved) return false;
            p.Resolved = true;
            return true;
        }

        public IReadOnlyList<Problem> Ordered => _all.OrderBy(p => p.Priority).ThenBy(p => p.LadderRank).ThenBy(p => p.Order).ToList();

        public IReadOnlyList<Problem> Open => Ordered.Where(p => !p.Resolved).ToList();

        /// <summary>What the trainee sees while working: open problems except the information-only ones.</summary>
        public IReadOnlyList<Problem> Visible => Open.Where(p => !p.Hidden).ToList();

        /// <summary>The handover lists every problem, resolved or not, information-only included (the commonest stacked-case error is forgetting some).</summary>
        public IReadOnlyList<Problem> Handover => Ordered;

        /// <summary>The problem to deal with first, or null.</summary>
        public Problem? First => Open.FirstOrDefault();

        /// <summary>A life threat switches the whole case to emergency mode.</summary>
        public bool Emergency => _all.Any(p => !p.Resolved && p.Type == "life_threat");

        /// <summary>A pathway-changing problem means a new diagnosis: it beats the old plan.</summary>
        public Problem? NewDiagnosis => Open.FirstOrDefault(p => p.Type == "pathway_changing");

        public CasePace Pace
        {
            get
            {
                if (Emergency) return CasePace.Emergency;
                int fixable = _all.Count(p => !p.Resolved && !p.Hidden);
                if (fixable == 0) return CasePace.Proceed;
                if (_scenario == "S1") return fixable >= 2 ? CasePace.PostponeCertain : CasePace.Postpone;
                return CasePace.Parallel;
            }
        }
    }
}
