using ProcSense.Core.Content;

namespace ProcSense.Core.Flow
{
    /// <summary>
    /// P3-SCN-05 (pure): the facts the trainee may see on the briefing card before the case starts, nothing else: who the patient is as the admission says (name, age, sex, setting) and the reason
    /// the patient gives for the admission (the case's own answer to "why are you here"). No observation, result, history item, risk or problem is read, so nothing the trainee has to find out is shown.
    /// </summary>
    public sealed record BriefingFacts(string CaseId, string Name, long Age, string Sex, string Setting, string Reason)
    {
        public static BriefingFacts From(CaseFile file) => new BriefingFacts(file.Id, file.Identity.FullName, file.Identity.Age, file.Identity.Sex, file.Presentation.Setting, file.ReasonAnswer);
    }
}
