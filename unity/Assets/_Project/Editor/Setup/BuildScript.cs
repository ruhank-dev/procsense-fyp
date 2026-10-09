using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Command-line builds. Quest APK: -executeMethod ProcSense.EditorTools.BuildScript.BuildQuest
    /// The APK goes to artifacts/unity/ProcSense.apk (git-ignored). The content folder is copied into StreamingAssets first,
    /// because the player reads content from there (the Editor reads the repo's content/ folder directly).
    /// </summary>
    public static class BuildScript
    {
        private const string StreamingContent = "Assets/StreamingAssets/content";

        public static void BuildQuest()
        {
            CopyContent();
            Directory.CreateDirectory("../artifacts/unity");
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "../artifacts/unity/ProcSense.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[BuildScript] " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static void CopyContent()
        {
            if (Directory.Exists(StreamingContent)) Directory.Delete(StreamingContent, true);
            CopyDir("../content", StreamingContent);
            AssetDatabase.Refresh();
        }

        private static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
        }
    }
}
