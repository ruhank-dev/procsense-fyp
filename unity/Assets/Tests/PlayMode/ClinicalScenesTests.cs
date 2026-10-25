using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The scenes and panels that were missing (plans p0-evaluation-performance-s04, p1-theatre-extension-s02 and s03, p0-patient-records-emr-s04 and s05, p0-diagnostic-transitions-s04 and s05):
    /// the main menu, the recovery bay with its nurse and handover panel, and the EMR panel, event cards and diagnostics props installed with the session. Placement in the headset and comfort are Sannan's checks.</summary>
    public class ClinicalScenesTests
    {
        [SetUp] public void Setup() { ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false; ModeChoice.Reset(); }
        [TearDown] public void Teardown() { ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null; ModeChoice.Reset(); ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null; }
        [UnityTearDown] public IEnumerator Cleanup() => EditorXRTestEnvironment.Cleanup();

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            SimulationHost.Instance.AutoTick = false;
            SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        [UnityTest]
        public IEnumerator The_main_menu_scene_chooses_the_mode_and_starts_boot_without_a_clinical_action()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsNotNull(menu);
            string loaded = null;
            menu.LoadScene = name => loaded = name;
            menu.Chooser.AssessmentButton.onClick.Invoke();
            Assert.AreEqual(SessionMode.Assessment, ModeChoice.Chosen);
            Assert.IsTrue(menu.Start_());
            Assert.AreEqual("Boot", loaded);
            Assert.AreEqual(SessionMode.Assessment, menu.Mode);
            Assert.IsNull(SimulationHost.Instance);                                       // the menu starts no session itself
            ModeChoice.Lock();
            Assert.IsFalse(menu.Start_());                                                // a running session keeps its mode
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_practice_hint_of_the_menu_is_shown_in_practice_and_not_in_assessment()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            yield return null;
            Assert.IsTrue(menu.PracticeHint.transform.GetChild(0).gameObject.activeSelf);       // no session yet: practice aids are on
            ModeGate.SessionProvider = () => new Session(ProcSense.Core.Content.ContentLoader.Load(new ProcSense.Core.Content.FileContentReader(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "content"))), "VAR-S1-A").Cases["VAR-S1-A"], 1, SessionMode.Assessment, ProcSense.Core.Workflow.Subsystems.Default(ProcSense.Core.Content.ContentLoader.Load(new ProcSense.Core.Content.FileContentReader(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "content"))), "VAR-S1-A")));
            yield return null;
            Assert.IsFalse(menu.PracticeHint.transform.GetChild(0).gameObject.activeSelf);      // assessment: no hint
        }

        [UnityTest]
        public IEnumerator The_session_installs_the_emr_panel_the_event_cards_and_the_diagnostics_props_once()
        {
            yield return Boot();
            var installer = Object.FindFirstObjectByType<ClinicalPanelsInstaller>();
            Assert.IsNotNull(installer, "Sys_Core carries the installer");
            for (int i = 0; i < 5 && installer.Emr == null; i++) yield return null;
            Assert.IsNotNull(installer.Emr);
            Assert.IsNotNull(installer.Cards);
            Assert.IsNotNull(installer.Diagnostics);
            Assert.IsNotNull(installer.Subtitles);
            Assert.GreaterOrEqual(installer.Emr.TabButtons.Count, 7);
            var emr = installer.Emr;
            yield return null;
            Assert.AreSame(emr, installer.Emr);                                               // created once
            Assert.AreEqual(1, Object.FindObjectsByType<EmrPanelView>().Length);
        }

        [UnityTest]
        public IEnumerator An_anchor_in_the_scene_moves_a_panel_to_where_it_was_put()
        {
            yield return Boot();
            var anchor = new GameObject("EmrPanelAnchor");
            anchor.transform.position = new Vector3(3.3f, 1.1f, -2.2f);
            var moved = new GameObject("second installer").AddComponent<ClinicalPanelsInstaller>();      // a fresh installer places its panel at the anchor the scene holds
            Assert.IsTrue(moved.Install());
            Assert.AreEqual(anchor.transform.position, moved.Emr.transform.position);
            Object.Destroy(anchor);
        }

        [UnityTest]
        public IEnumerator The_recovery_bay_shows_the_nurse_and_the_handover_items_and_the_engine_refuses_before_the_sign_out()
        {
            yield return Boot();
            yield return SceneManager.LoadSceneAsync("Recovery", LoadSceneMode.Additive);
            var view = Object.FindFirstObjectByType<RecoveryHandoverView>();
            for (int i = 0; i < 5 && view.Canvas == null; i++) yield return null;
            Assert.IsNotNull(view.Canvas);
            Assert.IsNotNull(view.Nurse);
            Assert.Greater(view.ItemButtons.Count, 0);
            Assert.AreEqual(view.ItemIds.Count, view.ItemButtons.Count);
            view.ItemButtons[0].onClick.Invoke();
            Assert.AreEqual("Confirm the patient's identity first.", view.Status);                // the engine decides; the view only says so
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" })
                Assert.IsTrue(SimulationHost.Instance.Apply(new ProcSense.Core.Actions.SimAction(verb)).Accepted, verb);
            view.ItemButtons[0].onClick.Invoke();
            Assert.AreEqual("Finish the sign out first.", view.Status);
            view.FinishButton.onClick.Invoke();
            Assert.AreEqual("Finish the sign out first.", view.Status);
        }
    }
}
