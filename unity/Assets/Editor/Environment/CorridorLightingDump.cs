using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only dump of the corridor lighting inputs (lights, door and glass geometry, volumes, ambient) for planning the sun-through-doors rework.</summary>
    public static class CorridorLightingDump
    {
        public static void Run()
        {
            var s = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single); var sb = new StringBuilder();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"LIGHT {l.name} on={l.gameObject.activeInHierarchy} {l.type} pos {l.transform.position:F2} rot {l.transform.eulerAngles:F0} I={l.intensity:F2} col={l.color} temp={(l.useColorTemperature ? l.colorTemperature : 0)} bake={l.lightmapBakeType} sh={l.shadows} range={l.range:F1} spot={l.spotAngle:F0}");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var n = r.name.ToLowerInvariant();
                if (n.Contains("door") || n.Contains("glass") || n.Contains("pane") || n.Contains("window") || n.Contains("wall") || n.Contains("ceiling") || n.Contains("floor"))
                    sb.AppendLine($"REND {r.name} <{r.transform.parent?.name}> c={r.bounds.center:F2} s={r.bounds.size:F2} mat={string.Join("|", r.sharedMaterials.Select(m => m ? m.name + "/" + m.shader.name.Split('/').Last() : "null"))} sh={r.shadowCastingMode} static={GameObjectUtility.GetStaticEditorFlags(r.gameObject)}");
            }
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"VOLUME {v.name} global={v.isGlobal} profile={(v.sharedProfile ? v.sharedProfile.name : "null")} comps={(v.sharedProfile ? string.Join(",", v.sharedProfile.components.Select(c => c.GetType().Name)) : "")}");
            sb.AppendLine($"AMBIENT mode={RenderSettings.ambientMode} sky={RenderSettings.ambientSkyColor} eq={RenderSettings.ambientEquatorColor} gr={RenderSettings.ambientGroundColor} skybox={(RenderSettings.skybox ? RenderSettings.skybox.name : "null")}");
            Directory.CreateDirectory("Logs/Lighting"); File.WriteAllText(Path.GetFullPath("Logs/Lighting/corridor_dump.txt"), sb.ToString());
            Debug.Log("[CorridorLightingDump] done");
        }
    }
}
