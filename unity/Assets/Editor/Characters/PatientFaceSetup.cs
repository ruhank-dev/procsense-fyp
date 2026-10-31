using System.IO;
using System.Linq;
using System.Text;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Patient face tooling: Dump() lists every skinned surface, its mesh, blendshapes and bones; Install() adds the audio-driven PatientSpeechFace to the surface that carries the facial shapes.</summary>
    public static class PatientFaceSetup
    {
        public static void Dump()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var sb = new StringBuilder(); var patient = GameObject.Find("MPFB patient");
            foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.GetComponentInParent<Transform>() != null))
            {
                var m = r.sharedMesh; if (m == null) continue;
                var shapes = Enumerable.Range(0, m.blendShapeCount).Select(m.GetBlendShapeName).ToArray();
                sb.AppendLine($"{r.name} | mesh {m.name} ({AssetDatabase.GetAssetPath(m)}) | verts {m.vertexCount} tris {m.triangles.Length / 3} | bones {r.bones.Length} | shapes {shapes.Length} | on {r.enabled && r.gameObject.activeInHierarchy} | mats {string.Join(";", r.sharedMaterials.Select(x => x ? x.name : "null"))}");
                if (shapes.Length > 0) sb.AppendLine("   shapes: " + string.Join(",", shapes));
            }
            Directory.CreateDirectory("Logs/FinalLook"); File.WriteAllText("Logs/FinalLook/patient_dump.txt", sb.ToString());
            Debug.Log("[PatientFace] dump written");
        }

        static readonly string[] SpeechShapes = { "jawOpen", "viseme_aa", "viseme_O", "viseme_U", "viseme_E", "viseme_I", "viseme_SS", "viseme_PP", "mouthClose", "mouthFunnel", "mouthPucker", "browInnerUp" };

        /// <summary>Appends the 12 speech channels to the existing skin idle mesh in place (breathing, blink and gaze channels and the asset GUID are untouched); idempotent.</summary>
        public static void AddSpeechShapes()
        {
            const string folder = "Assets/Models/Characters/MPFBPatient/";
            var idle = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "patient_body_idle.asset");
            var original = AssetDatabase.LoadAllAssetsAtPath(folder + "mpfb_patient.fbx").OfType<Mesh>().Single(m => m.name.EndsWith(".body", System.StringComparison.Ordinal));
            if (idle.vertexCount != original.vertexCount) throw new System.InvalidOperationException("idle body and source body differ in vertex count");
            int added = 0;
            foreach (var name in SpeechShapes)
            {
                if (idle.GetBlendShapeIndex(name) >= 0) continue;
                int s = original.GetBlendShapeIndex(name); if (s < 0) { Debug.LogWarning("[PatientFace] source lacks " + name); continue; }
                for (int f = 0; f < original.GetBlendShapeFrameCount(s); f++)
                {
                    var dv = new Vector3[idle.vertexCount]; var dn = new Vector3[idle.vertexCount]; var dt = new Vector3[idle.vertexCount];
                    original.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                    idle.AddBlendShapeFrame(name, original.GetBlendShapeFrameWeight(s, f), dv, dn, dt);
                }
                added++;
            }
            EditorUtility.SetDirty(idle); AssetDatabase.SaveAssets();
            Debug.Log($"[PatientFace] speech shapes added to the skin: {added} new, total {idle.blendShapeCount}");
        }

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("MPFB patient");
            var existing = patient.GetComponent<PatientSpeechFace>(); if (existing != null) Object.DestroyImmediate(existing);
            var faces = patient.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("jawOpen") >= 0).ToArray();
            if (faces.Length == 0) Debug.LogWarning("[PatientFace] no surface carries jawOpen; the speech face stays inactive until the full facial shapes are present");
            var comp = patient.AddComponent<PatientSpeechFace>(); comp.Configure(faces);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PatientFace] speech face installed on {comp.Surfaces} surfaces: {string.Join(", ", faces.Select(x => x.name))}; shapes found {comp.ShapesFound}");
        }
    }
}
