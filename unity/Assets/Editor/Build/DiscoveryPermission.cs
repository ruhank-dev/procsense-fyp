using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace ProcSense.EditorTools.Build
{
    /// <summary>
    /// P3-NET-06: the Quest build may take a Wi-Fi multicast lock while the laptop screen listens for the discovery beacon (Android drops broadcast datagrams otherwise). Adds the one permission
    /// it needs, CHANGE_WIFI_MULTICAST_STATE (a normal permission: no prompt), to the generated manifest. Nothing else in the manifest is touched.
    /// </summary>
    public sealed class DiscoveryPermission : IPostGenerateGradleAndroidProject
    {
        public const string Permission = "android.permission.CHANGE_WIFI_MULTICAST_STATE";
        public int callbackOrder => 50;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifest)) Add(manifest);
        }

        /// <summary>Adds the permission to a manifest file once; returns true when it was added.</summary>
        public static bool Add(string manifest)
        {
            var doc = new XmlDocument();
            doc.Load(manifest);
            const string android = "http://schemas.android.com/apk/res/android";
            var root = doc.DocumentElement;
            foreach (XmlElement e in root.GetElementsByTagName("uses-permission"))
                if (e.GetAttribute("name", android) == Permission) return false;
            var p = doc.CreateElement("uses-permission");
            p.SetAttribute("name", android, Permission);
            root.PrependChild(p);
            doc.Save(manifest);
            return true;
        }
    }
}
