using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Fast look iteration without recompiling or rebaking: applies material values from Logs/LookUpgrade/tune.json and dumps the
    /// PatientBay's renderer/material facts. Assets only; never saves a scene.
    /// tune.json: [{"material":"Name", "floats":{"_Smoothness":.5}, "colors":{"_BaseColor":[r,g,b,a]}, "scales":{"_BaseMap":[x,y]},
    ///              "enable":["_EMISSION"], "disable":[]}]
    /// Batch: -executeMethod ProcSense.EditorTools.PatientBayTune.Apply -quit   |   ...PatientBayTune.Dump -quit
    /// </summary>
    public static class PatientBayTune
    {
        static string Logs => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));

        [Serializable] class Entry
        {
            public string material;
            public List<NamedFloat> floats = new(); public List<NamedColor> colors = new(); public List<NamedVec> scales = new(); public List<NamedText> textures = new();
            public string[] enable = new string[0], disable = new string[0]; public int giFlags = -1;
        }
        [Serializable] class NamedFloat { public string name; public float value; }
        [Serializable] class NamedColor { public string name; public float[] value; }
        [Serializable] class NamedText { public string name; public string path; }
        [Serializable] class NamedVec { public string name; public float[] value; }
        [Serializable] class Root { public List<Entry> entries = new(); public float reflectionIntensity = -1f; public float probeIntensity = -1f; public float probeSizeY = -1f; }

        public static void Apply()
        {
            var file = Path.Combine(Logs, "LookUpgrade", "tune.json");
            var root = JsonUtility.FromJson<Root>(File.ReadAllText(file));
            foreach (var e in root.entries)
            {
                var guids = AssetDatabase.FindAssets(e.material + " t:Material");
                var path = guids.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == e.material);
                if (path == null) { Debug.Log("[Tune] material not found: " + e.material); continue; }
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                foreach (var f in e.floats) m.SetFloat(f.name, f.value);
                foreach (var c in e.colors) m.SetColor(c.name, new Color(c.value[0], c.value[1], c.value[2], c.value.Length > 3 ? c.value[3] : 1f));
                foreach (var s in e.scales) m.SetTextureScale(s.name, new Vector2(s.value[0], s.value[1]));
                foreach (var t in e.textures) m.SetTexture(t.name, string.IsNullOrEmpty(t.path) ? null : AssetDatabase.LoadAssetAtPath<Texture>(t.path));
                foreach (var k in e.enable) m.EnableKeyword(k);
                if (e.giFlags >= 0) m.globalIlluminationFlags = (MaterialGlobalIlluminationFlags)e.giFlags;
                foreach (var k in e.disable) m.DisableKeyword(k);
                EditorUtility.SetDirty(m);
                Debug.Log($"[Tune] {path}");
            }
            AssetDatabase.SaveAssets();
            if (root.reflectionIntensity >= 0f || root.probeIntensity >= 0f || root.probeSizeY > 0f)
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
                if (root.reflectionIntensity >= 0f) RenderSettings.reflectionIntensity = root.reflectionIntensity;
                if (root.probeSizeY > 0f)
                    foreach (var p in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true))) p.size = new Vector3(p.size.x, root.probeSizeY, p.size.z);
                if (root.probeIntensity >= 0f)
                    foreach (var p in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true))) p.intensity = root.probeIntensity;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Tune] scene reflections: global {RenderSettings.reflectionIntensity} probe {root.probeIntensity}");
            }
        }

        public static void Dump()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();
            foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                var ms = r.sharedMaterials;
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer s) mesh = s.sharedMesh; else if (r.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                sb.AppendLine($"{r.name} | mesh={(mesh ? mesh.name : "-")} | mats=" + string.Join("; ", ms.Select(m => m ? $"{m.name} [{AssetDatabase.GetAssetPath(m)}] sm={(m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : -1):F2} met={(m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1):F2}" : "NULL")));
            }
            foreach (var pr in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)))
                sb.AppendLine($"PROBE {pr.name} enabled={pr.enabled} mode={pr.mode} importance={pr.importance} center={pr.transform.position:F2} size={pr.size:F2} bounds={pr.bounds} tex={(pr.bakedTexture ? pr.bakedTexture.name + " " + pr.bakedTexture.dimension : "none")} intensity={pr.intensity} box={pr.boxProjection}");
            foreach (var fr in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name == "Floor"))
                sb.AppendLine($"FLOOR usage={fr.reflectionProbeUsage} anchor={(fr.probeAnchor ? fr.probeAnchor.name : "none")} bounds={fr.bounds} static={GameObjectUtility.GetStaticEditorFlags(fr.gameObject)} lm={fr.lightmapIndex} mat={fr.sharedMaterial.name} shader={fr.sharedMaterial.shader.name}");
            sb.AppendLine($"RenderSettings defaultReflectionMode={RenderSettings.defaultReflectionMode} intensity={RenderSettings.reflectionIntensity} custom={(RenderSettings.customReflectionTexture ? RenderSettings.customReflectionTexture.name : "none")}");
            File.WriteAllText(Path.Combine(Logs, "LookReview", "materials-dump.txt"), sb.ToString());
            Debug.Log("[Tune] dumped " + sb.ToString().Split('\n').Length + " renderers");
        }
    }
}
