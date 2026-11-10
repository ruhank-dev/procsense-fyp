using System.Collections.Generic;
using ProcSense.VR.Bridge;
using ProcSense.VR.Records.Emr;
using ProcSense.VR.Records.Folder;
using ProcSense.VR.UI.Results;
using UnityEngine;

namespace ProcSense.VR.Records
{
    /// <summary>Owns the bay's record views. Build after Core supplies a case; discard case-local UI and paper when that session changes.</summary>
    public sealed class RecordsWorkstation : MonoBehaviour
    {
        public Transform EmrAnchor;
        public Transform ResultsAnchor;
        public Transform PhoneAnchor;
        public Transform ReportAnchor;

        public EmrShell Emr { get; private set; }
        public ResultsView Results { get; private set; }
        public PhoneView Phone { get; private set; }
        public ReportTray Tray { get; private set; }
        private object _session;
        private readonly List<GameObject> _owned = new List<GameObject>();

        private void Update() => Sync();
        private void OnDisable() => Clear();

        public void Sync()
        {
            if (!isActiveAndEnabled) return;
            var session = SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            if (ReferenceEquals(session, _session) && (session == null || (Emr != null && Results != null && Phone != null && Tray != null))) return;
            Clear();
            if (session == null || EmrAnchor == null || ResultsAnchor == null || PhoneAnchor == null || ReportAnchor == null) return;
            _session = session;
            Emr = At<EmrShell>(EmrAnchor, "EMR (runtime)");
            Results = At<ResultsView>(ResultsAnchor, "Results (runtime)");
            Phone = At<PhoneView>(PhoneAnchor, "Phone (runtime)");
            Tray = At<ReportTray>(ReportAnchor, "Reports (runtime)");
            Tray.Sync();
        }

        private T At<T>(Transform anchor, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(anchor, false);
            _owned.Add(go);
            return go.AddComponent<T>();
        }

        private void Clear()
        {
            // XRI reparents a held printout out of the tray. Keep ownership of those sheets across a case reset too.
            if (Tray != null)
                foreach (var paper in Tray.Printouts)
                    if (paper != null) { paper.gameObject.SetActive(false); Destroy(paper.gameObject); }
            // Disable immediately: deferred destruction must not leave yesterday's buttons or sockets active for another frame.
            foreach (var go in _owned)
                if (go != null) { go.SetActive(false); Destroy(go); }
            _owned.Clear();
            _session = null;
            Emr = null; Results = null; Phone = null; Tray = null;
        }
    }
}
