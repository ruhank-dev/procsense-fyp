using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    public static class EquipmentDropSetup
    {
        [MenuItem("ProcSense/Equipment/Enable Gravity and Update Cuff")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(WorkspaceMenu.BayPath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Open and save PatientBay first.");
            const string source = "Logs/CuffBuild/bp_cuff.fbx";
            if (!File.Exists(source)) throw new FileNotFoundException("Generate the padded cuff first", source);
            // Replace model contents through AssetDatabase import, retaining its original .meta/GUID.
            File.Copy(source, "Assets/Models/Equipment/bp_cuff.fbx", true);
            File.Copy("Logs/CuffBuild/bp_cuff.equipment.json", "Assets/Models/Equipment/bp_cuff.equipment.json", true);
            AssetDatabase.ImportAsset("Assets/Models/Equipment/bp_cuff.equipment.json", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset("Assets/Models/Equipment/bp_cuff.fbx", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            foreach (string id in EquipmentPrefabBuilder.Pilots.Concat(EquipmentPrefabBuilder.Sensors).Concat(EquipmentPrefabBuilder.Documents))
            {
                string path = "Assets/Prefabs/Equipment/" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var body = root.GetComponent<Rigidbody>();
                    body.isKinematic = false;
                    body.useGravity = true;
                    // XRI temporarily makes held/socketed bodies kinematic; this mode supports both states.
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.linearDamping = .2f;
                    body.angularDamping = .5f;
                    if (id == "bp_cuff")
                    {
                        var collider = root.GetComponent<BoxCollider>();
                        collider.size = new Vector3(.28f, .08f, .16f);
                        collider.center = new Vector3(0, .04f, 0);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            // Check model sub-object identity survived import before saving any scene state.
            var cuff = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ProcSense.VR.ActionEmitters.SensorAttachment>(true))
                .Single(s => s.name == "bp_cuff");
            var references = new SerializedObject(cuff).FindProperty("looseVisuals");
            for (int i = 0; i < references.arraySize; i++)
                if (references.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException("Cuff model import changed an existing visual reference");
            AssetDatabase.SaveAssets();
        }
    }
}
