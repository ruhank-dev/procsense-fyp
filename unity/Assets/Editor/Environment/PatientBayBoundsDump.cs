using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only: prints the floor, wall, bed and window bounds of the patient bay so a capture camera can be placed inside the room.</summary>
    public static class PatientBayBoundsDump
    {
        public static void Run()
        {
            var s = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            foreach (var r in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).Where(r => r.enabled))
            {
                var n = r.name;
                if (n.StartsWith("Floor") || n.StartsWith("Wall") || n.StartsWith("Ceiling") || n.Contains("Window") || n.Contains("bed") || n.Contains("Bed frame") || n.Contains("sofa") || n.Contains("Sofa"))
                    Debug.Log($"[PBBounds] {n} c={r.bounds.center:F2} s={r.bounds.size:F2}");
            }
        }
    }
}
