using System;
using System.Collections.Generic;
using ProcSense.VR.Settings;
using UnityEngine;

namespace ProcSense.VR.UI.Theme
{
    /// <summary>One running tween: a value from 0 to 1 over a duration, eased, applied every frame until done.</summary>
    public sealed class Tween
    {
        public readonly object Owner;
        public readonly string Channel;
        public readonly float Duration;
        public readonly Func<float, float> Ease;
        public readonly Action<float> Apply;
        public readonly Action Done;
        public float Elapsed;
        public bool Finished { get; private set; }

        public Tween(object owner, string channel, float duration, Func<float, float> ease, Action<float> apply, Action done)
        { Owner = owner; Channel = channel; Duration = duration; Ease = ease; Apply = apply; Done = done; }

        /// <summary>Advances by dt seconds; returns true when it has reached the end (and applied the end value).</summary>
        public bool Step(float dt)
        {
            if (Finished) return true;
            Elapsed += dt;
            float t = Duration <= 0 ? 1f : Mathf.Clamp01(Elapsed / Duration);
            Apply(Ease(t));
            if (t < 1f) return false;
            Finished = true;
            Done?.Invoke();
            return true;
        }

        internal void Cancel() => Finished = true;
    }

    /// <summary>
    /// The UI motion library (P3-ANI-09): a small in-house tween runner (no paid package) on unscaled time, so motion keeps going while the session is
    /// paused, with the presets of the kit, each timed by the design tokens (UiTheme: open 180 ms ease-out, close 120 ms, hover 60 ms, tick 250 ms).
    /// One tween per (owner, channel): starting a new one replaces the running one, so a hover that ends mid-way never fights the next. With
    /// "reduce motion" on (P3-SET-06), decorative motion jumps straight to its end; the feedback that carries meaning (the tick drawing in, a number
    /// counting up) still plays, faster. Comfort: nothing here ever moves the camera.
    /// </summary>
    public static class UiMotion
    {
        private static readonly List<Tween> Running = new List<Tween>();
        private static Runner _runner;

        public static int Count => Running.Count;

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        public static float EaseIn(float t) => t * t * t;
        public static float Linear(float t) => t;
        /// <summary>A small dip and return (press feedback).</summary>
        public static float Dip(float t) => 1f - Mathf.Sin(t * Mathf.PI) * .06f;

        /// <summary>Starts a tween, replacing any running one on the same owner and channel. Decorative tweens finish at once when reduce motion is on.</summary>
        public static Tween Play(object owner, string channel, float duration, Func<float, float> ease, Action<float> apply, Action done = null, bool essential = false)
        {
            Stop(owner, channel);
            bool reduce = GameSettings.Current.reduceMotion;
            if (reduce && !essential) duration = 0f;
            else if (reduce) duration *= .5f;
            var tw = new Tween(owner, channel, duration, ease, apply, done);
            if (duration <= 0f) { tw.Step(0f); return tw; }
            Running.Add(tw);
            EnsureRunner();
            return tw;
        }

        public static void Stop(object owner, string channel)
        {
            for (int i = Running.Count - 1; i >= 0; i--)
                if (ReferenceEquals(Running[i].Owner, owner) && Running[i].Channel == channel) { Running[i].Cancel(); Running.RemoveAt(i); }
        }

        /// <summary>Advances every tween by dt (the runner calls it each frame; tests call it directly).</summary>
        public static void Tick(float dt)
        {
            for (int i = Running.Count - 1; i >= 0; i--)
            {
                var tw = Running[i];
                bool ownerGone = tw.Owner is UnityEngine.Object o && o == null;
                if (ownerGone || tw.Step(dt)) Running.Remove(tw);
            }
        }

        public static void Clear() { foreach (var t in Running) t.Cancel(); Running.Clear(); Rest.Clear(); }

        // ------------------------------------------------------------- presets

        private static UiTheme T => UiTheme.Current;

        // the resting scale of each animated transform, taken the first time it is animated, so a tween replaced half-way never leaves it shrunk
        private static readonly Dictionary<Transform, Vector3> Rest = new Dictionary<Transform, Vector3>();
        private static Vector3 RestScale(Transform t)
        {
            if (!Rest.TryGetValue(t, out var s)) { s = t.localScale; Rest[t] = s; }
            return s;
        }

        /// <summary>Panel opens: scales up from 92 % and fades in (180 ms, ease-out).</summary>
        public static Tween Open(Transform panel, CanvasGroup group, Action done = null)
        {
            var baseScale = RestScale(panel);
            panel.gameObject.SetActive(true);
            return Play(panel, "visibility", T.Open, EaseOut, v => { panel.localScale = baseScale * Mathf.Lerp(.92f, 1f, v); if (group != null) group.alpha = v; },
                () => { panel.localScale = baseScale; done?.Invoke(); });
        }

        /// <summary>Panel closes: fades out and shrinks a little (120 ms), then hides and restores its scale.</summary>
        public static Tween Close(Transform panel, CanvasGroup group, Action done = null)
        {
            var baseScale = RestScale(panel);
            return Play(panel, "visibility", T.Close, EaseIn, v => { panel.localScale = baseScale * Mathf.Lerp(1f, .96f, v); if (group != null) group.alpha = 1f - v; },
                () => { panel.localScale = baseScale; panel.gameObject.SetActive(false); done?.Invoke(); });
        }

        /// <summary>Hover lift: the button moves towards the viewer by a few units (60 ms); call with false when the ray leaves.</summary>
        public static Tween Hover(RectTransform button, bool on, float lift = 6f)
        {
            float from = button.anchoredPosition3D.z, to = on ? -lift : 0f;
            return Play(button, "hover", T.Hover, EaseOut, v => { var p = button.anchoredPosition3D; p.z = Mathf.Lerp(from, to, v); button.anchoredPosition3D = p; });
        }

        /// <summary>Press dip: a short scale dip and return (two hover times).</summary>
        public static Tween Press(Transform button)
        {
            var baseScale = RestScale(button);
            return Play(button, "press", T.Hover * 2f, Linear, v => button.localScale = baseScale * Dip(v), () => button.localScale = baseScale);
        }

        /// <summary>Tick draws in (250 ms): an Image filled from 0 to 1. Carries meaning, so it plays even with reduce motion (faster).</summary>
        public static Tween TickDraw(UnityEngine.UI.Image tick, Action done = null)
        {
            tick.fillAmount = 0f;
            return Play(tick, "tick", T.Tick, EaseOut, v => tick.fillAmount = v, done, essential: true);
        }

        /// <summary>A number counts up to its value (for the score), shown through `show`; essential.</summary>
        public static Tween CountUp(object owner, int to, Action<int> show, float seconds = .8f, Action done = null) =>
            Play(owner, "count", seconds, EaseOut, v => show(Mathf.RoundToInt(to * v)), () => { show(to); done?.Invoke(); }, essential: true);

        /// <summary>Toast slides up a little and fades in, stays, then fades out.</summary>
        public static Tween Toast(RectTransform toast, CanvasGroup group, float hold = 2.5f, float rise = 20f, Action done = null)
        {
            var pos = toast.anchoredPosition;
            toast.gameObject.SetActive(true);
            float total = T.Open + hold + T.Close;
            return Play(toast, "toast", total, Linear, v =>
            {
                float s = v * total;
                float a = s < T.Open ? EaseOut(s / T.Open) : s < T.Open + hold ? 1f : 1f - Mathf.Clamp01((s - T.Open - hold) / T.Close);
                if (group != null) group.alpha = a;
                toast.anchoredPosition = pos + new Vector2(0, rise * (s < T.Open ? EaseOut(s / T.Open) : 1f));
            }, () => { toast.anchoredPosition = pos; toast.gameObject.SetActive(false); done?.Invoke(); });
        }

        /// <summary>Error shake (P3-UIX-07): a short sideways shake that dies away (300 ms, 3 swings). Decorative: with reduce motion the error is shown by colour and text only.</summary>
        public static Tween Shake(RectTransform target, float amplitude = 10f, float seconds = .3f)
        {
            var pos = target.anchoredPosition;
            return Play(target, "shake", seconds, Linear, v => target.anchoredPosition = pos + new Vector2(Mathf.Sin(v * Mathf.PI * 6f) * amplitude * (1f - v), 0f),
                () => target.anchoredPosition = pos);
        }

        private static void EnsureRunner()
        {
            if (_runner != null || !Application.isPlaying) return;
            var go = new GameObject("UI motion") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update() => Tick(Time.unscaledDeltaTime);
            private void OnDestroy() { if (_runner == this) _runner = null; }
        }
    }
}
