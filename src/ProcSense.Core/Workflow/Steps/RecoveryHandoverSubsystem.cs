using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// The handover to the recovery nurse after the WHO Sign Out (p1-theatre-extension-s03, walkthrough C.24; a ProcSense educational extension). It is the Step 12 handover again with other items
    /// (content/handover/recovery_required.json): the trainee fills each SBAR section from a menu of facts they already know (the Sign Out flags tell what they know: the procedure, the specimen, the counts, the
    /// recovery briefing), and finish_recovery_handover judges it against what recovery needs. A missing item is scored by its severity (allergies are critical; the antibiotic plan, analgesia plan, counts and
    /// concerns are major); in practice the recovery nurse asks for each missing item, and leaving the same one out again is a further major item. It can only start once the Sign Out is complete. The recovery bay is
    /// the patient bay layout reused (phase review P6), so the Core does not move the trainee: the scene is the VR app's.
    /// </summary>
    public sealed class RecoveryHandoverSubsystem : ISubsystem
    {
        public const string HandoverVerb = "recovery_handover";
        public const string FinishVerb = "finish_recovery_handover";

        public const string SignOutNotComplete = "SIGN_OUT_NOT_COMPLETE";
        public const string MissingSection = "MISSING_SECTION";
        public const string UnknownSection = "UNKNOWN_SECTION";
        public const string MissingItems = "MISSING_ITEMS";
        public const string UnknownItem = "UNKNOWN_ITEM";
        public const string WrongSection = "WRONG_SECTION";
        public const string UnknownFact = "UNKNOWN_FACT";
        public const string NothingHandedOver = "NOTHING_HANDED_OVER";
        public const string AlreadyHandedOver = "ALREADY_HANDED_OVER";

        private readonly ContentPackage _content;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<string> _given = new List<string>();
        private readonly List<HandoverResult> _results = new List<HandoverResult>();
        private readonly HashSet<string> _asked = new HashSet<string>(StringComparer.Ordinal);
        private Session _s = null!;

        public RecoveryHandoverSubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Investigations + 8;

        /// <summary>Recovery handover sections, finishes and omissions with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        public IReadOnlyList<string> Given => _given;

        public IReadOnlyList<HandoverResult> Results => _results;

        /// <summary>True once the recovery handover has been finished complete.</summary>
        public bool Complete => _results.Count > 0 && _results[^1].Complete;

        private HandoverTables Tables => _content.RecoveryHandover;

        public void Init(Session session)
        {
            _s = session;
            session.RecoveryHandoverStep = this;
            if (Tables.Items.Count == 0) return;                          // a package without the extension has no recovery handover
            session.Verbs.Register(HandoverVerb, requiresIdentity: true);
            session.Verbs.Register(FinishVerb, requiresIdentity: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case HandoverVerb: Hand(a, o); break;
                case FinishVerb: Finish(o); break;
            }
        }

        public void OnTick() { }

        /// <summary>The items this scenario requires.</summary>
        public IReadOnlyList<HandoverItem> Required() => Tables.Items.Where(i => i.RequiredIn.Contains(_s.Case.Scenario)).ToList();

        private bool SignedOut => _s.Safety != null && _s.Safety.PhaseState("sign_out") == "COMPLETE";

        private void Hand(SimAction a, ActionOutcome o)
        {
            if (!SignedOut) { o.Reject(SignOutNotComplete); return; }
            if (Complete) { o.Reject(AlreadyHandedOver); return; }
            if (!a.Params.TryGetValue("section", out var section) || section.Length == 0) { o.Reject(MissingSection); return; }
            if (!Tables.Sections.Contains(section)) { o.Reject(UnknownSection); return; }
            if (!a.Params.TryGetValue("items", out var list) || list.Trim().Length == 0) { o.Reject(MissingItems); return; }
            var ids = list.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var data = RuleState.Build(_s);
            foreach (var id in ids)
            {
                var item = Tables.Find(id);
                if (item == null) { o.Reject(UnknownItem); return; }
                if (item.Section != section) { o.Reject(WrongSection); return; }
                if (!item.Known.Test(data)) { o.Effects.Add("not_known:" + id); o.Reject(UnknownFact); return; }   // you cannot hand over what you do not know yet
            }
            foreach (var id in ids) if (!_given.Contains(id)) _given.Add(id);
            o.Effects.Add("handed_over:" + section + ":" + string.Join(",", ids));
            _history.Add(new GraphEvent("recovery_handover:" + section, string.Join(",", ids), _s.Clock.Steps, Array.Empty<string>()));
        }

        // the recovery nurse judges what was given against what recovery needs
        private void Finish(ActionOutcome o)
        {
            if (!SignedOut) { o.Reject(SignOutNotComplete); return; }
            if (Complete) { o.Reject(AlreadyHandedOver); return; }
            if (_given.Count == 0) { o.Reject(NothingHandedOver); return; }
            var missing = Required().Where(i => !_given.Contains(i.Id)).ToList();
            bool practice = _s.Mode == SessionMode.Practice;
            var items = new List<string>();
            foreach (var m in missing)
            {
                items.Add(Tables.Scored[m.Severity]);
                if (_asked.Contains(m.Id)) items.Add(Tables.Scored["repeat"]);          // asked for it last time and still left it out
                if (practice) { o.Effects.Add("recovery_nurse_asks:" + m.Id); _asked.Add(m.Id); }
                o.Effects.Add("missing:" + m.Id);
            }
            bool complete = missing.Count == 0;
            if (complete) items.Add(Tables.Scored["complete"]);
            _results.Add(new HandoverResult(_s.Clock.Steps, _given.ToList(), missing.Select(m => m.Id).ToList(), complete));
            _history.Add(new GraphEvent(complete ? "recovery_handover_complete" : "recovery_handover_incomplete", string.Join(",", missing.Select(m => m.Id)), _s.Clock.Steps, items));
            if (complete) o.Effects.Add("recovery_handover_complete");
        }
    }
}
