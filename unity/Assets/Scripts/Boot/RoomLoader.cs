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
    public sealed class RoomLoader : MonoBehaviour
    {
        public string CurrentRoom { get; private set; } = "PatientBay";
        public bool Busy { get; private set; }
        public string LastError { get; private set; } = "";
        private XROrigin _rig;
        private CanvasGroup _fade;
        private bool _cancelled;
        private readonly Dictionary<Behaviour, bool> _movement = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<GameObject, bool> _sourceRoots = new Dictionary<GameObject, bool>();

        public void InitializeOpenedRoom(Scene room)
        {
            if (Busy || !room.isLoaded) return;
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
        }

        public bool RequestRoom(string room)
        {
            if (!isActiveAndEnabled || Busy) return false;
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
                if (source.isLoaded)
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
                    if (source.isLoaded) yield return SceneManager.UnloadSceneAsync(source);
                    _sourceRoots.Clear();
                }
                yield return Fade(0);
            }
            finally
            {
                RestoreSource();
                RestoreMovement();
                if (_fade != null) _fade.alpha = 0;
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
            var go = new GameObject("TransitionFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(_rig.Camera.transform, false);
            go.transform.localPosition = Vector3.forward * Mathf.Max(0.15f, _rig.Camera.nearClipPlane + 0.05f);
            ((RectTransform)go.transform).sizeDelta = new Vector2(4, 4);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            _fade = go.GetComponent<CanvasGroup>();
            _fade.alpha = 0;
            _fade.blocksRaycasts = false;
        }

        private IEnumerator Fade(float target)
        {
            float start = _fade.alpha;
            for (float time = 0; time < 0.3f; time += Time.unscaledDeltaTime)
            { _fade.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, time / 0.3f)); yield return null; }
            _fade.alpha = target;
        }

        private void OnDisable()
        {
            // A disabled component's coroutine can finish an in-flight load and unload its result safely.
            _cancelled = true;
            if (Busy) return;
            RestoreSource();
            RestoreMovement();
            if (_fade != null) _fade.alpha = 0;
            Busy = false;
        }
    }
}
