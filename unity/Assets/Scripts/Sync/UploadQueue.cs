using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ProcSense.VR.Sync
{
    /// <summary>One finished session waiting to be sent. It keeps the identity the session was played under, so it is only ever sent as that person.</summary>
    [Serializable]
    public class UploadEntry
    {
        public string sessionId;
        public string path;
        public string userId;
        public string organisationId;
        public string state = Waiting;     // Waiting: will be tried when due; NeedsAttention: the service refused it for good and it is not retried
        public int attempts;
        public long nextAtUnix;
        public long queuedAtUnix;
        public string reason = "";

        public const string Waiting = "waiting";
        public const string NeedsAttention = "needs_attention";
    }

    [Serializable]
    internal class QueueFile { public List<UploadEntry> entries = new List<UploadEntry>(); }

    /// <summary>
    /// The list of sessions that still have to reach the service (p1-backend-s03). It lives in a file under the application's data folder, so a session finished without a connection, or while the app is closed
    /// mid-upload, is still there at the next start. An entry is removed only after the service has confirmed it has the session. Retries back off 15 s, 30 s, 1 min ... up to 10 min.
    /// The times are wall-clock seconds and only decide when to try again; a wrong device date can delay a retry but never lose or duplicate a session (the service stores a session id once).
    /// </summary>
    public sealed class UploadQueue
    {
        public const string FileName = "outbox.json";
        private readonly string _file;
        private readonly Func<long> _now;
        private QueueFile _data = new QueueFile();

        public UploadQueue(string directory, Func<long> nowUnix)
        {
            Directory.CreateDirectory(directory);
            _file = Path.Combine(directory, FileName);
            _now = nowUnix;
            if (File.Exists(_file))
            {
                try { _data = JsonUtility.FromJson<QueueFile>(File.ReadAllText(_file)) ?? new QueueFile(); }
                catch (ArgumentException) { _data = new QueueFile(); }   // a damaged list must not stop the app; the session files themselves are still on disk
                if (_data.entries == null) _data.entries = new List<UploadEntry>();
            }
        }

        public IReadOnlyList<UploadEntry> Entries => _data.entries;

        public static long BackoffSeconds(int attempts) => Math.Min(600L, 15L * (1L << Math.Min(Math.Max(attempts - 1, 0), 10)));

        /// <summary>Adds a session once; the same session id again changes nothing (so a repeated end-of-session cannot queue it twice).</summary>
        public bool Enqueue(string sessionId, string path, string userId, string organisationId)
        {
            if (_data.entries.Any(e => e.sessionId == sessionId)) return false;
            _data.entries.Add(new UploadEntry { sessionId = sessionId, path = path, userId = userId, organisationId = organisationId, queuedAtUnix = _now(), nextAtUnix = _now() });
            Save();
            return true;
        }

        /// <summary>The entries to try now for this person: waiting, played as them, and due.</summary>
        public List<UploadEntry> Due(string userId) => _data.entries.Where(e => e.state == UploadEntry.Waiting && e.userId == userId && e.nextAtUnix <= _now()).ToList();

        public int WaitingFor(string userId) => _data.entries.Count(e => e.state == UploadEntry.Waiting && e.userId == userId);
        public int NeedingAttention => _data.entries.Count(e => e.state == UploadEntry.NeedsAttention);
        public int NotUploadable(string userId) => _data.entries.Count(e => e.state == UploadEntry.Waiting && e.userId != userId);

        public void Succeeded(UploadEntry entry) { _data.entries.Remove(entry); Save(); }

        public void Retry(UploadEntry entry, string reason)
        {
            entry.attempts++;
            entry.reason = reason;
            entry.nextAtUnix = _now() + BackoffSeconds(entry.attempts);
            Save();
        }

        public void GiveUp(UploadEntry entry, string reason)
        {
            entry.state = UploadEntry.NeedsAttention;
            entry.reason = reason;
            Save();
        }

        private void Save()
        {
            string temp = _file + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(_data));
            if (File.Exists(_file)) File.Delete(_file);   // replace in two steps: a crash between them leaves the .tmp, and the list is rebuilt empty, never half-written
            File.Move(temp, _file);
        }
    }
}
