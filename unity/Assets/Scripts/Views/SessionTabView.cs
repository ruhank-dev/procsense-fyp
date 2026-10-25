using System.Collections;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Sync;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The tablet's "Session" tab: who is signed in, what has reached the service, a button to finish the session and send it, and the person's latest results fetched from the service.
    /// It reads from the service and the upload queue only; it never touches the simulation (the one action it takes is ending the session, which saves and queues it).
    /// </summary>
    public sealed class SessionTabView : MonoBehaviour
    {
        public const float RefreshWhilePendingSeconds = 8f;

        private TMP_Text _account, _upload, _results, _note;
        private Button _finish, _refresh, _followUp;
        private float _nextText;
        private bool _fetching;
        private string _message = "";

        public string AccountText => _account != null ? _account.text : "";
        public string UploadText => _upload != null ? _upload.text : "";
        public string ResultsText => _results != null ? _results.text : "";
        public string MessageText => _note != null ? _note.text : "";
        public Button FinishButton => _finish;
        public Button RefreshButton => _refresh;
        public Button FollowUpButton => _followUp;

        /// <summary>Draws the tab's controls under <paramref name="root"/> (the tablet's tab area starts 190 px from the top).</summary>
        public void Build(Transform root)
        {
            UiKit.Panel(root, "Session background", new Vector2(0, 190), new Vector2(900, 410), new Color(0.08f, 0.10f, 0.15f, 1f));   // covers the clerking note, which belongs to the other tabs
            _account = UiKit.Label(root, "Account", "", new Vector2(14, 192), new Vector2(872, 30), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _upload = UiKit.Label(root, "Upload status", "", new Vector2(14, 224), new Vector2(872, 54), 22);
            _finish = UiKit.MakeButton(root, "Finish session and upload", new Vector2(10, 284), new Vector2(430, 50), 22);
            _refresh = UiKit.MakeButton(root, "Refresh my results", new Vector2(460, 284), new Vector2(430, 50), 22);
            _note = UiKit.Label(root, "Message", "", new Vector2(14, 338), new Vector2(440, 28), 20, TextAlignmentOptions.TopLeft, UiKit.WarnColor);
            _followUp = UiKit.MakeButton(root, "Follow-up encounter (practice)", new Vector2(460, 334), new Vector2(430, 34), 20);
            UiKit.Label(root, "Results title", "My latest sessions on the service", new Vector2(14, 368), new Vector2(872, 28), 20, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _results = UiKit.Label(root, "Results", "", new Vector2(14, 398), new Vector2(872, 160), 21);
            UiKit.Label(root, "Footnote", "The score uses provisional weights; the result does not depend on them.", new Vector2(14, 562), new Vector2(872, 28), 16, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _finish.onClick.AddListener(FinishSession);
            _refresh.onClick.AddListener(() => StartCoroutine(RefreshResults()));
            _followUp.onClick.AddListener(() => StartFollowUp());
            RefreshText();
        }

        private void OnEnable()
        {
            if (_results != null) { RefreshText(); StartCoroutine(Watch()); }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextText) return;
            _nextText = Time.unscaledTime + 0.5f;
            RefreshText();
        }

        private void RefreshText()
        {
            var auth = AuthService.Current;
            SetText(_account, auth != null && auth.IsSignedIn
                ? "Signed in as " + auth.Pseudonym + "  -  " + auth.OrganisationName + "  -  " + auth.Role
                : "Practising offline: not signed in. Sessions stay on this device.");
            var sync = SessionUploader.Instance != null ? SessionUploader.Instance.Sync : null;
            SetText(_upload, sync != null ? sync.Describe() : "Uploads are not running.");
            var host = SimulationHost.Instance;
            _finish.interactable = host != null && !host.Ended && host.Session != null && host.Session.Log.Count > 0;
            _followUp.interactable = host != null && host.Session != null && host.Session.Mode == ProcSense.Core.SessionMode.Practice && host.Session.Log.Count > 0;
            if (_message.StartsWith("Session saved. Sending") && sync != null && auth != null && auth.IsSignedIn && sync.LastUploadUnix > 0 && !UploadsWaiting())
                _message = "Saved on the service.";
            SetText(_note, _message);
        }

        private static bool UploadsWaiting()
        {
            var auth = AuthService.Current;
            var sync = SessionUploader.Instance != null ? SessionUploader.Instance.Sync : null;
            return sync != null && auth != null && auth.IsSignedIn && sync.Queue.WaitingFor(auth.UserId) > 0;
        }

        private void FinishSession()
        {
            var host = SimulationHost.Instance;
            if (host == null) return;
            string file = host.EndSession();
            var auth = AuthService.Current;
            _message = file == null ? "Nothing to save yet: do something in the room first."
                : auth != null && auth.IsSignedIn ? "Session saved. Sending it to the service..." : "Session saved on this device. Sign in to upload sessions.";
            RefreshText();
            if (file != null && auth != null && auth.IsSignedIn) StartCoroutine(Watch());
        }

        /// <summary>Plan p2-ai-advanced-simulation-s02: the same patient again, from where this encounter left them (the host saves the encounter that ends). Practice only; the host refuses assessment.</summary>
        public bool StartFollowUp()
        {
            var host = SimulationHost.Instance;
            if (host == null || host.Session == null) return false;
            try
            {
                var twin = host.CaptureTwin();
                host.StartFollowUp(twin, host.Session.Seed + 1);
                _message = "Follow-up encounter started: the same patient at " + twin.ClockTime + ". It stays on this device.";
                RefreshText();
                return true;
            }
            catch (ProcSense.Core.Content.ContentException e) { _message = e.Message; RefreshText(); return false; }
        }

        /// <summary>Fetches the latest sessions now, and again every few seconds while one is still waiting to be checked (for a couple of minutes).</summary>
        private IEnumerator Watch()
        {
            float stop = Time.unscaledTime + 120f;
            do
            {
                yield return RefreshResults();
                if (!_lastAnyPending && !UploadsWaiting()) yield break;
                float until = Time.unscaledTime + RefreshWhilePendingSeconds;
                while (Time.unscaledTime < until) yield return null;
            } while (Time.unscaledTime < stop);
        }

        private bool _lastAnyPending;

        public IEnumerator RefreshResults()
        {
            if (_fetching) yield break;
            _fetching = true;
            ResultsView view = null;
            yield return ResultsClient.Fetch(AuthService.Current, ResultsClient.DefaultCount, v => view = v);
            _fetching = false;
            if (_results == null || view == null) yield break;
            _lastAnyPending = view.Ok && view.AnyPending;
            SetText(_results, view.Text);
            RefreshText();
        }

        private static void SetText(TMP_Text label, string value) { if (label != null && label.text != value) label.text = value; }
    }
}
