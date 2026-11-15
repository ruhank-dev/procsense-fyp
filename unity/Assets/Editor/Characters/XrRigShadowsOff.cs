using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Quest overhaul, Agent D: the player rig (hands, controllers, pointers such as Pinch_Pointer_LOD0 from the XR sample) must not cast or receive realtime shadows and does not need reflection probes.
    /// Applied as overrides on Assets/Prefabs/XR/PlayerRig.prefab (the XR sample assets themselves are not edited). Idempotent. Batch: -executeMethod ProcSense.EditorTools.XrRigShadowsOff.Run
    /// </summary>
    public static class XrRigShadowsOff
    {
        public static void Run()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/XR" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r is ParticleSystemRenderer) { if (r.shadowCastingMode != ShadowCastingMode.Off || r.receiveShadows) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; changed = true; n++; } continue; }
                        if (r.shadowCastingMode != ShadowCastingMode.Off || r.receiveShadows || r.reflectionProbeUsage != ReflectionProbeUsage.Off)
                        { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.reflectionProbeUsage = ReflectionProbeUsage.Off; changed = true; n++; }
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("[XrRigShadowsOff] renderers changed " + n);
            EditorApplication.Exit(0);
        }
    }
}
