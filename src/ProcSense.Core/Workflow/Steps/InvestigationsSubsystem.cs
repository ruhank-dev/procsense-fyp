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
        public const string SendVerb = "send_samples";

        public const string MissingTest = "MISSING_TEST";
        public const string UnknownTest = "UNKNOWN_TEST";
        public const string OrderPending = "ORDER_PENDING";
        public const string NotOrdered = "NOT_ORDERED";
        public const string WrongKind = "WRONG_KIND";
        public const string AlreadyTaken = "ALREADY_TAKEN";
        public const string UnknownTube = "UNKNOWN_TUBE";
        public const string UnknownLabel = "UNKNOWN_LABEL";
        public const string NotReady = "NOT_READY";
        public const string ViewFirst = "VIEW_FIRST";
        public const string PregnancyStatusRequired = "PREGNANCY_STATUS_REQUIRED";
        public const string BadSend = "BAD_SEND";
        public const string UnknownDispatch = "UNKNOWN_DISPATCH";
        public const string NothingToSend = "NOTHING_TO_SEND";
        public const string NotWaiting = "NOT_WAITING";
        public const string PregnancyFlag = "pregnancy.status_required";
        public const string TestsOrderedFlag = "tests.needed_ordered";
        public const string ResultsReviewedFlag = "tests.needed_reviewed";
        /// <summary>Set while the latest opened result of a test carries a sample-quality fault (haemolysed, drip arm, wrong tube, clotted...) and the test has not been repeated (DT-094).</summary>
        public const string UnrepeatedFaultFlag = "results.unrepeated_fault";
        public const string AnyViewedFlag = "results.any_viewed";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly IndicationEvaluator _indications;
        private readonly ResultGenerator _generator;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<string> _missedReported = new HashSet<string>(StringComparer.Ordinal);
        private CaseFile? _file;
        private Session _s = null!;
        private SimRandom _rng = null!;
        private SimRandom _samplesRng = null!;
        private HashSet<string> _caseTests = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public InvestigationsSubsystem(ContentPackage content, CaseFile? caseFile = null)
        {
            _content = content; _fixed = caseFile; _indications = new IndicationEvaluator(content.Investigations); _generator = new ResultGenerator(content.Investigations);
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
            _samplesRng = session.Random.Stream("samples");     // its own stream: the porter's time for samples never changes another draw
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f.ForVariant(session.Variant) : null);
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
            session.Verbs.Register(SendVerb, requiresIdentity: true);
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
                case SendVerb: Send(a, o); break;
                case RiskSubsystem.DecideReadyVerb: if (o.Accepted) ReportMissed(o); break;
            }
            if (o.Accepted) Refresh();
        }

        // Who must have a known pregnancy status: the patient's sex and age against the gate's band.
        private bool PregnancyEligible() =>
            _file != null && _file.Identity.Sex == Tables.Gate.Sex && _file.Identity.Age >= Tables.Gate.MinAge && _file.Identity.Age <= Tables.Gate.MaxAge;

        // Known once the pregnancy question has been asked or a pregnancy test has resulted.
        private bool PregnancyKnown() =>
            Clinical.IsRevealed("pregnancy") || Clinical.Investigations.Any(x => x.Test == "pregnancy" && (x.State == "RESULT_AVAILABLE" || x.State == "VIEWED" || x.State == "ACKNOWLEDGED"));

        /// <summary>True while the case needs a result nobody has opened yet (the master decision cannot be made: more information is needed).</summary>
        public bool NeededResultsUnopened
        {
            get
            {
                var needed = NeededTests();
                return needed.Count > 0 && !needed.All(t => { var l = Latest(t); return l != null && (l.State == "VIEWED" || l.State == "ACKNOWLEDGED"); });
            }
        }

        // The flags the blockers and goals read: pregnancy status still needed, every needed test ordered, every needed result opened.
        private void Refresh()
        {
            if (PregnancyEligible() && !PregnancyKnown()) Clinical.SetFlag(PregnancyFlag); else Clinical.ClearFlag(PregnancyFlag);
            if (Clinical.Investigations.Any(x => x.State == "VIEWED" || x.State == "ACKNOWLEDGED")) Clinical.SetFlag(AnyViewedFlag); else Clinical.ClearFlag(AnyViewedFlag);
            var needed = NeededTests();
            if (needed.Count > 0 && needed.All(t => Latest(t) != null)) Clinical.SetFlag(TestsOrderedFlag); else Clinical.ClearFlag(TestsOrderedFlag);
            if (needed.Count > 0 && needed.All(t => { var l = Latest(t); return l != null && (l.State == "VIEWED" || l.State == "ACKNOWLEDGED"); })) Clinical.SetFlag(ResultsReviewedFlag); else Clinical.ClearFlag(ResultsReviewedFlag);
            if (Clinical.Investigations.Select(x => x.Test).Distinct().Any(test => { var l = Latest(test); return l != null && (l.State == "VIEWED" || l.State == "ACKNOWLEDGED") && l.Results.Any(r => r.Quality.Length > 0); })) Clinical.SetFlag(UnrepeatedFaultFlag); else Clinical.ClearFlag(UnrepeatedFaultFlag);
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
            if (last != null && last.State != "RESULT_AVAILABLE" && last.State != "VIEWED" && last.State != "ACKNOWLEDGED" && last.State != "REJECTED") { o.Reject(OrderPending); return; }
            if (Tables.Gate.Blocks.Contains(def.Id) && PregnancyEligible() && !PregnancyKnown()) { o.Reject(PregnancyStatusRequired); return; }
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
            o.Effects.Add("order_id:" + order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Note(order, "REQUESTED", verdict.Verdict == "NOT_INDICATED" ? new[] { "unnecessary.test_ordered" } : verdict.Needed ? new[] { "credit.test_appropriate" } : Array.Empty<string>());
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
            // send=later keeps a blood sample at the bedside until send_samples (the tube station or the porter, p0-diagnostic-transitions-s04); without it the sample goes at once, as before
            string send = a.Params.TryGetValue("send", out var sendText) && sendText.Length > 0 ? sendText : "now";
            if (send != "now" && send != "later" || send == "later" && kind != "blood") { o.Reject(BadSend); return; }
            if (a.Params.TryGetValue("tube", out var tube) && tube.Length > 0 && !Tables.Tubes.Contains(tube)) { o.Reject(UnknownTube); return; }
            if (a.Params.TryGetValue("label", out var label) && label.Length > 0 && !Tables.Labels.Contains(label)) { o.Reject(UnknownLabel); return; }
            foreach (var key in new[] { "tube", "arm", "label" })
                if (a.Params.TryGetValue(key, out var v)) order.Sample[key] = v;
            Note(order, "SAMPLE_TAKEN", Array.Empty<string>());
            o.Effects.Add("sample_taken:" + def.Id);
            if (send == "later")
            {
                order.State = "SAMPLE_TAKEN";
                o.Effects.Add("sample_waiting:" + def.Id);
                return;
            }
            StartProcessing(order, def);
        }

        // The labelled tubes waiting at the bedside go to the laboratory by the tube station or the porter; the laboratory's turnaround starts when they arrive.
        // The laboratory still checks each tube when it is processed (an unlabelled or mislabelled tube is rejected or gives someone else's result).
        private void Send(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("via", out var via) || via.Length == 0 || _content.Diagnostics.Way(via) == null) { o.Reject(UnknownDispatch); return; }
            var way = _content.Diagnostics.Way(via)!;
            var waiting = Clinical.Investigations.Where(x => x.State == "SAMPLE_TAKEN").ToList();
            if (a.Params.TryGetValue("tests", out var tests) && tests.Length > 0)
            {
                var named = tests.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
                if (named.Any(t => waiting.All(x => x.Test != t))) { o.Reject(NotWaiting); return; }
                waiting = waiting.Where(x => named.Contains(x.Test)).ToList();
            }
            if (waiting.Count == 0) { o.Reject(NothingToSend); return; }
            double minutes = way.TransitMinutes;
            if (way.Porter) minutes += _content.Locations.PorterMin + _samplesRng.NextDouble() * (_content.Locations.PorterMax - _content.Locations.PorterMin);
            long arrives = _s.Clock.Steps + (long)Math.Round(minutes * 60 / SimClock.Step);
            foreach (var order in waiting)
            {
                order.Sample["via"] = way.Id;
                order.ReadyStep = arrives;
                order.State = "SENT";
                Note(order, "SENT", Array.Empty<string>());
                o.Effects.Add("sent:" + order.Test + ":" + way.Id);
            }
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
            if (order.State == "VIEWED" && _s.ResultReview != null && !_s.ResultReview.GateAcknowledge(a, o, order)) return;
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
                else if (order.State == "SENT" && now >= order.ReadyStep) StartProcessing(order, Tables.Find(order.Test)!);   // the sample has reached the laboratory
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
            var results = ResultGenerator.CaseResults(_file, def, ValidBefore(order));
            order.ResultIds = results.Select(r => r.Id).ToList();
            double minutes = results.Count > 0 ? results.Max(r => (double)r.MinutesToResult) : def.MinMinutes + _rng.NextDouble() * (def.MaxMinutes - def.MinMinutes);
            order.ReadyStep = _s.Clock.Steps + (long)Math.Round(minutes * 60 / SimClock.Step);
            order.State = "PROCESSING";
            Note(order, "PROCESSING", Array.Empty<string>());
        }

        // The earlier samples of this test that gave results to trust (a rejected or mislabelled one does not count), so a re-take is a first sample and not a repeat.
        private int ValidBefore(InvestigationOrder order) =>
            Clinical.Investigations.Count(x => x.Test == order.Test && x.Id < order.Id && x.Valid && (x.State == "RESULT_AVAILABLE" || x.State == "VIEWED" || x.State == "ACKNOWLEDGED"));

        private void Finish(InvestigationOrder order)
        {
            var def = Tables.Find(order.Test)!;
            var made = _generator.Generate(_file, def, ValidBefore(order), order.Sample, _s.Case.Scenario);
            order.Valid = made.Valid;
            order.Note = made.Note;
            if (made.Outcome == "rejected")
            {
                order.State = "REJECTED";
                Note(order, "REJECTED", new[] { "incident.sample_rejected" });
                Clinical.SetFlag("incident.sample_rejected");
                _s.Events.Publish(new WorkflowEvent("E-SAMPLE-REJECTED", order.Test));
                return;
            }
            order.Results.Clear();
            order.Results.AddRange(made.Records);
            order.Report = _content.Imaging.Resolve(_file?.TableKey ?? _s.Case.Id, def.Id);
            order.ResultIds = made.Records.Select(r => r.Id).ToList();
            order.State = "RESULT_AVAILABLE";
            Note(order, "RESULT_AVAILABLE", Array.Empty<string>());
            if (made.Outcome == "mislabelled")
            {
                Clinical.SetFlag("incident.wrong_blood");
                _history.Add(new GraphEvent("sample_mislabelled", order.Test, _s.Clock.Steps, new[] { "incident.wrong_blood" }));
                _s.Events.Publish(new WorkflowEvent("E-SAMPLE-MISLABELLED", order.Test));
            }
            foreach (var r in made.Records.Where(r => r.Quality.Length > 0 && r.Quality != "mislabelled").Select(r => r.Quality).Distinct())
                _history.Add(new GraphEvent("sample_artefact:" + r, order.Test, _s.Clock.Steps, Array.Empty<string>()));
            if (order.ResultIds.Count == 0) _s.Events.Publish(new ResultReady(order.Test, "", order.Instance));
            foreach (var id in order.ResultIds) _s.Events.Publish(new ResultReady(order.Test, id, order.Instance));
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
