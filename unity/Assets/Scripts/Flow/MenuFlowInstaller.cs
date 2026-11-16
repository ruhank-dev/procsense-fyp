using System;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using UnityEngine;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// Makes the game flow reachable in the shipped player (P3-02). Created by BootLoader once the real sign-in (DemoLoginView) has finished and Sys_Core has loaded. It waits for the SimulationHost, builds
    /// <see cref="MenuFlow"/> and the glass <see cref="FlowPanel"/> exactly as the PlayMode tests do, tells the flow that sign-in is done (the panel therefore opens at the consent step when a service identity is signed in,
    /// owner decision D2, then the Main Menu; never at the placeholder login screen), frees the mode lock taken by the host's fixed start-up case so Training or Test can be chosen, and keeps the panel in front of the player. Training or Test, the surgery, the drawn
    /// case and seed, Begin, Submit to OT, Results (with the Debrief) and Retry or New scenario then all go through MenuFlow. The panel is a world-space canvas, so the controller pointer works (UiKit adds the
    /// tracked-device raycaster). No scene is edited: the panel lives on this object, which lives on the Boot object.
    /// </summary>
    public sealed class MenuFlowInstaller : MonoBehaviour
    {
        public const float Distance = 1.5f;

        /// <summary>Test hook: a seed source (default is a random one per scenario).</summary>
        public static Func<ulong> SeedSource { get; set; }

        public MenuFlow Flow { get; private set; }
        public FlowPanel Panel { get; private set; }

        private GameObject _panelRoot;
        private MenuPhase _placedFor = (MenuPhase)(-1);
        private readonly System.Random _random = new System.Random();

        private void Update() => Install();

        /// <summary>Creates the flow and panel once the host exists; keeps the panel placed. Returns true when installed.</summary>
        public bool Install()
        {
            var host = SimulationHost.Instance;
            if (host == null) return false;
            if (Flow == null || Flow.Host != host)
            {
                if (_panelRoot != null) Destroy(_panelRoot);
                ModeChoice.Unlock();                                 // the host's start-up case locked the mode; nothing has been played, so the menu may choose
                Flow = new MenuFlow(host, UnityContentReader.ForThisPlatform(), SeedSource ?? NextSeed);
                _panelRoot = new GameObject("Game flow panel");
                _panelRoot.transform.SetParent(transform, false);
                Panel = _panelRoot.AddComponent<FlowPanel>();
                Panel.Build(Flow, _panelRoot.transform);
                Flow.Changed += () => _placedFor = (MenuPhase)(-1);
                Flow.SignedIn();                                     // sign-in finished before this object was made; the consent step (if any) comes next
                Place();
            }
            if (_placedFor != Flow.Phase) Place();
            return true;
        }

        private ulong NextSeed()
        {
            var b = new byte[8];
            _random.NextBytes(b);
            return BitConverter.ToUInt64(b, 0) & 0x7FFFFFFFFFFFFFFFUL;
        }

        private void Place()
        {
            _placedFor = Flow.Phase;
            if (_panelRoot == null) return;
            var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam == null) return;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            // while the patient is being prepared the panel steps to the left so the bay stays visible
            if (Flow.Phase == MenuPhase.Playing) forward = Quaternion.Euler(0f, -35f, 0f) * forward;
            var pos = cam.transform.position + forward * Distance;
            pos.y = cam.transform.position.y - 0.1f;
            _panelRoot.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, Vector3.up));
            _panelRoot.SetActive(Flow.Phase != MenuPhase.Login);
        }
    }
}
