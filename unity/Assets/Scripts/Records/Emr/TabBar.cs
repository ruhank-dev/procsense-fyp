using System;
using System.Collections.Generic;

namespace ProcSense.VR.Records.Emr
{
    /// <summary>
    /// The open tabs of the EMR shell (p0-patient-records-emr-s03): several record sections open at once, at most six, in the order they were opened (no reordering), one of them active. Opening an open tab switches to it;
    /// closing the active one makes its neighbour active. Pure screen state: Core logs the documents opened, not the tabs.
    /// </summary>
    public sealed class TabBar
    {
        public const int Max = 6;
        private readonly List<string> _open = new List<string>();

        public IReadOnlyList<string> Open => _open;
        public string Active { get; private set; } = "";

        /// <summary>Opens the section as a tab, or switches to it when it is already open. False when six are open already.</summary>
        public bool OpenTab(string id)
        {
            if (_open.Contains(id)) { Active = id; return true; }
            if (_open.Count >= Max) return false;
            _open.Add(id);
            Active = id;
            return true;
        }

        public bool Switch(string id)
        {
            if (!_open.Contains(id)) return false;
            Active = id;
            return true;
        }

        public bool Close(string id)
        {
            int i = _open.IndexOf(id);
            if (i < 0) return false;
            _open.RemoveAt(i);
            if (Active == id) Active = _open.Count == 0 ? "" : _open[Math.Min(i, _open.Count - 1)];
            return true;
        }

        public void Clear()
        {
            _open.Clear();
            Active = "";
        }
    }
}
