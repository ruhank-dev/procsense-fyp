using System.IO;
using ProcSense.VR.Auth;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Creates the one build setting the headset needs to reach the hosted service: Assets/Resources/ProcSenseService.asset.</summary>
    public static class ServiceSettingsSetup
    {
        public const string AssetPath = "Assets/Resources/" + ServiceSettings.ResourceName + ".asset";
        public const string DefaultUrl = "https://procsense.onrender.com";

        [MenuItem("ProcSense/Service settings (create or select)")]
        public static void CreateOrSelect()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ServiceSettings>(AssetPath) ?? Create(DefaultUrl);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        /// <summary>Used from the command line too: Unity -batchmode -executeMethod ProcSense.EditorTools.ServiceSettingsSetup.CreateForBatch</summary>
        public static void CreateForBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<ServiceSettings>(AssetPath) == null) Create(DefaultUrl);
            AssetDatabase.SaveAssets();
        }

        public static ServiceSettings Create(string baseUrl)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var settings = ScriptableObject.CreateInstance<ServiceSettings>();
            settings.BaseUrl = baseUrl;
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
