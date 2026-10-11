using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>The tier the patient reached and when (sim seconds), with the NEWS2 it came from.</summary>
    public sealed record SepsisTierChange(double Seconds, string Tier, int News2);

    /// <summary>
    /// One timed treatment: the tier that set the tightest deadline, when the tier was first met, the deadline, when the treatment was given (null: not yet), and how late it was.
    /// LateMinutes is time after the deadline (0 when on time); SinceFirstMetMinutes is the time from the first moderate-or-higher tier to the treatment (or to now), which is the number the walkthrough's debrief line quotes.
    /// </summary>
    public sealed record SepsisTimerResult(string Kind, string Tier, double StartedSeconds, double DeadlineSeconds, double? GivenSeconds, double LateMinutes, double SinceFirstMetMinutes);

    public sealed record SepsisReport(bool Applies, string Tier, double? FirstMetSeconds, bool Recognised, IReadOnlyList<SepsisTierChange> Tiers, IReadOnlyList<SepsisTimerResult> Timers);

    /// <summary>
    /// Works out the NICE NG253 sepsis tier from each NEWS2 the trainee gets, for the cases where an infection is the working diagnosis, and keeps the timers that follow (antibiotics, fluids) against the sim clock.
    /// The antibiotic and fluid times come from the medicine workflow (TreatmentGiven). The trainee can say the concern out loud (say_concern sepsis), which is a recognition item. Nothing here is shown to the trainee:
    /// the tier is the engine's truth for the debrief; what the trainee sees is the NEWS2 chart.
    /// </summary>
    public sealed class SepsisSubsystem : ISubsystem
    {
        public const string SayConcernVerb = "say_concern";
        public const string MissingTopic = "MISSING_TOPIC";
        public const string UnknownTopic = "UNKNOWN_TOPIC";
        public const string RecognisedFlag = "sepsis.recognised";
        public const string ConcernFlag = "sepsis.clinical_concern";
        public static readonly string[] Topics = { "sepsis", "infection", "deterioration", "pain", "other" };

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<SepsisTierChange> _tiers = new List<SepsisTierChange>();
        private readonly Dictionary<string, double> _firstGiven = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<string> _overdue = new HashSet<string>(StringComparer.Ordinal);
        private CaseFile? _file;
        private Session _s = null!;
        private int _seen;
        private bool _infection;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public SepsisSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        // after the NEWS2 step, so the score it has just worked out is there to read
        public int Order => PipelineOrder.Clinical + 4;

        /// <summary>Tier changes, recognition and lateness with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private SepsisTables Tables => _content.Sepsis;

        public void Init(Session session)
        {
            _s = session;
            session.SepsisStep = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
            _infection = Tables.IsInfectionCase(_file?.Id ?? session.Case.Id);
            session.Verbs.Register(SayConcernVerb);
            session.Events.Subscribe<TreatmentGiven>(OnGiven);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (a.Verb == SayConcernVerb && o.Accepted) SayConcern(a, o);
            if (o.Accepted) Refresh();
        }

        public void OnTick()
        {
            if (_tiers.Count == 0) return;
            foreach (var t in Timers())
            {
                if (t.GivenSeconds.HasValue || t.DeadlineSeconds > _s.Clock.SimSeconds || !_overdue.Add(t.Kind)) continue;
                _history.Add(new GraphEvent("sepsis_" + t.Kind + "_overdue", t.Tier, _s.Clock.Steps, Array.Empty<string>()));
            }
        }

        // ---------------------------------------------------------------- the tier

        private void Refresh()
        {
            var records = _s.Patient.Clinical.News2;
            while (_seen < records.Count)
            {
                var r = records[_seen++];
                if (r.Tier == "not_used") continue;
                var a = Tables.Assess(_infection, r.Total, r.Single3, _s.Patient.Clinical.HasFlag(ConcernFlag));
                if (a.Tier == SepsisTables.None) continue;
                if (_tiers.Count > 0 && _tiers[_tiers.Count - 1].Tier == a.Tier) continue;
                _tiers.Add(new SepsisTierChange(r.Step * ProcSense.Core.Time.SimClock.Step, a.Tier, r.Total));
                _history.Add(new GraphEvent("sepsis_tier:" + a.Tier, a.Tier, r.Step, Array.Empty<string>()));
            }
        }

        private void SayConcern(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("topic", out var topic) || topic.Length == 0) { o.Reject(MissingTopic); return; }
            if (!Topics.Contains(topic)) { o.Reject(UnknownTopic); return; }
            o.Effects.Add("concern:" + topic);
            if (topic != "sepsis") return;
            bool met = _tiers.Any(t => Rank(t.Tier) >= Rank("moderate"));
            if (!met) { o.Effects.Add("concern_before_criteria"); return; }     // said before the criteria were met: noted, not credited
            if (_s.Patient.Clinical.HasFlag(RecognisedFlag)) return;
            _s.Patient.Clinical.SetFlag(RecognisedFlag);
            o.Effects.Add("sepsis_recognised");
            _history.Add(new GraphEvent("sepsis_recognised", "sepsis", _s.Clock.Steps, new[] { "credit.sepsis_recognised" }));
        }

        private int Rank(string tier) => Tables.Find(tier)?.Rank ?? -1;

        // ---------------------------------------------------------------- the timers

        private void OnGiven(TreatmentGiven e)
        {
            double now = _s.Clock.SimSeconds;
            foreach (var kind in new[] { "antibiotics", "fluids" })
            {
                string effect = kind == "antibiotics" ? "EFF-ANTIBIOTIC" : "EFF-FLUID-BOLUS";
                if (!e.Effects.Contains(effect) || _firstGiven.ContainsKey(kind)) continue;
                _firstGiven[kind] = now;
                var timer = Timers().FirstOrDefault(t => t.Kind == kind);
                if (timer == null) continue;                                    // no tier asks for it (yet): nothing to judge
                var items = timer.LateMinutes > 0 ? new[] { "major.sepsis_" + kind + "_late" } : new[] { "credit.sepsis_" + kind + "_on_time" };
                _history.Add(new GraphEvent("sepsis_" + kind + (timer.LateMinutes > 0 ? "_late" : "_on_time"), timer.Tier, _s.Clock.Steps, items));
            }
        }

        private IEnumerable<(string Kind, Func<SepsisTierDef, double?> Hours)> Kinds() => new (string, Func<SepsisTierDef, double?>)[]
        {
            ("antibiotics", d => d.AntibioticsHours), ("fluids", d => d.FluidsHours),
        };

        /// <summary>For each timed treatment, the tightest deadline among the tiers reached so far; the tier's clock starts when it, or a higher tier, was first met.</summary>
        private List<SepsisTimerResult> Timers()
        {
            var result = new List<SepsisTimerResult>();
            if (_tiers.Count == 0) return result;
            double first = _tiers.Where(t => Rank(t.Tier) >= Rank("moderate")).Select(t => t.Seconds).DefaultIfEmpty(double.NaN).First();
            foreach (var (kind, hours) in Kinds())
            {
                string bestTier = ""; double bestStart = 0, bestDeadline = double.MaxValue;
                foreach (var def in Tables.Tiers)
                {
                    double? h = hours(def);
                    if (!h.HasValue) continue;
                    var reached = _tiers.Where(t => Rank(t.Tier) >= def.Rank).Select(t => t.Seconds).DefaultIfEmpty(double.NaN).First();
                    if (double.IsNaN(reached)) continue;
                    double deadline = reached + h.Value * 3600;
                    if (deadline < bestDeadline) { bestDeadline = deadline; bestStart = reached; bestTier = def.Id; }
                }
                if (bestTier.Length == 0) continue;
                double? given = _firstGiven.TryGetValue(kind, out var g) ? g : (double?)null;
                double now = _s.Clock.SimSeconds;
                double late = Math.Max(0, ((given ?? now) - bestDeadline) / 60);
                double since = double.IsNaN(first) ? 0 : Math.Max(0, ((given ?? now) - first) / 60);
                result.Add(new SepsisTimerResult(kind, bestTier, bestStart, bestDeadline, given, Math.Round(late, 1), Math.Round(since, 1)));
            }
            return result;
        }

        /// <summary>Where things stand now: the tier, when the criteria were first met, the timers and how late they are.</summary>
        public SepsisReport Report()
        {
            string tier = _tiers.Count == 0 ? SepsisTables.None : _tiers[_tiers.Count - 1].Tier;
            double? first = _tiers.Where(t => Rank(t.Tier) >= Rank("moderate")).Select(t => (double?)t.Seconds).FirstOrDefault();
            return new SepsisReport(_infection, tier, first, _s.Patient.Clinical.HasFlag(RecognisedFlag), _tiers.ToList(), Timers());
        }
    }
}
