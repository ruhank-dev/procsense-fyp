using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Quest overhaul, Agent D: cheaper characters without touching their gameplay.
    /// 1. Every skinned character/cloth renderer: no realtime shadow casting, no shadow receiving (the Quest build has no realtime shadow maps), light probes on, reflection probes off.
    /// 2. Every material those renderers use: environment reflections and shadow receiving switched off (two fewer texture/shadow samples per pixel; specular highlights stay).
    /// 3. A LODGroup on every multi-renderer person (staff, MPFB patient): LOD0 everything, LOD1 drops the mouth interior and brows/lashes (teeth alone are about 7,000 triangles),
    ///    LOD2 keeps body, clothes, hair and eyes only, then culled. Facial blendshapes live on the body mesh, which every LOD keeps, so speech and expression state is never lost.
    /// Idempotent. Batch: -executeMethod ProcSense.EditorTools.CharacterQuestOptimiser.Run. Revert is git (prefabs, scenes and materials only change serialized flags).
    /// </summary>
    public static class CharacterQuestOptimiser
    {
        // screen-height fractions: LOD0 above 0.30 (about 2.5 m for an adult), LOD1 down to 0.12, LOD2 down to 0.04, then culled
        static readonly float[] Thresholds = { 0.30f, 0.12f, 0.04f };
        static readonly string[] MouthAndBrows = { "teeth", "tongue", "eyebrow", "eyelash" };
        static readonly HashSet<Material> Done = new HashSet<Material>();
        static int _renderers, _materials, _lodGroups;

        public static void Run()
        {
            Done.Clear(); _renderers = _materials = _lodGroups = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Characters" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { if (Process(root, true)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var sc in new[] { "PatientBay", "Corridor", "OT" })
            {
                var path = AssetDatabase.FindAssets("t:Scene " + sc, new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).First(x => Path.GetFileNameWithoutExtension(x) == sc);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool dirty = false;
                foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0 && !PrefabUtility.IsPartOfPrefabInstance(a)) dirty |= Process(a.gameObject, false);
                // skinned props outside an Animator (curtains, gown): flags only
                var loose = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var r in loose) dirty |= Flags(r);
                // unpacked people without an Animator on the parent (the corridor receptionist): one group per parent that holds four or more skinned parts
                foreach (var grp in loose.Where(r => r.GetComponentInParent<Animator>(true) == null && r.transform.parent != null).GroupBy(r => r.transform.parent).Where(g => g.Count() >= 4))
                    if (grp.Key.GetComponent<LODGroup>() == null && grp.Key.GetComponentInParent<LODGroup>(true) == null) dirty |= AddLods(grp.Key.gameObject, grp.ToArray());
                if (dirty) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterQuestOptimiser] renderers changed {_renderers}, materials {_materials}, LODGroups {_lodGroups}");
            EditorApplication.Exit(0);
        }

        static bool Process(GameObject root, bool prefab)
        {
            bool changed = false;
            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in smrs) changed |= Flags(r);
            if (smrs.Length >= 4 && root.GetComponentInChildren<LODGroup>(true) == null) changed |= AddLods(root, smrs);
            return changed;
        }

        static bool Flags(Renderer r)
        {
            bool c = false;
            if (r.shadowCastingMode != ShadowCastingMode.Off) { r.shadowCastingMode = ShadowCastingMode.Off; c = true; }
            if (r.receiveShadows) { r.receiveShadows = false; c = true; }
            if (r.reflectionProbeUsage != ReflectionProbeUsage.Off) { r.reflectionProbeUsage = ReflectionProbeUsage.Off; c = true; }
            if (r.lightProbeUsage != LightProbeUsage.BlendProbes) { r.lightProbeUsage = LightProbeUsage.BlendProbes; c = true; }
            if (c) _renderers++;
            foreach (var m in r.sharedMaterials) CheapMaterial(m);
            return c;
        }

        static void CheapMaterial(Material m)
        {
            if (m == null || !Done.Add(m) || m.shader == null || !m.shader.name.StartsWith("Universal Render Pipeline/")) return;
            bool c = false;
            if (m.HasProperty("_EnvironmentReflections") && m.GetFloat("_EnvironmentReflections") != 0f) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); c = true; }
            if (m.HasProperty("_ReceiveShadows") && m.GetFloat("_ReceiveShadows") != 0f) { m.SetFloat("_ReceiveShadows", 0f); m.EnableKeyword("_RECEIVE_SHADOWS_OFF"); c = true; }
            if (c) { EditorUtility.SetDirty(m); _materials++; }
        }

        static bool AddLods(GameObject root, SkinnedMeshRenderer[] smrs)
        {
            var all = smrs.Cast<Renderer>().Concat(root.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r != null && !r.name.ToLower().Contains("badge")).ToArray();
            bool IsMouth(Renderer r) => MouthAndBrows.Any(k => r.name.ToLower().Contains(k));
            var lod0 = all;
            var lod1 = all.Where(r => !IsMouth(r)).ToArray();
            var lod2 = lod1.Where(r => !r.name.ToLower().Contains("shoe") && !r.name.ToLower().Contains("accessor")).ToArray();
            if (lod1.Length == all.Length) return false; // nothing to drop (single-mesh characters need no group)
            var g = root.AddComponent<LODGroup>();
            g.fadeMode = LODFadeMode.None; // cross-fade variants are stripped on Android; hysteresis comes from the band widths
            g.SetLODs(new[] { new LOD(Thresholds[0], lod0), new LOD(Thresholds[1], lod1), new LOD(Thresholds[2], lod2) });
            g.RecalculateBounds(); _lodGroups++;
            return true;
        }
    }
}
