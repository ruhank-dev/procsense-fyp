using System.Linq;
using NUnit.Framework;
using ProcSense.EditorTools;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.EditMode
{
    public class EquipmentImportTests
    {
        [TestCase("vital_signs_monitor", .327f, .288f, .182f, 0)]
        [TestCase("iv_pole_pump", .214f, .068f, .124f, 4)]
        [TestCase("wristband", .279f, .001f, .025f, 0)]
        [TestCase("spo2_probe", .033f, .008f, .056f, 0)]
        [TestCase("bp_cuff", .300f, .003f, .160f, 0)]
        public void PilotsHaveMetreScaleMaterialsAndIsolatedSockets(string id, float x, float y, float z, int sockets)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/" + id + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            var body = renderers.Single(r => r.name == "EQ_" + id + "_Body");
            Assert.That(Vector3.Distance(body.bounds.size, new Vector3(x, y, z)), Is.LessThan(.00001));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(prefab.GetComponent<XRGrabInteractable>().attachTransform, Is.Not.Null);
            Assert.That(prefab.GetComponents<BoxCollider>().All(c => c.size.x > 0 && c.size.y > 0 && c.size.z > 0), Is.True);
            Assert.That(prefab.GetComponentsInChildren<EquipmentSurfaceView>().Length, Is.EqualTo(EquipmentPrefabBuilder.Sensors.Contains(id) ? 0 : 1));
            Assert.That(renderers.SelectMany(r => r.sharedMaterials).All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit"), Is.True);
            var hooks = prefab.GetComponentsInChildren<XRSocketInteractor>();
            Assert.That(hooks.Length, Is.EqualTo(sockets));
            Assert.That(hooks.All(h => h.interactionLayers.value == 1 << 8), Is.True);
        }

        [Test]
        public void ReimportPreservesPrefabIdentityAndAuthoredSettings()
        {
            const string path = "Assets/Prefabs/Equipment/vital_signs_monitor.prefab";
            string guid = AssetDatabase.AssetPathToGUID(path);
            float originalMass = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Rigidbody>().mass;
            try
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.GetComponent<Rigidbody>().mass = 9.25f;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.ImportAsset("Assets/Models/Equipment/vital_signs_monitor.fbx", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                EquipmentPrefabBuilder.CreateIfMissing("Assets/Models/Equipment/vital_signs_monitor.fbx");
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Rigidbody>().mass, Is.EqualTo(9.25f));
            }
            finally
            {
                // Restore the tested setting through PrefabUtility, preserving Editor ownership.
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.GetComponent<Rigidbody>().mass = originalMass;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
