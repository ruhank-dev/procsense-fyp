#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The patient's gown through real poses: lying with the legs resting, one knee raised, one leg rotated out, rising, standing, and lying down again. In every state the cloth must stay finite and
    /// no edge may stretch into a bridge across the gap between the thighs (the failure the old tube skirt had). With GOWN_POSE_DIR set it also photographs each state from the foot end and from the side.
    /// </summary>
    public class GownPoseTests
    {
        private const float Dt = 1f / 60f;
        [TearDown] public void Cleanup() => TestHostHygiene.DestroyRoomLeftovers();

        private sealed class Subject
        {
            public GameObject Patient; public GownSecondaryMotion Gown; public SkinnedMeshRenderer Smr; public Dictionary<string, Transform> Bone;
            public (Vector3 pos, Quaternion rot)[] Rest; public Transform[] All; public Vector3 RootPos; public Quaternion RootRot;
            public int[] Tris; public float[] RestEdge; public int[][] Edges;
        }

        private static IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/PatientBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestHostHygiene.WaitForHost(30);
            SimulationHost.Instance.AutoTick = false; SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        private static Subject Prepare()
        {
            var s = new Subject { Patient = GameObject.Find("MPFB patient") };
            s.Patient.GetComponent<PatientIdleView>().enabled = false; s.Patient.GetComponent<PatientIdleView>().RestorePose();
            s.Gown = s.Patient.GetComponent<GownSecondaryMotion>(); s.Gown.enabled = false;
            s.Smr = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            s.All = s.Patient.GetComponentsInChildren<Transform>(true); s.Bone = s.All.GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            s.Rest = s.All.Select(t => (t.localPosition, t.localRotation)).ToArray(); s.RootPos = s.Patient.transform.position; s.RootRot = s.Patient.transform.rotation;
            var mesh = new Mesh(); s.Smr.BakeMesh(mesh, true); var v = mesh.vertices; s.Tris = mesh.triangles;
            var edges = new HashSet<long>(); var list = new List<int[]>();
            for (int i = 0; i < s.Tris.Length; i += 3)
                for (int k = 0; k < 3; k++) { int a = s.Tris[i + k], b = s.Tris[i + (k + 1) % 3]; long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (edges.Add(key)) list.Add(new[] { Mathf.Min(a, b), Mathf.Max(a, b) }); }
            s.Edges = list.ToArray(); s.RestEdge = s.Edges.Select(e => Vector3.Distance(v[e[0]], v[e[1]])).ToArray();
            UnityEngine.Object.Destroy(mesh);
            return s;
        }

        private static void Restore(Subject s)
        {
            for (int i = 0; i < s.All.Length; i++) { s.All[i].localPosition = s.Rest[i].pos; s.All[i].localRotation = s.Rest[i].rot; }
            s.Patient.transform.SetPositionAndRotation(s.RootPos, s.RootRot); s.Gown.SurfaceY = .70f;
        }

        private static void Settle(Subject s, int frames = 90) { for (int i = 0; i < frames; i++) s.Gown.Step(Dt); }

        /// <summary>Largest edge (m) and the largest stretch of any edge against the rest pose, over the skinned gown as it is drawn now.</summary>
        private static (float longest, float stretch, bool finite) Measure(Subject s)
        {
            var m = new Mesh(); s.Smr.BakeMesh(m, true); var v = m.vertices; float longest = 0, stretch = 0; bool finite = true;
            foreach (var p in v) if (float.IsNaN(p.x + p.y + p.z) || Mathf.Abs(p.x) > 20f) finite = false;
            for (int i = 0; i < s.Edges.Length; i++)
            {
                float d = Vector3.Distance(v[s.Edges[i][0]], v[s.Edges[i][1]]); longest = Mathf.Max(longest, d);
                if (s.RestEdge[i] > .02f) stretch = Mathf.Max(stretch, d / s.RestEdge[i]);
            }
            UnityEngine.Object.Destroy(m); return (longest, stretch, finite);
        }

        private static void RaiseKnee(Subject s, string side, float hip, float knee)
        {
            var thigh = s.Bone["thigh_" + side]; var calf = s.Bone["calf_" + side];
            thigh.rotation = Quaternion.AngleAxis(hip, Vector3.right) * thigh.rotation;
            calf.rotation = Quaternion.AngleAxis(-knee, Vector3.right) * calf.rotation;
        }

        private static void RotateLeg(Subject s, string side, float abduct, float twist)
        {
            var thigh = s.Bone["thigh_" + side]; var calf = s.Bone["calf_" + side];
            float outward = thigh.position.x > s.Bone["pelvis"].position.x ? -1f : 1f;
            thigh.rotation = Quaternion.AngleAxis(outward * abduct, Vector3.up) * thigh.rotation;
            thigh.rotation = Quaternion.AngleAxis(twist, (calf.position - thigh.position).normalized) * thigh.rotation;
        }

        /// <summary>Tips the whole patient from lying (0) to upright (1) about the feet while they slide to the bedside; upright, they are stood on the floor.</summary>
        private static void Rise(Subject s, float t)
        {
            var root = s.Patient.transform; root.SetPositionAndRotation(s.RootPos, s.RootRot);
            var feet = (s.Bone["foot_l"].position + s.Bone["foot_r"].position) * .5f;
            root.RotateAround(feet, Vector3.right, -90f * t);
            root.position += new Vector3(Mathf.Lerp(0, .9f - feet.x, t), 0, Mathf.Lerp(0, .05f - feet.z, t));
            root.RotateAround((s.Bone["foot_l"].position + s.Bone["foot_r"].position) * .5f, Vector3.up, -90f * t);     // turns to face the doorway
            if (t >= .99f)
            {
                var bounds = s.Smr.bounds; foreach (var r in s.Patient.GetComponentsInChildren<SkinnedMeshRenderer>()) bounds.Encapsulate(r.bounds);
                root.position += Vector3.down * bounds.min.y;
            }
            s.Gown.SurfaceY = t >= .6f ? 0f : .70f;
        }

        private static IEnumerator Photograph(string dir, string name, Subject s, Func<Subject, (Vector3 pos, Vector3 look)> front, Func<Subject, (Vector3 pos, Vector3 look)> side)
        {
            if (string.IsNullOrEmpty(dir)) yield break;
            foreach (var view in new[] { ("front", front(s)), ("side", side(s)) })
            {
                var go = new GameObject("PoseCamera"); var cam = go.AddComponent<Camera>(); cam.transform.position = view.Item2.pos; cam.transform.LookAt(view.Item2.look); cam.fieldOfView = name.StartsWith("5") || name.StartsWith("6") ? 60f : 46f;
                cam.nearClipPlane = .02f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .12f, .14f);
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
                go.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None;
                yield return null; cam.Render(); yield return null; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"{name}_{view.Item1}.png"), tex.EncodeToPNG()); RenderTexture.active = prev; cam.targetTexture = null;
                UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
            }
        }

        [UnityTest]
        public IEnumerator Gown_stays_whole_and_unbridged_through_lying_knee_leg_rotation_standing_and_lying_again()
        {
            yield return Load();
            var s = Prepare(); var dir = Environment.GetEnvironmentVariable("GOWN_POSE_DIR"); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var report = new StringBuilder(); var restM = Measure(s);
            report.AppendLine($"rest: longest edge {restM.longest:F3} m");
            Func<Subject, (Vector3, Vector3)> lyingFront = p => (new Vector3(0f, 1.15f, -.55f), new Vector3(0f, .85f, .8f));
            Func<Subject, (Vector3, Vector3)> lyingSide = p => (new Vector3(-1.05f, 1.0f, .62f), new Vector3(0f, .8f, .62f));
            Func<Subject, (Vector3, Vector3)> standFront = p => (new Vector3(2.35f, 1.15f, .05f), new Vector3(.9f, .95f, .05f));
            Func<Subject, (Vector3, Vector3)> standSide = p => (new Vector3(.9f, 1.15f, -1.3f), new Vector3(.9f, .95f, .05f));
            var states = new List<(string name, Action pose, bool standing, bool mid)>
            {
                ("1_lying", () => { }, false, false),
                ("2_legs_resting", () => { Settle(s, 240); }, false, false),
                ("3_knee_raised", () => RaiseKnee(s, "r", 62f, 92f), false, false),
                ("4_leg_rotated", () => { Restore(s); RotateLeg(s, "l", 24f, 38f); }, false, false),
                ("5_rising", () => { Restore(s); Rise(s, .5f); }, false, true),
                ("6_standing", () => { Restore(s); Rise(s, 1f); }, true, false),
                ("7_lying_again", () => Restore(s), false, false),
            };
            float worstStretch = 0, worstLongest = 0; var failures = new List<(string name, (float longest, float stretch, bool finite) m)>();
            foreach (var st in states)
            {
                st.pose(); Settle(s, st.name == "2_legs_resting" ? 30 : 100);
                var m = Measure(s); worstStretch = Mathf.Max(worstStretch, m.stretch); worstLongest = Mathf.Max(worstLongest, m.longest);
                report.AppendLine($"{st.name}: longest edge {m.longest:F3} m, max stretch x{m.stretch:F2}, finite {m.finite}, max node lag {s.Gown.MaxLag():F3} m");
                yield return Photograph(dir, st.name, s, st.standing || st.mid ? standFront : lyingFront, st.standing || st.mid ? standSide : lyingSide);
                failures.Add((st.name, m));
            }
            report.AppendLine($"worst over all poses: longest edge {worstLongest:F3} m, stretch x{worstStretch:F2}");
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            foreach (var f in failures)
            {
                Assert.IsTrue(f.m.finite, f.name + ": every gown vertex is finite");
                Assert.Less(f.m.longest, .34f, f.name + ": no edge stretches into a bridge");
                Assert.Less(f.m.stretch, 4.2f, f.name + ": no cloth stretches more than the pose can explain");
            }
        }
    }
}
#endif
