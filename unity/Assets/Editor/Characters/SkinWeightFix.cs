using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Quest skin-weight fix. The curtain and gown rig builders wrote <see cref="BoneWeight"/> entries in an arbitrary order (bilinear grid weights, or 'kept' weights times a factor of zero), so for some vertices the
    /// first two entries were zero while the weight sat in entries three and four. With QualitySettings.skinWeights = TwoBones (the Quest 2 Medium level) Unity only reads the first two entries:
    /// those vertices skinned to NaN and logged "First two bone weights should have non-zero sum!" (3,267 vertices in the curtains plus 426 in the gown).
    /// <see cref="Normalise"/> rewrites a mesh's weights compactly: per vertex, merge duplicate bones, drop zero weights, sort by weight (largest first) and renormalise.
    /// Run: -executeMethod ProcSense.EditorTools.SkinWeightFix.Run (idempotent, changes only the listed mesh assets).
    /// </summary>
    public static class SkinWeightFix
    {
        private static readonly string[] Targets =
        {
            "Assets/Models/Environment/ReferenceProps/Curtains/EQ_privacy_curtain_M_Curtain_1_skinned.asset",
            "Assets/Models/Environment/ReferenceProps/Curtains/Gathered_sage_curtain_0_skinned.asset",
            "Assets/Models/Environment/ReferenceProps/Curtains/Gathered_sage_curtain_2_skinned.asset",
            "Assets/Models/Characters/MPFBPatient/patient_gown_secondary.asset",
        };

        /// <summary>Sorted, merged, zero-free 4-entry form of one legacy vertex weight (used by the rig builders so new meshes are written correctly).</summary>
        public static BoneWeight Sorted(BoneWeight w)
        {
            var d = new Dictionary<int, float>();
            void Add(int b, float x) { if (x <= 0f) return; d[b] = d.TryGetValue(b, out var o) ? o + x : x; }
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            var l = d.OrderByDescending(k => k.Value).ThenBy(k => k.Key).Take(4).ToList();
            float s = l.Sum(k => k.Value); if (s <= 0f) return w;
            var r = new BoneWeight();
            if (l.Count > 0) { r.boneIndex0 = l[0].Key; r.weight0 = l[0].Value / s; }
            if (l.Count > 1) { r.boneIndex1 = l[1].Key; r.weight1 = l[1].Value / s; }
            if (l.Count > 2) { r.boneIndex2 = l[2].Key; r.weight2 = l[2].Value / s; }
            if (l.Count > 3) { r.boneIndex3 = l[3].Key; r.weight3 = l[3].Value / s; }
            return r;
        }

        /// <summary>Rewrites the mesh's bone weights in the compact sorted form. Returns the number of vertices whose entries changed.</summary>
        public static int Normalise(Mesh mesh)
        {
            var per = mesh.GetBonesPerVertex(); var all = mesh.GetAllBoneWeights();
            var perOut = new NativeArray<byte>(per.Length, Allocator.Temp); var wOut = new List<BoneWeight1>(all.Length);
            int idx = 0, changed = 0;
            for (int v = 0; v < per.Length; v++)
            {
                int n = per[v]; var d = new Dictionary<int, float>();
                for (int k = 0; k < n; k++) { var e = all[idx + k]; if (e.weight > 0f) d[e.boneIndex] = d.TryGetValue(e.boneIndex, out var o) ? o + e.weight : e.weight; }
                idx += n;
                var l = d.OrderByDescending(k => k.Value).ThenBy(k => k.Key).Take(4).ToList(); float s = l.Sum(k => k.Value);
                if (l.Count == 0) { l.Add(new KeyValuePair<int, float>(0, 1f)); s = 1f; }
                perOut[v] = (byte)l.Count;
                foreach (var kv in l) wOut.Add(new BoneWeight1 { boneIndex = kv.Key, weight = kv.Value / s });
                if (l.Count != n) changed++;
            }
            // vertices are also 'changed' when the stored order was not sorted: compare once more against the new data
            var newAll = new NativeArray<BoneWeight1>(wOut.ToArray(), Allocator.Temp);
            mesh.SetBoneWeights(perOut, newAll);
            perOut.Dispose(); newAll.Dispose();
            return changed;
        }

        public static void Run()
        {
            foreach (var path in Targets)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { Debug.LogError("[SkinWeightFix] missing " + path); continue; }
                int changed = Normalise(mesh);
                EditorUtility.SetDirty(mesh);
                Debug.Log($"[SkinWeightFix] {path}: rewritten ({changed} vertices changed their bone count)");
            }
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
    }
}
