using System;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// The trainee holds one of the patient's own (home) medicines, for example "hold the metformin" (DT-208; the spoken order HOLD_MED releases to this verb).
    /// The medicine must be one the patient has told about: the medicines history must have been asked and the drug named in the patient's answer, so a drug the trainee has not heard of cannot be held.
    /// The decision is recorded as the flag held_medicine:&lt;drug&gt; and an effect; the engine does not score it here (whether a hold was right belongs to the decision rules of the case).
    /// </summary>
    public sealed class HomeMedicineSubsystem : ISubsystem
    {
        public const string Verb = "hold_medicine";
        public const string FlagPrefix = "held_medicine:";
        public const string MissingDrug = "MISSING_DRUG";
        public const string MedicinesNotAsked = "MEDICINES_NOT_ASKED";
        public const string NotOnList = "NOT_A_HOME_MEDICINE";
        public const string AlreadyHeld = "ALREADY_HELD";

        private Session _s = null!;

        public int Order => PipelineOrder.Clinical + 1;

        public void Init(Session session)
        {
            _s = session;
            session.Verbs.Register(Verb, requiresIdentity: true, requiresPatient: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (a.Verb != Verb) return;
            if (!a.Params.TryGetValue("drug", out var raw) || string.IsNullOrWhiteSpace(raw)) { o.Reject(MissingDrug); return; }
            string drug = raw.Trim().ToLowerInvariant();
            var clinical = _s.Patient.Clinical;
            if (!clinical.IsRevealed("medicines")) { o.Reject(MedicinesNotAsked); return; }
            var fact = _s.Patient.History.FirstOrDefault(h => h.Topic == "medicines");
            if (fact == null || !Regex.IsMatch(fact.Answer, @"\b" + Regex.Escape(drug) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) { o.Reject(NotOnList); return; }
            if (clinical.HasFlag(FlagPrefix + drug)) { o.Reject(AlreadyHeld); return; }
            clinical.SetFlag(FlagPrefix + drug);
            o.Effects.Add("medicine_held:" + drug);
        }

        public void OnTick() { }
    }
}
