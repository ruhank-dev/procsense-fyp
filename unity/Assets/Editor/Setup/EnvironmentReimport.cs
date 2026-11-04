using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Forces Unity to rebuild its import cache for the baked lighting and settings assets (lightmaps, reflection probes, volume profiles, render pipeline settings), exactly as a fresh clone imports them:
    /// <c>Unity.exe -batchmode -quit -projectPath unity -executeMethod ProcSense.EditorTools.EnvironmentReimport.Run</c>. Used to tell a stale local import cache from a real difference between a working project and a fresh clone.
    /// </summary>
    public static class EnvironmentReimport
    {
        /// <summary>Puts the project back on the Windows desktop build target. A Quest build (BuildScript.BuildQuest) leaves the Editor on Android, where the Editor imports textures and picks quality settings for the headset, so the Editor view is slightly different from desktop.</summary>
        public static void SwitchToDesktop()
        {
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.Build.NamedBuildTarget.Standalone, BuildTarget.StandaloneWindows64);
            Debug.Log("[EnvironmentReimport] active build target is now " + EditorUserBuildSettings.activeBuildTarget + " (switch " + (ok ? "ok" : "failed") + ")");
        }

        public static void Run()
        {
            foreach (var folder in new[] { "Assets/Scenes", "Assets/Settings", "Assets/Textures/Environment/Sky" })
                AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnvironmentReimport] forced reimport of Assets/Scenes, Assets/Settings and the sky HDRs");
        }
    }
}
