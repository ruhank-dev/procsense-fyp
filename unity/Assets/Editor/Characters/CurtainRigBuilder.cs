using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Turns the three static curtains (two window curtains and the bed privacy curtain) into skinned meshes driven by CurtainCloth: a 14 x 3 grid of secondary bones per curtain, vertex weights blended
    /// bilinearly between the two nearest rows and columns. Skinned meshes are not lightmapped, so the curtains switch from baked to light-probe lighting. The original mesh is kept on the
    /// component and Remove() restores the static curtain.
    /// </summary>
    public static class CurtainRigBuilder
    {
        const int Cols = 14;
        static readonly float[] RowH = { .06f, .5f, .94f };            // normalised height from the track (0) to the hem (1)
        static readonly float[] RowLoose = { .05f, .5f, 1f };
        const string Out = "Assets/Models/Environment/ReferenceProps/Curtains";

        static IEnumerable<MeshRenderer> Targets() => UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => r.name == "Gathered sage curtain" || r.name == "EQ_privacy_curtain_M_Curtain").OrderBy(r => r.bounds.center.z);

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(Out)) { Directory.CreateDirectory(Out); AssetDatabase.Refresh(); }
            int n = 0;
            foreach (var r in Targets().ToArray()) { Rig(r, n++); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[CurtainRig] rigged {n} curtains");
        }

        static void EnsureReadable(MeshFilter mf)
        {
            if (mf.sharedMesh.isReadable) return;
            var path = AssetDatabase.GetAssetPath(mf.sharedMesh);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) throw new InvalidOperationException("mesh not readable and not an imported model: " + path);
            imp.isReadable = true; imp.SaveAndReimport();
        }

        static void Rig(MeshRenderer r, int index)
        {
            var go = r.gameObject; var mf = go.GetComponent<MeshFilter>(); if (mf == null) return;
            EnsureReadable(mf);
            var src = mf.sharedMesh; if (!src.isReadable) throw new InvalidOperationException("still not readable: " + go.name);
            var verts = src.vertices; var t = go.transform;
            var wv = verts.Select(v => t.TransformPoint(v)).ToArray();
            var b = r.bounds;
            float minZ = b.min.z, maxZ = b.max.z, top = b.max.y, bot = b.min.y, width = maxZ - minZ, height = top - bot;
            bool gatherPlusZ = Mathf.Abs(maxZ) > Mathf.Abs(minZ);                      // gathered end = the end farther from the room centre
            Func<float, float> col = z => gatherPlusZ ? (maxZ - z) / width : (z - minZ) / width;
            var gatherWorld = gatherPlusZ ? Vector3.forward : Vector3.back;
            var gatherLocal = t.InverseTransformDirection(gatherWorld);
            var bones = new List<Transform>(); var bind = new List<Matrix4x4>(); var nodes = new List<CurtainCloth.Node>();
            var rigRoot = new GameObject("Cloth bones " + index).transform; rigRoot.SetParent(t, false);
            // bones are created in the world-aligned frame at rest; rotation follows the curtain object
            var gridPos = new Vector3[Cols, RowH.Length];
            for (int c = 0; c < Cols; c++)
                for (int k = 0; k < RowH.Length; k++)
                {
                    float cc = (c + .5f) / Cols;
                    float z = gatherPlusZ ? maxZ - cc * width : minZ + cc * width;
                    var wp = new Vector3(b.center.x, top - RowH[k] * height, z);
                    var bone = new GameObject($"cloth_{c:00}_{k}").transform; bone.SetParent(rigRoot, false); bone.position = wp; bone.rotation = t.rotation;
                    bones.Add(bone); nodes.Add(new CurtainCloth.Node { Bone = bone, RestLocal = t.InverseTransformPoint(wp), Column = cc, Looseness = RowLoose[k] });
                    gridPos[c, k] = wp;
                }
            var weights = new BoneWeight[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float cc = Mathf.Clamp01(col(wv[i].z)) * Cols - .5f; int c0 = Mathf.Clamp(Mathf.FloorToInt(cc), 0, Cols - 1), c1 = Mathf.Min(c0 + 1, Cols - 1); float fc = Mathf.Clamp01(cc - c0); if (cc < 0) { c0 = c1 = 0; fc = 0; }
                float hh = Mathf.Clamp01((top - wv[i].y) / height); int k0 = 0; while (k0 < RowH.Length - 2 && hh > RowH[k0 + 1]) k0++; int k1 = k0 + 1;
                float fk = Mathf.Clamp01((hh - RowH[k0]) / (RowH[k1] - RowH[k0]));
                (int, float)[] ws = { (c0 * RowH.Length + k0, (1 - fc) * (1 - fk)), (c1 * RowH.Length + k0, fc * (1 - fk)), (c0 * RowH.Length + k1, (1 - fc) * fk), (c1 * RowH.Length + k1, fc * fk) };
                weights[i] = new BoneWeight { boneIndex0 = ws[0].Item1, weight0 = ws[0].Item2, boneIndex1 = ws[1].Item1, weight1 = ws[1].Item2, boneIndex2 = ws[2].Item1, weight2 = ws[2].Item2, boneIndex3 = ws[3].Item1, weight3 = ws[3].Item2 };
            }
            var mats = r.sharedMaterials; var flags = GameObjectUtility.GetStaticEditorFlags(go);
            var copy = UnityEngine.Object.Instantiate(src); copy.name = go.name.Replace(' ', '_') + "_" + index + "_skinned";
            var path = Out + "/" + copy.name + ".asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(copy, path);
            UnityEngine.Object.DestroyImmediate(r); UnityEngine.Object.DestroyImmediate(mf);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMaterials = mats; smr.bones = bones.ToArray(); smr.rootBone = rigRoot;
            foreach (var bn in bones) bind.Add(bn.worldToLocalMatrix * t.localToWorldMatrix);
            copy.boneWeights = weights; copy.bindposes = bind.ToArray();
            smr.sharedMesh = copy; smr.updateWhenOffscreen = true; smr.localBounds = new Bounds(copy.bounds.center, copy.bounds.size + Vector3.one * .4f);
            smr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes; smr.receiveShadows = true;
            var cloth = go.AddComponent<CurtainCloth>(); cloth.Configure(nodes.ToArray(), gatherLocal, width, src);
        }

        public static void Remove()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            foreach (var cc in UnityEngine.Object.FindObjectsByType<CurtainCloth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = cc.gameObject; var smr = go.GetComponent<SkinnedMeshRenderer>(); var mats = smr.sharedMaterials; var mesh = cc.OriginalMesh;
                var bones = go.transform.Cast<Transform>().Where(x => x.name.StartsWith("Cloth bones")).ToArray();
                UnityEngine.Object.DestroyImmediate(cc); UnityEngine.Object.DestroyImmediate(smr);
                foreach (var bn in bones) UnityEngine.Object.DestroyImmediate(bn.gameObject);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[CurtainRig] static curtains restored");
        }
    }
}
