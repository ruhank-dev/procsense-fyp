using System;
using System.Collections.Generic;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// How the patient holds and moves for a behaviour mode, beside the face (<see cref="PatientExpression"/>). Yaw, Nod and Tilt are head degrees (turn away, chin up or down, ear to shoulder); GazeX and GazeY shift where the eyes rest,
    /// Dart is how far and how fast the eyes jump about (anxious, confused); Depth scales how deep each breath is (the rate always follows the measured respiratory rate, never the mood); Restless scales the head drift; Guard (0 to 1)
    /// brings the right hand to the lower right abdomen. Cosmetic only: it reads the mode from the observable state and decides nothing.
    /// </summary>
    public readonly struct PatientPose
    {
        public readonly float Yaw, Nod, Tilt, GazeX, GazeY, Dart, DartSpeed, Depth, Restless, Guard;

        public PatientPose(float yaw = 0, float nod = 0, float tilt = 0, float gazeX = 0, float gazeY = 0, float dart = 0, float dartSpeed = 1, float depth = 1, float restless = 1, float guard = 0)
        { Yaw = yaw; Nod = nod; Tilt = tilt; GazeX = gazeX; GazeY = gazeY; Dart = dart; DartSpeed = dartSpeed; Depth = depth; Restless = restless; Guard = guard; }

        public static readonly PatientPose Calm = new PatientPose(depth: 1f, restless: 1f);                // not new PatientPose(): a struct ignores the constructor defaults

        public static PatientPose Lerp(in PatientPose a, in PatientPose b, float k) => new PatientPose(
            a.Yaw + (b.Yaw - a.Yaw) * k, a.Nod + (b.Nod - a.Nod) * k, a.Tilt + (b.Tilt - a.Tilt) * k, a.GazeX + (b.GazeX - a.GazeX) * k, a.GazeY + (b.GazeY - a.GazeY) * k,
            a.Dart + (b.Dart - a.Dart) * k, a.DartSpeed + (b.DartSpeed - a.DartSpeed) * k, a.Depth + (b.Depth - a.Depth) * k, a.Restless + (b.Restless - a.Restless) * k, a.Guard + (b.Guard - a.Guard) * k);

        private static readonly Dictionary<string, PatientPose> Table = new Dictionary<string, PatientPose>(StringComparer.OrdinalIgnoreCase)
        {
            { "IN_PAIN", new PatientPose(yaw: -10, nod: -6, tilt: 4, gazeY: -.25f, depth: .6f, restless: 1.6f, guard: 1f) },
            { "ANXIOUS", new PatientPose(nod: 3, dart: .9f, dartSpeed: 2.2f, depth: .8f, restless: 2f) },
            { "DISTRESSED", new PatientPose(yaw: -14, nod: -4, tilt: 7, gazeY: -.15f, dart: .5f, dartSpeed: 1.6f, depth: .7f, restless: 2.4f, guard: .6f) },
            { "CONFUSED", new PatientPose(yaw: 8, tilt: 9, dart: .45f, dartSpeed: .9f, restless: 1.2f) },
            { "DROWSY", new PatientPose(yaw: 12, nod: -8, tilt: 10, gazeY: -.5f, depth: 1.2f, restless: .25f) },
            { "NAUSEATED", new PatientPose(yaw: -16, nod: -5, gazeY: -.3f, depth: .7f, restless: 1.2f) },
            { "UNCOOPERATIVE", new PatientPose(yaw: 26, nod: 2, gazeX: .6f, dart: .15f, restless: 1.5f) },
        };

        public static IEnumerable<string> Modes => Table.Keys;

        /// <summary>The pose for a mode; calm when the mode is empty or unknown.</summary>
        public static PatientPose For(string mode) => !string.IsNullOrEmpty(mode) && Table.TryGetValue(mode, out var p) ? p : Calm;
    }
}
