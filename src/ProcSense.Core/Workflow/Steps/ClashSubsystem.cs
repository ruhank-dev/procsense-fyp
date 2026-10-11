using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>Published when a request meets a clash: the clash id, what was requested and the tags that raised it.</summary>
    public sealed record ClashRaised(string Clash, string Request, IReadOnlyList<string> Tags);

    /// <summary>
    /// The patient's factors (kidney injury, pregnancy, a blood thinner and so on) are read from the case once; they become the session flags condition.&lt;tag&gt; that the treatment restrictions of the medicine workflow
    /// read. Every request for a medicine, fluid, oxygen or a test is then checked against the clash table (14B.3). A clash raised is recorded for the debrief, published, and becomes a safety-blocker problem for the
    /// senior or anaesthetist to decide: the engine never decides it, and the trainee has to spot it. The medicine plans for the medicines the case lists are kept for the Step 8 decisions.
    /// </summary>
    public sealed class ClashSubsystem : ISubsystem
    {
        public const string ConditionPrefix = "condition.";

        private static readonly string[] MedicineVerbs = { "request_medication", "request_fluids", "request_oxygen", "request_iv_access" };

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<string> _raised = new HashSet<string>(StringComparer.Ordinal);
        private CaseFile? _file;
        private Session _s = null!;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public ClashSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        public int Order => PipelineOrder.Treatments + 1;

        /// <summary>Clashes raised, with sim steps. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        /// <summary>The factors the patient has (sorted).</summary>
        public IReadOnlyList<string> Factors { get; private set; } = Array.Empty<string>();

        /// <summary>The perioperative plans for the medicines the case lists.</summary>
        public IReadOnlyList<MedicinePlan> Plans { get; private set; } = Array.Empty<MedicinePlan>();

        private ClashTables Tables => _content.Clashes;

        public void Init(Session session)
        {
            _s = session;
            session.ClashStep = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
            if (_file == null) return;
            var factors = PatientFactors.From(_file, Tables);
            Factors = factors.ToList();
            Plans = MedicinePlans.For(_file, Tables);
            foreach (var tag in factors) session.Patient.Clinical.SetFlag(ConditionPrefix + tag);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (!o.Accepted || _file == null) return;
            string? request = null;
            if (MedicineVerbs.Contains(a.Verb)) request = _s.Patient.Clinical.Orders.LastOrDefault()?.Treatment;
            else if (a.Verb == "order_investigation" && a.Params.TryGetValue("test", out var test)) request = test;
            if (request == null) return;
            var tags = ClashChecker.TagsFor(Tables, request, Factors, Context());
            foreach (var hit in ClashChecker.Check(Tables, tags))
            {
                if (!_raised.Add(hit.Clash.Id)) continue;
                o.Effects.Add("clash:" + hit.Clash.Id);
                _history.Add(new GraphEvent("clash:" + hit.Clash.Id, request, _s.Clock.Steps, Array.Empty<string>()));
                _s.ProblemsStep?.AddProblem("clash." + hit.Clash.Id, hit.Clash.ProblemType, hit.Clash.Clash);
                _s.Events.Publish(new ClashRaised(hit.Clash.Id, request, hit.Tags));
            }
        }

        public void OnTick() { }

        private IEnumerable<string> Context()
        {
            if (_s.Case.Scenario == "S2") yield return "urgent_surgery";
            var tier = _s.Sepsis?.Tier;
            if (tier == "moderate" || tier == "high") yield return "sepsis";
        }
    }
}
