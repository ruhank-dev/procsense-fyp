using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The rebuilt monitor keeps its interactions: the NIBP trigger sits on the printed key, still collides and still has its interactable.</summary>
    public class MonitorInteractionTests
    {
        [UnityTest]
        public IEnumerator Nibp_trigger_sits_on_the_printed_key_and_readouts_remain()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            try
            {
                yield return SceneManager.LoadSceneAsync("PatientBay");
                for (var i = 0; i < 10; i++) yield return null;
                var button = GameObject.Find("Start BP cycle");
                Assert.IsNotNull(button);
                var monitor = button.transform.parent;
                // printed key: model x = -.0555 (viewer's right), height 1.176 m, front face z = +.0985 in the prefab's frame
                var key = monitor.TransformPoint(new Vector3(-.0555f, 1.176f, .0985f));
                Assert.That(Vector3.Distance(button.transform.position, key), Is.LessThan(.01f), "trigger must sit on the printed NIBP key");
                Assert.IsTrue(button.GetComponent<Collider>().enabled);
                Assert.IsNotNull(button.GetComponent<XRSimpleInteractable>());
                Assert.IsFalse(button.GetComponent<MeshRenderer>().enabled, "the placeholder cube is invisible; the real key is the model's");
                var surface = monitor.GetComponentsInChildren<EquipmentSurfaceView>(true).Single();
                Assert.That(surface.DisplayText, Does.Contain("SpO2"), "the permitted-state readout is still drawn on the screen");
            }
            finally { ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null; }
        }
    }
}
