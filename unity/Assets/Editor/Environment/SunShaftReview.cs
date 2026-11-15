using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Read-only sunshaft review (15 Nov 2026 regression: shafts gone on Quest, dust still visible). Opens the three connected rooms, writes the facts that decide whether a shaft can draw
    /// (host active, renderer enabled, shader supported, cull mode, noise texture format, last build report) and renders each shaft from the side, from inside the beam and from two eye
    /// positions 64 mm apart, with and without the shaft renderers, so the shaft contribution is measured as a difference image (dust stays on in both). Never saves a scene or an asset.
    /// The two eye renders are two ordinary cameras, not single-pass instanced / multiview: true stereo needs the headset.
    /// Batch (no -nographics): -executeMethod ProcSense.EditorTools.SunShaftReview.Run -shaftTag NAME [-questPipeline] [-shaftCull 0|2] [-redNoise]. Output: D:/ProcSenseWork/reports/shafts.
    /// </summary>
    public static class SunShaftReview
    {
        const int W = 960, H = 720;
        public const string BakedShader = "ProcSense/Sun Shaft Baked";
        static string Out => "D:/ProcSenseWork/reports/shafts";

        static string Arg(string name, string fallback)
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }
        static bool Flag(string name) => System.Environment.GetCommandLineArgs().Contains(name);

        /// <summary>The shaft prism renderers (the ProcSense shaft shaders), in every loaded scene, active or not.</summary>
        public static List<MeshRenderer> ShaftRenderers() => Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.StartsWith("ProcSense/Sun Shaft")).OrderBy(r => r.gameObject.scene.name).ThenBy(r => r.name).ToList();

        public static void Run()
        {
            Directory.CreateDirectory(Out); var tag = Arg("-shaftTag", "review");
            var bay = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Additive);
            EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(bay);
            if (Flag("-questPipeline"))
            {
                var q = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/URP_Quest_Android.asset");
                GraphicsSettings.defaultRenderPipeline = q; QualitySettings.renderPipeline = q;           // in memory only, never saved
            }
            var shafts = ShaftRenderers();
            // experiments run on copies: an edited material asset would be written back when the Editor exits
            var copies = shafts.Select(r => r.sharedMaterial).Distinct().ToDictionary(m => m, m => new Material(m) { name = m.name + " (review copy)" });
            foreach (var r in shafts) r.sharedMaterial = copies[r.sharedMaterial];
            var mats = copies.Values.ToList();
            if (Arg("-shaftCull", "") is var cull && cull != "") foreach (var m in mats) m.SetFloat("_Cull", float.Parse(cull));
            if (Flag("-redNoise"))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat };
                t.LoadImage(File.ReadAllBytes("Assets/Textures/Effects/sunshaft_noise.png")); t.wrapMode = TextureWrapMode.Repeat;
                foreach (var m in mats) m.SetTexture("_NoiseTex", t);
            }
            if (Flag("-origShader")) { var o = Shader.Find("ProcSense/Sun Shaft"); foreach (var m in mats) m.shader = o; }     // the pre-overhaul look, for reference (in memory)
            var curtains = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.name.Contains("privacy_curtain")).ToList();
            if (Flag("-hideCurtainLods")) foreach (var r in curtains) r.enabled = false;                                          // what PatientBaySun.RemoveBeams intended (in memory)
            File.WriteAllText(Path.Combine(Out, tag + "-facts.txt"), Facts(shafts) + string.Join("\n", curtains.Select(r =>
                $"curtain {r.gameObject.scene.name}/{HierPath(r.transform)} enabled={r.enabled} active={r.gameObject.activeInHierarchy} lodGroup={(r.GetComponentInParent<LODGroup>(true) ? r.GetComponentInParent<LODGroup>(true).name : "-")} bounds={r.bounds.center:F2}/{r.bounds.size:F2}")));

            var csv = new StringBuilder("view,shaftPixels(>2/255),meanDiff(0-255),maxDiff\n");
            foreach (var (name, pos, look, fov) in Views(shafts))
            {
                foreach (var (eye, off) in new[] { ("L", -0.032f), ("R", 0.032f) })
                {
                    var rot = Quaternion.LookRotation(look - pos); var p = pos + rot * Vector3.right * off;
                    var on = Render(p, rot, fov, true); var offImg = Render(p, rot, fov, false);
                    int count = 0; long sum = 0; int max = 0; var diff = new Color32[on.Length];
                    for (int i = 0; i < on.Length; i++)
                    {
                        int d = Mathf.Max(on[i].r - offImg[i].r, on[i].g - offImg[i].g, on[i].b - offImg[i].b, 0);
                        if (d > 2) count++; sum += d; if (d > max) max = d; byte v = (byte)Mathf.Min(255, d * 8); diff[i] = new Color32(v, v, v, 255);
                    }
                    Save(on, Path.Combine(Out, $"{tag}-{name}-{eye}.png"));
                    if (eye == "L") { Save(offImg, Path.Combine(Out, $"{tag}-{name}-noshafts.png")); Save(diff, Path.Combine(Out, $"{tag}-{name}-diffx8.png")); }
                    csv.AppendLine($"{name}-{eye},{count},{(double)sum / on.Length:F3},{max}");
                }
            }
            File.WriteAllText(Path.Combine(Out, tag + "-contribution.csv"), csv.ToString());
            Debug.Log("[SunShaftReview] done: " + tag + "\n" + csv);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>For every shaft mesh: a side view 2.6 m off the beam axis at eye height, a view from inside the beam toward the window, and a turned view; plus the bay reference viewpoint.</summary>
        static IEnumerable<(string, Vector3, Vector3, float)> Views(List<MeshRenderer> shafts)
        {
            yield return ("bayRef", new Vector3(2.38f, 1.62f, -1.84f), new Vector3(-0.62f, 0.95f, 1.15f), 80);
            yield return ("bayFoot", new Vector3(0.6f, 1.6f, -1.6f), new Vector3(-1.6f, 0.9f, 0.4f), 80);          // standing at the bed's foot, looking at the window
            yield return ("bayTurned", new Vector3(1.4f, 1.6f, 1.6f), new Vector3(-1.4f, 0.8f, -0.2f), 80);        // the other side of the bed, head turned toward the window
            foreach (var r in shafts.Where(r => r.gameObject.activeInHierarchy && r.name.StartsWith("Shafts")))
            {
                var b = r.bounds; var m = r.sharedMaterial; var d = ((Vector3)m.GetVector("_ShaftDir")).normalized;
                var side = Vector3.Cross(Vector3.up, new Vector3(d.x, 0, d.z).normalized); var c = b.center;
                string n = r.gameObject.scene.name + "-" + r.name.Replace(" ", "");
                var eye = c + side * 2.6f; eye.y = 1.6f; if (!InsideRoom(eye)) { eye = c - side * 2.6f; eye.y = 1.6f; }
                yield return (n + "-side", eye, c, 75);
                var inBeam = c + d * 0.15f; inBeam.y = Mathf.Clamp(inBeam.y, 1.2f, 1.7f);
                yield return (n + "-inside", inBeam, inBeam - d * 3f + Vector3.up * 0.4f, 85);
                var far = c + d * (b.extents.magnitude * 0.9f); far.y = 1.6f;
                yield return (n + "-towardWindow", far, c - d * 0.5f, 80);
            }
        }

        static bool InsideRoom(Vector3 p) => Physics.Raycast(p, Vector3.down, 3f) && Physics.Raycast(p, Vector3.up, 3f);

        static Color32[] Render(Vector3 pos, Quaternion rot, float fov, bool shaftsOn)
        {
            var list = ShaftRenderers(); var was = list.Select(r => r.enabled).ToList();
            if (!shaftsOn) foreach (var r in list) r.enabled = false;
            var go = new GameObject("ShaftReviewCamera") { hideFlags = HideFlags.HideAndDontSave }; var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot); cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); cam.targetTexture = rt;
            for (int i = 0; i < 2; i++) cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            var px = tex.GetPixels32(); RenderTexture.active = prev; cam.targetTexture = null;
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            for (int i = 0; i < list.Count; i++) list[i].enabled = was[i];
            return px;
        }

        static void Save(Color32[] px, string file)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false, false); t.SetPixels32(px); t.Apply(); File.WriteAllBytes(file, t.EncodeToPNG()); Object.DestroyImmediate(t);
        }

        static string Facts(List<MeshRenderer> shafts)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"pipeline {(GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "none")}  device {SystemInfo.graphicsDeviceType}  target {EditorUserBuildSettings.activeBuildTarget}");
            foreach (var h in Object.FindObjectsByType<ProcSense.VR.Views.DesktopOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"host {h.gameObject.scene.name}/{HierPath(h.transform)} activeInHierarchy={h.gameObject.activeInHierarchy} children={h.transform.childCount}");
            foreach (var r in shafts)
            {
                var m = r.sharedMaterial; var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null;
                sb.AppendLine($"shaft {r.gameObject.scene.name}/{HierPath(r.transform)} active={r.gameObject.activeInHierarchy} enabled={r.enabled} forceOff={r.forceRenderingOff} layer={r.gameObject.layer} bounds={r.bounds.center:F2}/{r.bounds.size:F2} verts={(mesh ? mesh.vertexCount : 0)}");
                sb.AppendLine($"   material {m.name} shader={m.shader.name} supported={m.shader.isSupported} queue={m.renderQueue} cull={(m.HasProperty("_Cull") ? m.GetFloat("_Cull").ToString() : "fixed")} intensity={m.GetFloat("_Intensity")} dir={m.GetVector("_ShaftDir")} noise={(m.HasProperty("_NoiseTex") && m.GetTexture("_NoiseTex") ? m.GetTexture("_NoiseTex").name : "none")}");
            }
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Effects/sunshaft_noise.png");
            var ti = AssetImporter.GetAtPath("Assets/Textures/Effects/sunshaft_noise.png") as TextureImporter;
            if (noise != null && ti != null)
            {
                var android = ti.GetPlatformTextureSettings("Android");
                var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts);
                sb.AppendLine($"noise format(editor)={noise.format} type={ti.textureType} channel={ts.singleChannelComponent} android override={android.overridden} {android.format} max={android.maxTextureSize}");
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.type == LightType.Directional))
                sb.AppendLine($"directional {l.gameObject.scene.name}/{l.name} enabled={l.enabled} bake={l.lightmapBakeType} forward={l.transform.forward:F3}");
            var rep = "Library/LastBuild.buildreport";
            if (File.Exists(rep))
            {
                var objs = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(rep);
                var br = objs.OfType<UnityEditor.Build.Reporting.BuildReport>().FirstOrDefault();
                if (br != null)
                {
                    sb.AppendLine($"last build {br.summary.platform} {br.summary.result} {br.summary.buildEndedAt:u}");
                    foreach (var pa in br.packedAssets) foreach (var c in pa.contents)
                        if (c.sourceAssetPath.Contains("SunShaft") || c.sourceAssetPath.Contains("sunshaft") || c.sourceAssetPath.Contains("Sun shaft")) sb.AppendLine($"   packed {c.sourceAssetPath} {c.type} {c.packedSize}B in {pa.shortPath}");
                }
            }
            return sb.ToString();
        }

        static string HierPath(Transform t) => t.parent == null ? t.name : HierPath(t.parent) + "/" + t.name;
    }
}
