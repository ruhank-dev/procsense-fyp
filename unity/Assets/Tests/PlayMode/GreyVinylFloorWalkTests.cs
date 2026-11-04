using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The new floor is still solid ground: ray-casts from above hit it at y = 0 across the room, it renders with the grey vinyl material, and it carries the room's baked lightmap.</summary>
    public class GreyVinylFloorWalkTests
    {
        [UnityTest]
        public IEnumerator Floor_is_solid_grey_vinyl_at_height_zero_with_baked_light()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            try
            {
                yield return SceneManager.LoadSceneAsync("PatientBay");
                yield return TestHostHygiene.WaitForHost(50);
                var floor = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Single(r => r.name == "Floor" && r.gameObject.scene.name == "PatientBay");
                Assert.That(floor.sharedMaterial.name, Is.EqualTo("PatientBay grey vinyl"));
                Assert.That(floor.lightmapIndex, Is.GreaterThanOrEqualTo(0), "the floor receives the baked lighting");
                foreach (var p in new[] { new Vector2(-2.2f, -1.7f), new Vector2(0, 0), new Vector2(2.2f, 1.7f), new Vector2(1.1f, 1.6f) })
                {
                    Assert.IsTrue(Physics.Raycast(new Vector3(p.x, 2f, p.y), Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore));
                    if (hit.collider.GetComponent<MeshRenderer>() == floor) Assert.That(hit.point.y, Is.EqualTo(0f).Within(.002f));
                }
                Assert.IsTrue(Physics.Raycast(new Vector3(-2.2f, 2f, -1.7f), Vector3.down, out var corner, 4f, ~0, QueryTriggerInteraction.Ignore));
                Assert.That(corner.point.y, Is.EqualTo(0f).Within(.002f), "open floor surface at the room corner");
            }
            finally { ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null; }
        }
    }
}
