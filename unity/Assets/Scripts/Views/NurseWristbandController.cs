using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.VR.Audio;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ProcSense.VR.Views
{
    public enum NurseVisitPhase { Idle, WaitingForPatient, Entering, CheckingRecords, ReplacingBand, Resolved, Leaving }

    /// <summary>Paces the accepted public nurse effects cosmetically. Never applies an action, reads hidden identifiers or advances Core.</summary>
    [DisallowMultipleComponent]
    public sealed class NurseWristbandController : MonoBehaviour
    {
        public static NurseWristbandController Instance { get; private set; }
        public Transform Entry, InsideDoor, Bedside, Exit, Wrist;
        public GameObject ClipboardPrefab, BandPrefab, AllergyPrefab;
        public NurseVisitPhase Phase { get; private set; }
        public NurseVisitActor Actor { get; private set; }
        public Transform Head => Actor != null && PatientPresent ? Actor.Head : null;
        public bool Blocked { get; private set; }
        public string Status { get; private set; } = "";
        public bool BandPlaced { get; private set; }
        public int VisitsStarted { get; private set; }
        public string CoreStep => Phase == NurseVisitPhase.CheckingRecords ? "AT_BEDSIDE" :
            Phase == NurseVisitPhase.ReplacingBand ? "REPLACING" :
            Phase == NurseVisitPhase.Resolved || Phase == NurseVisitPhase.Leaving || (_hadVisit && Phase == NurseVisitPhase.Idle) ? "DONE" : "CALLED";

        private SimulationHost _host;
        private object _session;
        private bool _hadVisit, _active, _pendingSwap, _oldBand, _knownMissing;
        private int _visitLogIndex = -1, _waypoint;
        private float _phaseSeconds, _retry, _speechHold;
        private Vector3 _goal;
        private bool _pathReady;
        private readonly Dictionary<Renderer, bool> _renderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Canvas, bool> _canvases = new Dictionary<Canvas, bool>();
        private readonly List<Speech> _speech = new List<Speech>();
        private readonly HashSet<string> _ownedLines = new HashSet<string>();
        private VoicePlayer _speaker;
        private VoiceLine _awaitingLine;
        private readonly List<VoiceLine> _ownedSpeech = new List<VoiceLine>();
        private Transform _allergy;
        private sealed class Speech { public VoiceLine Line; public int Rank; }

        public bool PatientPresent
        {
            get
            {
                var s = _host?.Observable; string trip = s?.Transport?.State ?? "";
                return s?.Location == "patient_bay" && (trip.Length == 0 || trip == "REQUESTED" || trip == "PREPARING" || trip == "DELAYED" || trip == "BACK" || trip == "CANCELLED");
            }
        }
        public string StepFor(object session) => isActiveAndEnabled && _hadVisit && _host != null && !_host.Ended && ReferenceEquals(session, _session) ? CoreStep : null;

        private void OnEnable()
        {
            if (Instance != null && Instance != this && Instance.isActiveAndEnabled) { enabled = false; return; }
            Instance = this; Sync();
        }
        private void OnDisable()
        {
            if (!ReferenceEquals(_host, null)) _host.Applied -= Applied;
            ClearVisit(); RestoreBand(); _host = null; _session = null;
            if (Instance == this) Instance = null;
        }
        private void Applied(SimAction action, ActionOutcome outcome) => ObserveApplied(action, outcome);

        public void Sync()
        {
            if (!isActiveAndEnabled) return;
            if (Wrist != null && _renderers.Count == 0)
            {
                foreach (var r in Wrist.GetComponentsInChildren<Renderer>(true)) _renderers[r] = r.enabled;
                foreach (var c in Wrist.GetComponentsInChildren<Canvas>(true)) _canvases[c] = c.enabled;
            }
            var host = SimulationHost.Instance; var session = host != null ? host.Session : null;
            if (!ReferenceEquals(host, _host) || !ReferenceEquals(session, _session))
            {
                if (!ReferenceEquals(_host, null)) _host.Applied -= Applied;
                ClearVisit(); RestoreBand(); _host = host; _session = session;
                _knownMissing = _hadVisit = _pendingSwap = BandPlaced = false; _visitLogIndex = -1; VisitsStarted = 0;
                if (_host != null) _host.Applied += Applied;
            }
            if (_host == null || _host.Session == null || _host.Ended) { ClearVisit(); RestoreBand(); return; }
            var speaker = SessionPresentation.Instance != null ? SessionPresentation.Instance.Voice : null;
            if (_speaker != speaker)
            {
                if (_speaker != null) _speaker.Started -= SpeechStarted;
                _speaker = speaker;
                if (_speaker != null) _speaker.Started += SpeechStarted;
            }
        }

        /// <summary>Also called by the voice view before dispatch, making event subscription order irrelevant.</summary>
        public bool ObserveApplied(SimAction action, ActionOutcome outcome)
        {
            Sync();
            if (_host == null || _host.Ended || outcome == null) return false;
            if (action.Verb == "read_wristband" && outcome.RejectReason == "WRISTBAND_MISSING") _knownMissing = true;
            if (outcome.Accepted && action.Verb == "read_wristband" && _host.Observable.Wristband != null && !_pendingSwap) _knownMissing = false;
            bool correction = outcome.Accepted && outcome.Effects.Contains("nurse:CALLED") &&
                (outcome.Effects.Contains("wristband_applied") || outcome.Effects.Contains("wristband_replaced"));
            if (!correction) { ApplyBand(); return false; }
            int index = _host.Session.Log.Count;
            if (_visitLogIndex == index) return true;
            ClearVisit(); _visitLogIndex = index; _hadVisit = _active = _pendingSwap = true;
            _oldBand = outcome.Effects.Contains("wristband_replaced"); BandPlaced = false;
            _knownMissing = !_oldBand; VisitsStarted++;
            SetPhase(NurseVisitPhase.WaitingForPatient); TryEnter(); ApplyBand();
            return true;
        }

        public void QueueSpeech(VoiceLine line)
        {
            if (!_active || line == null || !_ownedLines.Add(line.Id)) return;
            string state = line.Event.TryGetValue("nurse_state", out var ns) ? ns as string : null;
            string effect = line.Event.TryGetValue("effect", out var ef) ? ef as string : null;
            int rank = state == "AT_BEDSIDE" ? 1 : state == "REPLACING" ? 2 : state == "DONE" || effect == "allergy_band_applied" ? 3 : 0;
            var copy = new VoiceLine { Id = line.Id, Text = line.Text, Speaker = line.Speaker, Case = line.Case,
                Template = line.Template, Event = new Dictionary<string, object>(line.Event) };
            _ownedSpeech.Add(copy); _speech.Add(new Speech { Line = copy, Rank = rank });
        }
        private void SpeechStarted(VoiceLine line)
        {
            if (ReferenceEquals(_awaitingLine, line))
                _speechHold = Mathf.Max(SubtitleView.MinSeconds, line.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length / VoicePlayer.WordsPerSecond);
        }
        private static int Rank(string step) => step == "AT_BEDSIDE" ? 1 : step == "REPLACING" ? 2 : step == "DONE" ? 3 : 0;
        private bool DialogueFinished(int rank)
        {
            if (_speaker == null) { _speech.RemoveAll(s => s.Rank <= rank); _awaitingLine = null; return true; }
            bool queued = _awaitingLine != null && (ReferenceEquals(_speaker.Current, _awaitingLine) || _speaker.Queue.Any(l => ReferenceEquals(l, _awaitingLine)));
            return _speechHold <= 0 && !queued && !_speech.Any(s => s.Rank <= rank);
        }
        private void Dialogue(float dt)
        {
            _speechHold = Mathf.Max(0, _speechHold - dt);
            if (_speaker == null || Actor == null) return;
            if (_awaitingLine != null)
            {
                if (_speechHold > 0 || ReferenceEquals(_speaker.Current, _awaitingLine) || _speaker.Queue.Any(l => ReferenceEquals(l, _awaitingLine))) return;
                _awaitingLine = null;
            }
            var next = _speech.FirstOrDefault(s => s.Rank <= Rank(CoreStep));
            if (next == null) return;
            _speech.Remove(next); _awaitingLine = next.Line; _speechHold = 0;
            SessionPresentation.Instance?.Sync();
            _speaker.Say(next.Line);
        }

        private void Update()
        {
            Sync();
            if (_host == null || _host.Session == null || _host.Ended) return;
            if (Actor != null && Wrist == null) { StopAgent(); Blocked = true; Status = "Nurse wrist anchor unavailable"; return; }
            float dt = Mathf.Min(Time.deltaTime, .1f);
            if (_host.Paused || dt <= 0) { StopAgent(); return; }
            if (_active && !PatientPresent)
            {
                RemoveActor(); SetPhase(NurseVisitPhase.WaitingForPatient);
            }
            if (_active && Actor == null) TryEnter();
            if (Actor != null)
            {
                _phaseSeconds += dt; Dialogue(dt); Blocked = false;
                switch (Phase)
                {
                    case NurseVisitPhase.Entering:
                        Door(_waypoint == 0);
                        if (Move(_waypoint == 0 ? InsideDoor : Bedside))
                        {
                            if (_waypoint == 0) { _waypoint = 1; _pathReady = false; }
                            else if (DialogueFinished(0)) { FaceWrist(); SetPhase(NurseVisitPhase.CheckingRecords); }
                        }
                        break;
                    case NurseVisitPhase.CheckingRecords:
                        StopAgent(); FaceWrist();
                        if (_phaseSeconds >= 1.5f && DialogueFinished(1)) SetPhase(NurseVisitPhase.ReplacingBand);
                        break;
                    case NurseVisitPhase.ReplacingBand:
                        StopAgent(); FaceWrist();
                        if (!BandPlaced && _phaseSeconds >= 1.35f)
                        {
                            if (Vector3.Distance(Actor.RightHand.position, Wrist.position) <= .055f)
                            { BandPlaced = true; _pendingSwap = _knownMissing = false; _phaseSeconds = 1.35f; ApplyBand(); AllergyMarker(); }
                            else { Blocked = true; Status = "Waiting for reachable wrist"; }
                        }
                        if (BandPlaced && _phaseSeconds >= 2f && DialogueFinished(2)) SetPhase(NurseVisitPhase.Resolved);
                        break;
                    case NurseVisitPhase.Resolved:
                        StopAgent(); if (_phaseSeconds >= .9f && DialogueFinished(3)) { _waypoint = 0; SetPhase(NurseVisitPhase.Leaving); }
                        break;
                    case NurseVisitPhase.Leaving:
                        Door(_waypoint == 1);
                        if (Move(_waypoint == 0 ? InsideDoor : Exit))
                        {
                            if (_waypoint == 0) { _waypoint = 1; _pathReady = false; }
                            else { RemoveActor(); _active = false; _speech.Clear(); _ownedSpeech.Clear(); _ownedLines.Clear(); _awaitingLine = null; SetPhase(NurseVisitPhase.Idle); }
                        }
                        break;
                }
                if (Actor != null)
                {
                    if (!Blocked) Status = Phase == NurseVisitPhase.Leaving ? "Leaving" : CoreStep == "CALLED" ? "Entering" : CoreStep == "AT_BEDSIDE" ? "Checking records" : CoreStep == "REPLACING" ? "Applying band" : "Visit complete";
                    Actor.Pose(Phase == NurseVisitPhase.Leaving ? "EXIT" : CoreStep, _phaseSeconds, Phase == NurseVisitPhase.ReplacingBand ? Mathf.Clamp01(_phaseSeconds / 2f) : 0,
                        Wrist, (Phase == NurseVisitPhase.Entering || Phase == NurseVisitPhase.Leaving) && Actor.Agent.velocity.sqrMagnitude > .0025f && !Blocked, BandPlaced, Status);
                }
            }

        }

        private void LateUpdate()
        {
            if (_host != null && _host.Session != null && !_host.Ended) { ApplyBand(); AllergyMarker(); }
        }

        private void TryEnter()
        {
            if (!_active || !PatientPresent || Actor != null) return;
            if (Entry == null || InsideDoor == null || Bedside == null || Exit == null || Wrist == null)
            { Blocked = true; Status = "Nurse anchors unavailable"; return; }
            if (!NavMesh.SamplePosition(Entry.position, out var hit, .35f, NavMesh.AllAreas))
            { Blocked = true; Status = "Nurse route unavailable"; return; }
            var go = new GameObject("Nurse wristband visit - PROTOTYPE"); go.transform.SetParent(transform, false); go.transform.position = hit.position;
            Actor = go.AddComponent<NurseVisitActor>(); Actor.Build(ClipboardPrefab, BandPrefab);
            Actor.Pose("CALLED", 0, 0, Wrist, false, false, "Called");
            if (!Actor.Agent.Warp(hit.position)) { RemoveActor(); Blocked = true; Status = "Nurse route unavailable"; return; }
            _waypoint = 0; SetPhase(NurseVisitPhase.Entering); SessionPresentation.Instance?.Sync();
        }
        private bool Move(Transform target)
        {
            if (target == null) { StopAgent(); Blocked = true; Status = "Nurse anchor unavailable"; return false; }
            if (Actor == null || !Actor.Agent.enabled || !Actor.Agent.isOnNavMesh) { Blocked = true; Status = "Nurse route unavailable"; return false; }
            var agent = Actor.Agent;
            if (!NavMesh.SamplePosition(target.position, out var destination, .35f, NavMesh.AllAreas))
            { StopAgent(); Blocked = true; Status = "Nurse route unavailable"; return false; }
            var goal = destination.position;
            if (Vector3.Distance(Actor.transform.position, goal) < .075f) { agent.isStopped = true; return true; }
            if (!_pathReady || Time.unscaledTime >= _retry || Vector3.Distance(_goal, goal) > .01f)
            {
                _retry = Time.unscaledTime + .6f; _goal = goal;
                var path = new NavMeshPath();
                if (!agent.CalculatePath(_goal, path) || path.status != NavMeshPathStatus.PathComplete)
                { agent.ResetPath(); agent.isStopped = true; _pathReady = false; Blocked = true; Status = "Nurse path blocked - waiting"; return false; }
                agent.SetPath(path); _pathReady = true;
            }
            agent.isStopped = false;
            var next = agent.nextPosition; next.y = Actor.transform.position.y;
            if (!ClearMotion(Actor.transform.position, next))
            { agent.nextPosition = Actor.transform.position; agent.isStopped = true; Blocked = true; Status = "Waiting for space"; return false; }
            var delta = next - Actor.transform.position;
            if (delta.sqrMagnitude > .000001f) Actor.transform.rotation = Quaternion.RotateTowards(Actor.transform.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 160);
            Actor.transform.position = next; return false;
        }
        private bool ClearMotion(Vector3 from, Vector3 to)
        {
            bool Obstruction(Collider c) => !c.transform.IsChildOf(Actor.transform) && c.bounds.max.y > .06f;
            if (Physics.OverlapCapsule(to + Vector3.up * .25f, to + Vector3.up * 1.5f, .18f, ~0, QueryTriggerInteraction.Ignore).Any(Obstruction)) return false;
            var delta = to - from;
            return delta.sqrMagnitude < .000001f || !Physics.CapsuleCastAll(from + Vector3.up * .25f, from + Vector3.up * 1.5f, .18f,
                delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).Any(h => Obstruction(h.collider));
        }
        private void FaceWrist()
        {
            var direction = Wrist.position - Actor.transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) Actor.transform.rotation = Quaternion.RotateTowards(Actor.transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 180);
        }
        private void Door(bool open) { foreach (var d in Object.FindObjectsByType<SwingDoor>()) if (d.Connection == "BayCorridor") d.SetNpcPassage(this, open); }
        private void StopAgent() { if (Actor != null && Actor.Agent.enabled && Actor.Agent.isOnNavMesh) Actor.Agent.isStopped = true; }
        private void SetPhase(NurseVisitPhase phase) { Phase = phase; _phaseSeconds = 0; _pathReady = false; }
        private void RemoveActor()
        {
            Door(false);
            if (Actor != null) { Actor.gameObject.SetActive(false); Destroy(Actor.gameObject); Actor = null; }
            _pathReady = false;
        }
        private void ClearVisit()
        {
            RemoveActor();
            if (_speaker != null) { _speaker.CancelLines(_ownedSpeech); _speaker.Started -= SpeechStarted; }
            _speaker = null; _speech.Clear(); _ownedLines.Clear(); _ownedSpeech.Clear(); _awaitingLine = null; _speechHold = 0;
            if (_allergy != null) { _allergy.gameObject.SetActive(false); Destroy(_allergy.gameObject); _allergy = null; }
            _active = _pendingSwap = false; Phase = NurseVisitPhase.Idle; Blocked = false; Status = "";
        }
        private void ApplyBand()
        {
            bool visible = PatientPresent && !_knownMissing && (!_pendingSwap || _oldBand);
            if (Phase == NurseVisitPhase.ReplacingBand && _phaseSeconds >= 1.15f && !BandPlaced) visible = false;
            foreach (var pair in _renderers) if (pair.Key != null) pair.Key.enabled = pair.Value && visible;
            foreach (var pair in _canvases) if (pair.Key != null) pair.Key.enabled = pair.Value && visible && !_pendingSwap;
        }
        private void RestoreBand()
        {
            bool show = _host == null || _host.Session == null || PatientPresent;
            foreach (var p in _renderers) if (p.Key != null) p.Key.enabled = p.Value && show;
            foreach (var p in _canvases) if (p.Key != null) p.Key.enabled = p.Value && show;
        }
        private void AllergyMarker()
        {
            if (Wrist == null) { if (_allergy != null) _allergy.gameObject.SetActive(false); return; }
            bool show = PatientPresent && _host.Observable.AllergyBand && !_pendingSwap;
            if (show && _allergy == null && AllergyPrefab != null)
            {
                var root = new GameObject("Prototype applied allergy band"); root.transform.SetParent(transform, false);
                _allergy = root.transform; NurseVisitActor.ModelOnly(AllergyPrefab, "Imported band visual", _allergy);
                _allergy.localScale = new Vector3(.25f, 1, 1);
            }
            if (_allergy == null) return;
            _allergy.gameObject.SetActive(show);
            var axis = Vector3.ProjectOnPlane(Wrist.up, Vector3.up).normalized;
            if (axis.sqrMagnitude < .1f) axis = Vector3.forward;
            _allergy.SetPositionAndRotation(Wrist.position + Vector3.up * .032f - axis * .03f, Quaternion.LookRotation(axis, Vector3.up));
        }
    }
}
