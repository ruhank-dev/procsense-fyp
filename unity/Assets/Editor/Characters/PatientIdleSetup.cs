using System;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class PatientIdleSetup
    {
        private const string Folder = "Assets/Models/Characters/MPFBPatient/";

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve unsaved scenes first.");
            var patient = GameObject.Find("MPFB patient");
            if (patient == null || patient.GetComponent<PatientIdleView>() != null) throw new InvalidOperationException("Open the unchanged PatientBay; preserve an existing idle setup.");
            var body = patient.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.EndsWith(".body", StringComparison.Ordinal));
            var gown = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            AddBreath(body, "patient_body_idle.asset");
            AddBreath(gown, "patient_gown_idle.asset");

            var face = patient.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray();
            patient.AddComponent<PatientIdleView>().Configure(patient.GetComponentsInChildren<Transform>().Single(t => t.name == "head"), new[] { body, gown }, face);
            EditorSceneManager.MarkSceneDirty(patient.scene);
            EditorSceneManager.SaveScene(patient.scene);
            SaveMeshes();
            return "Installed fitted torso breathing, shared facial blink/gaze and constrained head idle.";
        }

        public static string UpdateSurfaces()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve unsaved scenes first.");
            var body = GameObject.Find("MPFB patient").GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.EndsWith(".body", StringComparison.Ordinal));
            GameObject.Find("MPFB patient").GetComponent<PatientIdleView>()?.RestorePose();
            body.sharedMesh = AssetDatabase.LoadAllAssetsAtPath(Folder + "mpfb_patient.fbx").OfType<Mesh>().Single(m => m.name.EndsWith(".body", StringComparison.Ordinal));
            AddBreath(body, "patient_body_idle.asset", true);
            var gown = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            gown.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "patient_gown_relaxed.asset") ?? AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "patient_gown_draped.asset");
            AddBreath(gown, "patient_gown_idle.asset", true);
            var shorts = GameObject.Find("Patient shorts")?.GetComponent<SkinnedMeshRenderer>();
            if (shorts != null)
            {
                shorts.sharedMesh = AssetDatabase.LoadAllAssetsAtPath(Folder + "patient_shorts.fbx").OfType<Mesh>().Single();
                AddBreath(shorts, "patient_shorts_idle.asset", true);
            }
            EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
            EditorSceneManager.SaveScene(body.gameObject.scene);
            SaveMeshes();
            return "Body and gown breathing surfaces updated for the saved rest pose.";
        }

        private static void SaveMeshes()
        {
            foreach (string file in new[] { "patient_body_idle.asset", "patient_gown_idle.asset", "patient_shorts_idle.asset" })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + file);
                if (mesh != null) AssetDatabase.SaveAssetIfDirty(mesh);
            }
        }

        private static void AddBreath(SkinnedMeshRenderer renderer, string file, bool replace = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + file);
            if (existing != null && !replace)
            {
                if (existing.GetBlendShapeIndex(PatientIdleView.BreathShape) < 0) throw new InvalidOperationException("Existing mesh is not an idle surface.");
                renderer.sharedMesh = existing;
                ExpandBounds(renderer);
                return;
            }
            var original = renderer.sharedMesh;
            var mesh = UnityEngine.Object.Instantiate(original);
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(file);
            if (file == "patient_body_idle.asset")
            {
                // Keep this idle copy within B-S2's 15-channel target. The original FBX retains all 67
                // face/viseme shapes for later expression/speech work; never edit the imported source.
                mesh.ClearBlendShapes();
                for (int s = 0; s < original.blendShapeCount; s++)
                {
                    string name = original.GetBlendShapeName(s);
                    if (!name.StartsWith("eyeBlink", StringComparison.Ordinal) && !name.StartsWith("eyeLook", StringComparison.Ordinal)
                        && name != "browDownLeft" && name != "browDownRight" && name != "mouthSmileLeft" && name != "mouthSmileRight") continue;
                    for (int f = 0; f < original.GetBlendShapeFrameCount(s); f++)
                    {
                        var dv = new Vector3[mesh.vertexCount]; var dn = new Vector3[mesh.vertexCount]; var dt = new Vector3[mesh.vertexCount];
                        original.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                        mesh.AddBlendShapeFrame(name, original.GetBlendShapeFrameWeight(s, f), dv, dn, dt);
                    }
                }
            }
            var baked = new Mesh();
            // Imported FBX bones carry a 100x unit conversion. useScale=true returns mesh-local points.
            renderer.BakeMesh(baked, true);
            var points = baked.vertices;
            var deltas = new Vector3[mesh.vertexCount];
            var weights = mesh.boneWeights;
            var bind = mesh.bindposes;
            var bones = renderer.bones;
            var matrices = bones.Select((b, i) => b.localToWorldMatrix * bind[i]).ToArray();
            for (int i = 0; i < points.Length; i++)
            {
                var world = renderer.transform.TransformPoint(points[i]);
                float lift = Lift(world);
                if (lift == 0) continue;
                var delta = Vector3.up * lift;
                if (weights.Length == points.Length && matrices.Length > 0)
                {
                    var w = weights[i];
                    var skin = new Matrix4x4();
                    for (int c = 0; c < 16; c++) skin[c] = matrices[w.boneIndex0][c] * w.weight0 + matrices[w.boneIndex1][c] * w.weight1 + matrices[w.boneIndex2][c] * w.weight2 + matrices[w.boneIndex3][c] * w.weight3;
                    deltas[i] = skin.inverse.MultiplyVector(delta);
                }
                else deltas[i] = renderer.transform.InverseTransformVector(delta);
            }
            mesh.AddBlendShapeFrame(PatientIdleView.BreathShape, 100, deltas, null, null);
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * .02f);
            if (existing == null) AssetDatabase.CreateAsset(mesh, Folder + file);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            renderer.sharedMesh = mesh;
            ExpandBounds(renderer);
            UnityEngine.Object.DestroyImmediate(baked);
        }

        private static void ExpandBounds(SkinnedMeshRenderer renderer)
        {
            var space = renderer.rootBone != null ? renderer.rootBone : renderer.transform;
            var scale = space.lossyScale;
            var margin = new Vector3(.05f / Mathf.Abs(scale.x), .05f / Mathf.Abs(scale.y), .05f / Mathf.Abs(scale.z));
            var baked = new Mesh();
            try
            {
                renderer.BakeMesh(baked, true);
                var points = baked.vertices;
                var bounds = new Bounds(space.InverseTransformPoint(renderer.transform.TransformPoint(points[0])), Vector3.zero);
                foreach (var p in points) bounds.Encapsulate(space.InverseTransformPoint(renderer.transform.TransformPoint(p)));
                renderer.localBounds = new Bounds(bounds.center, bounds.size + margin);
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
        }

        private static float Lift(Vector3 p)
        {
            // All surfaces use the same world-space profile; back, neck, arms, hands and bed edges remain still.
            float width = Mathf.Clamp01(1 - p.x * p.x / (.27f * .27f));
            width *= width;
            float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.84f, .98f, p.y));
            float chest = Mathf.Clamp01(1 - Mathf.Pow((p.z - 1.28f) / .29f, 2));
            float abdomen = Mathf.Clamp01(1 - Mathf.Pow((p.z - .98f) / .27f, 2));
            return width * front * Mathf.Max(.014f * chest * chest, .009f * abdomen * abdomen);
        }
    }
}
