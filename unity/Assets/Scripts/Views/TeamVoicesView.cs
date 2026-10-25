using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>One reply of one character to the trainee's question: who says it, the words (the patient's earned answer or a staff member's approved line) and where it came from (model, cache or fallback).</summary>
    public sealed class TeamReply
    {
        public string Role;
        public string Text;
        public string Intent = "";
        public string Source = "";
        public TeamReply(string role, string text) { Role = role; Text = text; }
    }

    /// <summary>
    /// Three voices in the scene (plan p2-ai-advanced-simulation-s03, the Unity half): the patient, the nurse and the anaesthetist answer one question one after another, never over each other, each with its own voice profile and its own place
    /// in the room. The order is fixed (patient, nurse, anaesthetist) so the same replies play the same way; a reply with no words is dropped, and so is a role the scene does not know. The words come from the dialogue service (staff
    /// speak approved lines only); this class decides nothing about the content and applies no action. The recorded audio of a line is played by whoever listens to <see cref="Spoke"/>.
    /// </summary>
    public sealed class TeamVoicesView : MonoBehaviour
    {
        public const float SecondsPerLine = 3f;
        public static readonly string[] Order = { "patient", "nurse", "anaesthesia" };          // the role names of content/ai/intents.json; "anaesthetist" is accepted as the same role
        /// <summary>The voice profile of each role (distinct on purpose: a trainee tells the speakers apart by ear).</summary>
        public static readonly Dictionary<string, string> Voices = new Dictionary<string, string> { { "patient", "voice.patient" }, { "nurse", "voice.nurse" }, { "anaesthesia", "voice.anaesthetist" } };
        private static readonly Dictionary<string, Vector3> Places = new Dictionary<string, Vector3>
        {
            { "patient", new Vector3(0f, 1.5f, 1.2f) }, { "nurse", new Vector3(-0.8f, 1.9f, 1.4f) }, { "anaesthesia", new Vector3(0.8f, 1.9f, 1.4f) },
        };

        /// <summary>Raised when a line starts: role, text, voice profile. The audio layer plays the recorded line of the same words.</summary>
        public event Action<string, string, string> Spoke;

        /// <summary>When it answers true (a recorded line is still playing) the next character waits, so the voices never overlap.</summary>
        public Func<bool> Busy;
        public IReadOnlyList<string> SpokenRoles => _spoken;
        public int Pending => _queue.Count;
        public string Current(string role) => _labels.TryGetValue(role, out var l) ? l.text : "";

        private readonly Queue<TeamReply> _queue = new Queue<TeamReply>();
        private readonly List<string> _spoken = new List<string>();
        private readonly Dictionary<string, TMP_Text> _labels = new Dictionary<string, TMP_Text>();
        private readonly Dictionary<string, Canvas> _canvases = new Dictionary<string, Canvas>();
        private float _until;
        private string _active;

        /// <summary>The replies in the order they play: the known roles in <see cref="Order"/>; a reply with no words or an unknown role is left out; the first reply of a role wins.</summary>
        public static List<TeamReply> Arrange(IEnumerable<TeamReply> replies)
        {
            var list = (replies ?? Enumerable.Empty<TeamReply>()).Where(r => r != null).Select(r => { if (r.Role == "anaesthetist") r.Role = "anaesthesia"; return r; })
                .Where(r => !string.IsNullOrWhiteSpace(r.Text) && Voices.ContainsKey(r.Role ?? "")).ToList();
            return Order.Select(role => list.FirstOrDefault(r => r.Role == role)).Where(r => r != null).ToList();
        }

        /// <summary>Queues the replies of one question; whatever was still waiting from an earlier question is dropped (the trainee has moved on).</summary>
        public void Offer(IEnumerable<TeamReply> replies)
        {
            _queue.Clear();
            foreach (var r in Arrange(replies)) _queue.Enqueue(r);
            _until = 0f;
        }

        /// <summary>Moves on by this many seconds; tests call it with a number, the scene with the frame time. Only one character speaks at a time.</summary>
        public void Step(float seconds)
        {
            _until -= seconds;
            if (_until > 0f) return;
            if (Busy != null && Busy()) return;
            if (_active != null && _canvases.TryGetValue(_active, out var old)) old.gameObject.SetActive(false);
            _active = null;
            if (_queue.Count == 0) return;
            var next = _queue.Dequeue();
            Show(next.Role).text = next.Text;
            _canvases[next.Role].gameObject.SetActive(true);
            _active = next.Role;
            _spoken.Add(next.Role);
            _until = SecondsPerLine;
            Spoke?.Invoke(next.Role, next.Text, Voices[next.Role]);
        }

        private void Update() => Step(Time.unscaledDeltaTime);

        private TMP_Text Show(string role)
        {
            if (_labels.TryGetValue(role, out var label)) return label;
            var canvas = UiKit.WorldCanvas(role + " subtitle", transform, new Vector2(700, 110), 0.0006f);
            canvas.transform.localPosition = Places[role];
            label = UiKit.Label(canvas.transform, "Line", "", new Vector2(16, 12), new Vector2(668, 86), 28, TextAlignmentOptions.Center);
            canvas.gameObject.SetActive(false);
            _labels[role] = label;
            _canvases[role] = canvas;
            return label;
        }
    }
}
