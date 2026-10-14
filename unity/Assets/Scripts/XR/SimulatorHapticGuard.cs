using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Feedback;

namespace ProcSense.VR.XR
{
    /// <summary>The Editor's virtual controllers cannot receive vibration commands.</summary>
    public sealed class SimulatorHapticGuard : MonoBehaviour
    {
        private readonly List<SimpleHapticFeedback> _muted = new List<SimpleHapticFeedback>();
        private void OnEnable() => InputSystem.onDeviceChange += DeviceChanged;
        private void DeviceChanged(InputDevice device, InputDeviceChange change) => Apply();
        private void Start() => Apply();
        private void Update() => Apply();

        private void Apply()
        {
            if (!Application.isEditor) { enabled = false; return; }
            bool simulated = InputSystem.devices.Any(d => d.GetType().Name == "XRSimulatedController");
            if (!simulated) { Restore(); return; }
            foreach (var feedback in FindObjectsByType<SimpleHapticFeedback>())
            {
                if (!feedback.enabled) continue;
                _muted.Add(feedback);
                feedback.enabled = false;
            }
        }

        private void OnDisable() { InputSystem.onDeviceChange -= DeviceChanged; Restore(); }
        private void Restore()
        {
            foreach (var feedback in _muted) if (feedback != null) feedback.enabled = true;
            _muted.Clear();
        }
    }
}
