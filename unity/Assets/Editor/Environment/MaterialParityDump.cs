using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProcSense.EditorTools
{
    /// <summary>Facts about how the PatientBay monitor's steel is actually shaded: materials, shader, reflection sources, URP/exposure settings.</summary>
    public static class MaterialParityDump
    {
        /// <summary>Do any Corridor or OT renderers fall inside PatientBay's reflection volumes (and so pick up PatientBay's probes when all three rooms are resident)?</summary>
        public static void ProbeOverlap()
        {
            var sb = new StringBuilder();
            var bay = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var corridor = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Additive);
            var ot = EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Additive);
            foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include).Where(p => p.gameObject.scene == bay))
            {
                var box = new Bounds(probe.transform.position + probe.center, probe.size);
                sb.AppendLine($"probe '{probe.name}' enabled={probe.enabled} world box centre {box.center:F2} size {box.size:F2}");
                foreach (var room in new[] { corridor, ot })
                {
                    var hits = room.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Where(r => r.enabled && box.Intersects(r.bounds)).ToArray();
                    sb.AppendLine($"  {room.name}: {hits.Length} renderer(s) intersect" + (hits.Length > 0 ? ": " + string.Join(", ", hits.Take(8).Select(h => h.name + " " + h.bounds.center.ToString("F2"))) : ""));
                }
            }
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MonitorBuild", "parity")); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "probe_overlap.txt"), sb.ToString());
            Debug.Log("[Parity] overlap written");
        }

        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            sb.AppendLine($"colorSpace={PlayerSettings.colorSpace} pipeline={(rp ? rp.name : "none")}");
            if (rp != null)
                sb.AppendLine($"URP hdr={rp.supportsHDR} msaa={rp.msaaSampleCount} renderScale={rp.renderScale} reflectionProbeBlending={rp.reflectionProbeBlending} boxProjection={rp.reflectionProbeBoxProjection} atlasBlending={(rp.reflectionProbeBlending ? "on" : "off")}");
            sb.AppendLine($"RenderSettings: ambientMode={RenderSettings.ambientMode} ambientIntensity={RenderSettings.ambientIntensity} skybox={(RenderSettings.skybox ? RenderSettings.skybox.name : "none")} defaultReflectionMode={RenderSettings.defaultReflectionMode} defaultReflectionResolution={RenderSettings.defaultReflectionResolution} reflectionIntensity={RenderSettings.reflectionIntensity} reflectionBounces={RenderSettings.reflectionBounces}");
            var cube = RenderSettings.customReflectionTexture;
            if (cube != null)
            {
                var path = AssetDatabase.GetAssetPath(cube);
                sb.AppendLine($"customReflection: {cube.name} {cube.width}x{cube.height} dim={cube.dimension} mips={(cube is Cubemap c ? c.mipmapCount : 0)} fmt={(cube is Cubemap c2 ? c2.format.ToString() : "?")} path={path}");
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null) sb.AppendLine($"  importer: shape={ti.textureShape} compression={ti.textureCompression} sRGB={ti.sRGBTexture} filter={ti.filterMode} maxSize={ti.maxTextureSize} mips={ti.mipmapEnabled}");
            }
            foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include))
                sb.AppendLine($"probe '{p.name}' enabled={p.enabled} mode={p.mode} res={p.resolution} hdr={p.hdr} importance={p.importance} intensity={p.intensity} boxProj={p.boxProjection} center={p.transform.position:F2} size={p.size:F2} texture={(p.bakedTexture ? p.bakedTexture.name + " " + p.bakedTexture.width : "none")}");
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            {
                var tm = v.sharedProfile != null && v.sharedProfile.TryGet<UnityEngine.Rendering.Universal.Tonemapping>(out var t) ? t.mode.value.ToString() : "none";
                sb.AppendLine($"volume '{v.name}' global={v.isGlobal} profile={(v.sharedProfile ? v.sharedProfile.name : "none")} tonemapping={tm} components={(v.sharedProfile ? string.Join(",", v.sharedProfile.components.Select(x => x.GetType().Name)) : "")}");
            }
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            var monitor = all.First(t => t.name == "ATTACH_ECG").root;
            foreach (var r in monitor.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("EQ_vital")))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { sb.AppendLine($"{r.name}: <null material>"); continue; }
                    sb.Append($"{r.name} -> {m.name} shader={m.shader.name} receiveGI={r.receiveGI} probeUsage={r.reflectionProbeUsage} static={GameObjectUtility.GetStaticEditorFlags(r.gameObject)}");
                    foreach (var prop in new[] { "_BaseColor", "_Metallic", "_Smoothness", "_SpecularHighlights", "_EnvironmentReflections", "_BumpScale" })
                        if (m.HasProperty(prop)) sb.Append(prop.StartsWith("_BaseColor") ? $" {prop}={m.GetColor(prop)}" : $" {prop}={m.GetFloat(prop):F2}");
                    sb.AppendLine($" keywords=[{string.Join(" ", m.shaderKeywords)}]");
                }
            }
            sb.AppendLine("--- other chrome-ish materials in Assets/Materials");
            foreach (var g in AssetDatabase.FindAssets("t:Material chrome", new[] { "Assets/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                sb.AppendLine($"{AssetDatabase.GUIDToAssetPath(g)}: metallic={(m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1):F2} smoothness={(m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : -1):F2} color={(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "?")}");
            }
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MonitorBuild", "parity"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "unity_facts.txt"), sb.ToString());
            Debug.Log("[Parity] facts written");
        }
    }
}
