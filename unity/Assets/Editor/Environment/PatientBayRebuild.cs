using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Imports the rebuilt PatientBay props (Blender output in Logs/BayRebuild, see Tools/Environment/build_bay_props.py) over the
    /// shipped FBX files, keeping each FBX's GUID and node names so prefabs, scene instances, colliders and interactions keep
    /// working. Parts that did not exist before (new node names) get PatientBay materials and the same lightmap flags as their
    /// siblings. Re-runnable. Called by PatientBayLookUpgrade.RunAll before the connected rebake.
    /// </summary>
    public static class PatientBayRebuild
    {
        const string MatDir = "Assets/Materials/Hospital/PatientBayLook";

        /// <summary>Asset id -> (model path, new-node material keys).</summary>
        static readonly Dictionary<string, string> Models = new()
        {
            ["bay_headwall"] = "Assets/Models/Environment/Bedside/bay_headwall.fbx",
            ["bedside_cabinet"] = "Assets/Models/Environment/Bedside/bedside_cabinet.fbx",
            ["tablet_table"] = "Assets/Models/Environment/Bedside/tablet_table.fbx",
            ["sensor_equipment_table"] = "Assets/Models/Environment/Bedside/sensor_equipment_table.fbx",
            ["clinical_sink"] = "Assets/Models/Environment/ClinicalDetails/clinical_sink.fbx",
            ["visitor_chair"] = "Assets/Models/Environment/ClinicalDetails/visitor_chair.fbx",
            ["charting_counter"] = "Assets/Models/Environment/ClinicalDetails/charting_counter.fbx",
            ["wall_clock"] = "Assets/Models/Environment/ClinicalDetails/wall_clock.fbx",
            ["wall_bumper"] = "Assets/Models/Environment/ClinicalDetails/wall_bumper.fbx",
            ["sharps_container"] = "Assets/Models/Environment/ClinicalDetails/sharps_container.fbx",
            ["glove_dispenser"] = "Assets/Models/Environment/ClinicalDetails/glove_dispenser.fbx",
            ["towel_dispenser"] = "Assets/Models/Environment/ClinicalDetails/towel_dispenser.fbx",
            ["pedal_bin"] = "Assets/Models/Environment/RoomDressing/pedal_bin.fbx",
            ["handrub"] = "Assets/Models/Environment/RoomDressing/handrub.fbx",
            ["privacy_curtain"] = "Assets/Models/Environment/RoomDressing/privacy_curtain.fbx",
            ["vital_signs_monitor"] = "Assets/Models/Equipment/vital_signs_monitor.fbx",
        };

        /// <summary>Equipment comes from the repository's own Blender pipeline; its importer settings and material remaps stay as authored.</summary>
        static readonly HashSet<string> KeepImporter = new() { "vital_signs_monitor" };

        /// <summary>Models that carry their own clean lightmap UVs (second UV layer) instead of Unity's generated ones.</summary>
        static readonly HashSet<string> OwnLightmapUv = new() { "privacy_curtain" };

        static string Logs => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BayRebuild"));

        static Material M(string name, Color c, float smooth, float metal = 0f)
        {
            var path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.enableInstancing = true;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Material for a node suffix such as M_GasGreen, or null if the suffix is an existing one.</summary>
        static Material NewMaterial(string suffix) => suffix switch
        {
            "M_GasGreen" => M("PatientBay gas green", new Color(.05f, .45f, .13f), .55f),
            "M_GasYellow" => M("PatientBay gas yellow", new Color(.82f, .64f, .06f), .55f),
            "M_GasBlue" => M("PatientBay gas blue", new Color(.05f, .20f, .62f), .55f),
            "M_GasWhite" => M("PatientBay gas white", new Color(.86f, .87f, .85f), .5f),
            "M_AlarmRed" => M("PatientBay alarm red", new Color(.66f, .04f, .04f), .6f),
            "M_Chrome" => M("PatientBay chrome", new Color(.84f, .87f, .89f), .86f, 1f),
            "M_Brass" => M("PatientBay brass", new Color(.62f, .46f, .18f), .6f, .9f),
            "M_Teal" => M("PatientBay teal upholstery", new Color(.12f, .38f, .40f), .12f),
            "M_Laminate" => M("PatientBay laminate", new Color(.80f, .72f, .58f), .35f),
            "M_ScreenBlack" => M("PatientBay screen black", new Color(.01f, .012f, .014f), .8f),
            _ => null,
        };

        public static void ImportAll(IEnumerable<string> ids = null)
        {
            foreach (var id in ids ?? Models.Keys)
            {
                var src = KeepImporter.Contains(id) ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "blender", id + ".fbx")) : Path.Combine(Logs, id + ".fbx");
                if (!File.Exists(src)) { Debug.Log("[Rebuild] no build for " + id); continue; }
                var dst = Models[id];
                if (!File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)))) throw new InvalidOperationException("Refusing to create a new asset: " + dst);
                File.Copy(src, Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                if (KeepImporter.Contains(id)) { AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate); continue; }
                var importer = (ModelImporter)AssetImporter.GetAtPath(dst);
                importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false;
                importer.importLights = false; importer.importCameras = false; importer.isReadable = false;
                importer.generateSecondaryUV = !OwnLightmapUv.Contains(id); importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                FixPrefab(id);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>New nodes of the model get their PatientBay material and the sibling lightmap flags, in the prefab asset.</summary>
        static void FixPrefab(string id)
        {
            var guid = AssetDatabase.FindAssets(id + " t:Prefab", new[] { "Assets/Prefabs" }).FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == id);
            if (guid == null) { Debug.Log("[Rebuild] no prefab for " + id); return; }
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rs = root.GetComponentsInChildren<MeshRenderer>(true);
                var known = rs.FirstOrDefault(r => r.sharedMaterial != null && NewMaterialKey(r.name) == null);
                var n = 0;
                foreach (var r in rs)
                {
                    var cur = r.sharedMaterial;
                    var unset = cur == null || cur.name == "Lit" || cur.name == "Default-Material";
                    if (unset)                                         // a new node of an existing material family
                    {
                        var i = r.name.LastIndexOf("_M_", StringComparison.Ordinal);
                        var dm = i >= 0 ? DefaultFor(r.name.Substring(i + 1)) : null;
                        if (dm != null)
                        {
                            r.sharedMaterial = dm;
                            if (known != null) GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(known.gameObject));
                            r.receiveGI = known != null ? known.receiveGI : ReceiveGI.Lightmaps;
                            n++;
                            continue;
                        }
                    }
                    var key = NewMaterialKey(r.name);
                    if (key == null) continue;
                    r.sharedMaterial = NewMaterial(key);
                    if (known != null) GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(known.gameObject));
                    r.receiveGI = known != null ? known.receiveGI : ReceiveGI.Lightmaps;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    n++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Rebuild] {id}: prefab {path}, {rs.Length} renderers, {n} new-material nodes");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Serializable] class CurtainData { public float width, height; public float[] vertices, normals, uv, uv2; public int[] tris; }

        /// <summary>
        /// Rewrites the two window-curtain mesh assets (GUIDs kept) with the pleated cloth from Tools/Environment/curtain_gen.py,
        /// mapped into each mesh's own frame: width along the track, up along world up, the room-facing side toward +X.
        /// </summary>
        public static void ApplyWindowCurtains(UnityEngine.SceneManagement.Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var panels = all.Where(f => f.sharedMesh != null && f.sharedMesh.name.StartsWith("Gathered curtain")).OrderBy(f => f.transform.position.z).ToArray();
            for (var k = 0; k < panels.Length; k++)
            {
                var file = Path.Combine(Logs, k == 0 ? "window_curtain_a.json" : "window_curtain_b.json");
                if (!File.Exists(file)) { Debug.Log("[Rebuild] no curtain data " + file); continue; }
                var d = JsonUtility.FromJson<CurtainData>(File.ReadAllText(file));
                var t = panels[k].transform; var mesh = panels[k].sharedMesh; var c = mesh.bounds.center;
                var depth = t.InverseTransformDirection(Vector3.right).normalized;
                var up = t.InverseTransformDirection(Vector3.up).normalized;
                var wdt = Vector3.Cross(up, depth).normalized;
                var n = d.vertices.Length / 3;
                var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n]; var uv2 = new Vector2[n];
                for (var i = 0; i < n; i++)
                {
                    var p = new Vector3(d.vertices[i * 3], d.vertices[i * 3 + 1], d.vertices[i * 3 + 2]);
                    v[i] = c + (p.x - d.width / 2) * wdt + (p.y - d.height / 2) * up + p.z * depth;
                    nr[i] = (d.normals[i * 3] * wdt + d.normals[i * 3 + 1] * up + d.normals[i * 3 + 2] * depth).normalized;
                    uv[i] = new Vector2(d.uv[i * 2], d.uv[i * 2 + 1]); uv2[i] = new Vector2(d.uv2[i * 2], d.uv2[i * 2 + 1]);
                }
                mesh.Clear();
                mesh.indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(v); mesh.SetNormals(nr); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2);
                mesh.SetTriangles(d.tris, 0, false);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                EditorUtility.SetDirty(mesh);
                Debug.Log($"[Rebuild] curtain {panels[k].name}: {n} verts, {d.tris.Length / 3} tris, bounds {mesh.bounds.size:F2}");
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Patient shading only (rig, sockets and clearance untouched): skin gets a sheen and pore-scale relief, the gown a woven
        /// printed fabric. Materials are the patient's own assets under Models/Characters/MPFBPatient.
        /// </summary>
        public static void ApplyPatientMaterials(Texture2D skinPores, Texture2D gownPrint, Texture2D clothDetail = null)
        {
            var dir = "Assets/Models/Characters/MPFBPatient/Materials/";
            var skin = AssetDatabase.LoadAssetAtPath<Material>(dir + "ProcSensePatient_body.mat");
            if (skin != null)
            {
                skin.SetFloat("_Smoothness", .42f); skin.SetFloat("_Metallic", 0f);
                skin.SetTexture("_DetailNormalMap", skinPores); skin.SetTextureScale("_DetailNormalMap", new Vector2(34f, 34f));
                skin.SetFloat("_DetailNormalMapScale", .7f); skin.EnableKeyword("_DETAIL_MULX2");
                EditorUtility.SetDirty(skin);
            }
            var gown = AssetDatabase.LoadAssetAtPath<Material>(dir + "PatientGown_Blue.mat");
            if (gown != null)
            {
                var weave = clothDetail != null ? clothDetail : AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Hospital/Bed/bed_fabric_weave_normal.png");
                gown.SetTexture("_BaseMap", gownPrint); gown.SetTextureScale("_BaseMap", new Vector2(6f, 6f)); gown.SetColor("_BaseColor", Color.white);
                gown.SetFloat("_Smoothness", .12f); gown.SetFloat("_Metallic", 0f);
                if (weave != null)
                {
                    gown.SetTexture("_DetailNormalMap", weave); gown.SetTextureScale("_DetailNormalMap", new Vector2(clothDetail != null ? 9f : 60f, clothDetail != null ? 9f : 60f));
                    gown.SetFloat("_DetailNormalMapScale", .8f); gown.EnableKeyword("_DETAIL_MULX2");
                }
                EditorUtility.SetDirty(gown);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Rebuild] patient materials: skin={(skin != null)} gown={(gown != null)}");
        }

        /// <summary>
        /// PatientBay-only visual polish on shared materials (copies, so Corridor and OT are untouched): lighter, softer teleport pads,
        /// a lighter ceiling grid, and a warm veneer door leaf with the room's other finishes.
        /// </summary>
        public static void ApplyScenePolish(UnityEngine.SceneManagement.Scene scene)
        {
            var pad = M("PatientBay teleport pad", new Color(.40f, .56f, .80f), .55f);
            var grid = M("PatientBay ceiling grid", new Color(.84f, .86f, .84f), .10f);
            var door = M("PatientBay door veneer", new Color(.74f, .54f, .33f), .38f);
            var n = 0;
            foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                var ms = r.sharedMaterials; var changed = false;
                for (var i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    if (ms[i].name == "Mat_295C94") { ms[i] = pad; changed = true; }
                    else if (ms[i].name == "Ceiling joints") { ms[i] = grid; changed = true; }
                    else if (r.name == "Door leaf" && ms[i].name == "Sage clinical door") { ms[i] = door; changed = true; }
                }
                if (changed) { r.sharedMaterials = ms; n++; }
            }
            Debug.Log($"[Rebuild] scene polish: {n} renderers");
        }

        /// <summary>Applies the scene polish alone (no rebake) and saves the scene.</summary>
        public static void PolishOnly()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            ApplyScenePolish(scene);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Photographic window exterior (Poly Haven "Stone Pines", CC0): a distant spherical patch centred near the window, so the view
        /// has correct perspective and parallax through the opening. Unlit, not baked, never shadowed. The old emissive plane is retired.
        /// </summary>
        public static void ApplyExterior(UnityEngine.SceneManagement.Scene scene, Texture2D photo)
        {
            var old = GameObject.Find("Window exterior backdrop");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Frosted daylight backing"))
                if (t.TryGetComponent<MeshRenderer>(out var r)) r.enabled = false;
            // a real opening in the window wall: the solid wall keeps only its collision, four painted pieces frame the opening
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var old2 in all.Where(t => t != null && t.name.StartsWith("Wall left piece")).ToArray()) UnityEngine.Object.DestroyImmediate(old2.gameObject);
            var wall = all.FirstOrDefault(t => t != null && t.name == "Wall left" && t.GetComponent<MeshRenderer>() != null);
            var paint = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PatientBay wall paint.mat");
            if (wall != null)
            {
                wall.GetComponent<MeshRenderer>().enabled = false;
                var pieces = new (string n, Vector3 pc, Vector3 psz)[]
                {
                    ("Wall left piece front", new Vector3(-2.5f, 1.40f, -1.51f), new Vector3(.10f, 2.80f, .98f)),
                    ("Wall left piece back", new Vector3(-2.5f, 1.40f, 1.51f), new Vector3(.10f, 2.80f, .98f)),
                    ("Wall left piece below", new Vector3(-2.5f, .65f, 0f), new Vector3(.10f, 1.30f, 2.04f)),
                    ("Wall left piece above", new Vector3(-2.5f, 2.67f, 0f), new Vector3(.10f, .26f, 2.04f)),
                };
                foreach (var (n, pc, psz) in pieces)
                {
                    var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
                    piece.name = n; piece.transform.SetParent(wall.parent, true);
                    piece.transform.position = pc; piece.transform.rotation = Quaternion.identity; piece.transform.localScale = psz;
                    var pr = piece.GetComponent<MeshRenderer>();
                    pr.sharedMaterial = paint != null ? paint : wall.GetComponent<MeshRenderer>().sharedMaterial;
                    GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                    pr.receiveGI = ReceiveGI.Lightmaps; pr.shadowCastingMode = ShadowCastingMode.On;
                }
            }
            foreach (var t in all.Where(t => t != null && t.name == "Window recess"))
                if (t.TryGetComponent<MeshRenderer>(out var rec)) rec.enabled = false;
            var path = "Assets/Models/Environment/PatientBay exterior backdrop.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "PatientBay exterior backdrop" }; AssetDatabase.CreateAsset(mesh, path); }
            const int nu = 48, nv = 28; const float radius = 22f, az0 = -55f, az1 = 55f, el0 = -20f, el1 = 45f;
            var centre = new Vector3(-1.0f, 1.7f, 0f);
            var v = new Vector3[(nu + 1) * (nv + 1)]; var uv = new Vector2[v.Length];
            for (var j = 0; j <= nv; j++)
                for (var i = 0; i <= nu; i++)
                {
                    var u = i / (float)nu; var w = j / (float)nv;
                    var az = Mathf.Deg2Rad * Mathf.Lerp(az0, az1, u); var el = Mathf.Deg2Rad * Mathf.Lerp(el0, el1, w);
                    v[j * (nu + 1) + i] = centre + radius * new Vector3(-Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el));
                    uv[j * (nu + 1) + i] = new Vector2(u, w);
                }
            var tri = new List<int>();
            for (var j = 0; j < nv; j++)
                for (var i = 0; i < nu; i++)
                {
                    var a = j * (nu + 1) + i; var b = a + 1; var c = a + nu + 2; var d = a + nu + 1;
                    tri.AddRange(new[] { a, d, c, a, c, b });
                }
            mesh.Clear(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds(); mesh.RecalculateNormals();
            EditorUtility.SetDirty(mesh);
            var mat = M("PatientBay window exterior view", Color.white, 0f);
            mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat.SetTexture("_BaseMap", photo); mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(mat);
            var go = new GameObject("Window exterior backdrop", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Debug.Log($"[Rebuild] exterior backdrop: {v.Length} verts, radius {radius} m");
        }

        [Serializable] class Dummy { }

        /// <summary>
        /// Extends the patient's gown skirt to just below the knee. The hem loop is found from the mesh boundary, then K new rings are
        /// grown along each leg's own bone path (hip, knee, ankle, taken from the skin bind poses) with a slight flare, UVs continued,
        /// and bone weights blended thigh -> calf so the cloth bends with the knee. The existing vertices are untouched. Run once:
        /// a flag file records it (delete Logs/LookUpgrade/gown_extended.txt after restoring the original asset to run it again).
        /// </summary>
        public static void ExtendGown()
        {
            var flag = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "LookUpgrade", "gown_extended.txt"));
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var smr = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true)).FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.name == "patient_gown_idle");
            if (smr == null) { Debug.Log("[Gown] patient_gown_idle renderer not found"); return; }
            if (File.Exists(flag)) { Debug.Log("[Gown] already extended"); return; }
            var mesh = smr.sharedMesh;
            var v = mesh.vertices.ToList(); var nrm = mesh.normals.ToList(); var uv = mesh.uv.ToList(); var bw = mesh.boneWeights.ToList();
            var tris = mesh.triangles.ToList(); var tan = mesh.tangents;
            var bones = smr.bones; var bind = mesh.bindposes;
            Vector3 BindPos(string name)
            {
                for (var i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].name == name) return bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
                throw new InvalidOperationException("bone " + name);
            }
            int BoneIndex(string name) { for (var i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].name == name) return i; throw new InvalidOperationException("bone " + name); }
            var hipL = BindPos("thigh_l"); var kneeL = BindPos("calf_l"); var ankL = BindPos("foot_l");
            var hipR = BindPos("thigh_r"); var kneeR = BindPos("calf_r"); var ankR = BindPos("foot_r");
            Debug.Log($"[Gown] bind hipL {hipL:F3} kneeL {kneeL:F3} ankL {ankL:F3} hipR {hipR:F3}  verts {v.Count} tris {tris.Count / 3}");
            // boundary edges -> loops
            var count = new Dictionary<(int, int), int>(); var dir = new Dictionary<(int, int), (int a, int b)>();
            for (var t = 0; t < tris.Count; t += 3)
                for (var e = 0; e < 3; e++)
                {
                    int a = tris[t + e], b = tris[t + (e + 1) % 3]; var key = a < b ? (a, b) : (b, a);
                    count[key] = count.TryGetValue(key, out var c) ? c + 1 : 1; dir[key] = (a, b);
                }
            var next = new Dictionary<int, int>();
            foreach (var kv in count.Where(k => k.Value == 1)) next[dir[kv.Key].a] = dir[kv.Key].b;
            var seen = new HashSet<int>(); var loops = new List<List<int>>();
            foreach (var start in next.Keys)
            {
                if (seen.Contains(start)) continue;
                var loop = new List<int>(); var cur = start;
                while (!seen.Contains(cur) && next.ContainsKey(cur)) { seen.Add(cur); loop.Add(cur); cur = next[cur]; }
                if (loop.Count > 8) loops.Add(loop);
            }
            var legAxis = ((ankL - hipL) + (ankR - hipR)).normalized;
            var hemLoop = loops.OrderByDescending(l => l.Select(i => Vector3.Dot(v[i], legAxis)).Average()).First();
            foreach (var l in loops) Debug.Log($"[Gown] boundary loop {l.Count} verts, mean along leg {l.Select(i => Vector3.Dot(v[i], legAxis)).Average():F3}");
            var midHip = (hipL + hipR) / 2;
            const int K = 9; const float extra = .30f;
            var sideAxis = (hipL - hipR).normalized;
            var ringStart = new List<int>();
            float PathParam(Vector3 p, Vector3 hip, Vector3 knee, Vector3 ank, out Vector3 onPath, float s)
            {
                // position on hip->knee->ankle after travelling s metres from the hip; returns the path tangent length used
                var l1 = (knee - hip).magnitude; var l2 = (ank - knee).magnitude;
                onPath = s <= l1 ? hip + (knee - hip).normalized * s : knee + (ank - knee).normalized * Mathf.Min(s - l1, l2);
                return l1;
            }
            var ringIdx = new int[K + 1][]; ringIdx[0] = hemLoop.ToArray();
            var loopCount = hemLoop.Count;
            for (var k = 1; k <= K; k++)
            {
                ringIdx[k] = new int[loopCount];
                for (var i = 0; i < loopCount; i++)
                {
                    var h = hemLoop[i]; var p = v[h];
                    var left = Vector3.Dot(p - midHip, sideAxis) >= 0;
                    Vector3 hip = left ? hipL : hipR, knee = left ? kneeL : kneeR, ank = left ? ankL : ankR;
                    var l1 = (knee - hip).magnitude;
                    var s0 = Mathf.Clamp(Vector3.Dot(p - hip, (knee - hip).normalized), 0f, l1);
                    PathParam(p, hip, knee, ank, out var p0, s0);
                    var off = p - p0;
                    var s = s0 + extra * k / K;
                    PathParam(p, hip, knee, ank, out var pk, s);
                    var np = pk + off * (1f + .018f * k);                                    // a slight flare
                    var w = Mathf.Clamp01((s - (l1 - .07f)) / .16f);                       // 0 = thigh, 1 = calf
                    var src = bw[h];
                    var thighIdx = BoneIndex(left ? "thigh_l" : "thigh_r"); var calfIdx = BoneIndex(left ? "calf_l" : "calf_r");
                    ringIdx[k][i] = v.Count;
                    v.Add(np); nrm.Add(nrm[h]); uv.Add(uv[h] + new Vector2(0f, -.045f * k));
                    bw.Add(new BoneWeight { boneIndex0 = thighIdx, weight0 = 1f - w, boneIndex1 = calfIdx, weight1 = w });
                }
            }
            for (var k = 1; k <= K; k++)
                for (var i = 0; i < loopCount; i++)
                {
                    var j = (i + 1) % loopCount;
                    int a = ringIdx[k - 1][i], b = ringIdx[k - 1][j], a2 = ringIdx[k][i], b2 = ringIdx[k][j];
                    tris.AddRange(new[] { b, a, a2, b, a2, b2 });                                // continues the surface with the same orientation
                }
            mesh.vertices = v.ToArray(); mesh.normals = nrm.ToArray(); mesh.uv = uv.ToArray(); mesh.boneWeights = bw.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            File.WriteAllText(flag, "extended " + DateTime.UtcNow.ToString("o"));
            Debug.Log($"[Gown] extended: +{K * loopCount} verts, +{K * loopCount * 2} tris (hem loop {loopCount})");
        }

        static Material Existing(string name)
        {
            var guid = AssetDatabase.FindAssets(name + " t:Material").FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
        }

        /// <summary>Project material for a node-name suffix (M_PlasticGrey ...), used for newly added parts of an existing material family.</summary>
        static Material DefaultFor(string suffix) => suffix switch
        {
            "M_Ivory" => Existing("Clean ivory housing"),
            "M_Sage" => Existing("Sage clinical door"),
            "M_Oak" => Existing("Pale oak laminate"),
            "M_PlasticWhite" => Existing("Satin ivory trim"),
            "M_PlasticGrey" => Existing("Recesses and tablet bezel"),
            "M_SteelBrushed" => Existing("PatientBay satin steel") ?? Existing("Brushed stainless hardware"),
            "M_RubberBlack" => Existing("Castor rubber"),
            "M_Diffuser" => Existing("PatientBay ceiling diffuser"),
            "M_Curtain" => Existing("PatientBay curtain privacy"),
            "M_Upholstery" => Existing("Teal clinical upholstery"),
            "M_Amber" => Existing("Amber waste lid"),
            "M_ScreenOff" => Existing("Inactive display glass"),
            _ => null,
        };

        static string NewMaterialKey(string nodeName)
        {
            var i = nodeName.LastIndexOf("_M_", StringComparison.Ordinal);
            if (i < 0) return null;
            var suffix = nodeName.Substring(i + 1);
            return NewMaterial(suffix) != null ? suffix : null;
        }
    }
}
