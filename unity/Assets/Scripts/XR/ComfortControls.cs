using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace ProcSense.VR.XR
{
    public sealed class ComfortControls : MonoBehaviour
    {
        public bool Seated { get; private set; }
        private SnapTurnProvider _snap;
        public float TurnAngle { get { if (_snap == null) _snap = GetComponentInChildren<SnapTurnProvider>(true); return _snap.turnAmount; } }
        private float _standingOffset;
        private XROrigin _rig;
        private int _nextAnchor;
        public void SetSeated(bool seated)
        {
            if (seated == Seated || GetComponentInChildren<SafeTeleportProvider>().Busy || FindAnyObjectByType<ProcSense.VR.Boot.RoomLoader>().Busy) return;
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
