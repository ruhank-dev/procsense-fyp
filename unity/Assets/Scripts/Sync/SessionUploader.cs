using System;
using System.Collections;
using System.IO;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Sync
{
    /// <summary>
    /// The one object that keeps uploading in the background for as long as the app runs (p1-backend-s03): it tries the queue about every 30 seconds, and straight away when a session ends or a person signs in.
    /// It survives scene changes. A session whose person was not signed in when it ended is never queued (it stays on the device: it has no one to belong to).
    /// </summary>
    public sealed class SessionUploader : MonoBehaviour
    {
        public const float IntervalSeconds = 30f;
        public static SessionUploader Instance { get; private set; }

        /// <summary>The queue and sender. Settable so a test can give it a queue in a temporary folder.</summary>
        public SessionSync Sync { get; set; }
        private bool _kick;

        public static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static string OutboxDirectory => Path.Combine(Application.persistentDataPath, "outbox");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("Session uploader");
            DontDestroyOnLoad(go);
            go.AddComponent<SessionUploader>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Sync = new SessionSync(new UploadQueue(OutboxDirectory, UnixNow), () => AuthService.Current, UnixNow);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private IEnumerator Start()
        {
            while (true)
            {
                yield return Sync.Process();
                float waited = 0f;
                while (waited < IntervalSeconds && !_kick) { waited += Time.unscaledDeltaTime; yield return null; }
                _kick = false;
            }
        }

        /// <summary>Try the queue as soon as possible (a session just ended, or someone signed in).</summary>
        public void Kick() => _kick = true;

        /// <summary>Queues a finished session for the signed-in person. Returns false when nobody is signed in (the session stays on the device) or it was queued already.</summary>
        public static bool Enqueue(string sessionId, string path)
        {
            var auth = AuthService.Current;
            if (Instance == null || auth == null || !auth.IsSignedIn || string.IsNullOrEmpty(auth.UserId) || string.IsNullOrEmpty(auth.OrganisationId)) return false;
            bool added = Instance.Sync.Queue.Enqueue(sessionId, path, auth.UserId, auth.OrganisationId);
            if (added) Instance.Kick();
            return added;
        }
    }
}
