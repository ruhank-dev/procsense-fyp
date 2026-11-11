using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Settings;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The one server address (P3-NET-03): the laptop entered on the headset wins over the build's own, a bad address is refused with a reason, and only ServerEndpoint reads the build-time asset.</summary>
    public class ServerEndpointTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public string GetString(string key) => Values.TryGetValue(key, out var v) ? v : "";
            public bool HasKey(string key) => Values.ContainsKey(key);
            public int GetInt(string key, int fallback) => fallback;
            public void SetString(string key, string value) => Values[key] = value;
            public void DeleteKey(string key) => Values.Remove(key);
            public void Save() { }
        }

        private ISettingsStore _store;
        private System.Func<string> _buildTime;

        [SetUp]
        public void Swap()
        {
            _store = GameSettings.Store;
            _buildTime = ServerEndpoint.BuildTime;
            GameSettings.Store = new MemoryStore();
            ServerEndpoint.BuildTime = () => "https://procsense.onrender.com/";
        }

        [TearDown]
        public void Restore()
        {
            GameSettings.Store = _store;
            ServerEndpoint.BuildTime = _buildTime;
        }

        [Test]
        public void Without_an_entered_address_the_build_one_is_used()
        {
            Assert.AreEqual("https://procsense.onrender.com", ServerEndpoint.Current);
            Assert.AreEqual("built in", ServerEndpoint.Source);
            ServerEndpoint.BuildTime = () => "";
            Assert.AreEqual("none", ServerEndpoint.Source);
        }

        [Test]
        public void The_laptop_typed_as_a_bare_address_gets_http_and_the_demo_port()
        {
            Assert.AreEqual("http://192.168.1.20:8000", ServerEndpoint.Normalise(" 192.168.1.20/ "));
            Assert.AreEqual("http://10.0.0.5:9000", ServerEndpoint.Normalise("10.0.0.5:9000"));
            Assert.AreEqual("https://server.example", ServerEndpoint.Normalise("https://server.example/"));
            Assert.AreEqual("", ServerEndpoint.Normalise("   "));
        }

        [Test]
        public void An_entered_laptop_address_is_used_by_every_client_until_forgotten()
        {
            string heard = null;
            void OnChange(string url) => heard = url;
            ServerEndpoint.Changed += OnChange;
            try
            {
                Assert.AreEqual("", ServerEndpoint.Enter("192.168.1.20"));   // the Editor is a Dev build
                Assert.AreEqual("http://192.168.1.20:8000", ServerEndpoint.Current);
                Assert.AreEqual("entered on this headset", ServerEndpoint.Source);
                Assert.AreEqual("http://192.168.1.20:8000", heard);
                var auth = AuthService.CreateDefault();
                Assert.AreEqual("http://192.168.1.20:8000", auth.BaseUrl, "the sign-in service reads the same endpoint");
                ServerEndpoint.Forget();
                Assert.AreEqual("https://procsense.onrender.com", ServerEndpoint.Current);
            }
            finally { ServerEndpoint.Changed -= OnChange; }
        }

        [Test]
        public void A_public_or_empty_address_is_refused_with_a_reason_and_nothing_is_saved()
        {
            StringAssert.Contains("cannot be used", ServerEndpoint.Enter("8.8.8.8"));
            StringAssert.Contains("cannot be used", ServerEndpoint.Enter("http://laptop.local"));
            StringAssert.Contains("Type the laptop", ServerEndpoint.Enter(""));
            Assert.AreEqual("", ServerEndpoint.Saved);
        }

        [Test]
        public void The_last_address_that_answered_is_remembered()
        {
            ServerEndpoint.MarkGood("http://192.168.1.20:8000/");
            Assert.AreEqual("http://192.168.1.20:8000", ServerEndpoint.LastKnownGood);
        }

        [Test]
        public void Only_ServerEndpoint_reads_the_build_time_address()
        {
            string scripts = Path.Combine(Application.dataPath, "Scripts");
            var readers = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains("ServiceSettings.Load()"))
                .Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEquivalent(new[] { "ServerEndpoint.cs" }, readers);
        }
    }
}
