using System;
using System.Globalization;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>One step of the nurse's visit, published on the EventBus so views can show her walking in and replacing the band.</summary>
    public readonly struct NurseTaskChanged
    {
        public NurseTaskChanged(string state, string location) { State = state; Location = location; }

        public string State { get; }

        /// <summary>Where the nurse is at this step: outside (in the corridor) when she has been called, then with the patient.</summary>
        public string Location { get; }
    }

    /// <summary>
    /// The nurse fixes a wrong band or applies a missing one (rule L1-04, test T01-4). Owner decision 09 Oct 2026: it happens at once, so the
    /// simulation clock does not advance; the steps are published in order and the views pace the animation.
    /// </summary>
    public static class WristbandResolution
    {
        public const string Called = "CALLED";
        public const string AtBedside = "AT_BEDSIDE";
        public const string Replacing = "REPLACING";
        public const string Done = "DONE";

        /// <summary>
        /// The band text for this patient, from the authoritative identity.
        /// shortcut: the format (upper-case name, dd/MM/yyyy, "MR " prefix) is copied from the walkthrough's example band; replace it when the hospital's band format is decided.
        /// </summary>
        public static Wristband CorrectBand(Identity id)
        {
            DateTime.TryParseExact(id.Dob, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var born);
            return new Wristband(id.FullName.ToUpperInvariant(), born.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), "MR " + id.Mrn);
        }

        /// <summary>Nurse puts the correct band on. The trainee must read it again before identity can be confirmed.</summary>
        public static void ApplyCorrectBand(Session s, ActionOutcome o, bool wasMissing)
        {
            var clinical = s.Patient.Clinical;
            foreach (var step in new[] { Called, AtBedside, Replacing, Done })
            {
                clinical.NurseLocation = step == Called ? "corridor" : clinical.Location;
                s.Events.Publish(new NurseTaskChanged(step, clinical.NurseLocation));
                o.Effects.Add("nurse:" + step);
            }
            clinical.CurrentWristband = CorrectBand(s.Case.Identity);
            clinical.WristbandRead = false;
            clinical.NurseState = Done;
            o.Effects.Add(wasMissing ? "wristband_applied" : "wristband_replaced");
            if (wasMissing && s.Case.Allergies.Count > 0)
            {
                clinical.AllergyBand = true; // a reported allergy also gets its band; the trainee still has to ask about the reaction
                o.Effects.Add("allergy_band_applied");
            }
        }
    }
}
