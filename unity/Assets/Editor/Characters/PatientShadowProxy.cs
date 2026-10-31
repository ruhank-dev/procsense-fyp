using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Grounds the patient in the baked lighting. The patient is a skinned, dynamic character, so the lightmapper cannot see it and the mattress, pillow and bed receive no contact shadow or
    /// occlusion from it (the body seems to hover). This builds an invisible, static, shadows-only copy of the body, gown and shorts in the saved rest pose: the bake then darkens the mattress
    /// under the body exactly where real contact would. Nothing is rendered by the camera (ShadowsOnly); it costs nothing at runtime beyond one static mesh in the shadow pass.
    /// Rebuild it whenever the rest pose or the gown fit changes, then rebake PatientBay.
    /// </summary>
    public static class PatientShadowProxy
    {
        const string Name = "Patient baked shadow proxy";

        static void Set(bool on)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var go = GameObject.Find(Name); if (go == null) { Debug.LogWarning("[ShadowProxy] not built"); return; }
            go.GetComponent<MeshRenderer>().enabled = on;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[ShadowProxy] renderer " + (on ? "ON for the bake" : "OFF after the bake (the baked contact shadows stay; no runtime cost)"));
        }

        public static void EnableForBake() => Set(true);
        public static void DisableAfterBake() => Set(false);

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var old = GameObject.Find(Name); if (old != null) Object.DestroyImmediate(old);
            var patient = GameObject.Find("MPFB patient");
            patient.GetComponent<PatientIdleView>()?.RestorePose();
            var combine = new System.Collections.Generic.List<CombineInstance>();
            foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var n = smr.name;
                if (!(n.EndsWith(".body") || n == "Draped patient gown" || n == "Patient shorts")) continue;
                var m = new Mesh(); smr.BakeMesh(m, false);
                combine.Add(new CombineInstance { mesh = m, transform = smr.transform.localToWorldMatrix });
            }
            var mesh = new Mesh { name = "patient_shadow_proxy", indexFormat = IndexFormat.UInt32 }; mesh.CombineMeshes(combine.ToArray(), true, true);
            var path = "Assets/Models/Characters/MPFBPatient/patient_shadow_proxy.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(Name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/PatientBayLook/PatientBay window glass.mat");
            mr.shadowCastingMode = ShadowCastingMode.ShadowsOnly; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            mr.enabled = true;
            Debug.Log($"[ShadowProxy] built: {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles (shadows only, static)");
        }
    }
}
