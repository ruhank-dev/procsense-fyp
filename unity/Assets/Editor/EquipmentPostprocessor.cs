using UnityEditor;

namespace ProcSense.EditorTools
{
    public sealed class EquipmentPostprocessor : AssetPostprocessor
    {
        internal const string ModelFolder = "Assets/Models/Equipment/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private UnityEngine.Material OnAssignMaterialModel(UnityEngine.Material material, UnityEngine.Renderer renderer)
        {
            if (!assetPath.StartsWith(ModelFolder, System.StringComparison.Ordinal)) return null;
            var shared = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Materials/Equipment/" + material.name + ".mat");
            if (shared == null) UnityEngine.Debug.LogWarning("Equipment shared material missing: " + material.name);
            return shared;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.StartsWith(ModelFolder, System.StringComparison.Ordinal) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                string captured = path;
                EditorApplication.delayCall += () => EquipmentPrefabBuilder.CreateIfMissing(captured);
            }
        }
    }
}
