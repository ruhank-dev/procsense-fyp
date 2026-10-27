using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>One build variant (p0-vr-platform-s07): its name, the scripting define it sets, the APK it writes and whether it is a development build.</summary>
    public sealed class BuildVariantSpec
    {
        public string Name = "", Define = "", Apk = "";
        public bool Development;
    }

    /// <summary>
    /// Command-line and menu builds of the Quest APK. Variants (p0-vr-platform-s07): Dev (development build, profiler attachable, PROCSENSE_DEV), Demo (release, PROCSENSE_DEMO) and Study (release, PROCSENSE_STUDY);
    /// the runtime reads the define through ProcSense.VR.Boot.BuildVariant. Menu: ProcSense > Build > Dev / Demo / Study. Batch mode:
    /// -executeMethod ProcSense.EditorTools.BuildScript.BuildQuest [-variant dev|demo|study] (task unity-build -- demo). The APK goes to artifacts/unity/ProcSense-&lt;variant&gt;.apk (git-ignored) with its
    /// SHA-256 next to it (.sha256), for the release checklist (docs/RELEASE.md). The content folder is copied into StreamingAssets first, because the player reads content from there. The Android defines are put
    /// back as they were after the build, so a build never leaves the project changed.
    /// </summary>
    public static class BuildScript
    {
        private const string StreamingContent = "Assets/StreamingAssets/content";
        private static readonly string[] AllDefines = { "PROCSENSE_DEV", "PROCSENSE_DEMO", "PROCSENSE_STUDY" };

        public static readonly BuildVariantSpec[] Variants =
        {
            new BuildVariantSpec { Name = "dev", Define = "PROCSENSE_DEV", Apk = "ProcSense-dev.apk", Development = true },
            new BuildVariantSpec { Name = "demo", Define = "PROCSENSE_DEMO", Apk = "ProcSense-demo.apk", Development = false },
            new BuildVariantSpec { Name = "study", Define = "PROCSENSE_STUDY", Apk = "ProcSense-study.apk", Development = false },
        };

        public static BuildVariantSpec Variant(string name) =>
            Variants.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("unknown build variant '" + name + "' (dev, demo or study)");

        /// <summary>The defines a variant builds with: the project's own, without any other variant's, plus its own.</summary>
        public static string[] DefinesFor(BuildVariantSpec v, string[] current) => current.Where(d => !AllDefines.Contains(d)).Concat(new[] { v.Define }).Distinct().ToArray();

        [MenuItem("ProcSense/Build/Dev")] private static void MenuDev() => Build(Variant("dev"), false);
        [MenuItem("ProcSense/Build/Demo")] private static void MenuDemo() => Build(Variant("demo"), false);
        [MenuItem("ProcSense/Build/Study")] private static void MenuStudy() => Build(Variant("study"), false);

        /// <summary>Batch entry point: the variant comes from -variant (dev when absent).</summary>
        public static void BuildQuest()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-variant");
            var v = Variant(i >= 0 && i + 1 < args.Length ? args[i + 1] : "dev");
            bool ok = Build(v, true);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(BuildVariantSpec v, bool batch)
        {
            CopyContent();
            Directory.CreateDirectory("../artifacts/unity");
            var target = NamedBuildTarget.Android;
            var before = PlayerSettings.GetScriptingDefineSymbols(target);
            try
            {
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", DefinesFor(v, before.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))));
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = "../artifacts/unity/" + v.Apk,
                    target = BuildTarget.Android,
                    options = v.Development ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.None,
                };
                if (batch) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                var report = BuildPipeline.BuildPlayer(options);
                bool ok = report.summary.result == BuildResult.Succeeded;
                string sha = ok ? Checksum(options.locationPathName) : "";
                Debug.Log("[BuildScript] " + v.Name + " " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors + (sha.Length > 0 ? " sha256=" + sha : ""));
                return ok;
            }
            finally { PlayerSettings.SetScriptingDefineSymbols(target, before); }
        }

        /// <summary>Writes the APK's SHA-256 next to it (ProcSense-demo.apk.sha256) and returns it.</summary>
        public static string Checksum(string apk)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(apk);
            string hex = string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
            File.WriteAllText(apk + ".sha256", hex + "  " + Path.GetFileName(apk) + "\n");
            return hex;
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
