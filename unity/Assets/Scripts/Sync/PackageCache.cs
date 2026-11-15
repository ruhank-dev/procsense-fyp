using System;
using System.IO;
using UnityEngine;

namespace ProcSense.VR.Sync
{
    /// <summary>Where cached packages are kept. A seam so tests use memory; the real one is a folder on the device.</summary>
    public interface IPackageStorage { string Read(string key); void Write(string key, string text); }

    public sealed class FilePackageStorage : IPackageStorage
    {
        private readonly string _folder;
        public FilePackageStorage(string folder = null) { _folder = folder ?? Path.Combine(Application.persistentDataPath, "package_cache"); }
        private string PathOf(string key) => Path.Combine(_folder, key + ".package.json");
        public string Read(string key) { try { var p = PathOf(key); return File.Exists(p) ? File.ReadAllText(p) : ""; } catch (Exception) { return ""; } }
        public void Write(string key, string text)
        {
            try { Directory.CreateDirectory(_folder); var tmp = PathOf(key) + ".tmp"; File.WriteAllText(tmp, text); if (File.Exists(PathOf(key))) File.Delete(PathOf(key)); File.Move(tmp, PathOf(key)); }
            catch (Exception) { /* a cache that cannot be written is only a missing cache */ }
        }
    }

    /// <summary>
    /// The last good generated package per scenario family and level, kept on the headset (P3-NET-14). A package is cached only after it matched its hash; it is read back only if it still does, so a damaged file is never played.
    /// The cache never decides what may be played: the engine's mode gate does, exactly as for a fresh package.
    /// </summary>
    public sealed class PackageCache
    {
        public static PackageCache Default = new PackageCache(new FilePackageStorage());
        private readonly IPackageStorage _storage;
        public PackageCache(IPackageStorage storage) { _storage = storage; }

        private static string Key(string scenario, string level) => (scenario ?? "") + "_" + (level ?? "");

        public void Save(string scenario, string level, string hash, string text)
        {
            if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(text)) return;
            _storage.Write(Key(scenario, level), hash + "\n" + text);
        }

        /// <summary>The cached package for this family and level, or null when none or when it no longer matches its hash.</summary>
        public FetchedPackage Load(string scenario, string level)
        {
            var raw = _storage.Read(Key(scenario, level));
            int cut = raw.IndexOf('\n');
            if (cut != 64) return null;
            var hash = raw.Substring(0, 64);
            var text = raw.Substring(cut + 1);
            if (PackageClient.Sha256Hex(text) != hash) return null;
            return new FetchedPackage { Ok = true, Text = text, Hash = hash, Cached = true, Error = "The laptop could not be used, so the last scenario saved on this headset is used." };
        }
    }
}
