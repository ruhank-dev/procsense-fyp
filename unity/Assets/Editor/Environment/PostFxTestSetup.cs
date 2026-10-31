using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Test-only copy of the Quest URP asset with HDR and the post-processing pipeline switched ON, plus global Volume prefabs, so the effect of tone mapping
    /// and grading can be measured in a real play session without touching URP_Quest. Why this is needed: URP_Quest_Renderer has
    /// <c>postProcessData: {fileID: 0}</c>, which disables post-processing entirely (a deliberate Quest default), so any Volume in a scene is ignored.
    /// </summary>
    public static class PostFxTestSetup
    {
        const string Dir = "Assets/Settings/PostFxTest";

        static VolumeProfile Profile(string name, Action<VolumeProfile> fill)
        {
            var path = $"{Dir}/Profile_{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>(); p.name = "Profile_" + name;
            AssetDatabase.CreateAsset(p, path);
            fill(p);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
            var go = new GameObject("Vol_" + name); var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10; vol.sharedProfile = p;
            PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/Vol_{name}.prefab"); UnityEngine.Object.DestroyImmediate(go);
            return p;
        }

        public static void Create()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", Dir)));
            var rPath = $"{Dir}/URP_PostFxTest_Renderer.asset"; var aPath = $"{Dir}/URP_PostFxTest.asset";
            AssetDatabase.DeleteAsset(rPath); AssetDatabase.DeleteAsset(aPath);
            if (!AssetDatabase.CopyAsset("Assets/Settings/URP_Quest_Renderer.asset", rPath)) throw new InvalidOperationException("renderer copy failed");
            if (!AssetDatabase.CopyAsset("Assets/Settings/URP_Quest.asset", aPath)) throw new InvalidOperationException("URP copy failed");
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rPath);
            rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (rd.postProcessData == null) throw new InvalidOperationException("default PostProcessData not found");
            EditorUtility.SetDirty(rd);
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(aPath);
            var so = new SerializedObject(urp);
            so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = rd;
            so.FindProperty("m_SupportsHDR").boolValue = true;
            so.FindProperty("m_ColorGradingMode").intValue = 1;             // high dynamic range grading
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();

            Profile("proof", p =>                                            // deliberately obvious: grey-scale, darker, harder contrast
            {
                var ca = p.Add<ColorAdjustments>(true); ca.saturation.Override(-100f); ca.postExposure.Override(-.8f); ca.contrast.Override(25f);
            });
            Profile("neutral", p => { var t = p.Add<Tonemapping>(true); t.mode.Override(TonemappingMode.Neutral); });
            Profile("aces", p => { var t = p.Add<Tonemapping>(true); t.mode.Override(TonemappingMode.ACES); });
            Profile("candidate", p =>
            {
                var t = p.Add<Tonemapping>(true); t.mode.Override(TonemappingMode.Neutral);
                var ca = p.Add<ColorAdjustments>(true); ca.contrast.Override(8f); ca.saturation.Override(-2f);
            });
            // Neutral removes clipped highlights but also lowers mean luminance (~.05) and saturation (~30%); this restores both
            Profile("candidate2", p =>
            {
                var t = p.Add<Tonemapping>(true); t.mode.Override(TonemappingMode.Neutral);
                var ca = p.Add<ColorAdjustments>(true); ca.postExposure.Override(.28f); ca.saturation.Override(14f);
            });
            Debug.Log("[PostFx] test pipeline and volumes created: HDR=" + urp.supportsHDR + " postProcessData=" + rd.postProcessData.name);
        }
    }
}
