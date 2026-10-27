using System.Collections.Generic;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Scoring;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Debrief
{
    /// <summary>
    /// The debrief's timeline (p0-vr-interaction-ui-s09, T-DU1): a strip from the start of the session to its end with a marker for each scored item at the moment it happened (red for a penalty, green for a
    /// credit), each linked to the log entry it came from, and under it the lateness of anything done after its window (for example the sepsis antibiotics), with the source.
    /// </summary>
    public sealed class TimelineView
    {
        private readonly Transform _root;
        private readonly Vector2 _at, _size;
        private readonly DebriefView _owner;
        private readonly List<(Button button, int seq)> _markers = new List<(Button, int)>();
        private readonly TMP_Text _late;

        public IReadOnlyList<int> MarkerSeqs => _markers.Select(m => m.seq).ToList();
        public string LatenessText => _late.text;

        public TimelineView(Transform root, Vector2 topLeft, Vector2 size, DebriefView owner)
        {
            _root = root; _at = topLeft; _size = size; _owner = owner;
            UiKit.Panel(root, "Timeline strip", topLeft + new Vector2(0, 20), new Vector2(size.x, 6), UiKit.DimTextColor);
            _late = UiKit.Label(root, "Lateness", "", topLeft + new Vector2(0, 52), new Vector2(size.x, 36), 14, TextAlignmentOptions.MidlineLeft, UiKit.WarnColor);
        }

        public void Draw(Session s, DebriefFacts f)
        {
            foreach (var m in _markers) Object.Destroy(m.button.gameObject);
            _markers.Clear();
            long end = System.Math.Max(1, s.Clock.Steps);
            foreach (var t in f.Timeline.Where(t => t.Kind == "item" && t.Seq >= 0))
            {
                var line = f.Lines.FirstOrDefault(l => l.Item == t.Text);
                float x = _at.x + (_size.x - 16) * Mathf.Clamp01(t.Step / (float)end);
                var b = UiKit.MakeButton(_root, "", new Vector2(x, _at.y + 8), new Vector2(16, 30), 10f);
                var c = b.colors;
                c.normalColor = line != null && line.Points > 0 ? new Color(0.2f, 0.7f, 0.35f) : new Color(0.85f, 0.25f, 0.2f);
                b.colors = c;
                int seq = t.Seq;
                b.onClick.AddListener(() => _owner.OpenSeq(seq));
                _markers.Add((b, seq));
            }
            _late.text = f.Lateness.Count == 0 ? "" : string.Join("   ", f.Lateness.Select(l => l.What.Replace("_", " ") + ": " + DebriefText.Minutes(l.Minutes) + " late" + (l.Source.Length > 0 ? " [" + l.Source + "]" : "")));
        }
    }
}
