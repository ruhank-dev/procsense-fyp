using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Companion of the PlayMode TextureCropProbe (audit lane 3): sets a Standalone-only import override on the text-bearing textures so the desktop shows what a given Android compression looks like
    /// (Unity decodes ASTC in software on the desktop). TEXPROBE_FMT = U (RGBA32 reference), A66 (ASTC 6x6, today's Android setting), A55, A44 (ASTC 4x4), or CLEAR.
    /// Batch: -executeMethod ProcSense.EditorTools.TextureQualityProbe.Apply.
    ///
    /// Leaves nothing behind: before the first change each texture's .meta file is copied to unity/Logs/texprobe_meta_backup; CLEAR copies those files back byte for byte and re-imports, so a texture that never had a
    /// Standalone override gets no new block and one that had an inactive block keeps it (git status stays empty). Every state logs the format Unity really loaded, as proof that the state was applied.
    /// </summary>
    public static class TextureQualityProbe
    {
        private static readonly string[] Targets =
        {
            "reception_screen", "tv_welcome", "bulletin_board", "poster_01", "poster_02", "poster_03", "poster_04", "poster_05", "poster_06", "poster_10", "poster_11", "poster_12",
            "monitor_ui", "monitor_labels",
        };

        private static string BackupDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/texprobe_meta_backup"));

        /// <summary>Size of the imported (stored) texture data via the editor's internal TextureUtil, or -1 when that call is not available: shows that an ASTC override really changes what is stored.</summary>
        private static long StorageBytes(Texture t)
        {
            if (t == null) return -1;
            try
            {
                var tu = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.TextureUtil");
                var m = tu != null ? (tu.GetMethod("GetStorageMemorySizeLong", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) ?? tu.GetMethod("GetStorageMemorySize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)) : null;
                return m == null ? -1 : System.Convert.ToInt64(m.Invoke(null, new object[] { t }));
            }
            catch { return -1; }
        }

        public static void Apply()
        {
            string fmt = (System.Environment.GetEnvironmentVariable("TEXPROBE_FMT") ?? "CLEAR").ToUpperInvariant();
            // The textures the Editor loads in play mode are the ones imported for the ACTIVE build target (Android after an APK build), so that is the platform to override.
            string platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "Standalone";
            Debug.Log("[TextureQualityProbe] active build target " + EditorUserBuildSettings.activeBuildTarget + ", overriding the " + platform + " import");
            int n = 0, differs = 0, notApplied = 0;
            Directory.CreateDirectory(BackupDir);
            foreach (var name in Targets)
            {
                var path = AssetDatabase.FindAssets(name + " t:Texture2D", new[] { "Assets/Textures" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
                if (path == null) { Debug.LogWarning("[TextureQualityProbe] missing " + name); continue; }
                string meta = path + ".meta", backup = Path.Combine(BackupDir, name + ".meta.bak");
                if (fmt == "CLEAR")
                {
                    if (!File.Exists(backup)) continue; // never changed by the probe: leave the file alone
                    File.Copy(backup, meta, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    bool same = File.ReadAllBytes(meta).SequenceEqual(File.ReadAllBytes(backup));
                    if (!same) { differs++; Debug.LogWarning("[TextureQualityProbe] " + name + ": .meta differs from the backup after the re-import"); }
                    else File.Delete(backup);
                    n++; continue;
                }
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { Debug.LogWarning("[TextureQualityProbe] not a texture importer: " + name); continue; }
                if (!File.Exists(backup)) File.Copy(meta, backup, false);
                var f = fmt == "U" ? TextureImporterFormat.RGBA32 : fmt == "A66" ? TextureImporterFormat.ASTC_6x6 : fmt == "A55" ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_4x4;
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = platform, overridden = true, maxTextureSize = 2048, format = f, compressionQuality = 100, textureCompression = fmt == "U" ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed });
                ti.SaveAndReimport();
                var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var applied = ((TextureImporter)AssetImporter.GetAtPath(path)).GetPlatformTextureSettings(platform);
                long storage = StorageBytes(loaded);
                Debug.Log("[TextureQualityProbe] " + fmt + " " + name + ": importer Standalone format=" + applied.format + " overridden=" + applied.overridden + ", loaded texture format=" + (loaded != null ? loaded.format.ToString() : "null") + " " + (loaded != null ? loaded.width + "x" + loaded.height : "") + ", storage bytes=" + (storage >= 0 ? storage.ToString() : "n/a"));
                if (!applied.overridden || applied.format != f) { notApplied++; Debug.LogError("[TextureQualityProbe] state NOT applied for " + name + ": wanted " + f + ", importer says " + applied.format); }
                n++;
            }
            Debug.Log("[TextureQualityProbe] " + fmt + " done on " + n + " textures" + (differs > 0 ? ", " + differs + " .meta files differ from their backup" : "") + (notApplied > 0 ? ", " + notApplied + " states NOT applied" : ""));
            EditorApplication.Exit(notApplied > 0 ? 4 : differs > 0 ? 3 : 0);
        }
    }
}
