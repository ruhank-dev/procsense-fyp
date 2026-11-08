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
                twoRenderers = urp.name == AndroidPipelineName;    // only the Quest tier lists the legacy renderer as index 1
            }
            ExteriorVisibility.Enabled = !p.ExteriorCullOff;
            ExteriorVisibility.ForceHidden = p.CityHidden;
            if (cam != null) cam.useOcclusionCulling = !p.OcclusionOff;
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
            foreach (var kv in _animators) if (kv.Key != null) kv.Key.enabled = p.CharactersHidden ? false : kv.Value;
        }

        /// <summary>Remembers the authored state of every light, renderer and animator once (again when a scene was loaded or unloaded).</summary>
        private void Index()
        {
            if (SceneManager.sceneCount == _sceneCount) return;
            _sceneCount = SceneManager.sceneCount;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!_lights.ContainsKey(l)) _lights[l] = (l.enabled, l.shadows);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!_renderers.ContainsKey(r)) _renderers[r] = r.forceRenderingOff;
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
