using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Random;
using ProcSense.Core.Rules;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Events
{
    /// <summary>Published on the session's EventBus when an event fires.</summary>
    public sealed record EventFired(string Event, long Step);

    /// <summary>A fixed entry of the assessment schedule: fire this event at a sim time, or when the rule becomes true.</summary>
    public sealed record ScheduledEvent(string Event, double? AtSeconds, Logic? When);

    /// <summary>A cue the trainee can see while an event is active (what the monitor, the patient or the nurse shows).</summary>
    public sealed record ObservedCue(string Event, string Text);

    /// <summary>
    /// The Event Director (B-R14): fires approved events at the right moment and judges the trainee's response.
    /// Practice: a "caused" event fires when its trigger and eligibility rules are true, decided once with the seeded 'events' stream against base probability times the matching modifiers; a "random" event also waits for the warm-up and the global cooldown.
    /// Assessment: probabilities are ignored (a true trigger fires, a schedule fires at its time), so every run of the same case is the same. Only events approved for the mode are loaded. At most one clinical and one other event run at once.
    /// Each fired event opens its response windows; an accepted action with the expected verb and parameters inside its window counts, and a window that runs out is a missed response. Nothing here invents medicine: the events come from the library.
    /// </summary>
    public sealed class EventDirector : ISubsystem
    {
        private sealed class Active
        {
            public Active(EventDef def, long firedStep, long[] deadlines) { Def = def; FiredStep = firedStep; Deadlines = deadlines; Done = new bool[deadlines.Length]; }
            public EventDef Def { get; }
            public long FiredStep { get; }
            public long[] Deadlines { get; }
            public bool[] Done { get; }
            public bool Wrong { get; set; }
        }

        private readonly Content.ContentPackage _content;
        private readonly EventTables? _fixedTables;
        private readonly IReadOnlyList<ScheduledEvent> _schedule;
        private Session _s = null!;
        private EventTables _tables = EventTables.Empty;
        private IReadOnlyList<EventDef> _library = new List<EventDef>();
        private SimRandom _rng = null!;
        private readonly List<Active> _active = new List<Active>();
        private readonly HashSet<string> _decided = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _firedAt = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<ObservedCue> _cues = new List<ObservedCue>();
        private long _ticks;
        private long _lastRandomFire = long.MinValue;
        private bool _anyWatching;

        /// <param name="tables">Test hook: use this library instead of the content's.</param>
        /// <param name="schedule">The assessment schedule of the case (empty until the case library binds one).</param>
        public EventDirector(Content.ContentPackage content, EventTables? tables = null, IReadOnlyList<ScheduledEvent>? schedule = null)
        {
            _content = content; _fixedTables = tables; _schedule = schedule ?? new List<ScheduledEvent>();
        }

        public int Order => PipelineOrder.Events;

        /// <summary>What happened to each event: fired, handled, missed or answered wrongly, with the score items attached (for the scorer).</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        /// <summary>The events this session loaded (after the status gate for its mode).</summary>
        public IReadOnlyList<EventDef> Library => _library;

        public IReadOnlyList<ObservedCue> Cues => _cues;

        public bool IsActive(string eventId) => _active.Any(a => a.Def.Id == eventId);

        public bool HasFired(string eventId) => _firedAt.ContainsKey(eventId);

        public void Init(Session session)
        {
            _s = session;
            session.DirectorStep = this;
            _tables = _fixedTables ?? _content.Events;
            _library = _tables.For(session.Mode);
            if (session.Mode == SessionMode.Practice && session.PlannedEvents.Length > 0)                       // the case plans which events may fire (practice only); an id the library does not know is simply never met
            {
                var planned = new HashSet<string>(session.PlannedEvents.Split(','), StringComparer.Ordinal);
                _library = _library.Where(d => planned.Contains(d.Id)).ToList();
            }
            _rng = session.Random.Stream("events");
            _anyWatching = _library.Count > 0 || _schedule.Count > 0;
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (!o.Accepted) return;
            if (_active.Count > 0) Respond(a);
            if (_anyWatching) Evaluate();
        }

        public void OnTick()
        {
            if (!_anyWatching) return;
            _ticks++;
            if (_active.Count > 0) Expire();
            if (_ticks % _tables.EvaluateEverySteps == 0) Evaluate();
        }

        // ---------------------------------------------------------------- choosing what fires

        private void Evaluate()
        {
            Dictionary<string, object?>? state = null;
            for (int d = 0; d < _library.Count; d++)
            {
                var def = _library[d];
                if (_firedAt.ContainsKey(def.Id) || _decided.Contains(def.Id)) continue;
                if (def.NeedsFlag.Length > 0 && !_s.Patient.Clinical.HasFlag(def.NeedsFlag)) continue;
                if (!def.Watch.Holds(_s.Patient)) continue;
                if (!Fits(def)) continue;
                if (def.Kind == "random" && (_s.Clock.SimSeconds < _tables.WarmupSeconds || Cooling(def))) continue;
                state ??= State(def);
                if (!def.Trigger.Test(state) || !def.Eligibility.Test(state)) continue;
                _decided.Add(def.Id);                                                  // decided once: the draw is not repeated every ten seconds
                if (_s.Mode == SessionMode.Practice)
                {
                    double weight = def.BaseProbability;
                    foreach (var m in _tables.Modifiers) if (m.Event == def.Id && m.When.Test(state)) weight *= m.Multiply;
                    if (_rng.NextDouble() >= Math.Min(1.0, weight)) continue;
                }
                Fire(def);
                state = null;                                                          // firing changes the state
            }
            if (_schedule.Count > 0 && _s.Mode == SessionMode.Assessment) RunSchedule();
        }

        private void RunSchedule()
        {
            Dictionary<string, object?>? state = null;
            foreach (var item in _schedule)
            {
                if (_firedAt.ContainsKey(item.Event)) continue;
                var def = _library.FirstOrDefault(d => d.Id == item.Event);
                if (def == null || !Fits(def)) continue;
                bool due = item.AtSeconds.HasValue && _s.Clock.SimSeconds >= item.AtSeconds.Value;
                if (!due && item.When != null) { state ??= State(null); due = item.When.Test(state); }
                if (due) { _decided.Add(def.Id); Fire(def); state = null; }
            }
        }

        // concurrency: one clinical and one other event at a time
        private bool Fits(EventDef def) => _active.Count(a => a.Def.Category == def.Category) < (def.Category == "clinical" ? _tables.MaxClinical : _tables.MaxOther);

        private bool Cooling(EventDef def) =>
            (_lastRandomFire != long.MinValue && _s.Clock.Steps - _lastRandomFire < (long)Math.Round(_tables.GlobalCooldownSeconds / Time.SimClock.Step))
            || (_firedAt.TryGetValue(def.Id, out var at) && _s.Clock.Steps - at < (long)Math.Round(def.CooldownSeconds / Time.SimClock.Step));

        // keys a rule may read from the light state; any other key needs the full rule state (the whole case graph facts)
        private static readonly string[] LightKeys = { "vitals", "flags", "sim_seconds", "mode", "case", "scenario", "events_fired", "events_active" };

        private static bool Light(Logic rule) => rule.ReadKeys != null && rule.ReadKeys.All(k => Array.IndexOf(LightKeys, k) >= 0);

        private Dictionary<string, object?> State(EventDef? def)
        {
            bool light = def != null && Light(def.Trigger) && Light(def.Eligibility);
            Dictionary<string, object?> state;
            if (light)
            {
                var flags = new List<object?>();
                foreach (var f in _s.Patient.Clinical.Flags) flags.Add(f);
                state = new Dictionary<string, object?>(StringComparer.Ordinal) { ["flags"] = flags, ["sim_seconds"] = _s.Clock.SimSeconds, ["mode"] = _s.Mode.ToString().ToLowerInvariant(), ["case"] = _s.Case.Id, ["scenario"] = _s.Case.Scenario };
            }
            else state = RuleState.Build(_s);
            var fired = new List<object?>();
            foreach (var k in _firedAt.Keys) fired.Add(k);
            var v = _s.Patient.Physiological.Vitals;                       // the patient's true numbers, not what the trainee has been shown
            state["vitals"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["hr"] = v.HeartRate, ["sbp"] = v.SystolicBp, ["dbp"] = v.DiastolicBp, ["rr"] = v.RespiratoryRate, ["spo2"] = v.Spo2, ["temp"] = v.TemperatureC, ["pain"] = v.PainScore };
            state["events_fired"] = fired;
            var active = new List<object?>();
            foreach (var a in _active) active.Add(a.Def.Id);
            state["events_active"] = active;
            return state;
        }

        // ---------------------------------------------------------------- firing and resolving

        /// <summary>Fires an event now (used by the schedule and by tests that force an event, plan test T-E1).</summary>
        public void Fire(EventDef def)
        {
            long step = _s.Clock.Steps;
            _firedAt[def.Id] = step;
            if (def.Kind == "random") _lastRandomFire = step;
            foreach (var id in def.PhysioEffects) ApplyEffect(id);
            foreach (var f in def.SetFlags) _s.Patient.Clinical.SetFlag(f);
            foreach (var cue in def.Cues) _cues.Add(new ObservedCue(def.Id, cue));
            var deadlines = def.Expected.Select(r => step + (long)Math.Round(r.WithinSeconds / Time.SimClock.Step)).ToArray();
            _active.Add(new Active(def, step, deadlines));
            _history.Add(new GraphEvent("event_fired", def.Id, step, Array.Empty<string>()));
            _s.Events.Publish(new EventFired(def.Id, step));
            if (def.Expected.Count == 0) Close(_active[_active.Count - 1], def.Handled, "event_handled");
        }

        private void ApplyEffect(string id)
        {
            var effect = _content.Physio.FindEffect(id);
            if (effect != null && effect.Targets.Count > 0) _s.Physiology?.Apply(effect);
        }

        private void Respond(SimAction a)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var act = _active[i];
                for (int r = 0; r < act.Def.Expected.Count; r++)
                    if (!act.Done[r] && _s.Clock.Steps <= act.Deadlines[r] && Matches(act.Def.Expected[r], a)) act.Done[r] = true;
                if (!act.Wrong && act.Def.Unacceptable.Any(u => Matches(u, a))) { act.Wrong = true; Apply(act.Def.Wrong, act.Def.Id, "event_wrong"); }
                if (act.Done.All(d => d)) Close(act, act.Def.Handled, "event_handled");
            }
        }

        private static bool Matches(ExpectedResponse r, SimAction a)
        {
            if (r.Verb != a.Verb) return false;
            foreach (var kv in r.Params) if (!a.Params.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
            return true;
        }

        private void Expire()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var act = _active[i];
                bool late = false;
                for (int r = 0; r < act.Done.Length; r++) if (!act.Done[r] && _s.Clock.Steps > act.Deadlines[r]) late = true;
                if (late) Close(act, act.Def.Missed, "event_missed");
            }
        }

        private void Close(Active act, EventConsequence consequence, string kind)
        {
            _active.Remove(act);
            _cues.RemoveAll(c => c.Event == act.Def.Id);
            Apply(consequence, act.Def.Id, kind);
        }

        private void Apply(EventConsequence c, string id, string kind)
        {
            if (c.Flag.Length > 0) _s.Patient.Clinical.SetFlag(c.Flag);
            foreach (var e in c.PhysioEffects) ApplyEffect(e);
            _history.Add(new GraphEvent(kind, id, _s.Clock.Steps, c.Items));
        }
    }
}
