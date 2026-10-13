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
            if (SaveLogOnDestroy) SaveSessionLog();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveSessionLog();
        }

        public void StartSession(string caseId, ulong seed, SessionMode mode, IContentReader reader = null)
        {
            var content = ContentLoader.Load(reader ?? UnityContentReader.ForThisPlatform(), caseId);
            Session = new Session(content.Cases[caseId], seed, mode, (SubsystemFactory ?? Subsystems.Default)(content));
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

        /// <summary>Writes the session as JSONL (header + log) to the persistent data folder; the CLI's replay command reads it.</summary>
        public string SaveSessionLog()
        {
            if (Session == null || Session.Log.Count == 0) return null;
            string dir = Path.Combine(Application.persistentDataPath, "sessions");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, Session.Case.Id + "_" + Session.Seed + "_" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            File.WriteAllText(file, SessionFile.Write(Session));
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
