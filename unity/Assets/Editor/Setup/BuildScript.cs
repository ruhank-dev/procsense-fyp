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
        /// <summary>Extra defines (the diagnostic variants: PROCSENSE_PERF_DIAG, and PROCSENSE_PERF_GPUSKIN for the GPU-skinning one).</summary>
        public string[] Extra = Array.Empty<string>();
        /// <summary>Diagnostic build (P3-PRF-01): frame-timing statistics on for the build only.</summary>
        public bool Diagnostic;
        /// <summary>Build with GPU skinning (mesh deformation on the GPU) for the build only, to measure CPU against GPU skinning on the headset.</summary>
        public bool GpuSkinning;
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
        private static readonly string[] AllDefines = { "PROCSENSE_DEV", "PROCSENSE_DEMO", "PROCSENSE_STUDY", "PROCSENSE_PERF_DIAG", "PROCSENSE_PERF_GPUSKIN" };

        public static readonly BuildVariantSpec[] Variants =
        {
            new BuildVariantSpec { Name = "dev", Define = "PROCSENSE_DEV", Apk = "ProcSense-dev.apk", Development = true },
            new BuildVariantSpec { Name = "demo", Define = "PROCSENSE_DEMO", Apk = "ProcSense-demo.apk", Development = false },
            new BuildVariantSpec { Name = "study", Define = "PROCSENSE_STUDY", Apk = "ProcSense-study.apk", Development = false },
            // Quest benchmark builds (P3-PRF-01, DC-13 part B): release builds like Demo, plus the diagnostic readout, toggles and CSV log; never shipped
            new BuildVariantSpec { Name = "diag", Define = "PROCSENSE_DEMO", Extra = new[] { "PROCSENSE_PERF_DIAG" }, Apk = "ProcSense-diag.apk", Diagnostic = true },
            new BuildVariantSpec { Name = "diag-gpuskin", Define = "PROCSENSE_DEMO", Extra = new[] { "PROCSENSE_PERF_DIAG", "PROCSENSE_PERF_GPUSKIN" }, Apk = "ProcSense-diag-gpuskin.apk", Diagnostic = true, GpuSkinning = true },
        };

        public static BuildVariantSpec Variant(string name) =>
            Variants.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("unknown build variant '" + name + "' (dev, demo, study, diag or diag-gpuskin)");

        /// <summary>The defines a variant builds with: the project's own, without any other variant's, plus its own.</summary>
        public static string[] DefinesFor(BuildVariantSpec v, string[] current) => current.Where(d => !AllDefines.Contains(d)).Concat(new[] { v.Define }).Concat(v.Extra).Distinct().ToArray();

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

        /// <summary>Batch entry point for a Windows desktop player (development build, same scenes and content as the Quest build): artifacts/unity/desktop/ProcSense-&lt;variant&gt;.exe. Added for the environment handoff check (3 Nov 2026).</summary>
        public static void BuildDesktop()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-variant");
            var v = Variant(i >= 0 && i + 1 < args.Length ? args[i + 1] : "dev");
            CopyContent();
            Directory.CreateDirectory("../artifacts/unity/desktop");
            var target = NamedBuildTarget.Standalone;
            var before = PlayerSettings.GetScriptingDefineSymbols(target);
            bool ok = false;
            try
            {
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", DefinesFor(v, before.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))));
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = "../artifacts/unity/desktop/ProcSense-" + v.Name + ".exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = v.Development ? BuildOptions.Development : BuildOptions.None,
                };
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                var report = BuildPipeline.BuildPlayer(options);
                ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log("[BuildScript] desktop " + v.Name + " " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors);
            }
            finally { PlayerSettings.SetScriptingDefineSymbols(target, before); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(BuildVariantSpec v, bool batch)
        {
            // a Quest build without the OpenXR loader runs as a flat Android window: black in the headset
            ProjectSetup.EnsureOpenXr();
            if (!ProjectSetup.AndroidStartsOpenXr())
            {
                Debug.LogError("[BuildScript] " + v.Name + " refused: the Android XR settings do not start the OpenXR loader (run ProcSense > Project setup)");
                return false;
            }
            CopyContent();
            Directory.CreateDirectory("../artifacts/unity");
            var target = NamedBuildTarget.Android;
            var before = PlayerSettings.GetScriptingDefineSymbols(target);
            var skinningBefore = PlayerSettings.meshDeformation;
            bool timingBefore = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", DefinesFor(v, before.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))));
                if (v.Diagnostic) PlayerSettings.enableFrameTimingStats = true;
                if (v.GpuSkinning) PlayerSettings.meshDeformation = MeshDeformation.GPU;
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
                if (ok) WriteReport(report, options.locationPathName + ".report.txt");
                return ok;
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(target, before);
                PlayerSettings.meshDeformation = skinningBefore;          // a diagnostic build never leaves the project changed
                PlayerSettings.enableFrameTimingStats = timingBefore;
            }
        }

        /// <summary>Writes the build report's facts next to the APK (size, time, the largest packed assets) for the Quest performance analysis (docs/qa/quest2_performance_results.md).</summary>
        private static void WriteReport(BuildReport report, string path)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "result " + report.summary.result, "total_size_bytes " + report.summary.totalSize, "build_seconds " + (int)report.summary.totalTime.TotalSeconds,
                "warnings " + report.summary.totalWarnings, "errors " + report.summary.totalErrors, "largest packed assets:",
            };
            foreach (var a in report.packedAssets.SelectMany(p => p.contents).OrderByDescending(c => c.packedSize).Take(40))
                lines.Add("  " + a.packedSize + " " + a.sourceAssetPath);
            File.WriteAllLines(path, lines);
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
