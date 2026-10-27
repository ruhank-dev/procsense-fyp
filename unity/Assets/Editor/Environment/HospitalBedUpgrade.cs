using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Applies the remodelled hospital bed to the existing PatientBay without touching scene YAML by hand: reimports the frame
    /// FBX and the pillow OBJ with their metadata (GUIDs) kept, rewrites the existing mattress mesh asset in place (now the blue
    /// cover, one submesh), imports the baked maps, and assigns four PBR materials (frame atlas, cover, pillowcase). The bed
    /// is dynamic and probe-lit, so the room lightmaps are not rebaked. Inputs come from Logs/HospitalBed (Tools/Environment/*).
    /// Safe to run again; it only overwrites those assets and re-assigns materials.
    /// </summary>
    public static class HospitalBedUpgrade
    {
        const string BayPath = "Assets/Scenes/PatientBay.unity";
        const string FbxPath = "Assets/Models/Environment/RoomDressing/hospital_bed.fbx";
        const string PrefabPath = "Assets/Prefabs/Environment/RoomDressing/hospital_bed.prefab";
        const string PillowPath = "Assets/Models/Characters/MPFBPatient/patient_pillow.obj";
        const string MaterialsDir = "Assets/Materials/Hospital/RoomDressing";
        const string TexDir = "Assets/Textures/Hospital/Bed";
        
        [Serializable] class MeshData { public float[] vertices, normals, uv; public int[] tris0, tris1; }

        static string Logs => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "HospitalBed"));
        static string Abs(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        public static void Run()
        {
            Copy("hospital_bed.fbx", FbxPath);
            Copy("patient_pillow.obj", PillowPath);
            Directory.CreateDirectory(Abs(TexDir));
            foreach (var old in new[] { "bed_sheet_ao.png" }) AssetDatabase.DeleteAsset(TexDir + "/" + old);
            foreach (var old in new[] { "Bed mattress vinyl", "Bed fitted sheet", "Bed control keypad" })
                AssetDatabase.DeleteAsset(MaterialsDir + "/" + old + ".mat");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var pillowImporter = (ModelImporter)AssetImporter.GetAtPath(PillowPath);
            if (!pillowImporter.isReadable) { pillowImporter.isReadable = true; pillowImporter.SaveAndReimport(); }

            var tex = new[]
            {
                Tex("frame_albedo.png", "bed_frame_albedo.png", TexKind.Colour, 256),
                Tex("frame_metal_smooth.png", "bed_frame_metal_smooth.png", TexKind.Data, 256),
                Tex("cover_albedo.png", "bed_cover_albedo.png", TexKind.Colour, 2048),
                Tex("cover_normal.png", "bed_cover_normal.png", TexKind.Normal, 2048),
                Tex("cover_occlusion.png", "bed_cover_occlusion.png", TexKind.Data, 512),
                Tex("pillow_albedo.png", "bed_pillow_albedo.png", TexKind.Colour, 1024),
                Tex("pillow_normal.png", "bed_pillow_normal.png", TexKind.Normal, 1024),
                Tex("pillow_occlusion.png", "bed_pillow_occlusion.png", TexKind.Data, 512),
            };
            var weave = Tex("fabric_weave_normal.png", "bed_fabric_weave_normal.png", TexKind.Normal, 256);
            var frame = Mat("Bed frame atlas", c => { c.SetTexture("_BaseMap", tex[0]); c.SetTexture("_MetallicGlossMap", tex[1]);
                c.EnableKeyword("_METALLICSPECGLOSSMAP"); c.SetFloat("_Smoothness", 1f); c.SetFloat("_Metallic", 1f); });
            var cover = Mat("Bed mattress cover", c => Fabric(c, tex[2], tex[3], tex[4], weave, 1.35f / .075f, 2.4f / .075f, .9f));
            var pillowcase = Mat("Bed pillowcase", c => Fabric(c, tex[5], tex[6], tex[7], weave, .627f / .075f, .36f / .075f, .8f));

            var scene = EditorSceneManager.OpenScene(BayPath, OpenSceneMode.Single);
            var mattress = GameObject.Find("Soft mattress");
            var mesh = mattress.GetComponent<MeshFilter>().sharedMesh;
            Apply(mesh, JsonUtility.FromJson<MeshData>(File.ReadAllText(Path.Combine(Logs, "bed_cover.json"))));
            mattress.GetComponent<MeshRenderer>().sharedMaterials = new[] { cover };
            foreach (var r in GameObject.Find("patient_pillow").GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = pillowcase;
            UpdatePrefab(frame);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Report(mesh);
        }

        static void Fabric(Material m, Texture2D albedo, Texture2D normal, Texture2D occlusion, Texture2D weave, float tx, float ty, float detail)
        {
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 1f);                        // smoothness comes from the albedo alpha
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            m.SetFloat("_Metallic", 0f);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", occlusion); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
            m.SetTexture("_DetailNormalMap", weave); m.SetFloat("_DetailNormalMapScale", detail);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(tx, ty));
            m.EnableKeyword("_DETAIL_MULX2");
        }

        enum TexKind { Colour, Normal, Data }

        static Texture2D Tex(string logName, string assetName, TexKind kind, int max)
        {
            var path = TexDir + "/" + assetName;
            File.Copy(Path.Combine(Logs, logName), Abs(path), true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var t = (TextureImporter)AssetImporter.GetAtPath(path);
            t.textureType = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = kind == TexKind.Colour;
            t.alphaSource = kind == TexKind.Normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = false;
            t.mipmapEnabled = true;
            t.wrapMode = kind == TexKind.Data && logName.StartsWith("frame") ? TextureWrapMode.Clamp : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 4;
            t.maxTextureSize = max;
            t.isReadable = false;
            t.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = max,
                format = kind == TexKind.Normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed
            });
            t.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Copy(string file, string assetPath)
        {
            var dst = Abs(assetPath);
            if (!File.Exists(dst)) throw new InvalidOperationException("Refusing to create a new asset: " + assetPath);
            File.Copy(Path.Combine(Logs, file), dst, true);
        }

        static Material Mat(string name, Action<Material> setup)
        {
            var path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.enableInstancing = true;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Apply(Mesh mesh, MeshData d)
        {
            var v = new Vector3[d.vertices.Length / 3];
            var n = new Vector3[v.Length];
            var uv = new Vector2[v.Length];
            for (var i = 0; i < v.Length; i++)
            {
                v[i] = new Vector3(d.vertices[i * 3], d.vertices[i * 3 + 1], d.vertices[i * 3 + 2]);
                n[i] = new Vector3(d.normals[i * 3], d.normals[i * 3 + 1], d.normals[i * 3 + 2]);
                uv[i] = new Vector2(d.uv[i * 2], d.uv[i * 2 + 1]);
            }
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(d.tris0, 0, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
        }

        static void UpdatePrefab(Material frame)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = frame;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);        // dynamic, probe-lit: no room rebake
                    r.receiveGI = ReceiveGI.LightProbes;
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                    r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Report(Mesh cover)
        {
            var bed = GameObject.Find("hospital_bed");
            foreach (var r in bed.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Debug.Log($"[BedUpgrade] {r.name} tris={mesh.triangles.Length / 3} mats=[{string.Join(", ", r.sharedMaterials.Select(m => m ? m.name : "NULL"))}] static={GameObjectUtility.GetStaticEditorFlags(r.gameObject)}");
            }
            Debug.Log($"[BedUpgrade] cover mesh tris={cover.triangles.Length / 3} submeshes={cover.subMeshCount} bounds={cover.bounds}");
        }
    }
}
