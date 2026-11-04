using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only inventory of PatientBay for the final-look overhaul: every renderer (path, materials, bounds, triangles, static flags), skinned meshes, animators, rigidbodies, cloth.</summary>
    public static class FinalLookDump
    {
        static string Path_(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }

        public static void Run() => Dump("PatientBay");
        public static void DumpCorridor() => Dump("Corridor");
        public static void DumpOT() => Dump("OT");
        public static void DumpRecovery() => Dump("Recovery");

        static void Dump(string sceneName)
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            long tris = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(x => Path_(x.transform)))
            {
                int t = 0;
                if (r is MeshRenderer mr && mr.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null) t = mf.sharedMesh.triangles.Length / 3;
                if (r is SkinnedMeshRenderer sm && sm.sharedMesh != null) t = sm.sharedMesh.triangles.Length / 3;
                tris += t;
                sb.AppendLine($"{(r is SkinnedMeshRenderer ? "SKIN" : "MESH")} {Path_(r.transform)} | tris {t} | b {r.bounds.min:F2}..{r.bounds.max:F2} | mats {string.Join(";", r.sharedMaterials.Select(m => m == null ? "null" : m.name + "[" + m.shader.name.Replace("Universal Render Pipeline/", "URP/") + "]"))} | on {r.enabled && r.gameObject.activeInHierarchy} | static {(GameObjectUtility.GetStaticEditorFlags(r.gameObject) != 0)}");
            }
            sb.AppendLine("TOTAL TRIS " + tris);
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(x => Path_(x.transform))) sb.AppendLine($"COLLIDER {Path_(c.transform)} | {c.GetType().Name} trig {c.isTrigger} | b {c.bounds.min:F2}..{c.bounds.max:F2}");
            foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine($"ANIMATOR {Path_(a.transform)} ctrl {(a.runtimeAnimatorController ? a.runtimeAnimatorController.name : "none")} avatar {(a.avatar ? a.avatar.name : "none")}");
            foreach (var b in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine($"RIGIDBODY {Path_(b.transform)} kinematic {b.isKinematic} mass {b.mass}");
            foreach (var c in Object.FindObjectsByType<Cloth>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine($"CLOTH {Path_(c.transform)}");
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(m => m != null).GroupBy(m => m.GetType().FullName)) sb.AppendLine($"SCRIPT {m.Key} x{m.Count()}");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "FinalLook")); Directory.CreateDirectory(dir);
            File.WriteAllText(System.IO.Path.Combine(dir, sceneName == "PatientBay" ? "scene_dump.txt" : "dump_" + sceneName + ".txt"), sb.ToString());
            Debug.Log("[FinalLookDump] written " + tris + " tris");
        }
    }
}
