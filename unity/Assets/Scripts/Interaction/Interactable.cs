using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>The kinds of thing the contextual menu offers actions for (plan p3-04, entity map). The actions themselves come from the interaction catalogue, never from here.</summary>
    public enum EntityType { Patient, Nurse, Doctor, Anaesthetist, Receptionist, Porter, Equipment, Records, TheatreTeam }

    /// <summary>
    /// Marks a scene object the trainee can point at to open its contextual menu (P3-CTX-02): who or what it is, where the panel appears, which renderers light up when it is hovered, and the name the voice mode answers to ("nurse", "monitor").
    /// It holds no clinical rule and sends nothing; selection (P3-CTX-03) and the panel (P3-CTX-04) read it.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> Live = new List<Interactable>();

        [SerializeField, Tooltip("Unique in the scene, for example patient, nurse_ward, monitor_bay, records_workstation.")] private string entityId = "";
        [SerializeField] private EntityType entityType = EntityType.Equipment;
        [SerializeField, Tooltip("Where the contextual panel appears. The object's own transform when empty.")] private Transform panelAnchor;
        [SerializeField, Tooltip("The renderers that light up on hover. All renderers under the object when empty.")] private Renderer[] highlightRenderers = Array.Empty<Renderer>();
        [SerializeField, Tooltip("What the trainee says to address it by voice, for example nurse, monitor.")] private string voiceName = "";

        [SerializeField, Tooltip("Its visible parts are built at runtime (a phone, a screen), so having nothing to highlight in the editor is not a mistake.")] private bool renderersAtRuntime;
        private Renderer[] cached;

        public string EntityId => entityId;
        public EntityType Type => entityType;
        public string VoiceName => voiceName;
        public Transform PanelAnchor => panelAnchor != null ? panelAnchor : transform;
        public bool Highlighted { get; private set; }

        /// <summary>Raised when the highlight turns on or off, so the highlight view can react without this component owning a material.</summary>
        public event Action<Interactable, bool> HighlightChanged;

        /// <summary>Every enabled Interactable, in no particular order.</summary>
        public static IReadOnlyList<Interactable> All => Live;

        public static Interactable Find(string id)
        {
            foreach (var i in Live) if (i.entityId == id) return i;
            return null;
        }

        public static IEnumerable<Interactable> OfType(EntityType type)
        {
            foreach (var i in Live) if (i.entityType == type) yield return i;
        }

        /// <summary>The renderers that light up: the ones given, or every renderer under the object.</summary>
        public IReadOnlyList<Renderer> HighlightRenderers
        {
            get
            {
                if (highlightRenderers.Length > 0) return highlightRenderers;
                if (cached == null || cached.Length == 0) cached = GetComponentsInChildren<Renderer>(true);          // an empty answer is not kept: parts built at runtime appear later
                return cached;
            }
        }

        public void Configure(string id, EntityType type, string voice = "", Transform anchor = null, Renderer[] renderers = null, bool partsBuiltAtRuntime = false)
        {
            entityId = id ?? ""; entityType = type; voiceName = voice ?? ""; panelAnchor = anchor; highlightRenderers = renderers ?? Array.Empty<Renderer>(); renderersAtRuntime = partsBuiltAtRuntime; cached = null;
        }

        public void SetHighlighted(bool on)
        {
            if (Highlighted == on) return;
            Highlighted = on;
            HighlightChanged?.Invoke(this, on);
        }

        /// <summary>What is wrong with this one: an empty list means it is usable. Used by the placement tool and by the coverage check.</summary>
        public IEnumerable<string> Problems()
        {
            if (string.IsNullOrWhiteSpace(entityId)) yield return name + ": no entity id";
            if (HighlightRenderers.Count == 0 && !renderersAtRuntime) yield return name + ": nothing to highlight";
            if (entityType != EntityType.Equipment && entityType != EntityType.Records && string.IsNullOrWhiteSpace(voiceName)) yield return name + ": a person needs a voice name";
        }

        private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }

        private void OnDisable()
        {
            Live.Remove(this);
            if (Highlighted) { Highlighted = false; HighlightChanged?.Invoke(this, false); }
        }
    }
}
