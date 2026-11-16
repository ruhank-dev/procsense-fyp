using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ProcSense.VR.Auth
{
    public enum PinDecision { Accept, Refuse }

    /// <summary>
    /// The optional HTTPS of the LAN (P3-NET-15). The laptop run with --tls has a self-signed certificate; its SHA-256 fingerprint comes back at pairing and is kept here (on the device only).
    /// Afterwards a certificate with any other fingerprint is refused, and a refusal raises <see cref="Mismatch"/> for the banner. Pinning applies only to https to a literal private LAN address (or this
    /// computer through adb reverse); the hosted service keeps ordinary certificate checking. Without a pin the certificate is accepted only inside the short pairing window, and the pairing then checks
    /// that the fingerprint the laptop reported is the one the connection actually showed.
    /// </summary>
    public static class TlsPin
    {
        public const string Key = "procsense.tls_pin";
        public static DeviceTokenStore.IStorage Storage = null;      // null = the same PlayerPrefs store as the device token
        private static DeviceTokenStore.IStorage Store => Storage ?? DeviceTokenStore.Storage;

        public static bool Mismatch { get; private set; }
        public static bool PairingWindow { get; set; }
        public static string Observed { get; private set; } = "";

        public static string Get() => Store.Get(Key);
        public static bool IsPinned => !string.IsNullOrEmpty(Get());
        public static void Set(string fingerprint) => Store.Set(Key, Normalize(fingerprint));
        public static void Clear() { Store.Delete(Key); Mismatch = false; Observed = ""; }
        public static void ResetState() { Mismatch = false; Observed = ""; PairingWindow = false; }

        /// <summary>Lower-case hex without separators, so "AA:BB" and "aabb" are the same fingerprint.</summary>
        public static string Normalize(string fingerprint)
        {
            var sb = new StringBuilder();
            foreach (var c in fingerprint ?? "") if (Uri.IsHexDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        public static string Sha256Hex(byte[] der)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder(64);
                foreach (var b in sha.ComputeHash(der ?? new byte[0])) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>True for an https address that must be pinned: a literal private IPv4 host or this computer. Names and public hosts are not pinned (they use the normal certificate check).</summary>
        public static bool AppliesTo(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            return ServiceSettings.IsPrivateIPv4(uri.Host) || uri.Host == "127.0.0.1" || uri.Host == "localhost";
        }

        /// <summary>The decision for the certificate a connection showed. Records the fingerprint seen and sets <see cref="Mismatch"/> when a pinned certificate was refused.</summary>
        public static PinDecision Decide(string certSha256Hex)
        {
            var seen = Normalize(certSha256Hex);
            Observed = seen;
            var pin = Get();
            if (!string.IsNullOrEmpty(pin))
            {
                if (seen == pin) { Mismatch = false; return PinDecision.Accept; }
                Mismatch = true;
                return PinDecision.Refuse;
            }
            return PairingWindow && seen.Length == 64 ? PinDecision.Accept : PinDecision.Refuse;
        }

        public static string Banner() => Mismatch ? "The laptop's security certificate is not the one paired with this headset. The connection was refused; pair again from the laptop if you changed it." : "";
    }

    /// <summary>Unity hook that applies <see cref="TlsPin"/> to one UnityWebRequest (certificateData is the certificate as the platform gives it).</summary>
    public sealed class PinnedCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => TlsPin.Decide(TlsPin.Sha256Hex(certificateData)) == PinDecision.Accept;
    }
}
