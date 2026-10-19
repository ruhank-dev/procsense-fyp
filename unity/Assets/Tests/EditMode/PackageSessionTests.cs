using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The Unity side of a generated scenario package (plan p1-live-scenario-generation-s10, DT-198): the host plays a package as a content folder of one case in front of the real content, gives the session the package hash and the events it plans,
    /// and refuses the package in assessment mode. This is the engine-in-Unity half; fetching the package over the network and the on-device playthrough are Sannan's checks (DC-02).
    /// </summary>
    public class PackageSessionTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Fixture => Path.Combine(Repo, "tests", "fixtures", "generated", "GEN-S1-0003-001.package.json");

        private static IContentReader Real() =>
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        private static SimulationHost NewHost()
        {
            var go = new GameObject("host");
            var host = go.AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            return host;
        }

        [Test]
        public void A_package_is_played_in_practice_mode_with_its_hash_and_planned_events()
        {
            var host = NewHost();
            try
            {
                var text = File.ReadAllText(Fixture);
                var reader = new ScenarioPackageReader(text);
                string hash = host.StartFromPackage(text, 7, SessionMode.Practice, Real());
                Assert.AreEqual(reader.PackageHash, hash);
                Assert.AreEqual(reader.PackageHash, host.PackageHash);
                Assert.AreEqual(reader.CaseId, host.Session.Case.Id);
                Assert.AreEqual(reader.PlannedEvents, host.Session.PlannedEvents);
                Assert.AreEqual(SessionMode.Practice, host.Session.Mode);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_package_is_refused_in_assessment_mode()
        {
            var host = NewHost();
            try
            {
                Assert.Throws<ContentException>(() => host.StartFromPackage(File.ReadAllText(Fixture), 7, SessionMode.Assessment, Real()));
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void An_authored_case_after_a_package_has_no_package_hash()
        {
            var host = NewHost();
            try
            {
                host.StartFromPackage(File.ReadAllText(Fixture), 7, SessionMode.Practice, Real());
                host.StartSession("VAR-S1-A", 42, SessionMode.Practice, Real());
                Assert.AreEqual("", host.PackageHash);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }
    }
}
