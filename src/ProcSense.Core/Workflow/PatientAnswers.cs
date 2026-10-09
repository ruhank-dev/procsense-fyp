using System.Collections.Generic;
using ProcSense.Core.Content;

namespace ProcSense.Core.Workflow
{
    /// <summary>What the patient says for each conversation topic outside the 12 history topics. The words come from the case file.</summary>
    public static class PatientAnswers
    {
        public const string Identity = "identity";
        public const string Allergies = "allergies";
        public const string AllergyReaction = "allergy_reaction";
        public const string ReasonForAdmission = "reason_for_admission";
        public const string ResponsibleAdult = "responsible_adult";

        /// <summary>Topics in the order they are shown; history topics follow in case order.</summary>
        public static readonly string[] Topics = { Identity, Allergies, AllergyReaction, ReasonForAdmission, ResponsibleAdult };

        /// <summary>The patient's answer, or null when the case has nothing to say (e.g. no reaction to describe when there are no allergies).</summary>
        public static string? For(CaseCard c, string topic)
        {
            switch (topic)
            {
                case Identity: return c.SpokenIdentityAnswer;
                case Allergies: return c.AllergyAnswer;
                case AllergyReaction:
                    if (c.Allergies.Count == 0) return null;
                    var parts = new List<string>();
                    foreach (var a in c.Allergies) parts.Add(a.Substance + ": " + a.Reaction);
                    return string.Join("; ", parts);
                case ReasonForAdmission: return c.ReasonAnswer;
                case ResponsibleAdult: return c.ResponsibleAdult.Text;
                default: return null;
            }
        }
    }
}
