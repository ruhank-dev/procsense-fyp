using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace ProcSense.VR.XR
{
    /// <summary>
    /// Editor-only FPS navigation through the same constrained body path as XR locomotion.
    /// The toolkit simulator normally translates tracked poses, bypassing all room colliders.
    /// Controller pose manipulation stays available for testing reach and interactions.
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    [RequireComponent(typeof(XRDeviceSimulator))]
    public sealed class SimulatorLocomotion : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.5f;
        private XRDeviceSimulator _simulator;
        private XRBodyTransformer _body;
        private XROrigin _origin;
        private float _bodyMultiplier;
        private readonly XROriginMovement _movement = new XROriginMovement();
        public bool Suspended { get; set; }
        private ProcSense.VR.Boot.RoomLoader _rooms;

        private void Awake()
        {
            // Also covers Play from the editable bay, where DevOnly is already open.
            // Run before the sample creates virtual HMD/controllers so they cannot steal tracking.
            if (Application.isEditor && !EditorInputSettings.ShouldUseSimulator)
                gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (!Application.isEditor) { enabled = false; return; }
            _simulator = GetComponent<XRDeviceSimulator>();
            _bodyMultiplier = _simulator.keyboardBodyTranslateMultiplier;
            _simulator.keyboardBodyTranslateMultiplier = 0f;
        }

        private void OnDisable()
        {
            if (_simulator != null) _simulator.keyboardBodyTranslateMultiplier = _bodyMultiplier;
        }

        private void Update()
        {
            if (_rooms == null) _rooms = FindAnyObjectByType<ProcSense.VR.Boot.RoomLoader>();
            if (_rooms != null && _rooms.Busy) return;
            if (_body == null)
            {
                _body = FindFirstObjectByType<XRBodyTransformer>();
                _origin = FindFirstObjectByType<XROrigin>();
            }
            if (Suspended || _body == null || _origin == null || !_simulator.manipulatingFPS || Time.time <= 1f) return;
            var input = new Vector2(
                _simulator.keyboardXTranslateAction != null ? _simulator.keyboardXTranslateAction.action.ReadValue<float>() : 0f,
                _simulator.keyboardZTranslateAction != null ? _simulator.keyboardZTranslateAction.action.ReadValue<float>() : 0f);
            if (input == Vector2.zero) return;
            var forward = Vector3.ProjectOnPlane(_origin.Camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = _origin.transform.forward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            _movement.motion = (right * input.x + forward * input.y).normalized * (moveSpeed * Time.deltaTime);
            // Small downward motion grounds the capsule; FPS navigation cannot fly out of the room.
            _movement.motion += Vector3.down * (2f * Time.deltaTime);
            _body.QueueTransformation(_movement);
        }
    }
}
