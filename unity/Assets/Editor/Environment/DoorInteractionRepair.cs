using System;
using System.Linq;
using ProcSense.VR.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>Fits imported door art to existing physics/interaction owners, without rebuilding rooms or changing routes.</summary>
    public static class DoorInteractionRepair
    {
        [MenuItem("ProcSense/Setup/Repair modeled door interactions")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve unsaved scenes first.");
            foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
                foreach (var model in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Door leaf (modelled)").ToArray())
                {
                    var parent = model.parent;
                    while (parent != null && !parent.name.EndsWith(" architecture", StringComparison.Ordinal)) parent = parent.parent;
                    if (parent == null) throw new InvalidOperationException("Modeled door has no authored architecture owner.");
                    var leaf = parent.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Door leaf");
                    Reconcile(parent, leaf, model.gameObject);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
        }

        public static void Reconcile(Transform architecture, Transform leaf, GameObject model)
        {
            var hinge = leaf.GetComponentInParent<SwingDoor>(true);
            model.transform.SetParent(leaf.parent, false);
            // The imported mesh is authored around the opening centre. The existing physical hinge is at its left edge.
            model.transform.localPosition = hinge != null ? new Vector3(leaf.localPosition.x, 0, 0) : Vector3.zero;
            model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            // The opposite room intentionally owns no leaf. Do not resurrect its inactive physics proxy as visible art.
            model.SetActive(leaf.gameObject.activeInHierarchy);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            if (hinge == null) return; // Restricted/NPC doors keep their existing sliding owner and trainee barrier.
            foreach (var part in architecture.GetComponentsInChildren<Transform>(true))
            {
                if (part.IsChildOf(model.transform)) continue;
                if (part.name == "Door leaf" || part.name == "Kick plate" || part.name.StartsWith("Observation ", StringComparison.Ordinal) ||
                    part.name == "Frosted observation inset" || part.name.Contains("handle") || part.name.Contains("Handle") || part.name == "Hinge")
                {
                    var renderer = part.GetComponent<MeshRenderer>(); if (renderer != null) renderer.enabled = false;
                }
            }
            var grab = hinge.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab.colliders.Count != 2) throw new InvalidOperationException("Preserve the authored two-sided door grip.");
            // FBX export combines hardware by material, so there are no separate handle objects.
            // Fit the authored grip region (build_doors.py), retaining the two existing XRI owners.
            var steel = model.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name.Contains("SteelBrushed"));
            var hardwareBounds = steel.sharedMesh.bounds;
            bool lever = hardwareBounds.min.z < 0;
            for (int side = 0; side < 2; side++)
            {
                var collider = grab.colliders[side] as BoxCollider;
                if (collider == null) throw new InvalidOperationException("Expected the existing box grip collider.");
                collider.transform.position = steel.transform.TransformPoint(new Vector3(lever ? .38f : .405f,
                    lever ? 1f : 1.05f, side == 0 ? hardwareBounds.min.z : hardwareBounds.max.z));
                collider.center = Vector3.zero;
                var scale = collider.transform.lossyScale;
                collider.size = new Vector3(.18f / Mathf.Abs(scale.x), .52f / Mathf.Abs(scale.y), .14f / Mathf.Abs(scale.z));
            }
        }
    }
}
