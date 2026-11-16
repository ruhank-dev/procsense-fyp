using ProcSense.VR.Auth;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.UI.Kit
{
    /// <summary>
    /// The connection status chip (P3-UIX-11): a small glass pill with a coloured dot and the words of <see cref="ServiceStatusModel.Text"/>: green "Laptop connected · Cloud AI ready",
    /// amber "Reconnecting…" or "Cloud AI unavailable · local fallback" or a version mismatch, red "Laptop not found · play in menu mode". It reads <see cref="ServiceStatus.Instance"/>
    /// (or a model given to it) and redraws only when the state changes. Shown above the game-flow menu; the wrist menu carries the dot alone.
    /// </summary>
    public sealed class ConnectionChip : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(560, 64);

        public ServiceStatusModel Model;
        public Canvas Canvas { get; private set; }
        public GlassPanel Dot { get; private set; }
        public TMP_Text Label { get; private set; }
        private ServiceStatusModel _bound;

        public static Color ColourOf(ConnectionLevel level)
        {
            var t = UiTheme.Current;
            return level == ConnectionLevel.Connected ? t.Done : level == ConnectionLevel.Degraded ? t.Hint : t.Danger;
        }

        public void Build(float distance = 1.5f)
        {
            if (Canvas != null) return;
            Canvas = Kit.Canvas("Connection chip", transform, Size, distance);
            var c = Canvas.transform;
            Kit.Panel(c, "Pill", Vector2.zero, Size, GlassPanel.Style.Dark, null, Size.y / 2);
            Dot = Kit.Panel(c, "Dot", new Vector2(20, 20), new Vector2(24, 24), GlassPanel.Style.Solid, ColourOf(ConnectionLevel.Degraded), 12);
            Label = Kit.Text(c, "Text", "", new Vector2(56, 0), new Vector2(Size.x - 72, Size.y), TextStyle.Caption, UiTheme.Current.TextPrimary);
            Label.verticalAlignment = VerticalAlignmentOptions.Middle;
            Draw();
        }

        private void Update()
        {
            var m = Model ?? (ServiceStatus.Instance != null ? ServiceStatus.Instance.Model : null);
            if (m == _bound) return;
            if (_bound != null) _bound.Changed -= Draw;
            _bound = m;
            if (_bound != null) _bound.Changed += Draw;
            Draw();
        }

        private void OnDestroy() { if (_bound != null) _bound.Changed -= Draw; }

        public void Draw()
        {
            if (Canvas == null) return;
            var m = Model ?? _bound ?? (ServiceStatus.Instance != null ? ServiceStatus.Instance.Model : null);
            var level = m != null ? m.Level : ConnectionLevel.Offline;
            Dot.color = ColourOf(level);
            Label.text = m != null ? m.Text : "No laptop configured · menu mode";
        }
    }
}
