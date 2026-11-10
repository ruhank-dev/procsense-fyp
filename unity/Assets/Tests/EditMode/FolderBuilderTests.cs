using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.EditorTools;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Records.Folder;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.Tests.EditMode
{
    /// <summary>DT-263: run the production builder from the real exported FBX and compare its wiring to the authored folder.</summary>
    public sealed class FolderBuilderTests
    {
        private const string Model = "Assets/Models/Equipment/patient_folder.fbx";
        private const string Authored = "Assets/Prefabs/Equipment/patient_folder.prefab";
        private const string Generated = "Assets/Tests/EditMode/__FolderBuilderVerification.prefab";
        private string _ownedGuid;

        [SetUp] public void GuardExistingFiles()
        {
            Assert.IsFalse(File.Exists(Generated) || File.Exists(Generated + ".meta"), "Refuse to replace any pre-existing verification asset");
        }
        [TearDown] public void RemoveOnlyOwnedVerificationAsset()
        {
            if (string.IsNullOrEmpty(_ownedGuid)) return;
            Assert.AreEqual(_ownedGuid, AssetDatabase.AssetPathToGUID(Generated), "Do not delete an asset whose identity changed");
            Assert.IsTrue(AssetDatabase.DeleteAsset(Generated));
            _ownedGuid = null;
        }
        private GameObject Build()
        {
            EquipmentPrefabBuilder.CreateIfMissing(Model, Generated);
            _ownedGuid = AssetDatabase.AssetPathToGUID(Generated);
            Assert.IsNotEmpty(_ownedGuid);
            return AssetDatabase.LoadAssetAtPath<GameObject>(Generated);
        }
        private static string PathOf(Transform root, Transform t) => AnimationUtility.CalculateTransformPath(t, root);
        private static string[] Structure(GameObject root) => root.GetComponentsInChildren<Transform>(true)
            .Select(t => PathOf(root.transform, t) + ":" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().FullName).OrderBy(n => n)))
            .OrderBy(s => s).ToArray();

        [Test] public void Reproduced_folder_matches_authored_hierarchy_and_bindings()
        {
            var bytes = File.ReadAllBytes(Authored); var meta = File.ReadAllBytes(Authored + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(Authored);
            // Reproduce the Editor state after Play; authoring must still produce the same prefab.
            typeof(UiKit).Assembly.GetType("ProcSense.VR.XR.WorldUiRaycaster").GetMethod("Hook", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            var generated = Build();
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(Authored);
            Assert.IsNotNull(generated.GetComponent<FolderController>(), "The builder must reproduce Moiz's added controller");
            CollectionAssert.AreEqual(Structure(authored), Structure(generated), "The same named model, UI and components");
            foreach (var t in generated.GetComponentsInChildren<Transform>(true))
            {
                string path = PathOf(generated.transform, t);
                var expected = path.Length == 0 ? authored.transform : authored.transform.Find(path);
                Assert.Less(Vector3.Distance(expected.localPosition, t.localPosition), .00001f, path + " position");
                Assert.Less(Quaternion.Angle(expected.localRotation, t.localRotation), .001f, path + " rotation");
                Assert.Less(Vector3.Distance(expected.localScale, t.localScale), .00001f, path + " scale");
            }
            var a = generated.GetComponent<NotesFolderInteraction>();
            var data = new SerializedObject(a);
            Assert.AreEqual("EQ_patient_folder_Cover", data.FindProperty("cover").objectReferenceValue.name);
            Assert.AreEqual("Notes page", a.Page.name);
            var page = new SerializedObject(a.Page.GetComponent<NotesPanelView>());
            Assert.AreEqual("Identity", page.FindProperty("_body").objectReferenceValue.name);
            var grab = generated.GetComponent<XRGrabInteractable>();
            Assert.AreEqual("GRAB_Folder", grab.attachTransform.name);
            Assert.AreEqual(1, grab.colliders.Count);
            Assert.IsFalse(grab.throwOnDetach);
            Assert.IsTrue(generated.GetComponent<Rigidbody>().useGravity);
            Assert.AreEqual(.4f, generated.GetComponent<Rigidbody>().mass);
            var box = generated.GetComponent<BoxCollider>(); var originalBox = authored.GetComponent<BoxCollider>();
            Assert.AreEqual(originalBox.center, box.center); Assert.AreEqual(originalBox.size, box.size);
            Assert.IsTrue(generated.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit"));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Authored));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Authored));
            CollectionAssert.AreEqual(meta, File.ReadAllBytes(Authored + ".meta"));
        }

        [Test] public void Repeated_build_preserves_existing_prefab_identity_and_authored_mass()
        {
            Build();
            var root = PrefabUtility.LoadPrefabContents(Generated);
            try { root.GetComponent<Rigidbody>().mass = .725f; PrefabUtility.SaveAsPrefabAsset(root, Generated); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            byte[] before = File.ReadAllBytes(Generated);
            EquipmentPrefabBuilder.CreateIfMissing(Model, Generated);
            Assert.AreEqual(_ownedGuid, AssetDatabase.AssetPathToGUID(Generated));
            Assert.AreEqual(.725f, AssetDatabase.LoadAssetAtPath<GameObject>(Generated).GetComponent<Rigidbody>().mass);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Generated));
        }

        [Test] public void Invalid_destination_is_rejected_before_writing_any_asset()
        {
            Assert.Throws<ArgumentException>(() => EquipmentPrefabBuilder.CreateIfMissing(Model, "../outside.prefab"));
            Assert.Throws<ArgumentException>(() => EquipmentPrefabBuilder.CreateIfMissing(Model, Generated + ".txt"));
            Assert.IsFalse(File.Exists(Generated));
        }
    }
}
