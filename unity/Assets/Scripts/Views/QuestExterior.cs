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
        public enum Variant { Original, Optimised }

        [SerializeField] private Renderer[] originals = new Renderer[0];
        [SerializeField] private Renderer[] optimised = new Renderer[0];
        private static readonly List<QuestExterior> Instances = new List<QuestExterior>();
        private static Variant _current = Variant.Original;

        /// <summary>The variant every instance shows. Original unless a diagnostic configuration (PerfToggles, B17) or an approved default says otherwise.</summary>
        public static Variant Current { get => _current; set { _current = value; foreach (var i in Instances) if (i != null) i.Apply(); } }
        public int OriginalCount => originals.Length;
        public int OptimisedCount => optimised.Length;

        public void Configure(Renderer[] originalRenderers, Renderer[] optimisedRenderers) { originals = originalRenderers; optimised = optimisedRenderers; Apply(); }

        private void OnEnable() { Instances.Add(this); Apply(); }
        private void OnDisable() { Instances.Remove(this); SetAll(originals, true); SetAll(optimised, false); }

        private void Apply()
        {
            bool opt = _current == Variant.Optimised && optimised.Length > 0;
            SetAll(originals, !opt); SetAll(optimised, opt);
        }

        private static void SetAll(Renderer[] list, bool on) { foreach (var r in list) if (r != null) r.enabled = on; }
    }
}
