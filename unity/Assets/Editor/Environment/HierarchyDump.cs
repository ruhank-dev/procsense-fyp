using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only structure dump (names, transforms, components) of a scene to depth N, for planning architectural changes.</summary>
    public static class HierarchyDump
    {
        static void Walk(Transform t, int depth, int max, StringBuilder sb)
        {
            var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            sb.AppendLine($"{new string(' ', depth * 2)}{t.name} pos {t.position:F2} rot {t.eulerAngles:F0} scl {t.localScale:F2} [{comps}] kids {t.childCount}");
            if (depth < max) foreach (Transform c in t) Walk(c, depth + 1, max, sb);
        }
        public static void Corridor() => Run("Corridor", 3);
        public static void OT() => Run("OT", 2);
        static void Run(string scene, int depth)
        {
            var s = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single); var sb = new StringBuilder();
            foreach (var g in s.GetRootGameObjects()) Walk(g.transform, 0, depth, sb);
            File.WriteAllText(Path.GetFullPath("Logs/FinalLook/hier_" + scene + ".txt"), sb.ToString());
            Debug.Log("[HierarchyDump] " + scene);
        }
    }
}
