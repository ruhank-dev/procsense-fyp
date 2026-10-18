using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace ProcSense.VR.Login
{
    /// <summary>Stationary launch UI, independent of room locomotion and delayed XR startup.</summary>
    public sealed class LoginPresentation : MonoBehaviour
    {
        private Camera _camera;
        private Canvas _canvas;
        private bool _headset, _placed;
        private int _settleFrames;
        private readonly List<XRInputSubsystem> _tracking = new List<XRInputSubsystem>();

        public static void FreezeRig(GameObject rig)
        {
            foreach (var provider in rig.GetComponentsInChildren<LocomotionProvider>(true)) provider.enabled = false;
            foreach (var body in rig.GetComponentsInChildren<XRBodyTransformer>(true)) body.enabled = false;
            foreach (var controller in rig.GetComponentsInChildren<CharacterController>(true)) controller.enabled = false;
        }

        public void Configure(Camera camera, DemoLoginView view, bool headset)
        {
            _camera = camera; _canvas = GetComponent<Canvas>(); _headset = headset;
            _canvas.worldCamera = camera;
            if (!headset)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.planeDistance = .3f;
                return;
            }
            _canvas.renderMode = RenderMode.WorldSpace;
            GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            ((RectTransform)transform).sizeDelta = new Vector2(1000, 1300);
            transform.localScale = Vector3.one * .001f;
            transform.Find("Footer")?.gameObject.SetActive(false);
            transform.Find("Ambient teal")?.gameObject.SetActive(false);
            // Do not anchor to the untracked boot camera's default pose.
            _canvas.enabled = false;
            gameObject.AddComponent<LoginKeyboard>().Configure(view);
            SubsystemManager.GetSubsystems(_tracking);
            foreach (var tracking in _tracking) tracking.trackingOriginUpdated += Recenter;
        }

        private void Recenter(XRInputSubsystem subsystem) { _placed = false; _settleFrames = 0; }

        private void LateUpdate()
        {
            if (!_headset || _placed || _camera == null) return;
            if (Application.platform == RuntimePlatform.Android)
            {
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (!head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) return;
            }
            if (_settleFrames++ < 3) return;
            PlaceInFront(_camera.transform);
        }

        public void PlaceInFront(Transform head)
        {
            // XR may have finished initializing after Configure ran.
            if (_tracking.Count == 0)
            {
                SubsystemManager.GetSubsystems(_tracking);
                foreach (var tracking in _tracking) tracking.trackingOriginUpdated += Recenter;
            }
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            forward.Normalize();
            transform.SetPositionAndRotation(head.position + forward * 1.6f + Vector3.down * .12f,
                Quaternion.LookRotation(forward, Vector3.up));
            _canvas.enabled = true;
            _placed = true;
        }

        private void OnDestroy()
        {
            foreach (var tracking in _tracking) tracking.trackingOriginUpdated -= Recenter;
        }
    }
}
