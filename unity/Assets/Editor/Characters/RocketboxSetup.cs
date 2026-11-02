using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (owner: stop generating characters; use professional ready-made, clothed, rigged people): imports the Microsoft Rocketbox avatars (MIT, fetched by Tools/Characters/fetch_rocketbox.py) as Humanoid
    /// models with URP Lit materials, imports the Quaternius Universal Animation Library (CC0) as Humanoid clips that retarget onto every Rocketbox avatar, and builds one prefab per avatar plus shared animator controllers
    /// (IK pass on for hand grips). Sources and licences: docs/research/asset_sources_and_licenses.md. Idempotent.
    /// </summary>
    public static class RocketboxSetup
    {
        public const string Root = "Assets/Characters/Rocketbox", Anim = "Assets/Characters/Animations", Prefabs = "Assets/Prefabs/Characters", Ctrl = "Assets/Animations/Characters";

        static void Dir(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Dir(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }

        static Texture2D Tex(string path, bool normal, bool srgb = true)
        {
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path); ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = srgb && !normal; ti.mipmapEnabled = true; ti.anisoLevel = 8;
            ti.maxTextureSize = 2048; ti.alphaIsTransparency = path.EndsWith("opacity_color.png");
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string dir, string name)
        {
            Dir(dir + "/Materials"); var path = dir + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            var color = Tex(dir + "/" + name + "_color.jpg", false) ?? Tex(dir + "/" + name + "_color.png", false); var normal = Tex(dir + "/" + name + "_normal.png", true);
            bool hair = name.EndsWith("_opacity"), head = name.EndsWith("_head"), steth = name.Contains("stetoskop"), prot = name.Contains("protection");
            m.SetTexture("_BaseMap", color); m.SetColor("_BaseColor", Color.white);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Metallic", steth ? .55f : 0f); m.SetFloat("_Smoothness", hair ? .42f : head ? .38f : steth ? .55f : prot ? .25f : .22f);
            m.SetFloat("_Cull", hair ? 0f : 2f);
            if (hair) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", .35f); m.EnableKeyword("_ALPHATEST_ON"); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest; }
            EditorUtility.SetDirty(m); return m;
        }

        /// <summary>Humanoid import of every avatar, URP materials mapped by slot name, one prefab each (no animation yet, see BuildPrefabs).</summary>
        public static void Import()
        {
            Dir(Prefabs); Dir(Anim); Dir(Ctrl);
            foreach (var folder in Directory.GetDirectories(Root))
            {
                var name = Path.GetFileName(folder); var dir = Root + "/" + name; var fbx = dir + "/" + name + ".fbx";
                AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx); if (mi == null) continue;
                mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mi.importBlendShapes = true; mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; mi.materialLocation = ModelImporterMaterialLocation.InPrefab; mi.isReadable = false; mi.generateSecondaryUV = false; mi.SaveAndReimport();
                Debug.Log($"[Rocketbox] {name}: avatar valid = {AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault()?.isValid}");
            }
            // humanoid clips
            var src = "../../build/ual/x/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx"; var dst = Anim + "/UAL1_Standard.fbx";
            File.Copy(Path.GetFullPath(src), Path.GetFullPath(dst), true); File.Copy(Path.GetFullPath("../../build/ual/x/Universal Animation Library[Standard]/License.txt"), Path.GetFullPath(Anim + "/LICENSE_Quaternius_UAL_CC0.txt"), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var um = (ModelImporter)AssetImporter.GetAtPath(dst); um.animationType = ModelImporterAnimationType.Human; um.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; um.importAnimation = true; um.SaveAndReimport();
            um = (ModelImporter)AssetImporter.GetAtPath(dst); var clips = um.defaultClipAnimations;
            foreach (var c in clips) { c.name = c.name.Replace("Armature|Armature|", "").Replace("Armature|", ""); bool loop = c.name.EndsWith("_Loop"); c.loopTime = loop; c.loopPose = loop; c.lockRootPositionXZ = true; c.lockRootRotation = true; c.keepOriginalPositionY = true; c.heightFromFeet = false; }
            um.clipAnimations = clips; um.SaveAndReimport();
            Debug.Log($"[Rocketbox] animations: UAL avatar valid = {AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Avatar>().FirstOrDefault()?.isValid}, clips = {AssetDatabase.LoadAllAssetsAtPath(dst).OfType<AnimationClip>().Count(a => !a.name.StartsWith("__"))}");
        }

        static AnimatorController Controller(string clipName)
        {
            var path = Ctrl + "/RB_" + clipName + ".controller"; var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); if (existing != null) return existing;
            var clip = AssetDatabase.LoadAllAssetsAtPath(Anim + "/UAL1_Standard.fbx").OfType<AnimationClip>().FirstOrDefault(a => a.name == clipName);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path); var sm = c.layers[0].stateMachine; var st = sm.AddState(clipName); st.motion = clip; sm.defaultState = st;
            var layers = c.layers; layers[0].iKPass = true; c.layers = layers; return c;
        }

        public static void BuildPrefabs()
        {
            Dir(Prefabs);
            foreach (var folder in Directory.GetDirectories(Root))
            {
                var name = Path.GetFileName(folder); var dir = Root + "/" + name; var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + name + ".fbx"); if (model == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model); go.name = "RB_" + name;
                var cache = new System.Collections.Generic.Dictionary<string, Material>();
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var slots = r.sharedMaterials; var mapped = new Material[slots.Length];
                    for (int i = 0; i < slots.Length; i++) { var n = slots[i] != null ? slots[i].name : ""; if (!cache.TryGetValue(n, out var m)) cache[n] = m = File.Exists(dir + "/" + n + "_color.jpg") || File.Exists(dir + "/" + n + "_color.png") ? Mat(dir, n) : slots[i]; mapped[i] = m; }
                    r.sharedMaterials = mapped; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.BlendProbes; r.updateWhenOffscreen = false;
                }
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>(); anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms; anim.runtimeAnimatorController = Controller("Idle_Loop");
                var view = go.AddComponent<ProcSense.VR.Views.StaffIdleView>(); view.FacialOnly = true; view.PersonRole = ProcSense.VR.Views.StaffIdleView.Role.CivilianWaiting;
                PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/RB_" + name + ".prefab"); Object.DestroyImmediate(go);
            }
            foreach (var c in new[] { "Idle_Loop", "Idle_Talking_Loop", "Sitting_Idle_Loop", "Sitting_Talking_Loop", "Walk_Loop", "Walk_Formal_Loop" }) Controller(c);
            AssetDatabase.SaveAssets(); Debug.Log("[Rocketbox] prefabs built");
        }

        public static Runtime Get(string clip) => new Runtime { controller = Controller(clip) };
        public struct Runtime { public AnimatorController controller; }
    }
}
