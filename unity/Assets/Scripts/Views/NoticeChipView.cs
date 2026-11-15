using ProcSense.VR.Auth;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// A small world-space chip showing the laptop link: connected, reconnecting, offline (grey), generation progress, quota, laptop shutting down, and the certificate-pin mismatch banner. It only reads <see cref="HeadsetNoticeSession"/>; play never
    /// depends on it. The text is rebuilt only when the state or the progress percent changes, so a steady frame allocates nothing. Sized for the headset (28 px text on a 640 x 96 canvas).
    /// </summary>
    public sealed class NoticeChipView : MonoBehaviour
    {
        public HeadsetNoticeSession Session;
        public Canvas Canvas { get; private set; }
        public string Text => _label != null ? _label.text : "";
        public bool Alert { get; private set; }

        private TMP_Text _label;
        private UnityEngine.UI.Image _bg;
        private int _key = int.MinValue;
        private string _lastPin = "";

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Laptop notices", transform, new Vector2(640, 96), 0.0006f);
            _bg = Canvas.transform.Find("Background").GetComponent<UnityEngine.UI.Image>();
            _label = UiKit.Label(Canvas.transform, "Notice", "", new Vector2(16, 8), new Vector2(608, 80), 28, TextAlignmentOptions.Left);
            Refresh();
        }

        private void Update() => Refresh();

        /// <summary>Re-reads the session; returns whether the text changed.</summary>
        public bool Refresh()
        {
            if (_label == null || Session == null) return false;
            int key = Key(Session);
            if (key == _key) return false;
            _key = key;
            var line = Session.Line();
            _label.text = line;
            Alert = TlsPin.Mismatch || (Session.Notices != null && (Session.Notices.Shutdown || Session.Notices.QuotaExhausted));
            var grey = Session.Link == NoticeLink.Offline && !Alert;
            _label.color = grey ? UiKit.DimTextColor : Alert ? UiKit.WarnColor : UiKit.TextColor;
            return true;
        }

        // Compact state key: link, flags, pin mismatch, progress percent. No strings built per frame.
        private static int Key(HeadsetNoticeSession s)
        {
            var n = s.Notices;
            int pct = n != null && n.Progress >= 0f ? Mathf.RoundToInt(n.Progress * 100f) + 1 : 0;
            return (int)s.Link | (TlsPin.Mismatch ? 4 : 0) | (n != null && n.Shutdown ? 8 : 0) | (n != null && n.QuotaExhausted ? 16 : 0) | (pct << 5);
        }
    }
}
