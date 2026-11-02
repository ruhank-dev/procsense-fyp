using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Layout pass of the hospital overhaul for PatientBay (owner brief, part 2). Idempotent: every target is an absolute position, so running it twice gives the same room.
    /// South wall (opposite the bed) is cleared and centred on a 55-inch tilt-mounted television; the sink gets standing room (the sofa moves away from it); the pedal bin and the glove
    /// dispenser move out of the television's wall zone; the workstation shifts east; the palms shrink back from the new U-shaped privacy track; the three teleport pads lose their blue
    /// disc (the trigger collider, anchor and destination stay, so teleporting still works). Import() brings in the new television and U-track curtain models.
    /// </summary>
    public static class PatientBayLayout
    {
        const string Models = "Assets/Models/Environment/ReferenceProps";
        const string Mats = "Assets/Materials/Hospital/ReferenceProps";
        public const float WallSouth = -1.95f;         // inner face of the wall opposite the bed
        public const float TvCentreY = 1.70f;          // screen centre height
        const string RootName = "Reference room props";
        public const float TightGather = .93f, TightAccordion = .06f;   // privacy curtain parked: 7.75 m of fabric folded into ~0.54 m of track

        /// <summary>Parks the bed privacy curtain tightly at its gathered end (2 Nov 2026 request: it covered half the sofa and window view). Scene-only, no rebake (curtains are light-probe lit).</summary>
        public static void TightenPrivacyCurtain()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single); int n = 0;
            foreach (var cc in UnityEngine.Object.FindObjectsByType<ProcSense.VR.Views.CurtainCloth>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.UsesPath)) { cc.Open = 1f; cc.GatherFraction = TightGather; cc.Accordion = TightAccordion; EditorUtility.SetDirty(cc); n++; }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[PatientBayLayout] privacy curtain tightened: " + n);
        }

        static void Folder(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }

        public static void Import()
        {
            Folder(Models);
            var tv = Models + "/hospital_tv.fbx";
            File.Copy(Path.GetFullPath("Logs/HospitalTv/hospital_tv.fbx"), Path.GetFullPath(tv), true);
            AssetDatabase.ImportAsset(tv, ImportAssetOptions.ForceSynchronousImport);
            var imp = (ModelImporter)AssetImporter.GetAtPath(tv);
            imp.globalScale = 1; imp.useFileScale = true; imp.importAnimation = false; imp.importCameras = false; imp.importLights = false; imp.isReadable = false;
            imp.generateSecondaryUV = true; imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.SaveAndReimport();
            var cur = "Assets/Models/Environment/RoomDressing/privacy_curtain.fbx";
            File.Copy(Path.GetFullPath("Logs/BayRebuild/privacy_curtain.fbx"), Path.GetFullPath(cur), true);
            AssetDatabase.ImportAsset(cur, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("[PatientBayLayout] imported hospital_tv and the U-track privacy curtain");
        }

        static Bounds Bnd(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b;
        }

        static Transform Find(Scene scene, string name, string parent = null) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name && (parent == null || (t.parent != null && t.parent.name == parent)));

        /// <summary>Moves so the bounds centre is at the given x / z (null keeps that axis).</summary>
        static void MoveTo(Transform t, float? cx, float? cz)
        {
            var b = Bnd(t.gameObject); t.position += new Vector3(cx.HasValue ? cx.Value - b.center.x : 0, 0, cz.HasValue ? cz.Value - b.center.z : 0);
        }

        static Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>(Mats + "/" + name + ".mat");

        static GameObject SpawnTv(Transform root)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/hospital_tv.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(root, false); go.name = "hospital_tv";
            go.transform.position = new Vector3(0, TvCentreY, WallSouth); go.transform.rotation = Quaternion.identity;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = r.name.EndsWith("M_TvScreen", StringComparison.Ordinal) ? M("Wall TV screen off") : r.name.EndsWith("M_TvAlu", StringComparison.Ordinal) ? M("Sofa steel leg") ?? M("Wall TV housing")
                    : r.name.EndsWith("M_AlarmRed", StringComparison.Ordinal) ? AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prototype/PatientBay alarm red.mat") ?? M("Wall TV housing") : M("Wall TV housing");
                r.receiveGI = ReceiveGI.Lightmaps; r.shadowCastingMode = ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI);
            }
            return go;
        }

        static void Collider(Transform root, string name, Bounds b, bool capsule = false)
        {
            var old = root.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var c = new GameObject(name); c.transform.SetParent(root, false); c.transform.position = b.center;
            if (capsule) { var cc = c.AddComponent<CapsuleCollider>(); cc.radius = Mathf.Min(b.extents.x, b.extents.z); cc.height = b.size.y; }
            else c.AddComponent<BoxCollider>().size = b.size;
        }

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var rootGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName); if (rootGo == null) throw new InvalidOperationException("run FinalLookProps.Place first");
            var root = rootGo.transform;

            // ---- television: replace the thin slab by the 55-inch tilt-mounted set, centred on the wall opposite the bed
            foreach (var old in root.Cast<Transform>().Where(t => t.name == "ref_wall_tv" || t.name == "hospital_tv").ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var tv = SpawnTv(root);
            Collider(root, "hospital_tv collision", Bnd(tv));

            // ---- south wall, west to east: sink (standing room in front), bin below the towel dispenser, television, workstation; glove dispenser on the east wall
            var sink = Find(scene, "clinical_sink", "Clinical bay details"); MoveTo(sink, -1.42f, null);
            var handrub = Find(scene, "handrub", "Hospital room dressing"); MoveTo(handrub, -1.66f, null);
            var towel = Find(scene, "towel_dispenser", "Clinical bay details"); MoveTo(towel, -.98f, null);
            var bin = Find(scene, "pedal_bin", "Hospital room dressing"); MoveTo(bin, -.76f, -1.70f);
            var counter = Find(scene, "charting_counter", "Clinical bay details"); MoveTo(counter, 1.36f, null);
            var gloves = Find(scene, "glove_dispenser", "Clinical bay details");
            {
                gloves.rotation = Quaternion.Euler(0, -90f, 0);                         // faces into the room from the east wall
                var b = Bnd(gloves.gameObject); gloves.position += new Vector3(2.45f - b.max.x, 0, -1.70f - b.center.z);
            }

            // ---- sofa: shorter, further from the sink, clear of the privacy track; vine and palms stay off the track too
            var sofa = root.Cast<Transform>().First(t => t.name == "ref_sofa");
            sofa.localScale = new Vector3(.85f, 1f, 1f); sofa.rotation = Quaternion.Euler(0, 90f, 0); sofa.position = new Vector3(-2.03f, 0, .42f);
            Collider(root, "ref_sofa collision", Bnd(sofa.gameObject));
            var palms = root.Cast<Transform>().Where(t => t.name == "ref_palm").OrderBy(t => t.position.x).ToArray();
            palms[0].localScale = Vector3.one * .72f; palms[0].position = new Vector3(-2.02f, 0, 1.70f);
            palms[1].localScale = Vector3.one * .66f; palms[1].position = new Vector3(2.12f, 0, 1.70f);
            foreach (var p in palms) { var pb = Bnd(p.gameObject); Collider(root, "ref_palm collision " + (p.position.x < 0 ? "west" : "east"), new Bounds(new Vector3(p.position.x, .26f, p.position.z), new Vector3(.42f, .52f, .42f)), true); }
            foreach (var old in root.Cast<Transform>().Where(t => t.name == "Prop collision").ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (var t in new[] { sofa }.Concat(palms)) foreach (var stale in t.Cast<Transform>().Where(c => c.name == "Prop collision").ToArray()) UnityEngine.Object.DestroyImmediate(stale.gameObject);

            // ---- privacy curtain: gathered at the head-left end by default, double-sided fabric
            foreach (var cc in UnityEngine.Object.FindObjectsByType<ProcSense.VR.Views.CurtainCloth>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.UsesPath)) { cc.Open = 1f; cc.GatherFraction = TightGather; cc.Accordion = TightAccordion; }   // 2 Nov: parked it takes ~0.5 m of track, so the sofa, window and side table stay visible
            var privacy = AssetDatabase.FindAssets("PatientBay curtain privacy t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault();
            if (privacy != null) { privacy.SetFloat("_Cull", 0f); EditorUtility.SetDirty(privacy); }

            HideTeleportVisuals(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[PatientBayLayout] applied; sink " + Bnd(sink.gameObject) + " tv " + Bnd(tv) + " sofa " + Bnd(sofa.gameObject));
        }

        /// <summary>The blue pads are only visual markers on top of the teleport anchors: drop the mesh and renderer, keep the trigger collider, the anchor and its destination.</summary>
        public static int HideTeleportVisuals(Scene scene)
        {
            int n = 0;
            foreach (var a in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ProcSense.VR.XR.SafeTeleportAnchor>(true)))
            {
                var mr = a.GetComponent<MeshRenderer>(); var mf = a.GetComponent<MeshFilter>();
                if (mr != null) { UnityEngine.Object.DestroyImmediate(mr); n++; }
                if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
            }
            return n;
        }

        /// <summary>Opens PatientBay and writes the body / shorts / gown / mattress snapshot that the Blender gown fit reads (Logs/PatientArmsReview/gown-fit-input.json).</summary>
        public static void ExportGownFit()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            Debug.Log("[PatientBayLayout] " + ProcSense.EditorTools.PatientArmSetup.ExportFit());
        }

        public static void ImportRedrapedGown()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            Debug.Log("[PatientBayLayout] " + ProcSense.EditorTools.PatientArmSetup.ImportRedrapedGown());
        }

        public static void HideTeleportVisualsEverywhere()
        {
            foreach (var name in new[] { "PatientBay", "Corridor", "OT", "Recovery" })
            {
                var path = "Assets/Scenes/" + name + ".unity"; if (!File.Exists(Path.GetFullPath(path))) continue;
                var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = HideTeleportVisuals(s);
                if (n > 0) { EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s); }
                Debug.Log($"[PatientBayLayout] {name}: removed {n} visible teleport pads");
            }
        }
    }
}
