using System.Linq;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>Grab the folder and activate to open/close. Only Core reveals its contents.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class NotesFolderInteraction : ActionEmitter
    {
        [SerializeField] private Transform cover;
        [SerializeField] private GameObject page;
        private XRGrabInteractable _grab;
        private Transform _hinge;
        private Session _session;
        private bool _knownNotes;
        public bool IsOpen { get; private set; }

        public void ConfigureFolder(Transform coverTransform, GameObject pageObject)
        {
            cover = coverTransform;
            page = pageObject;
            Configure("open_notes", "patient", fallbackLabel: "Open notes");
        }

        private void Awake()
        {
            _hinge = new GameObject("Cover hinge").transform;
            _hinge.SetParent(transform, false);
            _hinge.localPosition = new Vector3(-.112f, .009f, 0);
            if (cover != null) cover.SetParent(_hinge, true);
            SetOpen(false);
        }

        private void OnEnable()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _grab.activated.AddListener(Activate);
            RegisterFallback();
        }

        private void OnDisable()
        {
            _grab.activated.RemoveListener(Activate);
            UnregisterFallback();
        }

        private void Activate(ActivateEventArgs args) => ToggleHeld();

        public bool ToggleHeld()
        {
            SyncNow();
            if (!_grab.interactorsSelecting.Any(i => !(i is XRSocketInteractor))) return false;
            if (IsOpen) { SetOpen(false); return true; }
            var host = SimulationHost.Instance;
            if (host?.Observable?.Notes == null)
            {
                var answer = Fire();
                if (answer == null || !answer.Accepted) return false;
            }
            _knownNotes = host?.Observable?.Notes != null;
            if (!_knownNotes) return false;
            SetOpen(true);
            return true;
        }

        private void Update() => SyncNow();

        public void SyncNow()
        {
            var host = SimulationHost.Instance;
            bool known = host?.Observable?.Notes != null;
            if (!ReferenceEquals(_session, host?.Session) || known != _knownNotes)
            {
                _session = host?.Session;
                _knownNotes = known;
                SetOpen(known); // Tablet open and scene/session restore use the same visible folder.
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (_hinge != null) _hinge.localRotation = Quaternion.Euler(0, 0, open ? 155 : 0);
            if (page != null) page.SetActive(open);
            if (open && page != null) page.GetComponent<ProcSense.VR.Views.NotesPanelView>()?.RenderNow();
        }
    }
}
