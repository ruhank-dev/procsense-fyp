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

        [MenuItem("ProcSense/Characters/Fit patients to the bed")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var mattress = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy && r.name == "Soft mattress").ToList();
            if (mattress.Count == 0) { Debug.LogError("[PatientBedFit] no mattress"); return; }
            var mb = mattress[0].bounds; float top = mb.max.y, cx = mb.center.x;
            var profiles = Object.FindObjectsByType<ProcSense.VR.Views.PatientMotion>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(m => m.gameObject).Distinct().ToList();
            var pillow = Pillow(top, cx);
            foreach (var go in profiles) { Fit(go, top, cx); FitHead(go, cx, pillow.max.y); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private const string PillowName = "Bed pillow";

        /// <summary>A pillow at the head of the bed (the hair and the back of the head were inside the mattress). Soft white box, no collider, no shadows; hidden by <see cref="ProcSense.VR.Views.BedBackrest"/> while the patient sits.</summary>
        private static Bounds Pillow(float top, float cx)
        {
            var o = GameObject.Find(PillowName);
            if (o == null)
            {
                o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = PillowName; Object.DestroyImmediate(o.GetComponent<Collider>());
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Bed_Pillow.mat");
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.93f, .95f, .96f) }; mat.SetFloat("_Smoothness", .2f); AssetDatabase.CreateAsset(mat, "Assets/Materials/Environment/Bed_Pillow.mat"); }
                o.GetComponent<Renderer>().sharedMaterial = mat;
            }
            o.transform.SetPositionAndRotation(new Vector3(cx, top + .05f, 1.70f), Quaternion.identity); o.transform.localScale = new Vector3(.52f, .10f, .34f);
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
                if (r.sharedMesh == null) continue; var mesh = new Mesh(); r.BakeMesh(mesh, true); var l2w = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices) { var w = l2w.MultiplyPoint3x4(v); if (w.z >= head.position.z - .06f && Mathf.Abs(w.x - cx) < .2f && w.y < min) min = w.y; }
                Object.DestroyImmediate(mesh);
            }
            return min == float.MaxValue ? float.NaN : min;
        }

        /// <summary>Bends the neck forward until the back of the head (hair included) rests on the pillow, with 1.5 cm of give.</summary>
        private static void FitHead(GameObject go, float cx, float pillowTop)
        {
            var neck = Bone(go, "Bip01 Neck"); var l = Bone(go, "Bip01 L UpperArm"); var rr = Bone(go, "Bip01 R UpperArm"); if (neck == null || l == null || rr == null) return;
            bool was = go.activeSelf; go.SetActive(true);
            var axis = (rr.position - l.position).normalized; var rest = neck.rotation; float target = pillowTop - Give;
            float best = 0, bestErr = float.MaxValue;
            for (int sign = -1; sign <= 1; sign += 2)
                for (float a = 0; a <= 40; a += 2)
                {
                    neck.rotation = Quaternion.AngleAxis(sign * a, axis) * rest; float y = HeadMinY(go, cx); if (float.IsNaN(y)) continue;
                    float err = Mathf.Abs(y - target); if (err < bestErr - 1e-4f) { bestErr = err; best = sign * a; }
                }
            neck.rotation = Quaternion.AngleAxis(best, axis) * rest;
            Debug.Log($"[PatientBedFit] {go.name}: neck bent {best:F0} degrees, back of head at {HeadMinY(go, cx):F3}, pillow top {pillowTop:F3}");
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
                if (r.sharedMesh == null || r.name.Contains("opacity") && r.sharedMaterial != null && r.sharedMaterial.name.ToLower().Contains("hidden")) continue;
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
