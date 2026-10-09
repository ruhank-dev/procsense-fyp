using System.Collections.Generic;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Observation
{
    public sealed record DisclosedFact(string Topic, string Answer);

    /// <summary>One value on the monitor or chart, with the clock time it was read.</summary>
    public sealed record ObservedVital(string Name, string Value, string At);

    /// <summary>
    /// Read-only snapshot of what the trainee can perceive right now. Unity and AI read only this, never PatientState,
    /// so undisclosed facts cannot leak. Vitals appear only for attached sensors and readings the trainee has taken.
    /// </summary>
    public sealed record ObservableState(
        string ClockTime, string Location, bool IdentityConfirmed, bool AllergiesKnown,
        Wristband? Wristband, NotesIdentifiers? Notes, IReadOnlyList<DisclosedFact> DisclosedFacts, IReadOnlyList<ObservedVital> Vitals, string Nurse, bool AllergyBand, string LastRejection)
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
            sb.Append("],\"vitals\":[");
            for (int i = 0; i < Vitals.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":"); JsonText.Quote(sb, Vitals[i].Name);
                sb.Append(",\"value\":"); JsonText.Quote(sb, Vitals[i].Value);
                sb.Append(",\"at\":"); JsonText.Quote(sb, Vitals[i].At);
                sb.Append('}');
            }
            sb.Append("],\"nurse\":"); JsonText.Quote(sb, Nurse);
            sb.Append(",\"allergy_band\":").Append(AllergyBand ? "true" : "false");
            sb.Append(",\"last_rejection\":"); JsonText.Quote(sb, LastRejection);
            return sb.Append('}').ToString();
        }

        public string Hash() => JsonText.Sha256Hex(ToJson());
    }

    public static class ObservableBuilder
    {
        private static readonly string[] VitalOrder =
            { "spo2", "oxygen", "heart_rate", "systolic_bp", "diastolic_bp", "respiratory_rate", "temperature_c", "acvpu", "pain_score" };

        // Only what the trainee has gathered: live values while the SpO2 probe is on, and the readings taken so far.
        private static List<ObservedVital> BuildVitals(Session s)
        {
            var p = s.Patient.Physiological;
            var v = p.Vitals;
            string now = At(s, s.Clock.Steps);
            var live = new Dictionary<string, string>();
            if (p.IsAttached(Workflow.Steps.ObservationsSubsystem.Spo2Probe))
            {
                live["spo2"] = Num(v.Spo2);
                live["oxygen"] = v.Oxygen;
                live["heart_rate"] = Num(v.HeartRate);
            }
            var list = new List<ObservedVital>();
            foreach (var name in VitalOrder)
            {
                if (live.TryGetValue(name, out var value)) list.Add(new ObservedVital(name, value, now));
                else if (p.Measured.TryGetValue(name, out var m)) list.Add(new ObservedVital(name, m.Value, At(s, m.AtStep)));
            }
            return list;
        }

        private static string At(Session s, long step) => Hms(s.Clock.Start + System.TimeSpan.FromSeconds(step * Time.SimClock.Step));

        /// <summary>HH:MM:SS, with one decimal only when the time falls on a half second ("08:00:44.5").</summary>
        public static string Hms(System.TimeSpan t) =>
            t.Milliseconds == 0 ? t.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture) : t.ToString(@"hh\:mm\:ss\.f", System.Globalization.CultureInfo.InvariantCulture);

        public static string Num(double x) => x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        public static ObservableState Build(Session s)
        {
            var c = s.Patient.Clinical;
            var facts = new List<DisclosedFact>();
            foreach (var topic in c.RevealOrder) // the clerking note: oldest line first
            {
                string? answer = PatientAnswers.For(s.Case, topic);
                if (answer == null)
                    foreach (var f in s.Patient.History)
                        if (f.Topic == topic) { answer = f.Answer; break; }
                if (answer != null) facts.Add(new DisclosedFact(topic, answer));
            }

            return new ObservableState(
                Hms(s.Clock.ClockTime),
                c.Location, c.IdentityVerified, c.AllergiesKnown,
                c.WristbandRead ? c.CurrentWristband : null,
                c.NotesOpened ? s.Patient.Profile.Notes : null,
                facts, BuildVitals(s), c.NurseState, c.AllergyBand, s.LastRejection);
        }
    }
}
