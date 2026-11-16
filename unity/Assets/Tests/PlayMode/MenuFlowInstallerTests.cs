using System.Collections;
using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The shipped player boots into the game flow (P3-02): MenuFlow.Current exists after Boot, and Menu -> scenario -> Submit -> Results -> Retry advances through the installed glass panel.</summary>
    public class MenuFlowInstallerTests
    {
        [TearDown]
        public void TearDown()
        {
            ProcSense.VR.Boot.BootLoader.MenuStartsSession = !Application.isEditor;
            SimulationHost.AutoStartFromConfig = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            SimulationHost.SessionsDirectoryOverride = null;
            ModeChoice.Reset();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Boot_creates_the_flow_and_the_menu_reaches_results()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "installer_tests");
            ProcSense.VR.Boot.BootLoader.MenuStartsSession = true;                    // the player configuration: the menu, not the fixed case, starts the session
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            var host = SimulationHost.Instance; host.AutoTick = false; host.SaveLogOnDestroy = false;
            for (int i = 0; i < 20 && MenuFlow.Current == null; i++) yield return null;

            Assert.IsNotNull(MenuFlow.Current, "the player creates the game flow");
            Assert.IsNull(host.Session, "no fixed case is started before the menu chooses");
            var installer = Object.FindFirstObjectByType<MenuFlowInstaller>();
            Assert.IsNotNull(installer);
            var flow = installer.Flow; var panel = installer.Panel;
            Assert.AreEqual(MenuPhase.MainMenu, flow.Phase, "after sign-in the flow opens at the main menu, not the placeholder login");
            CollectionAssert.Contains(panel.VisibleButtons(), "TRAINING MODE");

            Assert.IsTrue(panel.Click("TRAINING MODE"));
            Assert.IsTrue(panel.Click(System.Linq.Enumerable.First(panel.VisibleButtons(), b => b.StartsWith("Gallbladder removal"))));
            Assert.AreEqual(MenuPhase.Briefing, flow.Phase, flow.Message);
            Assert.AreEqual(flow.Plan.CaseId, host.Session.Case.Id, "the chosen case is the one loaded");
            for (int i = 0; i < 5; i++) yield return null;
            var emr = Object.FindFirstObjectByType<ProcSense.VR.Views.ClinicalPanelsInstaller>().Emr;
            Assert.IsNotNull(emr, "the EMR panel is built for the chosen case");
            var tabs = ProcSense.VR.Views.EmrText.Build(host.Session.Records);
            foreach (var t in tabs) if (t.Id == emr.SelectedTab) Assert.AreEqual(t.Body, emr.BodyText, "the EMR shows the menu-chosen case's own record");
            Assert.IsTrue(panel.Click("Begin"));
            Assert.IsTrue(panel.Click("Submit patient to OT"));
            Assert.IsTrue(panel.Click("Confirm: submit patient to OT"));
            Assert.AreEqual(MenuPhase.Results, flow.Phase);
            Assert.IsTrue(panel.Click("Review details"));   // P3-RES-05
            Assert.IsTrue(panel.DebriefOpen);
            Assert.IsTrue(panel.Click("Retry"));
            Assert.AreEqual(MenuPhase.Briefing, flow.Phase);
            Assert.IsFalse(panel.DebriefOpen);
        }
    }
}
