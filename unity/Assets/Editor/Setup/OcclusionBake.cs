using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Bakes Unity occlusion culling for the connected hospital (P3-PRF-20, owner decision D7): PatientBay, Corridor and OT are opened together, building shell and large opaque fixtures are marked
    /// Occluder + Occludee static, everything else that is already static is marked Occludee, the data is computed and saved per scene. Occlusion culling only decides which renderers are
    /// *skipped when fully hidden behind others*; nothing is deleted or changed in appearance. Glass and alpha-cut materials never become occluders (the city must stay visible through windows).
    /// Only OR-ing bits into m_StaticEditorFlags and the OcclusionCullingData asset change: undo with git checkout of the three scene files (and removal of the *OcclusionCullingData.asset files).
    /// Batch: -executeMethod ProcSense.EditorTools.OcclusionBake.Run  (task unity-occlusion-bake); report: ../docs/qa/quest2_occlusion_bake.md.
    /// </summary>
    public static class OcclusionBake
    {
        private static readonly string[] Scenes = { "Assets/Scenes/PatientBay.unity", "Assets/Scenes/Corridor.unity", "Assets/Scenes/OT.unity" };
        public const float SmallestOccluder = 1f, SmallestHole = 0.25f, BackfaceThreshold = 100f;

        public static void Run()
        {
            bool ok = false;
            try { Bake(); ok = true; }
            catch (Exception e) { Debug.LogError("[OcclusionBake] " + e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool IsTransparentOrCut(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                string n = (m.name + " " + (m.shader != null ? m.shader.name : "")).ToLowerInvariant();
                if (m.renderQueue >= 2450 || n.Contains("glass") || n.Contains("window") || n.Contains("transparent") || n.Contains("curtain")) return true;
            }
            return false;
        }

        private static void Bake()
        {
            EditorSceneManager.OpenScene(Scenes[0], OpenSceneMode.Single);
            for (int i = 1; i < Scenes.Length; i++) EditorSceneManager.OpenScene(Scenes[i], OpenSceneMode.Additive);
            var layouts = UnityEngine.Object.FindObjectsByType<ProcSense.VR.Boot.RoomLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var stats = new Dictionary<string, int[]>();   // scene -> occluders, occludees, glass/cut cleared, skipped(dynamic)
            foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string scene = mr.gameObject.scene.name; if (!stats.ContainsKey(scene)) stats[scene] = new int[4];
                var flags = GameObjectUtility.GetStaticEditorFlags(mr.gameObject);
                const StaticEditorFlags Occ = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                if (!layouts.Any(l => l.Contains(mr.bounds.center, -3f)))              // the city and anything outside the hospital stays out of the bake: its extent made the first attempt run out of memory ("Out of memory in TomeGenerator"); it is culled at run time as a dynamic occludee
                {
                    if ((flags & Occ) != 0) GameObjectUtility.SetStaticEditorFlags(mr.gameObject, flags & ~Occ);
                    stats[scene][3]++; continue;
                }
                bool authoredStatic = (flags & (StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI)) != 0;
                if (!authoredStatic) { stats[scene][3]++; continue; }              // authored as moving or unflagged: left exactly as it is (tested as a dynamic occludee at run time)
                var want = flags | StaticEditorFlags.OccludeeStatic;
                bool cut = IsTransparentOrCut(mr);
                var size = mr.bounds.size; float big = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (cut) { want &= ~StaticEditorFlags.OccluderStatic; stats[scene][2]++; }
                else if (big >= 0.8f) { want |= StaticEditorFlags.OccluderStatic; stats[scene][0]++; }
                if (want != flags) GameObjectUtility.SetStaticEditorFlags(mr.gameObject, want);
                stats[scene][1]++;
            }
            StaticOcclusionCulling.smallestOccluder = SmallestOccluder;
            StaticOcclusionCulling.smallestHole = SmallestHole;
            StaticOcclusionCulling.backfaceThreshold = BackfaceThreshold;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool done = StaticOcclusionCulling.Compute();
            sw.Stop();
            if (!done) throw new Exception("StaticOcclusionCulling.Compute returned false");
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            var sb = new StringBuilder();
            sb.AppendLine("# Occlusion culling bake (generated)").AppendLine();
            sb.AppendLine("_Generated by `OcclusionBake` (`task unity-occlusion-bake`). Settings: smallest occluder " + SmallestOccluder + " m, smallest hole " + SmallestHole + " m, backface threshold " + BackfaceThreshold + ". Bake time " + sw.Elapsed.TotalSeconds.ToString("0") + " s. Glass, alpha-cut and curtain materials are never occluders. Do not edit by hand._").AppendLine();
            sb.AppendLine("| Scene | Marked occluder (opaque, >= 0.8 m) | Marked occludee (authored static) | Glass/cut kept as non-occluders | Left dynamic (not authored static) |").AppendLine("|---|---|---|---|---|");
            foreach (var kv in stats.OrderBy(k => k.Key)) sb.AppendLine("| " + kv.Key + " | " + kv.Value[0] + " | " + kv.Value[1] + " | " + kv.Value[2] + " | " + kv.Value[3] + " |");
            File.WriteAllText("../docs/qa/quest2_occlusion_bake.md", sb.ToString().Replace("\r\n", "\n"));
            Debug.Log("[OcclusionBake] baked in " + sw.Elapsed.TotalSeconds.ToString("0") + " s");
        }
    }
}
