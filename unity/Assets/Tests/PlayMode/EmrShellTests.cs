using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Records.Emr;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The EMR shell (p0-patient-records-emr-s03): the quick list shows the patient in the bay first; a search for "khan" ranks VAR-S1-A's patient above the near-miss name; the Profile, Labs and Imaging tabs open
    /// together, switch and close, six at most; documents open through Core (logged), and a distractor patient has no record.
    /// </summary>
    public class EmrShellTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go.AddComponent<T>();
        }

        private EmrShell Start()
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);   // a host another test left behind would be the one the shell reads
            _host = Make<SimulationHost>("host");
            Assert.AreSame(_host, SimulationHost.Instance);
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            ModeGate.SessionProvider = () => _host.Session;
            return Make<EmrShell>("emr");
        }

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void Searching_khan_puts_the_patient_in_the_bay_first_and_her_tabs_open_switch_and_close()
        {
            var emr = Start();
            Assert.IsTrue(emr.Shown[0].IsCasePatient, "the quick list shows the patient in the bay first");

            foreach (var c in "khan".ToUpperInvariant()) emr.Search.Type(c.ToString());
            Assert.AreEqual("KHAN", emr.Search.Query);
            int before = _host.Session.Log.Count;
            Assert.IsTrue(emr.Find(emr.Search.Query).Accepted);
            Assert.AreEqual(before + 1, _host.Session.Log.Count, "the search is a logged action");
            Assert.IsTrue(emr.Shown[0].IsCasePatient, "VAR-S1-A's patient first");
            Assert.IsTrue(emr.Shown.Any(p => !p.IsCasePatient && p.Name.Contains("Khan")), "the near-miss name is found too, below her");

            Snapshots.Photograph(emr.Canvas, Snapshots.Out("emr-shell-search.png"));
            Assert.IsTrue(emr.Choose(emr.Shown[0].Id));
            StringAssert.Contains("Ayesha Khan", emr.BannerText);
            StringAssert.Contains("MR 102938", emr.BannerText, "the record number as the wristband prints it (DT-264)");
            StringAssert.Contains("Patient bay", emr.BannerText, "where she is now (DT-264)");
            Assert.IsTrue(emr.Shown.Where(p => !p.IsCasePatient).All(p => p.Location.Length > 0), "every distractor has a ward and bed");
            Assert.AreEqual("profile", emr.Tabs.Active, "the record opens on the profile");
            Assert.IsTrue(emr.OpenTab("labs"));
            Assert.IsTrue(emr.OpenTab("imaging"));
            CollectionAssert.AreEqual(new[] { "profile", "labs", "imaging" }, emr.Tabs.Open.ToList(), "several tabs open at once, in the order opened");
            Assert.AreEqual(3, emr.TabLabels.Count);
            StringAssert.Contains("Gallstones", emr.BodyText, "the imaging tab shows the earlier scan");

            Assert.IsTrue(emr.SwitchTab("profile"));
            StringAssert.Contains("Record number", emr.BodyText);
            Snapshots.Photograph(emr.Canvas, Snapshots.Out("emr-shell-imaging.png"));
            Assert.IsTrue(emr.CloseTab("labs"));
            CollectionAssert.AreEqual(new[] { "profile", "imaging" }, emr.Tabs.Open.ToList());
            Assert.AreEqual("profile", emr.Tabs.Active, "closing another tab keeps the one in front");
        }

        [Test]
        public void Six_tabs_at_most_documents_open_through_core_and_a_distractor_has_no_record()
        {
            var emr = Start();
            Assert.IsTrue(emr.Choose(emr.Shown.First(p => p.IsCasePatient).Id));
            var sections = _host.Session.Records.Tabs.Select(t => t.Id).ToList();
            Assert.AreEqual(7, sections.Count);
            foreach (var id in sections.Take(6)) Assert.IsTrue(emr.OpenTab(id));
            Assert.IsFalse(emr.OpenTab(sections[6]), "a seventh tab is refused");
            StringAssert.Contains("close one first", emr.StatusText);

            Assert.IsTrue(emr.SwitchTab("imaging"));
            Assert.IsNotEmpty(emr.Documents);
            var doc = emr.Documents[0];
            var opened = emr.OpenDocument(doc.Id);
            Assert.IsTrue(opened.Accepted);
            CollectionAssert.Contains(opened.Effects, "opened:" + doc.Id);
            if (doc.ImageId.Length == 0) Assert.AreEqual("NO_IMAGE", emr.ViewImage(doc.Id).RejectReason, "a report without a picture has no image to view");

            var other = emr.Shown.First(p => !p.IsCasePatient);
            Assert.IsTrue(emr.Choose(other.Id));
            Assert.IsEmpty(emr.Tabs.Open);
            Assert.IsFalse(emr.OpenTab("labs"), "a distractor has no record to open");
            StringAssert.Contains("No record", emr.BodyText);
            StringAssert.Contains(other.Mrn, emr.BannerText);
        }
    }
}
