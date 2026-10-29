using System.Collections.Generic;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Checklist;
using ProcSense.VR.UI.Debrief;
using ProcSense.VR.Views;
using UnityEngine;
namespace ProcSense.VR.UI
{
    /// <summary>Owns scene checklists and creates the debrief only after the trainee finishes the session.</summary>
    public sealed class ClinicalPanelStation : MonoBehaviour
    {
        public Transform WardAnchor, WhoAnchor, DebriefAnchor;
        public WardChecklistPanel Ward { get; private set; }
        public ChecklistBoard Who { get; private set; }
        public DebriefView Debrief { get; private set; }
        public bool WaitingForDebrief => _notice != null && _notice.activeInHierarchy;
        private SimulationHost _host;
        private object _session;
        private GameObject _notice;
        private readonly List<GameObject> _owned = new List<GameObject>();
        private void Update() => Sync();
        private void OnDisable() => Clear();
        private void Finished(string id) => Sync();
        public void Sync()
        {
            if (!isActiveAndEnabled) return;
            var host = SimulationHost.Instance;
            var session = host != null ? host.Session : null;
            if (!ReferenceEquals(session, _session) || host != _host)
            {
                Clear();
                if (session == null) return;
                _session = session; _host = host;
                _host.SessionEnded += Finished;
                if (WardAnchor != null) Ward = At<WardChecklistPanel>(WardAnchor, "Ward checklist (runtime)");
                if (WhoAnchor != null) Who = At<ChecklistBoard>(WhoAnchor, "WHO board (runtime)");
                if (DebriefAnchor != null)
                {
                    _notice = Owned(DebriefAnchor, "Debrief notice (runtime)");
                    var canvas = UiKit.WorldCanvas("Debrief notice", _notice.transform, new Vector2(1400, 180), .0008f);
                    UiKit.Label(canvas.transform, "Notice", "Session review\nUse Finish session in the tablet's Session tab to view the debrief.", new Vector2(24, 20), new Vector2(1352, 140), 28, TMPro.TextAlignmentOptions.Center);
                }
            }
            if (_host == null || _session == null) return;
            if (Ward != null) Ward.gameObject.SetActive(!_host.Ended);
            if (Who != null) Who.gameObject.SetActive(!_host.Ended);
            if (_host.Ended && DebriefAnchor != null && Debrief == null)
            {
                if (_notice != null) _notice.SetActive(false);
                Debrief = At<DebriefView>(DebriefAnchor, "Debrief (runtime)");
            }
        }
        private GameObject Owned(Transform anchor, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(anchor, false); _owned.Add(go); return go;
        }
        private T At<T>(Transform anchor, string name) where T : Component => Owned(anchor, name).AddComponent<T>();
        private void Clear()
        {
            if (_host != null) _host.SessionEnded -= Finished;
            foreach (var go in _owned) if (go != null) { go.SetActive(false); Destroy(go); }
            _owned.Clear(); _host = null; _session = null;
            Ward = null; Who = null; Debrief = null; _notice = null;
        }
    }
}
