using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Interaction;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-CTX-02: the patient, staff, equipment and records of the patient bay, corridor and theatre carry an Interactable of a catalogue entity type, with renderers to highlight and an anchor;
    /// every entity type of content/ui/interactions.json is either in a room or brought by a session (the transport doctor), and the binder's types are exactly the catalogue's.
    /// </summary>
    public class InteractableSceneTests
    {
        private static readonly string[] Rooms = { "Assets/Scenes/PatientBay.unity", "Assets/Scenes/Corridor.unity", "Assets/Scenes/OT.unity" };
        // doctor: only the transport escort (TransportActor.Doctor) during a session; marked by BindRuntime, covered by the play test of the binder
        private static readonly string[] SessionOnly = { "doctor", "nurse" };   // nurse: also the visiting wristband nurse and the transport escort, both session actors

        [Test]
        public void The_binder_types_are_the_catalogue_entity_types()
        {
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "..", "..", "content", "ui", "interactions.json"));
            foreach (var type in Interactable.Types) StringAssert.Contains("\"id\": \"" + type + "\"", json.Replace("\"id\":\"", "\"id\": \""), type);
            foreach (var (_, type) in InteractableBinder.Rules) CollectionAssert.Contains(Interactable.Types, type);
        }

        [Test]
        public void Every_entity_type_is_in_a_room_with_something_to_highlight()
        {
            var types = new HashSet<string>();
            var report = new List<string>();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var path in Rooms)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var found = InteractableBinder.BindScene(scene);
                    foreach (var i in found) Assert.IsNull(i.transform.parent != null ? i.transform.parent.GetComponentInParent<Interactable>() is Interactable p && p.EntityType == i.EntityType ? p : null : null, i.name + " is marked twice");
                    foreach (var i in found)
                    {
                        Assert.IsNotEmpty(i.Highlight, i.name + " has renderers to highlight");
                        Assert.IsNotNull(i.PanelAnchor);
                        Assert.IsFalse(string.IsNullOrEmpty(i.VoiceName));
                        types.Add(i.EntityType);
                    }
                    report.Add(Path.GetFileNameWithoutExtension(path) + ": " + string.Join(", ", found.Select(f => f.EntityType + " (" + f.name + ")")));
                }
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
            TestContext.WriteLine(string.Join("\n", report));
            foreach (var type in Interactable.Types.Except(SessionOnly)) Assert.IsTrue(types.Contains(type), type + " is in no room:\n" + string.Join("\n", report));
        }
    }
}
