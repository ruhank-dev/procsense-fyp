using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Relaxed supine resting pose for the PatientBay mannequin. Segments are aimed in WORLD space (hip->knee, knee->ankle, ankle->toe),
    /// so the result does not depend on the rig's local bone axes. Parameters come from Logs/LookUpgrade/pose.json:
    /// {"hipOut":.12,"kneeRise":.10,"calfDrop":.10,"footOut":.35,"footUp":.55,"footBack":.78}. ApplyPreview changes the open scene only
    /// (never saves); ApplySave also saves the scene. Arms, head and the idle animation are left as authored.
    /// </summary>
    public static class PatientPose
    {
        [Serializable] public class Params { public float hipOut = .12f, kneeRise = .10f, calfDrop = .10f, footOut = .35f, footUp = .55f, footBack = .78f, spread = 0f, lower = 0f, headKeep = 1f, handKeep = 1f, headYaw = 0f, rHipOut = -9f, rKneeRise = -9f, rCalfDrop = -9f, rFootOut = -9f, rFootUp = -9f; }

        /// <summary>Authored rest height of the patient root before any sinking (world y).</summary>
        const float RestY = 0.828f;
        const float PillowRestY = 0.785f;
        static string PoseFile => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "LookUpgrade", "pose.json"));

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static void Aim(Transform bone, Transform child, Vector3 desiredWorld)
        {
            var cur = (child.position - bone.position).normalized;
            bone.rotation = Quaternion.FromToRotation(cur, desiredWorld.normalized) * bone.rotation;
        }

        public static void ApplyPreview()
        {
            var p = File.Exists(PoseFile) ? JsonUtility.FromJson<Params>(File.ReadAllText(PoseFile)) : new Params();
            var patient = GameObject.Find("MPFB patient");
            if (patient == null) { Debug.Log("[Pose] patient not found"); return; }
            var head = Find(patient.transform, "head"); var spine1 = Find(patient.transform, "spine_01");
            var headBefore = head != null ? head.position : Vector3.zero;
            var hands = new Dictionary<string, (Transform upper, Transform hand, Vector3 before)>();
            foreach (var side in new[] { "l", "r" })
            {
                var ua = Find(patient.transform, "upperarm_" + side); var hd = Find(patient.transform, "hand_" + side);
                if (ua != null && hd != null) hands[side] = (ua, hd, hd.position);
            }
            patient.transform.position = new Vector3(patient.transform.position.x, RestY - p.lower, patient.transform.position.z);
            if (head != null && spine1 != null && p.headKeep > 0f)
            {
                // the pelvis and legs sink into the mattress; tilt the spine so the head stays on the pillow
                var cur = (head.position - spine1.position).normalized;
                var want = (headBefore - spine1.position).normalized;
                var full = Quaternion.FromToRotation(cur, want);
                spine1.rotation = Quaternion.Slerp(Quaternion.identity, full, p.headKeep) * spine1.rotation;
            }
            if (head != null && Mathf.Abs(p.headYaw) > .01f)       // a slight natural turn of the head on the pillow (axis: neck to crown = world z)
                head.rotation = Quaternion.AngleAxis(p.headYaw, Vector3.forward) * head.rotation;
            // hands keep resting ON the mattress surface: re-aim each arm from the shoulder toward the hand's pre-sinking position
            foreach (var kv in hands)
            {
                var (ua, hd, before) = kv.Value;
                var cur = (hd.position - ua.position).normalized;
                var want = (Vector3.Lerp(hd.position, before, p.handKeep) - ua.position).normalized;
                ua.rotation = Quaternion.FromToRotation(cur, want) * ua.rotation;
            }
            // the pillow sinks with the head (its own compression is already in its mesh)
            var pillow = GameObject.Find("patient_pillow");
            if (pillow != null && head != null)
            {
                var drop = p.lower * (1f - p.headKeep);                    // deterministic, so re-running never moves the pillow again
                pillow.transform.position = new Vector3(pillow.transform.position.x, PillowRestY - Mathf.Max(0f, drop), pillow.transform.position.z);
                Debug.Log($"[Pose] head drop {drop * 100f:F1} cm, pillow y {pillow.transform.position.y:F3}");
            }
            foreach (var s in new[] { "l", "r" })
            {
                var thigh = Find(patient.transform, "thigh_" + s); var calf = Find(patient.transform, "calf_" + s);
                var foot = Find(patient.transform, "foot_" + s); var ball = Find(patient.transform, "ball_" + s);
                if (thigh == null || calf == null || foot == null) { Debug.Log("[Pose] missing leg bones for " + s); continue; }
                // outward = the side of the body this hip is on, in world x
                var outward = Mathf.Sign(thigh.position.x - patient.transform.position.x);
                if (outward == 0) outward = s == "l" ? 1f : -1f;
                // legs run toward -z (the foot end); the knee rises a little and the heel returns to the mattress
                var right = s == "r";
                float hip = right && p.rHipOut > -8f ? p.rHipOut : p.hipOut, rise = right && p.rKneeRise > -8f ? p.rKneeRise : p.kneeRise;
                float drop = right && p.rCalfDrop > -8f ? p.rCalfDrop : p.calfDrop, fout = right && p.rFootOut > -8f ? p.rFootOut : p.footOut;
                float fup = right && p.rFootUp > -8f ? p.rFootUp : p.footUp;
                Aim(thigh, calf, new Vector3(outward * hip, rise, -1f));
                Aim(calf, foot, new Vector3(outward * (hip * .45f + p.spread), -drop, -1f));
                if (ball != null) Aim(foot, ball, new Vector3(outward * fout, fup, -p.footBack));
                Debug.Log($"[Pose] {s}: hip {thigh.position:F3} knee {calf.position:F3} ankle {foot.position:F3} outward {outward}");
            }
        }

        static IEnumerable<(SkinnedMeshRenderer r, Vector3[] w)> BodyVertices(GameObject patient)
        {
            foreach (var r in patient.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!r.enabled || r.sharedMesh == null) continue;
                var baked = new Mesh();
                r.BakeMesh(baked, true);
                var v = baked.vertices;
                var w = new Vector3[v.Length];
                for (var i = 0; i < v.Length; i++) w[i] = r.transform.TransformPoint(v[i]);
                UnityEngine.Object.DestroyImmediate(baked);
                yield return (r, w);
            }
        }

        /// <summary>
        /// Writes the per-region gap between the body's underside and the flat mattress top, and a 1 cm height-field of the body underside
        /// (Logs/LookUpgrade/body_field.json) for the contact depression in the cover.
        /// </summary>
        public static void Report(string reportFile)
        {
            var patient = GameObject.Find("MPFB patient");
            var mattress = GameObject.Find("Soft mattress");
            if (patient == null) return;
            const float top = 0.700f, x0 = -.55f, z0 = -.05f, step = .01f; const int nx = 111, nz = 211;
            var field = new float[nx * nz]; for (var i = 0; i < field.Length; i++) field[i] = -9f;
            var sb = new System.Text.StringBuilder();
            var bands = new SortedDictionary<int, float>();
            var lowest = 9f; string lowestPart = "";
            foreach (var (r, w) in BodyVertices(patient))
            {
                if (!r.name.Contains("body") && !r.name.Contains("Gown") && !r.name.Contains("gown") && !r.name.Contains("shorts") && !r.name.Contains("short")) continue;
                foreach (var p in w)
                {
                    var ix = Mathf.RoundToInt((p.x - x0) / step); var iz = Mathf.RoundToInt((p.z - z0) / step);
                    if (ix < 0 || iz < 0 || ix >= nx || iz >= nz) continue;
                    var k = iz * nx + ix;
                    if (field[k] < -8f || p.y < field[k]) field[k] = p.y;
                    var b = Mathf.FloorToInt(p.z / .10f);
                    if (!bands.TryGetValue(b, out var m) || p.y < m) bands[b] = p.y;
                    if (p.y < lowest) { lowest = p.y; lowestPart = r.name; }
                }
            }
            var pillowGo = GameObject.Find("patient_pillow");
            if (pillowGo != null)
                foreach (var mr in pillowGo.GetComponentsInChildren<MeshRenderer>())
                {
                    var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    foreach (var lv in mf.sharedMesh.vertices)
                    {
                        var p = mr.transform.TransformPoint(lv);
                        var ix = Mathf.RoundToInt((p.x - x0) / step); var iz = Mathf.RoundToInt((p.z - z0) / step);
                        if (ix < 0 || iz < 0 || ix >= nx || iz >= nz) continue;
                        var k = iz * nx + ix;
                        if (field[k] < -8f || p.y < field[k]) field[k] = p.y;
                    }
                }
            foreach (var kv in bands) sb.AppendLine($"z {kv.Key * .1f:F1}..{kv.Key * .1f + .1f:F1}: lowest body y {kv.Value:F3}  gap to flat top {(kv.Value - top) * 100f:F1} cm");
            sb.AppendLine($"overall lowest {lowest:F3} ({lowestPart}); patient root y {patient.transform.position.y:F3}");
            File.WriteAllText(reportFile, sb.ToString());
            var json = "{\"x0\":" + x0 + ",\"z0\":" + z0 + ",\"step\":" + step + ",\"nx\":" + nx + ",\"nz\":" + nz + ",\"minY\":[" + string.Join(",", field.Select(f => f.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))) + "]}";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(PoseFile)!, "body_field.json"), json);
        }

        public static void ApplySave()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            ApplyPreview();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
