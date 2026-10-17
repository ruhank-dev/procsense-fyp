using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class SensorEquipmentTable
    {
        private const string Id = "sensor_equipment_table";
        private const string Model = "Assets/Models/Environment/Bedside/" + Id + ".fbx";
        private const string Prefab = "Assets/Prefabs/Environment/Bedside/" + Id + ".prefab";

        public static object Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve unsaved scenes first");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null) throw new InvalidOperationException("Preserve the installed table and manual edits");
            string source = "Logs/SensorTableBuild/" + Id + ".fbx";
            if (!File.Exists(source)) throw new FileNotFoundException("Generate the table with pinned Blender first", source);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var monitoring = scene.GetRootGameObjects().Single(g => g.name == "PatientBay").transform.Find("Physical bedside monitoring");
            var oldPad = monitoring.Find("Sensor tray");
            if (oldPad == null || monitoring.Find(Id) != null) throw new InvalidOperationException("Unexpected sensor tray setup");
            var anchorPositions = monitoring.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith(" tray pose", StringComparison.Ordinal)).ToDictionary(t => t, t => new Pose(t.position, t.rotation));
            File.Copy(source, Model, true);
            AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false;
            importer.importLights = false; importer.importCameras = false; importer.isReadable = false;
            importer.generateSecondaryUV = true; importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            var map = new Dictionary<string, Material> {
                ["M_Ivory"] = Existing("Bedside/Clean ivory housing"), ["M_Sage"] = Existing("Doors/Sage clinical door"),
                ["M_SteelBrushed"] = Existing("Doors/Brushed stainless hardware"), ["M_RubberBlack"] = Existing("Bedside/Castor rubber")
            };
            var wrapper = new GameObject(Id);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
                model.transform.SetParent(wrapper.transform, false);
                if (model.transform.InverseTransformPoint(model.GetComponentsInChildren<Transform>().Single(t => t.name == "FRONT_MARKER").position).z > 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = map.Single(p => renderer.name.EndsWith(p.Key, StringComparison.Ordinal)).Value;
                    renderer.receiveGI = ReceiveGI.Lightmaps;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                }
                Solid(wrapper.transform, "Worktop collision", new Vector3(0, .8225f, 0), new Vector3(.48f, .04f, .43f));
                Solid(wrapper.transform, "Shelf collision", new Vector3(0, .195f, 0), new Vector3(.40f, .022f, .35f));
                foreach (float x in new[] { -.185f, .185f }) foreach (float z in new[] { -.16f, .16f })
                {
                    Solid(wrapper.transform, "Leg collision", new Vector3(x, .445f, z), new Vector3(.026f, .68f, .026f));
                    Solid(wrapper.transform, "Castor collision", new Vector3(x, .045f, z), new Vector3(.055f, .09f, .075f));
                }
                Solid(wrapper.transform, "Back lip collision", new Vector3(0, .85f, .208f), new Vector3(.47f, .025f, .012f));
                foreach (float x in new[] { -.233f, .233f }) Solid(wrapper.transform, "Side lip collision", new Vector3(x, .85f, .005f), new Vector3(.012f, .018f, .40f));
                PrefabUtility.SaveAsPrefabAsset(wrapper, Prefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            instance.transform.SetParent(monitoring, false); instance.transform.localPosition = new Vector3(.78f, 0, 1.13f);
            oldPad.GetComponent<Renderer>().enabled = false; oldPad.GetComponent<Collider>().enabled = false;
            foreach (var pair in anchorPositions)
                if (pair.Key.position != pair.Value.position || pair.Key.rotation != pair.Value.rotation) throw new InvalidOperationException("A sensor return anchor moved");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return new { scene = scene.name, tableTop = .8425f, preservedSensorReturnAnchors = anchorPositions.Count, readyToBake = true };
        }

        private static Material Existing(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/" + name + ".mat") ?? throw new InvalidOperationException("Missing material " + name);
        private static void Solid(Transform root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider)); go.transform.SetParent(root, false);
            var collider = go.GetComponent<BoxCollider>(); collider.center = center; collider.size = size;
        }
    }
}
