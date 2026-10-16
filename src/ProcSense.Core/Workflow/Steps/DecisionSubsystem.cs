using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>What the trainee declared at Step 8 and what the engine expected at that moment.</summary>
    public sealed record DeclaredDecision(long Step, string Declared, string Reason, string Expected, string Check, IReadOnlyList<string> Evidence, bool Correct);

    /// <summary>A call to the senior or the anaesthetist: the role, the SBAR parts that were missing and whether the role is one the master decision called for.</summary>
    public sealed record Escalation(long Step, string Role, IReadOnlyList<string> Missing, bool RightRole);

    /// <summary>
    /// Step 8 of the walkthrough: the master decision, 'Is it safe to continue?'. The engine asks it again whenever information changes and keeps the outcome it expects (for the score only, never shown in
    /// assessment). The trainee states the outcome (declare_outcome), calls the senior or the anaesthetist with an SBAR (escalate), says a concern out loud (say_concern, in the sepsis step), and can end the case by
    /// postponing or redirecting it. A treatment is only ever requested (the medicine workflow); there is no verb that gives one. The engine compares, it never decides for the trainee.
    /// </summary>
    public sealed class DecisionSubsystem : ISubsystem
    {
        public const string DeclareVerb = "declare_outcome";
        public const string EscalateVerb = "escalate";
        public const string PostponeVerb = "postpone";
        public const string RedirectVerb = "redirect";

        public const string MissingOutcome = "MISSING_OUTCOME";
        public const string UnknownOutcome = "UNKNOWN_OUTCOME";
        public const string MissingRole = "MISSING_ROLE";
        public const string UnknownRole = "UNKNOWN_ROLE";
        public const string MissingReason = "MISSING_REASON";
        public const string MissingService = "MISSING_SERVICE";
        public const string AlreadyEnded = "ALREADY_ENDED";

        public const string DeclaredFlag = "decision.declared";
        public const string VariantPrefix = "variant.";
        public const string EndStateEvent = "E-END-STATE";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly DecisionEngine _engine;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<DeclaredDecision> _declared = new List<DeclaredDecision>();
        private readonly List<Escalation> _escalations = new List<Escalation>();
        private CaseFile? _file;
        private Session _s = null!;
        private string _last = "";

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public DecisionSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; _engine = new DecisionEngine(content.Decision); }

        public int Order => PipelineOrder.Events - 2;

        /// <summary>Each change of the expected outcome, each declaration, call and end action, with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        public IReadOnlyList<DeclaredDecision> Declared => _declared;

        public IReadOnlyList<Escalation> Escalations => _escalations;

        /// <summary>The end state the trainee's postponing or redirecting led to (READY_SAFE and the others are in content/end_states.csv), or "" while the case goes on.</summary>
        public string EndState { get; private set; } = "";

        /// <summary>The outcome the engine expects now.</summary>
        public DecisionResult Current => _engine.Evaluate(Input());

        private DecisionTables Tables => _content.Decision;

        public void Init(Session session)
        {
            _s = session;
            session.DecisionStep = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f.ForVariant(session.Variant) : null);
            session.Verbs.Register(DeclareVerb, requiresIdentity: true);
            session.Verbs.Register(EscalateVerb);
            session.Verbs.Register(PostponeVerb, requiresIdentity: true);
            session.Verbs.Register(RedirectVerb, requiresIdentity: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case DeclareVerb: Declare(a, o); break;
                case EscalateVerb: Escalate(a, o); break;
                case PostponeVerb: End(a, o, postpone: true); break;
                case RedirectVerb: End(a, o, postpone: false); break;
            }
            if (o.Accepted) Record();
        }

        public void OnTick() { }

        // ---------------------------------------------------------------- declare

        private void Declare(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("outcome", out var said) || said.Length == 0) { o.Reject(MissingOutcome); return; }
            string code = said.ToUpperInvariant();
            if (Tables.Outcomes.All(x => x.Code != code)) { o.Reject(UnknownOutcome); return; }
            var expected = Current;
            bool right = code == expected.Outcome;
            a.Params.TryGetValue("reason", out var reason);
            _declared.Add(new DeclaredDecision(_s.Clock.Steps, code, reason ?? "", expected.Outcome, expected.Check, expected.Evidence, right));
            _s.Patient.Clinical.SetFlag(DeclaredFlag);
            var items = Tables.Items;
            string item = right ? items["correct"] : expected.Outcome == "EMERGENCY" ? items["emergency_missed"] : items["wrong"];
            _history.Add(new GraphEvent(right ? "declared_correct" : "declared_wrong", code, _s.Clock.Steps, new[] { item }));
            o.Effects.Add(right ? "decision:correct" : "decision:wrong");
            if (_s.Mode == SessionMode.Practice) o.Effects.Add("expected:" + expected.Outcome);       // practice shows the answer; assessment never does
        }

        // ---------------------------------------------------------------- escalate

        private void Escalate(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("role", out var role) || role.Length == 0) { o.Reject(MissingRole); return; }
            if (Tables.Roles.All(r => r.Id != role)) { o.Reject(UnknownRole); return; }
            var missing = Tables.Sbar.Where(p => !a.Params.TryGetValue(p, out var text) || text.Trim().Length == 0).ToList();
            var wanted = Tables.ExpectedRoles.TryGetValue(Current.Check, out var roles) ? roles : (IReadOnlyList<string>)Array.Empty<string>();
            bool rightRole = wanted.Contains(role);
            _escalations.Add(new Escalation(_s.Clock.Steps, role, missing, rightRole));
            var items = new List<string> { missing.Count == 0 ? Tables.Items["sbar_complete"] : Tables.Items["sbar_incomplete"] };
            if (rightRole) items.Add(Tables.Items["right_role"]);
            _history.Add(new GraphEvent("escalated:" + role, role, _s.Clock.Steps, items));
            o.Effects.Add("escalated:" + role);
            if (missing.Count > 0) o.Effects.Add("sbar_missing:" + string.Join(",", missing));
            if (!rightRole) o.Effects.Add("role_not_needed");
            o.Effects.Add("responded:" + role);                                                 // the senior or anaesthetist answers the call
        }

        // ---------------------------------------------------------------- postpone / redirect

        private void End(SimAction a, ActionOutcome o, bool postpone)
        {
            if (EndState.Length > 0) { o.Reject(AlreadyEnded); return; }
            string param = postpone ? "reason" : "service";
            if (!a.Params.TryGetValue(param, out var why) || why.Trim().Length == 0) { o.Reject(postpone ? MissingReason : MissingService); return; }
            string wantedEnd = ExpectedEnd();
            var items = Tables.Items;
            string end, item;
            if (postpone && Current.Outcome == "EMERGENCY") { end = "SAFETY_FAILURE"; item = items["postpone_in_emergency"]; }
            else if (postpone) { bool ok = wantedEnd == "postponed"; end = ok ? "POSTPONED_APPROPRIATELY" : "SAFE_BUT_DELAYED"; item = ok ? items["postpone_right"] : items["postpone_wrong"]; }
            else { bool ok = wantedEnd == "redirected"; end = ok ? "REDIRECTED_APPROPRIATELY" : "SAFE_BUT_DELAYED"; item = ok ? items["redirect_right"] : items["redirect_wrong"]; }
            EndState = end;
            _history.Add(new GraphEvent((postpone ? "postponed:" : "redirected:") + end, why, _s.Clock.Steps, new[] { item }));
            o.Effects.Add("end_state:" + end);
            _s.Events.Publish(new WorkflowEvent(EndStateEvent, end));
        }

        /// <summary>The end state the case expects: the variant's if a variant is set (flag variant.ID), else the case's own.</summary>
        private string ExpectedEnd()
        {
            if (_file == null) return "";
            foreach (var kv in _file.VariantEndStates)
                if (_s.Patient.Clinical.HasFlag(VariantPrefix + kv.Key)) return kv.Value;
            return _file.ExpectedEndState;
        }

        // ---------------------------------------------------------------- re-asking

        private void Record()
        {
            var r = Current;
            string key = r.Outcome + "|" + r.Check + "|" + string.Join(",", r.Evidence);
            if (key == _last) return;
            _last = key;
            _history.Add(new GraphEvent("decision_expected:" + r.Outcome, r.Check, _s.Clock.Steps, Array.Empty<string>()));
        }

        private DecisionInput Input()
        {
            var c = _s.Patient.Clinical;
            var v = _s.Patient.Physiological.Vitals;
            int? news2 = null;
            for (int i = c.News2.Count - 1; i >= 0; i--) if (c.News2[i].Tier != "not_used") { news2 = c.News2[i].Total; break; }
            bool pregnancyPositive = c.Investigations.Any(x => x.Test == "pregnancy" && x.Results.Any(r => r.Value.IndexOf("positive", StringComparison.OrdinalIgnoreCase) >= 0));
            var open = _s.Problems?.Open ?? (IReadOnlyList<Problem>)Array.Empty<Problem>();
            return new DecisionInput(_s.Case.Scenario, open, news2, v.SystolicBp, v.Spo2, v.Acvpu, c.Flags, pregnancyPositive,
                _s.Investigations?.NeededResultsUnopened ?? false, c.Fasting?.Status == "breach", _file?.Risk?.Asa?.Max ?? 0);
        }
    }
}
