using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Phase 1 of the final-look overhaul (owner's neutral blue-grey reference): grey seamless vinyl, white upper walls, muted blue-grey lower panels, blue curtains, a wider window
    /// with a headrail and horizontal blinds. Every step sets absolute values, so re-running never compounds. Only PatientBay is edited and saved; bake separately with the
    /// connected three-room procedure.
    /// </summary>
    public static class FinalLookRoom
    {
        const string Mat = "Assets/Materials/Hospital/PatientBayLook/";
        const float HalfW = 1.5f;                                 // half width of the window opening (m), was 1.02

        static Color Hex(string v) { ColorUtility.TryParseHtmlString("#" + v, out var c); return c; }
        static Material Load(string n) => AssetDatabase.LoadAssetAtPath<Material>(Mat + n + ".mat");

        /// <summary>Sets a cube-mesh renderer to the given world bounds (parents are unscaled in this scene).</summary>
        static void Fit(Transform t, Vector3 min, Vector3 max)
        {
            var p = t.parent ? t.parent.lossyScale : Vector3.one;
            t.position = (min + max) * .5f;
            var s = max - min; t.localScale = new Vector3(s.x / p.x, s.y / p.y, s.z / p.z);
        }

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();

            // ---- materials -------------------------------------------------------------------------------------------------------------------------------
            Tint("PatientBay wall paint", "F2F2EE", .06f, keepTexture: true);
            Tint("Bay sage washable panels", "A9B8CE", .16f, keepTexture: false);
            Tint("PatientBay curtain teal", "4C6E98", .02f, keepTexture: false);
            Tint("PatientBay curtain privacy", "5C7DA6", .02f, keepTexture: false);
            Tint("Sage clinical door", "9DB0C9", .2f, keepTexture: false);
            var vinyl = Vinyl();
            var floor = all.First(t => t.name == "Floor" && t.GetComponent<MeshRenderer>()).GetComponent<MeshRenderer>();
            floor.sharedMaterial = vinyl;

            // ---- window: wider opening ---------------------------------------------------------------------------------------------------------------------
            float h = HalfW, x0 = -2.55f, x1 = -2.45f;
            Fit(Wall(all, "Wall left piece above"), new Vector3(x0, 2.54f, -h), new Vector3(x1, 2.80f, h));
            Fit(Wall(all, "Wall left piece below"), new Vector3(x0, 0f, -h), new Vector3(x1, 1.30f, h));
            Fit(Wall(all, "Wall left piece front"), new Vector3(x0, 0f, -2f), new Vector3(x1, 2.80f, -h));
            Fit(Wall(all, "Wall left piece back"), new Vector3(x0, 0f, h), new Vector3(x1, 2.80f, 2f));
            var frame = all.Where(t => t.name.EndsWith("window frame") || t.name == "Window glazing bar").ToArray();
            var horiz = frame.Where(t => t.name == "Horizontal window frame").OrderBy(t => t.position.y).ToArray();
            Fit(horiz[0], new Vector3(-2.37f, 1.28f, -h - .06f), new Vector3(-2.31f, 1.34f, h + .06f));
            Fit(horiz[1], new Vector3(-2.37f, 2.50f, -h - .06f), new Vector3(-2.31f, 2.56f, h + .06f));
            var verts = frame.Where(t => t.name == "Vertical window frame").OrderBy(t => t.position.z).ToArray();      // front, centre, back
            Fit(verts[0], new Vector3(-2.37f, 1.30f, -h - .06f), new Vector3(-2.31f, 2.55f, -h));
            Fit(verts[2], new Vector3(-2.37f, 1.30f, h), new Vector3(-2.31f, 2.55f, h + .06f));
            var third = h / 1.5f;                                                                                       // two mullions make three panes
            Fit(verts[1], new Vector3(-2.37f, 1.30f, -third - .03f), new Vector3(-2.31f, 2.55f, -third + .03f));
            var second = all.FirstOrDefault(t => t.name == "Vertical window frame (2)");
            if (second == null) { second = Object.Instantiate(verts[1].gameObject, verts[1].parent).transform; second.name = "Vertical window frame (2)"; }
            Fit(second, new Vector3(-2.37f, 1.30f, third - .03f), new Vector3(-2.31f, 2.55f, third + .03f));
            foreach (var bar in all.Where(t => t.name == "Window glazing bar").ToArray()) Object.DestroyImmediate(bar.gameObject);   // blinds replace the transom
            Fit(all.First(t => t.name == "Window sill"), new Vector3(-2.41f, 1.24f, -h - .1f), new Vector3(-2.20f, 1.30f, h + .1f));
            all.First(t => t.name == "Curtain pelmet").gameObject.SetActive(false);
            var curtains = all.Where(t => t.name == "Gathered sage curtain").OrderBy(t => t.GetComponent<Renderer>().bounds.center.z).ToArray();
            if (curtains.Length == 2) { MoveTo(curtains[0], -h - .30f); MoveTo(curtains[1], h + .30f); }
            var glass = GameObject.Find("Window glass pane");
            if (glass != null) { glass.transform.position = new Vector3(-2.405f, 1.92f, 0); glass.transform.localScale = new Vector3(.004f, 1.22f, 2f * h + .04f); }
            var glare = GameObject.Find("Window glare"); if (glare != null) Object.DestroyImmediate(glare);

            // ---- horizontal blinds -------------------------------------------------------------------------------------------------------------------------
            Blinds(scene, h);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[FinalLookRoom] applied");
        }

        static Transform Wall(Transform[] all, string n) => all.First(t => t.name == n);

        static void MoveTo(Transform curtain, float centreZ)
        {
            var r = curtain.GetComponent<Renderer>(); var d = centreZ - r.bounds.center.z; curtain.position += new Vector3(0, 0, d);
        }

        static void Tint(string name, string hex, float smooth, bool keepTexture)
        {
            var m = Load(name); if (m == null) { var g = AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(q => Path.GetFileNameWithoutExtension(q) == name); if (g != null) m = AssetDatabase.LoadAssetAtPath<Material>(g); }
            if (m == null) { Debug.LogWarning("[FinalLookRoom] material not found: " + name); return; }
            m.SetColor("_BaseColor", Hex(hex)); m.color = Hex(hex); m.SetFloat("_Smoothness", smooth);
            if (!keepTexture) { m.SetTexture("_BaseMap", null); m.mainTexture = null; }
            EditorUtility.SetDirty(m);
        }

        static Material Vinyl()
        {
            var path = Mat + "PatientBay grey vinyl.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PatientBay grey vinyl" }; AssetDatabase.CreateAsset(m, path); }
            var old = Load("PatientBay vinyl floor");
            m.enableInstancing = true; m.SetColor("_BaseColor", Hex("A5ABB6")); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", .30f);
            if (old != null && old.GetTexture("_BumpMap") != null) { m.SetTexture("_BumpMap", old.GetTexture("_BumpMap")); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", .25f); m.SetTextureScale("_BumpMap", old.GetTextureScale("_BumpMap")); }
            EditorUtility.SetDirty(m); return m;
        }

        /// <summary>One mesh of thin slats hanging from a headrail across the top of the window; most of the window stays open, as in the reference.</summary>
        static void Blinds(UnityEngine.SceneManagement.Scene scene, float half)
        {
            var old = GameObject.Find("Window blinds"); if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("Window blinds", typeof(MeshFilter), typeof(MeshRenderer)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            float yTop = 2.50f, drop = .30f, pitch = .036f, depth = .034f, halfLen = half - .02f, x = -2.30f;
            int slats = Mathf.RoundToInt(drop / pitch);
            var v = new System.Collections.Generic.List<Vector3>(); var n = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var tri = new System.Collections.Generic.List<int>();
            for (int i = 0; i < slats; i++)
            {
                float y = yTop - .05f - i * pitch; float tilt = .012f;           // each slat is a thin tilted strip, 0.045 m deep, 6 mm thick
                Vector3 a = new Vector3(x - depth * .5f, y - tilt, -halfLen), b = new Vector3(x + depth * .5f, y + tilt, -halfLen), c = new Vector3(x + depth * .5f, y + tilt, halfLen), d = new Vector3(x - depth * .5f, y - tilt, halfLen);
                var up = Vector3.Cross(c - b, a - b).normalized; if (up.y < 0) up = -up; var th = up * .003f;
                int o = v.Count; v.AddRange(new[] { a + th, b + th, c + th, d + th, a - th, b - th, c - th, d - th });
                for (int k = 0; k < 4; k++) n.Add(up); for (int k = 0; k < 4; k++) n.Add(-up);
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                tri.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2, o + 4, o + 5, o + 6, o + 4, o + 6, o + 7 });
            }
            // headrail
            int r0 = v.Count; var rmin = new Vector3(x - .03f, yTop - .045f, -half); var rmax = new Vector3(x + .03f, yTop, half);
            Cube(v, n, uv, tri, rmin, rmax);
            var mesh = new Mesh { name = "Window blinds" }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var path = Mat + "PatientBay blind white.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PatientBay blind white" }; AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", Hex("F4F5F7")); m.SetFloat("_Smoothness", .25f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(.42f, .43f, .45f)); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; m.SetFloat("_Cull", 0f); m.enableInstancing = true; EditorUtility.SetDirty(m);
            go.GetComponent<MeshRenderer>().sharedMaterial = m; go.isStatic = true;
            Debug.Log($"[FinalLookRoom] blinds: {slats} slats, {tri.Count / 3} triangles");
        }

        static void Cube(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> n, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> tri, Vector3 a, Vector3 b)
        {
            var c = new[] { new Vector3(a.x, a.y, a.z), new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, b.z), new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z) };
            int[][] f = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 0, 1, 5, 4 }, new[] { 3, 7, 6, 2 } };
            foreach (var q in f)
            {
                int o = v.Count; var nn = Vector3.Cross(c[q[1]] - c[q[0]], c[q[2]] - c[q[0]]).normalized;
                foreach (var i in q) { v.Add(c[i]); n.Add(nn); }
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                tri.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
        }
    }
}
