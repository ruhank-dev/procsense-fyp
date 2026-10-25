using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>One authored nurse line of content/voice/nurse_wristband.json: its trigger keys and its words.</summary>
    [Serializable]
    public sealed class NurseLine
    {
        public string id;
        public string text;
        public string status;
        public Trigger trigger;

        [Serializable]
        public sealed class Trigger
        {
            public string nurse_state;
            public int order;
            public string effect;
            public string verb;
            public bool accepted;
            public string rejection;
        }
    }

    [Serializable] public sealed class NurseLineFile { public NurseLine[] lines; }

    /// <summary>
    /// Which nurse lines an action causes (plan p0-voice-audio-s06, Unity half): the engine says what happened (the effects "nurse:CALLED", "nurse:AT_BEDSIDE", "wristband_replaced", "allergy_band_applied", the
    /// rejection VERIFY_IDENTITY_FIRST), and this class returns the authored lines, in the order the nurse speaks them. Pure data: it adds no meaning and decides nothing.
    /// </summary>
    public static class NurseSubtitles
    {
        public static List<NurseLine> Parse(string json) => (JsonUtility.FromJson<NurseLineFile>(json)?.lines ?? new NurseLine[0]).ToList();

        public static List<NurseLine> For(IReadOnlyList<NurseLine> lines, SimAction action, ActionOutcome outcome)
        {
            var found = new List<NurseLine>();
            if (lines == null || action == null || outcome == null) return found;
            // the nurse's walk, in the order the engine published it
            foreach (var effect in outcome.Effects.Where(e => e.StartsWith("nurse:")))
            {
                string state = effect.Substring("nurse:".Length);
                foreach (var l in lines.Where(x => x.trigger != null && x.trigger.nurse_state == state).OrderBy(x => x.trigger.order))
                {
                    bool needsEffect = !string.IsNullOrEmpty(l.trigger.effect);
                    if (!needsEffect || outcome.Effects.Contains(l.trigger.effect)) found.Add(l);
                }
            }
            // the reply to the request and the lines about what the nurse did
            foreach (var l in lines)
            {
                var t = l.trigger;
                if (t == null || !string.IsNullOrEmpty(t.nurse_state)) continue;
                if (!string.IsNullOrEmpty(t.verb) && t.verb == action.Verb)
                {
                    if (!string.IsNullOrEmpty(t.rejection)) { if (!outcome.Accepted && outcome.RejectReason == t.rejection) found.Add(l); }
                    else if (outcome.Accepted == t.accepted) found.Add(l);
                }
                else if (string.IsNullOrEmpty(t.verb) && !string.IsNullOrEmpty(t.effect) && outcome.Effects.Contains(t.effect)) found.Add(l);
            }
            return found;
        }
    }

    /// <summary>
    /// Shows the nurse's lines as subtitles, one after the other, under the nurse (plan p0-voice-audio-s06): it listens to <see cref="SimulationHost.Applied"/> and shows what <see cref="NurseSubtitles"/> returns.
    /// The recorded audio of the same lines is played by the voice layer; subtitles stay on for accessibility. Presentation only: it applies no action.
    /// </summary>
    public sealed class NurseSubtitleView : MonoBehaviour
    {
        public const float SecondsPerLine = 3f;
        public Canvas Canvas { get; private set; }
        public string Current => _label != null ? _label.text : "";
        public IReadOnlyList<string> Shown => _shown;
        public int Pending => _queue.Count;
        public string LinesJson;                       // the nurse lines; when empty the view reads content/voice/nurse_wristband.json from StreamingAssets or the repository

        private readonly Queue<NurseLine> _queue = new Queue<NurseLine>();
        private readonly List<string> _shown = new List<string>();
        private List<NurseLine> _lines;
        private TMP_Text _label;
        private float _until;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Nurse subtitles", transform, new Vector2(900, 120), 0.0006f);
            _label = UiKit.Label(Canvas.transform, "Line", "", new Vector2(20, 14), new Vector2(860, 92), 30, TextAlignmentOptions.Center);
            Canvas.gameObject.SetActive(false);
        }

        private void OnEnable() { if (SimulationHost.Instance != null) SimulationHost.Instance.Applied += OnApplied; }
        private void OnDisable() { if (SimulationHost.Instance != null) SimulationHost.Instance.Applied -= OnApplied; }

        private void Update()
        {
            if (SimulationHost.Instance != null && !_subscribed) { SimulationHost.Instance.Applied += OnApplied; _subscribed = true; }
            Step(Time.unscaledDeltaTime);
        }

        private bool _subscribed;

        public void OnApplied(SimAction action, ActionOutcome outcome)
        {
            _lines = _lines ?? Load();
            foreach (var line in NurseSubtitles.For(_lines, action, outcome)) _queue.Enqueue(line);
        }

        /// <summary>Moves the queue on by this many seconds; tests call it with a number, the scene with the frame time.</summary>
        public void Step(float seconds)
        {
            _until -= seconds;
            if (_until > 0f) return;
            if (_queue.Count == 0) { if (Canvas != null) Canvas.gameObject.SetActive(false); return; }
            var next = _queue.Dequeue();
            _label.text = next.text;
            _shown.Add(next.id);
            Canvas.gameObject.SetActive(true);
            _until = SecondsPerLine;
        }

        private List<NurseLine> Load()
        {
            if (!string.IsNullOrEmpty(LinesJson)) return NurseSubtitles.Parse(LinesJson);
            foreach (var path in new[] { System.IO.Path.Combine(Application.streamingAssetsPath, "content", "voice", "nurse_wristband.json"), System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "content", "voice", "nurse_wristband.json")) })
                if (System.IO.File.Exists(path)) return NurseSubtitles.Parse(System.IO.File.ReadAllText(path));
            return new List<NurseLine>();
        }
    }
}
