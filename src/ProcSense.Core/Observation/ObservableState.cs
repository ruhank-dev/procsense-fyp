using System.Collections.Generic;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Observation
{
    public sealed record DisclosedFact(string Topic, string Answer);

    /// <summary>
    /// Read-only snapshot of what the trainee can perceive right now. Unity and AI read only this, never PatientState,
    /// so undisclosed facts cannot leak. Vitals appear here once sensors exist (simulation-workflow step 3).
    /// </summary>
    public sealed record ObservableState(
        string ClockTime, string Location, bool IdentityConfirmed, bool AllergiesKnown,
        Wristband? Wristband, NotesIdentifiers? Notes, IReadOnlyList<DisclosedFact> DisclosedFacts, string LastRejection)
    {
        /// <summary>Canonical JSON (fixed key order) so its hash is the same on every machine.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"clock\":"); JsonText.Quote(sb, ClockTime);
            sb.Append(",\"location\":"); JsonText.Quote(sb, Location);
            sb.Append(",\"identity_confirmed\":").Append(IdentityConfirmed ? "true" : "false");
            sb.Append(",\"allergies_known\":").Append(AllergiesKnown ? "true" : "false");
            sb.Append(",\"wristband\":");
            if (Wristband == null) sb.Append("null");
            else
            {
                sb.Append("{\"name\":"); JsonText.Quote(sb, Wristband.NameText);
                sb.Append(",\"dob\":"); JsonText.Quote(sb, Wristband.DobText);
                sb.Append(",\"mrn\":"); JsonText.Quote(sb, Wristband.MrnText);
                sb.Append('}');
            }
            sb.Append(",\"notes\":");
            if (Notes == null) sb.Append("null");
            else
            {
                sb.Append("{\"name\":"); JsonText.Quote(sb, Notes.FullName);
                sb.Append(",\"dob\":"); JsonText.Quote(sb, Notes.Dob);
                sb.Append(",\"mrn\":"); JsonText.Quote(sb, Notes.Mrn);
                sb.Append('}');
            }
            sb.Append(",\"disclosed\":[");
            for (int i = 0; i < DisclosedFacts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"topic\":"); JsonText.Quote(sb, DisclosedFacts[i].Topic);
                sb.Append(",\"answer\":"); JsonText.Quote(sb, DisclosedFacts[i].Answer);
                sb.Append('}');
            }
            sb.Append("],\"last_rejection\":"); JsonText.Quote(sb, LastRejection);
            return sb.Append('}').ToString();
        }

        public string Hash() => JsonText.Sha256Hex(ToJson());
    }

    public static class ObservableBuilder
    {
        public static ObservableState Build(Session s)
        {
            var c = s.Patient.Clinical;
            var facts = new List<DisclosedFact>();
            foreach (var topic in PatientAnswers.Topics) // fixed order, so output never depends on set ordering
                if (c.IsRevealed(topic) && PatientAnswers.For(s.Case, topic) is string answer) facts.Add(new DisclosedFact(topic, answer));
            foreach (var f in s.Patient.History)
                if (c.IsRevealed(f.Topic)) facts.Add(new DisclosedFact(f.Topic, f.Answer));

            return new ObservableState(
                s.Clock.ClockTime.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
                c.Location, c.IdentityVerified, c.AllergiesKnown,
                c.WristbandRead ? s.Patient.Profile.Wristband : null,
                c.NotesOpened ? s.Patient.Profile.Notes : null,
                facts, s.LastRejection);
        }
    }
}
