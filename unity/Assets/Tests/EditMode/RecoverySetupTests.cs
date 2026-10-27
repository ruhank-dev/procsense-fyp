using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.EditorTools;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The P1 theatre equipment (p1-theatre-extension-s04): the recovery bay manifest places the real monitor prefab and a labelled stand-in for any piece whose model is not built yet; the specimen label shows the
    /// running patient, never baked-in text.
    /// </summary>
    public class RecoverySetupTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [Test]
        public void The_recovery_bay_reuses_the_monitor_and_stands_in_for_what_is_not_built_yet()
        {
            var root = new GameObject("RecoveryBay test");
            try
            {
                var placed = RecoverySetup.Place(root.transform);
                CollectionAssert.AreEquivalent(new[] { "Monitor", "Oxygen", "Suction" }, placed.Keys.ToList());
                Assert.AreEqual("Recovery Monitor", placed["Monitor"].name, "the P0 monitor prefab, not a stand-in");
                Assert.IsNotNull(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(placed["Monitor"]));
                foreach (var id in RecoverySetup.StandIns()) StringAssert.EndsWith("(stand-in)", placed[id].name, id + " has no model yet");
                Assert.IsTrue(placed.Values.All(g => g.transform.parent == root.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void The_specimen_label_shows_the_running_patient()
        {
            var hostGo = new GameObject("host");
            var labelGo = new GameObject("specimen");
            try
            {
                var host = hostGo.AddComponent<SimulationHost>();
                host.AutoTick = false;
                host.SaveLogOnDestroy = false;
                host.StartSession("VAR-S1-A", 42, SessionMode.Practice,
                    new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))));
                var label = labelGo.AddComponent<SpecimenLabelView>();
                label.Show(host.Session);
                var id = host.Session.Case.Identity;
                StringAssert.Contains(id.FullName, label.Text);
                StringAssert.Contains(id.Mrn, label.Text);
            }
            finally { Object.DestroyImmediate(labelGo); Object.DestroyImmediate(hostGo); }
        }
    }
}
