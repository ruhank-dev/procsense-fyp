using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    public sealed class ModeledDoorTests
    {
        [SetUp] public void Setup() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            yield return EditorXRTestEnvironment.Cleanup();
        }
        [UnityTest] public IEnumerator One_visible_leaf_per_connection_matches_and_moves_with_the_solid_hinge()
        {
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            var host = SimulationHost.Instance; host.AutoTick = false; host.SaveLogOnDestroy = false;
            var hash = host.Session.Log.Hash(); var state = host.Observable.Hash();
            var models = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Door leaf (modelled)").ToArray();
            foreach (var hinge in Object.FindObjectsByType<SwingDoor>(FindObjectsSortMode.None))
            {
                var routes = Object.FindObjectsByType<RoomDoor>(FindObjectsSortMode.None).Where(d => d.Connection == hinge.Connection).ToArray();
                Assert.AreEqual(2, routes.Length);
                var leaves = models.Where(m => routes.Any(d => m.IsChildOf(d.transform.parent)) && m.gameObject.activeInHierarchy).ToArray();
                Assert.AreEqual(1, leaves.Length, "No stationary duplicate in the adjoining room: " + hinge.Connection);
                Assert.IsTrue(leaves[0].IsChildOf(hinge.transform));
                var slab = leaves[0].GetComponentsInChildren<MeshRenderer>().OrderByDescending(r => r.bounds.size.y).First();
                Assert.Less(Vector3.Distance(slab.bounds.center, hinge.Leaf.bounds.center), .04f, "Art must coincide with its solid physics leaf");
                Assert.IsFalse(hinge.Leaf.GetComponent<MeshRenderer>().enabled);
                var grab = hinge.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                Assert.AreEqual(2, grab.colliders.Count);
                Assert.IsTrue(hinge.Toggle());
                yield return new WaitForSeconds(1);
                Assert.Greater(Mathf.Abs(hinge.Angle), 90);
                Assert.Less(Vector3.Distance(slab.bounds.center, hinge.Leaf.bounds.center), .04f, "The visible leaf must rotate with the collider");
                Assert.IsTrue(hinge.Toggle()); yield return new WaitForSeconds(1);
                Assert.Less(Mathf.Abs(hinge.Angle), 1);
            }
            Assert.AreEqual(hash, host.Session.Log.Hash()); Assert.AreEqual(state, host.Observable.Hash());
        }
    }
}
