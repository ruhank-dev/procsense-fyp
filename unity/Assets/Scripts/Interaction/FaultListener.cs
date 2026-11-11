using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// Shows an equipment fault (a suction that does not work, a sterility indicator that failed) only when Core says so. Core's observable state does not publish equipment faults yet
    /// (a Core change, owner Ruhan), so the provider is empty and this listener is inert until a provider is set: nothing is invented. The listener reads and shows; it never sends an action.
    /// </summary>
    public sealed class FaultState
    {
        private readonly HashSet<string> _active = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Active => _active;
        public bool Has(string faultId) => _active.Contains(faultId);

        /// <summary>Raised with the fault id and whether it is now on, for each change.</summary>
        public event Action<string, bool> Changed;

        /// <summary>Takes the full list of faults Core reports now and raises one event per fault that appeared or cleared.</summary>
        public void Update(IEnumerable<string> reported)
        {
            var now = new HashSet<string>(reported ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (var id in _active.Except(now).ToList()) { _active.Remove(id); Changed?.Invoke(id, false); }
            foreach (var id in now.Except(_active).ToList()) { _active.Add(id); Changed?.Invoke(id, true); }
        }
    }

    /// <summary>Polls a provider of Core's reported faults and shows a fault visual (a child object) per fault id. With no provider it does nothing.</summary>
    [DisallowMultipleComponent]
    public sealed class FaultListener : MonoBehaviour
    {
        [Serializable]
        public sealed class FaultVisual { public string faultId = ""; public GameObject show; }

        [SerializeField] private List<FaultVisual> visuals = new List<FaultVisual>();

        public FaultState State { get; } = new FaultState();

        /// <summary>Set by whoever can read Core's faults (none can yet). Null keeps the listener inert.</summary>
        public Func<IEnumerable<string>> Provider { get; set; }

        private void OnEnable() => State.Changed += Show;
        private void OnDisable() => State.Changed -= Show;

        private void Update()
        {
            if (Provider != null) State.Update(Provider());
        }

        private void Show(string id, bool on)
        {
            foreach (var v in visuals) if (v.faultId == id && v.show != null) v.show.SetActive(on);
        }
    }
}
