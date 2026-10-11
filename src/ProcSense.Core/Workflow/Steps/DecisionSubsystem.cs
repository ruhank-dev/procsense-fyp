using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Asks the master decision, 'Is it safe to continue?', again whenever information changes and keeps the outcome the engine expects, for the score only: it is never shown to the trainee and never decides for them.
    /// It reads the problems found so far (the problem registry), the patient's signs, the results the case needs, the fasting result and the case's ASA. Each time the expected outcome changes it is written to History
    /// (kind decision_expected:OUTCOME) with the sim step. The declare-outcome step of the workflow compares the trainee's choice with <see cref="Current"/>.
    /// </summary>
    public sealed class DecisionSubsystem : ISubsystem
    {
        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly DecisionEngine _engine;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private CaseFile? _file;
        private Session _s = null!;
        private string _last = "";

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public DecisionSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; _engine = new DecisionEngine(content.Decision); }

        public int Order => PipelineOrder.Events - 2;

        /// <summary>Each change of the expected outcome, with sim steps. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        /// <summary>The outcome the engine expects now.</summary>
        public DecisionResult Current => _engine.Evaluate(Input());

        public void Init(Session session)
        {
            _s = session;
            session.DecisionStep = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (o.Accepted) Record();
        }

        public void OnTick() { }

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
