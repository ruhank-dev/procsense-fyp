using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Views;
using ProcSense.VR.XR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Diagnostics
{
    /// <summary>
    /// Applies a <see cref="PerfPreset"/> to the running scenes and restores the authored state when the preset no longer asks for a change. Only run-time switches are used
    /// (Renderer.forceRenderingOff, Light.enabled/shadows, Animator.enabled, the camera's renderer index, the pipeline's MSAA in memory, the XR display); nothing is saved, so the
    /// scenes and assets stay exactly as approved. Used only by the diagnostic build (QuestPerfDiagnostics).
    /// </summary>
    public sealed class PerfToggles
    {
        private static readonly string[] Rooms = { "PatientBay", "Corridor", "OT" };
        public const string AndroidPipelineName = "URP_Quest_Android";
        private readonly Dictionary<Light, (bool enabled, LightShadows shadows)> _lights = new Dictionary<Light, (bool, LightShadows)>();
        private readonly Dictionary<Renderer, bool> _renderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Animator, bool> _animators = new Dictionary<Animator, bool>();
        private int _sceneCount = -1;
        private int _authoredMsaa = -1;
        private (float dist, bool soft, int size, int maxLights, LightRenderingMode mode, int cascades, bool depth, bool opaque, bool hdr, bool dyn, bool srpb)? _authoredUrp;
        private DepthPrimingMode? _authoredPriming;
        private AnisotropicFiltering? _authoredAniso;
        private int _lastAnisoMax = -2;
        private readonly Dictionary<Material, bool> _instancing = new Dictionary<Material, bool>();
        /// <summary>True once the interiors were statically batched in this process; the batching cannot be undone, so later measurements are no longer comparable to the authored state.</summary>
        public static bool StaticBatchApplied { get; private set; }
        private readonly Dictionary<ReflectionProbe, bool> _probes = new Dictionary<ReflectionProbe, bool>();
        private readonly Dictionary<SkinnedMeshRenderer, bool> _offscreen = new Dictionary<SkinnedMeshRenderer, bool>();

        /// <summary>Applies the preset. Call again whenever scenes change or another script may have switched a light back (RoomLoader keeps one clinical key per room).</summary>
        public void Apply(PerfPreset p, Camera cam, string currentRoom)
        {
            Index();
            DesktopOnly.ShowOnAndroid = !p.ShaftsOff;
            QuestRenderTier.SetFoveation(p.Foveation);
            if (Mathf.Abs(UnityEngine.XR.XRSettings.eyeTextureResolutionScale - QuestRenderTier.ClampEyeScale(p.EyeScale)) > 0.001f) QuestRenderTier.SetEyeScale(p.EyeScale);
            bool twoRenderers = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (_authoredMsaa < 0) _authoredMsaa = urp.msaaSampleCount;
                int want = p.Msaa == 4 ? _authoredMsaa : p.Msaa;
                if (urp.msaaSampleCount != want) urp.msaaSampleCount = want;
                if (_authoredUrp == null) _authoredUrp = (urp.shadowDistance, urp.supportsSoftShadows, urp.mainLightShadowmapResolution, urp.maxAdditionalLightsCount, urp.additionalLightsRenderingMode, urp.shadowCascadeCount, urp.supportsCameraDepthTexture, urp.supportsCameraOpaqueTexture, urp.supportsHDR, urp.supportsDynamicBatching, GraphicsSettings.useScriptableRenderPipelineBatching);
                var au = _authoredUrp.Value;
                urp.shadowDistance = p.ShadowDistance > 0 ? p.ShadowDistance : au.dist; SetPrivate(urp, "m_SoftShadowsSupported", p.SoftShadowsOff ? false : au.soft);
                urp.mainLightShadowmapResolution = p.ShadowMapSize > 0 ? p.ShadowMapSize : au.size; urp.maxAdditionalLightsCount = p.MaxAdditionalLights >= 0 ? p.MaxAdditionalLights : au.maxLights;
                SetPrivate(urp, "m_AdditionalLightsRenderingMode", p.AdditionalLightsOff ? LightRenderingMode.Disabled : p.AdditionalLightsPerVertex ? LightRenderingMode.PerVertex : au.mode);
                urp.shadowCascadeCount = p.ShadowCascades > 0 ? p.ShadowCascades : au.cascades;
                urp.supportsCameraDepthTexture = p.DepthTextureOn || au.depth; urp.supportsCameraOpaqueTexture = p.OpaqueTextureOn || au.opaque;
                urp.supportsHDR = p.HdrOn || au.hdr; urp.supportsDynamicBatching = p.DynamicBatchingOn || au.dyn;
                GraphicsSettings.useScriptableRenderPipelineBatching = !p.SrpBatcherOff && au.srpb;
                if (urp.GetRenderer(0) is UniversalRenderer r0)
                {
                    if (_authoredPriming == null) _authoredPriming = r0.depthPrimingMode;
                    var wantPriming = p.DepthPrimingOn ? DepthPrimingMode.Forced : _authoredPriming.Value;
                    if (r0.depthPrimingMode != wantPriming) r0.depthPrimingMode = wantPriming;
                }
                twoRenderers = urp.name == AndroidPipelineName;    // only the Quest tier lists the legacy renderer as index 1
            }
            ExteriorVisibility.Enabled = !p.ExteriorCullOff; ExteriorVisibility.PortalCullEnabled = !p.PortalCullOff;
            QuestExterior.Current = p.OptimisedExterior ? QuestExterior.Variant.Optimised : p.OriginalExterior ? QuestExterior.Variant.Original : QuestExterior.DefaultVariant;
            ExteriorVisibility.ForceHidden = p.CityHidden;
            if (p.RoomCull) RoomVisibility.Enabled = true; else if (p.RoomCullOff) RoomVisibility.Enabled = false; else RoomVisibility.ResetToDefault();
            CharacterPerfCuller.Enabled = p.CharCull || CharacterPerfCuller.DefaultEnabled;
            QuestCullingProfile.OcclusionEnabled = p.OcclusionOn || (!p.OcclusionOff && QuestCullingProfile.OcclusionDefault);
            if (cam != null) cam.useOcclusionCulling = QuestCullingProfile.OcclusionEnabled;
            if (cam != null && twoRenderers) cam.GetUniversalAdditionalCameraData().SetRenderer(p.LegacyRenderer ? 1 : 0);

            foreach (var kv in _lights)
            {
                var l = kv.Key; if (l == null) continue;
                bool fill = l.bakingOutput.lightmapBakeType == LightmapBakeType.Realtime && (l.name.StartsWith("Ceiling fill") || l.name == "Soft clinical key");
                if (fill && p.RealtimeFillsOff) l.enabled = false;
                else if (fill && l.name == "Soft clinical key") l.enabled = l.gameObject.scene.name == currentRoom;   // RoomLoader's rule: one clinical key, the current room's
                else if (fill) l.enabled = kv.Value.enabled;
                bool sun = l.type == LightType.Directional && l.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed;
                if (sun) l.shadows = p.MainShadowsOff ? LightShadows.None : kv.Value.shadows;
            }
            foreach (var kv in _renderers)
            {
                var r = kv.Key; if (r == null) continue;
                bool off = kv.Value;                                       // authored forceRenderingOff
                string scene = r.gameObject.scene.name;
                bool city = UnderCity(r.transform);
                if (p.CurrentRoomOnly && Rooms.Contains(scene) && scene != currentRoom && !city) off = true;
                if (p.CityHidden && city) off = true;
                if (p.CharactersHidden && r is SkinnedMeshRenderer && !city) off = true;
                r.forceRenderingOff = off;
            }
            if (_authoredAniso == null) _authoredAniso = QualitySettings.anisotropicFiltering;
            var wantAniso = p.AnisoMode == 1 ? AnisotropicFiltering.Disable : p.AnisoMode == 2 ? AnisotropicFiltering.Enable : _authoredAniso.Value;
            if (QualitySettings.anisotropicFiltering != wantAniso) QualitySettings.anisotropicFiltering = wantAniso;
            int anisoMax = p.AnisoMax > 0 ? p.AnisoMax : -1;
            if (anisoMax != _lastAnisoMax) { _lastAnisoMax = anisoMax; Texture.SetGlobalAnisotropicFilteringLimits(-1, anisoMax); }   // -1 = unchanged (the authored limits)
            foreach (var kv in _instancing) if (kv.Key != null) kv.Key.enableInstancing = p.InstancingAll || kv.Value;
            if (p.StaticBatch && !StaticBatchApplied)
            {
                StaticBatchApplied = true;
                int combined = 0;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i); if (!sc.isLoaded) continue;
                    var go = new GameObject("PerfStaticBatch"); SceneManager.MoveGameObjectToScene(go, sc);
                    combined += go.AddComponent<StaticBatchingAtLoad>().Combine();
                }
                Debug.Log("[PerfToggles] static batching combined " + combined + " renderers (irreversible for this process)");
            }
            foreach (var kv in _probes) if (kv.Key != null) kv.Key.enabled = !p.ReflectionProbesOff && kv.Value;
            foreach (var kv in _offscreen) if (kv.Key != null) kv.Key.updateWhenOffscreen = p.SkinOffscreenOff ? false : kv.Value;
            foreach (var kv in _animators) if (kv.Key != null) kv.Key.enabled = p.CharactersHidden ? false : kv.Value;
        }

        /// <summary>The pipeline asset exposes these two settings read-only; the diagnostic build switches the in-memory value (never saved) and puts the authored one back.</summary>
        private static void SetPrivate(UniversalRenderPipelineAsset asset, string field, object value)
            => typeof(UniversalRenderPipelineAsset).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(asset, value);

        /// <summary>Remembers the authored state of every light, renderer and animator once (again when a scene was loaded or unloaded).</summary>
        private void Index()
        {
            if (SceneManager.sceneCount == _sceneCount) return;
            _sceneCount = SceneManager.sceneCount;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!_lights.ContainsKey(l)) _lights[l] = (l.enabled, l.shadows);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!_renderers.ContainsKey(r)) _renderers[r] = r.forceRenderingOff;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) foreach (var m in r.sharedMaterials) if (m != null && !_instancing.ContainsKey(m)) _instancing[m] = m.enableInstancing;
            foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!_probes.ContainsKey(rp)) _probes[rp] = rp.enabled;
            foreach (var sk in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!_offscreen.ContainsKey(sk)) _offscreen[sk] = sk.updateWhenOffscreen;
            foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!_animators.ContainsKey(a) && a.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) _animators[a] = a.enabled;
        }

        private static bool UnderCity(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "City exterior" || t.name == "Window exterior backdrop") return true;
            return false;
        }
    }
}
