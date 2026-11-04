using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Imports the hospital staff built by unity/Tools/MPFB/build_staff.py + staff_pose.py (receptionist, female nurse, male nurse): FBX with the pose baked in, textures (Android ASTC 6x6), URP materials,
    /// then one prefab per person with a body collider and the StaffIdleView presentation animation. Idempotent: re-running keeps GUIDs and replaces the prefabs.
    /// </summary>
    public static class StaffSetup
    {
        public const string Folder = "Assets/Models/Characters/Staff";
        public static readonly string[] Keys = { "receptionist", "nurse_f", "nurse_m", "patient_m_old", "patient_f_old", "adult_f_phone", "child", "parent_f", "visitor_m_paper", "visitor_f_talk", "visitor_m_cough" };
        static readonly (string key, ProcSense.VR.Views.StaffIdleView.Role role, float phase)[] Civilians =
        {
            ("patient_m_old", ProcSense.VR.Views.StaffIdleView.Role.CivilianLookAround, 1.7f), ("patient_f_old", ProcSense.VR.Views.StaffIdleView.Role.CivilianWaiting, 6.2f), ("adult_f_phone", ProcSense.VR.Views.StaffIdleView.Role.CivilianPhone, 3.4f),
            ("child", ProcSense.VR.Views.StaffIdleView.Role.CivilianLookAround, 8.8f), ("parent_f", ProcSense.VR.Views.StaffIdleView.Role.CivilianTalking, 4.1f), ("visitor_m_paper", ProcSense.VR.Views.StaffIdleView.Role.CivilianReading, 9.5f),
            ("visitor_f_talk", ProcSense.VR.Views.StaffIdleView.Role.CivilianTalking, 12.9f), ("visitor_m_cough", ProcSense.VR.Views.StaffIdleView.Role.CivilianCough, 15.3f),
        };

        [Serializable] class Entry { public string mesh = "", name = "", texture; public float[] color = { 1, 1, 1, 1 }; public float roughness = .6f, metallic; public bool cutout; }

        static void Dir(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Dir(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }

        public static void Import()
        {
            foreach (var key in Keys)
            {
                var src = Path.GetFullPath("../build/mpfb/output/staff/" + key); var dir = Folder + "/" + key;
                Dir(dir + "/Textures"); Dir(dir + "/Materials");
                File.Copy(src + "/" + key + ".fbx", dir + "/" + key + ".fbx", true);
                foreach (var f in Directory.GetFiles(src + "/textures")) File.Copy(f, dir + "/Textures/" + Path.GetFileName(f), true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var f in Directory.GetFiles(dir + "/Textures", "*.png"))
                {
                    var ti = (TextureImporter)AssetImporter.GetAtPath(f.Replace('\\', '/'));
                    bool big = Path.GetFileName(f).Contains("diffuse") || Path.GetFileName(f).Contains("skin");
                    ti.maxTextureSize = big ? 2048 : 1024; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed; ti.anisoLevel = 4;
                    var android = ti.GetPlatformTextureSettings("Android"); android.overridden = true; android.maxTextureSize = big ? 2048 : 1024; android.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(android);
                    ti.SaveAndReimport();
                }
                var entries = JsonConvert.DeserializeObject<Entry[]>(File.ReadAllText(src + "/materials.json"));
                var mi = (ModelImporter)AssetImporter.GetAtPath(dir + "/" + key + ".fbx");
                bool animated = File.Exists(Path.GetFullPath("../build/mpfb/output/staff/" + key + "_anim.json"));
                mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = animated; mi.importBlendShapes = true; mi.importNormals = ModelImporterNormals.Import;
                mi.isReadable = true; mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; mi.importCameras = false; mi.importLights = false;
                foreach (var e in entries)
                {
                    string path = dir + "/Materials/" + e.name.Replace('.', '_').Replace(' ', '_') + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
                    mat.name = e.name;
                    mat.SetColor("_BaseColor", e.texture != null ? Color.white : new Color(e.color[0], e.color[1], e.color[2], 1).gamma);
                    if (e.texture != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/" + e.texture));
                    bool skin = e.mesh.EndsWith(".body"), eye = e.mesh.Contains("high-poly"), metal = e.metallic > .5f;
                    mat.SetFloat("_Smoothness", eye ? .62f : skin ? .24f : metal ? .65f : e.name.Contains("badge") ? .55f : e.name.Contains("coat") || e.name.Contains("scrub") ? .12f : e.name.Contains("shoes") ? .3f : .15f);
                    mat.SetFloat("_Metallic", metal ? .9f : 0f);
                    if (e.cutout || eye)
                    {
                        mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .35f); mat.SetFloat("_Cull", 0); mat.EnableKeyword("_ALPHATEST_ON"); mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = (int)RenderQueue.AlphaTest;
                    }
                    EditorUtility.SetDirty(mat);
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), e.name), mat);
                }
                mi.SaveAndReimport();
                if (animated)
                {
                    mi = (ModelImporter)AssetImporter.GetAtPath(dir + "/" + key + ".fbx"); var clips = mi.defaultClipAnimations;
                    foreach (var c in clips) { c.loopTime = c.name.EndsWith("_Loop"); c.loopPose = c.loopTime; c.lockRootRotation = false; }
                    mi.clipAnimations = clips; mi.SaveAndReimport();
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[StaffSetup] imported " + string.Join(", ", Keys));
        }


        /// <summary>The first (main) clip recorded for this person by Tools/MPFB/retarget_ual.py, or null if the person is not animated.</summary>
        static AnimationClip MainClip(string key)
        {
            var json = Path.GetFullPath("../build/mpfb/output/staff/" + key + "_anim.json"); if (!File.Exists(json)) return null;
            var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(json)); var name = root["clips"].First.Path.Replace("clips.", "");
            return AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + key + "/" + key + ".fbx").OfType<AnimationClip>().FirstOrDefault(a => a.name == name);
        }

        /// <summary>One prefab per person under Assets/Prefabs/Characters: the imported model, the idle animation, and a body collider for the standing nurses (the receptionist sits behind her desk).</summary>
        public static void BuildPrefabs()
        {
            Dir("Assets/Prefabs/Characters");
            var cfg = new (string key, ProcSense.VR.Views.StaffIdleView.Role role, float phase, bool collider)[]
            {
                ("receptionist", ProcSense.VR.Views.StaffIdleView.Role.Receptionist, 2.3f, false),
                ("nurse_f", ProcSense.VR.Views.StaffIdleView.Role.NurseSpeakingFirst, 0f, true),
                ("nurse_m", ProcSense.VR.Views.StaffIdleView.Role.NurseListeningFirst, 5.1f, true),
            }.Concat(Civilians.Select(c => (key: c.key, role: c.role, phase: c.phase, collider: true))).ToArray();
            foreach (var c in cfg)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + c.key + "/" + c.key + ".fbx");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.name = "Staff_" + c.key;
                var view = go.AddComponent<ProcSense.VR.Views.StaffIdleView>(); view.PersonRole = c.role; view.Phase = c.phase;
                var clip = MainClip(c.key);
                if (clip != null)
                {
                    Dir("Assets/Animations/Characters"); var ctrlPath = "Assets/Animations/Characters/" + c.key + ".controller";
                    AssetDatabase.DeleteAsset(ctrlPath); var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                    var st = ctrl.layers[0].stateMachine.AddState(clip.name); st.motion = clip; ctrl.layers[0].stateMachine.defaultState = st;
                    var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>(); anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    view.FacialOnly = true;
                }
                Bounds b = default; bool first = true;
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.updateWhenOffscreen = false; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.BlendProbes; r.skinnedMotionVectors = false;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                if (c.collider)
                {
                    var col = new GameObject("Body collision"); col.transform.SetParent(go.transform, false);
                    var cap = col.AddComponent<CapsuleCollider>(); cap.radius = .26f; cap.height = Mathf.Max(1.2f, b.size.y); cap.center = new Vector3(0, b.size.y * .5f, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Characters/Staff_" + c.key + ".prefab"); UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[StaffSetup] built prefabs for " + string.Join(", ", cfg.Select(c => c.key)));
        }


        /// <summary>Cotton twill for the lab coats: a tileable diagonal weave (albedo and normal from Tools/MPFB/make_coat_textures.py), a matte-satin response and no environment shine.</summary>
        static void CoatFabric(Material m)
        {
            const string dir = "Assets/Textures/Characters/Staff";
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Characters")) AssetDatabase.CreateFolder("Assets/Textures", "Characters");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Textures/Characters", "Staff");
            Texture2D Bring(string name, bool normal)
            {
                var dst = dir + "/" + name; System.IO.File.Copy(System.IO.Path.GetFullPath("Logs/Staff/" + name), System.IO.Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = !normal; ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.maxTextureSize = 1024;
                var android = ti.GetPlatformTextureSettings("Android"); android.overridden = true; android.maxTextureSize = 512; android.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
            }
            var albedo = Bring("labcoat_albedo.png", false); var normalMap = Bring("labcoat_normal.png", true);
            m.SetTexture("_BaseMap", albedo); m.SetColor("_BaseColor", Color.white); m.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            m.SetTexture("_BumpMap", normalMap); m.SetTextureScale("_BumpMap", new Vector2(3f, 3f)); m.SetFloat("_BumpScale", .25f); m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", .22f); m.SetFloat("_Metallic", 0f); m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
        }

        /// <summary>Garment shells are open and may face either way, so cloth materials are drawn double-sided (URP flips the normal on back faces).</summary>
        public static void FixCloth()
        {
            int n = 0;
            foreach (var key in Keys)
                foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { Folder + "/" + key + "/Materials" }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                    if (m.name.Contains("lab_coat") || m.name.Contains("scrub") || m.name.Contains("suit") || m.name.Contains("badge") || m.name.Contains("Clipboard") || m.name.Contains("clip")) { m.SetFloat("_Cull", 0); EditorUtility.SetDirty(m); n++; }
                    if (m.name.Contains("lab_coat")) { CoatFabric(m); }
                }
            AssetDatabase.SaveAssets(); Debug.Log("[StaffSetup] double-sided cloth materials: " + n);
        }

        /// <summary>Prints each model's skeleton and blend shape names (to author the idle animation against what is really there).</summary>
        public static void Describe()
        {
            foreach (var key in Keys)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + key + "/" + key + ".fbx");
                var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Debug.Log($"[StaffSetup] {key}: {smrs.Length} skinned meshes, {go.GetComponentsInChildren<Transform>(true).Length} transforms, root '{go.transform.GetChild(0).name}' height-ish");
                foreach (var r in smrs) Debug.Log($"[StaffSetup]   {r.name}: tris {Enumerable.Range(0, r.sharedMesh.subMeshCount).Sum(i => r.sharedMesh.GetTriangles(i).Length / 3)}, shapes {r.sharedMesh.blendShapeCount}, bounds {r.sharedMesh.bounds.size:F2}");
                var body = smrs.First(r => r.name.EndsWith(".body")); var names = Enumerable.Range(0, body.sharedMesh.blendShapeCount).Select(i => body.sharedMesh.GetBlendShapeName(i)).ToArray();
                Debug.Log("[StaffSetup]   shapes: " + string.Join(",", names.Where(n => n.Contains("Blink") || n.Contains("jaw") || n.Contains("Smile") || n.Contains("viseme") || n.Contains("brow")).Take(40)));
                Debug.Log("[StaffSetup]   bones: " + string.Join(",", go.GetComponentsInChildren<Transform>(true).Select(t => t.name).Where(n => n == "spine_03" || n == "head" || n == "pelvis" || n.StartsWith("upperarm") || n.StartsWith("lowerarm") || n.StartsWith("hand_") || n == "neck_01")));
            }
        }
    }
}
