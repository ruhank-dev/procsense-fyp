using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Workflow
{
    public enum FlowState { Waiting, Active, Done, Skipped }

    /// <summary>
    /// One stage of a scenario master map. It works through some of the 15 steps, runs on a track, waits for other stages (<see cref="After"/>),
    /// and an interrupt stage (deterioration, emergency) stops the tracks it names until it is resolved.
    /// </summary>
    public sealed class FlowStageDef
    {
        public FlowStageDef(string id, int number, string title, string track, IReadOnlyList<int> steps, IReadOnlyList<string> after, Logic appliesIf, Logic startIf, Logic doneIf,
            bool interrupt, IReadOnlyList<string> suspends, IReadOnlyList<string> refs)
        {
            Id = id; Number = number; Title = title; Track = track; Steps = steps; After = after; AppliesIf = appliesIf; StartIf = startIf; DoneIf = doneIf;
            Interrupt = interrupt; Suspends = suspends; Refs = refs;
        }

        public string Id { get; }
        /// <summary>1 to 20, the position in the scenario map.</summary>
        public int Number { get; }
        public string Title { get; }
        public string Track { get; }
        /// <summary>The walkthrough steps (canonical ids 1 to 15) this stage works through.</summary>
        public IReadOnlyList<int> Steps { get; }
        public IReadOnlyList<string> After { get; }
        public Logic AppliesIf { get; }
        public Logic StartIf { get; }
        public Logic DoneIf { get; }
        public bool Interrupt { get; }
        /// <summary>Tracks that stop while this interrupt is active.</summary>
        public IReadOnlyList<string> Suspends { get; }
        public IReadOnlyList<string> Refs { get; }
    }

    /// <summary>The 20 stages of one scenario, in map order.</summary>
    public sealed class FlowMap
    {
        public FlowMap(string scenario, IReadOnlyList<FlowStageDef> stages) { Scenario = scenario; Stages = stages; }

        public string Scenario { get; }
        public IReadOnlyList<FlowStageDef> Stages { get; }
    }

    /// <summary>The scenario master maps (content/flow/flow_maps.json), by scenario.</summary>
    public sealed class FlowMaps
    {
        public static readonly FlowMaps Empty = new FlowMaps(new List<FlowMap>());

        private readonly Dictionary<string, FlowMap> _by;

        public FlowMaps(IReadOnlyList<FlowMap> maps)
        {
            Maps = maps;
            _by = maps.ToDictionary(m => m.Scenario, StringComparer.Ordinal);
        }

        public IReadOnlyList<FlowMap> Maps { get; }

        /// <summary>The map for a scenario, or null when there is none.</summary>
        public FlowMap? For(string scenario) => _by.TryGetValue(scenario, out var m) ? m : null;

        private static readonly Regex StageId = new Regex("^S[0-9]\\.[0-9]{2}$", RegexOptions.CultureInvariant);
        private static readonly Regex TrackName = new Regex("^[a-z][a-z_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex Ref = new Regex("^SR-[0-9]{4}$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses {"maps":[{"scenario":"S1","stages":[...]}]}. Every problem is a ContentException naming the place: unknown fields, duplicate stages,
        /// a stage that waits on a stage that does not exist or on itself in a circle, a step outside 1 to 15, an interrupt that suspends nothing,
        /// a stage that is not in order, or any stage without a source-register id.
        /// </summary>
        public static FlowMaps Parse(string json, string source = "flow")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var raw = root.Arr("maps");
            var maps = new List<FlowMap>();
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("maps"), i));
                string scenario = o.Str("scenario");
                if (maps.Any(m => m.Scenario == scenario)) throw new ContentException(o.Sub("scenario") + ": duplicate scenario '" + scenario + "'");
                var stages = new List<FlowStageDef>();
                var rawStages = o.Arr("stages");
                for (int j = 0; j < rawStages.Count; j++)
                {
                    var so = new JsonObj(rawStages[j], JsonObj.Index(o.Sub("stages"), j));
                    stages.Add(Stage(so, scenario));
                    so.Finish();
                }
                o.Finish();
                Validate(stages, o.Sub("stages"));
                maps.Add(new FlowMap(scenario, stages));
            }
            root.Finish();
            return new FlowMaps(maps);
        }

        private static List<string> Strings(JsonObj o, string key, Regex? pattern = null, string expected = "text")
        {
            var raw = o.Arr(key);
            var list = new List<string>();
            for (int i = 0; i < raw.Count; i++)
                list.Add(raw[i] is string s && (pattern == null || pattern.IsMatch(s)) ? s : throw new ContentException(JsonObj.Index(o.Sub(key), i) + ": expected " + expected));
            return list;
        }

        private static FlowStageDef Stage(JsonObj o, string scenario)
        {
            string id = o.Str("id");
            if (!StageId.IsMatch(id) || !id.StartsWith(scenario + ".", StringComparison.Ordinal)) throw new ContentException(o.Sub("id") + ": '" + id + "' must look like " + scenario + ".07");
            int number = (int)o.Int("stage");
            if (number < 1 || number > 20 || id.Substring(scenario.Length + 1) != number.ToString("00", System.Globalization.CultureInfo.InvariantCulture))
                throw new ContentException(o.Sub("stage") + ": " + number + " does not match the id " + id + " (stages are numbered 1 to 20)");
            string title = o.Str("title");
            string track = o.Str("track");
            if (!TrackName.IsMatch(track)) throw new ContentException(o.Sub("track") + ": '" + track + "' must be lower-case letters");
            var steps = new List<int>();
            var rawSteps = o.Arr("steps");
            if (rawSteps.Count == 0) throw new ContentException(o.Sub("steps") + ": needs at least one step");
            for (int i = 0; i < rawSteps.Count; i++)
            {
                if (!(rawSteps[i] is long n) || n < 1 || n > 15) throw new ContentException(JsonObj.Index(o.Sub("steps"), i) + ": must be 1 to 15 (the 15 steps of the walkthrough)");
                steps.Add((int)n);
            }
            var after = Strings(o, "after", StageId, "a stage id like " + scenario + ".05");
            var applies = Logic.Compile(o.Any("applies_if"), o.Sub("applies_if"));
            var start = Logic.Compile(o.Any("start_if"), o.Sub("start_if"));
            var done = Logic.Compile(o.Any("done_if"), o.Sub("done_if"));
            bool interrupt = o.Bool("interrupt");
            var suspends = Strings(o, "suspends", TrackName, "a track name");
            if (interrupt && suspends.Count == 0) throw new ContentException(o.Sub("suspends") + ": an interrupt must stop at least one track");
            if (!interrupt && suspends.Count > 0) throw new ContentException(o.Sub("suspends") + ": only an interrupt stage suspends tracks");
            if (interrupt && after.Count > 0) throw new ContentException(o.Sub("after") + ": an interrupt starts on its own rule, not after other stages");
            var refs = Strings(o, "refs", Ref, "a register id like SR-1800");
            if (refs.Count == 0) throw new ContentException(o.Sub("refs") + ": needs at least one entry");
            return new FlowStageDef(id, number, title, track, steps, after, applies, start, done, interrupt, suspends, refs);
        }

        private static void Validate(IReadOnlyList<FlowStageDef> stages, string path)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in stages)
                if (!ids.Add(s.Id)) throw new ContentException(path + ": duplicate stage '" + s.Id + "'");
            for (int i = 1; i < stages.Count; i++)
                if (stages[i].Number <= stages[i - 1].Number) throw new ContentException(path + ": stage " + stages[i].Id + " is out of order");
            foreach (var s in stages)
                foreach (var a in s.After)
                {
                    if (!ids.Contains(a)) throw new ContentException(path + "." + s.Id + ".after: stage '" + a + "' does not exist");
                    if (stages.First(x => x.Id == a).Interrupt) throw new ContentException(path + "." + s.Id + ".after: '" + a + "' is an interrupt, which is never waited for");
                }
            // Stages only wait for earlier stages, so a circle is impossible once the order check above passes; that is also why it need not be searched for.
            foreach (var s in stages)
                foreach (var a in s.After)
                    if (stages.First(x => x.Id == a).Number >= s.Number) throw new ContentException(path + "." + s.Id + ".after: '" + a + "' comes later in the map (stages wait only for earlier stages)");
        }
    }
}
