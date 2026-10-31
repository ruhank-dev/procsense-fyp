using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.VR.Boot
{
    /// <summary>A hand-driven hinge. Only the handles are selectable; the leaf remains solid.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class SwingDoor : MonoBehaviour
    {
        public string Connection;
        public string RoomA;
        public string RoomB;
        public BoxCollider Leaf;
        public float Angle { get; private set; }
        public bool NpcPassageRequested => _npcPassage;
        public bool Ready => _rooms != null && !_rooms.Busy
            && UnityEngine.SceneManagement.SceneManager.GetSceneByName(RoomA).isLoaded
            && UnityEngine.SceneManagement.SceneManager.GetSceneByName(RoomB).isLoaded;
        private Rigidbody _body;
        private XRGrabInteractable _grab;
        private RoomLoader _rooms;
        private IXRSelectInteractor _hand;
        private Quaternion _closed;
        private Vector3 _lastDirection;
        private float _target;
        private bool _npcPassage;
        private float _beforeNpc;
        public void SetNpcPassage(bool open) { if (open == _npcPassage) return; if (open) _beforeNpc = _target; _npcPassage = open; if (_hand == null) _target = open ? 100 : _beforeNpc; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _grab = GetComponent<XRGrabInteractable>();
            _closed = transform.localRotation;
            _grab.selectEntered.AddListener(BeginGrab);
            _grab.selectExited.AddListener(EndGrab);
            // Activate/trigger while holding offers a one-handed accessible open/close.
            _grab.activated.AddListener(Activate);
        }

        private void BeginGrab(SelectEnterEventArgs args)
        {
            _hand = args.interactorObject;
            _lastDirection = Direction();
            _target = Angle;
        }

        private void EndGrab(SelectExitEventArgs args) { _hand = null; _target = Angle; }
        private void Activate(ActivateEventArgs args) => Toggle();

        private Vector3 Direction() => _hand == null ? Vector3.zero
            : Vector3.ProjectOnPlane(_hand.GetAttachTransform(_grab).position - transform.position, transform.up).normalized;

        public bool Toggle()
        {
            if (!Ready || !isActiveAndEnabled) return false;
            _target = Mathf.Abs(Angle) < 35 ? -100 : 0;
            return true;
        }

        private void Update()
        {
            if (_rooms == null) _rooms = FindAnyObjectByType<RoomLoader>();
            // A closed solid leaf gates an incomplete preload, never a void beyond an open door.
            if (_grab.enabled != Ready) _grab.enabled = Ready;
            if (_npcPassage && _hand == null && Ready) _target = 100;
            if (_hand == null || !Ready) return;
            var direction = Direction();
            if (direction.sqrMagnitude < .5f || _lastDirection.sqrMagnitude < .5f)
            { _lastDirection = direction; return; }
            float delta = Vector3.SignedAngle(_lastDirection, direction, transform.up);
            _lastDirection = direction;
            _target = Mathf.Clamp(_target + delta, -105, 105);
        }

        private void FixedUpdate()
        {
            if (!Ready) return;
            var next = Mathf.MoveTowards(Angle, _target, 160 * Time.fixedDeltaTime);
            // Small angular steps prevent a held/activated door sweeping through the player.
            while (Mathf.Abs(next - Angle) > .01f)
            {
                float step = Mathf.MoveTowards(Angle, next, 1);
                var rotation = (transform.parent != null ? transform.parent.rotation : Quaternion.identity)
                    * _closed * Quaternion.Euler(0, step, 0);
                var center = transform.position + rotation * Leaf.transform.localPosition;
                var overlaps = Physics.OverlapBox(center, Vector3.Scale(Leaf.size, Leaf.transform.lossyScale) * .5f + Vector3.one * .025f,
                    rotation, ~0, QueryTriggerInteraction.Ignore);
                bool playerInWay = false;
                foreach (var hit in overlaps) if (hit is CharacterController) { playerInWay = true; break; }
                if (playerInWay) { _target = Angle; break; }
                Angle = step;
            }
            _body.MoveRotation((transform.parent != null ? transform.parent.rotation : Quaternion.identity)
                * _closed * Quaternion.Euler(0, Angle, 0));
        }

        private void OnDisable() { _hand = null; _target = Angle; }
        private void OnDestroy()
        {
            if (_grab == null) return;
            _grab.selectEntered.RemoveListener(BeginGrab);
            _grab.selectExited.RemoveListener(EndGrab);
            _grab.activated.RemoveListener(Activate);
        }
    }
}
