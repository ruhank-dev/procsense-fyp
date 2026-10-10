using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Random;
using ProcSense.Core.State;
using ProcSense.Core.Time;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>Published on the EventBus when a test finishes processing, one for each result it holds (ResultId is "" when the case states none). The EMR and the printed report subscribe later.</summary>
    public readonly struct ResultReady
    {
        public ResultReady(string test, string resultId, int instance) { Test = test; ResultId = resultId; Instance = instance; }
        public string Test { get; }
        public string ResultId { get; }
        public int Instance { get; }
    }

    /// <summary>
    /// Walkthrough Step 6: ordering and doing tests. Each order moves REQUESTED, then SAMPLE_TAKEN (blood or a bedside test) or TRANSPORTED (imaging, once the patient is in the scan room), PROCESSING on the sim
    /// clock, RESULT_AVAILABLE, VIEWED and ACKNOWLEDGED. The indication rule judges every order (appropriate or not indicated) and, when the trainee decides on readiness, lists the tests the case needed that were
    /// never ordered. Results are only those the case states; a second order of the same test returns the case's repeat results when it has them. Tests need a confirmed identity before anything is ordered.
    /// </summary>
    public sealed class InvestigationsSubsystem : ISubsystem
    {
        public const string OrderVerb = "order_investigation";
        public const string TakeSampleVerb = "take_sample";
        public const string BedsideVerb = "perform_bedside_test";
        public const string ViewVerb = "view_result";
        public const string AcknowledgeVerb = "acknowledge_result";

        public const string MissingTest = "MISSING_TEST";
        public const string UnknownTest = "UNKNOWN_TEST";
        public const string OrderPending = "ORDER_PENDING";
        public const string NotOrdered = "NOT_ORDERED";
        public const string WrongKind = "WRONG_KIND";
        public const string AlreadyTaken = "ALREADY_TAKEN";
        public const string UnknownTube = "UNKNOWN_TUBE";
        public const string NotReady = "NOT_READY";
        public const string ViewFirst = "VIEW_FIRST";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly IndicationEvaluator _indications;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<string> _missedReported = new HashSet<string>(StringComparer.Ordinal);
        private CaseFile? _file;
        private Session _s = null!;
        private SimRandom _rng = null!;
        private HashSet<string> _caseTests = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public InvestigationsSubsystem(ContentPackage content, CaseFile? caseFile = null)
        {
            _content = content; _fixed = caseFile; _indications = new IndicationEvaluator(content.Investigations);
        }

        public int Order => PipelineOrder.Investigations;

        /// <summary>Orders, state changes, tests not indicated and tests missed, with the sim step and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private ClinicalLayer Clinical => _s.Patient.Clinical;
        private InvestigationTables Tables => _content.Investigations;

        public void Init(Session session)
        {
            _s = session;
            session.Investigations = this;
            _rng = session.Random.Stream("investigations");
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
            if (_file != null)
                foreach (var r in _file.Results)
                {
                    var t = Tables.TestOfAnalyte(r.Test);
                    if (t != null && !InvestigationTables.IsRepeat(r.Test)) _caseTests.Add(t.Id);
                }
            session.Verbs.Register(OrderVerb, requiresIdentity: true);
            session.Verbs.Register(TakeSampleVerb, requiresIdentity: true, requiresPatient: true);
            session.Verbs.Register(BedsideVerb, requiresIdentity: true, requiresPatient: true);
            session.Verbs.Register(ViewVerb, requiresIdentity: true);
            session.Verbs.Register(AcknowledgeVerb, requiresIdentity: true);
            if (Tables.DelayEvent.Length > 0) session.Events.Subscribe<WorkflowEvent>(OnDelayEvent);
        }

        /// <summary>The tests this case needs (see <see cref="IndicationEvaluator.Needed"/>).</summary>
        public IReadOnlyList<string> NeededTests() => _indications.Needed(_s.Case.Scenario, _caseTests);

        // ---------------------------------------------------------------- actions

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case OrderVerb: Order_(a, o); break;
                case TakeSampleVerb: Take(a, o, "blood"); break;
                case BedsideVerb: Take(a, o, "bedside"); break;
                case ViewVerb: View(a, o); break;
                case AcknowledgeVerb: Acknowledge(a, o); break;
                case RiskSubsystem.DecideReadyVerb: if (o.Accepted) ReportMissed(o); break;
            }
        }

        private bool TestFrom(SimAction a, ActionOutcome o, out InvestigationDef def)
        {
            def = null!;
            if (!a.Params.TryGetValue("test", out var id) || id.Length == 0) { o.Reject(MissingTest); return false; }
            var found = Tables.Find(id);
            if (found == null) { o.Reject(UnknownTest); return false; }
            def = found;
            return true;
        }

        private InvestigationOrder? Latest(string test) => Clinical.Investigations.LastOrDefault(x => x.Test == test);

        private void Order_(SimAction a, ActionOutcome o)
        {
            if (!TestFrom(a, o, out var def)) return;
            var last = Latest(def.Id);
            if (last != null && last.State != "RESULT_AVAILABLE" && last.State != "VIEWED" && last.State != "ACKNOWLEDGED") { o.Reject(OrderPending); return; }
            // an imaging test can ask for the transfer in the same request (mode, escort, monitoring, accompany): the patient then goes to the scan room
            if (def.Kind == "imaging" && a.Params.ContainsKey("mode") && Clinical.Location != def.Location)
            {
                _s.Transport?.RequestTo(a, o, def.Location);
                if (!o.Accepted) return;
            }

            var verdict = _indications.Evaluate(def.Id, _s.Case.Scenario, _caseTests);
            var order = new InvestigationOrder
            {
                Id = Clinical.Investigations.Count + 1,
                Test = def.Id,
                Label = def.Label,
                Kind = def.Kind,
                Location = def.Location,
                Instance = Clinical.Investigations.Count(x => x.Test == def.Id) + 1,
                State = "REQUESTED",
                OrderedStep = _s.Clock.Steps,
                Indication = verdict.Verdict,
                Reason = a.Params.TryGetValue("reason", out var reason) ? reason : "",
                Urgency = a.Params.TryGetValue("urgency", out var urgency) ? urgency : "",
            };
            Clinical.Investigations.Add(order);
            Note(order, "REQUESTED", verdict.Verdict == "NOT_INDICATED" ? new[] { "unnecessary.test_ordered" } : Array.Empty<string>());
            o.Effects.Add("ordered:" + def.Id);
            o.Effects.Add("indication:" + verdict.Verdict.ToLowerInvariant());
            if (verdict.Verdict == "NOT_INDICATED") _history.Add(new GraphEvent("test_not_indicated", def.Id, _s.Clock.Steps, new[] { "unnecessary.test_ordered" }));
            if (def.Kind == "imaging" && Clinical.Location == def.Location) Arrived(order, def);
        }

        private void Take(SimAction a, ActionOutcome o, string kind)
        {
            if (!TestFrom(a, o, out var def)) return;
            if (def.Kind != kind) { o.Reject(WrongKind); return; }
            var order = Latest(def.Id);
            if (order == null) { o.Reject(NotOrdered); return; }
            if (order.State != "REQUESTED") { o.Reject(AlreadyTaken); return; }
            if (a.Params.TryGetValue("tube", out var tube) && tube.Length > 0 && !Tables.Tubes.Contains(tube)) { o.Reject(UnknownTube); return; }
            foreach (var key in new[] { "tube", "arm", "label" })
                if (a.Params.TryGetValue(key, out var v)) order.Sample[key] = v;
            Note(order, "SAMPLE_TAKEN", Array.Empty<string>());
            o.Effects.Add("sample_taken:" + def.Id);
            StartProcessing(order, def);
        }

        private void View(SimAction a, ActionOutcome o)
        {
            if (!TestFrom(a, o, out var def)) return;
            var order = Latest(def.Id);
            if (order == null) { o.Reject(NotOrdered); return; }
            if (order.State == "RESULT_AVAILABLE") { order.State = "VIEWED"; Note(order, "VIEWED", Array.Empty<string>()); }
            else if (order.State != "VIEWED" && order.State != "ACKNOWLEDGED") { o.Reject(NotReady); return; }
            o.Effects.Add("viewed:" + def.Id);
            foreach (var id in order.ResultIds) o.Effects.Add("result:" + id);
        }

        private void Acknowledge(SimAction a, ActionOutcome o)
        {
            if (!TestFrom(a, o, out var def)) return;
            var order = Latest(def.Id);
            if (order == null) { o.Reject(NotOrdered); return; }
            if (order.State == "VIEWED") { order.State = "ACKNOWLEDGED"; Note(order, "ACKNOWLEDGED", Array.Empty<string>()); o.Effects.Add("acknowledged:" + def.Id); }
            else if (order.State == "ACKNOWLEDGED") o.Effects.Add("already_acknowledged:" + def.Id);
            else o.Reject(order.State == "RESULT_AVAILABLE" ? ViewFirst : NotReady);
        }

        // At readiness: every test the case needs and the trainee never ordered.
        private void ReportMissed(ActionOutcome o)
        {
            foreach (var test in NeededTests())
                if (Latest(test) == null && _missedReported.Add(test))
                {
                    o.Effects.Add("missed_test:" + test);
                    _history.Add(new GraphEvent("test_missed", test, _s.Clock.Steps, new[] { "missed.test_not_ordered" }));
                }
        }

        // ---------------------------------------------------------------- time

        public void OnTick()
        {
            var list = Clinical.Investigations;
            if (list.Count == 0) return;
            long now = _s.Clock.Steps;
            for (int i = 0; i < list.Count; i++)
            {
                var order = list[i];
                if (order.State == "PROCESSING" && now >= order.ReadyStep) Finish(order);
                else if (order.State == "REQUESTED")
                {
                    if (order.Kind == "imaging" && Clinical.Location == order.Location) Arrived(order, Tables.Find(order.Test)!);   // the patient has reached the scan room
                }
            }
        }

        private void Arrived(InvestigationOrder order, InvestigationDef def)
        {
            Note(order, "TRANSPORTED", Array.Empty<string>());
            StartProcessing(order, def);
        }

        private void StartProcessing(InvestigationOrder order, InvestigationDef def)
        {
            order.ResultIds = ResultsFor(order, def).Select(r => r.Id).ToList();
            var results = ResultsFor(order, def);
            double minutes = results.Count > 0 ? results.Max(r => (double)r.MinutesToResult) : def.MinMinutes + _rng.NextDouble() * (def.MaxMinutes - def.MinMinutes);
            order.ReadyStep = _s.Clock.Steps + (long)Math.Round(minutes * 60 / SimClock.Step);
            order.State = "PROCESSING";
            Note(order, "PROCESSING", Array.Empty<string>());
        }

        private void Finish(InvestigationOrder order)
        {
            order.State = "RESULT_AVAILABLE";
            Note(order, "RESULT_AVAILABLE", Array.Empty<string>());
            if (order.ResultIds.Count == 0) _s.Events.Publish(new ResultReady(order.Test, "", order.Instance));
            foreach (var id in order.ResultIds) _s.Events.Publish(new ResultReady(order.Test, id, order.Instance));
        }

        // The case's results for this test. The first order gets the first set; a second or later order gets the repeat set when the case states one, otherwise the same results (nothing has changed).
        private List<CaseFileResult> ResultsFor(InvestigationOrder order, InvestigationDef def)
        {
            if (_file == null) return new List<CaseFileResult>();
            var mine = _file.Results.Where(r => def.Analytes.Contains(InvestigationTables.BaseName(r.Test))).ToList();
            var first = mine.Where(r => !InvestigationTables.IsRepeat(r.Test)).ToList();
            var repeat = mine.Where(r => InvestigationTables.IsRepeat(r.Test)).ToList();
            return order.Instance > 1 && repeat.Count > 0 ? repeat : first;
        }

        // A lab delay (the event named in the content) pushes every test still processing back by the content's minutes.
        private void OnDelayEvent(WorkflowEvent e)
        {
            if (e.Name != Tables.DelayEvent || _s == null) return;
            foreach (var order in Clinical.Investigations)
                if (order.State == "PROCESSING" && order.Kind == "blood")
                {
                    order.ReadyStep += (long)Math.Round(Tables.DelayMinutes * 60 / SimClock.Step);
                    order.Delayed = true;
                }
        }

        private void Note(InvestigationOrder order, string state, IReadOnlyList<string> scoreItems)
        {
            order.State = state == "SAMPLE_TAKEN" || state == "TRANSPORTED" ? order.State : state;
            _history.Add(new GraphEvent("investigation:" + order.Test + ":" + state, order.Test, _s.Clock.Steps, scoreItems));
        }
    }
}
