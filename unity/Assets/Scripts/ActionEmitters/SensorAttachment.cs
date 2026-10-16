using System;
using System.Linq;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>Physical placement sends actions; accepted public log entries drive the pose, including tablet and scene reload.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class SensorAttachment : ActionEmitter, IXRSelectFilter
    {
        [SerializeField] private XRSocketInteractor destination;
        [SerializeField] private Transform restingPlace;
        [SerializeField] private GameObject[] looseVisuals;
        [SerializeField] private GameObject wrappedVisual;
        [SerializeField] private Material placementHighlight;
        private XRGrabInteractable _grab;
        private Session _session;
        private int _count = -1;
        private bool _attached, _syncing, _returnHome;
        private bool _awaitSocketExit;
        private SensorPlacementGuide _guide;
        public bool canProcess => isActiveAndEnabled;
        public bool Attached => _attached;
        public XRSocketInteractor Destination => destination;
        public bool PlacementGuidesVisible => _guide != null && _guide.Visible;
        public float ReturnRadius => paramValue == "spo2" ? .065f : .09f;
        public void ConfigurePlacementHighlight(Material material) => placementHighlight = material;

        [Serializable] private class Parameters { public string sensor; }
        [Serializable] private class Entry { public string verb; public bool accepted; public Parameters @params; }

        public void ConfigureSensor(string sensor, XRSocketInteractor socket, Transform home, GameObject[] loose = null, GameObject wrapped = null)
        {
            destination = socket;
            restingPlace = home;
            looseVisuals = loose;
            wrappedVisual = wrapped;
            DebounceSeconds = 0; // State transitions guard duplicates; rapid removal must never be swallowed.
            Configure("attach_sensor", "patient", "sensor", sensor, sensor == "spo2" ? "Attach SpO2 probe" : "Attach BP cuff");
        }

        private void OnEnable()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _grab.selectFilters.Add(this);
            _grab.selectEntered.AddListener(Entered);
            _grab.selectExited.AddListener(Exited);
            _count = -1;
            RegisterFallback();
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(Entered);
            _grab.selectFilters.Remove(this);
            _grab.selectExited.RemoveListener(Exited);
            _guide?.Hide();
            UnregisterFallback();
        }

        private void OnDestroy() => _guide?.Dispose();

        private void Entered(SelectEnterEventArgs args)
        {
            if (_syncing || !ReferenceEquals(args.interactorObject, destination) || _attached) return;
            var answer = Fire();
            _attached = answer != null && answer.Accepted;
            _returnHome = !_attached;
        }

        private void Exited(SelectExitEventArgs args)
        {
            if (!_syncing && !args.isCanceled && !(args.interactorObject is XRSocketInteractor)
                && !_attached && restingPlace != null && Vector3.Distance(transform.position, restingPlace.position) <= ReturnRadius)
                _returnHome = true;
            if (_syncing || args.isCanceled || !ReferenceEquals(args.interactorObject, destination) || !_attached) return;
            var answer = Fire("detach_sensor");
            if (answer != null && answer.Accepted)
            {
                _attached = false;
                _awaitSocketExit = true;
            }
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!(interactor is XRSocketInteractor)) return true;
            if (!ReferenceEquals(interactor, destination)) return false;
            if (_syncing && _attached) return true; // Explicit accepted tablet restore.
            return !_awaitSocketExit && (_attached || NearSocket());
        }

        private bool NearSocket()
        {
            var trigger = destination.GetComponent<SphereCollider>();
            float radius = trigger != null ? trigger.radius * Mathf.Max(destination.transform.lossyScale.x,
                destination.transform.lossyScale.y, destination.transform.lossyScale.z) : .065f;
            return Vector3.Distance(_grab.GetAttachTransform(destination).position,
                destination.attachTransform != null ? destination.attachTransform.position : destination.transform.position) <= radius;
        }

        private void LateUpdate() => SyncNow();

        public void SyncNow()
        {
            if (_grab == null || destination == null || restingPlace == null || _syncing) return;
            if (_awaitSocketExit && !destination.IsHovering(_grab) && !NearSocket()) _awaitSocketExit = false;
            var session = SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            bool changedSession = !ReferenceEquals(session, _session);
            if (changedSession || _count != (session?.Log.Count ?? 0))
            {
                bool desired = false;
                if (session != null)
                {
                    // No PatientState access. Rehydrate visible equipment from accepted trainee actions.
                    foreach (string line in session.Log.ToJsonl().Split('\n'))
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        var entry = JsonUtility.FromJson<Entry>(line);
                        if (!entry.accepted || entry.@params?.sensor != paramValue) continue;
                        if (entry.verb == "attach_sensor") desired = true;
                        else if (entry.verb == "detach_sensor") desired = false;
                    }
                }
                _returnHome |= changedSession || (_attached && !desired);
                _attached = desired;
                if (desired) _awaitSocketExit = false;
                _session = session;
                _count = session?.Log.Count ?? 0;
            }
            _syncing = true;
            try
            {
                if (!_attached && _returnHome) _awaitSocketExit = true;
                var manager = _grab.interactionManager;
                bool inSocket = _grab.interactorsSelecting.Any(i => ReferenceEquals(i, destination));
                if (manager != null && ((_attached && !inSocket) || (!_attached && (inSocket || _returnHome))))
                {
                    foreach (var source in _grab.interactorsSelecting.ToArray()) manager.SelectExit(source, _grab);
                    if (_attached && destination.isActiveAndEnabled) manager.SelectEnter((IXRSelectInteractor)destination, (IXRSelectInteractable)_grab);
                }
                if (!_attached && _returnHome)
                {
                    // XRI can still apply its last socket target in the next physics phase.
                    // Move all three pose owners together, not only the visible Transform.
                    _grab.SetTargetPose(new Pose(restingPlace.position, restingPlace.rotation));
                    var body = GetComponent<Rigidbody>();
                    if (body != null)
                    {
                        if (!body.isKinematic)
                        {
                            body.linearVelocity = Vector3.zero;
                            body.angularVelocity = Vector3.zero;
                        }
                        body.position = restingPlace.position;
                        body.rotation = restingPlace.rotation;
                    }
                    transform.SetPositionAndRotation(restingPlace.position, restingPlace.rotation);
                    Physics.SyncTransforms();
                }
                _returnHome = false;
                if (wrappedVisual != null)
                {
                    wrappedVisual.SetActive(_attached);
                    foreach (var visual in looseVisuals ?? Array.Empty<GameObject>()) if (visual != null) visual.SetActive(!_attached);
                    var collider = GetComponent<BoxCollider>();
                    if (collider != null)
                    {
                        var size = _attached ? new Vector3(.105f, .105f, .16f) : new Vector3(.28f, .08f, .16f);
                        var center = _attached ? Vector3.zero : new Vector3(0, .04f, 0);
                        if (collider.size != size) collider.size = size;
                        if (collider.center != center) collider.center = center;
                    }
                }
            }
            finally { _syncing = false; }
            if (placementHighlight != null)
            {
                if (_guide == null) _guide = new SensorPlacementGuide(destination, restingPlace, placementHighlight, ReturnRadius);
                bool held = _grab.interactorsSelecting.Any(i => !(i is XRSocketInteractor));
                _guide.Refresh(held, !_awaitSocketExit && NearSocket(), Vector3.Distance(transform.position, restingPlace.position) <= ReturnRadius);
            }
        }
    }
}
