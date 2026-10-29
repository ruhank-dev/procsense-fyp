using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.UI.Accessibility
{
    /// <summary>
    /// The readability rules of docs/plans/p0/p0-vr-interaction-ui/ui-guidelines.md as numbers (p0-vr-interaction-ui-s10): the size of text in headset pixels at the distance
    /// its panel is read from, and the WCAG contrast ratio of a text colour on its background.
    /// </summary>
    public static class Readability
    {
        /// <summary>Quest 3 shows about 25 pixels per degree in the middle of the view.</summary>
        public const float PixelsPerDegree = 25f;
        /// <summary>The plan's minimum: text at least 24 px tall in the headset (about 17 mm per metre of distance).</summary>
        public const float MinHeadsetPixels = 24f;
        /// <summary>WCAG AA for normal text.</summary>
        public const float MinContrast = 4.5f;

        /// <summary>The em height of a label in headset pixels when read from this far away.</summary>
        public static float HeadsetPixels(TMP_Text label, float distance)
        {
            float metres = label.fontSize * Mathf.Abs(label.transform.lossyScale.y);
            return Mathf.Atan2(metres, Mathf.Max(.05f, distance)) * Mathf.Rad2Deg * PixelsPerDegree;
        }

        /// <summary>The WCAG 2 contrast ratio of two opaque colours (1 to 21).</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + .05f) / (Mathf.Min(la, lb) + .05f);
        }

        public static float Luminance(Color c)
        {
            float Lin(float v) => v <= .03928f ? v / 12.92f : Mathf.Pow((v + .055f) / 1.055f, 2.4f);
            return .2126f * Lin(c.r) + .7152f * Lin(c.g) + .0722f * Lin(c.b);
        }

        /// <summary>Every active label under the canvas that is smaller than the minimum at the canvas's reading distance: "canvas / label: 19.2 px".</summary>
        public static List<string> TooSmall(AccessibleCanvas canvas)
        {
            var list = new List<string>();
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                float px = HeadsetPixels(label, canvas.ViewingDistance);
                if (px + .05f < MinHeadsetPixels) list.Add(canvas.name + " / " + label.name + ": " + px.ToString("0.0") + " px");
            }
            return list;
        }
    }
}
