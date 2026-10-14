using System;
using System.Collections;
using System.IO;
using ProcSense.VR.Auth;

namespace ProcSense.VR.Sync
{
    /// <summary>
    /// Sends the queued sessions to the service (p1-backend-s03). One pass: for every session due and played by the person who is signed in, POST the session file, then ask the service whether it really has it,
    /// and only then remove it from the queue. What each answer means: 200 or 202 stored; 401 (signed out) wait for sign-in without counting a failure; 403 consent_required retry later (the person has to agree on the
    /// web page); 409, 413, 422 the service refused it for good (needs attention, not retried); 429, 5xx and no answer retry with backoff. Play is never affected: nothing here blocks a frame.
    /// </summary>
    public sealed class SessionSync
    {
        private readonly UploadQueue _queue;
        private readonly Func<AuthService> _auth;
        private readonly Func<long> _now;

        public SessionSync(UploadQueue queue, Func<AuthService> auth, Func<long> nowUnix) { _queue = queue; _auth = auth; _now = nowUnix; }

        public UploadQueue Queue => _queue;
        public long LastUploadUnix { get; private set; }
        public bool? ServiceReachable { get; private set; }     // null until the first try

        public IEnumerator Process()
        {
            var auth = _auth();
            if (auth == null || !auth.IsSignedIn) yield break;
            foreach (var entry in _queue.Due(auth.UserId))
            {
                if (!File.Exists(entry.path)) { _queue.GiveUp(entry, "file_missing"); continue; }
                string text = File.ReadAllText(entry.path);
                HttpResult sent = default;
                yield return auth.Authorized("POST", "/organisations/" + entry.organisationId + "/sessions", text, r => sent = r, "application/x-ndjson");
                ServiceReachable = !sent.NetworkError;
                if (sent.NetworkError) { _queue.Retry(entry, "no_connection"); yield break; }   // no network: the others would fail the same way, try again later
                if (!auth.IsSignedIn) yield break;                                              // renewing the sign-in was refused: nothing to count against the session
                if (sent.Status == 200 || sent.Status == 202)
                {
                    HttpResult confirmed = default;
                    yield return auth.Authorized("GET", "/organisations/" + entry.organisationId + "/sessions/" + entry.sessionId, null, r => confirmed = r);
                    if (confirmed.Ok) { _queue.Succeeded(entry); LastUploadUnix = _now(); }
                    else _queue.Retry(entry, "not_confirmed");
                }
                else if (sent.Status == 403 && sent.Body.Contains("consent_required")) _queue.Retry(entry, "consent_required");
                else if (sent.Status == 409 || sent.Status == 413 || sent.Status == 422) _queue.GiveUp(entry, "refused_" + sent.Status);
                else if (sent.Status == 429 || sent.Status >= 500) _queue.Retry(entry, "service_busy_" + sent.Status);
                else if (sent.Status == 401) yield break;
                else _queue.GiveUp(entry, "refused_" + sent.Status);
            }
        }

        /// <summary>One line for a status panel.</summary>
        public string Describe()
        {
            var auth = _auth();
            if (auth == null || !auth.IsSignedIn)
                return _queue.Entries.Count == 0 ? "Not signed in." : "Not signed in. " + _queue.Entries.Count + " session(s) saved on this device will upload after you sign in.";
            int waiting = _queue.WaitingFor(auth.UserId), attention = _queue.NeedingAttention;
            string line = waiting == 0 ? "All your sessions are saved on the service." : waiting + " session(s) waiting to upload.";
            if (attention > 0) line += " " + attention + " need attention.";
            if (ServiceReachable == false) line += " The service cannot be reached right now.";
            return line;
        }
    }
}
