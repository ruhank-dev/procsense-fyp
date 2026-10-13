using UnityEngine;

namespace ProcSense.VR.Boot
{
    /// <summary>Presentation arrival point; never changes Core patient location.</summary>
    public sealed class RoomAnchor : MonoBehaviour
    {
        public string FromRoom = "Default";
        public string LocationId = "LOC-BAY";
    }
}
