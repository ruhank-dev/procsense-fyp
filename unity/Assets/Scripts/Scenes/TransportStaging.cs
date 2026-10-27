using System.Collections.Generic;
using ProcSense.Core.Observation;

namespace ProcSense.VR.Scenes
{
    /// <summary>What the scene shows for one moment of a transfer: where the patient is (or is heading), whether they can be seen, how they travel, and whether the porter is with them.</summary>
    public sealed class Staging
    {
        /// <summary>The anchor the patient stands at or is heading for: a place id ("patient_bay"), a door ("door:ct") or the hidden space behind it ("inside:ct").</summary>
        public string Anchor = "";
        public bool Moving, Visible = true, Porter, TraineeFollows;
        /// <summary>walk, wheelchair or trolley ("" when not travelling).</summary>
        public string Mode = "";
    }

    /// <summary>
    /// Turns Core's transport state (p0-vr-platform-s06) into what the scene should show. Core decides every time and every place (T-B4): the porter is fetched while REQUESTED, DELAYED and PREPARING; in
    /// IN_TRANSIT the patient travels the corridor towards the destination's door and waits there if Core has not said ARRIVED; a room the simulation does not build (the scanners and the X-ray room) is entered
    /// through its door and the patient is hidden inside until RETURNING; BACK puts the patient where they started. The trainee goes too only when Core moved the trainee with the patient.
    /// </summary>
    public static class TransportStaging
    {
        /// <summary>The places with no room built: the patient goes behind the door and is not seen.</summary>
        public static readonly HashSet<string> HiddenRooms = new HashSet<string> { "ultrasound", "xray", "ct", "mri" };

        public static string Door(string place) => "door:" + place;
        public static string Inside(string place) => "inside:" + place;

        public static Staging For(ObservedTransport t, string patientLocation, string traineeLocation, string from)
        {
            var s = new Staging { Anchor = patientLocation };
            if (t == null || t.State.Length == 0 || t.State == "CANCELLED" || t.State == "BACK") return s;
            bool hidden = HiddenRooms.Contains(t.To);
            s.TraineeFollows = traineeLocation.Length > 0 && traineeLocation == patientLocation && patientLocation != from;   // Core moved the trainee with the patient
            switch (t.State)
            {
                case "REQUESTED":
                case "DELAYED":
                    break;                                                           // the porter is on the way; the patient stays put
                case "PREPARING":
                    s.Porter = true; s.Mode = t.Mode;
                    break;
                case "IN_TRANSIT":
                    s.Porter = true; s.Mode = t.Mode; s.Moving = true;
                    s.Anchor = Door(t.To);                                           // cosmetic travel; the door is as far as it goes until Core says ARRIVED
                    break;
                case "ARRIVED":
                case "DONE":
                    s.Mode = t.Mode;
                    s.Anchor = hidden ? Inside(t.To) : t.To;
                    s.Visible = !hidden;
                    s.Porter = !hidden && t.State == "ARRIVED";
                    break;
                case "RETURNING":
                    s.Porter = true; s.Mode = t.Mode; s.Moving = true;
                    s.Anchor = from.Length > 0 ? from : patientLocation;
                    break;
            }
            return s;
        }
    }
}
