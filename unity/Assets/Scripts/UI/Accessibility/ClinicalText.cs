using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.UI.Accessibility
{
    /// <summary>
    /// Sharp-text settings for the panels a trainee reads clinical values from on Quest 2 (EMR tablet, EMR, patient monitor, menu panel), 14 Nov 2026 UTC.
    /// They are drawn into the eye buffer at 0.8 of the native size (QuestPerformanceProfile), not on an OpenXR quad layer: the XR Composition Layers package
    /// (bundled 2.5.0) is not used for them, see docs/audit/graphics_matrix.md "compositor layers for UI". What this class does instead:
    /// 1. every label on these panels shares one SDF material per font with a small face dilation, so thin strokes keep their contrast when the eye buffer is
    ///    resampled (layout, glyph metrics and batching unchanged: one shared material, no per-label instance);
    /// 2. while one of these panels is in front of the head and near enough to read, dynamic resolution may not go below <see cref="FloorScale"/>.
    /// Legibility on the device is not claimed: it is the headset check DC-13.
    /// </summary>
    public static class ClinicalText
    {
        /// <summary>Canvas names (as UiKit names them) that carry clinical text.</summary>
        public static readonly HashSet<string> Canvases = new HashSet<string> { "Tablet", "EMR shell", "EMR", "Monitor", "Game flow", "Main menu" };

        /// <summary>SDF face dilation for those panels (0 = the font's own weight, TMP range -1..1).</summary>
        public const float FaceDilate = 0.1f;

        /// <summary>Lowest effective render scale while such a panel is being read (the profile's start scale; dynamic resolution may otherwise go to 0.7).</summary>
        public const float FloorScale = 0.8f;

        /// <summary>A panel counts as being read within this distance (m) and this angle (degrees) of the view direction.</summary>
        public const float ReadDistance = 2f, ReadAngle = 30f;

        private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        private static readonly Dictionary<TMP_FontAsset, Material> Materials = new Dictionary<TMP_FontAsset, Material>();
        private static readonly List<AccessibleCanvas> Open = new List<AccessibleCanvas>();

        public static bool IsClinical(string canvasName) => canvasName != null && Canvases.Contains(canvasName);

        /// <summary>The shared sharp material for a font (made once per font from the font's own material).</summary>
        public static Material SharpMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            if (Materials.TryGetValue(font, out var m) && m != null) return m;
            m = new Material(font.material) { name = font.material.name + " (clinical sharp)" };
            if (m.HasProperty(FaceDilateId)) m.SetFloat(FaceDilateId, FaceDilate);
            ShaderUtilities.UpdateShaderRatios(m);
            Materials[font] = m;
            return m;
        }

        internal static void Register(AccessibleCanvas c) { if (!Open.Contains(c)) Open.Add(c); }
        internal static void Unregister(AccessibleCanvas c) => Open.Remove(c);

        /// <summary>The clinical panels that are enabled now.</summary>
        public static IReadOnlyList<AccessibleCanvas> Active => Open;

        /// <summary>True when a point lies inside the reading cone of the head.</summary>
        public static bool InView(Vector3 head, Vector3 forward, Vector3 point)
        {
            var to = point - head;
            float d = to.magnitude;
            if (d > ReadDistance) return false;
            if (d < 1e-3f) return true;
            return Vector3.Angle(forward, to) <= ReadAngle;
        }

        /// <summary>True when an enabled clinical panel is in the reading cone of the head.</summary>
        public static bool Reading(Vector3 head, Vector3 forward)
        {
            for (int i = Open.Count - 1; i >= 0; i--)
            {
                var c = Open[i];
                if (c == null) { Open.RemoveAt(i); continue; }
                if (!c.isActiveAndEnabled) continue;
                var rect = c.transform as RectTransform;
                var centre = rect != null ? rect.TransformPoint(rect.rect.center) : c.transform.position;
                if (InView(head, forward, centre)) return true;
            }
            return false;
        }
    }
}
