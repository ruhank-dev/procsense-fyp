using ProcSense.VR.Settings;
using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// P3-VOX-03: in voice mode the persistent action menus are hidden: the tablet's canvases stop drawing (the tablet itself keeps running, so push-to-talk still reads which actions are enabled),
    /// the wrist menu's Tablet button is hidden, and the contextual panel becomes a reference card (ContextPanel). The allowed exceptions stay: Submit patient to OT, confirmations, the pause and
    /// settings buttons and the "Use menus" banner. Switching back to menu mode shows them again at once.
    /// </summary>
    public sealed class VoiceModeMenus : MonoBehaviour
    {
        public bool Hidden { get; private set; }

        private void OnEnable() { GameSettings.Changed += OnSettings; Apply(); }
        private void OnDisable() { GameSettings.Changed -= OnSettings; if (Hidden) Show(true); }
        private void OnSettings(SettingsData _) => Apply();

        private float _next;
        private void Update() { if (Time.unscaledTime >= _next) { _next = Time.unscaledTime + 1f; Apply(); } }   // a tablet built after the switch is caught within a second

        public void Apply()
        {
            bool voice = InteractionMode.Voice;
            if (voice || Hidden) Show(!voice);   // in menu mode nothing is touched unless this hid it, so other code's own hiding is left alone
            if (ContextPanel.Open != null && ContextPanel.Open.ReferenceCard != _cardShown) { _cardShown = ContextPanel.Open.ReferenceCard; ContextPanel.Open.Redraw(); }
        }

        private bool _cardShown;

        private void Show(bool menus)
        {
            Hidden = !menus;
            foreach (var tablet in FindObjectsByType<ProcSense.VR.Views.TabletView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var c in tablet.GetComponentsInChildren<Canvas>(true)) c.enabled = menus;
            var wrist = FindAnyObjectByType<ProcSense.VR.UI.Kit.WristMenu>();
            if (wrist != null && wrist.TabletButton != null) wrist.TabletButton.gameObject.SetActive(menus);
        }
    }
}
