using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Tier switch of the exterior city (P3-EXT-04, owner decision D7 follow-up of 8 Nov 2026). The corridor scene keeps the original city untouched under "City exterior"; the Quest variant is
    /// a set of replacement meshes (same seed, same placement, less far-away detail, built by build_city.py with CITY_VARIANT=quest) that sit beside the originals, switched off. This component
    /// holds both lists and shows exactly one of them: Original (default everywhere until the owner approves the side-by-side captures, P3-EXT-07) or Optimised.
    /// It only changes <see cref="Renderer.enabled"/> (ExteriorVisibility uses forceRenderingOff, so the two switches combine); nothing is deleted, moved or re-materialled.
    /// </summary>
    public sealed class QuestExterior : MonoBehaviour
    {
        public enum Variant { Original, Optimised, Impostor }

        /// <summary>A serialisable list of materials (Unity cannot serialise Material[][]).</summary>
        [System.Serializable] public sealed class MaterialSet { public Material[] materials = new Material[0]; }

        [SerializeField] private Renderer[] originals = new Renderer[0];
        [SerializeField] private Renderer[] optimised = new Renderer[0];
        // Impostor variant (overhaul, 14 Nov 2026): the optimised geometry with the cheap "ProcSense/Exterior Unlit" materials (one texture sample, shading from the normal in the vertex shader, no lights, shadows
        // or normal/metal maps) and a small LDR sky. materialTargets[i] shows originalMaterials[i] normally and impostorMaterials[i] in this variant.
        [SerializeField] private Renderer[] materialTargets = new Renderer[0];
        [SerializeField] private MaterialSet[] originalMaterials = new MaterialSet[0];
        [SerializeField] private MaterialSet[] impostorMaterials = new MaterialSet[0];
        [SerializeField] private Material skyOriginal, skyImpostor;
        private static readonly List<QuestExterior> Instances = new List<QuestExterior>();
        private static Variant? _override;

        /// <summary>The variant a build shows when nothing overrides it: Impostor on the Quest (Android), Original on desktop and in the Editor (the desktop URP and HDRP references keep the untouched city). Owner approval of 9 Nov 2026 (P3-EXT-07).</summary>
        public static Variant DefaultVariant => Application.platform == RuntimePlatform.Android ? Variant.Impostor : Variant.Original;

        /// <summary>The variant every instance shows: the platform default unless a diagnostic configuration (PerfToggles) or a test overrides it.</summary>
        public static Variant Current { get => _override ?? DefaultVariant; set { _override = value; foreach (var i in Instances) if (i != null) i.Apply(); } }

        /// <summary>Forgets any override and shows the platform default again.</summary>
        public static void ResetToDefault() { _override = null; foreach (var i in Instances) if (i != null) i.Apply(); }
        public int OriginalCount => originals.Length;
        public int OptimisedCount => optimised.Length;

        public void Configure(Renderer[] originalRenderers, Renderer[] optimisedRenderers) { originals = originalRenderers; optimised = optimisedRenderers; Apply(); }
        public bool HasImpostor => materialTargets.Length > 0 && impostorMaterials.Length == materialTargets.Length;
        public int ImpostorTargetCount => materialTargets.Length;

        public void ConfigureImpostor(Renderer[] targets, MaterialSet[] original, MaterialSet[] impostor, Material skyOrig, Material skyImp)
        {
            materialTargets = targets; originalMaterials = original; impostorMaterials = impostor; skyOriginal = skyOrig; skyImpostor = skyImp; Apply();
        }

        private void OnEnable() { Instances.Add(this); Apply(); }
        private void OnDisable() { Instances.Remove(this); SetAll(originals, true); SetAll(optimised, false); ApplyMaterials(false); }

        private void Apply()
        {
            ExteriorVisibility.MarkDirty();
            bool opt = Current != Variant.Original && optimised.Length > 0;
            SetAll(originals, !opt); SetAll(optimised, opt);
            ApplyMaterials(Current == Variant.Impostor && HasImpostor);
        }

        private void ApplyMaterials(bool impostor)
        {
            for (int i = 0; i < materialTargets.Length && i < originalMaterials.Length; i++)
            {
                var r = materialTargets[i]; if (r == null) continue;
                var set = impostor && i < impostorMaterials.Length ? impostorMaterials[i] : originalMaterials[i];
                if (set != null && set.materials.Length > 0) r.sharedMaterials = set.materials;
            }
            if (skyOriginal != null && skyImpostor != null) RenderSettings.skybox = impostor ? skyImpostor : skyOriginal;
        }

        private static void SetAll(Renderer[] list, bool on) { foreach (var r in list) if (r != null) r.enabled = on; }
    }
}
