using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Events;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The visible cards for events that are happening (plan p1-theatre-extension-s02, Unity half): one line per active cue of the Event Director (what the monitor, the patient or the nurse shows), headed by what it is, and nothing else. A cue is the engine's wording;
    /// the view adds no meaning, no hint and no timing. It is NOT placed in any scene; where the cards appear and the headset check (DC-06) are Sannan's.
    /// </summary>
    public class EventCardsView : MonoBehaviour
    {
        public const string NoEvents = "";
        public Canvas Canvas { get; private set; }
        public string Text => _text != null ? _text.text : "";
        private TMP_Text _text;

        public static string Render(IReadOnlyList<ObservedCue> cues) =>
            cues == null || cues.Count == 0 ? NoEvents : string.Join("\n", cues.Select(c => c.Text));

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("EventCards", transform, new Vector2(520, 200), 0.0006f);
            _text = UiKit.Label(Canvas.transform, "Cues", "", new Vector2(16, 12), new Vector2(488, 176), 26, TextAlignmentOptions.TopLeft, UiKit.WarnColor);
        }

        public void Show(IReadOnlyList<ObservedCue> cues)
        {
            Build();
            _text.text = Render(cues);
            Canvas.gameObject.SetActive(cues != null && cues.Count > 0);
        }

        private void Update()
        {
            var host = SimulationHost.Instance;
            if (host != null && host.Observable != null) Show(host.Observable.Cues);
        }
    }
}
