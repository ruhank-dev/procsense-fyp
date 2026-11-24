using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// The patients lay with their hip joint 2.5 cm above the mattress, which put the back, buttocks and shoulders 13 to 19 cm inside it. This lifts each lying patient until the back of the torso rests on the mattress
    /// (1.5 cm of give), then re-aims the legs and arms at the mattress so nothing floats. It measures the skinned mesh, not the bones, and reports the worst vertex below the surface per body part.
    /// Idempotent: run it again and nothing moves. Menu ProcSense > Characters > Fit patients to the bed. Batch: -executeMethod ProcSense.EditorTools.PatientBedFit.Run (Unity closed).
    /// </summary>
    public static class PatientBedFit
    {
        public const float Give = .015f;

        /// <summary>Only the pillow: the puffy one replaces the flat grey slab, at the height the heads were fitted to. The bodies are not touched.</summary>
        [MenuItem("ProcSense/Characters/Replace bed pillow")]
        public static void PillowOnly()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var m = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.gameObject.activeInHierarchy && r.name == "Soft mattress").bounds;
            var oldPillow = GameObject.Find("patient_pillow"); if (oldPillow != null) { oldPillow.SetActive(false); EditorUtility.SetDirty(oldPillow); }
            var b = Pillow(m.max.y, m.center.x); Debug.Log($"[PatientBedFit] pillow top {b.max.y:F3}");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("ProcSense/Characters/Fit patients to the bed")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var mattress = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy && r.name == "Soft mattress").ToList();
            if (mattress.Count == 0) { Debug.LogError("[PatientBedFit] no mattress"); return; }
            var mb = mattress[0].bounds; float top = mb.max.y, cx = mb.center.x;
            var profiles = Object.FindObjectsByType<ProcSense.VR.Views.PatientMotion>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(m => m.gameObject).Distinct().ToList();
            var oldPillow = GameObject.Find("patient_pillow"); if (oldPillow != null) { oldPillow.SetActive(false); EditorUtility.SetDirty(oldPillow); }   // the flat grey slab that was here before: replaced by the puffy one
            var pillow = Pillow(top, cx);
            foreach (var go in profiles) { Fit(go, top, cx); FitHead(go, cx, pillow.max.y); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private const string PillowName = "Bed pillow";

        /// <summary>A puffy pillow: a flattened superellipsoid 62 x 40 x 10 cm, softly pinched at the seams, with a shallow dip in the middle where a head rests. Saved as an asset.</summary>
        private static Mesh PillowMesh()
        {
            const string path = "Assets/Models/Environment/Bed_Pillow.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) AssetDatabase.DeleteAsset(path);
            const int U = 40, V = 24; float a = .31f, b = .20f, c = .052f;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            System.Func<float, float, float> sp = (x, e) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);
            for (int j = 0; j <= V; j++)
                for (int i = 0; i <= U; i++)
                {
                    float u = i / (float)U * Mathf.PI * 2, v = (j / (float)V - .5f) * Mathf.PI;
                    float cv = Mathf.Cos(v), sv = Mathf.Sin(v), cu = Mathf.Cos(u), su = Mathf.Sin(u);
                    float x = a * sp(cv, .55f) * sp(cu, .6f), y = b * sp(cv, .55f) * sp(su, .6f), z = c * sp(sv, .75f);
                    float dip = z > 0 ? -.012f * Mathf.Exp(-(x * x / (a * a * .25f) + y * y / (b * b * .35f))) : 0f;
                    verts.Add(new Vector3(x, z + dip, y)); uvs.Add(new Vector2(i / (float)U, j / (float)V));
                }
            for (int j = 0; j < V; j++)
                for (int i = 0; i < U; i++)
                {
                    int k = j * (U + 1) + i; tris.Add(k); tris.Add(k + U + 1); tris.Add(k + 1); tris.Add(k + 1); tris.Add(k + U + 1); tris.Add(k + U + 2);
                }
            var m = new Mesh { name = "Bed pillow", indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
            if (!AssetDatabase.IsValidFolder("Assets/Models/Environment")) AssetDatabase.CreateFolder("Assets/Models", "Environment");
            AssetDatabase.CreateAsset(m, path); return m;
        }

        /// <summary>A pillow at the head of the bed (the hair and the back of the head were inside the mattress). Soft white box, no collider, no shadows; hidden by <see cref="ProcSense.VR.Views.BedBackrest"/> while the patient sits.</summary>
        private static Bounds Pillow(float top, float cx)
        {
            var o = GameObject.Find(PillowName);
            if (o == null)
            {
                o = new GameObject(PillowName); o.AddComponent<MeshFilter>().sharedMesh = PillowMesh(); o.AddComponent<MeshRenderer>();
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Bed_Pillow.mat");
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.93f, .95f, .96f) }; mat.SetFloat("_Smoothness", .2f); AssetDatabase.CreateAsset(mat, "Assets/Materials/Environment/Bed_Pillow.mat"); }
                o.GetComponent<Renderer>().sharedMaterial = mat;
            }
            o.transform.SetPositionAndRotation(new Vector3(cx, top + .056f, 1.70f), Quaternion.identity); o.transform.localScale = Vector3.one; o.GetComponent<MeshFilter>().sharedMesh = PillowMesh();
            var r = o.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var br = Object.FindAnyObjectByType<ProcSense.VR.Views.BedBackrest>(FindObjectsInactive.Include); if (br != null) { br.Pillow = o.transform; EditorUtility.SetDirty(br); }
            EditorUtility.SetDirty(o);
            return r.bounds;
        }

        /// <summary>Lowest vertex of the head and hair (behind the ears and up), metres.</summary>
        private static float HeadMinY(GameObject go, float cx)
        {
            var head = Bone(go, "Bip01 Head"); if (head == null) return float.NaN; float min = float.MaxValue;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null || r.name == PatientGowns.GownName) continue; var mesh = new Mesh(); r.BakeMesh(mesh, true); var l2w = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices) { var w = l2w.MultiplyPoint3x4(v); if (w.z >= head.position.z - .06f && Mathf.Abs(w.x - cx) < .2f && w.y < min) min = w.y; }
                Object.DestroyImmediate(mesh);
            }
            return min == float.MaxValue ? float.NaN : min;
        }

        /// <summary>Bends the neck until the head bone sits at the height a head resting on the pillow has (the skull's base joint rests about 3 cm above the pillow top). The bone is used, not the lowest vertex, because long hair hangs below the head and would never match.
        /// Safe to run again: a head already within 1.5 cm is left alone.</summary>
        private static void FitHead(GameObject go, float cx, float pillowTop)
        {
            var neck = Bone(go, "Bip01 Neck"); var head = Bone(go, "Bip01 Head"); var l = Bone(go, "Bip01 L UpperArm"); var rr = Bone(go, "Bip01 R UpperArm"); if (neck == null || head == null || l == null || rr == null) return;
            bool was = go.activeSelf; go.SetActive(true);
            var axis = (rr.position - l.position).normalized; var rest = neck.rotation; float target = pillowTop + .03f;
            float now = head.position.y;
            if (Mathf.Abs(now - target) < .02f) { go.SetActive(was); return; }
            float best = 0, bestErr = float.MaxValue;
            for (float a = -25; a <= 25; a += 1)
            {
                neck.rotation = Quaternion.AngleAxis(a, axis) * rest; float err = Mathf.Abs(head.position.y - target);
                if (err < bestErr - 1e-4f) { bestErr = err; best = a; }
            }
            neck.rotation = Quaternion.AngleAxis(best, axis) * rest;
            Debug.Log($"[PatientBedFit] {go.name}: neck bent {best:F0} degrees, head at {head.position.y:F3}, wanted {target:F3}");
            go.SetActive(was); EditorUtility.SetDirty(go);
        }

        private static Transform Bone(GameObject go, string name) => go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        /// <summary>Back of the torso (lowest vertex between the hips and the shoulders, central strip), metres; NaN when there is no mesh.</summary>
        public static float TorsoMinY(GameObject go, float cx)
        {
            var pelvis = Bone(go, "Bip01 Pelvis"); var head = Bone(go, "Bip01 Head"); if (pelvis == null || head == null) return float.NaN;
            float z0 = pelvis.position.z - .1f, z1 = head.position.z - .25f, min = float.MaxValue;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null || r.name == PatientGowns.GownName) continue;
                var mesh = new Mesh(); r.BakeMesh(mesh, true);
                var l2w = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices) { var w = l2w.MultiplyPoint3x4(v); if (w.z >= z0 && w.z <= z1 && Mathf.Abs(w.x - cx) < .25f && w.y < min) min = w.y; }
                Object.DestroyImmediate(mesh);
            }
            return min == float.MaxValue ? float.NaN : min;
        }

        public static void Fit(GameObject go, float top, float cx)
        {
            bool was = go.activeSelf; go.SetActive(true);
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
            for (int i = 0; i < 4; i++)
            {
                float y = TorsoMinY(go, cx); if (float.IsNaN(y)) break;
                float delta = top - Give - y;
                Debug.Log($"[PatientBedFit] {go.name} pass {i}: torso back at {y:F3}, mattress {top:F3}, move {delta:F3}");
                if (Mathf.Abs(delta) < .004f) break;
                go.transform.position += Vector3.up * delta;
                Aim(go, top, cx);
            }
            go.SetActive(was);
            EditorUtility.SetDirty(go);
        }

        /// <summary>Legs together and arms along the sides, their joints a limb-radius above the mattress, from wherever the hips are now.</summary>
        private static void Aim(GameObject go, float top, float cx)
        {
            var pelvis = Bone(go, "Bip01 Pelvis");
            foreach (var s in new[] { "L", "R" })
            {
                var thigh = Bone(go, $"Bip01 {s} Thigh"); var foot = Bone(go, $"Bip01 {s} Foot");
                if (thigh != null && foot != null)
                {
                    float len = (foot.position - thigh.position).magnitude, side = foot.position.x >= pelvis.position.x ? 1f : -1f;
                    var target = new Vector3(cx + side * .10f, top + .085f, pelvis.position.z - len * .98f);   // the ankle joint sits a heel's depth above the surface
                    thigh.rotation = Quaternion.FromToRotation(foot.position - thigh.position, target - thigh.position) * thigh.rotation;
                }
                var up = Bone(go, $"Bip01 {s} UpperArm"); var fore = Bone(go, $"Bip01 {s} Forearm"); var hand = Bone(go, $"Bip01 {s} Hand");
                if (up != null && fore != null && hand != null)
                {
                    float lenU = (fore.position - up.position).magnitude, lenF = (hand.position - fore.position).magnitude, side = up.position.x >= cx ? 1f : -1f;
                    var tEl = new Vector3(cx + side * .30f, top + .04f, up.position.z - lenU * .96f); up.rotation = Quaternion.FromToRotation(fore.position - up.position, tEl - up.position) * up.rotation;
                    var tHa = new Vector3(cx + side * .27f, top + .04f, fore.position.z - lenF * .98f); fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, tHa - fore.position) * fore.rotation;
                }
            }
        }
    }
}
