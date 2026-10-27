using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Records.Folder
{
    /// <summary>
    /// Where delivered printouts lie (p0-patient-records-emr-s02): one grabbable <see cref="Printout"/> for each document Core has on the desk (<c>RecordsSubsystem.Desk</c>), stacked a little apart; a sheet goes when
    /// Core no longer has it on the desk (filed). Put it on the ward desk; it is not placed in a scene yet (UX-P0-EMR).
    /// </summary>
    public sealed class ReportTray : MonoBehaviour
    {
        private readonly Dictionary<string, Printout> _sheets = new Dictionary<string, Printout>();
        private object _shown;

        public IReadOnlyCollection<Printout> Printouts => _sheets.Values;

        private void Update()
        {
            var host = SimulationHost.Instance;
            if (host != null && host.Observable != _shown) Sync();
        }

        public void Sync()
        {
            var host = SimulationHost.Instance;
            _shown = host != null ? host.Observable : null;
            var desk = host?.Session?.Records?.Desk ?? new List<ProcSense.Core.Records.RecordDocument>();
            var ids = new HashSet<string>(desk.Select(d => d.Id));
            foreach (var gone in _sheets.Where(kv => !ids.Contains(kv.Key) || kv.Value == null).Select(kv => kv.Key).ToList())
            {
                if (_sheets[gone] != null) Destroy(_sheets[gone].gameObject);
                _sheets.Remove(gone);
            }
            foreach (var doc in desk.Where(d => !_sheets.ContainsKey(d.Id)))
                _sheets[doc.Id] = Printout.Create(transform, doc, new Vector3(0.03f * _sheets.Count, 0.004f * (_sheets.Count + 1), 0));
        }
    }
}
