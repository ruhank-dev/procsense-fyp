using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The patient on the bed fits the case: the profile comes from sex and age, one body is shown, and everything mounted on the body moves to the same bone of the chosen one.</summary>
    public class PatientProfilesTests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        [TearDown] public void Teardown() { foreach (var g in made) if (g != null) Object.DestroyImmediate(g); made.Clear(); }

        private Transform Body(string name) { var root = new GameObject(name); made.Add(root); var bone = new GameObject("Bip01 L Hand"); bone.transform.SetParent(root.transform, false); return root.transform; }

        [TestCase("F", 25, "female_young")] [TestCase("F", 39, "female_young")] [TestCase("F", 40, "female_adult")] [TestCase("F", 64, "female_adult")] [TestCase("F", 65, "female_older")]
        [TestCase("M", 16, "male_adult")] [TestCase("M", 42, "male_adult")] [TestCase("M", 64, "male_adult")] [TestCase("M", 65, "male_older")] [TestCase("M", 74, "male_older")]
        public void The_profile_follows_sex_and_age(string sex, int age, string expected) => Assert.AreEqual(expected, PatientProfiles.KeyFor(sex, age));

        [Test]
        public void One_body_is_shown_and_the_mounted_things_move_to_the_same_bone_of_it()
        {
            var male = Body("male"); var older = Body("older female");
            var wristband = new GameObject("Patient wristband").transform; made.Add(wristband.gameObject);
            var hand = male.Find("Bip01 L Hand"); wristband.SetParent(hand, false); wristband.localPosition = new Vector3(.03f, 0, 0);
            var host = new GameObject("profiles"); made.Add(host); var p = host.AddComponent<PatientProfiles>();
            p.profiles = new List<PatientProfiles.Profile> { new PatientProfiles.Profile { key = "male_adult", root = male }, new PatientProfiles.Profile { key = "female_older", root = older } };
            p.mounts = new List<PatientProfiles.Mount> { new PatientProfiles.Mount { item = wristband, bone = "Bip01 L Hand" } };

            Assert.AreEqual("female_older", p.Select("female_older"));
            Assert.IsTrue(older.gameObject.activeSelf); Assert.IsFalse(male.gameObject.activeSelf, "only one body is shown");
            Assert.AreSame(older.Find("Bip01 L Hand"), wristband.parent, "the wristband is on the new body's hand");
            Assert.AreEqual(.03f, wristband.localPosition.x, 1e-4f, "its place on the hand is kept");
        }

        [Test]
        public void A_profile_that_is_not_in_the_scene_falls_back_to_the_male_adult()
        {
            var male = Body("male"); var host = new GameObject("profiles"); made.Add(host); var p = host.AddComponent<PatientProfiles>();
            p.profiles = new List<PatientProfiles.Profile> { new PatientProfiles.Profile { key = "male_adult", root = male } };
            Assert.AreEqual("male_adult", p.Select("female_young"));
            Assert.IsTrue(male.gameObject.activeSelf);
        }
    }
}
