using ProcSense.VR.Settings;
using ProcSense.VR.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Kit
{
    /// <summary>
    /// The feedback components of the kit (P3-UIX-07), used by the training board (TRN), voice mode (VOX) and the results (RES): the tick that draws itself when a step is
    /// done, the error shake, the listening ring of push-to-talk, and the toast and progress ring (in <see cref="Kit"/>). Everything reads the theme; reduce motion keeps the
    /// meaning (tick, colour, text) and drops the decoration (shake, pulse, spin).
    /// </summary>
    public static class KitFeedback
    {
        /// <summary>A tick that draws in from left to right when <see cref="Draw"/> is called (hidden until then). Coloured with the done colour unless given.</summary>
        public static Image Tick(Transform parent, Vector2 topLeft, float size, Color? color = null)
        {
            var img = Kit.Icon(parent, UiIcons.Confirm, topLeft, size, color ?? UiTheme.Current.DoneText);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0f;
            img.name = "Tick";
            return img;
        }

        /// <summary>Draws a tick in (250 ms; essential, so it plays faster but still plays with reduce motion).</summary>
        public static void Draw(Image tick, System.Action done = null) => UiMotion.TickDraw(tick, done);

        /// <summary>The error shake of a control or panel, and its text turned to the danger colour when given.</summary>
        public static void Error(RectTransform target, TMPro.TMP_Text message = null)
        {
            if (message != null) message.color = UiTheme.Current.DangerText;
            UiMotion.Shake(target);
        }

        /// <summary>The listening ring: hidden when idle, a pulsing full ring while listening (its width follows the microphone level), a turning arc while the speech is recognised.</summary>
        public static KitListening Listening(Transform parent, Vector2 topLeft, float diameter, string icon = null)
        {
            var root = new GameObject("Listening", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            Kit.Place(root, topLeft, new Vector2(diameter, diameter));
            var ring = Kit.Ring(root, Vector2.zero, diameter, 1f, UiTheme.Current.ActionText, 6f);
            var mic = Kit.Icon(root, icon ?? UiIcons.Mic, new Vector2(diameter * .3f, diameter * .3f), diameter * .4f, UiTheme.Current.TextPrimary);
            var l = root.gameObject.AddComponent<KitListening>();
            l.Init(ring, mic);
            return l;
        }
    }

    /// <summary>The state of the listening ring (P3-UIX-07, used by P3-VOX-09).</summary>
    public sealed class KitListening : MonoBehaviour
    {
        public enum Mode { Idle, Listening, Processing }

        public Mode State { get; private set; } = Mode.Idle;
        /// <summary>The microphone level, 0 to 1 (thickens the ring while listening).</summary>
        public float Level { get; set; }
        public ProgressRing Ring { get; private set; }
        private Image _mic;
        private float _t;

        public void Init(ProgressRing ring, Image mic) { Ring = ring; _mic = mic; Set(Mode.Idle); }

        public void Set(Mode mode)
        {
            State = mode;
            bool on = mode != Mode.Idle;
            Ring.enabled = on;
            if (_mic != null) _mic.enabled = on && _mic.sprite != null;
            Ring.Value = mode == Mode.Processing ? .25f : 1f;
            Ring.rectTransform.localRotation = Quaternion.identity;
            Ring.rectTransform.localScale = Vector3.one;
            _t = 0;
        }

        /// <summary>Advances the pulse or the spin (Update calls it with the frame time; tests call it directly).</summary>
        public void Step(float dt)
        {
            if (State == Mode.Idle || GameSettings.Current.reduceMotion) return;
            _t += dt;
            if (State == Mode.Listening)
            {
                float pulse = 1f + .06f * (.5f + .5f * Mathf.Sin(_t * Mathf.PI * 2f / 1.2f));
                Ring.rectTransform.localScale = Vector3.one * pulse;
                Ring.Thickness = Mathf.Lerp(4f, 12f, Mathf.Clamp01(Level));
            }
            else Ring.rectTransform.localRotation = Quaternion.Euler(0, 0, -_t * 360f / .9f);   // one turn every 0.9 s, clockwise
        }

        private void Update() => Step(Time.unscaledDeltaTime);
    }
}
