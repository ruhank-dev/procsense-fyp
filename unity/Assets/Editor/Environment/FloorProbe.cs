using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only facts about PatientBay's Floor object: world extents, mesh UV layout on the top face, tangents, components, lightmap settings, other renderers on the floor plane.</summary>
    public static class FloorProbe
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Floor" && t.GetComponent<MeshRenderer>() != null);
            var r = floor.GetComponent<MeshRenderer>(); var mf = floor.GetComponent<MeshFilter>();
            sb.AppendLine($"floor world pos {floor.position:F3} scale {floor.lossyScale:F3} rot {floor.eulerAngles:F1} bounds {r.bounds.min:F3} .. {r.bounds.max:F3}");
            sb.AppendLine($"parent {floor.parent?.name} scale {floor.parent?.lossyScale:F3}; mesh {mf.sharedMesh.name} verts {mf.sharedMesh.vertexCount} uv2 {mf.sharedMesh.uv2.Length}");
            sb.AppendLine($"components: {string.Join(", ", floor.GetComponents<Component>().Select(c => c.GetType().Name))}; static flags {GameObjectUtility.GetStaticEditorFlags(floor.gameObject)}; scaleInLightmap {r.scaleInLightmap}; lightmapIndex {r.lightmapIndex}");
            var m = mf.sharedMesh; var v = m.vertices; var n = m.normals; var uv = m.uv; var t = m.tangents;
            for (var i = 0; i < v.Length; i++)
                if (n[i].y > .9f) sb.AppendLine($"top vertex local {v[i]:F2} uv {uv[i]:F2} tangent {t[i]:F2}");
            sb.AppendLine($"floor material: {r.sharedMaterial.name}");
            foreach (var other in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(o => o.bounds.min.y < .02f && o.bounds.size.x > 1.5f && o != r))
                sb.AppendLine($"large low renderer: {other.name} bounds {other.bounds.min:F2}..{other.bounds.max:F2}");
            foreach (var comp in floor.GetComponentsInParent<MonoBehaviour>(true)) sb.AppendLine($"script on floor/parents: {comp.GetType().Name} on {comp.name}");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "FloorBuild"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "floor_probe.txt"), sb.ToString());
            Debug.Log("[FloorProbe] written");
        }
    }
}
