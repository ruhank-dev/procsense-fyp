using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Holds the problem registry. Other subsystems call Detect(id) when the trainee finds a problem the case lists (an abnormal number, an unsafe fact); the problem then joins the registry, and a life threat
    /// switches the case to emergency mode (the flag <see cref="EmergencyFlag"/> and a WorkflowEvent for the NPCs and the flow to react to).
    /// </summary>
    public sealed class ProblemsSubsystem : ISubsystem
    {
        public const string EmergencyFlag = "mode.emergency";
        public const string EmergencyEvent = "E-EMERGENCY-MODE";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private CaseFile? _file;
        private Session _s = null!;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public ProblemsSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        public int Order => PipelineOrder.Events - 1;

        public ProblemRegistry Registry { get; private set; } = null!;
        private System.Collections.Generic.List<ProblemDiscovery> _waiting = new System.Collections.Generic.List<ProblemDiscovery>();
        private long _ticks;

        public void Init(Session session)
        {
            _s = session;
            session.ProblemsStep = this;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f.ForVariant(session.Variant) : null);
            Registry = new ProblemRegistry(_content.Problems, session.Case.Scenario);
            string caseId = _file?.TableKey ?? session.Case.Id;
            foreach (var d in _content.Problems.Discovery) if (d.Case == caseId) _waiting.Add(d);
        }

        /// <summary>The trainee has found this problem of the case. Returns it (null when the case has no such problem). A life threat starts emergency mode.</summary>
        public Problem? Detect(string problemId)
        {
            var listed = _file?.Problems.FirstOrDefault(p => p.Id == problemId);
            if (listed == null || !AppliesToVariant(listed)) return null;
            bool wasEmergency = Registry.Emergency;
            var problem = Registry.Register(listed.Id, listed.Type, listed.Label, (int)listed.Rank);
            if (!wasEmergency && Registry.Emergency)
            {
                _s.Patient.Clinical.SetFlag(EmergencyFlag);
                _s.Events.Publish(new WorkflowEvent(EmergencyEvent, problem.Id));
            }
            return problem;
        }

        /// <summary>A problem the engine found by itself (for example a treatment clash), not one the case lists.</summary>
        public Problem AddProblem(string id, string type, string label) => Registry.Register(id, type, label);

        /// <summary>The trainee has dealt with the problem. Emergency mode ends when no life threat is left.</summary>
        public bool Resolve(string problemId)
        {
            bool changed = Registry.Resolve(problemId);
            if (changed && !Registry.Emergency) _s.Patient.Clinical.ClearFlag(EmergencyFlag);
            return changed;
        }

        // a problem tied to a variant exists only there; "base" belongs to the case when no variant of it is being played (the session's variant, or a variant flag set by a test)
        private bool AppliesToVariant(CaseFileProblem p)
        {
            if (p.Variant == null) return true;
            var clinical = _s.Patient.Clinical;
            if (p.Variant == "base") return _file == null || !_file.VariantEndStates.Keys.Any(v => clinical.HasFlag(DecisionSubsystem.VariantPrefix + v));
            return clinical.HasFlag(DecisionSubsystem.VariantPrefix + p.Variant);
        }

        public void OnAction(SimAction action, ActionOutcome outcome)
        {
            if (_waiting.Count > 0 && outcome.Accepted) Discover();
        }

        // the sepsis tier moves with time, so the waiting rules are also asked every ten seconds of play
        public void OnTick()
        {
            if (_waiting.Count > 0 && ++_ticks % 20 == 0) Discover();
        }

        private void Discover()
        {
            var state = RuleState.Build(_s);
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                if (!_waiting[i].Trigger.Test(state)) continue;
                var found = Detect(_waiting[i].Problem);
                _waiting.RemoveAt(i);
                if (found != null) state = RuleState.Build(_s);
            }
        }
    }
}
