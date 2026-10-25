using ProcSense.VR.Bridge;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Puts the clinical panels in the scene (plans p0-patient-records-emr-s04 and s05, p1-theatre-extension-s02, p0-diagnostic-transitions-s04 and s05): the EMR panel on a wall of the bay, the event
    /// cards above the patient and the diagnostics props, as soon as a session is running. Each panel is placed at a scene anchor named EmrPanelAnchor, EventCardsAnchor or DiagnosticsAnchor when the
    /// scene has one (so Sannan can move them in the editor); otherwise at a default place in the bay. It lives on the persistent Sys_Core object, creates each panel once, and applies no action.
    /// </summary>
    public sealed class ClinicalPanelsInstaller : MonoBehaviour
    {
        public static readonly Vector3 EmrDefault = new Vector3(-2.2f, 1.5f, 1.0f);
        public static readonly Vector3 CardsDefault = new Vector3(0f, 2.1f, 0.6f);
        public static readonly Vector3 DiagnosticsDefault = new Vector3(1.2f, 0f, 1.6f);

        public EmrPanelView Emr { get; private set; }
        public EventCardsView Cards { get; private set; }
        public DiagnosticsSceneView Diagnostics { get; private set; }
        public NurseSubtitleView Subtitles { get; private set; }
        public TeamVoicesView Team { get; private set; }
        public TeamConversation Conversation { get; private set; }
        public PushToTalk Talk { get; private set; }
        public static readonly Vector3 TalkDefault = new Vector3(-0.6f, 1.2f, 1.1f);
        public static readonly Vector3 SubtitlesDefault = new Vector3(0.6f, 1.9f, 1.4f);

        private void Update() => Install();

        /// <summary>Creates whatever is missing; returns true when all three panels exist.</summary>
        public bool Install()
        {
            var host = SimulationHost.Instance;
            if (host == null || host.Session == null) return false;
            if (Emr == null)
            {
                var go = Place("EMR panel", "EmrPanelAnchor", EmrDefault, Quaternion.Euler(0f, 90f, 0f));
                Emr = go.AddComponent<EmrPanelView>();
                Emr.Build(host.Session);
            }
            if (Cards == null)
            {
                var go = Place("Event cards", "EventCardsAnchor", CardsDefault, Quaternion.identity);
                Cards = go.AddComponent<EventCardsView>();
                Cards.Build();
            }
            if (Diagnostics == null)
            {
                var go = Place("Diagnostics props", "DiagnosticsAnchor", DiagnosticsDefault, Quaternion.identity);
                Diagnostics = go.AddComponent<DiagnosticsSceneView>();
            }
            if (Subtitles == null)
            {
                var go = Place("Nurse subtitles", "NurseSubtitleAnchor", SubtitlesDefault, Quaternion.identity);
                Subtitles = go.AddComponent<NurseSubtitleView>();
            }
            if (Team == null)
            {
                var go = Place("Team voices", "TeamVoicesAnchor", Vector3.zero, Quaternion.identity);
                Team = go.AddComponent<TeamVoicesView>();
            }
            if (Conversation == null)
            {
                var go = Place("Team conversation", "TeamConversationAnchor", Vector3.zero, Quaternion.identity);
                go.SetActive(false);                                                   // so Voices is set before OnEnable subscribes
                Conversation = go.AddComponent<TeamConversation>();
                Conversation.Voices = Team;
                Conversation.Player = go.AddComponent<UnityClipPlayer>();
                go.SetActive(true);
            }
            if (Talk == null)
            {
                var go = Place("Push to talk", "PushToTalkAnchor", TalkDefault, Quaternion.identity);
                go.SetActive(false);
                Talk = go.AddComponent<PushToTalk>();
                var view = go.AddComponent<PushToTalkView>();
                view.Talk = Talk;
                Talk.Transcribed += text => Conversation.Ask(text, null);               // the words the recogniser heard go to the conversation; a topic is only ever released by the engine
                go.SetActive(true);
            }
            return true;
        }

        private GameObject Place(string name, string anchorName, Vector3 fallback, Quaternion fallbackRotation)
        {
            var anchor = GameObject.Find(anchorName);
            var go = new GameObject(name);
            if (anchor != null) { go.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation); }
            else go.transform.SetPositionAndRotation(fallback, fallbackRotation);
            go.transform.SetParent(transform, true);
            return go;
        }
    }
}
