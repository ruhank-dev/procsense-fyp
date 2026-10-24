using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Walkthrough Step 7: reading results, interpreting them and the critical-result phone call. When a result that crosses a lab limit becomes available the lab phones (a voice, no avatar). The trainee must answer,
    /// confirm the patient's name and date of birth with the caller, read every value back exactly, view the result and then acknowledge it with an action and a note. Until then the result is a blocker: nothing
    /// can be transferred to theatre, and deciding to proceed is a critical item. A result never opened that the case needed blocks in the same way. Interpretation (normal, abnormal or artefact) is checked against
    /// what the results really are; treating an artefact as real is recorded for the debrief.
    /// </summary>
    public sealed class ResultReviewSubsystem : ISubsystem
    {
        public const string AnswerVerb = "answer_phone";
        public const string ConfirmVerb = "confirm_patient_on_call";
        public const string ReadBackVerb = "read_back";
        public const string InterpretVerb = "interpret_result";
        public const string InterpretImagingVerb = "interpret_imaging";
        public const string UseImagingVerb = "USE_INTERPRET_IMAGING";

        public const string NoCall = "NO_CALL";
        public const string CallNotAnswered = "CALL_NOT_ANSWERED";
        public const string CallIdentityFirst = "CALL_IDENTITY_FIRST";
        public const string CallReadBackFirst = "CALL_READ_BACK_FIRST";
        public const string WrongIdentity = "WRONG_IDENTITY_ON_CALL";
        public const string MissingValues = "MISSING_VALUES";
        public const string MissingAction = "MISSING_ACTION";
        public const string UnknownAction = "UNKNOWN_ACTION";
        public const string MissingNote = "MISSING_NOTE";
        public const string MissingTest = "MISSING_TEST";
        public const string UnknownTest = "UNKNOWN_TEST";
        public const string NotOrdered = "NOT_ORDERED";
        public const string NotViewed = "NOT_VIEWED";
        public const string NothingToInterpret = "NOTHING_TO_INTERPRET";
        public const string UnknownInterpretation = "UNKNOWN_INTERPRETATION";

        public const string CriticalFlag = "critical.unacknowledged";
        public const string UnreviewedFlag = "results.unreviewed_required";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private CaseFile? _file;
        private Session _s = null!;
        private HashSet<string> _facts = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public ResultReviewSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        // after the investigations step, so the blockers see the state it has just changed; acknowledging a critical result is gated from inside the investigations step
        public int Order => PipelineOrder.Investigations + 1;

        /// <summary>Calls received, answered and finished, interpretations and critical findings, with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private ClinicalLayer Clinical => _s.Patient.Clinical;
        private CriticalTable Critical => _content.Investigations.Critical;

        public void Init(Session session)
        {
            _s = session;
            session.ResultReview = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f.ForVariant(session.Variant) : null);
            if (_file != null) _facts = new HashSet<string>(_file.Facts, StringComparer.Ordinal);
            foreach (var verb in new[] { AnswerVerb, ConfirmVerb, ReadBackVerb }) session.Verbs.Register(verb);
            session.Verbs.Register(InterpretVerb, requiresIdentity: true);
            session.Verbs.Register(InterpretImagingVerb, requiresIdentity: true);
            session.Events.Subscribe<ResultReady>(OnResultReady);
        }

        // ---------------------------------------------------------------- the lab rings

        private void OnResultReady(ResultReady e)
        {
            var order = Clinical.Investigations.LastOrDefault(x => x.Test == e.Test && x.Instance == e.Instance);
            if (order == null || !_seen.Add(order.Id)) return;
            var found = Critical.Evaluate(order.Results, _facts);
            if (found.Count == 0) { Refresh(); return; }       // a result the case needed that nobody has opened is a blocker from the moment it arrives
            var id = _s.Case.Identity;
            string born = DateTime.TryParseExact(id.Dob, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : id.Dob;
            var call = new CriticalCall
            {
                Id = Clinical.CriticalCalls.Count + 1,
                Test = order.Test,
                State = "RINGING",
                RangAtStep = _s.Clock.Steps,
                Message = Critical.Say(id.FullName, born, found.Select(x => x.Spoken)),
            };
            foreach (var f in found) call.Values.Add(new CriticalValue(f.Analyte, f.Value, f.Spoken));
            Clinical.CriticalCalls.Add(call);
            Clinical.SetFlag(CriticalFlag);
            _history.Add(new GraphEvent("critical_call_rings", order.Test, _s.Clock.Steps, Array.Empty<string>()));
            Refresh();
        }

        // ---------------------------------------------------------------- actions

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case AnswerVerb: Answer(o); break;
                case ConfirmVerb: Confirm(a, o); break;
                case ReadBackVerb: ReadBack(a, o); break;
                case InterpretVerb: Interpret(a, o); break;
                case InterpretImagingVerb: InterpretImaging(a, o); break;
                case RiskSubsystem.DecideReadyVerb: if (o.Accepted) Readiness(a, o); break;
            }
            if (o.Accepted) Refresh();
        }

        public void OnTick() { }

        private CriticalCall? Current() => Clinical.CriticalCalls.FirstOrDefault(c => c.State != "DONE");

        private void Answer(ActionOutcome o)
        {
            var call = Clinical.CriticalCalls.FirstOrDefault(c => c.State == "RINGING");
            if (call == null) { o.Reject(NoCall); return; }
            call.State = "ANSWERED";
            _history.Add(new GraphEvent("critical_call_answered", call.Test, _s.Clock.Steps, Array.Empty<string>()));
            o.Effects.Add("call_answered:" + call.Test);
        }

        private void Confirm(SimAction a, ActionOutcome o)
        {
            var call = Current();
            if (call == null) { o.Reject(NoCall); return; }
            if (call.State == "RINGING") { o.Reject(CallNotAnswered); return; }
            a.Params.TryGetValue("name", out var name);
            a.Params.TryGetValue("dob", out var dob);
            var fit = SpokenIdentity.Compare((name ?? "") + " " + (dob ?? ""), _s.Case.Identity);
            if (fit.Name != Fit.Match || fit.Date != Fit.Match) { o.Effects.Add("identity_on_call:wrong"); o.Reject(WrongIdentity); return; }
            if (call.State == "ANSWERED") call.State = "IDENTITY_CONFIRMED";
            o.Effects.Add("identity_on_call:confirmed");
        }

        // The values read back must be exactly the values phoned, compared as numbers (6.60 is 6.6), not as text.
        private void ReadBack(SimAction a, ActionOutcome o)
        {
            var call = Current();
            if (call == null) { o.Reject(NoCall); return; }
            if (call.State == "RINGING") { o.Reject(CallNotAnswered); return; }
            if (call.State == "ANSWERED") { o.Reject(CallIdentityFirst); return; }
            if (!a.Params.TryGetValue("values", out var text) || text.Trim().Length == 0) { o.Reject(MissingValues); return; }
            var said = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && double.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) said[kv[0].Trim()] = v;
            }
            call.ReadBackAttempts++;
            bool exact = call.Values.All(v => said.TryGetValue(v.Analyte, out double x) && Math.Abs(x - v.Value) < 1e-9) && said.Count == call.Values.Count;
            if (!exact)
            {
                o.Effects.Add("readback:wrong");
                _history.Add(new GraphEvent("critical_readback_wrong", call.Test, _s.Clock.Steps, new[] { "minor.readback_wrong" }));
                return;
            }
            if (call.State == "IDENTITY_CONFIRMED") call.State = "READ_BACK";
            o.Effects.Add("readback:correct");
        }

        /// <summary>
        /// Called by the investigations step when a viewed result is about to be acknowledged. Acknowledging a critical result needs the whole call done first, and an action and a note;
        /// otherwise the action is refused and the result stays a blocker. Returns false when it refused.
        /// </summary>
        internal bool GateAcknowledge(SimAction a, ActionOutcome o, InvestigationOrder order)
        {
            var call = Clinical.CriticalCalls.LastOrDefault(c => c.Test == order.Test && c.State != "DONE");
            if (call == null) return true;                                                              // not a critical result, or already dealt with
            if (call.State == "RINGING") { o.Reject(CallNotAnswered); return false; }
            if (call.State == "ANSWERED") { o.Reject(CallIdentityFirst); return false; }
            if (call.State == "IDENTITY_CONFIRMED") { o.Reject(CallReadBackFirst); return false; }
            if (!a.Params.TryGetValue("action", out var action) || action.Length == 0) { o.Reject(MissingAction); return false; }
            if (!Critical.Actions.Contains(action)) { o.Reject(UnknownAction); return false; }
            if (!a.Params.TryGetValue("note", out var note) || note.Trim().Length == 0) { o.Reject(MissingNote); return false; }
            call.State = "DONE"; call.Action = action; call.Note = note;
            _history.Add(new GraphEvent("critical_call_done", order.Test, _s.Clock.Steps, new[] { "credit.critical_acknowledged" }));
            o.Effects.Add("critical_acknowledged:" + action);
            return true;
        }

        // L7-02: the interpretation is checked against what the results really are.
        private void Interpret(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("test", out var test) || test.Length == 0) { o.Reject(MissingTest); return; }
            if (_content.Investigations.Find(test) == null) { o.Reject(UnknownTest); return; }
            var order = Clinical.Investigations.LastOrDefault(x => x.Test == test);
            if (order == null) { o.Reject(NotOrdered); return; }
            if (order.State != "VIEWED" && order.State != "ACKNOWLEDGED") { o.Reject(NotViewed); return; }
            if (order.Report != null) { o.Reject(UseImagingVerb); return; }
            if (order.Results.Count == 0) { o.Reject(NothingToInterpret); return; }
            if (!a.Params.TryGetValue("interpretation", out var said) || (said != "normal" && said != "abnormal" && said != "artefact")) { o.Reject(UnknownInterpretation); return; }

            string truth = TruthOf(order);
            order.Interpretation = said;
            bool right = said == truth;
            o.Effects.Add("interpretation:" + (right ? "correct" : "wrong"));
            _history.Add(new GraphEvent(right ? "interpretation_correct" : "interpretation_wrong", test, _s.Clock.Steps, new[] { right ? "credit.interpretation" : "major.interpretation_wrong" }));
            if (truth == "artefact" && said == "abnormal")
            {
                Clinical.SetFlag("interpretation.artefact_taken_at_face_value");
                _history.Add(new GraphEvent("artefact_taken_at_face_value", test, _s.Clock.Steps, new[] { "debrief.over_treatment_risk" }));
            }
        }

        // T-IV7: the category of the picture is checked against what the picture really shows. 'Not visualised' is equivocal, so calling it normal is the classic error.
        private void InterpretImaging(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("test", out var test) || test.Length == 0) { o.Reject(MissingTest); return; }
            if (_content.Investigations.Find(test) == null) { o.Reject(UnknownTest); return; }
            var order = Clinical.Investigations.LastOrDefault(x => x.Test == test);
            if (order == null) { o.Reject(NotOrdered); return; }
            if (order.State != "VIEWED" && order.State != "ACKNOWLEDGED") { o.Reject(NotViewed); return; }
            if (order.Report == null) { o.Reject(NothingToInterpret); return; }
            if (!a.Params.TryGetValue("category", out var said) || !_content.Imaging.Categories.Contains(said)) { o.Reject(UnknownInterpretation); return; }

            string truth = order.Report.Category;
            order.Interpretation = said;
            bool right = said == truth;
            o.Effects.Add("imaging_interpretation:" + (right ? "correct" : "wrong"));
            if (right) { _history.Add(new GraphEvent("imaging_interpretation_correct", test, _s.Clock.Steps, new[] { "credit.imaging_interpretation" })); return; }
            if (truth == "non_diagnostic" && said == "normal")
            {
                Clinical.SetFlag("imaging.equivocal_taken_as_negative");
                o.Effects.Add("equivocal_taken_as_negative");
                _history.Add(new GraphEvent("not_visualised_taken_as_negative", test, _s.Clock.Steps, new[] { "major.not_visualised_taken_as_negative" }));
            }
            else _history.Add(new GraphEvent("imaging_interpretation_wrong", test, _s.Clock.Steps, new[] { "major.interpretation_wrong" }));
        }

        private static string TruthOf(InvestigationOrder order)
        {
            if (order.Results.Any(r => r.Quality.Length > 0)) return "artefact";
            return order.Results.Any(r => r.Flag != "normal" && r.Flag != "negative") ? "abnormal" : "normal";
        }

        // ---------------------------------------------------------------- readiness (L7-03, E7-A)

        private void Readiness(SimAction a, ActionOutcome o)
        {
            a.Params.TryGetValue("option", out var option);
            if (Current() != null && option == "proceed")
            {
                o.Effects.Add("critical:proceed_with_unacknowledged_result");
                o.Effects.Add("score:critical.ignored_critical_result");
                _history.Add(new GraphEvent("proceeded_with_unacknowledged_critical", "", _s.Clock.Steps, new[] { "critical.ignored_critical_result" }));
            }
            var needed = _s.Investigations?.NeededTests() ?? new List<string>();
            foreach (var order in Clinical.Investigations)
            {
                if (order.State == "RESULT_AVAILABLE")
                {
                    o.Effects.Add("major:unreviewed_result:" + order.Test);
                    o.Effects.Add("score:major.result_not_reviewed");
                    _history.Add(new GraphEvent("result_not_reviewed", order.Test, _s.Clock.Steps, new[] { "major.result_not_reviewed" }));
                }
                else if (option == "proceed" && needed.Contains(order.Test) && (order.State == "REQUESTED" || order.State == "PROCESSING"))
                    o.Effects.Add("pending:" + order.Test);
            }
        }

        // The two readiness blockers follow the state: an open critical call, and a result the case needed that was never opened.
        private void Refresh()
        {
            if (Current() != null) Clinical.SetFlag(CriticalFlag); else Clinical.ClearFlag(CriticalFlag);
            var needed = _s.Investigations?.NeededTests() ?? new List<string>();
            if (Clinical.Investigations.Any(x => x.State == "RESULT_AVAILABLE" && needed.Contains(x.Test))) Clinical.SetFlag(UnreviewedFlag); else Clinical.ClearFlag(UnreviewedFlag);
        }
    }
}
