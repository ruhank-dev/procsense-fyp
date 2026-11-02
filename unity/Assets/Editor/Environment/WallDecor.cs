using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (owner: no wall may look plain or empty): fills free wall space in PatientBay, Corridor and OT with framed hospital posters (Tools/Environment/make_wall_posters.py), clocks, a framed print,
    /// fire alarm call points and extinguishers, plants and a parked wheelchair (CC0 Poly Haven models imported by PolyHavenProps). Every candidate is tested against every existing renderer, so nothing overlaps
    /// existing furniture, doors, signs or prints. Walls and positions come from Logs/wall_dump.csv (MaterialAudit.WallDump). Idempotent: the "Wall decoration" root is rebuilt on each run. Static, lightmapped
    /// (needs the final bake); plants and the wheelchair are probe-lit.
    /// </summary>
    public static class WallDecor
    {
        const string RootName = "Wall decoration", PosterDir = "Assets/Textures/Hospital/Posters", Prefabs = "Assets/Prefabs/Environment/PolyHaven", Mats = "Assets/Materials/Hospital/Decor";

        internal struct Wall { public Vector3 face, along, normal; public float from, to; public Wall(Vector3 f, Vector3 a, Vector3 n, float s, float e) { face = f; along = a; normal = n; from = s; to = e; } }

        internal static Wall[] WallsFor(string scene)
        {
            switch (scene)
            {
                case "PatientBay":
                    return new[] { new Wall(new Vector3(0, 0, 1.95f), Vector3.right, Vector3.back, -2.2f, 2.2f), new Wall(new Vector3(0, 0, -1.95f), Vector3.right, Vector3.forward, -2.2f, 2.2f),
                                   new Wall(new Vector3(-2.45f, 0, 0), Vector3.forward, Vector3.right, -1.7f, 1.7f), new Wall(new Vector3(2.45f, 0, 0), Vector3.forward, Vector3.left, -1.7f, 1.7f) };
                case "OT":
                    return new[] { new Wall(new Vector3(0, 0, 8.65f), Vector3.right, Vector3.back, 6.9f, 13.1f), new Wall(new Vector3(0, 0, 1.75f), Vector3.right, Vector3.forward, 6.9f, 13.1f),
                                   new Wall(new Vector3(13.45f, 0, 0), Vector3.forward, Vector3.left, 2.1f, 8.3f), new Wall(new Vector3(6.55f, 0, 0), Vector3.forward, Vector3.right, 2.1f, 8.3f) };
                default:
                    return new[] { new Wall(new Vector3(2.55f, 0, 0), Vector3.forward, Vector3.right, -9.3f, 13.7f), new Wall(new Vector3(6.45f, 0, 0), Vector3.forward, Vector3.left, -9.3f, 13.7f) };
            }
        }

        static Material Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p); }
            return null;
        }

        static Material PosterMat(int n)
        {
            if (!AssetDatabase.IsValidFolder(Mats)) AssetDatabase.CreateFolder("Assets/Materials/Hospital", "Decor");
            var path = Mats + "/Poster " + n.ToString("00") + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            var tp = PosterDir + "/poster_" + n.ToString("00") + ".png"; AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear; ti.maxTextureSize = 1024;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 512, format = TextureImporterFormat.ASTC_6x6 }); ti.SaveAndReimport();
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .38f); m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m); return m;
        }

        static GameObject Prefab(string id) => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab");

        static void Static(GameObject go, bool lightmapped)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, lightmapped ? StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic : StaticEditorFlags.BatchingStatic);
                r.receiveGI = lightmapped ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        static GameObject Framed(Transform parent, int poster, Vector3 pos, Quaternion rot, float w, float h, Material frameMat)
        {
            var root = new GameObject("Poster " + poster.ToString("00")); root.transform.SetParent(parent, false); root.transform.SetPositionAndRotation(pos, rot);
            GameObject Part(string n, PrimitiveType t, Vector3 lp, Vector3 ls, Material m)
            {
                var g = GameObject.CreatePrimitive(t); g.name = n; g.transform.SetParent(root.transform, false); g.transform.localPosition = lp; g.transform.localScale = ls; g.GetComponent<MeshRenderer>().sharedMaterial = m; return g;
            }
            const float b = .035f, d = .028f;
            Part("Backing", PrimitiveType.Cube, new Vector3(0, 0, d / 2), new Vector3(w, h, d), frameMat);
            Part("Mat board", PrimitiveType.Cube, new Vector3(0, 0, d / 2 - .006f), new Vector3(w - 2 * b, h - 2 * b, d), Find("Frame mat") ?? frameMat);
            var q = Part("Print", PrimitiveType.Quad, new Vector3(0, 0, -.0095f), new Vector3(w - 2 * b - .035f, h - 2 * b - .035f, 1f), PosterMat(poster));
            Part("Frame top", PrimitiveType.Cube, new Vector3(0, h / 2 - b / 2, d / 2 - .004f), new Vector3(w, b, d + .008f), frameMat);
            Part("Frame bottom", PrimitiveType.Cube, new Vector3(0, -h / 2 + b / 2, d / 2 - .004f), new Vector3(w, b, d + .008f), frameMat);
            Part("Frame left", PrimitiveType.Cube, new Vector3(-w / 2 + b / 2, 0, d / 2 - .004f), new Vector3(b, h - 2 * b, d + .008f), frameMat);
            Part("Frame right", PrimitiveType.Cube, new Vector3(w / 2 - b / 2, 0, d / 2 - .004f), new Vector3(b, h - 2 * b, d + .008f), frameMat);
            Static(root, true); return root;
        }

        public static void Apply()
        {
            var order = new[] { 1, 6, 10, 2, 7, 5, 11, 3, 12, 4 };
            var framesM = new[] { Find("Print frame oak"), Find("Frame walnut"), Find("Brushed stainless hardware") };
            foreach (var sceneName in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName); if (old != null) Object.DestroyImmediate(old);
                var root = new GameObject(RootName); SceneManagerMove(root, scene);
                var obstacles = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).Where(r => r.enabled && !(r is ParticleSystemRenderer) && r.gameObject.activeInHierarchy
                    && !(r.name.StartsWith("Wall") || r.name.StartsWith("Floor") || r.name.StartsWith("Ceiling") || r.name.ToLower().Contains("contact shadow") || r.transform.root.name == "Wall lamps" || r.transform.root.name == "Sun shafts" || r.name.StartsWith("EQ_city") || r.name.StartsWith("Skyline") || r.name.StartsWith("City") || r.name.StartsWith("Lower") || r.name.StartsWith("Panel cap"))).Select(r => (r.bounds, r.name)).ToList();
                var placed = new List<Bounds>(); int count = 0, pi = sceneName == "OT" ? 8 : sceneName == "PatientBay" ? 4 : 0, wi = 0;

                var blockers = new Dictionary<string, int>();
                bool Free(Bounds b, float margin = .1f)
                {
                    b.Expand(margin);
                    foreach (var o in obstacles) if (o.Item1.Intersects(b)) { blockers.TryGetValue(o.Item2, out var n); blockers[o.Item2] = n + 1; return false; }
                    return !placed.Any(o => o.Intersects(b));
                }
                Bounds Box(Wall w, float t, float y, float width, float height, float depth)
                {
                    var c = w.face + w.along * t + Vector3.up * y + w.normal * (depth / 2 + .005f);
                    var size = new Vector3(Mathf.Abs(w.along.x) * width + Mathf.Abs(w.normal.x) * depth, height, Mathf.Abs(w.along.z) * width + Mathf.Abs(w.normal.z) * depth); return new Bounds(c, size);
                }

                foreach (var wall in WallsFor(sceneName))
                {
                    float t = wall.from + .3f + (wi++ % 3) * .25f; int k = 0; bool bigRoom = sceneName == "Corridor";
                    while (t < wall.to - .3f)
                    {
                        // cycle: poster, poster, clock, poster, framed print, poster ...
                        int kind = k % 6; float width = kind == 2 ? .34f : kind == 4 ? .78f : .5f, height = kind == 2 ? .34f : kind == 4 ? .52f : .71f, y = kind == 2 ? (bigRoom ? 2.55f : 2.3f) : bigRoom ? 1.9f : 1.8f;
                        var bb = Box(wall, t + width / 2, y, width, height, .06f);
                        if (!Free(bb)) { t += .25f; continue; }
                        var rot = Quaternion.LookRotation(-wall.normal); var at = wall.face + wall.along * (t + width / 2) + Vector3.up * y + wall.normal * .004f;
                        GameObject go;
                        if (kind == 2) { go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("wall_clock"), root.transform); go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(wall.normal)); go.transform.localScale = Vector3.one * 1.15f; Static(go, true); }
                        else if (kind == 4) { go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("hanging_picture_frame_02"), root.transform); go.transform.SetPositionAndRotation(at + Vector3.down * .02f, Quaternion.LookRotation(wall.normal)); go.transform.localScale = Vector3.one * 1.0f; Static(go, true); }
                        else { int pn = order[pi++ % order.Length]; go = Framed(root.transform, pn, at, rot, width, height, framesM[(count + wi) % framesM.Length] ?? framesM[0]); }
                        placed.Add(bb); count++; k++; t += width + .75f + (count % 3) * .25f;
                    }
                }

                // fire safety and greenery on the floor / lower wall where free
                int fx = 0;
                foreach (var wall in WallsFor(sceneName))
                {
                    for (float t = wall.from + .8f; t < wall.to - .6f; t += sceneName == "Corridor" ? 5.5f : 3.2f, fx++)
                    {
                        string id = fx % 4 == 0 ? "potted_plant_02" : fx % 4 == 1 ? "korean_fire_extinguisher_01" : fx % 4 == 2 ? "potted_plant_01" : "wheelchair_01";
                        if (sceneName != "Corridor" && id == "wheelchair_01") id = "potted_plant_02";
                        if (sceneName == "OT" && id.StartsWith("potted")) continue;                              // a theatre carries no plants
                        float depth = id.StartsWith("potted") ? .8f : id == "wheelchair_01" ? 1.1f : .3f, width = id == "wheelchair_01" ? .8f : id.StartsWith("potted") ? .8f : .3f, height = id == "wheelchair_01" ? 1.1f : id == "potted_plant_01" ? 1.35f : id.StartsWith("potted") ? .85f : .7f;
                        var along = wall.along; var c = wall.face + along * t + wall.normal * (depth / 2 + .02f) + Vector3.up * height / 2;
                        var sz = new Vector3(Mathf.Abs(along.x) * (id == "wheelchair_01" ? depth : width) + Mathf.Abs(wall.normal.x) * (id == "wheelchair_01" ? width : depth), height, Mathf.Abs(along.z) * (id == "wheelchair_01" ? depth : width) + Mathf.Abs(wall.normal.z) * (id == "wheelchair_01" ? width : depth));
                        var bb = new Bounds(c, sz); if (!Free(bb, .04f)) continue;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(id), root.transform);
                        var yaw = id == "wheelchair_01" ? Quaternion.LookRotation(along) : Quaternion.LookRotation(wall.normal);
                        go.transform.SetPositionAndRotation(wall.face + wall.normal * (id == "wheelchair_01" ? .62f : id.StartsWith("potted") ? .42f : .2f) + along * t, yaw * (id.StartsWith("potted") ? Quaternion.Euler(0, (fx * 73) % 360, 0) : Quaternion.identity));
                        Static(go, false); placed.Add(bb); count++;
                        if (id.StartsWith("korean"))                                                                  // extinguisher pair: call point above it
                        {
                            var al = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("fire_alarm"), root.transform);
                            al.transform.SetPositionAndRotation(wall.face + along * (t + .45f) + Vector3.up * 1.35f + wall.normal * .004f, Quaternion.LookRotation(wall.normal)); al.transform.localScale = Vector3.one * 1.6f; Static(al, true);
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"[WallDecor] {sceneName}: {count} items placed; top blockers: " + string.Join(", ", blockers.OrderByDescending(k => k.Value).Take(10).Select(k => k.Key + "x" + k.Value)));
            }
            AssetDatabase.SaveAssets(); Debug.Log("[WallDecor] applied");
        }

        static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene) { UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); }
    }
}
