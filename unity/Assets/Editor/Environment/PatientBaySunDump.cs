using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only: prints the patient bay's directional lights, window glass and sun-related objects.</summary>
    public static class PatientBaySunDump
    {
        public static void Run()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Where(l => l.type == LightType.Directional))
                Debug.Log($"[PBSun] light {l.name} active={l.gameObject.activeInHierarchy} mode={l.lightmapBakeType} I={l.intensity} euler={l.transform.eulerAngles} dir={l.transform.forward} shadows={l.shadows}");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.name.Contains("Window") || r.name.Contains("Glass") || r.name.Contains("glass") || r.name.Contains("blind") || r.name.Contains("Blind") || r.name.Contains("urtain")))
                Debug.Log($"[PBSun] {r.name} active={r.gameObject.activeInHierarchy} center={r.bounds.center} size={r.bounds.size} mat={(r.sharedMaterial != null ? r.sharedMaterial.name : "-")}");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.name.StartsWith("Floor") || r.name.StartsWith("Wall") || r.name.StartsWith("Ceiling")))
                Debug.Log($"[PBSun] shell {r.name} center={r.bounds.center} size={r.bounds.size}");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.parent == null)) Debug.Log("[PBSun] root " + t.name);
            foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include))
                Debug.Log($"[PBSun] probe {rp.name} active={rp.gameObject.activeInHierarchy} mode={rp.mode} refresh={rp.refreshMode} timeSlicing={rp.timeSlicingMode} intensity={rp.intensity} size={rp.size} hasBaked={(rp.bakedTexture != null)} res={rp.resolution}");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                Debug.Log($"[PBSun] anylight {l.name} type={l.type} mode={l.lightmapBakeType} I={l.intensity} active={l.gameObject.activeInHierarchy} shadows={l.shadows} comps={string.Join("/", l.GetComponents<Component>().Select(c => c.GetType().Name))}");
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include)) Debug.Log($"[PBSun] volume {v.name} global={v.isGlobal} weight={v.weight} priority={v.priority} profile={(v.sharedProfile ? v.sharedProfile.name : "-")}");
            Debug.Log($"[PBSun] ambient mode={UnityEngine.RenderSettings.ambientMode} reflectionMode={UnityEngine.RenderSettings.defaultReflectionMode} customReflection={(UnityEngine.RenderSettings.customReflectionTexture != null)} skybox={(UnityEngine.RenderSettings.skybox != null)}");
        }
    }
}
