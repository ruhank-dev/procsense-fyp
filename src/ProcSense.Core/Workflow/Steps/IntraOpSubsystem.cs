using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>One answer the trainee can give to an intra-operative event, and whether it is the kind of teamwork the event asks for.</summary>
    public sealed record IntraOpResponse(string Id, string Label, bool Correct);

    /// <summary>One intra-operative training event (content/extension/intra_op_events.json): what is said in theatre, which scenarios it applies to, how long the trainee has and what they can do.</summary>
    public sealed record IntraOpEvent(string Id, string Title, string Label, IReadOnlyList<string> AppliesTo, string Cue, int DeadlineSteps, string Approval, bool PracticeOnly, IReadOnlyList<IntraOpResponse> Responses, string Ref);

    /// <summary>The events and the score items they earn.</summary>
    public sealed class IntraOpTables
    {
        public static readonly IntraOpTables Empty = new IntraOpTables("", new List<IntraOpEvent>(), new Dictionary<string, string>());

        public IntraOpTables(string label, IReadOnlyList<IntraOpEvent> events, IReadOnlyDictionary<string, string> scored) { Label = label; Events = events; Scored = scored; }

        public string Label { get; }
        public IReadOnlyList<IntraOpEvent> Events { get; }
        public IReadOnlyDictionary<string, string> Scored { get; }

        public static IntraOpTables Parse(string json, string source = "extension/intra_op_events")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            string label = root.Str("label");
            var events = new List<IntraOpEvent>();
            var raw = root.Arr("events");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("events"), i));
                string id = o.Str("id");
                if (events.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate event '" + id + "'");
                var applies = new List<string>();
                foreach (var a in o.Arr("applies_to"))
                {
                    string sc = a as string ?? throw new ContentException(o.Sub("applies_to") + ": expected text");
                    if (sc != "S1" && sc != "S2") throw new ContentException(o.Sub("applies_to") + ": '" + sc + "' is not a scenario");
                    applies.Add(sc);
                }
                string eventLabel = o.Str("label");
                if (eventLabel != label) throw new ContentException(o.Sub("label") + ": every event carries the visible label '" + label + "'");
                var responses = new List<IntraOpResponse>();
                var rr = o.Arr("responses");
                for (int j = 0; j < rr.Count; j++)
                {
                    var r = new JsonObj(rr[j], JsonObj.Index(o.Sub("responses"), j));
                    string rid = r.Str("id");
                    if (responses.Any(x => x.Id == rid)) throw new ContentException(r.Sub("id") + ": duplicate response '" + rid + "'");
                    responses.Add(new IntraOpResponse(rid, r.Str("label"), r.Bool("correct")));
                    r.Finish();
                }
                if (!responses.Any(x => x.Correct) || responses.All(x => x.Correct)) throw new ContentException(o.Sub("responses") + ": needs a correct and a wrong response");
                events.Add(new IntraOpEvent(id, o.Str("title"), eventLabel, applies, o.Str("cue"), (int)o.Int("deadline_steps"), o.Str("approval"), o.Bool("practice_only"), responses, o.Str("ref")));
                o.Finish();
            }
            var scored = new SortedDictionary<string, string>(root.StringMap("scored"), StringComparer.Ordinal);
            foreach (var need in new[] { "respond", "wrong", "freeze" })
                if (!scored.ContainsKey(need)) throw new ContentException(root.Sub("scored") + ": '" + need + "' is missing");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new IntraOpTables(label, events, scored);
        }
    }

    /// <summary>
    /// The intra-operative training events of the ProcSense educational extension (p1-theatre-extension-s02, walkthrough C.22). After the Time Out the operation is abstracted as a time-lapse; begin_intraop
    /// plays it and one event interrupts: a cue the theatre says aloud and a number of simulation steps to answer. The trainee answers with intraop_respond (an answer from the event's list): a teamwork answer
    /// (relaying, fetching, asking) is credited, a wrong one (carrying on) is a minor item, and letting the time run out is a major item (freezing: no communication). What the surgeon decides is never scored. The
    /// event is chosen from the ones that apply to the scenario by the session seed, so a replay is identical. Every event is labelled a ProcSense educational extension, awaits a clinician's review, and runs
    /// in practice mode only, never in assessment. Nothing here changes the patient: the operation is not simulated.
    /// </summary>
    public sealed class IntraOpSubsystem : ISubsystem
    {
        public const string BeginVerb = "intraop_begin";
        public const string RespondVerb = "intraop_respond";

        public const string TimeOutNotDone = "TIME_OUT_NOT_DONE";
        public const string AlreadyPlayed = "ALREADY_PLAYED";
        public const string NotInAssessment = "NOT_IN_ASSESSMENT";
        public const string NoEventForThisCase = "NO_EVENT_FOR_THIS_CASE";
        public const string NoEventPending = "NO_EVENT_PENDING";
        public const string MissingResponse = "MISSING_RESPONSE";
        public const string UnknownResponse = "UNKNOWN_RESPONSE";

        private readonly ContentPackage _content;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private IntraOpEvent? _event;
        private long _startedAt;
        private bool _played, _resolved;
        private Session _s = null!;

        public IntraOpSubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Investigations + 9;

        public IReadOnlyList<GraphEvent> History => _history;

        /// <summary>The event that has been played this session (null before intraop_begin).</summary>
        public IntraOpEvent? Event => _event;

        /// <summary>True while the cue is on show and the trainee has not answered or run out of time.</summary>
        public bool Pending => _event != null && !_resolved;

        /// <summary>The visible label every event carries.</summary>
        public string Label => Tables.Label;

        private IntraOpTables Tables => _content.IntraOp;

        public void Init(Session session)
        {
            _s = session;
            session.IntraOpStep = this;
            if (Tables.Events.Count == 0) return;                          // a package without the extension has no intra-operative events
            session.Verbs.Register(BeginVerb, requiresIdentity: true);
            session.Verbs.Register(RespondVerb, requiresIdentity: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case BeginVerb: Begin(o); break;
                case RespondVerb: Respond(a, o); break;
            }
        }

        public void OnTick()
        {
            if (!Pending) return;
            if (_s.Clock.Steps - _startedAt >= _event!.DeadlineSteps)
            {
                _resolved = true;                                           // the time-lapse moves on without an answer: freezing is a debrief item
                _history.Add(new GraphEvent("intraop_freeze", _event.Id, _s.Clock.Steps, new[] { Tables.Scored["freeze"] }));
            }
        }

        private void Begin(ActionOutcome o)
        {
            if (_s.Safety == null || _s.Safety.PhaseState("time_out") != "COMPLETE") { o.Reject(TimeOutNotDone); return; }
            if (_played) { o.Reject(AlreadyPlayed); return; }
            if (_s.Mode == SessionMode.Assessment && Tables.Events.Any(e => e.PracticeOnly)) { o.Reject(NotInAssessment); return; }
            var candidates = Tables.Events.Where(e => e.AppliesTo.Contains(_s.Case.Scenario)).ToList();
            if (candidates.Count == 0) { o.Reject(NoEventForThisCase); return; }
            _event = candidates[(int)(_s.Seed % (ulong)candidates.Count)];
            _played = true;
            _startedAt = _s.Clock.Steps;
            o.Effects.Add("time_lapse");
            o.Effects.Add("intraop_event:" + _event.Id);
            o.Effects.Add("label:" + _event.Label);
            _history.Add(new GraphEvent("intraop_event", _event.Id, _s.Clock.Steps, Array.Empty<string>()));
        }

        private void Respond(SimAction a, ActionOutcome o)
        {
            if (!Pending) { o.Reject(NoEventPending); return; }
            if (!a.Params.TryGetValue("response", out var id) || id.Length == 0) { o.Reject(MissingResponse); return; }
            var r = _event!.Responses.FirstOrDefault(x => x.Id == id);
            if (r == null) { o.Reject(UnknownResponse); return; }
            _resolved = true;
            o.Effects.Add((r.Correct ? "intraop_answered:" : "intraop_wrong:") + r.Id);
            _history.Add(new GraphEvent(r.Correct ? "intraop_response" : "intraop_wrong_response", r.Id, _s.Clock.Steps, new[] { Tables.Scored[r.Correct ? "respond" : "wrong"] }));
        }
    }
}
