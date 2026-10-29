using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;
using ProcSense.VR.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.Editor.Rooms
{
    /// <summary>
    /// Room manifests (p0-environment-rooms-s04): each room's anchors (Anchor_, SLOT_, NPC_, TELEPORT_, DOOR_) and which asset goes in which slot, as data in
    /// unity/Assets/Data/Rooms/&lt;room&gt;.json, so a room can be reviewed in a diff and a greybox prop swapped for a Blender one by changing an asset id.
    /// Export reads a scene and writes its manifest without changing the scene. Place builds the manifest's slots and assets under one ManifestOwned root,
    /// replacing what it built before, so running it twice gives the same scene. The rooms are Sannan's: Place runs on a room only when he chooses
    /// (the anchor names are his, agreed before the manifests are used to build).
    /// </summary>
    public static class RoomManifests
    {
        public const string Folder = "Assets/Data/Rooms";
        public const string Schema = "procsense.room-manifest/1";
        public const string OwnedRoot = "Manifest owned";
        public static readonly Regex AnchorName = new Regex(@"^(Anchor_[A-Za-z0-9]+|SLOT_[a-z0-9_]+_\d+|NPC_[A-Za-z]+|TELEPORT_[A-Za-z0-9]+|DOOR_[A-Za-z0-9]+(_(Inside|Outside))?)$");
        public static readonly string[] PrefabRoots = { "Assets/Prefabs/Equipment", "Assets/Prefabs/Environment" };

        /// <summary>The rooms and their scenes.</summary>
        public static readonly (string room, string scene)[] Rooms =
        {
            ("bay", "Assets/Scenes/PatientBay.unity"), ("corridor", "Assets/Scenes/Corridor.unity"), ("ot", "Assets/Scenes/OT.unity"), ("recovery", "Assets/Scenes/Recovery.unity"),
        };

        public sealed class Anchor { public string Name = "", Kind = ""; public Vector3 Pos; public Vector3 Rot; }
        public sealed class Placement { public string Asset = "", Slot = ""; }
        public sealed class Manifest
        {
            public string Room = "", Scene = "";
            public readonly List<Anchor> Anchors = new List<Anchor>();
            public readonly List<Placement> Placements = new List<Placement>();
        }

        public static string KindOf(string name) => name.Split('_')[0];

        // ------------------------------------------------------------------ export

        /// <summary>The manifest of an open scene: every anchor by name, and a slot and placement for every equipment or environment prefab in it.</summary>
        public static Manifest Export(Scene scene, string room)
        {
            var m = new Manifest { Room = room, Scene = scene.path };
            var counts = new Dictionary<string, int>();
            foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).OrderBy(Path, StringComparer.Ordinal))
            {
                bool owned = t.GetComponentInParent<ManifestOwned>(true) != null;
                if (owned && t.name.StartsWith("SLOT_", StringComparison.Ordinal)) continue;   // a placed slot is recorded with its asset below
                if (AnchorName.IsMatch(t.name) && !t.name.StartsWith("SLOT_", StringComparison.Ordinal))
                {
                    m.Anchors.Add(new Anchor { Name = t.name, Kind = KindOf(t.name), Pos = Round(t.position), Rot = Round(t.eulerAngles) });
                    continue;
                }
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if (!PrefabRoots.Any(r => path.StartsWith(r + "/", StringComparison.Ordinal))) continue;
                string asset = System.IO.Path.GetFileNameWithoutExtension(path);
                int n = counts.TryGetValue(asset, out var c) ? c + 1 : 1;
                counts[asset] = n;
                string slot = owned && t.parent != null && t.parent.name.StartsWith("SLOT_", StringComparison.Ordinal) ? t.parent.name : "SLOT_" + asset + "_" + n;
                m.Anchors.Add(new Anchor { Name = slot, Kind = "SLOT", Pos = Round(t.position), Rot = Round(t.eulerAngles) });
                m.Placements.Add(new Placement { Asset = asset, Slot = slot });
            }
            m.Anchors.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return m;
        }

        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name + "#" + t.GetSiblingIndex().ToString("D3", CultureInfo.InvariantCulture);
        private static Vector3 Round(Vector3 v) => new Vector3(R(v.x), R(v.y), R(v.z));
        private static float R(float x) { float r = (float)Math.Round(x, 3); return r == 0 ? 0 : r; }   // no "-0"

        // ------------------------------------------------------------------ text

        public static string ToJson(Manifest m)
        {
            var sb = new StringBuilder();
            sb.Append("{\n \"schema\": \"").Append(Schema).Append("\",\n \"room\": \"").Append(m.Room).Append("\",\n \"scene\": \"").Append(m.Scene).Append("\",\n \"anchors\": [\n");
            for (int i = 0; i < m.Anchors.Count; i++)
            {
                var a = m.Anchors[i];
                sb.Append("  {\"name\": \"").Append(a.Name).Append("\", \"kind\": \"").Append(a.Kind).Append("\", \"pos\": ").Append(V(a.Pos)).Append(", \"rot\": ").Append(V(a.Rot)).Append('}')
                  .Append(i + 1 < m.Anchors.Count ? ",\n" : "\n");
            }
            sb.Append(" ],\n \"placements\": [\n");
            for (int i = 0; i < m.Placements.Count; i++)
                sb.Append("  {\"asset\": \"").Append(m.Placements[i].Asset).Append("\", \"slot\": \"").Append(m.Placements[i].Slot).Append("\"}").Append(i + 1 < m.Placements.Count ? ",\n" : "\n");
            sb.Append(" ]\n}\n");
            return sb.ToString();
        }

        private static string V(Vector3 v) => "[" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + "]";
        private static string F(float x) => x.ToString("0.###", CultureInfo.InvariantCulture);

        public static Manifest Parse(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            if ((string)root["schema"] != Schema) throw new FormatException("not a room manifest (" + root["schema"] + ")");
            var m = new Manifest { Room = (string)root["room"], Scene = (string)root["scene"] };
            foreach (Dictionary<string, object> a in (List<object>)root["anchors"])
                m.Anchors.Add(new Anchor { Name = (string)a["name"], Kind = (string)a["kind"], Pos = Vec(a["pos"]), Rot = Vec(a["rot"]) });
            foreach (Dictionary<string, object> p in (List<object>)root["placements"])
                m.Placements.Add(new Placement { Asset = (string)p["asset"], Slot = (string)p["slot"] });
            return m;
        }

        private static Vector3 Vec(object o)
        {
            var l = ((List<object>)o).Select(x => Convert.ToSingle(x, CultureInfo.InvariantCulture)).ToArray();
            return new Vector3(l[0], l[1], l[2]);
        }

        // ------------------------------------------------------------------ validate

        /// <summary>What is wrong with a manifest: a badly named anchor, a name used twice, a placement in a slot that does not exist or shared with another, an asset with no prefab.</summary>
        public static List<string> Validate(Manifest m)
        {
            var problems = new List<string>();
            foreach (var a in m.Anchors.Where(a => !AnchorName.IsMatch(a.Name))) problems.Add(m.Room + ": anchor '" + a.Name + "' does not follow the naming rule (T-EN2)");
            foreach (var g in m.Anchors.GroupBy(a => a.Name).Where(g => g.Count() > 1)) problems.Add(m.Room + ": anchor '" + g.Key + "' is named " + g.Count() + " times");
            var slots = new HashSet<string>(m.Anchors.Where(a => a.Kind == "SLOT").Select(a => a.Name));
            foreach (var p in m.Placements)
            {
                if (!slots.Contains(p.Slot)) problems.Add(m.Room + ": " + p.Asset + " is placed in slot '" + p.Slot + "', which the manifest does not have");
                if (PrefabPath(p.Asset) == null) problems.Add(m.Room + ": no prefab for asset '" + p.Asset + "'");
            }
            foreach (var g in m.Placements.GroupBy(p => p.Slot).Where(g => g.Count() > 1)) problems.Add(m.Room + ": slot '" + g.Key + "' holds " + string.Join(" and ", g.Select(p => p.Asset)));
            return problems;
        }

        public static string PrefabPath(string asset)
        {
            foreach (var root in PrefabRoots)
            {
                var hit = AssetDatabase.FindAssets(asset + " t:Prefab", new[] { root }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == asset).OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();
                if (hit != null) return hit;
            }
            return null;
        }

        // ------------------------------------------------------------------ place

        /// <summary>
        /// Builds the manifest's slots and assets in the scene under one root marked ManifestOwned, replacing that root if it exists; nothing outside it is touched.
        /// Returns the root. Never hand-move an object under it: edit the manifest and place again.
        /// </summary>
        public static GameObject Place(Manifest m, Scene scene)
        {
            var problems = Validate(m);
            if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == OwnedRoot && g.GetComponent<ManifestOwned>() != null).ToList())
                UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(OwnedRoot);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<ManifestOwned>().Room = m.Room;
            var bySlot = m.Anchors.Where(a => a.Kind == "SLOT").ToDictionary(a => a.Name);
            foreach (var p in m.Placements)
            {
                var a = bySlot[p.Slot];
                var slot = new GameObject(a.Name);
                slot.transform.SetParent(root.transform, false);
                slot.transform.SetPositionAndRotation(a.Pos, Quaternion.Euler(a.Rot));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(p.Asset));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(slot.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return root;
        }

        /// <summary>A text picture of what Place built (names, prefab, position), to show that placing twice gives the same scene.</summary>
        public static string Dump(GameObject root)
        {
            var sb = new StringBuilder();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                sb.Append(Path(t)).Append(' ').Append(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)).Append(' ').Append(V(Round(t.position))).Append(' ').Append(V(Round(t.eulerAngles))).Append('\n');
            return sb.ToString();
        }

        // ------------------------------------------------------------------ menus

        public static string FileOf(string room) => Folder + "/" + room + ".json";

        [MenuItem("ProcSense/Rooms/Export room manifests from the scenes")]
        public static void ExportAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;   // opening the rooms replaces the open scenes
            WriteAll();
        }

        /// <summary>Writes every room's manifest from its scene, then reopens what was open. The caller makes sure no open scene has unsaved changes.</summary>
        public static void WriteAll()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder);
                foreach (var (room, path) in Rooms)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    File.WriteAllText(FileOf(room), ToJson(Export(scene, room)), new UTF8Encoding(false));
                }
                AssetDatabase.Refresh();
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        [MenuItem("ProcSense/Rooms/Place the open room from its manifest")]
        public static void PlaceOpenRoom()
        {
            var scene = SceneManager.GetActiveScene();
            var room = Rooms.FirstOrDefault(r => r.scene == scene.path).room;
            if (room == null || !File.Exists(FileOf(room))) { Debug.LogWarning("[ProcSense] no room manifest for " + scene.path); return; }
            if (!EditorUtility.DisplayDialog("Place from manifest", "Rebuild the manifest-owned slots and assets of " + room + "? Objects outside '" + OwnedRoot + "' are not touched.", "Place", "Cancel")) return;
            Place(Parse(File.ReadAllText(FileOf(room))), scene);
        }
    }
}
