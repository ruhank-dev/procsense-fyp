using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Brings the Operating Theatre's ceiling and lighting up to PatientBay's approved standard (owner review 1 Nov 2026): the same acoustic ceiling tile, hairline grid, luminaire housing and prism diffuser, and the same
    /// baked panel strength (1.9 in PatientBay; 2.2 here, a theatre is lit a little brighter) in a neutral white instead of the earlier intensity 9 with a blue cast, with the realtime key and room light casting no shadows.
    /// </summary>
    public static class OtParity
    {
        static Material Any(string name)
        {
            foreach (var g in AssetDatabase.FindAssets("t:Material " + name))
            { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p); }
            return null;
        }

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Single);
            var tileSrc = Any("PatientBay ceiling tile"); var grid = Any("PatientBay ceiling grid"); var housing = Any("Light housing"); var diffuser = Any("PatientBay ceiling diffuser");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            int lights = 0;
            foreach (var l in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)))
            {
                if (l.name.StartsWith("Baked ceiling panel")) { l.intensity = 2.2f; l.color = new Color(1f, .99f, .97f); l.range = 10f; lights++; }
                else if (l.type == LightType.Directional && l.lightmapBakeType == LightmapBakeType.Realtime) { l.shadows = LightShadows.None; if (l.name == "Soft clinical key") { l.intensity = .2f; l.color = new Color(1f, .97f, .92f); } }
            }
            var slab = all.FirstOrDefault(t => t.name == "Ceiling" && t.parent != null && t.parent.name == "Hospital finishes");
            if (tileSrc != null && slab != null)
            {
                var path = "Assets/Materials/Hospital/PatientBayLook/OT ceiling tile.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(tileSrc) { name = "OT ceiling tile" }; AssetDatabase.CreateAsset(m, path); } else m.CopyPropertiesFromMaterial(tileSrc);
                var s = slab.lossyScale; foreach (var prop in new[] { "_BaseMap", "_BumpMap" }) m.SetTextureScale(prop, new Vector2(s.x / 2.4f, s.z / 2.4f));
                EditorUtility.SetDirty(m); slab.GetComponent<MeshRenderer>().sharedMaterial = m;
                slab.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var t in all)
            {
                var r = t.GetComponent<MeshRenderer>(); if (r == null) continue;
                if (t.name == "OT ceiling grid" && grid != null) r.sharedMaterial = grid;
                else if (t.name == "OT luminaire frames" && housing != null) r.sharedMaterial = housing;
                else if (t.name == "OT luminaire diffusers" && diffuser != null) r.sharedMaterial = diffuser;
            }
            PatientBayLayout.HideTeleportVisuals(scene);                                                                               // the blue floor disc of the old teleport pad
            var vinyl = Any("PatientBay grey vinyl"); var floor = all.FirstOrDefault(x => x.name == "Floor" && x.GetComponent<MeshRenderer>() != null);
            if (vinyl != null && floor != null)
            {
                var vp = "Assets/Materials/Hospital/PatientBayLook/OT vinyl.mat"; var vm = AssetDatabase.LoadAssetAtPath<Material>(vp);
                if (vm == null) { vm = new Material(vinyl) { name = "OT vinyl" }; AssetDatabase.CreateAsset(vm, vp); } else vm.CopyPropertiesFromMaterial(vinyl);
                var fs = floor.lossyScale; foreach (var prop in new[] { "_BaseMap", "_BumpMap" }) vm.SetTextureScale(prop, new Vector2(fs.x, fs.z));
                EditorUtility.SetDirty(vm); floor.GetComponent<MeshRenderer>().sharedMaterial = vm;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[OtParity] OT ceiling and lighting matched to PatientBay; panels set: " + lights);
        }
    }
}
