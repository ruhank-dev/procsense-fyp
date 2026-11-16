using System.Net;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Auth;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-NET-06: the headset lists a laptop from its beacon (the same datagram server/lan/beacon.py sends), ignores anything else, forgets one not heard for five seconds, and receives a real datagram.</summary>
    public class LaptopDiscoveryTests
    {
        private static byte[] Beacon(string url, string name = "laptop") => Encoding.UTF8.GetBytes("{\"service\":\"procsense\",\"url\":\"" + url + "\",\"api_version\":1,\"name\":\"" + name + "\"}");

        [Test]
        public void A_beacon_lists_the_laptop_and_it_is_forgotten_after_five_seconds()
        {
            using (var d = new LaptopDiscovery())
            {
                var l = d.Hear(Beacon("http://127.0.0.1:8000"), 10f);
                Assert.IsNotNull(l);
                Assert.AreEqual("laptop", l.Name);
                d.Hear(Beacon("http://127.0.0.1:8000"), 11f);
                Assert.AreEqual(1, d.Laptops(12f).Count, "one laptop however often it is heard");
                Assert.AreEqual(1, d.Laptops(11f + LaptopDiscovery.ForgetAfter).Count);
                Assert.AreEqual(0, d.Laptops(11.1f + LaptopDiscovery.ForgetAfter).Count, "not heard for five seconds: gone");
                Assert.IsNull(d.Hear(Encoding.UTF8.GetBytes("{\"service\":\"other\",\"url\":\"http://127.0.0.1:8000\"}"), 12f));
                Assert.IsNull(d.Hear(Encoding.UTF8.GetBytes("not json"), 12f));
                Assert.IsNull(d.Hear(Beacon("http://203.0.113.9:8000"), 12f), "a public plain-http address is never accepted");
            }
        }

        [Test]
        public void The_android_build_gets_the_multicast_permission_once()
        {
            var path = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "AndroidManifest.xml");
            System.IO.File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"utf-8\"?><manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application/></manifest>");
            Assert.IsTrue(ProcSense.EditorTools.Build.DiscoveryPermission.Add(path));
            Assert.IsFalse(ProcSense.EditorTools.Build.DiscoveryPermission.Add(path), "added once");
            StringAssert.Contains(ProcSense.EditorTools.Build.DiscoveryPermission.Permission, System.IO.File.ReadAllText(path));
        }

        [Test]
        public void A_real_datagram_is_received()
        {
            int port;
            using (var probe = new UdpClient(0)) port = ((IPEndPoint)probe.Client.LocalEndPoint).Port;
            using (var d = new LaptopDiscovery())
            {
                d.Start(port);
                Assert.IsTrue(d.Listening, d.Error);
                using (var tx = new UdpClient()) tx.Send(Beacon("http://127.0.0.1:8000"), Beacon("http://127.0.0.1:8000").Length, new IPEndPoint(IPAddress.Loopback, port));
                for (int i = 0; i < 50 && d.Laptops(1f).Count == 0; i++) { System.Threading.Thread.Sleep(20); d.Poll(1f); }
                Assert.AreEqual("http://127.0.0.1:8000", d.Laptops(1f)[0].Url);
                d.Stop();
                Assert.IsFalse(d.Listening);
            }
        }
    }
}
