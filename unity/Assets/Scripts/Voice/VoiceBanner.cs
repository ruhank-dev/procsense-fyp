using ProcSense.VR.Auth;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Voice
{
    /// <summary>
    /// The voice availability banner (P3-VOX-10): in voice mode, when the laptop stops answering or speaks other versions (<see cref="ServiceStatusModel.VoiceAvailable"/>), a banner appears just
    /// below the line of sight with one button, "Use menus", that switches the interaction mode; the case goes on either way and nothing is blocked. It goes away by itself when voice is back.
    /// The status polls every 3 s with a 2 s timeout, so the banner follows a stopped server within 5 s.
    /// </summary>
    public sealed class VoiceBanner : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(760, 150);
        public static readonly Vector3 HeadOffset = new Vector3(0f, -.22f, 1.1f);

        public ServiceStatusModel Model;
        public Transform Head;
        public Canvas Canvas { get; private set; }
        public Button MenusButton { get; private set; }
        public TMP_Text Label { get; private set; }
        public bool Showing => Canvas != null && Canvas.enabled;

        public void Build()
        {
            if (Canvas != null) return;
            var root = new GameObject("Voice banner").transform;
            root.SetParent(transform, false);
            Canvas = Kit.Canvas("Voice banner", root, Size, HeadOffset.magnitude);
            var c = Canvas.transform;
            Kit.Panel(c, "Panel", Vector2.zero, Size);
            Kit.Icon(c, UiIcons.MicOff, new Vector2(24, 47), 56, UiTheme.Current.HintText);
            Label = Kit.Text(c, "Text", "Voice is unavailable: the laptop is not answering. The case goes on.", new Vector2(100, 16), new Vector2(400, 118), TextStyle.Body);
            Label.verticalAlignment = VerticalAlignmentOptions.Middle;
            MenusButton = Kit.Button(c, "Use menus", new Vector2(520, 39), new Vector2(216, 72), ButtonKind.Primary, UiIcons.MenuMode, () => InteractionMode.Set(InteractionModeSetting.Menu));
            Canvas.enabled = false;
        }

        /// <summary>Shows the banner when voice mode is on and voice is unavailable; keeps it below the line of sight (LateUpdate calls it; tests call it directly).</summary>
        public void Step(float dt)
        {
            if (Canvas == null) Build();
            var m = Model ?? (ServiceStatus.Instance != null ? ServiceStatus.Instance.Model : null);
            bool show = InteractionMode.Voice && m != null && m.Known && !m.VoiceAvailable;
            if (Canvas.enabled != show) Canvas.enabled = show;
            if (!show) return;
            if (m.Reachable) Label.text = PairingClient.Banner(m.Compat).Length > 0 ? PairingClient.Banner(m.Compat) : "Voice is unavailable right now. The case goes on.";
            else Label.text = "Voice is unavailable: the laptop is not answering. The case goes on.";
            var head = Head != null ? Head : (Camera.main != null ? Camera.main.transform : null);
            if (head == null) return;
            var root = Canvas.transform.parent;
            var target = head.TransformPoint(HeadOffset);
            float k = GameSettings.Current.reduceMotion ? 1f : 1f - Mathf.Exp(-6f * dt);
            root.position = Vector3.Lerp(root.position, target, k);
            root.rotation = Quaternion.LookRotation(root.position - head.position, Vector3.up);
        }

        private void LateUpdate() => Step(Time.unscaledDeltaTime);
    }
}
