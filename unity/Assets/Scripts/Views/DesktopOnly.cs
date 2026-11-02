using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Presentation-only desktop extras (light shafts): switched off on Android / Quest builds so the standalone headset never pays for them. Desktop and editor are unaffected.</summary>
    [DisallowMultipleComponent]
    public sealed class DesktopOnly : MonoBehaviour
    {
        private void Awake() { if (Application.platform == RuntimePlatform.Android) gameObject.SetActive(false); }
    }
}
