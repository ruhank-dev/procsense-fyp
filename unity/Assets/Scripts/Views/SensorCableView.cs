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
        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 17;
            _line.startWidth = _line.endWidth = .004f;
        }
        private void LateUpdate()
        {
            _line.enabled = DevicePort != null && SensorPort != null;
            if (!_line.enabled) return;
            for (int i = 0; i < _line.positionCount; i++)
            {
                float t = i / (float)(_line.positionCount - 1);
                _line.SetPosition(i, Vector3.Lerp(DevicePort.position, SensorPort.position, t) + Vector3.down * (.16f * 4 * t * (1 - t)));
            }
        }
    }
}
