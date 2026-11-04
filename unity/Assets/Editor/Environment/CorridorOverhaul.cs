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
    /// <summary>
    /// Corridor overhaul (owner brief, parts 4-7). Restyles the corridor to the hospital design system (grey vinyl, blue-grey panels, white paint), cuts a large east window onto the shared city exterior,
    /// adds the semi-circular reception (desk open at the north end so staff can walk behind it, chair, workstation, receptionist, television, sign), the notice board, seven framed doctor portraits,
    /// three certificates and two award plaques (all fictional), and stands two nurses talking by the window. Idempotent: everything it adds sits under "Corridor overhaul" and is rebuilt each run.
    /// </summary>
    public static class CorridorOverhaul
    {
        const string Models = "Assets/Models/Environment/Corridor", Tex = "Assets/Textures/Hospital/Corridor", Mats = "Assets/Materials/Hospital/Corridor";
        const string Root = "Corridor overhaul";
        // the expanded corridor: 4 m wide, 24 m long, 3.2 m to the ceiling; both short end walls are glass. Inner faces of the long walls:
        const float WestX = 2.55f, EastX = 6.45f, Z0 = -9.8f, Z1 = 14.2f, CeilH = 3.2f;       // Z0/Z1: outer faces of the glass walls; the glass sits at Z0+.05 / Z1-.05
        const float SouthZ = Z0 + .05f, NorthZ = Z1 - .05f;
        // free-standing reception toward the east side of the south end: the flat side of the arc faces the glass, 2.6 m of floor behind it for the receptionist and the route round
        static readonly Vector3 DeskPos = new Vector3(4.5f, 0, -7.2f);
        const float DeskYaw = 0f;

        static void Dir(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Dir(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }
        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        // ------------------------------------------------------------------------------------------------------------------------------------ import
        public static void Import()
        {
            Dir(Models); Dir(Tex); Dir(Mats);
            var files = new Dictionary<string, string>
            {
                ["reception_desk"] = "Logs/Reception", ["reception_chair"] = "Logs/Reception", ["reception_workstation"] = "Logs/Reception",
                ["frame_portrait"] = "Logs/WallArt", ["frame_landscape"] = "Logs/WallArt", ["award_plaque"] = "Logs/WallArt", ["notice_board"] = "Logs/WallArt",
            };
            foreach (var kv in files)
            {
                var dst = Models + "/" + kv.Key + ".fbx"; File.Copy(Path.GetFullPath(kv.Value + "/" + kv.Key + ".fbx"), Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(dst);
                mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.isReadable = false;
                mi.generateSecondaryUV = true; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport();
            }
            foreach (var f in Directory.GetFiles(Path.GetFullPath("Logs/CorridorArt"), "*.png").Where(f => !f.EndsWith("overview.png")))
            {
                var dst = Tex + "/" + Path.GetFileName(f); File.Copy(f, Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.textureCompression = TextureImporterCompression.Compressed; ti.sRGBTexture = true; ti.wrapMode = TextureWrapMode.Clamp;
                var android = ti.GetPlatformTextureSettings("Android"); android.overridden = true; android.maxTextureSize = 2048; android.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CorridorOverhaul] imported reception, wall art and textures");
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ materials
        static Material Mat(string name, string hex, float smooth, float metal = 0f, Texture2D map = null)
        {
            var path = Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.enableInstancing = true; m.SetColor("_BaseColor", map != null ? Color.white : Hex(hex)); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            m.SetTexture("_BaseMap", map); EditorUtility.SetDirty(m); return m;
        }

        static Material Glass(string name, float alpha, float smooth)
        {
            var m = Mat(name, "DCEBF2", smooth); m.SetColor("_BaseColor", new Color(.86f, .93f, .96f, alpha));
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 2);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m); return m;
        }

        static Material Emissive(string name, Texture2D tex, float intensity)
        {
            var m = Mat(name, "000000", .9f); m.SetColor("_BaseColor", new Color(.02f, .02f, .02f, 1)); m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.white * intensity);
            m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; EditorUtility.SetDirty(m); return m;
        }

        static Texture2D T(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + file);

        static Dictionary<string, Material> SuffixMaterials(Scene scene)
        {
            var map = new Dictionary<string, Material>();
            foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                int i = r.name.LastIndexOf("_M_", StringComparison.Ordinal); if (i < 0 || r.sharedMaterial == null) continue;
                var key = r.name.Substring(i + 3); if (!map.ContainsKey(key)) map[key] = r.sharedMaterial;
            }
            map["DeskQuartz"] = Mat("Reception quartz", "EDEEED", .78f); map["DeskWhite"] = Mat("Reception white laminate", "E4E6E4", .55f); map["DeskBlueGrey"] = Mat("Reception blue-grey panel", "8794A8", .45f);
            map["DeskTeal"] = Mat("Reception teal accent", "1F6F78", .6f); map["DeskKick"] = Mat("Reception kick", "14171A", .35f); map["ChairFabric"] = Mat("Reception chair fabric", "27666F", .12f);
            map["KeyGrey"] = Mat("Keyboard grey", "3A3D42", .4f); map["ScreenOn"] = Emissive("Reception screen", T("reception_screen.png"), 1.1f);
            map["FrameWalnut"] = Mat("Frame walnut", "4C2C1B", .5f); map["MatWhite"] = Mat("Frame mat", "EDEBE5", .1f); map["Glass"] = Glass("Frame glass", .06f, .96f);
            map["AluFrame"] = Mat("Board aluminium", "B4B8BE", .6f, .9f); map["BrassScrew"] = Mat("Brass screw", "A47C34", .65f, .9f); map["Print"] = Mat("Print placeholder", "FFFFFF", .5f);
            if (!map.ContainsKey("SteelBrushed")) map["SteelBrushed"] = Mat("Brushed steel", "9AA0A6", .6f, .9f);
            if (!map.ContainsKey("PlasticGrey")) map["PlasticGrey"] = Mat("Plastic grey", "5A5E64", .35f);
            if (!map.ContainsKey("PlasticWhite")) map["PlasticWhite"] = Mat("Plastic white", "E8E9E7", .45f);
            if (!map.ContainsKey("RubberBlack")) map["RubberBlack"] = Mat("Rubber black", "0F1012", .2f);
            return map;
        }

        static GameObject Spawn(string model, Transform parent, Vector3 pos, float yaw, Dictionary<string, Material> mats, Material print = null, Vector3? scale = null, bool contributeGi = true)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + model + ".fbx"); if (src == null) throw new InvalidOperationException("missing " + model);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(parent, false); go.name = model;
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); if (scale.HasValue) go.transform.localScale = scale.Value;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                int i = r.name.LastIndexOf("_M_", StringComparison.Ordinal); var key = i >= 0 ? r.name.Substring(i + 3) : "";
                r.sharedMaterial = key == "Print" && print != null ? print : mats.TryGetValue(key, out var m) ? m : mats["PlasticGrey"];
                r.shadowCastingMode = key == "Glass" || key == "Print" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                if (contributeGi) { r.receiveGI = ReceiveGI.Lightmaps; GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI); }
            }
            return go;
        }

        static Bounds Bnd(GameObject go) { var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b; }
        static void Fit(Transform t, Vector3 min, Vector3 max) { var p = t.parent ? t.parent.lossyScale : Vector3.one; t.position = (min + max) * .5f; var s = max - min; t.localScale = new Vector3(s.x / p.x, s.y / p.y, s.z / p.z); }
        static Transform Find(Scene scene, string name, string parent = null) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name && (parent == null || (t.parent != null && t.parent.name == parent)));
        static void MoveTo(Transform t, float? cx, float? cz) { var b = Bnd(t.gameObject); t.position += new Vector3(cx.HasValue ? cx.Value - b.center.x : 0, 0, cz.HasValue ? cz.Value - b.center.z : 0); }
        static Material Load(string name) { var g = AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(q => Path.GetFileNameWithoutExtension(q) == name); return g == null ? null : AssetDatabase.LoadAssetAtPath<Material>(g); }
        static void Tint(string name, string hex, float smooth, bool keepTexture)
        {
            var m = Load(name); if (m == null) { Debug.LogWarning("[CorridorOverhaul] material not found: " + name); return; }
            m.SetColor("_BaseColor", Hex(hex)); m.color = Hex(hex); m.SetFloat("_Smoothness", smooth); if (!keepTexture) { m.SetTexture("_BaseMap", null); m.mainTexture = null; }
            EditorUtility.SetDirty(m);
        }

        static void Box(Transform parent, string name, Vector3 min, Vector3 max, Material m, bool collide = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            if (!collide) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = (min + max) * .5f; go.transform.localScale = max - min; go.GetComponent<MeshRenderer>().sharedMaterial = m; GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ apply
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Root); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(Root).transform;
            var mats = SuffixMaterials(scene);
            Architecture(scene, root, mats);
            Reception(scene, root, mats);
            Art(root, mats);
            Dressing(scene);
            MatchPatientBayLighting(scene);
            People(root);
            Plants(root);
            PatientBayLayout.HideTeleportVisuals(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[CorridorOverhaul] applied: corridor " + (Z1 - Z0) + " m x " + (EastX - WestX + .1f) + " m, ceiling " + CeilH);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ architecture
        static void Architecture(Scene scene, Transform root, Dictionary<string, Material> mats)
        {
            var corridor = Find(scene, "Corridor", "Connected Corridor");
            var layoutRoot = scene.GetRootGameObjects().First(g => g.GetComponent<ProcSense.VR.Boot.RoomLayout>() != null).GetComponent<ProcSense.VR.Boot.RoomLayout>();
            layoutRoot.Size = new Vector2(4f, Z1 - Z0);                                                                                       // centred on the root at z = 2.2, unchanged
            foreach (var n in new[] { "East wall above window", "East wall window south pier", "East wall window north pier", "South wall above windows", "South wall west edge", "South wall door pier", "South wall east edge" })
            { var x = Find(scene, n); if (x != null) UnityEngine.Object.DestroyImmediate(x.gameObject); }
            float wallTop = CeilH + .1f;
            var vinyl = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/PatientBayLook/PatientBay grey vinyl.mat");
            var floor = Find(scene, "Floor", "Corridor"); Fit(floor, new Vector3(2.5f, -.1f, Z0), new Vector3(6.5f, 0, Z1)); if (vinyl != null) floor.GetComponent<MeshRenderer>().sharedMaterial = vinyl;
            Tint("Warm painted plaster", "F2F2EE", .06f, true); Tint("Pale oak laminate", "A9B8CE", .16f, false); Tint("Charcoal skirting", "56626F", .3f, true);
            Fit(Find(scene, "Wall left doorway left", "Corridor"), new Vector3(2.45f, 0, Z0), new Vector3(2.55f, wallTop, -1.37f));
            Fit(Find(scene, "Wall left doorway header", "Corridor"), new Vector3(2.45f, 2.14f, -1.37f), new Vector3(2.55f, wallTop, -.23f));
            Fit(Find(scene, "Wall left doorway right", "Corridor"), new Vector3(2.45f, 0, -.23f), new Vector3(2.55f, wallTop, Z1));
            Fit(Find(scene, "Wall right doorway left", "Corridor"), new Vector3(6.45f, 0, Z0), new Vector3(6.55f, wallTop, 4.63f));
            Fit(Find(scene, "Wall right doorway header", "Corridor"), new Vector3(6.45f, 2.14f, 4.63f), new Vector3(6.55f, wallTop, 5.77f));
            Fit(Find(scene, "Wall right doorway right", "Corridor"), new Vector3(6.45f, 0, 5.77f), new Vector3(6.55f, wallTop, Z1));
            foreach (var n in new[] { "Wall front", "Wall back" }) { var w = Find(scene, n, "Corridor"); if (w != null) UnityEngine.Object.DestroyImmediate(w.gameObject); }        // the short walls become glass
            foreach (var n in new[] { "Wall left", "Wall right" }) { var w = Find(scene, n, "Corridor"); if (w != null) w.gameObject.SetActive(false); }

            // finishes: remove the old panel / rail / skirting / ceiling parts and lay them out again for the long walls (the same materials)
            var fin = Find(scene, "Hospital finishes");
            Material panelM = null, railM = null, skirtM = null;
            foreach (var c in fin.Cast<Transform>().ToArray())
            {
                var r = c.GetComponent<MeshRenderer>();
                if (c.name.StartsWith("Lower oak wall panel")) { panelM = panelM ? panelM : r.sharedMaterial; UnityEngine.Object.DestroyImmediate(c.gameObject); }
                else if (c.name.StartsWith("Panel cap rail")) { railM = railM ? railM : r.sharedMaterial; UnityEngine.Object.DestroyImmediate(c.gameObject); }
                else if (c.name.StartsWith("Coved skirting")) { skirtM = skirtM ? skirtM : r.sharedMaterial; UnityEngine.Object.DestroyImmediate(c.gameObject); }
                else if (c.name == "Corridor ceiling grid" || c.name == "Corridor luminaire frames" || c.name == "Corridor luminaire diffusers" || c.name.StartsWith("Ceiling fill")) UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            panelM = panelM ? panelM : Load("Pale oak laminate"); railM = railM ? railM : mats["PlasticWhite"]; skirtM = skirtM ? skirtM : Load("Charcoal skirting");
            foreach (var old in fin.Cast<Transform>().Where(c => c.name == "Corridor trim").ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);                    // earlier runs' trim, grid and luminaires
            var trim = new GameObject("Corridor trim").transform; trim.SetParent(fin, false);
            foreach (var (x0, x1, za, zb) in new[] { (2.55f, 2.57f, Z0 + .1f, -1.37f), (2.55f, 2.57f, -.23f, Z1 - .1f), (6.43f, 6.45f, Z0 + .1f, 4.63f), (6.43f, 6.45f, 5.77f, Z1 - .1f) })
            {
                bool west = x0 < 4f; float xr = west ? 2.55f : 6.45f, dir = west ? 1f : -1f;
                Box(trim, "Lower panel", new Vector3(Mathf.Min(xr, xr + dir * .02f), 0, za), new Vector3(Mathf.Max(xr, xr + dir * .02f), 1.12f, zb), panelM);
                Box(trim, "Cap rail", new Vector3(Mathf.Min(xr, xr + dir * .045f), 1.12f, za), new Vector3(Mathf.Max(xr, xr + dir * .045f), 1.16f, zb), railM);
                Box(trim, "Skirting", new Vector3(Mathf.Min(xr, xr + dir * .05f), 0, za), new Vector3(Mathf.Max(xr, xr + dir * .05f), .12f, zb), skirtM);
            }
            Fit(Find(scene, "Ceiling", "Hospital finishes"), new Vector3(2.5f, CeilH, Z0), new Vector3(6.5f, CeilH + .08f, Z1));
            Ceiling(trim, mats);

            // doors that used to sit in the end walls move onto the long walls, each rigidly with its logic object and anchors
            MoveDoor(scene, "Door_MRI_MRCP", new Vector3(2.65f, 0, 7.10f), 270f);
            MoveDoor(scene, "Door_XRay", new Vector3(2.65f, 0, 9.80f), 270f);
            MoveDoor(scene, "Door_Laboratory", new Vector3(6.50f, 0, 9.80f), 90f);

            GlassWall(root, "South glass wall", SouthZ, mats); GlassWall(root, "North glass wall", NorthZ, mats);
            Probes(scene);
        }

        static void MoveDoor(Scene scene, string logic, Vector3 pos, float yaw)
        {
            var arch = Find(scene, logic + " architecture"); var door = Find(scene, logic); if (arch == null || door == null) { Debug.LogWarning("[CorridorOverhaul] door missing: " + logic); return; }
            var from = Matrix4x4.TRS(arch.position, arch.rotation, Vector3.one); var to = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), Vector3.one); var T = to * from.inverse;
            foreach (var t in new[] { arch, door })
            { var m = T * Matrix4x4.TRS(t.position, t.rotation, Vector3.one); t.SetPositionAndRotation(m.GetColumn(3), m.rotation); }
        }

        /// <summary>Material by name from anywhere in the project (the PatientBay finishes live in their own folder).</summary>
        static Material Any(string name)
        {
            foreach (var g in AssetDatabase.FindAssets("t:Material " + name))
            { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p); }
            return null;
        }

        /// <summary>The corridor ceiling is PatientBay's ceiling continued: the same acoustic tile material (repeat scaled to the corridor), the same hairline pale grid, the same luminaire housing and prism diffuser,
        /// lit by the same baked rectangle panel (1.9) and soft realtime fill (0.85) per luminaire. No thick grid, no blank white rectangles.</summary>
        static void Ceiling(Transform parent, Dictionary<string, Material> mats)
        {
            var tileSrc = Any("PatientBay ceiling tile"); var gridM = Any("PatientBay ceiling grid") ?? Load("Ceiling joints") ?? mats["PlasticWhite"]; var housing = Any("Light housing") ?? mats["PlasticGrey"]; var diff = Any("PatientBay ceiling diffuser") ?? Load("Diffused ceiling light") ?? mats["PlasticWhite"];
            var slab = Find(parent.root.gameObject.scene, "Ceiling", "Hospital finishes");
            if (tileSrc != null && slab != null)
            {
                var path = Mats + "/Corridor ceiling tile.mat"; var tm = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (tm == null) { tm = new Material(tileSrc) { name = "Corridor ceiling tile" }; AssetDatabase.CreateAsset(tm, path); } else tm.CopyPropertiesFromMaterial(tileSrc);
                foreach (var prop in new[] { "_BaseMap", "_BumpMap" }) { tm.SetTextureScale(prop, new Vector2(1.7f, 10f)); }
                EditorUtility.SetDirty(tm); slab.GetComponent<MeshRenderer>().sharedMaterial = tm;
                slab.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;                                         // the ceiling must block the scene-wide PatientBay sun, or its grid of bars prints on the walls
            }
            var grid = new GameObject("Ceiling grid").transform; grid.SetParent(parent, false);
            for (float z = Z0 + .6f; z < Z1; z += .6f) Box(grid, "T-bar", new Vector3(2.55f, CeilH - .005f, z - .004f), new Vector3(6.45f, CeilH, z + .004f), gridM);
            for (float x = 3.15f; x < 6.45f; x += .6f) Box(grid, "T-bar", new Vector3(x - .004f, CeilH - .005f, Z0), new Vector3(x + .004f, CeilH, Z1), gridM);
            var lights = Find(grid.root.gameObject.scene, "Hospital lighting");
            foreach (var c in lights.Cast<Transform>().Where(c => c.name.StartsWith("Baked ceiling panel") || c.name.StartsWith("Ceiling fill")).ToArray()) UnityEngine.Object.DestroyImmediate(c.gameObject);
            int n = 0;
            for (float z = Z0 + 1.5f; z < Z1 - .8f; z += 3.0f)
            {
                Box(grid, "Luminaire frame", new Vector3(4.5f - .325f, CeilH - .04f, z - .625f), new Vector3(4.5f + .325f, CeilH, z + .625f), housing);
                Box(grid, "Luminaire diffuser", new Vector3(4.5f - .29f, CeilH - .052f, z - .59f), new Vector3(4.5f + .29f, CeilH - .04f, z + .59f), diff);
                var l = new GameObject("Baked ceiling panel " + n); l.transform.SetParent(lights, false); l.transform.position = new Vector3(4.5f, CeilH - .1f, z); l.transform.rotation = Quaternion.Euler(90f, 0, 0);
                var lc = l.AddComponent<Light>(); lc.type = LightType.Rectangle; lc.areaSize = new Vector2(.58f, 1.18f); lc.color = new Color(1f, .98f, .95f); lc.intensity = 1.9f; lc.range = 10f; lc.lightmapBakeType = LightmapBakeType.Baked; lc.shadows = LightShadows.None;
                var f = new GameObject("Ceiling fill " + n++); f.transform.SetParent(lights, false); f.transform.position = new Vector3(4.5f, CeilH - .25f, z);
                var fl = f.AddComponent<Light>(); fl.type = LightType.Point; fl.range = 5f; fl.intensity = .85f; fl.color = new Color(.933f, .961f, .937f); fl.shadows = LightShadows.None;
            }
        }

        /// <summary>The realtime directional lights follow PatientBay's approved settings: a white room light and a faint warm key, neither casting shadows (the glass walls and rails would otherwise print hard grids on the walls).</summary>
        static void MatchPatientBayLighting(Scene scene)
        {
            foreach (var l in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)))
            {
                if (l.type == LightType.Directional && l.lightmapBakeType == LightmapBakeType.Realtime)
                {
                    l.shadows = LightShadows.None;
                    if (l.name == "Soft clinical key") { l.color = new Color(1f, .97f, .92f); l.intensity = .2f; }
                }
                if (l.name == "Desk downlight") l.intensity = 2.0f;
                if (l.name.EndsWith("daylight") && l.type == LightType.Rectangle) { l.color = new Color(.92f, .96f, 1f); l.intensity = 1.0f; l.range = 10f; }
            }
        }

        static void Probes(Scene scene)
        {
            var g = Find(scene, "Baked light probes"); var pg = g ? g.GetComponent<LightProbeGroup>() : null;
            if (pg != null)
            {
                var pts = new List<Vector3>();
                for (float z = Z0 + .8f; z < Z1; z += 2.0f) foreach (var x in new[] { 3.2f, 4.5f, 5.8f }) foreach (var y in new[] { .5f, 1.3f, 2.4f }) pts.Add(g.InverseTransformPoint(new Vector3(x, y, z)));
                pg.probePositions = pts.ToArray();
            }
            var rp = Find(scene, "Baked room reflections");
            if (rp != null)
            {
                var r = rp.GetComponent<ReflectionProbe>(); rp.position = new Vector3(4.5f, 1.4f, 2.2f); r.size = new Vector3(4.2f, CeilH, Z1 - Z0); r.center = Vector3.zero; r.boxProjection = true;
                foreach (var (name, z) in new[] { ("Reflections south", -6.0f), ("Reflections north", 10.5f) })
                {
                    var q = Find(scene, name); var go = q ? q.gameObject : new GameObject(name); go.transform.SetParent(rp.parent, false); go.transform.position = new Vector3(4.5f, 1.4f, z);
                    var c = go.GetComponent<ReflectionProbe>(); if (c == null) c = go.AddComponent<ReflectionProbe>(); c.mode = r.mode; c.size = new Vector3(4.2f, CeilH, 8f); c.boxProjection = true; c.resolution = r.resolution; c.importance = 2; c.refreshMode = r.refreshMode;
                }
            }
        }

        /// <summary>A floor-to-ceiling glazed end wall: four 0.97 m panes, slim graphite mullions, a head beam, a floor curb, and a stainless safety rail; an invisible collider keeps the player off the glass.</summary>
        static void GlassWall(Transform root, string name, float z, Dictionary<string, Material> mats)
        {
            var g = new GameObject(name).transform; g.SetParent(root, false); var frame = Mat("Glass wall mullion", "3E444B", .5f, .85f); var steel = mats["SteelBrushed"];
            float d = z < 0 ? 1f : -1f;                                                                                                       // toward the room
            Box(g, "Head beam", new Vector3(2.5f, CeilH - .16f, z - .1f), new Vector3(6.5f, CeilH, z + .1f), frame);
            Box(g, "Floor curb", new Vector3(2.5f, 0, z - .1f), new Vector3(6.5f, .1f, z + .1f), frame);
            for (int i = 0; i <= 3; i++)
            {
                float x = 2.55f + i * (EastX - WestX) / 3f;
                Box(g, "Mullion", new Vector3(x - .035f, .1f, z - .09f), new Vector3(x + .035f, CeilH - .16f, z + .09f), frame);
                Box(g, "Mullion cap", new Vector3(x - .045f, .1f, z + d * .09f - .01f), new Vector3(x + .045f, CeilH - .16f, z + d * .09f + .01f), steel);
            }
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rail.name = "Safety rail"; rail.transform.SetParent(g, false); UnityEngine.Object.DestroyImmediate(rail.GetComponent<Collider>());
            rail.transform.position = new Vector3(4.5f, 1.05f, z + d * .14f); rail.transform.rotation = Quaternion.Euler(0, 0, 90f); rail.transform.localScale = new Vector3(.035f, 2f, .035f); rail.GetComponent<MeshRenderer>().sharedMaterial = steel;
            for (int i = 0; i <= 3; i++) { float x = 2.55f + i * (EastX - WestX) / 3f; Box(g, "Rail bracket", new Vector3(x - .015f, 1.03f, z + d * .09f - .01f), new Vector3(x + .015f, 1.07f, z + d * .14f), steel); }
            var glass = Glass("Corridor glass wall", .03f, .88f);
            for (int i = 0; i < 3; i++)
            {
                float x0 = 2.55f + i * (EastX - WestX) / 3f + .035f, x1 = 2.55f + (i + 1) * (EastX - WestX) / 3f - .035f;
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube); p.name = "Glass pane"; p.transform.SetParent(g, false); UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.position = new Vector3((x0 + x1) / 2f, (CeilH - .16f + .1f) / 2f, z); p.transform.localScale = new Vector3(x1 - x0, CeilH - .26f, .012f);
                var mr = p.GetComponent<MeshRenderer>(); mr.sharedMaterial = glass; mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            var col = new GameObject("Glass wall collision"); col.transform.SetParent(g, false); col.transform.position = new Vector3(4.5f, CeilH / 2f, z); var bc = col.AddComponent<BoxCollider>(); bc.size = new Vector3(4f, CeilH, .12f);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ reception
        static void Reception(Scene scene, Transform root, Dictionary<string, Material> mats)
        {
            var recep = new GameObject("Reception").transform; recep.SetParent(root, false);
            var desk = Spawn("reception_desk", recep, DeskPos, DeskYaw, mats); var deskT = desk.transform;
            Spawn("reception_chair", recep, deskT.TransformPoint(new Vector3(0, 0, .50f - .06f)), DeskYaw, mats);
            Spawn("reception_workstation", recep, deskT.TransformPoint(new Vector3(0, .77f, 1.17f)), DeskYaw + 180f, mats);
            var col = new GameObject("Reception collision").transform; col.SetParent(recep, false);
            for (int i = 0; i < 10; i++)
            {
                float a0 = -50f + 10f * i, a1 = a0 + 10f, am = (a0 + a1) * .5f * Mathf.Deg2Rad; float r = (1.30f + 1.645f) * .5f; float chord = 2f * 1.475f * Mathf.Sin(5f * Mathf.Deg2Rad) + .02f;
                var b = new GameObject("Desk segment " + i); b.transform.SetParent(col, false);
                b.transform.position = deskT.TransformPoint(new Vector3(r * Mathf.Sin(am), .54f, r * Mathf.Cos(am))); b.transform.rotation = Quaternion.Euler(0, DeskYaw + am * Mathf.Rad2Deg, 0);
                b.AddComponent<BoxCollider>().size = new Vector3(chord, 1.08f, .36f);
            }
            // television: hung from the ceiling on two rods between the receptionist and the glass, at eye level, narrow enough to leave the skyline on either side
            var tv = SpawnTv(recep, new Vector3(DeskPos.x, 1.80f, -8.55f), 0f, 1.30f);
            var rodM = mats["SteelBrushed"]; var rods = new GameObject("TV ceiling mount").transform; rods.SetParent(recep, false);
            foreach (var dx in new[] { -.55f, .55f })
                Box(rods, "Rod", new Vector3(DeskPos.x + dx - .012f, 1.80f + .46f, -8.55f - .012f), new Vector3(DeskPos.x + dx + .012f, CeilH, -8.55f + .012f), rodM);
            Box(rods, "Crossbar", new Vector3(DeskPos.x - .66f, 1.80f + .44f, -8.55f - .02f), new Vector3(DeskPos.x + .66f, 1.80f + .47f, -8.55f + .02f), rodM);
            Box(rods, "Ceiling plate L", new Vector3(DeskPos.x - .55f - .05f, CeilH - .015f, -8.55f - .05f), new Vector3(DeskPos.x - .55f + .05f, CeilH, -8.55f + .05f), rodM);
            Box(rods, "Ceiling plate R", new Vector3(DeskPos.x + .55f - .05f, CeilH - .015f, -8.55f - .05f), new Vector3(DeskPos.x + .55f + .05f, CeilH, -8.55f + .05f), rodM);
            // sign hung over the desk
            var sign = new GameObject("Reception sign").transform; sign.SetParent(recep, false); sign.position = new Vector3(DeskPos.x, 2.62f, DeskPos.z + 1.05f); sign.rotation = Quaternion.Euler(0, 180f, 0);                  // reads from the corridor side (north of the desk)
            Box(sign, "Plate", new Vector3(-.55f, -.09f, -.012f), new Vector3(.55f, .09f, .012f), AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Reception teal accent.mat"));
            foreach (var m in sign.GetComponentsInChildren<Transform>().Where(x => x.name == "Plate")) { m.localPosition = Vector3.zero; m.localScale = new Vector3(1.1f, .18f, .024f); m.localRotation = Quaternion.identity; }
            foreach (var dx in new[] { -.4f, .4f }) Box(sign, "Hanger", new Vector3(dx - .008f, .09f, -.008f), new Vector3(dx + .008f, CeilH - 2.62f, .008f), rodM);
            var txt = new GameObject("Text"); txt.transform.SetParent(sign, false); txt.transform.localPosition = new Vector3(0, 0, -.0135f); txt.transform.localRotation = Quaternion.Euler(0, 0, 0);
            var tmp = txt.AddComponent<TextMeshPro>(); tmp.text = "RECEPTION"; tmp.fontSize = 1.3f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.fontStyle = FontStyles.Bold; tmp.rectTransform.sizeDelta = new Vector2(1f, .16f);
            foreach (var dz in new[] { -.9f, .9f })
            {
                var s = new GameObject("Desk downlight"); s.transform.SetParent(recep, false); s.transform.position = new Vector3(DeskPos.x + dz, CeilH - .08f, DeskPos.z + .6f); s.transform.rotation = Quaternion.Euler(90f, 0, 0);
                var sl = s.AddComponent<Light>(); sl.type = LightType.Spot; sl.spotAngle = 95f; sl.innerSpotAngle = 40f; sl.color = new Color(1f, .96f, .90f); sl.intensity = 3.4f; sl.range = 5.6f; sl.lightmapBakeType = LightmapBakeType.Baked; sl.shadows = LightShadows.None;
            }
            foreach (var cx in new[] { 3.0f, 4.5f, 6.0f })
                foreach (var (zz, nm) in new[] { (Z0 + 1.1f, "South"), (Z1 - 1.1f, "North") })
                {
                    var day = new GameObject(nm + " daylight"); day.transform.SetParent(recep, false); day.transform.position = new Vector3(cx, 1.6f, zz); day.transform.rotation = Quaternion.Euler(0, nm == "South" ? 0f : 180f, 0);
                    var dl = day.AddComponent<Light>(); dl.type = LightType.Rectangle; dl.areaSize = new Vector2(.9f, 2.2f); dl.color = new Color(.86f, .92f, 1f); dl.intensity = 1.0f; dl.range = 6f; dl.lightmapBakeType = LightmapBakeType.Baked; dl.shadows = LightShadows.None;
                }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ art, dressing, people
        static void Art(Transform root, Dictionary<string, Material> mats)
        {
            var art = new GameObject("Wall art").transform; art.SetParent(root, false);
            Spawn("notice_board", art, new Vector3(WestX + .01f, 1.62f, 2.23f), 90f, mats, Mat("Print notice board", "FFFFFF", .5f, 0f, T("bulletin_board.png")));
            for (int i = 0; i < 3; i++) Spawn("frame_portrait", art, new Vector3(EastX - .01f, 1.70f, .52f + i * .55f), -90f, mats, Mat("Print portrait " + i, "FFFFFF", .55f, 0f, T("portrait_" + i + ".png")));
            for (int i = 0; i < 4; i++) Spawn("frame_portrait", art, new Vector3(WestX + .01f, 1.70f, -8.2f + i * .64f), 90f, mats, Mat("Print portrait " + (3 + i), "FFFFFF", .55f, 0f, T("portrait_" + (3 + i) + ".png")));
            Spawn("frame_landscape", art, new Vector3(EastX - .01f, 1.74f, 2.55f), -90f, mats, Mat("Print certificate 0", "FFFFFF", .55f, 0f, T("cert_0.png")));
            Spawn("frame_landscape", art, new Vector3(EastX - .01f, 1.74f, 3.75f), -90f, mats, Mat("Print certificate 1", "FFFFFF", .55f, 0f, T("cert_1.png")));
            Spawn("frame_landscape", art, new Vector3(EastX - .01f, 1.74f, 7.2f), -90f, mats, Mat("Print certificate 2", "FFFFFF", .55f, 0f, T("cert_2.png")));
            Spawn("award_plaque", art, new Vector3(WestX + .01f, 1.55f, 12.3f), 90f, mats, Mat("Print plaque 0", "FFFFFF", .5f, 0f, T("plaque_0.png")));
            Spawn("award_plaque", art, new Vector3(EastX - .01f, 1.55f, 12.3f), -90f, mats, Mat("Print plaque 1", "FFFFFF", .5f, 0f, T("plaque_1.png")));
            Spawn("frame_landscape", art, new Vector3(WestX + .01f, 1.74f, 12.3f + 1.2f), 90f, mats, Mat("Print certificate 3", "FFFFFF", .55f, 0f, T("cert_3.png")));
        }

        static void Dressing(Scene scene)
        {
            var trolley = Find(scene, "clinical_trolley", "Hospital room dressing"); if (trolley != null) MoveTo(trolley, 6.0f, 6.2f);
            var rub = Find(scene, "handrub", "Hospital room dressing"); if (rub != null) MoveTo(rub, null, 3.95f);                          // back beside the theatre door, where it was
            // absolute rotation: a relative turn rotated it again on every re-run until it faced into the wall
            var clock = Find(scene, "wall_clock", "Clinical bay details"); if (clock != null) { clock.rotation = Quaternion.Euler(0, -90f, 0); var cb = Bnd(clock.gameObject); clock.position += new Vector3(WestX + .03f - cb.min.x, 2.08f - cb.center.y, 11.2f - cb.center.z); }
        }

        static void People(Transform root)
        {
            var people = new GameObject("Staff").transform; people.SetParent(root, false);
            Staff(people, "nurse_f", new Vector3(5.50f, 0, 7.30f), -28f);
            Staff(people, "nurse_m", new Vector3(5.45f, 0, 8.35f), 208f);
            Staff(people, "receptionist", new Vector3(DeskPos.x, 0, DeskPos.z + .50f), DeskYaw);
        }

        static GameObject Staff(Transform parent, string key, Vector3 pos, float yaw)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Staff_" + key + ".prefab"); if (src == null) { Debug.LogWarning("[CorridorOverhaul] prefab missing " + key); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(parent, false); go.name = "Staff " + key; go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ corner plants
        const string PlantModels = "Assets/Models/Environment/CorridorPlants";

        /// <summary>Brings the four Blender plants (Logs/CorridorPlants) into the project; the foliage cards need double-sided leaf materials, which the reference palm materials already are.</summary>
        public static void ImportPlants()
        {
            if (!AssetDatabase.IsValidFolder(PlantModels)) AssetDatabase.CreateFolder("Assets/Models/Environment", "CorridorPlants");
            foreach (var n in new[] { "corr_palm_a", "corr_palm_b", "corr_ficus_a", "corr_ficus_b" })
            {
                var dst = PlantModels + "/" + n + ".fbx"; File.Copy(Path.GetFullPath("Logs/CorridorPlants/" + n + ".fbx"), Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(dst); mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.generateSecondaryUV = true; mi.SaveAndReimport();
            }
            Debug.Log("[CorridorOverhaul] plants imported");
        }

        static readonly Dictionary<string, string> PlantMats = new Dictionary<string, string>
        {
            ["M_PlanterGrey"] = "Planter brushed grey", ["M_Soil"] = "Planter soil", ["M_Leaf"] = "Palm leaf dark", ["M_LeafLight"] = "Palm leaf light", ["M_PalmFrond"] = "Palm leaf dark", ["M_Stem"] = "Plant stem", ["M_Terracotta"] = "Terracotta pot",
        };

        /// <summary>Tall palms (grey metal) and large ficus (terracotta) in the four corners: clear of the doors, the desk, the glass rail and the walking lane; each with a capsule collider.</summary>
        static void Plants(Transform root)
        {
            var plants = new GameObject("Corner plants").transform; plants.SetParent(root, false);
            foreach (var (id, pos, yaw) in new[] { ("corr_palm_a", new Vector3(2.98f, 0, Z0 + .52f), 20f), ("corr_ficus_a", new Vector3(6.02f, 0, Z0 + .52f), 140f), ("corr_ficus_b", new Vector3(2.98f, 0, Z1 - .58f), 75f), ("corr_palm_b", new Vector3(6.02f, 0, Z1 - .58f), 200f) })
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(PlantModels + "/" + id + ".fbx"); if (src == null) { Debug.LogWarning("[CorridorOverhaul] plant missing " + id); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(plants, false); go.name = id; go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var key = r.name.Substring(r.name.LastIndexOf("M_", StringComparison.Ordinal));
                    r.sharedMaterial = key == "M_PlanterGrey" ? Mat("Corridor planter grey", "A9AEB3", .42f, .35f) : AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/ReferenceProps/" + PlantMats[key] + ".mat");
                    r.receiveGI = ReceiveGI.LightProbes; r.shadowCastingMode = ShadowCastingMode.On; GameObjectUtility.SetStaticEditorFlags(go, 0);
                }
                var c = new GameObject("Plant collision"); c.transform.SetParent(go.transform, false); c.transform.position = pos + Vector3.up * 1.0f;
                var cc = c.AddComponent<CapsuleCollider>(); cc.radius = .3f; cc.height = 2f;
            }
        }

        static GameObject SpawnTv(Transform parent, Vector3 pos, float yaw, float scale)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/ReferenceProps/hospital_tv.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(parent, false); go.name = "reception_tv";
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
            var housing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/ReferenceProps/Wall TV housing.mat"); var alu = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/ReferenceProps/Sofa steel leg.mat");
            var screen = Emissive("Reception TV screen", T("tv_welcome.png"), 1.0f);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = r.name.EndsWith("M_TvScreen", StringComparison.Ordinal) ? screen : r.name.EndsWith("M_TvAlu", StringComparison.Ordinal) ? alu : housing;
                r.receiveGI = ReceiveGI.Lightmaps; GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI);
            }
            return go;
        }
    }
}
