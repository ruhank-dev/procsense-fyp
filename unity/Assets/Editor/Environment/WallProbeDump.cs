using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only: how the corridor west wall slabs are built (mesh, scale, UV, material tiling), what shadow objects the corridor people have, and the lightmapper mixed mode.</summary>
    public static class WallProbeDump
    {
        public static void Run()
        {
            var s = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(r => r.name.StartsWith("Wall ")))
            {
                var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; var mat = r.sharedMaterial;
                Debug.Log($"[WallProbe] {r.name} mesh={(m ? m.name + " v" + m.vertexCount : "none")} lscale={r.transform.localScale:F2} pos={r.transform.position:F2} rot={r.transform.eulerAngles:F0} mat={mat?.name} tiling={(mat ? mat.mainTextureScale.ToString() : "")} offset={(mat ? mat.mainTextureOffset.ToString() : "")} uv1={(m && m.uv2 != null && m.uv2.Length > 0)} collider={r.GetComponent<Collider>() != null} parent={r.transform.parent?.name}");
                if (m != null && r.name == "Wall left doorway right") Debug.Log("[WallProbe] uv sample " + string.Join(" ", m.uv.Take(8).Select(u => u.ToString("F2"))) + " verts " + string.Join(" ", m.vertices.Take(8).Select(v => v.ToString("F2"))));
            }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r.name.ToLowerInvariant().Contains("contact") || r.name.ToLowerInvariant().Contains("shadow")).Take(12)) Debug.Log($"[WallProbe] shadowobj {r.name} <{r.transform.parent?.name}> c={r.bounds.center:F2}");
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/Settings/HospitalLighting/PatientBay.lighting");
            if (ls != null) Debug.Log($"[WallProbe] lighting: mixedMode={ls.mixedBakeMode} bounces={ls.maxBounces} res={ls.lightmapResolution} AO={ls.ao} dirMode={ls.directionalityMode} filtering={ls.filteringMode}");
        }
    }
}
