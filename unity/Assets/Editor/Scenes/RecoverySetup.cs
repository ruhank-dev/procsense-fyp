using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>One piece of the recovery bay: what it is, the equipment prefab it reuses (if that exists yet), where it stands and the size of its greybox stand-in.</summary>
    public sealed class RecoveryPlacement
    {
        public string Id = "", Prefab = "";
        public Vector3 Position, Size;
        public float Yaw;
    }

    /// <summary>
    /// The recovery bay's equipment (p1-theatre-extension-s04): the manifest of what stands where, reusing the P0 equipment prefabs (the vital signs monitor today; oxygen and suction once their models exist,
    /// p0-blender-equipment). A piece whose prefab is not built yet is placed as a labelled greybox stand-in of the same footprint, so the layout can be checked now and the real model drops into the same spot.
    /// AddonScenesSetup builds the Recovery scene with it (ProcSense > Tools > Build menu, recovery and clinical panels).
    /// </summary>
    public static class RecoverySetup
    {
        public const string Monitor = "Assets/Prefabs/Equipment/vital_signs_monitor.prefab";
        public const string Oxygen = "Assets/Prefabs/Equipment/oxygen_supply.prefab";
        public const string Suction = "Assets/Prefabs/Equipment/suction.prefab";

        // beside the head of the recovery bed (the bed stands at x 1.2, z 1.2, its head towards the back wall)
        public static readonly RecoveryPlacement[] Manifest =
        {
            new RecoveryPlacement { Id = "Monitor", Prefab = Monitor, Position = new Vector3(0.45f, 1.1f, 2.1f), Yaw = 200f, Size = new Vector3(0.33f, 0.29f, 0.18f) },
            new RecoveryPlacement { Id = "Oxygen", Prefab = Oxygen, Position = new Vector3(1.85f, 1.3f, 2.4f), Yaw = 180f, Size = new Vector3(0.12f, 0.3f, 0.1f) },
            new RecoveryPlacement { Id = "Suction", Prefab = Suction, Position = new Vector3(2.15f, 1.3f, 2.4f), Yaw = 180f, Size = new Vector3(0.16f, 0.3f, 0.14f) },
        };

        /// <summary>Places the manifest under the bay's root; returns what was placed by id (a prefab instance, or a stand-in named "... (stand-in)").</summary>
        public static Dictionary<string, GameObject> Place(Transform root)
        {
            var placed = new Dictionary<string, GameObject>();
            foreach (var p in Manifest)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.Prefab);
                GameObject go;
                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                    go.name = "Recovery " + p.Id;
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Recovery " + p.Id + " (stand-in)";
                    go.transform.SetParent(root, false);
                    go.transform.localScale = p.Size;
                }
                go.transform.position = p.Position;
                go.transform.rotation = Quaternion.Euler(0, p.Yaw, 0);
                placed[p.Id] = go;
            }
            return placed;
        }

        public static IEnumerable<string> StandIns() => Manifest.Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Prefab) == null).Select(p => p.Id);
    }
}
