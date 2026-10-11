using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Behaviour
{
    /// <summary>
    /// The patient's behaviour for the session. The communication acts of the trainee (and events, and waiting) move the attributes by the transition table; the mode (CALM, ANXIOUS, DISTRESSED, IN_PAIN, CONFUSED, DROWSY,
    /// NAUSEATED, UNCOOPERATIVE) is worked out from them with hysteresis and is the only thing the trainee sees. Whether the patient can consent is the engine's own judgement (capacity is lost while confusion, severe
    /// pain, a strong painkiller, sepsis, low oxygen, low sugar or low sodium is active) and is never shown. Other steps add to the patient's state through <see cref="Trigger"/>; sensitive facts are told only when
    /// trust and willingness reach the content's limits (<see cref="MayDisclose"/>).
    /// </summary>
    public sealed class BehaviourSubsystem : ISubsystem
    {
        private const long UpdateEverySteps = 60;      // 30 sim seconds

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<string> _causes = new List<string>();
        private TransitionEngine _engine = null!;
        private ModeResolver _resolver = null!;
        private CaseFile? _file;
        private Session _s = null!;
        private double _sodium = double.NaN;
        private long _lastTalk;
        private long _opioidAt = -1;
        private double _sigPain = double.NaN;
        private bool _sigFlag, _sigStrong;
        private string _sigTier = "";
        private bool _seen;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public BehaviourSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        public int Order => PipelineOrder.Clinical + 6;

        public BehaviouralState State { get; private set; } = null!;

        /// <summary>CALM, ANXIOUS, DISTRESSED, IN_PAIN, CONFUSED, DROWSY, NAUSEATED or UNCOOPERATIVE.</summary>
        public string Mode { get; private set; } = "CALM";

        /// <summary>What is taking capacity away right now (empty: the patient has capacity). The consent step reads this; the trainee never sees it.</summary>
        public IReadOnlyList<string> CapacityCauses => _causes;

        public bool HasCapacity => _causes.Count == 0;

        /// <summary>Every transition that fired and every change of mode, with sim steps. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private BehaviourTables Tables => _content.Behaviour;

        public void Init(Session session)
        {
            _s = session;
            session.BehaviourStep = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
            State = new BehaviouralState(Tables);
            _engine = new TransitionEngine(Tables);
            _resolver = new ModeResolver(Tables);
            Mode = Tables.DefaultMode;
            var sodium = _file?.Results.FirstOrDefault(r => r.Test.StartsWith("Sodium", StringComparison.Ordinal) && !r.Test.Contains("repeat"));
            if (sodium != null && double.TryParse(sodium.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double na)) _sodium = na;
            session.Events.Subscribe<TreatmentGiven>(e => { if (e.Treatment == "TR-OPIOID") _opioidAt = _s.Clock.Steps; });
            Recompute();
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (!o.Accepted) return;
            switch (a.Verb)
            {
                case "introduce_self": Trigger("introduce_self"); break;
                case "summarise": Trigger("summarise"); break;
                case "ask": _lastTalk = _s.Clock.Steps; break;
            }
        }

        public void OnTick()
        {
            long steps = _s.Clock.Steps;
            if (steps % UpdateEverySteps != 0) return;
            if (steps - _lastTalk > Tables.WaitMinutes * 60 / Time.SimClock.Step)           // a wait of MORE than the interval
            {
                _lastTalk = steps;                                                           // the next interval counts from now
                Trigger("long_wait", marksTalk: false);
            }
            if (Changed()) Recompute();
        }

        /// <summary>Something the trainee did or said (or an event) happens to the patient. Returns the rules that changed the patient and sets the question the patient asks, if any.</summary>
        public IReadOnlyList<string> Trigger(string trigger, bool marksTalk = true) => Trigger(trigger, out _, marksTalk);

        public IReadOnlyList<string> Trigger(string trigger, out string question, bool marksTalk = true)
        {
            var fired = _engine.Apply(trigger, State, out question);
            if (marksTalk && trigger != "approved_event") _lastTalk = _s.Clock.Steps;
            if (fired.Count > 0) _history.Add(new GraphEvent("behaviour:" + trigger, string.Join(",", fired), _s.Clock.Steps, Array.Empty<string>()));
            Recompute();
            return fired;
        }

        /// <summary>True when this sensitive topic may be told: trust and willingness have reached the content's limits (topics with no gate are always told).</summary>
        public bool MayDisclose(string topic)
        {
            var gate = Tables.Gates.FirstOrDefault(g => g.Topic == topic);
            return gate == null || (State.Get("trust") >= gate.Trust && State.Get("willingness") >= gate.Willingness);
        }

        // cheap to ask every half minute: only a few numbers are compared
        private bool Changed()
        {
            var v = _s.Patient.Physiological.Vitals;
            bool flag = _s.Patient.Clinical.HasFlag(Physiology.PhysiologySubsystem.FlagPrefix + "confusion");
            bool strong = _opioidAt >= 0 && _s.Clock.Steps - _opioidAt < Tables.Capacity.StrongPainkillerMinutes * 60 / Time.SimClock.Step;
            string tier = _s.SepsisStep?.Tier ?? "";
            return !_seen || v.PainScore != _sigPain || flag != _sigFlag || strong != _sigStrong || tier != _sigTier;
        }

        private void Recompute()
        {
            var v = _s.Patient.Physiological.Vitals;
            bool flag = _s.Patient.Clinical.HasFlag(Physiology.PhysiologySubsystem.FlagPrefix + "confusion");
            bool strong = _opioidAt >= 0 && _s.Clock.Steps - _opioidAt < Tables.Capacity.StrongPainkillerMinutes * 60 / Time.SimClock.Step;
            string tier = _s.SepsisStep?.Tier ?? "";
            _seen = true; _sigPain = v.PainScore; _sigFlag = flag; _sigStrong = strong; _sigTier = tier;

            State.Set("pain", v.PainScore);
            var values = new Dictionary<string, double>(State.Values, StringComparer.Ordinal);
            if (flag) values["confusion"] = Math.Min(3, values["confusion"] + 1);                      // the physiology adds to the confusion the conversation caused
            var phys = _s.Physiology?.Engine.Read().Values;
            values["sedation"] = phys != null && phys.TryGetValue("sedation", out var sed) ? sed : 0;
            values["nausea"] = phys != null && phys.TryGetValue("nausea", out var nau) ? nau : 0;

            string mode = _resolver.Resolve(values, Mode);
            if (mode != Mode) { _history.Add(new GraphEvent("mode:" + mode, Mode, _s.Clock.Steps, Array.Empty<string>())); Mode = mode; }

            _causes.Clear();
            var cap = Tables.Capacity;
            if (values["confusion"] >= cap.ConfusionAt) _causes.Add("confusion");
            if (values["pain"] >= cap.SeverePainAt) _causes.Add("severe_pain");
            if (strong) _causes.Add("strong_painkiller");
            if (!double.IsNaN(_sodium) && _sodium < cap.SodiumBelow) _causes.Add("low_sodium");
            if (cap.SepsisTiers.Contains(tier)) _causes.Add("sepsis");
        }
    }
}
