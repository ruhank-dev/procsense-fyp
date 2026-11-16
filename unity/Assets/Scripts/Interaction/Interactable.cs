using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// P3-CTX-02: marks something in a room the player can point at to get its contextual actions: the entity type of content/ui/interactions.json (patient, nurse, doctor, anaesthetist,
    /// porter, monitor, records, theatre_team), an id, the anchor the panel sits beside, the renderers the highlight tints, and the name voice uses for it. Presentation only: it sends no
    /// actions and holds no clinical state. Every live one is in <see cref="All"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Interactable : MonoBehaviour
    {
        /// <summary>The entity types of the catalogue (content/ui/interactions.json "entities").</summary>
        public static readonly string[] Types = { "patient", "nurse", "doctor", "anaesthetist", "porter", "monitor", "records", "theatre_team" };

        [SerializeField] private string entityId = "";
        [SerializeField] private string entityType = "";
        [SerializeField] private Transform panelAnchor;
        [SerializeField] private string voiceName = "";
        [SerializeField] private List<Renderer> highlight = new List<Renderer>();

        public string EntityId { get => string.IsNullOrEmpty(entityId) ? name : entityId; set => entityId = value; }
        public string EntityType { get => entityType; set => entityType = value; }
        public Transform PanelAnchor { get => panelAnchor != null ? panelAnchor : transform; set => panelAnchor = value; }
        public string VoiceName { get => string.IsNullOrEmpty(voiceName) ? DefaultVoiceName(entityType) : voiceName; set => voiceName = value; }
        public List<Renderer> Highlight => highlight;

        private static readonly List<Interactable> Live = new List<Interactable>();
        public static IReadOnlyList<Interactable> All => Live;

        private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        private void OnDisable() => Live.Remove(this);

        /// <summary>The words a trainee would use for it ("the nurse", "the monitor").</summary>
        public static string DefaultVoiceName(string type)
        {
            switch (type)
            {
                case "theatre_team": return "theatre team";
                case "porter": return "porter";
                case "records": return "notes";
                default: return type ?? "";
            }
        }

        /// <summary>Sets it up: type, id, anchor (a "head" bone when there is one, else the top of its bounds), and the renderers under it.</summary>
        public static Interactable Mark(GameObject go, string type, string id = null)
        {
            var i = go.GetComponent<Interactable>() ?? go.AddComponent<Interactable>();
            i.entityType = type;
            if (!string.IsNullOrEmpty(id)) i.entityId = id;
            i.highlight.Clear();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer) && !(r is LineRenderer) && r.GetComponentInParent<Canvas>() == null) i.highlight.Add(r);
            if (i.panelAnchor == null) i.panelAnchor = FindHead(go.transform) ?? TopAnchor(i);
            return i;
        }

        private static Transform FindHead(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "head" || t.name == "Bip01 Head" || t.name.EndsWith(" head") || t.name == "Head") return t;
            return null;
        }

        private static Transform TopAnchor(Interactable i)
        {
            if (i.highlight.Count == 0) return i.transform;
            var b = i.highlight[0].bounds;
            foreach (var r in i.highlight) b.Encapsulate(r.bounds);
            var a = new GameObject("Panel anchor").transform;
            a.SetParent(i.transform, false);
            a.position = new Vector3(b.center.x, b.max.y, b.center.z);
            return a;
        }
    }
}
