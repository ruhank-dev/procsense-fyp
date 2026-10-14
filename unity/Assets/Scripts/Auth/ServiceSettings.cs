using UnityEngine;

namespace ProcSense.VR.Auth
{
    /// <summary>
    /// Where the hosted ProcSense service is. A build setting, never a constant in code: the asset is `Resources/ProcSenseService`
    /// (ProcSense -> Service settings in the Editor creates it). Nothing secret is in it; the Supabase address and the publishable key
    /// are fetched from the service itself (`/config.json`), so there is one address to maintain.
    /// </summary>
    [CreateAssetMenu(menuName = "ProcSense/Service settings")]
    public sealed class ServiceSettings : ScriptableObject
    {
        public const string ResourceName = "ProcSenseService";

        [Tooltip("The service address, https only (http is accepted for localhost while developing).")]
        public string BaseUrl = "";

        [Tooltip("Seconds to wait for one request. The free host sleeps when idle and can take about a minute to wake.")]
        public int RequestTimeoutSeconds = 100;

        /// <summary>The settings asset, or null when the build has none (the headset then works offline only).</summary>
        public static ServiceSettings Load() => Resources.Load<ServiceSettings>(ResourceName);

        /// <summary>True for an https address, or an http address that stays on this computer (development).</summary>
        public static bool IsAcceptable(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase)) return true;
            return url.StartsWith("http://localhost", System.StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://127.0.0.1", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
