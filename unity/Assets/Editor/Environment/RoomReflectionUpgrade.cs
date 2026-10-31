using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Re-captures PatientBay's room reflection as a high-dynamic-range 512 px cube (the old one was 256 px, stored as ETC2 RGB = low dynamic range,
    /// so bright windows and ceiling panels were clipped and polished metal had nothing contrasty to reflect). Lightmaps are not re-baked.
    /// Windows/standalone keep half-float; Android (Quest) gets ASTC HDR. Args: -res 512
    /// </summary>
    public static class RoomReflectionUpgrade
    {
        const string LocalPath = "Assets/Settings/HospitalLighting/PatientBay monitor reflection.exr";

        /// <summary>
        /// A baked probe captured at the monitor's own position (so the pole, bezel and glass reflect what is around the monitor, not the room
        /// centre), with its box set to the room volume so box projection keeps the reflected walls, window and ceiling panels in place.
        /// </summary>
        public static void LocalProbe()
        {
            int res = 256; var a = Environment.GetCommandLineArgs(); var i = Array.IndexOf(a, "-res"); if (i >= 0) res = int.Parse(a[i + 1]);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var body = all.First(t => t.name == "ATTACH_ECG").root.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.EndsWith("_Body"));
            var roomProbe = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)).First(p => p.name == "Baked room reflections");
            var old = all.FirstOrDefault(t => t.name == "Monitor local reflections"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Monitor local reflections"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(roomProbe.transform.parent, true);
            go.transform.position = body.bounds.center;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked; probe.hdr = true; probe.resolution = res; probe.boxProjection = true; probe.importance = 2; probe.intensity = 1f; probe.blendDistance = .2f;
            probe.size = roomProbe.size; probe.center = roomProbe.transform.position + roomProbe.center - go.transform.position;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var oldHdr = urp.supportsHDR; urp.supportsHDR = true;
            try { if (!Lightmapping.BakeReflectionProbe(probe, LocalPath)) throw new InvalidOperationException("local reflection capture failed"); }
            finally { urp.supportsHDR = oldHdr; }
            AssetDatabase.ImportAsset(LocalPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(LocalPath);
            ti.textureShape = TextureImporterShape.TextureCube; ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear; ti.maxTextureSize = res;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = res, format = TextureImporterFormat.RGBAHalf });
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = res, format = TextureImporterFormat.ASTC_HDR_6x6 });
            ti.SaveAndReimport();
            // A Baked-mode probe keeps its texture in the scene's lighting data, which this capture does not touch, so the reference would not
            // survive a save (the room probe reads "texture=none" for that reason). Custom mode serialises the cube map with the scene.
            probe.mode = ReflectionProbeMode.Custom; probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(LocalPath);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[RoomReflection] local probe at {go.transform.position:F2} box {probe.size:F2} center {probe.center:F2} -> {probe.customBakedTexture.width}px (custom)");
        }

        /// <summary>
        /// Keeps the local probe's box off the shared wall: Corridor's door leaf and frame sit just past PatientBay's +X wall and would otherwise
        /// pick up this probe instead of their own scene's reflection. Pulls the +X face in by 6 cm and the -X face with it (box stays centred on the room).
        /// </summary>
        public static void TrimLocalProbe()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var probe = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)).First(p => p.name == "Monitor local reflections");
            var room = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)).First(p => p.name == "Baked room reflections");
            probe.size = new Vector3(room.size.x - .24f, room.size.y, room.size.z);
            probe.center = room.transform.position + room.center - probe.transform.position;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RoomReflection] local probe box {probe.size:F2}");
        }

        const string Path_ = "Assets/Settings/HospitalLighting/PatientBay reflection.exr";

        public static void Run()
        {
            int res = 512; var a = Environment.GetCommandLineArgs(); var i = Array.IndexOf(a, "-res"); if (i >= 0) res = int.Parse(a[i + 1]);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var probe = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)).First();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var oldHdr = urp.supportsHDR; urp.supportsHDR = true;                              // capture in HDR; the shipping frame buffer setting is restored below
            var wasEnabled = probe.enabled; probe.enabled = true; probe.hdr = true; probe.resolution = res; probe.mode = ReflectionProbeMode.Baked;
            try
            {
                if (!Lightmapping.BakeReflectionProbe(probe, Path_)) throw new InvalidOperationException("reflection capture failed");
            }
            finally { urp.supportsHDR = oldHdr; }
            AssetDatabase.ImportAsset(Path_, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(Path_);
            ti.textureShape = TextureImporterShape.TextureCube; ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear;
            ti.maxTextureSize = res;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "DefaultTexturePlatform", overridden = false, maxTextureSize = res, textureCompression = TextureImporterCompression.Uncompressed });
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = res, format = TextureImporterFormat.RGBAHalf });
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = res, format = TextureImporterFormat.ASTC_HDR_6x6 });
            ti.SaveAndReimport();
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(Path_);
            probe.bakedTexture = cube;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = cube; RenderSettings.reflectionIntensity = 1f;
            probe.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[RoomReflection] {cube.width}x{cube.height} format={cube.format} mips={cube.mipmapCount}");
        }
    }
}
