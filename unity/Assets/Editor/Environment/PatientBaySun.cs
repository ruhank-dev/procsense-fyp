using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Patient bay sunlight, same method as the corridor: the baked/mixed "Sun through window" (low pitch from golden.json, so the patches stretch across the floor and up the opposite wall) plus the desktop-only
    /// organic multi-shaft atmosphere and dust, built from the real window glass and the real sun direction. Shafts only; the surface lighting comes from the bake.
    /// </summary>
    public static class PatientBaySun
    {
        /// <summary>Moves the plant that stands in front of the exit door into the free floor spot closest to the computer corner (south-east), checked against every other renderer's footprint.</summary>
        public static void MovePlant()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var plant = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t => t.name == "potted_plant_02" && t.parent != null && t.parent.name == "Wall decoration");
            var others0 = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.gameObject.activeInHierarchy && !r.transform.IsChildOf(plant) && !(r.name.StartsWith("Wall") || r.name.StartsWith("Floor") || r.name.StartsWith("Ceiling") || r.name.Contains("ontact") || r.name.StartsWith("Skirting") || r.name.StartsWith("Cap rail") || r.name.StartsWith("Lower") || r.name.StartsWith("Panel cap")) && r.bounds.max.y > .05f && r.bounds.min.y < 1.2f && r.bounds.size.y < 2.7f).Select(r => r.bounds).ToList();
            var best = new Vector3(2.2f, 0f, -1.7f);                                                                           // the computer corner: south-east, beside the charting counter, clear of the exit door frame
            Debug.Log($"[PatientBaySun] plant {plant.position} -> {best}"); plant.position = best;
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
        }

        /// <summary>Owner request 3 Nov: the window sill slab, the glazing bar across the glass and the privacy-curtain ceiling track read as random beams; switch their renderers off.</summary>
        public static void RemoveBeams()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single); int n = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.name == "Window sill" || r.name == "Window glazing bar" || r.name == "Horizontal window frame" || r.name.StartsWith("EQ_privacy_curtain_"))) { r.enabled = false; n++; }
            Debug.Log($"[PatientBaySun] {n} beam renderers off"); EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
        }

        public static void Shafts()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            foreach (var n in new[] { "Sun shafts", "Patient bay sun rig" }) { var o = sc.GetRootGameObjects().FirstOrDefault(g => g.name == n); if (o != null) Object.DestroyImmediate(o); }
            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include).First(l => l.name == "Sun through window"); var dir = sun.transform.forward;
            var lit = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.name == "Window glass pane" && r.gameObject.activeInHierarchy).Select(r => r.bounds).ToList();
            var rig = new GameObject("Patient bay sun rig"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, sc);
            CorridorSun.ShaftsFor(rig.transform, lit, dir, 2.4f, " bay"); rig.transform.Find("Door sun shafts").gameObject.SetActive(!System.Environment.GetCommandLineArgs().Contains("-noshafts"));
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets(); Debug.Log($"[PatientBaySun] shafts for {lit.Count} windows, dir {dir}");
        }
    }
}
