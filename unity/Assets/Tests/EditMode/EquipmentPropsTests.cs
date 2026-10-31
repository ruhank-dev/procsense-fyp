using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core.Content;
using ProcSense.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The identity documents, bedside monitoring and sampling props (p0-blender-equipment-s06 to s08) import as working prefabs: metre scale (the body matches the
    /// sourced size in tools/blender/requirements.json), every named point the recipe promises, a collider, a grab point and shared URP materials.
    /// </summary>
    public class EquipmentPropsTests
    {
        private static readonly string Requirements = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "blender", "requirements.json"));

        private static Dictionary<string, object> Spec(string id) =>
            (Dictionary<string, object>)((Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Requirements)))[id];

        private static IEnumerable<string> Props => EquipmentPrefabBuilder.Props;

        [TestCaseSource(nameof(Props))]
        public void A_prop_imports_at_its_sourced_size_with_its_named_points(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/" + id + ".prefab");
            Assert.That(prefab, Is.Not.Null, id + ": no prefab (ProcSense > Equipment > Import Documents, Monitoring and Sampling Props)");
            var spec = Spec(id);
            var body = prefab.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "EQ_" + id + "_Body");
            var want = ((List<object>)spec["body"]).Select(x => System.Convert.ToSingle(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var size = body.bounds.size;   // Blender Z up becomes Unity Y up: x, z, y
            Assert.That(new[] { size.x, size.z, size.y }, Is.EqualTo(want).Within(.0006f), id + ": body size");
            var names = new HashSet<string>(prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name));
            foreach (string point in ((List<object>)spec["points"]).Cast<string>()) Assert.IsTrue(names.Contains(point), id + ": no " + point);
            Assert.That(prefab.GetComponents<BoxCollider>(), Is.Not.Empty);
            Assert.That(prefab.GetComponent<XRGrabInteractable>().attachTransform, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).All(m => m != null && m.name.StartsWith("M_") && m.shader.name == "Universal Render Pipeline/Lit"), Is.True, id + ": materials");
        }

        [Test]
        public void The_wall_station_is_fixed_and_a_hidden_applied_tourniquet_waits()
        {
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/tube_station.prefab");
            Assert.IsTrue(station.GetComponent<Rigidbody>().isKinematic);
            Assert.IsFalse(station.GetComponent<XRGrabInteractable>().enabled);
            var set = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/blood_collection_set.prefab");
            Assert.IsFalse(set.GetComponentsInChildren<Transform>(true).Single(t => t.name == "EQ_blood_collection_set_Wrapped").gameObject.activeSelf);
        }

        [Test]
        public void See_through_parts_use_a_transparent_material()
        {
            foreach (var name in new[] { "M_PlasticClear", "M_BrothAmber" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Equipment/" + name + ".mat");
                Assert.That(m, Is.Not.Null, name);
                Assert.AreEqual(1f, m.GetFloat("_Surface"), name);
            }
        }
    }
}
