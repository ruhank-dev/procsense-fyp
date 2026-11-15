using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Sharp clinical text on Quest 2 without composition layers (14 Nov 2026 UTC, docs/audit/graphics_matrix.md "compositor layers for UI"): the EMR tablet, EMR, monitor and menu panels
    /// share one dilated SDF material, other panels keep the font's own, a clinical panel in front of the head is reported as being read, and the text sizes on them are reported in
    /// estimated Quest 2 eye-buffer pixels at 0.7 / 0.8 / 0.9 (an estimate for the headset check DC-13, never a legibility claim).
    /// </summary>
    public sealed class ClinicalTextTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go.AddComponent<T>();
        }

        [UnitySetUp]
        public System.Collections.IEnumerator Setup()
        {
            yield return TestHostHygiene.ResetToEmptyScene();                    // a room or Boot left by an earlier fixture adds its own Tablet/EMR/Monitor canvases to the search below
            AccessibilitySettings.Reset();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            _host = Make<SimulationHost>("clinical text host");
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _made) Object.DestroyImmediate(go);
            _made.Clear();
            AccessibilitySettings.Reset();
        }

        private List<AccessibleCanvas> BuildPanels()
        {
            Make<TabletView>("clinical tablet").Build();
            Make<MonitorView>("clinical monitor");
            Make<ProcSense.VR.Records.Emr.EmrShell>("clinical emr");
            var flowRoot = new GameObject("clinical flow"); _made.Add(flowRoot);
            ulong seed = 1;
            flowRoot.AddComponent<FlowPanel>().Build(new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => seed++) { EndSessionOnSubmit = false }, flowRoot.transform);
            Make<ProcSense.VR.UI.Results.PhoneView>("non-clinical phone");
            return Object.FindObjectsByType<AccessibleCanvas>(FindObjectsInactive.Include).ToList();
        }

        [Test]
        public void Clinical_panels_share_one_dilated_sdf_material_and_other_panels_keep_the_font_material()
        {
            var canvases = BuildPanels();
            foreach (var name in new[] { "Tablet", "Monitor", "EMR shell", "Game flow" })
            {
                var c = canvases.SingleOrDefault(x => x.name == name);
                Assert.IsNotNull(c, name + " canvas");
                Assert.IsTrue(c.Clinical, name + " is clinical");
                var labels = c.GetComponentsInChildren<TMP_Text>(true).Where(l => l.font != null).ToList();
                Assert.IsNotEmpty(labels, name + " labels");
                foreach (var l in labels)
                {
                    var expected = ClinicalText.SharpMaterial(l.font);
                    Assert.AreSame(expected, l.fontSharedMaterial, name + " / " + l.name + " uses the shared sharp material");
                }
                Assert.AreEqual(ClinicalText.FaceDilate, ClinicalText.SharpMaterial(labels[0].font).GetFloat("_FaceDilate"), 1e-5f, name);
            }
            var phone = canvases.Single(x => x.name == "Phone");
            Assert.IsFalse(phone.Clinical);
            foreach (var l in phone.GetComponentsInChildren<TMP_Text>(true).Where(l => l.font != null))
                Assert.AreNotSame(ClinicalText.SharpMaterial(l.font), l.fontSharedMaterial, "Phone / " + l.name + " keeps the font material");
        }

        [Test]
        public void The_game_flow_menu_is_read_from_the_distance_it_is_placed_at()
        {
            Assert.AreEqual(MenuFlowInstaller.Distance, AccessibleCanvas.Distances["Game flow"], 1e-5f);
        }

        [Test]
        public void A_clinical_panel_in_front_of_the_head_counts_as_being_read()
        {
            var monitor = BuildPanels().Single(x => x.name == "Monitor");
            foreach (var c in ClinicalText.Active.ToList()) if (c != monitor) c.gameObject.SetActive(false);
            var rect = (RectTransform)monitor.transform;
            Vector3 centre = rect.TransformPoint(rect.rect.center);
            Vector3 normal = monitor.transform.forward;
            Assert.IsTrue(ClinicalText.Reading(centre - normal * 1f, normal), "1 m in front, looking at it");
            Assert.IsFalse(ClinicalText.Reading(centre - normal * 1f, -normal), "1 m in front, looking away");
            Assert.IsFalse(ClinicalText.Reading(centre - normal * 3f, normal), "3 m away");
            monitor.gameObject.SetActive(false);
            Assert.IsFalse(ClinicalText.Reading(centre - normal * 1f, normal), "hidden panel");
        }

        [Test]
        public void Clinical_text_sizes_are_reported_in_estimated_quest2_eye_buffer_pixels()
        {
            var canvases = BuildPanels().Where(c => c.Clinical).ToList();
            Assert.IsEmpty(canvases.SelectMany(Readability.TooSmall), "the 24-headset-pixel floor still holds at the corrected distances");
            var lines = new List<string>();
            foreach (var c in canvases.OrderBy(c => c.name))
            {
                var smallest = c.GetComponentsInChildren<TMP_Text>(true).OrderBy(l => l.fontSize * Mathf.Abs(l.transform.lossyScale.y)).First();
                string at(float s) => Readability.Quest2EyeBufferPixels(smallest, c.ViewingDistance, s).ToString("0.0");
                lines.Add(c.name + " (" + c.ViewingDistance + " m) smallest '" + smallest.name + "' " + smallest.fontSize + " pt: " + at(.7f) + " / " + at(.8f) + " / " + at(.9f) + " px em");
            }
            Debug.Log("ClinicalText estimate (Quest 2, assumed " + Readability.Quest2AssumedHorizontalFov + " deg per eye; scale 0.7 / 0.8 / 0.9):\n" + string.Join("\n", lines));
            Assert.Greater(Readability.Quest2EyeBufferPixelsPerDegree(.8f), Readability.Quest2EyeBufferPixelsPerDegree(.7f));
        }
    }
}
