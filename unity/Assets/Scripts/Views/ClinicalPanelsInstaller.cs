using ProcSense.VR.Auth;
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
        public SpokenActionRouter Router { get; private set; }
        public SpokenOrderFlow Orders { get; private set; }
        public OrderPanelView OrderPanel { get; private set; }
        public ProcSense.VR.UI.Training.TrainingBoardView Board { get; private set; }
        public static readonly Vector3 BoardDefault = new Vector3(1.4f, 1.45f, 1.2f);
        private ProcSense.VR.UI.Training.TrainingGuide _guide;
        private bool _guideFailed;
        public static readonly Vector3 OrderPanelDefault = new Vector3(-0.6f, 1.55f, 1.1f);
        public static readonly Vector3 TalkDefault = new Vector3(-0.6f, 1.2f, 1.1f);
        public static readonly Vector3 SubtitlesDefault = new Vector3(0.6f, 1.9f, 1.4f);

        public HeadsetNoticeSession NoticeSession { get; private set; }
        public NoticeChipView NoticeChip { get; private set; }
        public static readonly Vector3 NoticeDefault = new Vector3(0f, 2.45f, 0.6f);
        private string _noticeSession = "";

        private void Update() { Install(); SyncNotices(); }

        /// <summary>Starts the optional laptop notice socket for the running session (only when paired), restarts it for a new session id and stops it when the session ends. Never throws; play is identical without it.</summary>
        public void SyncNotices()
        {
            try
            {
                if (NoticeSession == null) NoticeSession = new HeadsetNoticeSession(() => new ClientWebSocketTransport());
                var host = SimulationHost.Instance;
                var id = host != null && host.Session != null ? host.SessionId ?? "" : "";
                if (id != _noticeSession)
                {
                    _noticeSession = id;
                    if (id == "") NoticeSession.End();
                    else NoticeSession.Begin(ServiceSettings.Load() != null ? ServiceSettings.Load().BaseUrl : "", id, Time.unscaledTime);
                }
                NoticeSession.Tick(Time.unscaledTime);
                if (NoticeChip == null && id != "")
                {
                    var go = Place("Laptop notices", "NoticeChipAnchor", NoticeDefault, Quaternion.identity);
                    NoticeChip = go.AddComponent<NoticeChipView>();
                    NoticeChip.Session = NoticeSession;
                    NoticeChip.Build();
                }
            }
            catch (System.Exception) { }
        }

        private void OnDestroy() { try { NoticeSession?.End(); } catch (System.Exception) { } }

        /// <summary>Creates whatever is missing; returns true when all three panels exist.</summary>
        public bool Install()
        {
            var host = SimulationHost.Instance;
            if (host == null || host.Session == null) return false;
            if (_boundSession != null && !ReferenceEquals(_boundSession, host.Session)) DropSessionPanels();   // a new session (the menu chose another case): its own data
            _boundSession = host.Session;
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
                Router = go.AddComponent<SpokenActionRouter>();
                Router.Talk = Conversation;
                view.Router = Router;
                Orders = go.AddComponent<SpokenOrderFlow>();                            // the spoken medical orders: Gemma Cloud first, the offline grammar as the fallback, the trainee's yes before anything reaches the engine
                Orders.Talk = Conversation;
                Router.Orders = Orders;
                Talk.Transcribed += text => Router.OnTranscribed(text);                // the words the recogniser heard become tablet actions (the engine decides), then the voices answer
                go.SetActive(true);
            }
            if (OrderPanel == null && Orders != null)
            {
                var go = Place("Spoken order panel", "OrderPanelAnchor", OrderPanelDefault, Quaternion.identity);
                go.SetActive(false);                                                   // so Flow is set before OnEnable subscribes
                OrderPanel = go.AddComponent<OrderPanelView>();
                OrderPanel.Flow = Orders;
                go.SetActive(true);
            }
            if (Board == null) InstallBoard();
            return true;
        }

        /// <summary>The training board (P3-TRN-03) at TrainingBoardAnchor, or beside the bed. It hides itself in the examination. The guide is read once; if it cannot be read the board is left out (logged), play is unchanged.</summary>
        private void InstallBoard()
        {
            if (_guide == null && !_guideFailed)
            {
                try { _guide = ProcSense.VR.UI.Training.TrainingGuide.Load(ProcSense.VR.Content.UnityContentReader.ForThisPlatform()); }
                catch (System.Exception e) { _guideFailed = true; Debug.LogWarning("Training board left out: " + ProcSense.VR.UI.Training.TrainingGuide.File + " could not be read (" + e.Message + ")"); }
            }
            if (_guide == null) return;
            var go = Place("Training board", "TrainingBoardAnchor", BoardDefault, Quaternion.Euler(0f, 30f, 0f));
            Board = go.AddComponent<ProcSense.VR.UI.Training.TrainingBoardView>();
            Board.Guide = _guide;
            Board.Build();
        }

        private object _boundSession;

        private void DropSessionPanels()
        {
            if (Emr != null) Destroy(Emr.gameObject);
            if (Cards != null) Destroy(Cards.gameObject);
            if (Diagnostics != null) Destroy(Diagnostics.gameObject);
            Emr = null; Cards = null; Diagnostics = null;
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
