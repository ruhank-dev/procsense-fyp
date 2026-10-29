using UnityEngine;

namespace ProcSense.VR.Environment
{
    /// <summary>Marks the root of what a room manifest built (p0-environment-rooms-s04): placing again replaces everything under it, so never hand-move an object under it; edit the manifest.</summary>
    [DisallowMultipleComponent]
    public sealed class ManifestOwned : MonoBehaviour
    {
        public string Room = "";
    }
}
