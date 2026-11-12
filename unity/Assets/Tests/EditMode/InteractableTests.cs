using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Interaction;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The Interactable component (P3-CTX-02): lookup by id and type, the panel anchor, the highlight switch, and the problems report.</summary>
    public class InteractableTests
    {
        private GameObject _root;

        [SetUp] public void Up() => _root = new GameObject("interactables under test");
        [TearDown] public void Down() => Object.DestroyImmediate(_root);

        private Interactable Make(string name, string id, EntityType type, string voice = "")
        {
            var go = new GameObject(name); go.transform.SetParent(_root.transform);
            go.AddComponent<MeshRenderer>();
            var i = go.AddComponent<Interactable>(); i.Configure(id, type, voice);
            return i;
        }

        [Test]
        public void An_enabled_interactable_is_found_by_id_and_by_type_and_a_disabled_one_is_not()
        {
            var nurse = Make("Nurse", "nurse_ward", EntityType.Nurse, "nurse");
            var monitor = Make("Monitor", "monitor_bay", EntityType.Equipment);
            Assert.AreSame(nurse, Interactable.Find("nurse_ward"));
            Assert.IsTrue(Interactable.OfType(EntityType.Equipment).Contains(monitor));
            Assert.IsFalse(Interactable.OfType(EntityType.Equipment).Contains(nurse));
            nurse.gameObject.SetActive(false);
            Assert.IsNull(Interactable.Find("nurse_ward"));
        }

        [Test]
        public void The_panel_anchor_is_the_object_itself_unless_one_is_given()
        {
            var i = Make("Patient", "patient", EntityType.Patient, "patient");
            Assert.AreSame(i.transform, i.PanelAnchor);
            var anchor = new GameObject("anchor").transform; anchor.SetParent(i.transform);
            i.Configure("patient", EntityType.Patient, "patient", anchor);
            Assert.AreSame(anchor, i.PanelAnchor);
        }

        [Test]
        public void The_highlight_switches_once_per_change_and_clears_when_the_object_is_disabled()
        {
            var i = Make("Doctor", "doctor", EntityType.Doctor, "doctor");
            int events = 0; i.HighlightChanged += (_, __) => events++;
            i.SetHighlighted(true); i.SetHighlighted(true);
            Assert.IsTrue(i.Highlighted);
            Assert.AreEqual(1, events, "the same state twice is not a change");
            i.gameObject.SetActive(false);
            Assert.IsFalse(i.Highlighted);
            Assert.AreEqual(2, events, "a hidden object must not stay lit");
        }

        [Test]
        public void Highlight_renderers_default_to_everything_under_the_object()
        {
            var i = Make("Monitor", "monitor_bay", EntityType.Equipment);
            Assert.AreEqual(1, i.HighlightRenderers.Count);
        }

        [Test]
        public void Problems_name_a_missing_id_a_missing_voice_name_and_nothing_to_light()
        {
            Assert.IsEmpty(Make("Good nurse", "nurse", EntityType.Nurse, "nurse").Problems());
            Assert.IsEmpty(Make("Good monitor", "monitor", EntityType.Equipment).Problems(), "equipment needs no voice name");
            StringAssert.Contains("no entity id", string.Join(";", Make("Nameless", "", EntityType.Equipment).Problems()));
            StringAssert.Contains("voice name", string.Join(";", Make("Mute nurse", "n2", EntityType.Nurse).Problems()));
            var bare = new GameObject("Bare"); bare.transform.SetParent(_root.transform);
            var i = bare.AddComponent<Interactable>(); i.Configure("bare", EntityType.Equipment);
            StringAssert.Contains("nothing to highlight", string.Join(";", i.Problems()));
        }
    }
}
