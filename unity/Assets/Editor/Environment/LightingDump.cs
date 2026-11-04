using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only facts about PatientBay lighting: every light, the lighting settings asset, ambient and reflection setup, window and exterior objects, floor material.</summary>
    public static class LightingDump
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(x => x.name))
                sb.AppendLine($"LIGHT {l.name}: {l.type} pos {l.transform.position:F2} euler {l.transform.eulerAngles:F1} color {l.color} intensity {l.intensity} mode {l.lightmapBakeType} shadows {l.shadows} size {(l.type == LightType.Rectangle ? l.areaSize.ToString() : l.range.ToString())} enabled {l.enabled}");
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/Settings/HospitalLighting/PatientBay.lighting");
            var so = new SerializedObject(ls); var it = so.GetIterator(); it.Next(true);
            while (it.NextVisible(true))
                if (it.propertyType != SerializedPropertyType.Generic && (it.name.StartsWith("m_PVR") || it.name.StartsWith("m_Bake") || it.name.StartsWith("m_Lightmap") || it.name.Contains("Filter") || it.name.Contains("Denois") || it.name.Contains("Compress") || it.name.Contains("AO") || it.name.Contains("Mixed") || it.name.Contains("Directional") || it.name.Contains("Padding") || it.name.Contains("Realtime") || it.name.Contains("Final") || it.name.Contains("Env")))
                    sb.AppendLine($"SETTING {it.name} = {(it.propertyType == SerializedPropertyType.Float ? it.floatValue.ToString() : it.propertyType == SerializedPropertyType.Integer ? it.intValue.ToString() : it.propertyType == SerializedPropertyType.Boolean ? it.boolValue.ToString() : it.propertyType.ToString())}");
            sb.AppendLine($"AMBIENT mode {RenderSettings.ambientMode} sky {RenderSettings.ambientSkyColor} equator {RenderSettings.ambientEquatorColor} ground {RenderSettings.ambientGroundColor} intensity {RenderSettings.ambientIntensity}; skybox {(RenderSettings.skybox ? RenderSettings.skybox.name : "none")}; reflection {RenderSettings.defaultReflectionMode} intensity {RenderSettings.reflectionIntensity}; fog {RenderSettings.fog}");
            foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"REFLPROBE {p.name} enabled {p.enabled} mode {p.mode} res {p.resolution} hdr {p.hdr} pos {p.transform.position:F2} size {p.size:F2} importance {p.importance}");
            foreach (var g in Object.FindObjectsByType<LightProbeGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine($"LIGHTPROBEGROUP {g.name} probes {g.probePositions.Length}");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = r.name.ToLowerInvariant();
                if (n.Contains("window") || n.Contains("exterior") || n.Contains("glass") || n.Contains("mullion") || n.Contains("sill") || n == "floor" || n.Contains("curtain"))
                    sb.AppendLine($"RENDERER {r.name}: bounds {r.bounds.min:F2}..{r.bounds.max:F2} mat {string.Join("|", r.sharedMaterials.Select(m => m == null ? "null" : $"{m.name}[{m.shader.name}]"))} static {GameObjectUtility.GetStaticEditorFlags(r.gameObject)} shadow {r.shadowCastingMode} lm {r.lightmapIndex}");
            }
            sb.AppendLine($"LIGHTMAPS {LightmapSettings.lightmaps.Length}, probes {LightmapSettings.lightProbes?.count}");
            foreach (var l in LightmapSettings.lightmaps) sb.AppendLine($"  lightmap {(l.lightmapColor ? l.lightmapColor.name + " " + l.lightmapColor.width + "x" + l.lightmapColor.height + " " + l.lightmapColor.format : "none")} dir {(l.lightmapDir ? l.lightmapDir.name : "none")}");
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Lighting")); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "lighting_dump.txt"), sb.ToString());
            Debug.Log("[LightingDump] written");
        }
    }
}
