using System;
using ProcSense.VR.Settings;

namespace ProcSense.VR.Auth
{
    /// <summary>
    /// The one server address every client uses (P3-NET-03, ADR-0032 D2). In order: the address entered on the headset (the laptop on the room's
    /// Wi-Fi, typed on the server screen's keypad or found by discovery, P3-NET-06), else the address the build came with (`Resources/ProcSenseService`).
    /// The last address that answered is remembered, so a laptop that moved can be found again. Only this class reads the build-time asset; the code
    /// search test in ServerEndpointTests keeps it that way.
    /// </summary>
    public static class ServerEndpoint
    {
        public const string SavedKey = "procsense.server", LastGoodKey = "procsense.server_last_good";
        public const int DefaultPort = 8000;

        /// <summary>The build-time address (tests replace it).</summary>
        public static Func<string> BuildTime = () => { var s = ServiceSettings.Load(); return s != null ? s.BaseUrl ?? "" : ""; };
        public static Func<int> BuildTimeout = () => { var s = ServiceSettings.Load(); return s != null ? s.RequestTimeoutSeconds : 100; };

        /// <summary>Raised when the address in use changes (clients rebuild their connection).</summary>
        public static event Action<string> Changed;

        private static ISettingsStore Store => GameSettings.Store;

        /// <summary>The address entered on this headset ("" when none).</summary>
        public static string Saved => Store.HasKey(SavedKey) ? Store.GetString(SavedKey) : "";

        /// <summary>The last address that answered ("" when none yet).</summary>
        public static string LastKnownGood => Store.HasKey(LastGoodKey) ? Store.GetString(LastGoodKey) : "";

        /// <summary>The address every client uses now: the saved one when this build accepts it, else the build-time one.</summary>
        public static string Current
        {
            get
            {
                var saved = Saved;
                if (saved.Length > 0 && ServiceSettings.IsAcceptable(saved)) return saved;
                return (BuildTime() ?? "").TrimEnd('/');
            }
        }

        /// <summary>Where <see cref="Current"/> comes from, for the server screen: "entered on this headset", "built in" or "none".</summary>
        public static string Source => Saved.Length > 0 && ServiceSettings.IsAcceptable(Saved) ? "entered on this headset" : (BuildTime() ?? "").Length > 0 ? "built in" : "none";

        /// <summary>
        /// Saves an address typed on the headset. A bare IPv4 address gets http:// and port 8000 (what `task demo-laptop` serves); anything this build
        /// may not call is refused with the reason and nothing is saved.
        /// </summary>
        public static string Enter(string typed)
        {
            var url = Normalise(typed);
            if (url.Length == 0) return "Type the laptop's address, for example 192.168.1.20.";
            if (!ServiceSettings.IsAcceptable(url))
                return Boot.BuildVariant.Name == Boot.BuildVariant.Study
                    ? "This build only uses secure (https) addresses."
                    : "That address cannot be used: use the laptop's address on this Wi-Fi (it starts 192.168., 10. or 172.16 to 172.31.), or an https address.";
            if (url == Saved) return "";
            Store.SetString(SavedKey, url);
            Store.Save();
            Changed?.Invoke(Current);
            return "";
        }

        /// <summary>Forgets the entered address: the build-time one is used again.</summary>
        public static void Forget()
        {
            if (!Store.HasKey(SavedKey)) return;
            Store.DeleteKey(SavedKey);
            Store.Save();
            Changed?.Invoke(Current);
        }

        /// <summary>A client reached this address: remember it as the last one that worked.</summary>
        public static void MarkGood(string url)
        {
            if (string.IsNullOrEmpty(url) || url == LastKnownGood) return;
            Store.SetString(LastGoodKey, url.TrimEnd('/'));
            Store.Save();
        }

        /// <summary>"192.168.1.20" → "http://192.168.1.20:8000"; a full address keeps its scheme and port; no trailing slash.</summary>
        public static string Normalise(string typed)
        {
            var t = (typed ?? "").Trim().TrimEnd('/');
            if (t.Length == 0) return "";
            if (t.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                var host = t.Split(':')[0];
                t = "http://" + t + (t.Contains(":") ? "" : ServiceSettings.IsPrivateIPv4(host) || host == "localhost" || host == "127.0.0.1" ? ":" + DefaultPort : "");
            }
            return t;
        }
    }
}
