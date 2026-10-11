using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class EquipmentPilotReview
    {
        public const string ScenePath = "Assets/Scenes/EquipmentPilotReview.unity";

        [MenuItem("ProcSense/Equipment/Open Pilot Review")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Create();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (File.Exists(ScenePath)) return;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Review floor";
                floor.transform.position = new Vector3(0, -.035f, 0);
                floor.transform.localScale = new Vector3(5, .07f, 4);
                floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Equipment/M_PlasticGrey.mat");
                var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Wristband review plinth";
                table.transform.position = new Vector3(1, .85f, 0);
                table.transform.localScale = new Vector3(.6f, .06f, .4f);
                table.GetComponent<Renderer>().sharedMaterial = floor.GetComponent<Renderer>().sharedMaterial;
                for (int i = 0; i < EquipmentPrefabBuilder.Pilots.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/" + EquipmentPrefabBuilder.Pilots[i] + ".prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.transform.position = new Vector3(i - 1, i == 2 ? .882f : 0, 0);
                }
                var camera = new GameObject("Review camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(2.8f, 2.1f, 4);
                camera.transform.LookAt(new Vector3(0, .95f, 0));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.06f, .08f, .11f);
                var light = new GameObject("Review key").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2;
                light.transform.rotation = Quaternion.Euler(45, -145, 0);
                // Keep additive creation/capture away from the currently open patient bay.
                foreach (var root in scene.GetRootGameObjects()) root.transform.position += Vector3.right * 100;
                EditorSceneManager.SaveScene(scene, ScenePath);
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/previews/unity-pilots.png"));
                var texture = new RenderTexture(1600, 1000, 24);
                var old = RenderTexture.active;
                var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = texture;
                    camera.Render();
                    RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(output, pixels.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = old;
                    Object.DestroyImmediate(pixels);
                    Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
