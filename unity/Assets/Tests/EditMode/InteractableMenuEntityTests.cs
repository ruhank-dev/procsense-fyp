using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Interaction;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Every kind of Interactable maps to an entity the interaction catalogue (content/ui/interactions.json) really has, so the contextual panel never opens for an entity with no actions.</summary>
    public class InteractableMenuEntityTests
    {
        [Serializable] private class Entity { public string id = ""; }
        [Serializable] private class Catalogue { public Entity[] entities = Array.Empty<Entity>(); }

        [Test]
        public void Every_entity_type_maps_to_an_entity_in_the_interaction_catalogue()
        {
            var path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..")), "content", "ui", "interactions.json");
            var ids = JsonUtility.FromJson<Catalogue>(File.ReadAllText(path)).entities.Select(e => e.id).ToList();
            CollectionAssert.IsNotEmpty(ids);
            foreach (EntityType type in Enum.GetValues(typeof(EntityType)))
                CollectionAssert.Contains(ids, Interactable.MenuEntityOf(type), type + " maps to an entity the catalogue does not have");
        }

        [Test]
        public void Several_objects_can_share_one_catalogue_entity()
        {
            Assert.AreEqual("porter", Interactable.MenuEntityOf(EntityType.Receptionist));
            Assert.AreEqual("porter", Interactable.MenuEntityOf(EntityType.Porter));
            Assert.AreEqual("records", Interactable.MenuEntityOf(EntityType.Records));
        }
    }
}
