using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Records.Folder;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The physical folder (p0-patient-records-emr-s02): it opens, its pages are Core's folder documents, three pages turn one at a time over a fixed time whatever the frame rate, reaching a document opens it in
    /// Core, a drag past the threshold turns a page, and a delivered printout dropped into the report pocket is filed and becomes a page.
    /// </summary>
    public class FolderTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private GameObject Made(GameObject go) { _made.Add(go); return go; }

        private ActionOutcome Do(string verb, params (string k, string v)[] p) => _host.Apply(new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(x => x.k, x => x.v)));

        private FolderController Folder()
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            _host = Made(new GameObject("host")).AddComponent<SimulationHost>();
            Assert.AreSame(_host, SimulationHost.Instance);
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            ModeGate.SessionProvider = () => _host.Session;

            var root = Made(new GameObject("folder"));
            root.transform.position = new Vector3(20f, 0f, 0f);                     // away from other test panels, for its picture
            root.AddComponent<BoxCollider>().size = new Vector3(0.23f, 0.02f, 0.31f);
            root.AddComponent<Rigidbody>().isKinematic = true;
            root.AddComponent<XRGrabInteractable>();
            var cover = new GameObject("cover").transform;
            cover.SetParent(root.transform, false);
            var page = new GameObject("identifiers page");
            page.transform.SetParent(root.transform, false);
            root.AddComponent<NotesFolderInteraction>().ConfigureFolder(cover, page);
            return root.AddComponent<FolderController>();
        }

        private static void Land(FolderController f) { for (int i = 0; i < 4; i++) f.Advance(0.1f); }

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void Open_the_folder_turn_three_pages_and_file_a_report()
        {
            var folder = Folder();
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband" }) Assert.IsTrue(Do(v).Accepted, v);
            Assert.IsFalse(folder.TurnNext(), "a shut folder turns no pages");
            Assert.IsTrue(Do("open_notes").Accepted);
            folder.GetComponent<NotesFolderInteraction>().SyncNow();
            folder.Refresh();
            Assert.IsTrue(folder.GetComponent<NotesFolderInteraction>().IsOpen);
            CollectionAssert.AreEqual(new[] { "clerking", "obs_chart" }, folder.Pages.Select(p => p.DocumentId).Distinct().ToList(), "the folder holds this admission's paperwork");

            // a printed result: ordered, taken, delivered to the desk, then dropped into the pocket
            Assert.IsTrue(Do("confirm_identity").Accepted);
            Assert.IsTrue(Do("order_investigation", ("test", "fbc")).Accepted);
            Assert.IsTrue(Do("take_sample", ("test", "fbc"), ("tube", "edta_purple")).Accepted);
            for (int i = 0; i < 24 && _host.Session.Records.Desk.Count == 0; i++) _host.Tick(15 * 60);
            Assert.AreEqual(1, _host.Session.Records.Desk.Count, "the porter brings the printout to the desk");
            var tray = Made(new GameObject("desk")).AddComponent<ReportTray>();
            tray.Sync();
            var printout = tray.Printouts.Single();
            StringAssert.Contains(_host.Session.Records.Desk[0].Title, printout.Text);
            folder.Pocket.interactionManager.SelectEnter((IXRSelectInteractor)folder.Pocket, (IXRSelectInteractable)printout.GetComponent<XRGrabInteractable>());
            Assert.AreEqual("", folder.LastRejection);
            Assert.IsEmpty(_host.Session.Records.Desk, "filed");
            Assert.AreEqual(printout.DocumentId, folder.Pages.Last().DocumentId, "the filed report is the folder's last page");
            Assert.GreaterOrEqual(folder.PageCount, 4);

            int log = _host.Session.Log.Count;
            for (int turn = 1; turn <= 3; turn++)
            {
                Assert.IsTrue(folder.TurnNext(), "turn " + turn);
                Assert.IsTrue(folder.Turning);
                Assert.IsFalse(folder.TurnNext(), "one page at a time");
                Land(folder);
                Assert.IsFalse(folder.Turning);
                Assert.AreEqual(turn, folder.PageIndex);
                StringAssert.Contains(folder.Pages[turn - 1].Title, folder.HeadingText);
            }
            Assert.AreEqual("Page 4 of " + folder.PageCount, folder.FooterText);
            Snapshots.Photograph(folder.PageCanvas, Snapshots.Out("folder-page.png"));
            var opened = _host.Session.Log.Entries.Skip(log).Where(e => e.Verb == "open_document").ToList();
            Assert.AreEqual(folder.Pages.Take(3).Select(p => p.DocumentId).Distinct().Count(), opened.Count, "reaching each document opens it once");
            Assert.IsTrue(opened.All(a => a.Params["view"] == "folder"));
            StringAssert.Contains("Ayesha Khan", folder.Pages.First(p => p.DocumentId == "clerking").Text, "the clerking note is written from the session");
        }

        [Test]
        public void A_turn_takes_the_same_time_at_any_frame_rate_and_a_drag_past_the_threshold_turns_a_page()
        {
            var folder = Folder();
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes" }) Assert.IsTrue(Do(v).Accepted, v);
            folder.GetComponent<NotesFolderInteraction>().SyncNow();
            folder.Refresh();

            Assert.IsTrue(folder.TurnNext());
            for (int i = 0; i < 10; i++) folder.Advance(0.02f);            // 0.2 s in small frames
            Assert.AreEqual(0.5f, folder.TurnProgress, 1e-4f);
            folder.Advance(0.2f);                                              // and 0.2 s in one frame
            Assert.IsFalse(folder.Turning);
            Assert.AreEqual(1, folder.PageIndex);

            var start = folder.transform.TransformPoint(new Vector3(0.08f, 0.02f, 0));
            folder.BeginDrag(start);
            folder.Drag(folder.transform.TransformPoint(new Vector3(0.05f, 0.02f, 0)));
            Assert.IsFalse(folder.Turning, "a short drag does not turn the page");
            folder.Drag(folder.transform.TransformPoint(new Vector3(0.01f, 0.02f, 0)));
            Assert.IsTrue(folder.Turning, "past the threshold towards the spine turns forward");
            Land(folder);
            Assert.AreEqual(2, folder.PageIndex);
            Assert.IsFalse(folder.TurnNext(), "the last page");
            Assert.IsTrue(folder.TurnPrevious());
            Land(folder);
            Assert.AreEqual(1, folder.PageIndex);
        }
    }
}
