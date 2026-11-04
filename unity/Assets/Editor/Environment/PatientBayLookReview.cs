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
    /// <summary>
    /// Read-only look-development review for the PatientBay: renders a fixed set of cameras at a fixed resolution from the saved
    /// scene state and dumps the lighting, probe, material and texture facts that make a before/after comparison reproducible.
    /// It never saves a scene or changes an asset. Editor measurements only; they are not headset numbers.
    /// Batch: -executeMethod ProcSense.EditorTools.PatientBayLookReview.Run -quit -lookTag NAME  (output: Logs/LookReview).
    /// </summary>
    public static class PatientBayLookReview
    {
        const string BayPath = "Assets/Scenes/PatientBay.unity";
        const int Width = 1672, Height = 941;

        static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "LookReview"));

        static string Tag()
        {
            var args = System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(args, "-lookTag");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "baseline";
        }

        public static readonly (string name, Vector3 pos, Vector3 look, float fov)[] Views =
        {
            ("ref", new Vector3(2.38f, 1.62f, -1.84f), new Vector3(-0.62f, 0.95f, 1.15f), 80),   // matches the reference photograph's viewpoint
            ("doorway", new Vector3(2.2f, 1.6f, -1.6f), new Vector3(-0.4f, 1.0f, 1.2f), 75),
            ("overview", new Vector3(2.3f, 2.2f, -1.8f), new Vector3(-0.8f, 0.8f, 1.2f), 80),
            ("bedside", new Vector3(1.5f, 1.55f, 0.6f), new Vector3(0f, 0.7f, 1.3f), 70),
            ("headwall", new Vector3(0.2f, 1.5f, -0.5f), new Vector3(0f, 1.4f, 1.95f), 70),
            ("window", new Vector3(1.0f, 1.5f, 0.2f), new Vector3(-2.5f, 1.5f, 0.0f), 70),
            ("ceiling", new Vector3(0.0f, 1.2f, -0.4f), new Vector3(0f, 2.75f, 0.6f), 85),
            ("equipment", new Vector3(-0.2f, 1.5f, 0.9f), new Vector3(-1.2f, 0.8f, 1.5f), 60),
            ("floor", new Vector3(1.2f, 1.2f, -1.0f), new Vector3(0.3f, 0.0f, 0.6f), 65),
            ("closeMonitor", new Vector3(0.4f, 1.45f, 0.9f), new Vector3(1.1f, 1.25f, 1.55f), 45),
            ("closeSink", new Vector3(-0.9f, 1.45f, -0.7f), new Vector3(-1.7f, 1.0f, -1.7f), 45),
            ("closeFabric", new Vector3(0.9f, 1.2f, 0.3f), new Vector3(0.1f, 0.72f, 0.6f), 50),
            ("closeCastor", new Vector3(1.0f, 0.55f, -0.6f), new Vector3(0.55f, 0.08f, 0.0f), 45),
            ("closeWindow", new Vector3(-0.2f, 1.7f, 0.2f), new Vector3(-2.4f, 1.9f, 0f), 40),
            ("patientSide", new Vector3(2.0f, 0.95f, 0.95f), new Vector3(0f, 0.80f, 0.95f), 42),
            ("patientFoot", new Vector3(0.0f, 1.05f, -0.75f), new Vector3(0f, 0.82f, 0.9f), 55),
            ("patientTop", new Vector3(0.0f, 2.7f, 0.95f), new Vector3(0f, 0.7f, 0.95f), 48),
            ("patientClose", new Vector3(1.1f, 1.3f, 0.35f), new Vector3(0f, 0.8f, 0.55f), 45),
            ("probeTest", new Vector3(1.9f, 0.9f, -1.5f), new Vector3(0.9f, 0.2f, -0.5f), 55),
            ("closeDesk", new Vector3(1.55f, 1.62f, -0.95f), new Vector3(0.92f, 1.08f, -1.72f), 48),
            ("closeKeys", new Vector3(1.0f, 1.42f, -1.22f), new Vector3(0.93f, 1.03f, -1.70f), 38),
            ("closeSteel", new Vector3(0.75f, 1.0f, 0.55f), new Vector3(1.05f, 0.72f, 1.55f), 50),
            ("floorLow", new Vector3(1.9f, 0.55f, -1.2f), new Vector3(-0.3f, 0.0f, 0.6f), 62),
            ("closeGown", new Vector3(1.15f, 1.35f, 0.85f), new Vector3(0.05f, 0.82f, 0.95f), 48),
            ("closeLaminate", new Vector3(-0.3f, 1.3f, -0.1f), new Vector3(-0.6f, 1.0f, -0.3f), 40),
        };

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene(BayPath, OpenSceneMode.Single);
            if (System.Environment.GetCommandLineArgs().Contains("-posePreview")) { PatientPose.ApplyPreview(); PatientPose.Report(Path.Combine(Out, Tag() + "-contact.txt")); }          // unsaved pose experiment
            if (System.Environment.GetCommandLineArgs().Contains("-probeTest"))
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (System.Environment.GetCommandLineArgs().Contains("-customRefl"))
                {
                    var cube = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Settings/HospitalLighting/PatientBay reflection.exr");
                    Debug.Log("[ProbeTest] custom cubemap " + (cube ? cube.name : "NULL"));
                    RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                    RenderSettings.customReflectionTexture = cube;
                    foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include)) rp.enabled = false;
                }
                var mirror = new Material(lit); mirror.SetFloat("_Metallic", 1f); mirror.SetFloat("_Smoothness", 1f); mirror.SetColor("_BaseColor", Color.white);
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.transform.position = new Vector3(0.9f, 0.45f, -0.35f); ball.transform.localScale = Vector3.one * .4f;
                ball.GetComponent<MeshRenderer>().sharedMaterial = mirror;
                var floorMat = GameObject.Find("Floor").GetComponent<MeshRenderer>().sharedMaterial;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.transform.position = new Vector3(0.9f, 0.003f, -0.9f); quad.transform.rotation = Quaternion.Euler(90, 0, 0); quad.transform.localScale = Vector3.one * 1.2f;
                quad.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
            }
            var tag = Tag();
            Dump(Path.Combine(Out, tag + "-facts.txt"));
            var v0 = Views[1];
            for (var i = 0; i < 3; i++) Render(null, v0.pos, v0.look, v0.fov, out _);          // warm-up: shader variants and lightmaps finish loading
            var stats = new StringBuilder("view, visibleRenderers, submeshes, materials, triangles(k), shadowCasters, cpuRenderMs(avg of 10)\n");
            foreach (var v in Views)
            {
                Render(Path.Combine(Out, $"{tag}-{v.name}.png"), v.pos, v.look, v.fov, out var s);
                stats.AppendLine($"{v.name}, {s}");
            }
            File.WriteAllText(Path.Combine(Out, tag + "-stats.txt"), stats.ToString());
        }

        /// <summary>Renders one view at the review resolution. Editor measurements only (CPU submit time, UnityStats), never headset numbers.</summary>
        static void Render(string file, Vector3 pos, Vector3 look, float fov, out string stats)
        {
            var go = new GameObject("LookReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos; cam.transform.LookAt(look);
            cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 10; i++) cam.Render();
            var ms = sw.Elapsed.TotalMilliseconds / 10;
            // headless runs have no profiler counters, so count what the camera can see: frustum-visible renderers
            // (about one draw call each under the SRP Batcher), their submeshes, triangles, unique materials and shadow casters
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            int draws = 0, sub = 0, casters = 0; long tris = 0;
            var mats = new System.Collections.Generic.HashSet<Material>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                if (r is MeshRenderer mr)
                {
                    var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    draws++; sub += mf.sharedMesh.subMeshCount; tris += mf.sharedMesh.triangles.Length / 3;
                }
                else if (r is SkinnedMeshRenderer sm && sm.sharedMesh != null) { draws++; sub += sm.sharedMesh.subMeshCount; tris += sm.sharedMesh.triangles.Length / 3; }
                else continue;
                if (r.shadowCastingMode != ShadowCastingMode.Off) casters++;
                foreach (var m in r.sharedMaterials) if (m) mats.Add(m);
            }
            stats = $"{draws}, {sub}, {mats.Count}, {tris / 1000f:F1}, {casters}, {ms:F2}";
            if (file != null)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                RenderTexture.active = prev;
                Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }

        static void Dump(string file)
        {
            var sb = new StringBuilder();
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            sb.AppendLine($"render pipeline: {(rp ? rp.name : "none")}  colorSpace={PlayerSettings.colorSpace}");
            if (rp != null)
                sb.AppendLine($"hdr={rp.supportsHDR} msaa={rp.msaaSampleCount} renderScale={rp.renderScale} mainShadowRes={rp.mainLightShadowmapResolution} soft={rp.supportsSoftShadows} cascades={rp.shadowCascadeCount} shadowDist={rp.shadowDistance} addLights={rp.additionalLightsRenderingMode} reflBlend={rp.reflectionProbeBlending} boxProj={rp.reflectionProbeBoxProjection}");
            sb.AppendLine($"ambientMode={RenderSettings.ambientMode} ambientLight={RenderSettings.ambientLight} skybox={(RenderSettings.skybox ? RenderSettings.skybox.name : "none")} defaultReflection={RenderSettings.defaultReflectionMode} intensity={RenderSettings.reflectionIntensity}");
            sb.AppendLine($"lightmapsMode={LightmapSettings.lightmapsMode} lightmaps={LightmapSettings.lightmaps.Length}");
            long lmBytes = 0;
            foreach (var l in LightmapSettings.lightmaps)
                foreach (var t in new[] { l.lightmapColor, l.lightmapDir, l.shadowMask })
                    if (t != null) { sb.AppendLine($"  lightmap {t.name} {t.width}x{t.height} {t.format}"); lmBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t); }
            sb.AppendLine($"lightmap memory (runtime, editor) = {lmBytes / 1048576.0:F2} MB");
            var probes = LightmapSettings.lightProbes;
            sb.AppendLine($"light probes in scene data: {(probes ? probes.count : 0)}");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include).OrderBy(l => l.name))
                sb.AppendLine($"light {l.name} type={l.type} mode={l.lightmapBakeType} enabled={l.enabled && l.gameObject.activeInHierarchy} shadows={l.shadows} int={l.intensity:F2} colour={l.color} temp={(l.useColorTemperature ? l.colorTemperature : 0)} range={l.range:F1} pos={l.transform.position:F2} size={(l.type == LightType.Rectangle ? l.areaSize.ToString() : "")}");
            foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include))
                sb.AppendLine($"reflection probe {p.name} mode={p.mode} res={p.resolution} boxProj={p.boxProjection} size={p.size} center={p.transform.position:F2} importance={p.importance} blend={p.blendDistance} hdr={p.hdr} texture={(p.bakedTexture ? p.bakedTexture.name : "none")}");
            foreach (var g in Object.FindObjectsByType<LightProbeGroup>(FindObjectsInactive.Include))
                sb.AppendLine($"light probe group {g.name} positions={g.probePositions.Length}");

            var rs = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(r => r.enabled).ToArray();
            long tris = 0; int casters = 0, lmStatic = 0;
            var mats = new System.Collections.Generic.HashSet<Material>();
            var texs = new System.Collections.Generic.HashSet<Texture>();
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
                if (r.shadowCastingMode != ShadowCastingMode.Off) casters++;
                if (r.lightmapIndex >= 0) lmStatic++;
                foreach (var m in r.sharedMaterials) if (m) mats.Add(m);
            }
            foreach (var m in mats) foreach (var id in m.GetTexturePropertyNameIDs()) { var t = m.GetTexture(id); if (t) texs.Add(t); }
            long texBytes = texs.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            sb.AppendLine($"mesh renderers={rs.Length} (lightmapped={lmStatic}, shadow casters={casters}) tris={tris} materials={mats.Count} textures={texs.Count} texture memory (editor runtime)={texBytes / 1048576.0:F1} MB");
            sb.AppendLine("--- materials");
            foreach (var m in mats.OrderBy(m => m.name))
                sb.AppendLine($"mat {m.name} shader={m.shader.name} base={(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "")} metal={(m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1):F2} smooth={(m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : -1):F2} emission={(m.IsKeywordEnabled("_EMISSION") ? m.GetColor("_EmissionColor").ToString() : "off")} maps=[{string.Join(",", m.GetTexturePropertyNames().Where(n => m.GetTexture(n)).Select(n => n))}]");
            sb.AppendLine("--- renderers");
            foreach (var r in rs.OrderBy(r => r.transform.GetHierarchyPath()))
            {
                var mf = r.GetComponent<MeshFilter>();
                sb.AppendLine($"R {r.transform.GetHierarchyPath()} mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name : "-")} tris={(mf && mf.sharedMesh ? mf.sharedMesh.triangles.Length / 3 : 0)} c={r.bounds.center:F2} s={r.bounds.size:F2} static={GameObjectUtility.GetStaticEditorFlags(r.gameObject)} lm={r.lightmapIndex} cast={r.shadowCastingMode} mats=[{string.Join("|", r.sharedMaterials.Select(m => m ? m.name : "NULL"))}]");
            }
            File.WriteAllText(file, sb.ToString());
        }

        static string GetHierarchyPath(this Transform t)
        {
            var n = t.name;
            while (t.parent != null) { t = t.parent; n = t.name + "/" + n; }
            return n;
        }
    }
}
