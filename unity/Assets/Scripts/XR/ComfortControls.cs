using System.Collections;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace ProcSense.VR.XR
{
    [DefaultExecutionOrder(-200)]
    public sealed class ComfortControls : MonoBehaviour
    {
        public bool Seated { get; private set; }
        private SnapTurnProvider _snap;
        public float TurnAngle { get { if (_snap == null) _snap = GetComponentInChildren<SnapTurnProvider>(true); return _snap.turnAmount; } }
        private float _standingOffset;
        private XROrigin _rig;
        private int _nextAnchor;
        private bool _login, _heightHeld;
        private bool HeadsetInput => Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive
            || (Application.isEditor && EditorInputSettings.Mode == EditorInputSettings.InputMode.Headset);

        private void Awake()
        {
            _rig = GetComponent<XROrigin>();
            _login = GetComponentInParent<ProcSense.VR.Boot.BootLoader>() != null;
            // Run before RoomLoader freezes and snapshots the providers.
            RigLocomotionSettings.Apply(_rig, EditorInputSettings.ThumbstickMovement, _login);
        }

        private IEnumerator Start()
        {
            if (_login || !HeadsetInput) yield break;
            _rig.CameraYOffset = 1.6f;
            _rig.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            // Do not calibrate against the camera's untracked startup pose.
            while (true)
            {
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (CanAdjustHeight() && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked) break;
                yield return null;
            }
            yield return new WaitForSeconds(.5f);
            CalibrateInitialHeight();
        }

        private bool CanAdjustHeight()
        {
            var teleport = GetComponentInChildren<SafeTeleportProvider>();
            var rooms = FindAnyObjectByType<ProcSense.VR.Boot.RoomLoader>();
            return !_login && teleport != null && rooms != null && !teleport.Busy && !rooms.Busy;
        }

        public void CalibrateInitialHeight()
        {
            float height = _rig.CameraInOriginSpaceHeight;
            // Seated users begin at a usable virtual standing height. Taller
            // tracked poses retain their real height; physical crouching still works.
            if (!Seated && height > .35f && height < 1.45f) SetSeated(true);
        }

        public bool ProcessHeightButton(bool pressed)
        {
            bool rising = pressed && !_heightHeld;
            _heightHeld = pressed;
            if (!rising || !CanAdjustHeight()) return false;
            SetSeated(!Seated);
            return true;
        }

        public void SetSeated(bool seated)
        {
            if (seated == Seated || !CanAdjustHeight()) return;
            _rig = GetComponent<XROrigin>();
            if (seated)
            {
                _standingOffset = _rig.CameraFloorOffsetObject.transform.localPosition.y;
                var position = _rig.CameraFloorOffsetObject.transform.localPosition;
                position.y += 1.6f - _rig.CameraInOriginSpaceHeight;
                _rig.CameraFloorOffsetObject.transform.localPosition = position;
            }
            else
            {
                var position = _rig.CameraFloorOffsetObject.transform.localPosition;
                position.y = _standingOffset;
                _rig.CameraFloorOffsetObject.transform.localPosition = position;
            }
            Seated = seated;
        }
        public void SetTurnAngle(float angle)
        {
            if (angle == 30 || angle == 45) GetComponentInChildren<SnapTurnProvider>(true).turnAmount = angle;
        }
        public bool Turn(int direction)
        {
            var provider = GetComponentInChildren<SafeTeleportProvider>();
            if (provider.Busy || FindAnyObjectByType<ProcSense.VR.Boot.RoomLoader>().Busy || (direction != -1 && direction != 1)) return false;
            GetComponentInChildren<XRBodyTransformer>().QueueTransformation(new XRBodyYawRotation { angleDelta = direction * TurnAngle });
            return true;
        }
        private void Update()
        {
            if (_login) return;
            if (HeadsetInput)
            {
                var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                ProcessHeightButton(left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool pressed) && pressed);
            }
            if (!Application.isEditor || Keyboard.current == null) return;
            if (Keyboard.current.f6Key.wasPressedThisFrame) SetSeated(!Seated);
            if (Keyboard.current.leftBracketKey.wasPressedThisFrame) Turn(-1);
            if (Keyboard.current.rightBracketKey.wasPressedThisFrame) Turn(1);
            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                var rig = GetComponent<XROrigin>();
                var anchors = FindObjectsByType<SafeTeleportAnchor>().OrderBy(a => a.name).ToArray();
                for (int i = 0; i < anchors.Length; i++)
                {
                    var next = anchors[_nextAnchor++ % anchors.Length];
                    if (Vector3.Distance(next.teleportAnchorTransform.position, Vector3.ProjectOnPlane(rig.Camera.transform.position, Vector3.up)) <= 0.5f) continue;
                    next.RequestTeleport();
                    break;
                }
            }
        }
    }
}
