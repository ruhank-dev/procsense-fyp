using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace ProcSense.VR.Boot
{
    /// <summary>Owns trainee room presentation. Sys_Core/DevOnly stay loaded; clinical state is untouched.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class RoomLoader : MonoBehaviour
    {
        public string CurrentRoom { get; private set; } = "PatientBay";
        public bool Busy { get; private set; }
        public string LastError { get; private set; } = "";
        public bool Continuous { get; private set; }
        private readonly List<RoomLayout> _layouts = new List<RoomLayout>();
        private XROrigin _rig;
        private LocomotionFade _fade;
        private bool _cancelled;
        private bool _initializing = true;
        private readonly Dictionary<Behaviour, bool> _movement = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<GameObject, bool> _sourceRoots = new Dictionary<GameObject, bool>();

        private void Awake()
        {
            Busy = true;
            _rig = FindAnyObjectByType<XROrigin>();
            if (_rig != null) FreezeMovement();
        }

        private IEnumerator Start()
        {
            while (!SceneManager.GetSceneByName(CurrentRoom).isLoaded) yield return null;
            Continuous = SceneManager.GetSceneByName(CurrentRoom).GetRootGameObjects()
                .Any(g => g.GetComponentInChildren<RoomLayout>() != null);
            if (Continuous)
            {
                foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
                {
                    if (SceneManager.GetSceneByName(name).isLoaded) continue;
                    if (!Application.CanStreamedLevelBeLoaded(name))
                    { LastError = "An adjoining room is unavailable. Its doors remain locked."; continue; }
                    yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                }
                _layouts.AddRange(FindObjectsByType<RoomLayout>());
                // Additive probe sets require retetrahedralization at their authored world positions.
                if (LightmapSettings.lightProbes != null) LightProbes.Tetrahedralize();
            }
            // Boot is a service shell: the visible room owns ambient lighting and baked probes.
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(CurrentRoom));
            if (_rig == null) _rig = FindAnyObjectByType<XROrigin>();
            if (_rig != null)
            {
                var scene = SceneManager.GetSceneByName(CurrentRoom);
                var anchors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomAnchor>()).ToArray();
                var anchor = anchors.FirstOrDefault(a => a.FromRoom == "Default") ?? anchors.FirstOrDefault();
                if (anchor != null)
                {
                    var controller = _rig.GetComponent<CharacterController>();
                    controller.enabled = false;
                    _rig.transform.position = anchor.transform.position;
                    _rig.transform.rotation = anchor.transform.rotation;
                    controller.enabled = true;
                    Physics.SyncTransforms();
                }
            }
            _initializing = false;
            RestoreMovement();
            Busy = false;
            UpdateLighting();
        }

        private void Update()
        {
            if (!Continuous || Busy || _rig == null || _rig.Camera == null) return;
            foreach (var layout in _layouts)
            {
                if (layout == null || layout.gameObject.scene.name == CurrentRoom || !layout.Contains(_rig.Camera.transform.position)) continue;
                CurrentRoom = layout.gameObject.scene.name;
                SceneManager.SetActiveScene(layout.gameObject.scene);
                UpdateLighting();
                break;
            }
        }

        private void UpdateLighting()
        {
            if (!Continuous) return;
            // Directional lights affect every loaded scene. Keep one clinical key, not three.
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (light.type == LightType.Directional && light.name == "Soft clinical key")
                    light.enabled = light.gameObject.scene.name == CurrentRoom;
        }

        public void InitializeOpenedRoom(Scene room)
        {
            if ((Busy && !_initializing) || !room.isLoaded) return;
            CurrentRoom = room.name;
            if (room.name == "PatientBay") return; // Keep the rig's normal bay starting pose.
            _rig = FindAnyObjectByType<XROrigin>();
            var anchor = room.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomAnchor>()).FirstOrDefault();
            if (_rig == null || anchor == null || !SafeArrival(anchor.transform.position)) return;
            var controller = _rig.GetComponent<CharacterController>();
            controller.enabled = false;
            _rig.transform.position = anchor.transform.position;
            _rig.transform.rotation = Quaternion.Euler(0, anchor.transform.eulerAngles.y, 0);
            controller.enabled = true;
            UpdateLighting();
        }

        public bool CancelPendingTransition() { if (!Busy || _initializing) return false; _cancelled = true; return true; }

        public bool RequestRoom(string room)
        {
            if (!isActiveAndEnabled || Busy) return false;
            var teleport = FindAnyObjectByType<SafeTeleportProvider>();
            if (teleport != null && teleport.Busy) return false;
            LastError = "";
            if (!Allowed(CurrentRoom, room) || !Application.CanStreamedLevelBeLoaded(room))
            { LastError = "This route is unavailable."; return false; }
            _rig = FindAnyObjectByType<XROrigin>();
            if (_rig == null) { LastError = "Player rig is unavailable."; return false; }
            Busy = true;
            _cancelled = false;
            StartCoroutine(Transition(room));
            return true;
        }

        private static bool Allowed(string from, string to) =>
            (from == "PatientBay" && to == "Corridor") ||
            (from == "Corridor" && (to == "PatientBay" || to == "OT")) ||
            (from == "OT" && to == "Corridor");

        private IEnumerator Transition(string room)
        {
            var source = SceneManager.GetSceneByName(CurrentRoom);
            var destination = SceneManager.GetSceneByName(room);
            bool loadedHere = !destination.isLoaded;
            FreezeMovement();
            EnsureFade();
            try
            {
                yield return Fade(1);
                if (loadedHere)
                {
                    var operation = SceneManager.LoadSceneAsync(room, LoadSceneMode.Additive);
                    if (operation != null) yield return operation;
                    destination = SceneManager.GetSceneByName(room);
                }
                if (_cancelled)
                {
                    if (loadedHere && destination.isLoaded) yield return SceneManager.UnloadSceneAsync(destination);
                    yield break;
                }
                if (source.isLoaded && !Continuous)
                    foreach (var root in source.GetRootGameObjects())
                    { _sourceRoots[root] = root.activeSelf; root.SetActive(false); }
                Physics.SyncTransforms();
                var anchors = destination.isLoaded ? destination.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<RoomAnchor>(true)).ToArray() : new RoomAnchor[0];
                var anchor = anchors.FirstOrDefault(a => a.FromRoom == CurrentRoom) ?? anchors.FirstOrDefault(a => a.FromRoom == "Default");
                if (anchor == null || !SafeArrival(anchor.transform.position))
                {
                    LastError = "Arrival point is missing or blocked. You remain in the previous room.";
                    RestoreSource();
                    if (loadedHere && destination.isLoaded) yield return SceneManager.UnloadSceneAsync(destination);
                }
                else
                {
                    var controller = _rig.GetComponent<CharacterController>();
                    controller.enabled = false;
                    // Keep tracked camera offset while placing the user's ground position at the anchor.
                    float yaw = anchor.transform.eulerAngles.y - _rig.Camera.transform.eulerAngles.y;
                    _rig.transform.Rotate(0, yaw, 0, Space.World);
                    var offset = Vector3.ProjectOnPlane(_rig.Camera.transform.position - _rig.transform.position, Vector3.up);
                    _rig.transform.position = anchor.transform.position - offset;
                    controller.enabled = true;
                    Physics.SyncTransforms();
                    SceneManager.SetActiveScene(destination);
                    CurrentRoom = room;
                    if (source.isLoaded && !Continuous) yield return SceneManager.UnloadSceneAsync(source);
                    _sourceRoots.Clear();
                    UpdateLighting();
                }
                yield return Fade(0);
            }
            finally
            {
                RestoreSource();
                RestoreMovement();
                if (_fade != null) _fade.Alpha = 0;
                Busy = false;
            }
        }

        private bool SafeArrival(Vector3 position)
        {
            var controller = _rig.GetComponent<CharacterController>();
            if (controller == null) return false;
            int mask = ~(1 << _rig.gameObject.layer);
            if (!Physics.Raycast(position + Vector3.up * 0.15f, Vector3.down, 0.3f, mask, QueryTriggerInteraction.Ignore)) return false;
            float radius = controller.radius;
            float height = Mathf.Max(_rig.CameraInOriginSpaceHeight, radius * 2 + 0.1f);
            return !Physics.CheckCapsule(position + Vector3.up * (radius + 0.05f),
                position + Vector3.up * (height - radius + 0.05f), radius, mask, QueryTriggerInteraction.Ignore);
        }

        private void FreezeMovement()
        {
            foreach (var provider in _rig.GetComponentsInChildren<LocomotionProvider>(true))
            { _movement[provider] = provider.enabled; provider.enabled = false; }
            var desktop = FindAnyObjectByType<SimulatorLocomotion>();
            // Leave the simulator adapter enabled so raw tracked-pose walking remains suppressed.
            if (desktop != null) desktop.Suspended = true;
        }

        private void RestoreMovement()
        {
            foreach (var entry in _movement) if (entry.Key != null) entry.Key.enabled = entry.Value;
            _movement.Clear();
            var desktop = FindAnyObjectByType<SimulatorLocomotion>();
            if (desktop != null) desktop.Suspended = false;
        }

        private void RestoreSource()
        {
            foreach (var entry in _sourceRoots) if (entry.Key != null) entry.Key.SetActive(entry.Value);
            _sourceRoots.Clear();
        }

        private void EnsureFade()
        {
            if (_fade != null) return;
            _fade = _rig.GetComponent<LocomotionFade>();
        }

        private IEnumerator Fade(float target)
        {
            yield return _fade.Fade(target, 0.3f);
        }

        private void OnDisable()
        {
            // A disabled component's coroutine can finish an in-flight load and unload its result safely.
            _cancelled = true;
            if (Busy) return;
            RestoreSource();
            RestoreMovement();
            if (_fade != null) _fade.Alpha = 0;
            Busy = false;
        }
    }
}
