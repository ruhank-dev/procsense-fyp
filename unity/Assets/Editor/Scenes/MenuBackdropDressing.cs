using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// P3-MNU-02 / P3-AST-06: dresses the menu's reception room with the corridor's existing props (plants in the corners, a wall clock, picture frames, a sign-off fire extinguisher) so the first thing the
    /// trainee sees is a lived-in waiting area. Props come from the corridor's own prefabs, cast no shadows (the scene has none), and are marked static. Idempotent: everything goes under "Menu dressing".
    /// </summary>
    public static class MenuBackdropDressing
    {
        private const string Scene = "Assets/Scenes/MainMenu.unity";
        private const string Poly = "Assets/Prefabs/Environment/PolyHaven/";

        [MenuItem("ProcSense/Environment/Dress the menu backdrop")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var existing = GameObject.Find("Menu dressing");
            if (existing != null) Object.DestroyImmediate(existing);
            var room = GameObject.Find("Menu backdrop");
            if (room == null) { Debug.LogError("MenuBackdropDressing: no 'Menu backdrop' in the scene"); return; }
            var rends = room.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Debug.LogError("MenuBackdropDressing: the backdrop has no renderers"); return; }
            var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);

            var root = new GameObject("Menu dressing").transform;
            float inset = .55f, wallZ = b.max.z - .12f;
            Put(root, "potted_plant_01", new Vector3(b.min.x + inset, b.min.y, b.max.z - inset), 0);
            Put(root, "potted_plant_02", new Vector3(b.max.x - inset, b.min.y, b.max.z - inset), 0);
            Put(root, "potted_plant_04", new Vector3(b.max.x - inset, b.min.y, b.min.z + inset + 1f), 0);
            Put(root, "wall_clock", new Vector3(b.center.x, b.min.y + 2.35f, wallZ), 180);
            Put(root, "hanging_picture_frame_02", new Vector3(b.center.x - 1.6f, b.min.y + 1.6f, wallZ), 180);
            Put(root, "hanging_picture_frame_02", new Vector3(b.center.x + 1.6f, b.min.y + 1.6f, wallZ), 180);
            Put(root, "korean_fire_extinguisher_01", new Vector3(b.min.x + .2f, b.min.y + 1.1f, b.center.z), 90);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MenuBackdropDressing: " + root.childCount + " props placed");
        }

        private static void Put(Transform root, string prefab, Vector3 at, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Poly + prefab + ".prefab");
            if (asset == null) { Debug.LogWarning("MenuBackdropDressing: missing " + prefab); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            go.transform.position = at; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
    }
}
