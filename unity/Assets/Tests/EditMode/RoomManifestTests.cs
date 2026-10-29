using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Editor.Rooms;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Room manifests (p0-environment-rooms-s04): every manifest is valid, placing twice gives the same scene, an asset swaps by its id, and nothing outside the owned root is touched.</summary>
    public class RoomManifestTests
    {
        private static RoomManifests.Manifest Load(string room) => RoomManifests.Parse(File.ReadAllText(RoomManifests.FileOf(room)));

        [Test]
        public void Every_room_has_a_valid_manifest_for_its_scene()
        {
            foreach (var (room, scene) in RoomManifests.Rooms)
            {
                Assert.IsTrue(File.Exists(RoomManifests.FileOf(room)), room + ": no manifest (ProcSense > Rooms > Export room manifests from the scenes)");
                var m = Load(room);
                Assert.AreEqual(scene, m.Scene);
                CollectionAssert.IsEmpty(RoomManifests.Validate(m), room);
            }
            Assert.Greater(Load("bay").Placements.Count, 10, "the bay's equipment and dressing are in its manifest");
        }

        [Test]
        public void A_bad_name_a_missing_slot_and_a_shared_slot_are_caught()
        {
            var m = Load("bay");
            m.Anchors.Add(new RoomManifests.Anchor { Name = "teleport bedside", Kind = "TELEPORT" });
            m.Placements.Add(new RoomManifests.Placement { Asset = "bp_cuff", Slot = "SLOT_bp_cuff_9" });
            m.Placements.Add(new RoomManifests.Placement { Asset = "spo2_probe", Slot = m.Placements[0].Slot });
            m.Placements.Add(new RoomManifests.Placement { Asset = "no_such_asset", Slot = m.Placements[1].Slot });
            var problems = RoomManifests.Validate(m);
            Assert.IsTrue(problems.Any(p => p.Contains("naming rule")));
            Assert.IsTrue(problems.Any(p => p.Contains("does not have")));
            Assert.IsTrue(problems.Any(p => p.Contains(" holds ")));
            Assert.IsTrue(problems.Any(p => p.Contains("no prefab")));
        }

        [Test]
        public void Placing_twice_gives_the_same_scene_and_an_asset_swaps_by_its_id()
        {
            var scene = SceneManager.GetActiveScene();   // the test runner's own scene
            var sentinel = new GameObject("Sannan's object");
            try
            {
                sentinel.transform.position = new Vector3(1, 2, 3);
                var m = Load("bay");
                string first = RoomManifests.Dump(RoomManifests.Place(m, scene));
                string second = RoomManifests.Dump(RoomManifests.Place(m, scene));
                Assert.AreEqual(first, second, "placing is idempotent");
                Assert.AreEqual(1, scene.GetRootGameObjects().Count(g => g.name == RoomManifests.OwnedRoot));
                Assert.AreEqual(new Vector3(1, 2, 3), sentinel.transform.position, "nothing outside the owned root moves");

                var cuff = m.Placements.First(p => p.Asset == "bp_cuff");
                cuff.Asset = "spo2_probe";   // the swap: change the id, place again
                var root = RoomManifests.Place(m, scene);
                var placed = root.transform.Find(cuff.Slot).GetChild(0).gameObject;
                StringAssert.EndsWith("spo2_probe.prefab", UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(placed));

                // what Place built exports back to the same manifest
                var again = RoomManifests.Export(scene, "bay");
                CollectionAssert.AreEquivalent(m.Placements.Select(p => p.Slot + "=" + p.Asset), again.Placements.Select(p => p.Slot + "=" + p.Asset));
            }
            finally
            {
                Object.DestroyImmediate(sentinel);
                foreach (var g in scene.GetRootGameObjects()) if (g.name == RoomManifests.OwnedRoot) Object.DestroyImmediate(g);
            }
        }

        [Test]
        public void The_manifests_still_describe_the_rooms()
        {
            // informative, not a failure: the rooms are Sannan's and change in the editor; this lists what a fresh export would change
            foreach (var (room, path) in RoomManifests.Rooms)
            {
                var open = SceneManager.GetSceneByPath(path);
                bool wasOpen = open.isLoaded;   // never close a room the user has open
                var scene = wasOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    string now = RoomManifests.ToJson(RoomManifests.Export(scene, room));
                    if (now != File.ReadAllText(RoomManifests.FileOf(room)).Replace("\r\n", "\n"))
                        Debug.LogWarning("[ProcSense] " + room + ": the room has changed since its manifest was exported (ProcSense > Rooms > Export room manifests from the scenes)");
                }
                finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
