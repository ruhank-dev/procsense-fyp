using ProcSense.VR.Audio;
using ProcSense.VR.Bridge;
using ProcSense.VR.Diagnostics;
using ProcSense.VR.Records;
using UnityEngine;
namespace ProcSense.VR.Views
{
    /// <summary>Owns the session's wait/voice/subtitle views. Core supplies events and timing; this owns presentation only.</summary>
    public sealed class SessionPresentation : MonoBehaviour
    {
        public static SessionPresentation Instance { get; private set; }
        public WaitOverlay Wait { get; private set; }
        public VoicePlayer Voice { get; private set; }
        private SimulationHost _host;
        private object _session;
        private GameObject _root;
        private CameraHudAnchor _anchor;
        private PatientIdleView _patient;
        private PatientTransportView _transport;
        private RecordsWorkstation _records;
        private void OnEnable()
        {
            if (Instance != null && Instance != this && Instance.isActiveAndEnabled) { enabled = false; return; }
            Instance = this;
        }
        private void OnDisable() { Clear(); if (Instance == this) Instance = null; }
        private void Update() => Sync();
        private void Finished(string id) => Clear();
        public void Sync()
        {
            if (!isActiveAndEnabled) return;
            var host = SimulationHost.Instance; var camera = Camera.main;
            var session = host != null ? host.Session : null;
            if (session == null || camera == null || host.Ended) { Clear(); return; }
            if (!ReferenceEquals(session, _session) || host != _host || _root == null)
            {
                Clear(); _session = session; _host = host; _host.SessionEnded += Finished;
                _root = new GameObject("Session presentation (runtime)"); _root.transform.SetParent(transform, false);
                var hud = new GameObject("Wait status"); hud.transform.SetParent(_root.transform, false);
                _anchor = hud.AddComponent<CameraHudAnchor>(); Wait = hud.AddComponent<WaitOverlay>();
                var speech = new GameObject("Voice and subtitles"); speech.transform.SetParent(_root.transform, false);
                Voice = speech.AddComponent<VoicePlayer>(); Voice.RequireSpeakerAnchor = true;
            }
            _anchor.Follow = camera.transform; _anchor.Sync();
            Voice.Subtitles.Follow = camera.transform;
            Voice.transform.position = camera.transform.position;
            BindHeads();
        }
        private void BindHeads()
        {
            if (_patient == null) _patient = Object.FindAnyObjectByType<PatientIdleView>();
            if (_transport == null) _transport = Object.FindAnyObjectByType<PatientTransportView>();
            if (_records == null) _records = Object.FindAnyObjectByType<RecordsWorkstation>();
            Voice.Heads.Clear();
            if (_records != null) Head("laboratory", _records.PhoneAnchor);
            string state = _host.Observable?.Transport?.State ?? "";
            var actor = _transport != null ? _transport.Actor : null;
            if ((state == "IN_TRANSIT" || state == "RETURNING") && actor != null && actor.gameObject.activeInHierarchy && !_transport.HiddenInside)
            {
                Head("patient", actor.transform.Find("Patient head"));
                if (actor.Nurse != null) Head("nurse", actor.Nurse.Find("Nurse prototype head"));
                if (actor.Porter != null) Head("porter", actor.Porter.Find("Porter prototype head"));
                if (actor.Doctor != null)
                {
                    var head = actor.Doctor.Find("Doctor prototype head"); Head("clinician", head); Head("doctor", head);
                }
            }
            else if (_host.Observable?.Location == "patient_bay" && _patient != null) Head("patient", _patient.Head);
        }
        private void Head(string speaker, Transform head) { if (head != null) Voice.Heads[speaker] = head; }
        private void Clear()
        {
            if (_host != null) _host.SessionEnded -= Finished;
            if (_root != null) { _root.SetActive(false); Destroy(_root); }
            _root = null; _session = null; _host = null; _anchor = null; Wait = null; Voice = null;
        }
    }
}
