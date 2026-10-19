using System.Collections.Generic;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// A flat EMR panel for the tablet or a wall screen (plan p0-patient-records-emr-s04 and s05, Unity half): a row of tab buttons, each labelled with its unopened-document count, and the text of the chosen tab (<see cref="EmrText"/>). It reads the running session's records
    /// and changes nothing in Core; opening a document is still the action `open_document`. It is NOT placed in any scene: where it hangs, how it looks and the headset check that the Labs badge appears when the patient returns are Sannan's (UX-P0-EMR).
    /// </summary>
    public class EmrPanelView : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public string SelectedTab { get; private set; } = "";
        public string BodyText => _body != null ? _body.text : "";
        public IReadOnlyList<Button> TabButtons => _buttons;
        public IReadOnlyList<string> TabLabels { get { var l = new List<string>(); foreach (var b in _buttons) l.Add(b.GetComponentInChildren<TMP_Text>().text); return l; } }
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<string> _ids = new List<string>();
        private TMP_Text _body;

        public void Build(Session session)
        {
            if (Canvas != null || session?.Records == null) return;
            Canvas = UiKit.WorldCanvas("EMR", transform, new Vector2(1000, 640), 0.0006f);
            float x = 12;
            foreach (var tab in session.Records.Tabs)
            {
                var button = UiKit.MakeButton(Canvas.transform, tab.Label, new Vector2(x, 10), new Vector2(136, 56), 18f);
                string id = tab.Id;
                button.onClick.AddListener(() => { SelectedTab = id; Refresh(); });
                _buttons.Add(button);
                _ids.Add(id);
                x += 140;
            }
            _body = UiKit.Label(Canvas.transform, "Body", "", new Vector2(16, 84), new Vector2(968, 540), 24);
            SelectedTab = _ids.Count > 0 ? _ids[0] : "";
            Refresh();
        }

        private void Update() => Refresh();

        public void Refresh()
        {
            var session = SimulationHost.Instance != null ? SimulationHost.Instance.Session : _session;
            if (_body == null || session?.Records == null) return;
            var tabs = EmrText.Build(session.Records);
            for (int i = 0; i < tabs.Count && i < _buttons.Count; i++)
            {
                _buttons[i].GetComponentInChildren<TMP_Text>().text = tabs[i].Label;
                if (tabs[i].Id == SelectedTab) _body.text = tabs[i].Body;
            }
        }

        private Session _session;
        /// <summary>For tests and previews that have no running host: read this session instead of the host's.</summary>
        public void Use(Session session) { _session = session; Build(session); }
    }
}
