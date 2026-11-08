using System;
using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Builds the Quest variant of the exterior city beside the original (P3-EXT-04). Imports the reduced traffic and tree meshes produced by
    /// `CITY_VARIANT=quest blender -b -P unity/Tools/Environment/build_city.py` (same seed and placement as the original city), instantiates them under "City exterior/Quest variant" with exactly the
    /// materials, shadow and probe settings of the originals they replace, switched off, and wires a QuestExterior component that shows one variant. The original city objects are not modified.
    /// Re-runnable: the previous "Quest variant" is replaced. Undo: git checkout Corridor.unity (the imported meshes can stay or be deleted).
    /// Batch: -executeMethod ProcSense.EditorTools.QuestExteriorBuild.Run
    /// </summary>
    public static class QuestExteriorBuild
    {
        private const string Models = "Assets/Models/Environment/City";
        // fbx, name of the new layer, name of the original layer it replaces, and the material key of the only original renderer replaced (null = the whole layer)
        private static readonly (string fbx, string layer, string replaces, string key)[] Parts =
        {
            ("city_traffic_q", "Rush-hour traffic and street furniture (Quest)", "Rush-hour traffic and street furniture", null),
            ("city_trees_q", "Street and podium trees (Quest)", "Street and podium trees", null),
            ("city_ground_q", "Ground, roads, podium terrace (Quest)", "Ground, roads, podium terrace", "Palette"),
            ("city_mid_q", "Layer B mid-distance towers (Quest)", "Layer B mid-distance towers", "Concrete"),
        };

        public static void Run()
        {
            bool ok = false;
            try { Build(); ok = true; } catch (Exception e) { Debug.LogError("[QuestExteriorBuild] " + e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void Build()
        {
            foreach (var p in Parts)
            {
                var dst = Models + "/" + p.fbx + ".fbx";
                File.Copy(Path.GetFullPath("Logs/CityBuild/quest/" + p.fbx + ".fbx"), Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(dst);
                mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.isReadable = false; mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.generateSecondaryUV = false; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.indexFormat = ModelImporterIndexFormat.UInt32; mi.SaveAndReimport();
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().First(g => g.name == "City exterior").transform;
            var oldVariant = root.Find("Quest variant"); if (oldVariant != null) UnityEngine.Object.DestroyImmediate(oldVariant.gameObject);
            var holder = new GameObject("Quest variant").transform; holder.SetParent(root, false);
            var originals = new System.Collections.Generic.List<Renderer>(); var optimised = new System.Collections.Generic.List<Renderer>();
            foreach (var p in Parts)
            {
                var src = root.Find(p.replaces); if (src == null) throw new Exception("original layer missing: " + p.replaces);
                var srcRenderers = src.GetComponentsInChildren<MeshRenderer>(true).Where(x => p.key == null || x.name.EndsWith("_M_" + p.key, StringComparison.Ordinal)).ToArray();
                if (srcRenderers.Length == 0) throw new Exception("no original renderer for " + p.replaces + " " + p.key);
                originals.AddRange(srcRenderers);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + p.fbx + ".fbx"));
                go.transform.SetParent(holder, false); go.name = p.layer; go.transform.position = src.position; go.transform.rotation = src.rotation; go.transform.localScale = src.localScale;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    int i = r.name.LastIndexOf("_M_", StringComparison.Ordinal); var key = i >= 0 ? r.name.Substring(i + 3) : "";
                    var match = srcRenderers.FirstOrDefault(s => { int j = s.name.LastIndexOf("_M_", StringComparison.Ordinal); return j >= 0 && s.name.Substring(j + 3) == key; }) ?? (srcRenderers.Length == 1 ? srcRenderers[0] : null);
                    if (match == null) throw new Exception("no original to copy settings from for " + r.name);
                    r.sharedMaterials = match.sharedMaterials; r.shadowCastingMode = match.shadowCastingMode; r.receiveShadows = match.receiveShadows; r.lightProbeUsage = match.lightProbeUsage;
                    r.reflectionProbeUsage = match.reflectionProbeUsage; r.renderingLayerMask = match.renderingLayerMask; r.motionVectorGenerationMode = match.motionVectorGenerationMode;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(match.gameObject) & ~(StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic));
                    r.enabled = false; optimised.Add(r);
                }
            }
            var q = root.GetComponent<QuestExterior>() ?? root.gameObject.AddComponent<QuestExterior>();
            q.Configure(originals.ToArray(), optimised.ToArray());
            EditorUtility.SetDirty(q);
            Debug.Log($"[QuestExteriorBuild] originals {originals.Count}, optimised {optimised.Count}, triangles original {originals.Sum(Tris)} optimised {optimised.Sum(Tris)}");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        private static int Tris(Renderer r) { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return 0; int t = 0; for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) t += (int)(mf.sharedMesh.GetIndexCount(s) / 3); return t; }
    }
}
