using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    // Applies finishes to the existing shells; never regenerates gameplay scenes or equipment.
    public static class HospitalFinishes
    {
        private const string Folder = "Assets/Materials/Hospital";
        private const string Meshes = "Assets/Models/Environment";
        private const string RootName = "Hospital finishes";
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        public static string Capture(string name)
        {
            var scene = SceneManager.GetSceneByName(name);
            if (!scene.isLoaded) throw new InvalidOperationException("Open the requested scene first.");
            var original = SceneManager.GetActiveScene();
            var hidden = new List<GameObject>();
            foreach (var other in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
                if (other != scene) foreach (var root in other.GetRootGameObjects()) if (root.activeSelf) { hidden.Add(root); root.SetActive(false); }
            SceneManager.SetActiveScene(scene);
            var go = new GameObject("Temporary finish preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.GetComponent<Camera>(); camera.fieldOfView = 72; camera.nearClipPlane = .02f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("C9D8D8");
            camera.transform.position = name == "PatientBay" ? new Vector3(1.85f, 1.65f, -1.65f) : name == "Corridor" ? new Vector3(1.15f, 1.65f, -4.4f) : new Vector3(2.85f, 1.65f, -2.85f);
            camera.transform.LookAt(name == "PatientBay" ? new Vector3(-.4f, 1.35f, .65f) : name == "Corridor" ? new Vector3(0, 1.45f, 3) : new Vector3(-.5f, 1.35f, .5f));
            var target = RenderTexture.GetTemporary(1400, 900, 24);
            var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image = new Texture2D(1400, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0); image.Apply();
                string path = Path.GetFullPath("Temp/EnvironmentReview/" + name + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG()); return path;
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                if (image != null) UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(go);
                foreach (var root in hidden) root.SetActive(true); SceneManager.SetActiveScene(original);
            }
        }

        public static object Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or review existing unsaved scenes first.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(Meshes); AssetDatabase.Refresh();
            var results = new List<string>();
            foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                if (scene.GetRootGameObjects().Any(g => g.name == RootName))
                    throw new InvalidOperationException(name + " already has authored finishes; preserve them.");
                var shell = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
                var floor = shell.Single(r => r.name == "Floor");
                var walls = shell.Where(r => r.name.StartsWith("Wall ")).ToArray();
                if (walls.Length != 4) throw new InvalidOperationException("Expected four shell walls in " + name);
                var b = floor.bounds; float width = b.size.x, depth = b.size.z;
                float height = walls.Max(r => r.bounds.max.y);
                var root = new GameObject(RootName).transform;
                SceneManager.MoveGameObjectToScene(root.gameObject, scene);
                var paint = Material("Warm painted plaster", "EEEDE5", .08f, "PaintedPlaster017", new Vector2(3, 2), .12f);
                var wood = Material("Pale oak laminate", "F4F0DF", .22f, "Wood050", new Vector2(3, 1.4f), .18f);
                var trim = Material("Satin ivory trim", "E1E3D9", .22f);
                var skirt = Material("Charcoal skirting", "5E6A68", .14f);
                var ceiling = Material("White ceiling", "EFF1EB", .07f);
                var grid = Material("Ceiling joints", "A9B4B1", .1f);
                var fixture = Material("Light housing", "C7D0CF", .3f);
                var glow = Material("Diffused ceiling light", "F3F8F3", .1f);
                glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", Hex("D9EBE5") * 1.25f);
                floor.sharedMaterial = name == "OT" ? Material("OT seamless blue grey", "9DAFAE", .22f)
                    : Material(name + " wood effect flooring", "F7F4E8", .23f, "WoodFloor040", new Vector2(width / 1.9f, depth / 1.9f), .18f);
                foreach (var wall in walls)
                {
                    wall.sharedMaterial = paint;
                    var bounds = wall.bounds;
                    bool side = bounds.size.x < bounds.size.z;
                    var inward = (floor.bounds.center - bounds.center); inward.y = 0; inward.Normalize();
                    var center = bounds.center + inward * ((side ? bounds.size.x : bounds.size.z) * .5f + .012f);
                    center.y = .56f;
                    Box(root, "Lower oak wall panel", center, side ? new Vector3(.024f, 1.12f, bounds.size.z) : new Vector3(bounds.size.x, 1.12f, .024f), wood);
                    center += inward * .009f; center.y = 1.135f;
                    Box(root, "Panel cap rail", center, side ? new Vector3(.042f, .042f, bounds.size.z) : new Vector3(bounds.size.x, .042f, .042f), trim);
                    center.y = .06f;
                    Box(root, "Coved skirting", center, side ? new Vector3(.045f, .12f, bounds.size.z) : new Vector3(bounds.size.x, .12f, .045f), skirt);
                }
                Box(root, "Ceiling", new Vector3(0, height + .04f, 0), new Vector3(width, .08f, depth), ceiling, true);
                var joints = new List<CombineInstance>();
                for (float x = -width / 2 + .6f; x < width / 2; x += .6f)
                    AddBox(joints, new Vector3(x, height - .002f, 0), new Vector3(.008f, .005f, depth));
                for (float z = -depth / 2 + .6f; z < depth / 2; z += .6f)
                    AddBox(joints, new Vector3(0, height - .002f, z), new Vector3(width, .005f, .008f));
                Combined(root, name + " ceiling grid", joints, grid);
                var housings = new List<CombineInstance>(); var diffusers = new List<CombineInstance>();
                int rows = Mathf.Max(2, Mathf.RoundToInt(depth / 2.5f));
                for (int i = 0; i < rows; i++)
                {
                    float z = -depth / 2 + (i + .5f) * depth / rows;
                    int columns = width > 6 ? 2 : 1;
                    for (int j = 0; j < columns; j++)
                    {
                        float x = columns == 1 ? 0 : (j == 0 ? -1.6f : 1.6f);
                        AddBox(housings, new Vector3(x, height - .026f, z), new Vector3(.65f, .04f, 1.25f));
                        AddBox(diffusers, new Vector3(x, height - .05f, z), new Vector3(.58f, .012f, 1.18f));
                    }
                    var l = new GameObject("Ceiling fill " + i, typeof(Light)); l.transform.SetParent(root, false);
                    l.transform.position = new Vector3(0, height - .25f, z);
                    var light = l.GetComponent<Light>(); light.type = LightType.Point; light.range = Mathf.Max(width, 5);
                    light.intensity = name == "OT" ? 1.1f : .85f; light.color = Hex("EEF5EF"); light.shadows = LightShadows.None;
                }
                Combined(root, name + " luminaire frames", housings, fixture);
                Combined(root, name + " luminaire diffusers", diffusers, glow);
                if (name == "PatientBay") Window(root, trim, skirt);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                results.Add(name + ": retained shell, floor collider and equipment; installed finishes.");
            }
            AssetDatabase.SaveAssets(); return results;
        }

        private static Material Material(string name, string color, float smoothness, string texture = null, Vector2 tiling = default, float bump = .2f)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = Hex(color); m.SetFloat("_Smoothness", smoothness); m.enableInstancing = true;
            if (texture != null)
            {
                string prefix = "Assets/Textures/Hospital/" + texture + "_1K-JPG_";
                m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "Color.jpg");
                if (m.mainTexture == null) throw new InvalidOperationException("Missing imported texture: " + prefix);
                m.mainTextureScale = tiling;
                // Keep the scanned microtexture, without the scan's visible hairline cracks.
                if (texture == "PaintedPlaster017") m.mainTexture = null;
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "NormalGL.jpg");
                if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetTextureScale("_BumpMap", tiling); m.SetFloat("_BumpScale", texture == "PaintedPlaster017" ? .025f : bump); m.EnableKeyword("_NORMALMAP"); }
            }
            EditorUtility.SetDirty(m); return m;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true; return go;
        }

        private static Mesh CubeMesh
        {
            get
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube); var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(temp); return mesh;
            }
        }
        private static void AddBox(List<CombineInstance> list, Vector3 p, Vector3 s) => list.Add(new CombineInstance { mesh = CubeMesh, transform = Matrix4x4.TRS(p, Quaternion.identity, s) });
        private static void Combined(Transform parent, string name, List<CombineInstance> pieces, Material material)
        {
            var mesh = new Mesh { name = name }; mesh.CombineMeshes(pieces.ToArray());
            AssetDatabase.CreateAsset(mesh, Meshes + "/" + name + ".asset");
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = material; go.isStatic = true;
        }

        private static void Window(Transform parent, Material trim, Material dark)
        {
            var root = new GameObject("Faux daylight window with curtains").transform; root.SetParent(parent, false);
            root.localPosition = new Vector3(-2.405f, 1.92f, 0); root.localRotation = Quaternion.Euler(0, 90, 0);
            var glass = Material("Soft daylight glazing", "B9D7DF", .15f); glass.EnableKeyword("_EMISSION"); glass.SetColor("_EmissionColor", Hex("B0CAD0") * .45f);
            Box(root, "Window recess", Vector3.zero, new Vector3(2.1f, 1.24f, .045f), dark);
            Box(root, "Frosted daylight backing", new Vector3(0, 0, .035f), new Vector3(1.94f, 1.08f, .02f), glass);
            foreach (float x in new[] { -1.03f, 0, 1.03f }) Box(root, "Vertical window frame", new Vector3(x, 0, .065f), new Vector3(.055f, 1.25f, .065f), trim);
            foreach (float y in new[] { -.61f, .61f }) Box(root, "Horizontal window frame", new Vector3(0, y, .065f), new Vector3(2.12f, .055f, .065f), trim);
            Box(root, "Window sill", new Vector3(0, -.65f, .10f), new Vector3(2.22f, .065f, .21f), trim);
            Box(root, "Curtain pelmet", new Vector3(0, .76f, .16f), new Vector3(2.6f, .12f, .23f), trim);
            var cloth = Material("Sage curtain fabric", "83A59B", .02f);
            cloth.SetFloat("_Cull", 0);
            for (int side = -1; side <= 1; side += 2)
            {
                int columns = 48, rows = 12; var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
                for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns, v = y / (float)rows;
                    float spread = Mathf.Lerp(.61f, .53f, Mathf.Sin(v * Mathf.PI));
                    vertices.Add(new Vector3(side * (.79f + (u - .5f) * spread), .67f - v * 1.48f, .30f + Mathf.Sin(u * Mathf.PI * 14) * .045f));
                    uv.Add(new Vector2(u, v));
                    if (x < columns && y < rows)
                    {
                        int a = y * (columns + 1) + x, b = a + columns + 1;
                        triangles.AddRange(side < 0 ? new[] { a, a + 1, b, a + 1, b + 1, b } : new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
                var mesh = new Mesh { name = "Gathered curtain " + side }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Meshes + "/Gathered curtain " + side + ".asset");
                var go = new GameObject("Gathered sage curtain", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = cloth;
            }
        }
    }
}
