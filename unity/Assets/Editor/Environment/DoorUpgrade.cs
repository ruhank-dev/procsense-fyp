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
    /// <summary>
    /// Replaces the stacked-box hospital doors with the Blender-modelled doors (Tools/Environment/build_doors.py): a pressed-steel frame with stop, seal, architraves and threshold, and one of four leaf types with
    /// bevelled slab, cut-out vision panel, kick plates, bar pull/push plate or levers, and knuckle hinges. The old parts stay in place with their renderers off, because the transport and door-sequence setups look
    /// them up by name and the leaf's collider lives on "Door leaf"; the new model is added next to them, so every interaction, collider and scene transition is unchanged.
    /// </summary>
    public static class DoorUpgrade
    {
        const string Models = "Assets/Models/Environment/Doors", Mats = "Assets/Materials/Hospital/Doors2";
        static readonly string[] OldParts = { "Door leaf", "Kick plate", "Observation surround", "Frosted observation inset", "Handle mounting", "Pull handle", "Hinge", "Frame left", "Frame right", "Frame lintel" };

        static void Dir(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Dir(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }

        public static void Import()
        {
            Dir(Models); Dir(Mats);
            foreach (var n in new[] { "door_frame", "door_leaf_bay", "door_leaf_diag", "door_leaf_lab", "door_leaf_ot" })
            {
                var dst = Models + "/" + n + ".fbx"; File.Copy(Path.GetFullPath("Logs/Doors/" + n + ".fbx"), Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(dst); mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.generateSecondaryUV = true; mi.SaveAndReimport();
            }
            Debug.Log("[DoorUpgrade] imported");
        }

        static Material Lit(string name, string hex, float smooth, float metal, float alpha = 1f)
        {
            var path = Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            ColorUtility.TryParseHtmlString("#" + hex, out var c); c.a = alpha;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); m.enableInstancing = true;
            if (alpha < 1f)
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m); return m;
        }

        static Dictionary<string, Material> Materials() => new Dictionary<string, Material>
        {
            ["DoorBay"] = Lit("Door veneer", "94704F", .42f, 0f), ["DoorDiag"] = Lit("Door diagnostic grey blue", "4D6178", .38f, 0f), ["DoorLab"] = Lit("Door laboratory white", "E6E8E6", .32f, 0f), ["DoorOT"] = Lit("Door theatre steel", "B8C2C6", .30f, .55f),
            ["FrameSteel"] = Lit("Door frame steel", "CCD1D1", .38f, .1f), ["TrimWhite"] = Lit("Door architrave", "EAEAE6", .5f, 0f), ["DoorGlass"] = Lit("Door vision glass", "B3D9E6", .55f, 0f, .42f),
            ["Seal"] = Lit("Door seal", "050505", .85f, 0f), ["SteelBrushed"] = Lit("Door steel", "C7CBCF", .55f, .85f),
        };

        static Transform Ancestor(Transform t, Func<string, bool> pred) { for (var p = t.parent; p != null; p = p.parent) if (pred(p.name)) return p; return null; }

        static string Variant(string door, string scene)
        {
            var n = door.ToUpperInvariant();
            if (n.Contains("OPERATING") || (scene == "OT" && n.Contains("CORRIDOR"))) return "ot";
            if (n.Contains("LABORATORY")) return "lab";
            if (n.Contains("PATIENT BAY") || (scene == "PatientBay" && n.Contains("CORRIDOR"))) return "bay";
            return "diag";
        }

        static GameObject Add(string asset, Transform parent, string name, Dictionary<string, Material> mats, bool isStatic)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + asset + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(parent, false); go.name = name; go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var key = r.name.Substring(r.name.LastIndexOf("M_", StringComparison.Ordinal) + 2);
                r.sharedMaterial = mats.TryGetValue(key, out var m) ? m : mats["FrameSteel"];
                r.shadowCastingMode = key == "DoorGlass" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                if (isStatic) { r.receiveGI = ReceiveGI.Lightmaps; GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI); }
                else { r.receiveGI = ReceiveGI.LightProbes; r.lightProbeUsage = LightProbeUsage.BlendProbes; }
            }
            return go;
        }

        public static void Apply()
        {
            var mats = Materials(); int doors = 0;
            foreach (var sceneName in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                foreach (var old in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Door leaf (modelled)" || t.name == "Door frame (modelled)").ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var leaves = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Door leaf").ToArray();
                foreach (var leaf in leaves)
                {
                    var arch = Ancestor(leaf, n => n.EndsWith(" architecture")); if (arch == null) continue;
                    var variant = Variant(arch.name.Replace(" architecture", ""), sceneName);
                    foreach (var part in arch.GetComponentsInChildren<Transform>(true).Where(t => OldParts.Contains(t.name)))
                        foreach (var r in part.GetComponents<MeshRenderer>()) r.enabled = false;
                    foreach (var plate in arch.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Door identity plate"))                    // the identity plate sits below the vision panel, not across it
                    { var lp = plate.localPosition; lp.y = variant == "ot" || variant == "lab" ? .90f : 1.12f; plate.localPosition = lp; }
                    Add("door_frame", arch, "Door frame (modelled)", mats, true);
                    Add("door_leaf_" + variant, leaf.parent, "Door leaf (modelled)", mats, false);
                    doors++;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[DoorUpgrade] doors upgraded: " + doors);
        }
    }
}
