using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class HospitalBedside
    {
        private const string RootName = "Hospital bedside furnishings";
        private const string Models = "Assets/Models/Environment/Bedside";
        private const string Prefabs = "Assets/Prefabs/Environment/Bedside";
        private const string Materials = "Assets/Materials/Hospital/Bedside";

        public static object ImportAndApply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play/baking first");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Preserve existing unsaved scenes first");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g=>g.name==RootName)) throw new InvalidOperationException("Already dressed; preserve authored furnishings");
            string source = Path.GetFullPath("Logs/BedsideBuild");
            var ids = new[] { "bay_headwall", "bedside_cabinet", "tablet_table" };
            foreach (string id in ids) if (!File.Exists(source + "/" + id + ".fbx")) throw new FileNotFoundException("Build the Blender props first", id);
            Folder(Models); Folder(Prefabs); Folder(Materials);
            var materials = new Dictionary<string, Material> {
                ["M_Ivory"] = MakeMaterial("Clean ivory housing", "DAE2DA", .3f),
                ["M_Sage"] = Existing("Doors/Sage clinical door"),
                ["M_Oak"] = Existing("Pale oak laminate"),
                ["M_PlasticWhite"] = Existing("Satin ivory trim"),
                ["M_PlasticGrey"] = MakeMaterial("Recesses and tablet bezel", "202B2C", .18f),
                ["M_SteelBrushed"] = Existing("Doors/Brushed stainless hardware"),
                ["M_RubberBlack"] = MakeMaterial("Castor rubber", "202423", .04f),
                ["M_Diffuser"] = Existing("Diffused ceiling light")
            };
            foreach (string id in ids)
            {
                string modelPath = Models + "/" + id + ".fbx";
                File.Copy(source + "/" + id + ".fbx", modelPath, true);
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false;
                importer.importLights = false; importer.importCameras = false; importer.isReadable = false;
                importer.generateSecondaryUV = true; importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                string prefabPath = Prefabs + "/" + id + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) throw new InvalidOperationException("Preserve existing prefab " + prefabPath);
                var wrapper = new GameObject(id);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                    model.transform.SetParent(wrapper.transform, false);
                    var face = model.GetComponentsInChildren<Transform>().Single(t=>t.name=="FRONT_MARKER");
                    if (model.transform.InverseTransformPoint(face.position).z > 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        string key = materials.Keys.Single(k=>renderer.name.EndsWith(k, StringComparison.Ordinal));
                        renderer.sharedMaterial = materials[key]; renderer.receiveGI = ReceiveGI.Lightmaps;
                        GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                    }
                    PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(wrapper); }
            }
            var bay = scene.GetRootGameObjects().Single(g=>g.name=="PatientBay");
            var root = new GameObject(RootName).transform;
            var headwall = Spawn(ids[0], root, new Vector3(0, 1.62f, 1.82f));
            Spawn(ids[1], root, new Vector3(-1.2f, 0, .4f));
            var table = Spawn(ids[2], root, new Vector3(-.6f, 0, -.4f));
            // Retain the cabinet's existing solid footprint and all live folder/sensor transforms.
            bay.transform.Find("Notes stand").GetComponent<Renderer>().enabled = false;
            var oldStand = bay.transform.Find("Tablet stand");
            oldStand.GetComponent<Renderer>().enabled = false; oldStand.GetComponent<Collider>().enabled = false;
            Collider(table, "Table top collision", new Vector3(0, 1.025f, .30f), new Vector3(.62f, .035f, .40f));
            Collider(table, "Table base collision", new Vector3(0, .07f, 0), new Vector3(.54f, .14f, .43f));
            Collider(table, "Table column collision", new Vector3(0, .52f, .10f), new Vector3(.07f, .91f, .075f));
            var labelGo = new GameObject("Bed bay identifier", typeof(TextMeshPro)); labelGo.transform.SetParent(headwall, false);
            labelGo.transform.localPosition = new Vector3(0, .03f, -.115f);
            var label = labelGo.GetComponent<TextMeshPro>(); label.font = TMP_Settings.defaultFontAsset; label.fontSharedMaterial = label.font.material;
            label.text = "BED 01"; label.fontSize = .60f; label.color = new Color(.12f, .24f, .24f); label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(.30f, .14f); label.enableAutoSizing = false; label.raycastTarget = false;
            labelGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lightGo = new GameObject("Baked headwall reading light", typeof(Light)); lightGo.transform.SetParent(headwall, false);
            lightGo.transform.localPosition = new Vector3(0, .315f, -.06f); lightGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var light = lightGo.GetComponent<Light>(); light.type = LightType.Rectangle; light.lightmapBakeType = LightmapBakeType.Baked;
            light.areaSize = new Vector2(1.4f, .075f); light.intensity = 2; light.color = new Color(1, .95f, .86f);
            CorrectLayout(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return new { room = "PatientBay", props = ids.Length, readyToBake = true, oldNotesFootprintPreserved = true };
        }

        public static object ApplyReviewCorrections()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play/baking first");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Preserve unsaved scenes first");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            CorrectLayout(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return new { tabletUpright = true, headboardTrimGap = true, cabinetBesideBed = true, readyToBake = true };
        }

        private static void CorrectLayout(Scene scene)
        {
            var bay = scene.GetRootGameObjects().Single(g=>g.name=="PatientBay").transform;
            bay.Find("Tablet view").rotation = Quaternion.identity;
            var root = scene.GetRootGameObjects().Single(g=>g.name==RootName).transform;
            var cabinet = root.Find("bedside_cabinet");
            var target = new Vector3(-.85f, 0, 1.40f);
            var delta = target - cabinet.position;
            cabinet.position = target;
            // Move both the visible folder and its retained legacy footprint/presentation anchors.
            foreach (string name in new[] { "Notes stand", "Notes", "Notes view", "Physical notes folder" })
            {
                var anchor = bay.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
                anchor.position += delta;
            }
            var finishes = scene.GetRootGameObjects().Single(g=>g.name=="Hospital finishes").transform;
            if (finishes.Find("Panel cap rail headboard right") == null)
            {
                var rail = finishes.GetComponentsInChildren<Transform>().Single(t=>t.name=="Panel cap rail" && t.position.z > 1.8f);
                float width = rail.localScale.x;
                float gap = bay.Find("Bed head").GetComponent<Renderer>().bounds.size.x + .10f;
                float section = (width-gap)*.5f;
                float offset = (gap+section)*.5f;
                var right = UnityEngine.Object.Instantiate(rail.gameObject, finishes).transform;
                right.name = "Panel cap rail headboard right";
                rail.localScale = right.localScale = new Vector3(section, rail.localScale.y, rail.localScale.z);
                var center = rail.localPosition;
                rail.localPosition = center + Vector3.left*offset;
                right.localPosition = center + Vector3.right*offset;
            }
        }

        private static Transform Spawn(string id, Transform parent, Vector3 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab"));
            go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform;
        }
        private static void Collider(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider)); go.transform.SetParent(parent, false);
            var collider = go.GetComponent<BoxCollider>(); collider.center = center; collider.size = size;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static Material Existing(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/" + name + ".mat");
            if (material == null) throw new InvalidOperationException("Missing shared material " + name); return material;
        }
        private static Material MakeMaterial(string name, string hex, float smoothness)
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
