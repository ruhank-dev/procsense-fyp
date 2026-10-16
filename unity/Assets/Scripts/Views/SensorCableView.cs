using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Visual cable only; length and motion never imply clinical attachment.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SensorCableView : MonoBehaviour
    {
        public Transform DevicePort;
        public Transform SensorPort;
        private LineRenderer _line;
        private ProcSense.VR.ActionEmitters.SensorAttachment _attachment;
        private Transform _loosePort;
        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 17;
            _line.startWidth = _line.endWidth = .004f;
        }
        private void LateUpdate()
        {
            if (_attachment == null && SensorPort != null)
            {
                _attachment = SensorPort.GetComponentInParent<ProcSense.VR.ActionEmitters.SensorAttachment>();
                if (SensorPort.parent != null) _loosePort = SensorPort.parent.Find("ATTACH_HoseLoose");
            }
            _line.enabled = DevicePort != null && SensorPort != null;
            if (!_line.enabled) return;
            var port = _attachment != null && !_attachment.Attached && _loosePort != null ? _loosePort : SensorPort;
            for (int i = 0; i < _line.positionCount; i++)
            {
                float t = i / (float)(_line.positionCount - 1);
                _line.SetPosition(i, Vector3.Lerp(DevicePort.position, port.position, t) + Vector3.down * (.16f * 4 * t * (1 - t)));
            }
        }
    }
}
