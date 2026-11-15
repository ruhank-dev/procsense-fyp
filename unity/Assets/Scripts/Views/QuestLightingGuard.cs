using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Quest overhaul (Agent B): the hospital is lit entirely by baked lighting, so no light needs a shadow map and no moving object needs to cast one. The scenes are authored that way; this
    /// guard keeps it true for anything added later (spawned equipment, a new light): every scene load, lights that are not baked get no shadows and non-static renderers cast none.
    /// Android only by default (desktop keeps its look); Sweep() is public so a test can run it.
    /// </summary>
    public static class QuestLightingGuard
    {
        static bool? _enabled;
        public static bool Enabled { get => _enabled ?? Application.isMobilePlatform; set => _enabled = value; }
        public static void ResetToDefault() => _enabled = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() { SceneManager.sceneLoaded += (s, m) => { if (Enabled) Sweep(s); }; if (Enabled) Sweep(default); }

        /// <summary>Applies the rule to one scene (or every loaded scene when the scene is invalid). Returns how many objects were changed.</summary>
        public static int Sweep(Scene scene)
        {
            int changed = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scene.IsValid() && l.gameObject.scene != scene) continue;
                if (l.bakingOutput.lightmapBakeType != LightmapBakeType.Baked && l.shadows != LightShadows.None) { l.shadows = LightShadows.None; changed++; }
            }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scene.IsValid() && r.gameObject.scene != scene) continue;
                if (r.gameObject.isStatic) continue;
                if (r.shadowCastingMode != ShadowCastingMode.Off) { r.shadowCastingMode = ShadowCastingMode.Off; changed++; }
            }
            return changed;
        }
    }
}
