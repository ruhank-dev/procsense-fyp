using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.EditorTools
{
    /// <summary>One-time migration of the authored rooms, not a prototype scene rebuild.</summary>
    public static class ContinuousHospitalSetup
    {
        public static string RefineSharedWalls()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play/baking first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes first.");
            foreach (string name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.GetComponent<RoomLayout>() != null);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled) continue;
                    var direction = Vector3.zero;
                    if ((name == "PatientBay" || name == "Corridor") && renderer.name.StartsWith("Wall right")) direction = Vector3.left;
                    if (name == "Corridor" && renderer.name.StartsWith("Wall left")) direction = Vector3.right;
                    if (name == "OT" && renderer.name.StartsWith("Wall front")) direction = Vector3.forward;
                    if (direction == Vector3.zero) continue;
                    string path = "Assets/Models/Environment/Shared wall inward " + direction.x + " " + direction.z + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        var original = renderer.GetComponent<MeshFilter>().sharedMesh;
                        mesh = UnityEngine.Object.Instantiate(original); mesh.name = "Room interior wall face";
                        var triangles = original.triangles; var normals = original.normals;
                        var face = new List<int>();
                        for (int t = 0; t < triangles.Length; t += 3)
                            if (Vector3.Dot(normals[triangles[t]], direction) > .9f)
                            { face.Add(triangles[t]); face.Add(triangles[t + 1]); face.Add(triangles[t + 2]); }
                        if (face.Count != 6) throw new InvalidOperationException("Expected one inward cube face.");
                        mesh.triangles = face.ToArray();
                        Unwrapping.GenerateSecondaryUVSet(mesh);
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
                    renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                }
                foreach (var door in root.GetComponentsInChildren<RoomDoor>().Where(d => !d.Restricted))
                {
                    var architecture = door.transform.parent;
                    bool owner = architecture.GetComponentInChildren<SwingDoor>() != null;
                    foreach (var frame in architecture.Cast<Transform>().Where(t => t.name.StartsWith("Frame ")))
                    {
                        frame.gameObject.SetActive(owner);
                        if (!owner) continue;
                        var size = frame.localScale; size.z = .2f; frame.localScale = size;
                        var position = frame.localPosition; position.z = 0; frame.localPosition = position;
                    }
                    foreach (string sign in new[] { "Room sign", "Sign accent", "Room number", "Room title" })
                    {
                        var item = architecture.Find(sign); var position = item.localPosition;
                        position.z = sign == "Room sign" ? -.09f : -.114f; item.localPosition = position;
                    }
                    var hinge = architecture.GetComponentInChildren<SwingDoor>();
                    if (hinge != null)
                    {
                        var leaf = hinge.Leaf.transform; var size = leaf.localScale; size.y = 2.13f; leaf.localScale = size;
                        var position = leaf.localPosition; position.y = 1.075f; leaf.localPosition = position;
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            return "Shared walls render only their own inward face; one full-depth frame, visible signs and fitted leaves.";
        }

        public static string Capture(string name, Vector3 position, Vector3 target)
        {
            var go = new GameObject("Temporary continuous doorway review", typeof(Camera));
            var camera = go.GetComponent<Camera>(); camera.fieldOfView = 72; camera.nearClipPlane = .03f;
            camera.enabled = false; camera.transform.position = position; camera.transform.LookAt(target);
            var render = RenderTexture.GetTemporary(1280, 900, 24); var previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
                texture = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); texture.Apply();
                string path = System.IO.Path.GetFullPath("Logs/ContinuousDoorReview/" + name + ".png");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); return path;
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play and baking before migrating rooms.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes first.");
            var results = new List<string>();
            foreach (string name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
                if (scene.GetRootGameObjects().Any(g => g.GetComponent<RoomLayout>() != null))
                    throw new InvalidOperationException(name + " is already connected; preserve authored changes.");
                var roots = scene.GetRootGameObjects();
                var root = new GameObject("Connected " + name, typeof(RoomLayout));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<RoomLayout>().Size = name == "PatientBay" ? new Vector2(5, 4)
                    : name == "Corridor" ? new Vector2(4, 10) : new Vector2(7, 7);
                foreach (var old in roots) old.transform.SetParent(root.transform, true);
                var doors = root.GetComponentsInChildren<RoomDoor>(true).Where(d => !d.Restricted).ToArray();
                foreach (var door in doors)
                {
                    var architecture = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == door.name + " architecture");
                    var center = architecture.position;
                    if (name == "PatientBay") center.x = 2.5f;
                    else if (name == "Corridor") center.x = door.Destination == "PatientBay" ? -2 : 2;
                    else center.z = -3.5f;
                    architecture.position = center;
                    door.transform.SetParent(architecture, true);
                    door.transform.localPosition = new Vector3(-.12f, 1.13f, -.015f);
                    door.Connection = (name == "PatientBay" || door.Destination == "PatientBay") ? "BayCorridor" : "CorridorOT";
                    bool side = name != "OT";
                    CutOpening(root.transform, center, side);
                    bool owner = name == "PatientBay" || (name == "Corridor" && door.Destination == "OT");
                    if (owner) BuildLeaf(architecture, door.Connection, name, door.Destination);
                    else HideDuplicateLeaf(architecture);
                    // The panel no longer owns movement; Enter() remains an accessible toggle API.
                    EditorUtility.SetDirty(door);
                }
                if (name == "Corridor") root.transform.position = new Vector3(4.5f, 0, 2.2f);
                if (name == "OT") root.transform.SetPositionAndRotation(new Vector3(10, 0, 5.2f), Quaternion.Euler(0, 90, 0));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                results.Add(name + ": retained existing contents; cut " + doors.Length + " portal(s)");
            }
            AssetDatabase.SaveAssets();
            return string.Join("\n", results);
        }

        private static bool MovingPart(string name) => name == "Door leaf" || name == "Kick plate"
            || name.StartsWith("Observation ") || name == "Frosted observation inset"
            || name.StartsWith("Handle ") || name == "Pull handle" || name == "Door identity plate" || name == "Access notice";

        private static void HideDuplicateLeaf(Transform architecture)
        {
            foreach (var child in architecture.Cast<Transform>().ToArray())
                if (MovingPart(child.name) || child.name == "Hinge") child.gameObject.SetActive(false);
        }

        private static void BuildLeaf(Transform architecture, string connection, string from, string to)
        {
            var pivot = new GameObject("Grabbable hinged door", typeof(Rigidbody));
            pivot.transform.SetParent(architecture, false);
            pivot.transform.localPosition = new Vector3(-.53f, 0, 0);
            var body = pivot.GetComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            foreach (var child in architecture.Cast<Transform>().Where(t => MovingPart(t.name)).ToArray())
            {
                child.SetParent(pivot.transform, true);
                foreach (var part in child.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
                foreach (var renderer in child.GetComponentsInChildren<MeshRenderer>(true))
                { renderer.lightmapIndex = -1; renderer.receiveGI = ReceiveGI.LightProbes; renderer.lightProbeUsage = LightProbeUsage.BlendProbes; }
            }
            var leaf = pivot.transform.Find("Door leaf").gameObject.AddComponent<BoxCollider>();
            var front = pivot.transform.Find("Pull handle");
            var frontCollider = front.gameObject.AddComponent<BoxCollider>(); frontCollider.size = new Vector3(4, 1.2f, 4);
            var back = UnityEngine.Object.Instantiate(front.gameObject, pivot.transform).transform; back.name = "Reverse pull handle";
            var backPosition = back.localPosition; backPosition.z = .16f; back.localPosition = backPosition;
            // Rear handle mounting posts, on the reverse face of the actual moving leaf.
            foreach (float y in new[] { .89f, 1.18f })
            {
                var mount = UnityEngine.Object.Instantiate(pivot.transform.Find("Handle mounting").gameObject, pivot.transform);
                mount.name = "Rear handle mounting"; mount.transform.localPosition = new Vector3(.955f, y, .12f);
            }
            var grab = pivot.AddComponent<XRGrabInteractable>();
            grab.colliders.Clear(); grab.colliders.Add(frontCollider); grab.colliders.Add(back.GetComponent<BoxCollider>());
            grab.trackPosition = false; grab.trackRotation = false; grab.trackScale = false;
            grab.throwOnDetach = false; grab.forceGravityOnDetach = false; grab.unparentTransformOnGrab = false;
            grab.retainTransformParent = true; grab.useDynamicAttach = true;
            grab.selectMode = InteractableSelectMode.Single;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            var hinge = pivot.AddComponent<SwingDoor>(); hinge.Connection = connection; hinge.RoomA = from; hinge.RoomB = to; hinge.Leaf = leaf;
        }

        private static void CutOpening(Transform room, Vector3 portal, bool side)
        {
            float cut = side ? portal.z : portal.x;
            float plane = side ? portal.x : portal.z;
            foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>().ToArray())
            {
                bool shell = renderer.name.StartsWith("Wall ");
                bool trim = renderer.name == "Lower oak wall panel" || renderer.name == "Panel cap rail" || renderer.name == "Coved skirting";
                if (!shell && !trim) continue;
                var b = renderer.bounds;
                if (Mathf.Abs((side ? b.center.x : b.center.z) - plane) > .15f || b.min.y >= 2.14f) continue;
                float lo = side ? b.min.z : b.min.x, hi = side ? b.max.z : b.max.x;
                if (hi <= cut - .57f || lo >= cut + .57f) continue;
                void Piece(float lower, float upper, float bottom, float top, string suffix)
                {
                    if (upper - lower < .002f || top - bottom < .002f) return;
                    var go = UnityEngine.Object.Instantiate(renderer.gameObject, renderer.transform.parent);
                    go.name = renderer.name + " doorway " + suffix;
                    var pos = b.center; pos.y = (bottom + top) / 2;
                    var size = b.size; size.y = top - bottom;
                    if (side) { pos.z = (lower + upper) / 2; size.z = upper - lower; }
                    else { pos.x = (lower + upper) / 2; size.x = upper - lower; }
                    go.transform.position = pos; go.transform.localScale = size;
                    var r = go.GetComponent<MeshRenderer>(); r.lightmapIndex = -1;
                }
                Piece(lo, cut - .57f, b.min.y, b.max.y, "left");
                Piece(cut + .57f, hi, b.min.y, b.max.y, "right");
                Piece(Mathf.Max(lo, cut - .57f), Mathf.Min(hi, cut + .57f), 2.14f, b.max.y, "header");
                renderer.enabled = false;
                foreach (var collider in renderer.GetComponents<Collider>()) collider.enabled = false;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
            }
        }
    }
}
