using System.Collections;
using System.Collections.Generic;
using ProcSense.VR.Boot;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ProcSense.VR.XR
{
    public sealed class SafeTeleportProvider : TeleportationProvider
    {
        public bool Busy { get; private set; }
        private XROrigin _rig;
        private RoomLoader _rooms;
        private LocomotionFade _fade;
        private readonly Dictionary<LocomotionProvider, bool> _movement = new Dictionary<LocomotionProvider, bool>();

        public bool CanTeleport(Vector3 point)
        {
            if (_rig == null) _rig = GetComponentInParent<XROrigin>();
            if (_rooms == null) _rooms = FindAnyObjectByType<RoomLoader>();
            if (_rig == null || _rooms == null || _rooms.Busy || Busy || !isActiveAndEnabled) return false;
            return Clear(point);
        }

        private bool Clear(Vector3 point)
        {
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)) return false;
            int mask = ~(1 << _rig.gameObject.layer);
            if (!Physics.Raycast(point + Vector3.up * 0.2f, Vector3.down, out var hit, 0.4f, mask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.gameObject.scene.name != _rooms.CurrentRoom || Mathf.Abs(hit.point.y - point.y) > 0.08f) return false;
            if (hit.collider.name != "Floor" && !hit.collider.name.StartsWith("TELEPORT_")) return false;
            var capsule = _rig.GetComponent<CharacterController>();
            float radius = capsule.radius;
            float height = Mathf.Max(_rig.CameraInOriginSpaceHeight, radius * 2 + 0.1f);
            return !Physics.CheckCapsule(point + Vector3.up * (radius + 0.04f), point + Vector3.up * (height - radius + 0.04f), radius, mask, QueryTriggerInteraction.Ignore);
        }

        public override bool QueueTeleportRequest(TeleportRequest request)
        {
            if (!CanTeleport(request.destinationPosition)) return false;
            Busy = true;
            StartCoroutine(Teleport(request));
            return true;
        }

        private IEnumerator Teleport(TeleportRequest request)
        {
            _fade = _rig.GetComponent<LocomotionFade>();
            foreach (var provider in _rig.GetComponentsInChildren<LocomotionProvider>(true))
                if (provider != this) { _movement[provider] = provider.enabled; provider.enabled = false; }
            var desktop = FindAnyObjectByType<SimulatorLocomotion>();
            if (desktop != null) desktop.Suspended = true;
            try
            {
                yield return _fade.Fade(1, 0.15f);
                // Dynamic obstruction can appear during the fade; reject it before any movement.
                if (Clear(request.destinationPosition))
                {
                    base.QueueTeleportRequest(request);
                    while (validRequest) yield return null;
                    yield return null; // Body transformer applies its queued movement under black.
                }
                yield return _fade.Fade(0, 0.15f);
            }
            finally { Restore(); }
        }

        protected override void Update()
        {
            if (validRequest && !Clear(currentRequest.destinationPosition)) validRequest = false;
            base.Update();
        }

        private void Restore()
        {
            foreach (var entry in _movement) if (entry.Key != null) entry.Key.enabled = entry.Value;
            _movement.Clear();
            var desktop = FindAnyObjectByType<SimulatorLocomotion>();
            if (desktop != null) desktop.Suspended = false;
            if (_fade != null) _fade.Alpha = 0;
            Busy = false;
            validRequest = false;
        }

        protected override void OnDisable()
        {
            StopAllCoroutines();
            Restore();
            base.OnDisable();
        }
    }
}
