using ProcSense.Core;
using ProcSense.Core.Logging;
using ProcSense.Core.Scoring;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.UI.Debrief
{
    /// <summary>
    /// The evidence drawer (p0-vr-interaction-ui-s09): for the item tapped, its sentence, the log entries behind it (time, sequence number, what the trainee did and with what, whether the engine took it), the
    /// rule and the source register ID. An item with no entry says so: that is a scorer fix, not a screen one.
    /// </summary>
    public sealed class EvidenceDrawer
    {
        private readonly TMP_Text _title, _body;
        public string Title => _title.text;
        public string Text => _body.text;

        public EvidenceDrawer(Transform root, Vector2 topLeft, Vector2 size)
        {
            UiKit.Panel(root, "Drawer", topLeft, size, new Color(0.13f, 0.16f, 0.22f, 1f));
            _title = UiKit.Label(root, "Drawer title", "Tap an item or a marker to see its evidence", topLeft + new Vector2(12, 8), new Vector2(size.x - 24, 34), 18);
            _body = UiKit.Label(root, "Drawer text", "", topLeft + new Vector2(12, 48), new Vector2(size.x - 24, size.y - 56), 14);
        }

        public void Show(Session s, DebriefLine line)
        {
            _title.text = line.Severity.ToUpperInvariant() + "  " + line.Label;
            _body.text = DebriefText.Drawer(s, line);
        }

        public void ShowEntry(Session s, LogEntry e)
        {
            _title.text = "Log entry #" + e.Seq;
            _body.text = DebriefText.Evidence(s, e);
        }
    }
}
