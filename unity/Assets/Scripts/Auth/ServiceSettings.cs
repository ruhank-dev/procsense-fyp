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

        /// <summary>P3-NET-02: plain http to a private LAN address (the networked demonstration laptop) is allowed in development, demo and diagnostic builds only; the study build and the Editor tests keep https plus localhost.</summary>
#if PROCSENSE_DEMO || PROCSENSE_DEV || DEVELOPMENT_BUILD
        public static bool AllowPrivateHttp { get; set; } = true;
#else
        public static bool AllowPrivateHttp { get; set; } = false;
#endif

        /// <summary>True for an https address, an http address that stays on this computer (development), or, when AllowPrivateHttp is on, an http address in a private range (10/8, 172.16/12, 192.168/16).</summary>
        public static bool IsAcceptable(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (url.StartsWith("http://localhost", System.StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://127.0.0.1", System.StringComparison.OrdinalIgnoreCase)) return true;
            return AllowPrivateHttp && url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) && System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) && IsPrivateIPv4(uri.Host);
        }

        /// <summary>True only for a literal private IPv4 address; names are refused so a lookalike host cannot pass.</summary>
        public static bool IsPrivateIPv4(string host)
        {
            if (!System.Net.IPAddress.TryParse(host, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            var b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }
    }
}
