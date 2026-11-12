using System;
using System.Collections.Generic;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The face the patient shows for the behaviour mode Core reports (content/behaviour/modes.json): ARKit blend shape names (matched by their ending, so "m162.AK_03_BrowInnerUp" is "BrowInnerUp") with a weight from 0 to 1.
    /// "EyeBlink" is not a face pose but how far the lids droop: the blink never opens them past it. A mode with no entry, or none, is a calm face. Cosmetic only: this reads the mode from the observable state and decides nothing.
    /// </summary>
    public static class PatientExpression
    {
        public const string Droop = "EyeBlink";
        public static readonly IReadOnlyDictionary<string, float> Calm = new Dictionary<string, float>();

        private static Dictionary<string, float> Build(params (string shape, float weight)[] items)
        {
            var d = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var (shape, weight) in items)
            {
                if (shape.EndsWith("*", StringComparison.Ordinal)) { string s = shape.Substring(0, shape.Length - 1); d[s + "Left"] = weight; d[s + "Right"] = weight; }
                else d[shape] = weight;
            }
            return d;
        }

        private static readonly Dictionary<string, IReadOnlyDictionary<string, float>> Table = new Dictionary<string, IReadOnlyDictionary<string, float>>(StringComparer.OrdinalIgnoreCase)
        {
            { "IN_PAIN", Build(("BrowDown*", .8f), ("BrowInnerUp", .9f), ("EyeSquint*", .9f), ("MouthFrown*", .8f), ("MouthPress*", .6f), ("NoseSneer*", .5f), ("CheekSquint*", .6f), ("MouthStretch*", .35f)) },
            { "ANXIOUS", Build(("BrowInnerUp", 1f), ("BrowOuterUp*", .55f), ("EyeWide*", .6f), ("MouthFrown*", .4f), ("MouthPress*", .45f), ("MouthRollLower", .4f), ("MouthStretch*", .15f)) },
            { "DISTRESSED", Build(("BrowInnerUp", 1f), ("BrowDown*", .4f), ("EyeSquint*", .55f), ("MouthFrown*", 1f), ("MouthStretch*", .5f), ("JawOpen", .15f), ("CheekSquint*", .4f), ("MouthLowerDown*", .4f)) },
            { "CONFUSED", Build(("BrowInnerUp", .6f), ("BrowDownLeft", .8f), ("BrowOuterUpRight", .9f), ("EyeSquintLeft", .5f), ("EyeWideRight", .3f), ("MouthPressLeft", .5f), ("MouthLeft", .5f), ("JawOpen", .05f)) },
            { "DROWSY", Build((Droop, .7f), ("EyeLookDown*", .25f), ("JawOpen", .08f), ("BrowInnerUp", .1f), ("BrowDown*", .15f)) },
            { "NAUSEATED", Build(("MouthFrown*", .7f), ("NoseSneer*", .6f), ("MouthShrugLower", .5f), ("CheekPuff", .35f), ("EyeSquint*", .45f), ("BrowDown*", .4f), ("BrowInnerUp", .35f), ("MouthPress*", .5f)) },
            { "UNCOOPERATIVE", Build(("BrowDown*", 1f), ("EyeSquint*", .5f), ("MouthPress*", .9f), ("MouthFrown*", .6f), ("NoseSneer*", .4f), ("JawForward", .3f), ("MouthShrugUpper", .3f)) },
        };

        /// <summary>The target face for a mode: shape ending to weight 0..1. Calm when the mode is empty or unknown.</summary>
        public static IReadOnlyDictionary<string, float> For(string mode) => !string.IsNullOrEmpty(mode) && Table.TryGetValue(mode, out var face) ? face : Calm;

        public static IEnumerable<string> Modes => Table.Keys;
    }
}
