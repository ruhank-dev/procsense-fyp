using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// One-shot, repeatable project setup for Meta Quest (plan p0-vr-platform-s01): Android + OpenXR loader + Quest features,
    /// IL2CPP / ARM64 / Vulkan, Quest-friendly quality, TextMeshPro essentials and the XR Interaction Toolkit samples.
    /// Batch: Unity.exe -batchmode -nographics -projectPath unity -executeMethod ProcSense.EditorTools.ProjectSetup.Run -logFile log.txt -quit
    /// </summary>
    public static class ProjectSetup
    {
        private const string XriVersionFolder = "Assets/Samples/XR Interaction Toolkit";

        [MenuItem("ProcSense/Project setup")]
        public static void Run()
        {
            ConfigureAndroid();
            ConfigureUrp();
            ConfigureOpenXr();
            ImportXriSamples();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] done");
        }

        private static void ConfigureAndroid()
        {
            PlayerSettings.companyName = "ProcSense";
            PlayerSettings.productName = "ProcSense";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.procsense.fyp");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
        }

        // Quest-friendly URP: no HDR, no post-processing cost, MSAA 4x, small shadow budget.
        private static void ConfigureUrp()
        {
            const string path = "Assets/Settings/URP_Quest.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Settings");
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Settings/URP_Quest_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
                asset.supportsHDR = false;
                asset.msaaSampleCount = 4;
                asset.shadowDistance = 8f;
                EditorUtility.SetDirty(asset);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
        }

        /// <summary>Imports the TextMeshPro essentials (fonts, shaders) and exits when Unity reports the import finished.</summary>
        public static void ImportTmp()
        {
            string[] found = System.IO.Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", System.IO.SearchOption.AllDirectories);
            if (found.Length == 0) { Debug.LogError("[ProjectSetup] TMP essentials package not found"); EditorApplication.Exit(1); return; }
            AssetDatabase.importPackageCompleted += _ => { Debug.Log("[ProjectSetup] tmp done"); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); };
            AssetDatabase.importPackageFailed += (_, e) => { Debug.LogError("[ProjectSetup] tmp failed " + e); EditorApplication.Exit(1); };
            AssetDatabase.ImportPackage(found[0], false);
        }

        private static void ConfigureOpenXr()
        {
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var perTarget = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                if (perTarget == null) continue;
                var manager = perTarget.AssignedSettings;
                if (manager == null)
                {
                    manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    AssetDatabase.CreateFolder("Assets", "XR");   // no-op if it exists
                    AssetDatabase.CreateAsset(manager, "Assets/XR/XRManagerSettings-" + group + ".asset");
                    perTarget.AssignedSettings = manager;
                }
                perTarget.InitManagerOnStart = true;
                XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
                EditorUtility.SetDirty(perTarget);
                EditorUtility.SetDirty(manager);
            }

            var android = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (android != null)
            {
                android.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                foreach (var feature in android.GetFeatures<OpenXRFeature>())
                {
                    bool wanted = feature is MetaQuestFeature
                        || feature.GetType().Name.Contains("MetaQuestTouchPlusControllerProfile")
                        || feature.GetType().Name.Contains("OculusTouchControllerProfile");
                    feature.enabled = wanted;
                    EditorUtility.SetDirty(feature);
                }
                EditorUtility.SetDirty(android);
            }
        }

        private static void ImportXriSamples()
        {
            if (AssetDatabase.IsValidFolder(XriVersionFolder)) return;
            var samples = UnityEditor.PackageManager.UI.Sample.FindByPackage("com.unity.xr.interaction.toolkit", null);
            foreach (var sample in samples.Where(s => s.displayName == "Starter Assets" || s.displayName == "XR Device Simulator"))
                sample.Import(UnityEditor.PackageManager.UI.Sample.ImportOptions.OverridePreviousImports);
        }
    }
}
