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
    [DefaultExecutionOrder(-10)]
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
        private readonly EquipmentNotice _notice = new EquipmentNotice();
        private static readonly System.Collections.Generic.List<SensorAttachment> Active = new System.Collections.Generic.List<SensorAttachment>();
        /// <summary>The reason shown above the patient target, or "" when none is shown (plain words; neutral in the examination).</summary>
        public string NoticeText => _notice.Text;
        public bool canProcess => isActiveAndEnabled;
        public bool Attached => _attached;
        public XRSocketInteractor Destination => destination;
        public bool PlacementGuidesVisible => _guide != null && _guide.Visible;
        /// <summary>What the patient target shows while the sensor is held: Ready (green, release to attach), Approach (blue), Unavailable (amber: no running session can record it).</summary>
        public SensorPlacementGuide.TargetState PatientTargetState => _guide != null ? _guide.PatientState : SensorPlacementGuide.TargetState.Hidden;
        public float ReturnRadius => paramValue == "spo2" ? .065f : .09f;
        /// <summary>
        /// True only while a running session can take the attach action (a session exists, is not paused, ended or submitted). The shipped player has no session until the menu
        /// starts one (BootLoader.MenuStartsSession), so before that the patient target is shown as unavailable and the socket refuses the sensor instead of snapping it and bouncing it home.
        /// </summary>
        public static bool SessionAcceptsActions
        {
            get
            {
                var host = SimulationHost.Instance;
                return host != null && host.Session != null && !host.Paused && !host.Ended && !host.Session.Paused && !host.Session.Submitted;
            }
        }

        /// <summary>Why <see cref="SessionAcceptsActions"/> is false ("" when it is true): no scenario yet, paused, or finished.</summary>
        public static string UnavailableReason
        {
            get
            {
                var host = SimulationHost.Instance;
                if (host == null || host.Session == null) return EquipmentRefusalText.NoSessionCode;
                if (host.Paused || host.Session.Paused) return EquipmentRefusalText.PausedCode;
                if (host.Ended || host.Session.Submitted) return EquipmentRefusalText.FinishedCode;
                return "";
            }
        }
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
            if (!Active.Contains(this)) Active.Add(this);
            RegisterFallback();
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(Entered);
            _grab.selectFilters.Remove(this);
            _grab.selectExited.RemoveListener(Exited);
            _guide?.Hide();
            _notice.Hide();
            Active.Remove(this);
            UnregisterFallback();
        }

        private void OnDestroy() { _guide?.Dispose(); _notice.Dispose(); }

        private Vector3 TargetPosition => destination.attachTransform != null ? destination.attachTransform.position : destination.transform.position;

        /// <summary>Says why it did not attach or come off, above the patient target. Never silent: the code is shown when no words are known.</summary>
        private void Explain(string reason, bool removing = false)
        {
            if (destination == null || string.IsNullOrEmpty(reason)) return;
            _notice.Show(EquipmentRefusalText.For(reason, ModeGate.PracticeAids, removing), TargetPosition);
        }

        private void Entered(SelectEnterEventArgs args)
        {
            if (_syncing || !ReferenceEquals(args.interactorObject, destination) || _attached) return;
            var answer = Fire();
            _attached = answer != null && answer.Accepted;
            _returnHome = !_attached;
            if (!_attached) Explain(answer == null ? UnavailableReason : answer.RejectReason);
            else _notice.Hide();
        }

        private void Exited(SelectExitEventArgs args)
        {
            if (!_syncing && !args.isCanceled && !(args.interactorObject is XRSocketInteractor)
                && !_attached && restingPlace != null && Vector3.Distance(transform.position, restingPlace.position) <= ReturnRadius)
                _returnHome = true;
            if (!_syncing && !args.isCanceled && !(args.interactorObject is XRSocketInteractor) && !_attached && destination != null)
            {
                // released by the hand at a patient target without attaching: say why
                if (NearTarget(destination) && !SessionAcceptsActions) Explain(UnavailableReason);
                else foreach (var other in Active)
                    if (other != this && other.destination != null && NearTarget(other.destination)) { Explain(EquipmentRefusalText.WrongTargetCode); break; }
            }
            if (_syncing || args.isCanceled || !ReferenceEquals(args.interactorObject, destination) || !_attached) return;
            var answer = Fire("detach_sensor");
            if (answer != null && answer.Accepted)
            {
                _attached = false;
                _awaitSocketExit = true;
                _notice.Hide();
            }
            else Explain(answer == null ? UnavailableReason : answer.RejectReason, removing: true);
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!(interactor is XRSocketInteractor)) return true;
            if (!ReferenceEquals(interactor, destination)) return false;
            if (_syncing && _attached) return true; // Explicit accepted tablet restore.
            if (!_attached && !SessionAcceptsActions) return false; // nothing can record the placement: never fake a snap
            return !_awaitSocketExit && (_attached || NearSocket());
        }

        private bool NearSocket() => NearTarget(destination);

        private bool NearTarget(XRSocketInteractor socket)
        {
            var trigger = socket.GetComponent<SphereCollider>();
            float radius = trigger != null ? trigger.radius * Mathf.Max(socket.transform.lossyScale.x,
                socket.transform.lossyScale.y, socket.transform.lossyScale.z) : .065f;
            return Vector3.Distance(_grab.GetAttachTransform(socket).position,
                socket.attachTransform != null ? socket.attachTransform.position : socket.transform.position) <= radius;
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
                if (_attached && destination.attachTransform != null)
                {
                    // Follow a moving patient bone in the same LateUpdate, including offset grab anchors.
                    var attach = _grab.GetAttachTransform(destination);
                    var target = destination.attachTransform;
                    transform.rotation = target.rotation * Quaternion.Inverse(attach.rotation) * transform.rotation;
                    transform.position += target.position - attach.position;
                    var pose = new Pose(transform.position, transform.rotation);
                    _grab.SetTargetPose(pose);
                    var body = GetComponent<Rigidbody>();
                    if (body != null) { body.position = pose.position; body.rotation = pose.rotation; }
                }
                _returnHome = false;
                if (wrappedVisual != null)
                {
                    wrappedVisual.SetActive(_attached);
                    if (paramValue != "spo2" && wrappedVisual.GetComponent<ProcSense.VR.Views.CuffInflate>() == null) wrappedVisual.AddComponent<ProcSense.VR.Views.CuffInflate>();
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
                bool available = _attached || SessionAcceptsActions;
                bool near = NearSocket();
                _guide.Refresh(held, available && !_awaitSocketExit && near, Vector3.Distance(transform.position, restingPlace.position) <= ReturnRadius, available);
                if (held && near && !available) Explain(UnavailableReason);   // the amber target says "not now"; the label says why
            }
            OfferPatientArm();
            _notice.Tick();
        }

        private bool _offered;

        /// <summary>The cuff: the patient holds out the arm it goes on (the one nearest the cuff's patient target) from the moment the trainee picks the cuff up until it is back on the tray, and keeps it out while the cuff is on.</summary>
        private void OfferPatientArm()
        {
            if (paramValue != "nibp_cuff" || _grab == null || destination == null) return;
            bool want = _attached || _grab.interactorsSelecting.Any(i => !(i is XRSocketInteractor));
            if (want == _offered) return;
            var patient = FindObjectsByType<ProcSense.VR.Views.PatientMotion>(FindObjectsInactive.Exclude).FirstOrDefault(p => p.isActiveAndEnabled);
            if (patient == null) return;
            _offered = want;
            patient.offerNear = destination.attachTransform != null ? destination.attachTransform : destination.transform;
            patient.OfferArm(want);
        }
    }
}
