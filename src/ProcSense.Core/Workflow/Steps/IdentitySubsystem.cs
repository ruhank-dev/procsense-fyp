using System.Globalization;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Walkthrough Step 1: identity and allergy check. Also guards every verb tagged requires-identity (rule L1-02),
    /// which works because this subsystem is first in the pipeline.
    /// </summary>
    public sealed class IdentitySubsystem : ISubsystem
    {
        public const string IdentityNotConfirmed = "IDENTITY_NOT_CONFIRMED";
        public const string IdentityCheckIncomplete = "IDENTITY_CHECK_INCOMPLETE";
        public const string Mismatch = "ST1_MISMATCH";
        public const string WristbandMissing = "WRISTBAND_MISSING";
        public const string VerifyIdentityFirst = "VERIFY_IDENTITY_FIRST";
        public const string SpokenAnswerUnclear = "SPOKEN_ANSWER_UNCLEAR";

        // Flags this step sets on the patient (read later by scoring and events).
        public const string Introduced = "introduced";
        public const string IdentityAskedOpen = "identity_asked_open";
        public const string IdentityAskedClosed = "identity_asked_closed";
        public const string WrongBloodRisk = "risk.wrong_blood";
        public const string MismatchReported = "mismatch_reported";
        public const string UnnecessaryMismatchReport = "unnecessary_mismatch_report";
        public const string AllergyReactionAsked = "allergy_reaction_asked";

        private readonly ContentPackage _content;
        private Session _s = null!;

        public IdentitySubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Clinical;

        private ClinicalLayer Clinical => _s.Patient.Clinical;

        public void Init(Session session)
        {
            _s = session;
            foreach (var v in new[]
            {
                "introduce_self", "ask_identity", "read_name_to_patient", "read_wristband", "open_notes", "confirm_identity",
                "report_mismatch", "request_wristband", "ask_allergies", "ask_allergy_reaction", "ask_reason_for_admission", "ask_responsible_adult",
            })
                session.Verbs.Register(v, requiresPatient: v != "open_notes" && v != "confirm_identity" && v != "report_mismatch");   // the notes are the folder, and confirming or reporting is the trainee's own judgement
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (_s.Verbs.RequiresIdentity(a.Verb) && !Clinical.IdentityVerified)
            {
                o.Reject(IdentityNotConfirmed);
                return;
            }

            switch (a.Verb)
            {
                case "introduce_self": IntroduceSelf(o); break;
                case "ask_identity": Ask(PatientAnswers.Identity, o); Clinical.SetFlag(IdentityAskedOpen); break;
                case "read_name_to_patient": ReadNameToPatient(o); break;
                case "read_wristband": ReadWristband(o); break;
                case "open_notes": Clinical.NotesOpened = true; o.Effects.Add("notes_opened"); break;
                case "confirm_identity": ConfirmIdentity(o); break;
                case "report_mismatch": ReportMismatch(o); break;
                case "request_wristband": RequestWristband(o); break;
                case "ask_allergies": Ask(PatientAnswers.Allergies, o); Clinical.AllergiesKnown = true; break;
                case "ask_allergy_reaction": Ask(PatientAnswers.AllergyReaction, o); Clinical.SetFlag(AllergyReactionAsked); break;
                case "ask_reason_for_admission": Ask(PatientAnswers.ReasonForAdmission, o); break;
                case "ask_responsible_adult": Ask(PatientAnswers.ResponsibleAdult, o); break;
            }
        }

        public void OnTick() { }

        private void Ask(string topic, ActionOutcome o)
        {
            if (PatientAnswers.For(_s.Case, topic) == null) { o.Effects.Add("nothing_to_describe:" + topic); return; }
            Clinical.Reveal(topic);
            o.Effects.Add("answered:" + topic);
        }

        // L1-03: only the very first act of the session earns the trust/anxiety change.
        private void IntroduceSelf(ActionOutcome o)
        {
            bool first = _s.Log.Count == 0 && !Clinical.HasFlag(Introduced);
            Clinical.SetFlag(Introduced);
            if (!first) { o.Effects.Add("introduced_not_first"); return; }
            var b = _content.IntroduceSelf;
            _s.Patient.Behavioural.Trust += (int)b.Trust;
            _s.Patient.Behavioural.Anxiety += (int)b.Anxiety;
            o.Effects.Add("trust" + Signed(b.Trust));
            o.Effects.Add("anxiety" + Signed(b.Anxiety));
        }

        // L1-WT1: a closed question proves nothing, the patient just agrees, and wrong-blood risk is armed.
        private void ReadNameToPatient(ActionOutcome o)
        {
            Clinical.SetFlag(IdentityAskedClosed);
            Clinical.SetFlag(WrongBloodRisk);
            o.Effects.Add("patient_agrees");
            o.Effects.Add("armed:" + WrongBloodRisk);
        }

        private void ReadWristband(ActionOutcome o)
        {
            if (Clinical.CurrentWristband == null) { o.Reject(WristbandMissing); return; }
            Clinical.WristbandRead = true;
            o.Effects.Add("wristband_read");
        }

        // L1-01: needs the open question, the wristband read and the notes opened; any mismatch stops it.
        private void ConfirmIdentity(ActionOutcome o)
        {
            if (Clinical.CurrentWristband == null) { o.Reject(WristbandMissing); return; }
            if (!Clinical.HasFlag(IdentityAskedOpen) || !Clinical.WristbandRead || !Clinical.NotesOpened)
            {
                o.Reject(IdentityCheckIncomplete);
                return;
            }
            var problems = IdentityCheck.Mismatches(_s.Case, Clinical.CurrentWristband);
            if (problems.Count > 0)
            {
                o.Reject(Mismatch);
                foreach (var p in problems) o.Effects.Add(p);
                return;
            }
            var unclear = IdentityCheck.Unclear(_s.Case);
            if (unclear.Count > 0)
            {
                o.Reject(SpokenAnswerUnclear);
                foreach (var u in unclear) o.Effects.Add(u);
                return;
            }
            Clinical.IdentityVerified = true;
            o.Effects.Add("identity_verified");
        }

        // L1-04: reporting a real mismatch gets it fixed by the nurse at once; reporting when everything matches is an unnecessary delay.
        // The nurse can only replace a wrong wristband; a problem in the notes or in what the patient says goes to admissions (not built here).
        private void ReportMismatch(ActionOutcome o)
        {
            var band = Clinical.CurrentWristband;
            if (band == null) { o.Effects.Add("no_wristband_use_request_wristband"); return; }
            var all = IdentityCheck.Mismatches(_s.Case, band);
            if (all.Count == 0)
            {
                Clinical.SetFlag(UnnecessaryMismatchReport);
                o.Effects.Add("no_mismatch_found");
                return;
            }
            Clinical.SetFlag(MismatchReported);
            o.Effects.Add("mismatch_reported");
            if (all.Count != IdentityCheck.Mismatches(_s.Case, null).Count) // the wristband is part of the problem
                WristbandResolution.ApplyCorrectBand(_s, o, wasMissing: false);
            foreach (var left in IdentityCheck.Mismatches(_s.Case, Clinical.CurrentWristband))
                o.Effects.Add("escalate_to_admissions:" + left);
        }

        // Missing band: the nurse applies one once identity has been verified another way (open question + notes agree with what the patient says).
        private void RequestWristband(ActionOutcome o)
        {
            if (Clinical.CurrentWristband != null) { o.Effects.Add("nothing_to_resolve"); return; }
            bool verifiedAnotherWay = Clinical.HasFlag(IdentityAskedOpen) && Clinical.NotesOpened && IdentityCheck.Mismatches(_s.Case, null).Count == 0 && IdentityCheck.Unclear(_s.Case).Count == 0;
            if (!verifiedAnotherWay) { o.Reject(VerifyIdentityFirst); return; }
            WristbandResolution.ApplyCorrectBand(_s, o, wasMissing: true);
        }

        private static string Signed(long n) => (n >= 0 ? "+" : "") + n.ToString(CultureInfo.InvariantCulture);
    }
}
