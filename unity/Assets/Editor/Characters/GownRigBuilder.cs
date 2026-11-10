using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// One-time authoring of the gown's secondary-motion rig. The patient is put in the saved rest pose, the gown is baked to read each vertex's world position, vertices are given a
    /// "looseness" (0 at chest and shoulders, rising down the skirt and along the sleeves), the loose vertices are clustered into ~26 points, and each point gets a secondary skin bone.
    /// Each secondary bone has the same bind pose as the original bone that carries it, so at rest the skinning is identical to the accepted fit; only motion differs.
    /// The original mesh and bone list are kept on the component, so Remove() restores the accepted gown exactly. Do not run PatientIdleSetup.UpdateSurfaces afterwards without Remove() first.
    /// </summary>
    public static class GownRigBuilder
    {
        const string MeshPath = "Assets/Models/Characters/MPFBPatient/patient_gown_secondary.asset";
        const int K = 26;

        static float Smooth(float a, float b, float x) { var t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        public static void Build()
        {
            Remove();                                                                       // opens PatientBay itself and restores any earlier rig; the scene handle below must be taken afterwards
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("MPFB patient");
            if (patient == null) throw new InvalidOperationException("patient not found");
            patient.GetComponent<PatientIdleView>()?.RestorePose();
            var smr = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            var mesh = smr.sharedMesh; var bones = smr.bones;
            if (!mesh.isReadable) throw new InvalidOperationException("gown mesh is not readable");
            var baked = new Mesh(); smr.BakeMesh(baked, false);
            var wv = baked.vertices.Select(v => smr.transform.TransformPoint(v)).ToArray();
            var bw = mesh.boneWeights;
            var byName = patient.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            Transform B(string n) => byName[n];
            var pelvis = B("pelvis"); var head = B("head");
            var axis = (head.position - pelvis.position).normalized;

            int Dom(BoneWeight w) { float m = w.weight0; int i = w.boneIndex0; if (w.weight1 > m) { m = w.weight1; i = w.boneIndex1; } if (w.weight2 > m) { m = w.weight2; i = w.boneIndex2; } if (w.weight3 > m) { i = w.boneIndex3; } return i; }
            var f = new float[wv.Length]; var dom = new int[wv.Length];
            for (int i = 0; i < wv.Length; i++)
            {
                dom[i] = Dom(bw[i]); var n = bones[dom[i]].name;
                float d = Vector3.Dot(pelvis.position - wv[i], axis);                      // metres toward the feet from the pelvis
                if (n.StartsWith("lowerarm") || n.StartsWith("hand")) f[i] = .70f;
                else if (n.StartsWith("upperarm")) f[i] = .22f;
                else if (n.StartsWith("pelvis") || n.StartsWith("thigh") || n.StartsWith("calf") || n.StartsWith("spine_01")) f[i] = Smooth(.03f, .30f, d);
            }
            // the strip of cloth that sags between the thighs follows the pelvis, not either thigh: a raised knee must not drag the centre of the gown across the gap
            int pelvisIdx = Array.IndexOf(bones, pelvis);
            var gap = new bool[wv.Length];
            for (int i = 0; i < wv.Length; i++)
            {
                var local = pelvis.InverseTransformPoint(wv[i]); float lateral = Mathf.Abs(Vector3.Dot(wv[i] - pelvis.position, Vector3.Cross(axis, Vector3.up).normalized));
                float towardFeet = Vector3.Dot(pelvis.position - wv[i], axis);
                gap[i] = pelvisIdx >= 0 && f[i] > .08f && lateral < .085f && towardFeet > .02f && wv[i].y < .88f;
                if (gap[i]) dom[i] = pelvisIdx;
            }
            var free = Enumerable.Range(0, wv.Length).Where(i => f[i] > .08f).ToArray();
            if (free.Length < K) throw new InvalidOperationException("too few loose vertices: " + free.Length);
            // k-means, farthest-point initialisation
            var cen = new List<Vector3> { wv[free[0]] };
            while (cen.Count < K) { int best = free[0]; float bd = -1; foreach (var i in free) { float d = cen.Min(c => (c - wv[i]).sqrMagnitude); if (d > bd) { bd = d; best = i; } } cen.Add(wv[best]); }
            var asg = new int[wv.Length];
            for (int it = 0; it < 10; it++)
            {
                foreach (var i in free) { float bd = float.MaxValue; for (int c = 0; c < K; c++) { float d = (cen[c] - wv[i]).sqrMagnitude; if (d < bd) { bd = d; asg[i] = c; } } }
                for (int c = 0; c < K; c++) { var m = free.Where(i => asg[i] == c).ToArray(); if (m.Length > 0) cen[c] = m.Aggregate(Vector3.zero, (a, i) => a + wv[i]) / m.Length; }
            }
            var root = new GameObject("Gown secondary").transform; root.SetParent(patient.transform, false);
            var newBones = bones.ToList(); var newBind = mesh.bindposes.ToList(); var nodes = new List<GownSecondaryMotion.Node>();
            for (int c = 0; c < K; c++)
            {
                var m = free.Where(i => asg[i] == c).ToArray(); if (m.Length == 0) { m = new[] { free[0] }; }
                int domIdx = m.GroupBy(i => dom[i]).OrderByDescending(g => g.Count()).First().Key;
                var driver = bones[domIdx];
                var go = new GameObject($"gown_secondary_{c:00}"); go.transform.SetParent(root, false); go.transform.position = driver.position; go.transform.rotation = driver.rotation;
                var ls = driver.lossyScale; var rs = root.lossyScale; go.transform.localScale = new Vector3(ls.x / rs.x, ls.y / rs.y, ls.z / rs.z);   // the imported skeleton carries a unit-conversion scale; the secondary bone must reproduce the driver's skinning matrix exactly
                newBones.Add(go.transform); newBind.Add(mesh.bindposes[domIdx]);
                nodes.Add(new GownSecondaryMotion.Node { Bone = go.transform, Driver = driver, DriverLocalPoint = driver.InverseTransformPoint(cen[c]), Looseness = Mathf.Clamp(m.Average(i => f[i]), .12f, 1f) });
            }
            var nbw = (BoneWeight[])bw.Clone();
            foreach (var i in free)
            {
                var w = bw[i]; var arr = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) }.OrderByDescending(t => t.Item2).ToArray();
                if (gap[i]) arr = new[] { (pelvisIdx, 1f), (arr[0].Item1 == pelvisIdx ? arr[1].Item1 : arr[0].Item1, 0f), (0, 0f), (0, 0f) };
                float s = arr[0].Item2 + arr[1].Item2; if (s < 1e-5f) continue;
                var near = Enumerable.Range(0, K).OrderBy(c => (cen[c] - wv[i]).sqrMagnitude).Take(2).ToArray();
                float d0 = 1f / (.003f + (cen[near[0]] - wv[i]).magnitude), d1 = 1f / (.003f + (cen[near[1]] - wv[i]).magnitude); float ds = d0 + d1;
                float keep = 1f - f[i];
                nbw[i] = ProcSense.EditorTools.SkinWeightFix.Sorted(new BoneWeight
                {
                    boneIndex0 = arr[0].Item1, weight0 = keep * arr[0].Item2 / s, boneIndex1 = arr[1].Item1, weight1 = keep * arr[1].Item2 / s,
                    boneIndex2 = bones.Length + near[0], weight2 = f[i] * d0 / ds, boneIndex3 = bones.Length + near[1], weight3 = f[i] * d1 / ds,
                });
            }
            var m2 = UnityEngine.Object.Instantiate(mesh); m2.name = "patient_gown_secondary";
            m2.boneWeights = nbw; m2.bindposes = newBind.ToArray();
            AssetDatabase.DeleteAsset(MeshPath); AssetDatabase.CreateAsset(m2, MeshPath);
            var capsules = new List<GownSecondaryMotion.Capsule>();
            foreach (var side in new[] { "l", "r" })
            {
                capsules.Add(new GownSecondaryMotion.Capsule { A = B("thigh_" + side), B = B("calf_" + side), Radius = .092f });
                capsules.Add(new GownSecondaryMotion.Capsule { A = B("calf_" + side), B = B("foot_" + side), Radius = .060f });
                capsules.Add(new GownSecondaryMotion.Capsule { A = B("upperarm_" + side), B = B("lowerarm_" + side), Radius = .052f });
                capsules.Add(new GownSecondaryMotion.Capsule { A = B("lowerarm_" + side), B = B("hand_" + side), Radius = .040f });
            }
            capsules.Add(new GownSecondaryMotion.Capsule { A = pelvis, B = B("spine_02"), Radius = .15f });
            var comp = patient.AddComponent<GownSecondaryMotion>();
            comp.Configure(nodes.ToArray(), capsules.ToArray(), .70f, mesh, bones);
            smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath); smr.bones = newBones.ToArray(); smr.updateWhenOffscreen = true;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[GownRig] built: {free.Length} loose vertices of {wv.Length}, {K} secondary bones, {capsules.Count} capsules");
        }

        public static void Remove()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("MPFB patient"); var comp = patient.GetComponent<GownSecondaryMotion>(); if (comp == null) return;
            var smr = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = comp.OriginalMesh; smr.bones = comp.OriginalBones;
            UnityEngine.Object.DestroyImmediate(comp);
            var root = patient.transform.Find("Gown secondary"); if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[GownRig] removed; accepted gown restored");
        }
    }
}
