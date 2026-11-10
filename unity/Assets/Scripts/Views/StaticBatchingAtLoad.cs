using System.Collections.Generic;
using ProcSense.VR.Boot;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Opt-in runtime static batching of immovable scenery (never added automatically: put it on one GameObject per room scene, or call <see cref="Combine"/> from a test). Combines, per root, only
    /// opaque MeshRenderers that cannot move or be touched: no Rigidbody, Animator, SwingDoor, RoomDoor, TransportDoor/Actor, XR interactable, CharacterController, Canvas, LODGroup, skinned/particle
    /// renderer, no collider that is a trigger, not the city, not window/vision glass, not already part of a static batch, mesh readable. Anything else stays an ordinary dynamic draw.
    /// Caveats: runtime batching copies vertex data (memory grows by the combined meshes), and it only helps where materials are shared, so measure draw calls and memory before keeping it on.
    /// Combine once at load; do not Combine again for the same objects. Renderer.forceRenderingOff still works per renderer on a batched object.
    /// </summary>
    public sealed class StaticBatchingAtLoad : MonoBehaviour
    {
        [Tooltip("Roots whose descendants are combined. Empty = this GameObject's scene roots.")]
        [SerializeField] private GameObject[] roots = new GameObject[0];
        [SerializeField] private bool combineOnStart = false;
        public static int LastCombined { get; private set; }

        private void Start() { if (combineOnStart) Combine(); }

        /// <summary>Combines the safe renderers under each root. Returns how many renderers were included.</summary>
        public int Combine()
        {
            var list = new List<GameObject>();
            var rs = roots.Length > 0 ? roots : gameObject.scene.GetRootGameObjects();
            foreach (var root in rs)
            {
                if (root == null) continue;
                var batch = new List<GameObject>();
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false)) if (Eligible(r)) batch.Add(r.gameObject);
                if (batch.Count > 1) { StaticBatchingUtility.Combine(batch.ToArray(), root); list.AddRange(batch); }
            }
            LastCombined = list.Count; return list.Count;
        }

        /// <summary>The safe rule; public so a test can assert that no interactable is ever eligible.</summary>
        public static bool Eligible(MeshRenderer r)
        {
            if (r == null || !r.enabled || r.isPartOfStaticBatch || r.forceRenderingOff || r.gameObject.layer == 5) return false;
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;
            if (ExteriorVisibility.UnderCity(r.transform) || ExteriorVisibility.IsWindowGlass(r)) return false;
            foreach (var m in r.sharedMaterials) if (m == null || m.renderQueue > 2500) return false;               // opaque and alpha-tested only: batching must not change transparent sorting
            var t = r.transform;
            if (t.GetComponentInParent<Rigidbody>(true) != null || t.GetComponentInParent<Animator>(true) != null || t.GetComponentInParent<SwingDoor>(true) != null
                || t.GetComponentInParent<RoomDoor>(true) != null || t.GetComponentInParent<TransportDoorSequence>(true) != null || t.GetComponentInParent<TransportActor>(true) != null
                || t.GetComponentInParent<CharacterController>(true) != null || t.GetComponentInParent<Canvas>(true) != null || t.GetComponentInParent<LODGroup>(true) != null) return false;
            if (t.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true) != null) return false;
            foreach (var c in r.GetComponentsInParent<Collider>(true)) if (c.isTrigger) return false;
            foreach (var mb in t.GetComponentsInParent<MonoBehaviour>(true)) if (mb is IMovesTransform) return false;   // opt-out marker for any script that moves its object
            return true;
        }
    }

    /// <summary>Marker for a MonoBehaviour that moves its own transform at run time; such objects are never batched statically.</summary>
    public interface IMovesTransform { }
}
