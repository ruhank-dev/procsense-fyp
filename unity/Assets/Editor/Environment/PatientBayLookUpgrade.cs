using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// PatientBay-only lighting, reflections, materials and rendering overhaul. Re-runnable: it rewrites its own assets and
    /// objects (all under "Look upgrade" / "PatientBay look" names), then rebakes this one room (never several: the room
    /// shells overlap in world space) and recaptures its reflection probe. Corridor and OT scenes and their shared
    /// materials are never touched; the shared URP asset only gains reflection-probe box projection.
    /// Textures come from Tools/Environment/build_bay_look_textures.py (Logs/LookUpgrade).
    /// Batch: -executeMethod ProcSense.EditorTools.PatientBayLookUpgrade.Run -quit
    /// </summary>
    public static class PatientBayLookUpgrade
    {
        const string BayPath = "Assets/Scenes/PatientBay.unity";
        const string TexDir = "Assets/Textures/Hospital/PatientBayLook";
        const string MatDir = "Assets/Materials/Hospital/PatientBayLook";
        const string LightingPath = "Assets/Settings/HospitalLighting/PatientBay.lighting";
        const string ReflectionPath = "Assets/Settings/HospitalLighting/PatientBay reflection.exr";

        static string Logs => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "LookUpgrade"));
        static string Abs(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        static Color Lin(float r, float g, float b) => new Color(r, g, b);

        enum Kind { Colour, Normal, Data }

        /// <summary>Imports the rebuilt props, then applies the look upgrade and rebakes the connected rooms.</summary>
        public static void RunAll()
        {
            Directory.CreateDirectory(Abs(MatDir));
            PatientBayRebuild.ImportAll();
            Run();
        }

        /// <summary>
        /// Baked sunlight through the window: a warm directional light that only reaches the room through the real opening, so the lit
        /// patch, the mullion and curtain shadows and the glossy reflections of it all come from the bake.
        /// </summary>
        static void AddSun(UnityEngine.SceneManagement.Scene scene)
        {
            var old = GameObject.Find("Sun through window");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var go = new GameObject("Sun through window", typeof(Light));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(36f, 78f, 0f);                 // from the window side, 36 degrees above the horizon
            var l = go.GetComponent<Light>();
            l.type = LightType.Directional; l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft;
            l.color = Lin(1.0f, .93f, .83f); l.intensity = 1.0f;
        }

        /// <summary>Anisotropic filtering forced on for every quality level: oblique floors and printed fabric stay sharp.</summary>
        static void ForceAnisotropic()
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qs == null || qs.Length == 0) return;
            var so = new SerializedObject(qs[0]); var levels = so.FindProperty("m_QualitySettings");
            for (var i = 0; i < levels.arraySize; i++) levels.GetArrayElementAtIndex(i).FindPropertyRelative("anisotropicTextures").intValue = 2;
            so.ApplyModifiedProperties();
            Debug.Log("[LookUpgrade] anisotropic filtering forced on for " + levels.arraySize + " quality levels");
        }

        public static void Run()
        {
            ForceAnisotropic();
            var scene = EditorSceneManager.OpenScene(BayPath, OpenSceneMode.Single);
            PatientBayRebuild.ApplyWindowCurtains(scene);
            PatientBayRebuild.ApplyScenePolish(scene);
            AddSun(scene);
            Directory.CreateDirectory(Abs(TexDir));
            Directory.CreateDirectory(Abs(MatDir));
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();

            var vinylAlbedo = Tex("vinyl_floor_albedo.png", Kind.Colour, 1024);
            var vinylNormal = Tex("vinyl_floor_normal.png", Kind.Normal, 1024);
            var exterior = Tex("window_exterior.png", Kind.Colour, 1024);
            var curtainTeal = Tex("curtain_teal_albedo.png", Kind.Colour, 1024);
            var curtainTealN = Tex("curtain_teal_normal.png", Kind.Normal, 1024);
            var curtainPriv = Tex("curtain_privacy_albedo.png", Kind.Colour, 1024);
            var clothDetail = Tex("cloth_detail_normal.png", Kind.Normal, 1024);
            var curtainPrivN = Tex("curtain_privacy_normal.png", Kind.Normal, 1024);
            var tileAlbedo = Tex("ceiling_tile_albedo.png", Kind.Colour, 512);
            var tileNormal = Tex("ceiling_tile_normal.png", Kind.Normal, 512);
            var photo = Tex("window_exterior_photo.jpg", Kind.Colour, 2048);
            PatientBayRebuild.ApplyExterior(scene, photo);
            all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();     // the exterior step replaces objects
            var skinPores = Tex("skin_pores_normal.png", Kind.Normal, 512);
            var gownPrint = Tex("gown_print_albedo.png", Kind.Colour, 512);
            foreach (var old in new[] { "patientbay_window_blind_albedo.png", "patientbay_window_blind_emission.png", "patientbay_window_blind_normal.png" })
                AssetDatabase.DeleteAsset(TexDir + "/" + old);
            AssetDatabase.DeleteAsset(MatDir + "/PatientBay window blind.mat");
            var shadowTex = Tex("contact_shadow.png", Kind.Colour, 256, alpha: true);

            // ---- materials (PatientBay copies: the originals are shared with Corridor and OT)
            var floorMat = Mat("PatientBay vinyl floor", c =>
            {
                c.SetTexture("_BaseMap", vinylAlbedo); c.SetTextureScale("_BaseMap", new Vector2(4f, 3.2f));
                c.SetColor("_BaseColor", Color.white);
                c.SetTexture("_BumpMap", vinylNormal); c.SetTextureScale("_BumpMap", new Vector2(4f, 3.2f)); c.SetFloat("_BumpScale", .35f); c.EnableKeyword("_NORMALMAP");
                c.SetFloat("_Smoothness", .62f); c.SetFloat("_Metallic", 0f);
            });
            var blindMat = Mat("PatientBay window exterior", c =>
            {
                // the daylight view is a self-lit picture plane (baked emissive), so room light does not wash it out
                c.SetTexture("_BaseMap", exterior); c.SetColor("_BaseColor", new Color(.06f, .06f, .06f));
                c.SetFloat("_Smoothness", .6f); c.SetFloat("_Metallic", 0f);
                c.EnableKeyword("_EMISSION"); c.SetTexture("_EmissionMap", exterior); c.SetColor("_EmissionColor", Color.white * 1.0f);
                c.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            });
            var curtainMat = Mat("PatientBay curtain teal", c => Cloth(c, curtainTeal, curtainTealN, 1.6f, 3.8f, clothDetail));
            var privacyMat = Mat("PatientBay curtain privacy", c => Cloth(c, curtainPriv, curtainPrivN, 1.6f, 3.8f, clothDetail));
            var tileMat = Mat("PatientBay ceiling tile", c =>
            {
                c.SetTexture("_BaseMap", tileAlbedo); c.SetTextureScale("_BaseMap", new Vector2(8.33f, 6.67f)); c.SetColor("_BaseColor", Color.white);
                c.SetTexture("_BumpMap", tileNormal); c.SetTextureScale("_BumpMap", new Vector2(8.33f, 6.67f)); c.SetFloat("_BumpScale", .8f); c.EnableKeyword("_NORMALMAP");
                c.SetFloat("_Smoothness", .05f); c.SetFloat("_Metallic", 0f);
            });
            var plasterSrc = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/Warm painted plaster.mat");
            var paintMat = Mat("PatientBay wall paint", c =>
            {
                if (plasterSrc != null) c.CopyPropertiesFromMaterial(plasterSrc);
                c.SetColor("_BaseColor", new Color(.93f, .915f, .895f)); c.SetFloat("_Smoothness", .14f);
            });
            var panelMat = Mat("PatientBay ceiling diffuser", c =>
            {
                c.SetColor("_BaseColor", Lin(.60f, .62f, .60f)); c.SetFloat("_Smoothness", .1f);
                c.EnableKeyword("_EMISSION"); c.SetColor("_EmissionColor", Lin(4.6f, 4.5f, 4.2f));      // HDR: real fixtures are far brighter than white paper
                c.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;                          // the rect lights carry the baked light; the probe sees the bright panel
            });
            var discMat = Mat("PatientBay downlight disc", c =>
            {
                c.SetColor("_BaseColor", Lin(.70f, .70f, .68f)); c.SetFloat("_Smoothness", .2f);
                c.EnableKeyword("_EMISSION"); c.SetColor("_EmissionColor", Lin(4.8f, 4.6f, 4.1f));
                c.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
            var steelSrc = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/Brushed stainless hardware.mat");
            var steelMat = Mat("PatientBay satin steel", c =>
            {
                c.SetColor("_BaseColor", Lin(.66f, .70f, .71f)); c.SetFloat("_Metallic", .95f); c.SetFloat("_Smoothness", .62f);
            });
            var shadowMat = Mat("PatientBay contact shadow", c =>
            {
                c.SetTexture("_BaseMap", shadowTex); c.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f));
                c.SetFloat("_Surface", 1f); c.SetFloat("_Blend", 0f);
                c.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); c.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                c.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); c.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                c.SetFloat("_ZWrite", 0f); c.SetFloat("_Cull", 2f);
                c.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); c.SetOverrideTag("RenderType", "Transparent");
                c.renderQueue = (int)RenderQueue.Transparent; c.SetShaderPassEnabled("ShadowCaster", false);
            }, "Universal Render Pipeline/Unlit");

            // ---- assign to the PatientBay renderers only
            var floor = all.Single(t => t.name == "Floor" && t.GetComponent<MeshRenderer>() != null).GetComponent<MeshRenderer>();
            floor.sharedMaterial = floorMat;
            Assign(all, "Frosted daylight backing", blindMat);
            Swap(all, "Bay woven Sage curtain fabric", curtainMat);
            Swap(all, "Bay woven Sage privacy fabric", privacyMat);
            // gathered curtains stay lightmapped, as authored (probe-lighting them gave black and glowing folds)
            foreach (var r in all.Where(t => t != null).Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null && r.sharedMaterials.Any(m => m == curtainMat || m == privacyMat)))
            {
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.ContributeGI);
                r.receiveGI = ReceiveGI.Lightmaps;
            }
            Swap(all, "White ceiling", tileMat);
            Swap(all, "Warm painted plaster", paintMat);
            Assign(all, "PatientBay luminaire diffusers", panelMat);
            Assign(all, "EQ_bay_headwall_M_Diffuser", panelMat);
            if (steelSrc != null)
                foreach (var r in all.Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null))
                {
                    var ms = r.sharedMaterials;
                    var changed = false;
                    for (var i = 0; i < ms.Length; i++) if (ms[i] == steelSrc) { ms[i] = steelMat; changed = true; }
                    if (changed) r.sharedMaterials = ms;
                }

            // ---- light tuning (all baked): the earlier values clipped the window and left two hotspots
            var root = all.Single(t => t.name == "Hospital lighting");
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.name.StartsWith("Baked ceiling panel")) { l.intensity = 8.5f; l.color = Lin(1.0f, .945f, .895f); }
                else if (l.name == "Baked headwall reading light") { l.intensity = 1.0f; l.color = Lin(1.0f, .95f, .90f); }
                else if (l.name == "Baked window daylight") { l.intensity = 7.5f; l.color = Lin(.96f, .97f, 1f); }
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Lin(.30f, .31f, .32f); RenderSettings.ambientEquatorColor = Lin(.20f, .20f, .20f); RenderSettings.ambientGroundColor = Lin(.10f, .095f, .085f);

            PatientBayRebuild.ApplyPatientMaterials(skinPores, gownPrint, clothDetail);
            AddDownlights(root, discMat);
            AddContactShadows(all, shadowMat);
            DensifyProbes(root);
            var reflection = root.GetComponentInChildren<ReflectionProbe>(true);
            RenderSettings.reflectionIntensity = .9f; reflection.intensity = .9f;
            reflection.size = new Vector3(reflection.size.x, 3.1f, reflection.size.z);      // the volume must reach below the floor slab, or the floor never sees the probe
            reflection.resolution = 256; reflection.boxProjection = true; reflection.blendDistance = .2f;

            // ---- shared URP asset: only box projection (blending stays off: one probe per room, no overlap to blend)
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                var so = new SerializedObject(urp);
                so.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(urp);
            }

            ConfigureBake();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[LookUpgrade] scene prepared; baking");

            // The three rooms share ONE connected bake stored in PatientBay's LightingData (rooms sit at their world positions):
            // bake with Corridor and OT open additively and PatientBay active. Their scene files are not modified or saved.
            EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Additive);
            EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            Lightmapping.lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!Lightmapping.Bake()) throw new InvalidOperationException("Bake failed");
            if (!Lightmapping.BakeReflectionProbe(reflection, ReflectionPath)) throw new InvalidOperationException("Reflection capture failed");
            AssetDatabase.ImportAsset(ReflectionPath, ImportAssetOptions.ForceUpdate);
            reflection.bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;      // proven in review: the floor and every metal now reflect the room
            RenderSettings.customReflectionTexture = reflection.bakedTexture;
            RenderSettings.reflectionIntensity = 1.0f;
            reflection.enabled = false;                                                                      // its capture is kept; the volume itself no longer overrides
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LookUpgrade] done: lightmaps={LightmapSettings.lightmaps.Length} probes={LightmapSettings.lightProbes?.count} mode={LightmapSettings.lightmapsMode}");
        }

        /// <summary>
        /// Sets the curtain print tiling from each curtain mesh's real UV-to-metre scale (no rebake): one print tile covers about
        /// 0.35 m of cloth. Measures the UV span against the world extent along the axis the coordinate follows.
        /// </summary>
        public static void RetileCurtains()
        {
            var scene = EditorSceneManager.OpenScene(BayPath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var matName in new[] { "PatientBay curtain teal", "PatientBay curtain privacy" })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + matName + ".mat");
                float su = 0, sv = 0; var n = 0;
                foreach (var r in all.Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null && r.sharedMaterials.Contains(mat)))
                {
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    var uv = mesh.uv; var vs = mesh.vertices;
                    var (mu, au) = MetresPerUv(r.transform, vs, uv.Select(x => x.x).ToArray());
                    var (mv, av) = MetresPerUv(r.transform, vs, uv.Select(x => x.y).ToArray());
                    Debug.Log($"[LookUpgrade] curtain {r.name} ({mesh.name}): u spans {uv.Min(x => x.x):F2}..{uv.Max(x => x.x):F2} = {mu:F2} m/unit on axis {au}; v spans {uv.Min(x => x.y):F2}..{uv.Max(x => x.y):F2} = {mv:F2} m/unit on axis {av}");
                    su += mu; sv += mv; n++;
                }
                if (n == 0) continue;
                var tile = new Vector2(Mathf.Max(.2f, su / n / .35f), Mathf.Max(.2f, sv / n / .35f));
                mat.SetTextureScale("_BaseMap", tile); mat.SetTextureScale("_BumpMap", tile);
                EditorUtility.SetDirty(mat);
                Debug.Log($"[LookUpgrade] {matName}: tiling {tile}");
            }
            AssetDatabase.SaveAssets();
        }

        static (float metres, int axis) MetresPerUv(Transform t, Vector3[] verts, float[] coord)
        {
            var world = verts.Select(v => t.TransformPoint(v)).ToArray();
            var best = 0; var bestCorr = -1f;
            for (var a = 0; a < 3; a++)
            {
                var xs = world.Select(w => w[a]).ToArray();
                var mx = xs.Average(); var mc = coord.Average();
                var cov = 0f; var vx = 0f; var vc = 0f;
                for (var i = 0; i < xs.Length; i++) { cov += (xs[i] - mx) * (coord[i] - mc); vx += (xs[i] - mx) * (xs[i] - mx); vc += (coord[i] - mc) * (coord[i] - mc); }
                var corr = Mathf.Abs(cov) / Mathf.Sqrt(Mathf.Max(1e-9f, vx * vc));
                if (corr > bestCorr) { bestCorr = corr; best = a; }
            }
            var ext = world.Max(w => w[best]) - world.Min(w => w[best]);
            return (ext / Mathf.Max(1e-4f, coord.Max() - coord.Min()), best);
        }

        static void Cloth(Material c, Texture2D albedo, Texture2D normal, float tx, float ty, Texture2D detail = null)
        {
            if (detail != null)
            {
                c.SetTexture("_DetailNormalMap", detail); c.SetTextureScale("_DetailNormalMap", new Vector2(tx * 2.2f, ty * 2.2f));
                c.SetFloat("_DetailNormalMapScale", .85f); c.EnableKeyword("_DETAIL_MULX2");
            }
            c.SetTexture("_BaseMap", albedo); c.SetTextureScale("_BaseMap", new Vector2(tx, ty)); c.SetColor("_BaseColor", Color.white);
            c.SetTexture("_BumpMap", normal); c.SetTextureScale("_BumpMap", new Vector2(tx, ty)); c.SetFloat("_BumpScale", 1f); c.EnableKeyword("_NORMALMAP");
            c.SetFloat("_Smoothness", .12f); c.SetFloat("_Metallic", 0f);
        }

        /// <summary>Replaces every use of a shared material with a PatientBay-only one, in this scene only.</summary>
        static void Swap(Transform[] all, string materialName, Material to)
        {
            var n = 0;
            foreach (var r in all.Where(t => t != null).Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null))
            {
                var ms = r.sharedMaterials; var changed = false;
                for (var i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name == materialName) { ms[i] = to; changed = true; }
                if (changed) { r.sharedMaterials = ms; n++; }
            }
            Debug.Log($"[LookUpgrade] {materialName} -> {to.name}: {n} renderer(s)");
        }

        static void Assign(Transform[] all, string rendererName, Material m)
        {
            var found = 0;
            foreach (var t in all.Where(t => t.name == rendererName))
            {
                var r = t.GetComponent<MeshRenderer>();
                if (r == null) continue;
                r.sharedMaterial = m; found++;
            }
            Debug.Log($"[LookUpgrade] {rendererName} -> {m.name}: {found} renderer(s)");
        }

        static void AddDownlights(Transform root, Material disc)
        {
            var old = root.Find("Downlight discs");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Downlight discs").transform;
            group.SetParent(root, false);
            foreach (var x in new[] { -1.5f, 1.5f })
                foreach (var z in new[] { -1.3f, 0f, 1.3f })
                {
                    var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
                    d.name = $"Downlight disc {x:0.0},{z:0.0}";
                    d.transform.SetParent(group, false);
                    d.transform.position = new Vector3(x, 2.797f, z);
                    d.transform.localScale = new Vector3(.2f, .004f, .2f);
                    var r = d.GetComponent<MeshRenderer>();
                    r.sharedMaterial = disc; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    GameObjectUtility.SetStaticEditorFlags(d, StaticEditorFlags.ContributeGI);
                    r.receiveGI = ReceiveGI.LightProbes;
                }
        }

        const string ShadowMeshPath = "Assets/Models/Environment/PatientBay contact shadows.asset";

        /// <summary>
        /// Soft footprints under every wheeled or free-standing item plus a tight spot under each castor or foot (vertices that
        /// touch the floor). All quads are merged into ONE mesh and one renderer: a single draw call.
        /// </summary>
        /// <summary>
        /// Rebuilds only the merged contact-shadow mesh (runtime geometry, nothing is baked): the monitor's stand was rebuilt with a different
        /// leg radius and castor positions, so its old wheel spots no longer sat under the castors.
        /// </summary>
        public static void RefreshContactShadows()
        {
            var scene = EditorSceneManager.OpenScene(BayPath, OpenSceneMode.Single);
            var old = GameObject.Find("Contact shadows");
            var mat = old.GetComponent<MeshRenderer>().sharedMaterial;
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            AddContactShadows(all, mat);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        static void AddContactShadows(Transform[] all, Material shadow)
        {
            var old = GameObject.Find("Contact shadows");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var quads = new List<Matrix4x4>();
            var targets = new (string name, float pad)[]
            {
                ("hospital_bed", 1.06f), ("tablet_table", 1.25f), ("vital_signs_monitor", 1.35f), ("pedal_bin", 1.35f),
                ("visitor_chair", 1.25f), ("bedside_cabinet", 1.10f),
            };
            foreach (var (name, pad) in targets)
            {
                var t = all.FirstOrDefault(x => x != null && x.name == name && x.GetComponentsInChildren<MeshRenderer>().Length > 0);
                if (t == null) { Debug.Log("[LookUpgrade] contact shadow target not found: " + name); continue; }
                var rs = t.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.bounds.min.y < .3f).ToArray();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                quads.Add(Matrix4x4.TRS(new Vector3(b.center.x, .006f, b.center.z), Quaternion.Euler(90f, 0f, 0f), new Vector3(b.size.x * pad, b.size.z * pad, 1f)));
                var wheels = WheelSpots(t);
                foreach (var p in wheels) quads.Add(Matrix4x4.TRS(new Vector3(p.x, .007f, p.z), Quaternion.Euler(90f, 0f, 0f), new Vector3(.17f, .17f, 1f)));
                Debug.Log($"[LookUpgrade] contact shadow {name}: footprint={b.size.x:F2}x{b.size.z:F2} wheel spots={wheels.Count}");
            }
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ShadowMeshPath);
            if (mesh == null) { mesh = new Mesh { name = "PatientBay contact shadows" }; AssetDatabase.CreateAsset(mesh, ShadowMeshPath); }
            mesh.Clear();
            mesh.CombineMeshes(quads.Select(m => new CombineInstance { mesh = quad, transform = m }).ToArray(), true, true);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var go = new GameObject("Contact shadows", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = shadow; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Debug.Log($"[LookUpgrade] contact shadows merged: {quads.Count} quads, {mesh.triangles.Length / 3} tris, 1 draw call");
        }

        static List<Vector3> WheelSpots(Transform item)
        {
            var clusters = new Dictionary<(int, int), (Vector3 sum, int n)>();
            foreach (var r in item.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = r.transform.TransformPoint(v);
                    if (w.y > .035f) continue;                  // only castors and feet reach the floor
                    var key = (Mathf.RoundToInt(w.x / .2f), Mathf.RoundToInt(w.z / .2f));
                    clusters.TryGetValue(key, out var c);
                    clusters[key] = (c.sum + w, c.n + 1);
                }
            }
            return clusters.Values.Where(c => c.n >= 3).Select(c => c.sum / c.n).ToList();
        }

        static void DensifyProbes(Transform root)
        {
            var g = root.GetComponentInChildren<LightProbeGroup>(true);
            // the bed, equipment and patient are probe-lit: a 0.55 m lattice around the bed and its services
            var lattice = new List<Vector3>();
            foreach (var x in new[] { -1.1f, -.55f, 0f, .55f, 1.1f, 1.65f })
                foreach (var z in new[] { .1f, .65f, 1.2f, 1.75f })
                    foreach (var y in new[] { .35f, .95f, 1.55f })
                        lattice.Add(g.transform.InverseTransformPoint(new Vector3(x, y, z)));
            var keep = g.probePositions.Where(p => !lattice.Any(l => (l - p).sqrMagnitude < 1e-6f)).ToList();   // re-runnable
            keep.AddRange(lattice);
            g.probePositions = keep.ToArray();
            Debug.Log($"[LookUpgrade] light probes: {keep.Count}");
        }

        static void ConfigureBake()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            var so = new SerializedObject(settings);
            so.FindProperty("m_BakeResolution").floatValue = 64f;
            so.FindProperty("m_LightmapMaxSize").intValue = 2048;
            so.FindProperty("m_LightmapsBakeMode").intValue = 1;               // directional
            so.FindProperty("m_PVRDirectSampleCount").intValue = 64;
            so.FindProperty("m_PVRSampleCount").intValue = 512;
            so.FindProperty("m_PVREnvironmentSampleCount").intValue = 128;
            so.FindProperty("m_PVRBounces").intValue = 4;
            foreach (var n in new[] { "m_PVRDenoiserTypeDirect", "m_PVRDenoiserTypeIndirect", "m_PVRDenoiserTypeAO" })
                so.FindProperty(n).intValue = 2;                                // OpenImageDenoise (CPU baker)
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
        }

        static Material Mat(string name, Action<Material> setup, string shader = "Universal Render Pipeline/Lit")
        {
            var path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.enableInstancing = true;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Textures that tile across a surface (floor, curtain prints, ceiling tile) must repeat; the rest are single pictures.</summary>
        static bool Tiles(string name) => name.StartsWith("vinyl") || name.StartsWith("curtain") || name.StartsWith("ceiling_tile") || name.StartsWith("skin_") || name.StartsWith("gown_") || name.StartsWith("cloth_");

        /// <summary>Re-imports the tiling textures with Repeat wrap, without rebaking.</summary>
        public static void FixWrap()
        {
            foreach (var f in Directory.GetFiles(Abs(TexDir), "patientbay_*.png"))
            {
                var name = Path.GetFileName(f).Replace("patientbay_", "");
                var path = TexDir + "/" + Path.GetFileName(f);
                var t = (TextureImporter)AssetImporter.GetAtPath(path);
                var want = Tiles(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (t.wrapMode != want) { t.wrapMode = want; t.SaveAndReimport(); Debug.Log("[LookUpgrade] wrap " + want + ": " + path); }
            }
        }

        static Texture2D Tex(string logName, Kind kind, int max, bool alpha = false)
        {
            var path = TexDir + "/patientbay_" + logName;
            File.Copy(Path.Combine(Logs, logName), Abs(path), true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var t = (TextureImporter)AssetImporter.GetAtPath(path);
            t.textureType = kind == Kind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = kind != Kind.Data && kind != Kind.Normal;
            t.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            t.alphaIsTransparency = alpha;
            t.mipmapEnabled = true;
            t.wrapMode = Tiles(logName) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = logName.StartsWith("vinyl") ? 8 : 4;
            t.maxTextureSize = max;
            t.isReadable = false;
            t.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = max,
                format = kind == Kind.Normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
            });
            t.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
