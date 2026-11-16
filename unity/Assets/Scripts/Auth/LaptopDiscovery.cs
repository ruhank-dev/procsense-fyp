using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    /// <summary>One laptop heard on the Wi-Fi: its server address and name, and when it was last heard.</summary>
    public sealed class FoundLaptop
    {
        public string Url = "", Name = "";
        public int ApiVersion;
        public float LastHeard;
    }

    /// <summary>
    /// P3-NET-06, headset side: listens for the laptop's discovery beacon (UDP 47800, server/lan/beacon.py) while the laptop screen is open, and lists every ProcSense laptop heard in the last
    /// <see cref="ForgetAfter"/> seconds. The beacon carries no secret; choosing a laptop saves its address (ServerEndpoint) and the pairing code is still typed. Manual entry and adb reverse
    /// stay. Datagrams are read on the main thread with a non-blocking socket (Poll), so nothing runs in the background and nothing is kept after Stop.
    /// </summary>
    public sealed class LaptopDiscovery : IDisposable
    {
        public const int Port = 47800;
        public const float ForgetAfter = 5f;
        [Serializable] private sealed class Beacon { public string service, url, name; public int api_version; }

        private readonly Dictionary<string, FoundLaptop> _found = new Dictionary<string, FoundLaptop>();
        private UdpClient _udp;

        public bool Listening => _udp != null;
        public string Error { get; private set; } = "";

        /// <summary>The laptops heard recently, newest first.</summary>
        public IReadOnlyList<FoundLaptop> Laptops(float now) => _found.Values.Where(l => now - l.LastHeard <= ForgetAfter).OrderByDescending(l => l.LastHeard).ToList();

        /// <summary>Reads one datagram; returns the laptop it announces, or null for anything that is not a ProcSense beacon or not an acceptable address.</summary>
        public FoundLaptop Hear(byte[] data, float now)
        {
            Beacon b = null;
            try { b = JsonUtility.FromJson<Beacon>(Encoding.UTF8.GetString(data)); } catch (ArgumentException) { }
            if (b == null || b.service != "procsense" || string.IsNullOrEmpty(b.url) || !ServiceSettings.IsAcceptable(b.url)) return null;
            if (!_found.TryGetValue(b.url, out var l)) _found[b.url] = l = new FoundLaptop { Url = b.url };
            l.Name = string.IsNullOrEmpty(b.name) ? b.url : b.name;
            l.ApiVersion = b.api_version;
            l.LastHeard = now;
            return l;
        }

        public void Start(int port = Port)
        {
            if (_udp != null) return;
            try
            {
                _udp = new UdpClient(AddressFamily.InterNetwork);
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                _udp.Client.Blocking = false;
                Error = "";
                Lock(true);
            }
            catch (SocketException e) { Error = "Cannot listen for laptops (" + e.SocketErrorCode + ")."; _udp?.Dispose(); _udp = null; }
        }

        /// <summary>Reads every datagram waiting now (call once a frame while the screen is open).</summary>
        public void Poll(float now)
        {
            if (_udp == null) return;
            try
            {
                while (_udp.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    Hear(_udp.Receive(ref from), now);
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { _udp = null; }
        }

        public void Stop() { _udp?.Dispose(); _udp = null; Lock(false); }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _lock;
#endif
        // Android drops broadcast datagrams on Wi-Fi unless a multicast lock is held; held only while listening (permission added by DiscoveryPermission at build)
        private void Lock(bool on)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (on && _lock == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                    {
                        _lock = wifi.Call<AndroidJavaObject>("createMulticastLock", "procsense-discovery");
                        _lock.Call("acquire");
                    }
                }
                else if (!on && _lock != null) { _lock.Call("release"); _lock.Dispose(); _lock = null; }
            }
            catch (AndroidJavaException) { }
#endif
        }
        public void Dispose() => Stop();
    }
}
