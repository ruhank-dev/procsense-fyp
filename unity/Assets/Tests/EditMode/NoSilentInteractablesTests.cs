using System.Linq;
using NUnit.Framework;
using ProcSense.VR.ActionEmitters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-INT-09: nothing in the three rooms highlights and then does nothing. Every XR interactable is a teleport target, the hinged door, or sends an action through an emitter.</summary>
    public class NoSilentInteractablesTests
    {
        [TestCase("Assets/Scenes/PatientBay.unity")] [TestCase("Assets/Scenes/Corridor.unity")] [TestCase("Assets/Scenes/OT.unity")]
        public void Every_interactable_in_the_room_does_something(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var silent = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<XRBaseInteractable>(true))
                    .Where(i => !i.GetType().Name.StartsWith("SafeTeleport") && i.name != "Grabbable hinged door" && i.GetComponent<ProcSense.VR.Interaction.TheatreHandoverPoint>() == null && i.GetComponent<ProcSense.VR.Interaction.BedControl>() == null
                                && i.GetComponentsInParent<ActionEmitter>(true).Length + i.GetComponentsInChildren<ActionEmitter>(true).Length == 0)
                    .Select(i => i.name).ToList();
                Assert.IsEmpty(silent, "highlighted but silent in " + path + ": " + string.Join(", ", silent));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
