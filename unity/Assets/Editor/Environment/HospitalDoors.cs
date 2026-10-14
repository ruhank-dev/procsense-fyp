using System;
using System.IO;
using System.Linq;
using ProcSense.VR.Boot;
using ProcSense.VR.XR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    // Dresses existing navigation points; never rebuilds a room or changes its routes.
    public static class HospitalDoors
    {
        private const string Folder = "Assets/Materials/Hospital/Doors";
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        public static string Capture(string name)
        {
            var scene = SceneManager.GetSceneByName(name);
            if (!scene.isLoaded || new[] { "PatientBay", "Corridor", "OT" }.Count(n => SceneManager.GetSceneByName(n).isLoaded) != 1)
                throw new InvalidOperationException("Open only one clinical room for a door preview");
            var door = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomDoor>()).First();
            var target = door.transform.position; target.y = 1.35f;
            var go = new GameObject("Temporary door review camera", typeof(Camera));
            var camera = go.GetComponent<Camera>(); camera.fieldOfView = 65; camera.nearClipPlane = .02f;
            camera.transform.position = target - door.transform.forward * 2.45f - door.transform.right * .55f + Vector3.up * .2f;
            camera.transform.LookAt(target);
            var render = RenderTexture.GetTemporary(1100, 900, 24); var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
                image = new Texture2D(1100, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1100, 900), 0, 0); image.Apply();
                string path = Path.GetFullPath("Temp/DoorReview/" + name + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG()); return path;
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render);
                if (image != null) UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public static object Apply(string name)
        {
            if (!new[] { "PatientBay", "Corridor", "OT" }.Contains(name)) throw new ArgumentException("Unknown room");
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play/baking first");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Preserve existing unsaved scenes first");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == "Hospital doors")) throw new InvalidOperationException("Doors already dressed; preserve authored changes");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var roots = scene.GetRootGameObjects();
            var doors = roots.SelectMany(g => g.GetComponentsInChildren<RoomDoor>(true)).ToArray();
            if (doors.Length != (name == "Corridor" ? 4 : 1)) throw new InvalidOperationException("Unexpected navigation layout");
            var comfort = roots.SelectMany(g => g.GetComponentsInChildren<ComfortPanel>(true)).ToArray();
            foreach (var panel in comfort)
            {
                if (panel.transform.childCount != 0 || panel.GetComponents<Component>().Length != 2)
                    throw new InvalidOperationException("Comfort display has extra authored content; review first");
            }
            foreach (var panel in comfort) UnityEngine.Object.DestroyImmediate(panel.gameObject);
            var root = new GameObject("Hospital doors").transform;
            var frame = Material("Ivory door frame", "E4E8E2", .3f);
            var leaf = Material("Sage clinical door", "849F99", .28f);
            var ot = Material("Theatre teal door", "527D80", .28f);
            var metal = Material("Brushed stainless hardware", "A9B7B8", .48f, .7f);
            var glass = Material("Frosted observation glass", "A9C4C5", .55f);
            var sign = Material("Teal wayfinding", "234E51", .18f);
            var amber = Material("Restricted access amber", "B78B4F", .2f);
            foreach (var door in doors)
            {
                var origin = door.transform.position; origin.y = 0;
                var dressed = new GameObject(door.name + " architecture").transform;
                dressed.SetParent(root, false); dressed.SetPositionAndRotation(origin, door.transform.rotation);
                Box(dressed, "Door leaf", new Vector3(0, 1.055f, .055f), new Vector3(1.06f, 2.09f, .055f), door.Destination == "OT" ? ot : leaf);
                Box(dressed, "Frame left", new Vector3(-.57f, 1.11f, .035f), new Vector3(.08f, 2.22f, .13f), frame);
                Box(dressed, "Frame right", new Vector3(.57f, 1.11f, .035f), new Vector3(.08f, 2.22f, .13f), frame);
                Box(dressed, "Frame lintel", new Vector3(0, 2.18f, .035f), new Vector3(1.22f, .08f, .13f), frame);
                Box(dressed, "Kick plate", new Vector3(0, .21f, .023f), new Vector3(.99f, .31f, .012f), metal);
                Box(dressed, "Observation surround", new Vector3(.19f, 1.72f, .019f), new Vector3(.31f, .61f, .022f), metal);
                Box(dressed, "Frosted observation inset", new Vector3(.19f, 1.72f, .004f), new Vector3(.255f, .55f, .013f), glass);
                foreach (float y in new[] { .6f, 1.1f, 1.85f })
                    Box(dressed, "Hinge", new Vector3(-.515f, y, .014f), new Vector3(.045f, .12f, .035f), metal);
                foreach (float y in new[] { .89f, 1.18f })
                    Box(dressed, "Handle mounting", new Vector3(.425f, y, -.008f), new Vector3(.05f, .04f, .07f), metal);
                Box(dressed, "Pull handle", new Vector3(.425f, 1.035f, -.051f), new Vector3(.035f, .35f, .035f), metal);
                Box(dressed, "Room sign", new Vector3(0, 2.42f, .015f), new Vector3(1.22f, .29f, .04f), sign);
                Box(dressed, "Sign accent", new Vector3(-.57f, 2.42f, -.009f), new Vector3(.035f, .23f, .008f), door.Restricted ? amber : metal);
                string number = name == "Corridor" ? door.Restricted ? (door.Title.Contains("CT") ? "D01" : "D02") : door.Destination == "OT" ? "T01" : "B01" : "EXIT";
                Label(dressed, "Room number", number, new Vector3(-.41f, 2.42f, -.009f), new Vector2(.25f, .19f), .82f, Hex("E3EAE6"));
                Label(dressed, "Room title", door.Title == "OPERATING THEATRE" ? "OPERATING\nTHEATRE" : door.Title,
                    new Vector3(.16f, 2.42f, -.009f), new Vector2(.81f, .24f), door.Title.Length > 12 ? .69f : .83f, Color.white);
                Box(dressed, "Door identity plate", new Vector3(-.12f, 1.34f, .011f), new Vector3(.64f, .17f, .022f), sign);
                Label(dressed, "Access notice", door.Restricted ? "NO TRAINEE ACCESS" : "CLINICAL AREA", new Vector3(-.12f, 1.34f, -.003f), new Vector2(.61f, .14f), .55f, Color.white);
                // Keep the existing RoomDoor component, route and orientation; mount its smaller control on the leaf.
                door.transform.position = dressed.TransformPoint(new Vector3(-.12f, 1.13f, -.015f));
                door.ArchitecturalSign = true;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return new { room = name, doors = doors.Length, comfortDisplaysRemoved = comfort.Length, readyToBake = true };
        }

        private static Material Material(string name, string color, float smooth, float metallic = 0)
        {
            string path = Folder + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            result.SetColor("_BaseColor", Hex(color)); result.SetFloat("_Smoothness", smooth); result.SetFloat("_Metallic", metallic);
            result.enableInstancing = true; AssetDatabase.CreateAsset(result, path); return result;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.receiveGI = ReceiveGI.Lightmaps;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
        }

        private static void Label(Transform parent, string name, string value, Vector3 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(TextMeshPro)); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.GetComponent<TextMeshPro>(); label.font = TMP_Settings.defaultFontAsset; label.fontSharedMaterial = label.font.material;
            label.text = value; label.fontSize = fontSize; label.color = color; label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.sizeDelta = size; label.raycastTarget = false;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
