using System;
using System.Collections.Generic;
using System.IO;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;
using ProcSense.VR.Content;
using ProcSense.VR.Sync;
using UnityEngine;

namespace ProcSense.VR.Bridge
{
    /// <summary>
    /// Owns the one Core Session. Unity sends actions in (<see cref="Apply"/>) and reads the ObservableState out; it never changes
    /// simulation state itself. Any Core exception pauses the session instead of crashing, and the session log is saved so a
    /// headset session can be replayed headless.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimulationHost : MonoBehaviour
    {
        [SerializeField] private SessionConfig config;
        [SerializeField] private bool autoTick = true;
        [SerializeField] private float speed = 1f;

        public static SimulationHost Instance { get; private set; }

        public Session Session { get; private set; }
        public ObservableState Observable { get; private set; }
        public bool Paused { get; private set; }
        public string PauseReason { get; private set; } = "";

        /// <summary>Raised after an action or a time step changed what the trainee can perceive.</summary>
        public event Action<ObservableState> Changed;

        /// <summary>Raised when the session pauses because Core threw.</summary>
        public event Action<string> SessionPaused;

        public SessionConfig Config { get => config; set => config = value; }
        public bool AutoTick { get => autoTick; set => autoTick = value; }
        public bool SaveLogOnDestroy { get; set; } = true;

        /// <summary>The id the host gave this session when it started (written into the saved file's header, so the service stores it once however often it is sent).</summary>
        public string SessionId { get; private set; } = "";

        /// <summary>The version of the content this session was played with (written into the header; the service can only verify a version it holds).</summary>
        public string ContentVersion { get; private set; } = "unversioned";

        /// <summary>Test seam: where session files are saved (default: the application's data folder, `sessions`).</summary>
        public static string SessionsDirectoryOverride { get; set; }

        /// <summary>Raised once when the session has been saved and queued for upload.</summary>
        public event Action<string> SessionEnded;

        private bool _ended;

        /// <summary>True once the session has been ended (saved and queued).</summary>
        public bool Ended => _ended;

        /// <summary>Test seam: replaces the default subsystem chain (used to prove a Core exception pauses the session).</summary>
        public Func<ContentPackage, IEnumerable<ProcSense.Core.Pipeline.ISubsystem>> SubsystemFactory { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A duplicate service must never replace the running session or tick it twice.
                enabled = false;
                Destroy(this);
                return;
            }
            Instance = this;
            if (config != null && Session == null) StartSession(config.caseId, (ulong)config.seed, config.mode);
        }

        private void OnDestroy()
        {
            if (SaveLogOnDestroy) EndSession();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveSessionLog();
        }

        public void StartSession(string caseId, ulong seed, SessionMode mode, IContentReader reader = null)
        {
            reader = reader ?? UnityContentReader.ForThisPlatform();
            var content = ContentLoader.Load(reader, caseId);
            Session = new Session(content.Cases[caseId], seed, mode, (SubsystemFactory ?? Subsystems.Default)(content));
            SessionId = Guid.NewGuid().ToString();
            ContentVersion = VersionOf(reader);
            _ended = false;
            Paused = false;
            PauseReason = "";
            Refresh();
        }

        private void Update()
        {
            if (autoTick) Tick(Time.deltaTime * speed);
        }

        /// <summary>Moves simulated time forward. Core runs whole 0.5 s steps and carries the remainder.</summary>
        public void Tick(double dt)
        {
            if (Session == null || Paused) return;
            long before = Session.Clock.Steps;
            try { Session.Tick(dt); }
            catch (Exception e) { Pause(e); return; }
            if (Session.Clock.Steps != before) Refresh();
        }

        /// <summary>The only way Unity changes the simulation. A Core exception pauses the session and returns a rejection.</summary>
        public ActionOutcome Apply(SimAction action)
        {
            if (Session == null || Paused) return Rejected(Paused ? "SESSION_PAUSED" : "NO_SESSION");
            try
            {
                var outcome = Session.Apply(action);
                Refresh();
                return outcome;
            }
            catch (Exception e)
            {
                Pause(e);
                return Rejected("SESSION_PAUSED");
            }
        }

        private static string VersionOf(IContentReader reader)
        {
            try { return ContentManifestCheck.Version(reader); }
            catch (Exception e) when (e is IOException || e is ContentException || e is ArgumentException) { return "unversioned"; }
        }

        /// <summary>Writes the session as JSONL (header + log) to the data folder; the CLI's replay and verify commands read it. The header carries the session id, the content version and the engine version.
        /// The file name holds the session id, so saving again (the app is paused, then closed) replaces the same file.</summary>
        public string SaveSessionLog()
        {
            if (Session == null || Session.Log.Count == 0) return null;
            string dir = SessionsDirectoryOverride ?? Path.Combine(Application.persistentDataPath, "sessions");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, Session.Case.Id + "_" + Session.Seed + "_" + SessionId + ".jsonl");
            File.WriteAllText(file, SessionFile.Write(Session, new SessionMeta(SessionId, ContentVersion)));
            return file;
        }

        /// <summary>The session is over (the player finished or the app is closing): saves it and queues it for upload for the person who is signed in. Safe to call more than once; only the first call does anything.</summary>
        public string EndSession()
        {
            if (_ended) return null;
            string file = SaveSessionLog();
            if (file == null) return null;
            _ended = true;
            SessionUploader.Enqueue(SessionId, file);
            SessionEnded?.Invoke(SessionId);
            return file;
        }

        private void Refresh()
        {
            Observable = Session.Observable;
            Changed?.Invoke(Observable);
        }

        private void Pause(Exception e)
        {
            Paused = true;
            PauseReason = e.GetType().Name + ": " + e.Message;
            Debug.LogError("[ProcSense] Core threw, session paused. " + e);
            SessionPaused?.Invoke(PauseReason);
        }

        private static ActionOutcome Rejected(string reason)
        {
            var o = new ActionOutcome();
            o.Reject(reason);
            return o;
        }
    }
}
