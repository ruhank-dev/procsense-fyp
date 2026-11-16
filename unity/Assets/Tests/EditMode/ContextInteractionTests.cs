using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Interaction;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-CTX-03/05/08, P3-VOX-04/05: the catalogue as Unity reads it, the placement sweep (height, distance, walls, beside the entity, facing the player), picking by ray and by poke, and the
    /// addressee ("Nurse, …" while looking at the patient goes to the nurse).
    /// </summary>
    public class ContextInteractionTests
    {
        private static InteractionCatalogue Catalogue => InteractionCatalogue.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "..", "..", "content", "ui", "interactions.json")))
            .WithExamples(File.ReadAllText(Path.Combine(Application.dataPath, "..", "..", "content", "ui", "voice_examples.json")));

        [Test]
        public void The_catalogue_reads_every_entity_and_every_group_entry_exists()
        {
            var c = Catalogue;
            CollectionAssert.AreEquivalent(Interactable.Types, c.Entities.Keys);
            foreach (var e in c.Entities.Values)
                foreach (var g in e.Groups)
                    foreach (var id in g.Entries) Assert.IsTrue(c.Entries.ContainsKey(id), e.Id + "/" + g.Id + ": " + id);
            Assert.IsTrue(c.Entries.Values.Any(x => x.HasForm), "forms are flagged");
            CollectionAssert.IsNotEmpty(c.Global, "the wrist menu's own entries");
        }

        [Test]
        public void Every_entity_type_shows_at_least_three_things_to_say()
        {
            var c = Catalogue;
            foreach (var type in Interactable.Types)
            {
                var examples = c.EntriesOf(type).Where(x => x.Spoken).SelectMany(x => x.VoiceExamples.Take(1))
                                .Concat(c.VoiceExamples.TryGetValue(type, out var more) ? more : new List<string>()).Distinct().ToList();
                Assert.GreaterOrEqual(examples.Count, 3, type + " reference card (P3-VOX-04)");
                foreach (var x in examples) Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(x, @"\d"), type + ": no numbers or doses in an example: " + x);
            }
            foreach (var x in c.VoiceExamples["nurse"]) Assert.AreEqual("nurse", AddresseeTarget.Named(x), "a nurse example is addressed to the nurse: " + x);
            foreach (var x in c.VoiceExamples["anaesthetist"]) Assert.AreEqual("anaesthesia", AddresseeTarget.Named(x), x);
        }

        [Test]
        public void Placement_sweep_keeps_the_panel_in_reach_beside_the_entity_and_in_front_of_walls()
        {
            int n = 0;
            for (float ax = -3; ax <= 3; ax += .75f)
                for (float az = -3; az <= 3; az += .75f)
                    for (float ay = .2f; ay <= 2.2f; ay += .5f)
                    {
                        var head = new Vector3(0, 1.6f, 0);
                        var anchor = new Vector3(ax, ay, az);
                        var (p, r) = PanelPlacement.Place(anchor, head);
                        Assert.That(p.y, Is.InRange(PanelPlacement.MinHeight - 1e-4f, PanelPlacement.MaxHeight + 1e-4f), "height");
                        float d = Vector3.ProjectOnPlane(p - head, Vector3.up).magnitude;
                        Assert.That(d, Is.InRange(PanelPlacement.MinDistance - 1e-3f, PanelPlacement.MaxDistance + 1e-3f), "distance from the eyes");
                        Assert.Greater(Vector3.Dot(r * Vector3.forward, Vector3.ProjectOnPlane(p - head, Vector3.up).normalized), .99f, "turned to the player");
                        n++;
                    }
            Assert.Greater(n, 300);
            // a wall 0.7 m in front: the panel stays in front of it
            var (q, _) = PanelPlacement.Place(new Vector3(0, 1.4f, 2), new Vector3(0, 1.6f, 0), (from, dir, max) => .7f);
            Assert.Less(Vector3.Distance(q, new Vector3(0, 1.6f, 0)), .7f, "pulled in front of the wall");
        }

        [Test]
        public void A_ray_picks_the_nearest_entity_and_a_hand_pokes_one_in_reach()
        {
            var near = GameObject.CreatePrimitive(PrimitiveType.Cube); near.transform.position = new Vector3(0, 1, 2);
            var far = GameObject.CreatePrimitive(PrimitiveType.Cube); far.transform.position = new Vector3(0, 1, 4);
            try
            {
                var a = Interactable.Mark(near, "nurse"); var b = Interactable.Mark(far, "monitor");
                var all = new[] { a, b };
                Assert.AreSame(a, InteractableSelector.Pick(new Ray(new Vector3(0, 1, 0), Vector3.forward), all, out _));
                Assert.IsNull(InteractableSelector.Pick(new Ray(new Vector3(0, 1, 0), Vector3.back), all, out _));
                Assert.AreSame(b, InteractableSelector.Poke(new Vector3(0, 1, 3.45f), all), "a hand within reach");
                Assert.IsNull(InteractableSelector.Poke(new Vector3(0, 1, 3f), all));
            }
            finally { Object.DestroyImmediate(near); Object.DestroyImmediate(far); }
        }

        [Test]
        public void A_name_wins_over_the_gaze_and_the_gaze_over_nothing()
        {
            var head = new GameObject("head").transform;
            var patient = new GameObject("patient"); patient.transform.position = new Vector3(0, 1.5f, 1.5f);
            var nurse = new GameObject("nurse"); nurse.transform.position = new Vector3(2, 1.5f, 0);
            try
            {
                head.position = new Vector3(0, 1.6f, 0); head.rotation = Quaternion.identity;   // looking at the patient
                var p = Interactable.Mark(patient, "patient"); var n = Interactable.Mark(nurse, "nurse");
                var all = new[] { p, n };
                Assert.AreEqual("nurse", AddresseeTarget.Choose("Nurse, can you take a blood sample?", null, head, all), "the test of P3-VOX-05");
                Assert.AreEqual("patient", AddresseeTarget.Choose("What is your name?", null, head, all), "looking at the patient");
                Assert.AreEqual("nurse", AddresseeTarget.Choose("Please check the drip", n, head, all), "the open panel's person");
                Assert.AreEqual("anaesthesia", AddresseeTarget.Named("Anaesthetist, is the patient fit?"));
                Assert.IsNull(AddresseeTarget.Named("Nursery rhymes"), "a whole word only");
                head.rotation = Quaternion.LookRotation(Vector3.back);
                Assert.IsNull(AddresseeTarget.Choose("Hello", null, head, all), "nobody in view: the service decides");
                StringAssert.Contains("\"addressee\":\"nurse\"", SpokenActionClient.RequestBody("Nurse, hi", new[] { "a" }, null, "nurse"));
                StringAssert.DoesNotContain("addressee", SpokenActionClient.RequestBody("hi", new[] { "a" }, null));
            }
            finally { Object.DestroyImmediate(head.gameObject); Object.DestroyImmediate(patient); Object.DestroyImmediate(nurse); }
        }
    }
}
