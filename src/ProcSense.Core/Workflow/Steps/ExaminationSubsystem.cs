using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Walkthrough Step 4: examining the patient. A finding stays hidden until the trainee examines that region (or makes that airway check); nothing is shown that the case
    /// does not author. Examining without asking permission, or pressing the painful region before any other, moves the patient's trust (and pain) and is kept as evidence for the debrief.
    /// The verbs need a confirmed identity, like the history questions (the identity gate enforces it because they are registered with requiresIdentity).
    /// </summary>
    public sealed class ExaminationSubsystem : ISubsystem
    {
        public const string AskPermissionVerb = "ask_exam_permission";
        public const string OfferChaperoneVerb = "offer_chaperone";
        public const string ExamineVerb = "examine";
        public const string MurphyVerb = "murphy_test";
        public const string AirwayVerb = "airway_check";

        public const string MissingRegion = "MISSING_REGION";
        public const string RegionNotInCase = "REGION_NOT_IN_CASE";
        public const string MurphyScenarioOnly = "MURPHY_S1_ONLY";
        public const string MissingItem = "MISSING_ITEM";
        public const string UnknownAirwayItem = "UNKNOWN_AIRWAY_ITEM";
        public const string ItemNotInCase = "ITEM_NOT_IN_CASE";

        /// <summary>The four airway checks of the walkthrough's airway checklist.</summary>
        public static readonly string[] AirwayItems = { "mouth_opening", "mallampati", "neck", "thyromental" };

        public const string PermissionAsked = "exam.permission_asked";
        public const string ChaperoneOffered = "exam.chaperone_offered";
        public const string Started = "exam.started";
        public const string NoPermission = "exam.no_permission";
        public const string PainfulFirst = "exam.painful_first";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private CaseFile? _file;
        private Session _s = null!;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public ExaminationSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        public int Order => PipelineOrder.Clinical;

        private ClinicalLayer Clinical => _s.Patient.Clinical;

        public void Init(Session session)
        {
            _s = session;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f.ForVariant(session.Variant) : null);
            foreach (var verb in new[] { AskPermissionVerb, OfferChaperoneVerb, ExamineVerb, MurphyVerb, AirwayVerb }) session.Verbs.Register(verb, requiresIdentity: true, requiresPatient: true);
            if (_file != null)
            {
                Clinical.ExamTotal = _file.Examination.Count(e => !IsAirway(e));
                Clinical.AirwayTotal = _file.Examination.Count(IsAirway);
            }
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case AskPermissionVerb: Clinical.SetFlag(PermissionAsked); o.Effects.Add("permission_asked"); break;
                case OfferChaperoneVerb: Clinical.SetFlag(ChaperoneOffered); o.Effects.Add("chaperone_offered"); break;   // whether one is required is the hospital's rule, so it is recorded, not enforced
                case ExamineVerb: Examine(a, o); break;
                case MurphyVerb: Murphy(o); break;
                case AirwayVerb: Airway(a, o); break;
            }
        }

        public void OnTick() { }

        private static bool IsAirway(CaseFileExamination e) => e.DiscloseOn.Any(d => d.StartsWith("airway_check:", StringComparison.Ordinal));

        private CaseFileExamination? Find(string key) => _file?.Examination.FirstOrDefault(e => e.DiscloseOn.Contains(key));

        private void Examine(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("region", out var region) || region.Length == 0) { o.Reject(MissingRegion); return; }
            var entry = Find("examine:" + region);
            if (entry == null) { o.Reject(RegionNotInCase); return; }
            Reveal(entry, region, o);
        }

        private void Murphy(ActionOutcome o)
        {
            if (_s.Case.Scenario != "S1") { o.Reject(MurphyScenarioOnly); return; }
            var entry = Find("examine:murphy");
            if (entry == null) { o.Reject(RegionNotInCase); return; }
            Reveal(entry, "murphy", o);
        }

        private void Airway(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("item", out var item) || item.Length == 0) { o.Reject(MissingItem); return; }
            if (!AirwayItems.Contains(item)) { o.Reject(UnknownAirwayItem); return; }
            var entry = Find("airway_check:" + item);
            if (entry == null) { o.Reject(ItemNotInCase); return; }
            CheckPermission(o);
            Clinical.SetFlag(Started);
            if (Clinical.AddFinding(new ExamFinding(entry.Id, RuleState.AirwayRegion + item, entry.Finding))) o.Effects.Add("finding:" + entry.Id);
            else o.Effects.Add("already_known:" + entry.Id);
            Clinical.SetAirway(item, entry.Finding);
            o.Effects.Add("airway:" + item);
        }

        // Shared by every examination: permission first, then the order (away from the pain first), then the finding itself.
        private void Reveal(CaseFileExamination entry, string region, ActionOutcome o)
        {
            CheckPermission(o);
            if (!Clinical.HasFlag(Started))
            {
                Clinical.SetFlag(Started);
                if (entry.Painful && HasAnotherRegion(entry)) PainfulRegionFirst(o);
            }
            if (Clinical.AddFinding(new ExamFinding(entry.Id, region, entry.Finding)))
            {
                o.Effects.Add("finding:" + entry.Id);
                if (entry.Flags != null) foreach (var f in entry.Flags) Clinical.SetFlag(f);
                if (entry.Flags != null && entry.Flags.Contains("finding.murphy_positive") && _content.Exam.MurphyReaction.Length > 0) o.Effects.Add("reaction:" + _content.Exam.MurphyReaction);
            }
            else o.Effects.Add("already_known:" + entry.Id);
        }

        private bool HasAnotherRegion(CaseFileExamination entry) =>
            _file!.Examination.Any(e => e.Id != entry.Id && !e.Painful && !IsAirway(e));

        // E4-A: the patient flinches and trust falls, once, until permission is asked.
        private void CheckPermission(ActionOutcome o)
        {
            if (Clinical.HasFlag(PermissionAsked) || Clinical.HasFlag(NoPermission)) return;
            Clinical.SetFlag(NoPermission);
            _s.Patient.Behavioural.Trust += (int)_content.Exam.NoPermissionTrust;
            o.Effects.Add("professionalism:exam_without_permission");
            if (_content.Exam.NoPermissionReaction.Length > 0) o.Effects.Add("reaction:" + _content.Exam.NoPermissionReaction);
        }

        // E4-B: pain goes up by the authored amount (never past 10) and trust falls.
        private void PainfulRegionFirst(ActionOutcome o)
        {
            Clinical.SetFlag(PainfulFirst);
            var phys = _s.Patient.Physiological;
            phys.Vitals = phys.Vitals with { PainScore = Math.Min(10, phys.Vitals.PainScore + _content.Exam.PainfulPain) };
            _s.Patient.Behavioural.Trust += (int)_content.Exam.PainfulTrust;
            o.Effects.Add("professionalism:painful_region_first");
            if (_content.Exam.PainfulReaction.Length > 0) o.Effects.Add("reaction:" + _content.Exam.PainfulReaction);
        }
    }
}
