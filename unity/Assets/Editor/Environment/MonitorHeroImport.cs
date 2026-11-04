using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Imports the hero patient monitor: texture set, PBR materials (named like the FBX materials so the equipment post-processor maps them),
    /// the FBX itself, the live-surface restyle on the prefab (clear canvas over the LCD, readouts aligned to the printed panels) and the
    /// invisible NIBP button placed on the real key. Idempotent.
    /// </summary>
    public static class MonitorHeroImport
    {
        const string TexDir = "Assets/Textures/Hospital/Monitor";
        const string MatDir = "Assets/Materials/Equipment";
        static string Build => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MonitorBuild"));

        static Texture2D Tex(string file, bool normal = false, bool linear = false)
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", TexDir)));
            var dst = TexDir + "/" + file;
            File.Copy(Path.Combine(Build, file), Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !(normal || linear);
            ti.anisoLevel = 8; ti.mipmapEnabled = true; ti.maxTextureSize = 2048;
            ti.alphaIsTransparency = file.Contains("labels");
            ti.textureCompression = TextureImporterCompression.Uncompressed;          // quality first; compression comes with the optimisation pass
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        static Material Mat(string name, Action<Material> set)
        {
            var path = MatDir + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + name }; AssetDatabase.CreateAsset(m, path); }
            m.enableInstancing = true;
            set(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Pbr(Material m, Color c, float smooth, float metal = 0f) { m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); }

        static void Emit(Material m, Color c, Texture tex = null)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c);
            if (tex != null) m.SetTexture("_EmissionMap", tex);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        public static void Materials()
        {
            var grain = Tex("plastic_orange_peel_n.png", normal: true);
            var labels = Tex("monitor_labels.png");
            var ui = Tex("monitor_ui.png");
            Tex("glass_smudge.png", linear: true); Tex("plastic_rough.png", linear: true);
            var glassGloss = Tex("glass_gloss.png", linear: true); var glassN = Tex("glass_normal.png", normal: true);
            var aoBody = Tex("ao_Body.png", linear: true); var aoParts = Tex("ao_MonShell2.png", linear: true);
            var aoBezel = Tex("ao_MonBezel.png", linear: true); var aoKey = Tex("ao_MonKey.png", linear: true);
            void Occ(Material m, Texture2D t, float strength) { m.SetTexture("_OcclusionMap", t); m.SetFloat("_OcclusionStrength", strength); m.EnableKeyword("_OCCLUSIONMAP"); }

            void Detail(Material m, float scale)
            {
                m.SetTexture("_DetailNormalMap", grain); m.SetFloat("_DetailNormalMapScale", scale);
                m.SetTextureScale("_DetailNormalMap", Vector2.one); m.EnableKeyword("_DETAIL_MULX2");
            }
            Mat("MonShell", m => { Pbr(m, new Color(.70f, .715f, .71f), .54f); Detail(m, .12f); m.SetTextureScale("_DetailNormalMap", Vector2.one * 60f); Occ(m, aoBody, .9f); });
            Mat("MonShell2", m => { Pbr(m, new Color(.70f, .715f, .71f), .54f); Detail(m, .12f); m.SetTextureScale("_DetailNormalMap", Vector2.one * 60f); Occ(m, aoParts, .9f); });
            Mat("MonBezel", m => { Pbr(m, new Color(.014f, .015f, .017f), .80f); Detail(m, .12f); m.SetTextureScale("_DetailNormalMap", Vector2.one * 30f); Occ(m, aoBezel, .8f); });
            Mat("MonKey", m => { Pbr(m, new Color(.05f, .055f, .06f), .38f); Detail(m, .3f); m.SetTextureScale("_DetailNormalMap", Vector2.one * 30f); Occ(m, aoKey, .85f); });
            Mat("MonSocket", m => Pbr(m, new Color(.005f, .005f, .006f), .35f));
            Mat("MonLabels", m =>
            {
                Pbr(m, Color.white, .38f); m.SetTexture("_BaseMap", labels);
                m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450;
            });
            Mat("MonLCD", m =>
            {
                Pbr(m, new Color(.03f, .03f, .03f), .22f); m.SetTexture("_BaseMap", ui); Emit(m, new Color(.95f, .95f, .95f), ui);          // display image is emission; the unlit panel does not lift the blacks
            });
            Mat("MonGlass", m =>
            {                                                                     // premultiplied blend keeps the specular reflection of a clear pane
                Pbr(m, new Color(.02f, .022f, .026f, .05f), .97f);
                // specular workflow: an anti-reflective coating reflects ~2% (bare glass 4%); smoothness varies with the smudge map
                m.SetFloat("_WorkflowMode", 0); m.EnableKeyword("_SPECULAR_SETUP"); m.SetColor("_SpecColor", new Color(.022f, .022f, .024f));
                m.SetTexture("_SpecGlossMap", glassGloss); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_SmoothnessTextureChannel", 0); m.SetFloat("_Smoothness", 1f);
                m.SetTexture("_BumpMap", glassN); m.SetFloat("_BumpScale", .25f); m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 1); m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.renderQueue = 3000;
                m.SetOverrideTag("RenderType", "Transparent");
            });
            Mat("LedGreen", m => { Pbr(m, new Color(.04f, .55f, .16f), .8f); Emit(m, new Color(.1f, 1.6f, .4f)); });
            Mat("LedAmber", m => { Pbr(m, new Color(.65f, .38f, .03f), .8f); Emit(m, new Color(1.6f, .8f, .05f)); });
            Mat("LedRed", m => { Pbr(m, new Color(.5f, .04f, .03f), .8f); Emit(m, new Color(.45f, .02f, .01f)); });
            Mat("MonChrome", m => Pbr(m, new Color(.85f, .86f, .88f), .94f, 1f));
            Mat("MonPlastic", m => { Pbr(m, new Color(.26f, .27f, .285f), .5f); Detail(m, .12f); });
            Mat("MonSteel", m => Pbr(m, new Color(.55f, .56f, .58f), .68f, 1f));
            Mat("GasGreen", m => Pbr(m, new Color(.05f, .45f, .13f), .55f));
            Mat("GasYellow", m => Pbr(m, new Color(.82f, .64f, .06f), .55f));
            Mat("GasBlue", m => Pbr(m, new Color(.05f, .20f, .62f), .55f));
            Mat("GasWhite", m => Pbr(m, new Color(.86f, .87f, .85f), .5f));
            AssetDatabase.SaveAssets();
        }

        /// <summary>Materials, then the FBX from build/blender, then the prefab and (if open) scene fix-ups.</summary>
        public static void Run()
        {
            Materials();
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "blender", "vital_signs_monitor.fbx"));
            var dst = "Assets/Models/Equipment/vital_signs_monitor.fbx";
            File.Copy(src, Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
            File.Copy(Path.Combine(Path.GetDirectoryName(src), "vital_signs_monitor.equipment.json"),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Assets/Models/Equipment/vital_signs_monitor.equipment.json")), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            FixPrefab();
        }

        /// <summary>Live surface: transparent canvas over the LCD, the four readouts laid on the printed panels.</summary>
        public static void FixPrefab()
        {
            const string path = "Assets/Prefabs/Equipment/vital_signs_monitor.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Live surface"))
                {
                    var bg = t.GetComponentInChildren<Image>(true);
                    if (bg != null) bg.color = new Color(0, 0, 0, 0);
                    var label = t.GetComponentInChildren<TMP_Text>(true);
                    if (label == null) continue;
                    var rt = label.rectTransform;
                    // canvas is 522 x 326 units = the 1044 x 652 px backdrop at half size; right panel column x 664..1032 px, rows every 128 px from y 64
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(364, -58); rt.sizeDelta = new Vector2(156, 260);
                    label.fontSize = 22; label.alignment = TextAlignmentOptions.TopLeft; label.enableWordWrapping = false;
                    label.lineSpacing = 173; label.color = new Color(.78f, 1f, .86f); label.fontStyle = FontStyles.Bold; label.margin = Vector4.zero;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("[MonitorHero] prefab restyled");
        }

        /// <summary>Scene part: the NIBP trigger sits on the printed key; its placeholder cube and caption are hidden (collider and interaction stay).</summary>
        public static void FixScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var button = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Start BP cycle");
            if (button == null) { Debug.Log("[MonitorHero] no Start BP cycle"); return; }
            var monitor = button.parent;
            var ecg = monitor.GetComponentsInChildren<Transform>(true).First(t => t.name == "ATTACH_ECG");
            var le = monitor.InverseTransformPoint(ecg.position);
            float sx = le.x < 0 ? 1f : -1f;                                   // Blender -X side maps to the same side of the anchor
            Debug.Log($"[MonitorHero] ATTACH_ECG local {le:F4} -> key x sign {sx}");
            button.localPosition = new Vector3(sx * .0555f, 1.176f, .0985f);
            button.localScale = new Vector3(.072f, .026f, .008f);
            var r = button.GetComponent<MeshRenderer>(); if (r != null) r.enabled = false;
            foreach (var c in button.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
            var caption = monitor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BP control label");
            if (caption != null) caption.gameObject.SetActive(false);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[MonitorHero] key trigger placed at " + button.localPosition);
        }
    }
}
