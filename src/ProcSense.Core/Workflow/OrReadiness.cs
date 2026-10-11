using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow
{
    /// <summary>One theatre station: what it is, what is on it, who checks it (the trainee, or the theatre staff whose word the trainee takes), whether a fault there is seriously concerning and who to call about a fault.</summary>
    public sealed record OrStation(string Id, int Order, string Label, IReadOnlyList<string> Items, string Owner, bool Critical, string Call, bool Required, string Ref);

    /// <summary>A fault in a case's theatre from the start (the case's own story).</summary>
    public sealed record OrFault(string Station, string What);

    /// <summary>The theatre stations, the cases with a fault, the replacement delay and the score items (content/or/or_requirements.json, built from the walkthrough's Step 11 list).</summary>
    public sealed class OrTables
    {
        public static readonly OrTables Empty = new OrTables(new List<OrStation>(), new Dictionary<string, OrFault>(), 15, new List<string>(), new Dictionary<string, string>(), "or.ready", "E-OR-FAULT");

        public OrTables(IReadOnlyList<OrStation> stations, IReadOnlyDictionary<string, OrFault> faults, int replacementDelayMinutes, IReadOnlyList<string> substituteIn, IReadOnlyDictionary<string, string> items, string readyFlag, string faultEvent)
        {
            Stations = stations; Faults = faults; ReplacementDelayMinutes = replacementDelayMinutes; SubstituteIn = substituteIn; Items = items; ReadyFlag = readyFlag; FaultEvent = faultEvent;
        }

        public IReadOnlyList<OrStation> Stations { get; }
        public IReadOnlyDictionary<string, OrFault> Faults { get; }
        public int ReplacementDelayMinutes { get; }
        public IReadOnlyList<string> SubstituteIn { get; }
        public IReadOnlyDictionary<string, string> Items { get; }
        public string ReadyFlag { get; }
        public string FaultEvent { get; }

        public OrStation? Find(string id) => Stations.FirstOrDefault(s => s.Id == id);

        public static OrTables Parse(string json, string source = "or")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var stations = new List<OrStation>();
            var raw = root.Arr("stations");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("stations"), i));
                string id = o.Str("id");
                if (stations.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate station '" + id + "'");
                var items = new List<string>();
                foreach (var x in o.Arr("items")) items.Add(x as string ?? throw new ContentException(o.Sub("items") + ": expected text"));
                stations.Add(new OrStation(id, (int)o.Int("order"), o.Str("label"), items, o.Enum("owner", "trainee", "npc_verified"), o.Bool("critical"), o.Enum("call", "senior", "anaesthetist"), o.Bool("required"), o.Str("ref")));
                o.Finish();
            }
            var faults = new SortedDictionary<string, OrFault>(StringComparer.Ordinal);
            var rawFaults = root.Any("faults") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("faults") + ": expected an object");
            foreach (var kv in rawFaults)
            {
                var o = new JsonObj(kv.Value, root.Sub("faults") + "." + kv.Key);
                string station = o.Str("station");
                if (stations.All(x => x.Id != station)) throw new ContentException(o.Sub("station") + ": '" + station + "' is not a station");
                faults[kv.Key] = new OrFault(station, o.Str("what"));
                o.Finish();
            }
            int delay = (int)root.Int("replacement_delay_minutes");
            if (delay <= 0) throw new ContentException(root.Sub("replacement_delay_minutes") + ": must be positive");
            var substitute = new List<string>();
            foreach (var s in root.Arr("substitute_theatre_in")) substitute.Add(s as string ?? throw new ContentException(root.Sub("substitute_theatre_in") + ": expected text"));
            var items2 = new SortedDictionary<string, string>(root.StringMap("items"), StringComparer.Ordinal);
            foreach (var need in new[] { "ready", "declared_not_ready", "fault_ignored", "fault_reported", "critical_unchecked", "substitute" })
                if (!items2.ContainsKey(need)) throw new ContentException(root.Sub("items") + ": '" + need + "' is missing");
            string readyFlag = root.Str("ready_flag"), faultEvent = root.Str("fault_event");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new OrTables(stations.OrderBy(x => x.Order).ToList(), faults, delay, substitute, items2, readyFlag, faultEvent);
        }
    }

    /// <summary>
    /// Step 11: is the theatre ready for this patient? Each station is unchecked, ready, in fault or being replaced. The trainee checks a station (a hidden fault shows then), reports a fault (a replacement
    /// arrives after the content's delay) and declares the theatre ready; declaring it ready when it is not is a major item and Time Out stays blocked (readiness blocker B-OR, flag or.ready). A fault can also come from
    /// the case's story or from an event (WorkflowEvent E-OR-FAULT), and an emergency case may use a substitute theatre. Stations are plain verbs so headless tests work; the VR interactions will send the same ones.
    /// </summary>
    public sealed class OrReadinessSubsystem : ISubsystem
    {
        public const string CheckVerb = "check_station";
        public const string ReportVerb = "report_fault";
        public const string DeclareVerb = "declare_or_ready";
        public const string SubstituteVerb = "use_substitute_theatre";

        public const string MissingStation = "MISSING_STATION";
        public const string UnknownStation = "UNKNOWN_STATION";
        public const string NotAllowed = "NOT_ALLOWED";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<string> _hiddenFaults = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _replaceAt = new Dictionary<string, long>(StringComparer.Ordinal);
        private Session _s = null!;
        private bool _started;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public OrReadinessSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        public int Order => PipelineOrder.Investigations + 6;

        /// <summary>Checks, faults, reports and declarations, with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private OrTables Tables => _content.Or;

        private SortedDictionary<string, string> States => _s.Patient.Clinical.OrStations;

        /// <summary>True once the trainee has declared the theatre ready and nothing has failed since.</summary>
        public bool Ready => _s.Patient.Clinical.HasFlag(Tables.ReadyFlag);

        public string StateOf(string station) => States.TryGetValue(station, out var s) ? s : "unchecked";

        public void Init(Session session)
        {
            _s = session;
            session.OrStep = this;
            foreach (var verb in new[] { CheckVerb, ReportVerb, DeclareVerb, SubstituteVerb }) session.Verbs.Register(verb);
            string id = (_fixed?.Id) ?? session.Case.Id;
            if (Tables.Faults.TryGetValue(id, out var fault)) _hiddenFaults.Add(fault.Station);
            session.Events.Subscribe<WorkflowEvent>(OnEvent);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case CheckVerb: Check(a, o); break;
                case ReportVerb: Report(a, o); break;
                case DeclareVerb: Declare(o); break;
                case SubstituteVerb: Substitute(o); break;
            }
        }

        public void OnTick()
        {
            if (_replaceAt.Count == 0) return;
            foreach (var kv in _replaceAt.ToList())
            {
                if (_s.Clock.Steps < kv.Value) continue;
                _replaceAt.Remove(kv.Key);
                States[kv.Key] = "ready";
                _history.Add(new GraphEvent("replaced", kv.Key, _s.Clock.Steps, Array.Empty<string>()));
            }
        }

        // The stations the theatre staff verify start ready (or in fault when the case's theatre has one); the trainee's start unchecked. Done on first use so a session that never reaches theatre is unchanged.
        private void Start()
        {
            if (_started) return;
            _started = true;
            foreach (var st in Tables.Stations)
                States[st.Id] = st.Owner == "npc_verified" ? (_hiddenFaults.Contains(st.Id) ? "fault" : "ready") : "unchecked";
            foreach (var id in _hiddenFaults.Where(x => States[x] == "fault").ToList()) _hiddenFaults.Remove(id);
        }

        private bool Station(SimAction a, ActionOutcome o, out OrStation st)
        {
            st = null!;
            if (!a.Params.TryGetValue("station", out var id) || id.Length == 0) { o.Reject(MissingStation); return false; }
            var found = Tables.Find(id);
            if (found == null) { o.Reject(UnknownStation); return false; }
            st = found;
            return true;
        }

        // L11-01: the light goes green or red
        private void Check(SimAction a, ActionOutcome o)
        {
            if (!Station(a, o, out var st)) return;
            Start();
            string state = StateOf(st.Id);
            if (state == "unchecked")
            {
                bool broken = _hiddenFaults.Remove(st.Id);
                States[st.Id] = broken ? "fault" : "ready";
                state = States[st.Id];
                _history.Add(new GraphEvent(broken ? "fault_found" : "station_ready", st.Id, _s.Clock.Steps, Array.Empty<string>()));
            }
            o.Effects.Add("station:" + st.Id + ":" + state);
            if (state == "fault") o.Effects.Add("call:" + st.Call);
        }

        // L11-02: a reported fault is replaced after a delay
        private void Report(SimAction a, ActionOutcome o)
        {
            if (!Station(a, o, out var st)) return;
            Start();
            if (StateOf(st.Id) != "fault") { o.Effects.Add("no_fault_found:" + st.Id); return; }
            States[st.Id] = "replacing";
            _replaceAt[st.Id] = _s.Clock.Steps + (long)(Tables.ReplacementDelayMinutes * 60 / Time.SimClock.Step);
            _history.Add(new GraphEvent("fault_reported", st.Id, _s.Clock.Steps, new[] { Tables.Items["fault_reported"] }));
            o.Effects.Add("replacement_in_minutes:" + Tables.ReplacementDelayMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _s.Patient.Clinical.ClearFlag(Tables.ReadyFlag);
        }

        // L11-03: declaring the theatre ready needs every required station ready; otherwise it is a major item
        private void Declare(ActionOutcome o)
        {
            Start();
            var notReady = Tables.Stations.Where(s => s.Required && StateOf(s.Id) != "ready").ToList();
            if (notReady.Count == 0)
            {
                _s.Patient.Clinical.SetFlag(Tables.ReadyFlag);
                _history.Add(new GraphEvent("or_ready", "or", _s.Clock.Steps, new[] { Tables.Items["ready"] }));
                o.Effects.Add("or_ready");
                return;
            }
            var items = new List<string> { Tables.Items["declared_not_ready"] };
            foreach (var s in notReady)
            {
                if (StateOf(s.Id) == "fault") items.Add(Tables.Items["fault_ignored"]);
                else if (s.Critical && StateOf(s.Id) == "unchecked") items.Add(Tables.Items["critical_unchecked"]);
            }
            _history.Add(new GraphEvent("declared_not_ready", "or", _s.Clock.Steps, items));
            o.Effects.Add("not_ready:" + string.Join(",", notReady.Select(s => s.Id)));
        }

        // S2: a substitute theatre may be used (institutional); every station of the first theatre is replaced by one that works
        private void Substitute(ActionOutcome o)
        {
            if (!Tables.SubstituteIn.Contains(_s.Case.Scenario)) { o.Reject(NotAllowed); return; }
            Start();
            foreach (var st in Tables.Stations.Where(x => StateOf(x.Id) == "fault" || StateOf(x.Id) == "replacing").ToList()) States[st.Id] = "ready";
            _replaceAt.Clear();
            _hiddenFaults.Clear();
            _history.Add(new GraphEvent("substitute_theatre", "or", _s.Clock.Steps, new[] { Tables.Items["substitute"] }));
            o.Effects.Add("substitute_theatre");
        }

        // a fault that arrives from an event: a station that was ready fails
        private void OnEvent(WorkflowEvent e)
        {
            if (e.Name != Tables.FaultEvent) return;
            if (Tables.Find(e.Source) == null) return;
            Start();
            States[e.Source] = "fault";
            _replaceAt.Remove(e.Source);
            _s.Patient.Clinical.ClearFlag(Tables.ReadyFlag);
            _history.Add(new GraphEvent("fault_event", e.Source, _s.Clock.Steps, Array.Empty<string>()));
        }
    }
}
