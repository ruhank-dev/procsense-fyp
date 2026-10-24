using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Content
{
    /// <summary>
    /// Builds the Steps 1 to 3 card a session starts from out of a playable case file (DT-070), so any of the 53 case files can be played, not only the ones that also have a hand-made card.
    /// The patient's identity, wristband, allergies, spoken answers, timing, the history of the base case (facts that belong to a variant are left out) and the first observation all come from the file; nothing is invented.
    /// A case file may say who looks after the patient at home (responsible_adult, DT-178); when it does not, that answer is "not stated".
    /// </summary>
    public static class CaseFileCard
    {
        public static CaseCard From(CaseFile f, IReadOnlyCollection<string> askableTopics)
        {
            var first = f.Observations.FirstOrDefault(o => o.Variant == null && o.Condition == "initial") ?? f.Observations.First(o => o.Variant == null);
            var history = f.History.Where(h => h.Variant == null && askableTopics.Contains(h.Topic)).Select(h => new HistoryFact(h.Topic, h.Answer, "narration", h.Volunteered, null)).ToList();
            return new CaseCard(
                f.Id, f.WalkthroughLabel, f.Scenario,
                new Identity(f.Identity.Title, f.Identity.FullName, f.Identity.Age, f.Identity.Occupation, f.Identity.Dob, f.Identity.Mrn),
                f.SpokenIdentityAnswer, f.Wristband, new NotesIdentifiers(f.Identity.FullName, f.Identity.Dob, f.Identity.Mrn), f.Allergies, f.AllergyAnswer, f.ReasonAnswer,
                f.ResponsibleAdult ?? new ResponsibleAdult("not stated", false, false, "Not stated in this case.", "narration"),
                new Timing(f.Presentation.TimeNow, f.Presentation.SurgeryTime ?? f.Presentation.TimeNow), history,
                new Vitals(first.HeartRate, first.HeartRhythm, first.SystolicBp, first.DiastolicBp, first.RespiratoryRate, first.Spo2, first.Oxygen, first.TemperatureC, first.Acvpu, first.PainScore),
                new Dictionary<string, string>());
        }
    }
}
