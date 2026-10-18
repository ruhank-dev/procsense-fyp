using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class HospitalLighting
    {
        private const string Folder = "Assets/Settings/HospitalLighting";
        private static Color RGB(float r, float g, float b) => new Color(r, g, b);

        public static object Prepare(string name)
        {
            if (!new[] { "PatientBay", "Corridor", "OT" }.Contains(name)) throw new ArgumentException("Unknown room");
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play/baking first");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Existing unsaved scenes must be reviewed first");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == "Hospital lighting")) throw new InvalidOperationException("Lighting already authored; preserve it");
            string before = HospitalFinishes.Capture(name);
            Directory.CreateDirectory("Temp/LightingReview"); File.Copy(before, "Temp/LightingReview/" + name + "-before.png", true);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach (string materialName in new[] { "Diffused ceiling light", "Soft daylight glazing" })
            {
                var emissive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/" + materialName + ".mat");
                emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                emissive.EnableKeyword("_EMISSION");
                emissive.SetColor("_EmissionColor", materialName == "Diffused ceiling light" ? RGB(1.6f, 1.55f, 1.4f) : RGB(.65f, .85f, 1.1f));
                EditorUtility.SetDirty(emissive);
            }
            var root = new GameObject("Hospital lighting").transform;
            var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var floor = objects.Single(t => t.name == "Floor").GetComponent<Renderer>();
            float width = floor.bounds.size.x, depth = floor.bounds.size.z;
            var finishes = scene.GetRootGameObjects().Single(g => g.name == "Hospital finishes");
            foreach (var old in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))) old.enabled = false;
            foreach (var renderer in objects.Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null))
            {
                bool architecture = renderer.transform.IsChildOf(finishes.transform) || renderer.name == "Floor" || renderer.name.StartsWith("Wall ");
                if (!architecture) continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.ContributeGI);
                renderer.receiveGI = ReceiveGI.Lightmaps;
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null && AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/Models/Environment/"))
                {
                    if (!Unwrapping.GenerateSecondaryUVSet(mesh)) throw new InvalidOperationException("Lightmap UV generation failed: " + mesh.name);
                    EditorUtility.SetDirty(mesh);
                }
            }
            // A weak overhead key supplies dynamic contact shadows; ceiling lighting itself is baked.
            foreach (var overhead in finishes.GetComponentsInChildren<Renderer>().Where(r => r.name == "Ceiling" || r.name.Contains("ceiling grid") || r.name.Contains("luminaire")))
                overhead.shadowCastingMode = ShadowCastingMode.Off;
            var key = Light(root, "Soft clinical key", LightType.Directional, Vector3.zero, name == "PatientBay" ? Quaternion.Euler(70, 60, 0) : Quaternion.Euler(75, -30, 0), .32f, name == "PatientBay" ? RGB(1, .96f, .89f) : RGB(.91f, .96f, 1));
            key.lightmapBakeType = LightmapBakeType.Realtime; key.shadows = LightShadows.Soft; key.shadowStrength = .55f; key.shadowBias = .025f; key.shadowNormalBias = .15f;
            int rows = Mathf.Max(2, Mathf.RoundToInt(depth / 2.5f)), columns = width > 6 ? 2 : 1;
            for (int i = 0; i < rows; i++) for (int j = 0; j < columns; j++)
            {
                float z = -depth / 2 + (i + .5f) * depth / rows, x = columns == 1 ? 0 : (j == 0 ? -1.6f : 1.6f);
                var ceiling = Light(root, "Baked ceiling panel " + i + " " + j, LightType.Rectangle, new Vector3(x, 2.72f, z), Quaternion.Euler(90, 0, 0), name == "OT" ? 9 : 8, name == "PatientBay" ? RGB(1, .95f, .86f) : RGB(.80f, .90f, 1));
                ceiling.areaSize = new Vector2(.58f, 1.18f); ceiling.lightmapBakeType = LightmapBakeType.Baked;
            }
            if (name == "PatientBay")
            {
                var daylight = Light(root, "Baked window daylight", LightType.Rectangle, new Vector3(-2.28f, 1.92f, 0), Quaternion.Euler(0, 90, 0), 10, RGB(.76f, .87f, 1));
                daylight.areaSize = new Vector2(1.8f, 1); daylight.lightmapBakeType = LightmapBakeType.Baked;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = RGB(.24f, .28f, .34f); RenderSettings.ambientEquatorColor = RGB(.15f, .17f, .20f); RenderSettings.ambientGroundColor = RGB(.08f, .075f, .065f);
            RenderSettings.reflectionIntensity = .65f;
            var probes = new GameObject("Baked light probes", typeof(LightProbeGroup)); probes.transform.SetParent(root, false);
            var positions = new List<Vector3>();
            for (float x = -width / 2 + .45f; x < width / 2; x += 1.1f)
                for (float z = -depth / 2 + .45f; z < depth / 2; z += 1.1f)
                    foreach (float y in new[] { .35f, 1.25f, 2.2f }) positions.Add(new Vector3(x, y, z));
            probes.GetComponent<LightProbeGroup>().probePositions = positions.ToArray();
            var reflection = new GameObject("Baked room reflections", typeof(ReflectionProbe)); reflection.transform.SetParent(root, false); reflection.transform.localPosition = new Vector3(0, 1.4f, 0);
            var probe = reflection.GetComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Baked; probe.resolution = 128; probe.size = new Vector3(width - .15f, 2.7f, depth - .15f); probe.boxProjection = false; probe.blendDistance = .2f; probe.intensity = .65f; probe.nearClipPlane = .05f;
            var settings = new LightingSettings { name = name + " lighting", bakedGI = true, realtimeGI = false, autoGenerate = false, lightmapper = LightingSettings.Lightmapper.ProgressiveCPU, lightmapResolution = 24, lightmapMaxSize = 1024, lightmapPadding = 4, compressLightmaps = true, directionalityMode = LightmapsMode.NonDirectional, directSampleCount = 32, indirectSampleCount = 128, environmentSampleCount = 64, maxBounces = 3, ao = true, aoMaxDistance = .45f, aoExponentIndirect = 1, aoExponentDirect = .35f };
            AssetDatabase.CreateAsset(settings, Folder + "/" + name + ".lighting"); Lightmapping.lightingSettings = settings;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                var serializedPipeline = new SerializedObject(pipeline);
                serializedPipeline.FindProperty("m_SoftShadowsSupported").boolValue = true;
                serializedPipeline.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return new { room = name, probes = positions.Count, bakedCeilingPanels = rows * columns, realtimeLights = 1, readyToBake = true };
        }

        private static Light Light(Transform parent, string name, LightType type, Vector3 position, Quaternion rotation, float intensity, Color color)
        {
            var go = new GameObject(name, typeof(Light)); go.transform.SetParent(parent, false); go.transform.SetLocalPositionAndRotation(position, rotation);
            var light = go.GetComponent<Light>(); light.type = type; light.intensity = intensity; light.color = color; light.shadows = LightShadows.Soft; return light;
        }

        public static object BeginBake()
        {
            if (SceneManager.sceneCount != 1) throw new InvalidOperationException("Bake one room at a time: shells share their world coordinates");
            if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Lightmapping refused to start");
            return new { baking = true, scene = SceneManager.GetActiveScene().name };
        }

        public static object Finish(string name)
        {
            if (Lightmapping.isRunning) throw new InvalidOperationException("Bake still running");
            var scene = SceneManager.GetActiveScene();
            if (scene.name != name || LightmapSettings.lightmaps.Length == 0 || Lightmapping.lightingDataAsset == null) throw new InvalidOperationException("No completed bake for requested room");
            var reflection = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                .Single(g => g.name == "Hospital lighting").GetComponentInChildren<ReflectionProbe>();
            string path = Folder + "/" + name + " reflection.exr";
            if (!Lightmapping.BakeReflectionProbe(reflection, path)) throw new InvalidOperationException("Reflection capture failed");
            reflection.bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Temp/LightingReview");
            string image = HospitalFinishes.Capture(name); File.Copy(image, "Temp/LightingReview/" + name + "-after.png", true);
            return new { room = name, lightmaps = LightmapSettings.lightmaps.Length, lightProbes = LightmapSettings.lightProbes?.count, reflection = reflection.bakedTexture != null, image };
        }
    }
}
