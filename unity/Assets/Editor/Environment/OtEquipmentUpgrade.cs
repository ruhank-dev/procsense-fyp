using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 overhaul: replaces the primitive OT props with the rebuilt models from Tools/Environment/build_ot_equipment.py under the same ids.
    /// Copies each FBX over the imported model (GUID, importer settings and prefab wiring stay), then re-maps materials on the prefab by renderer-name suffix,
    /// because the new models use a few more parts (chrome, diffuser lens, castor rubber, screens). Idempotent.
    /// </summary>
    public static class OtEquipmentUpgrade
    {
        const string Models = "Assets/Models/Environment/RoomDressing", Prefabs = "Assets/Prefabs/Environment/RoomDressing";
        static readonly string[] Ids = { "surgical_light", "operating_table", "anaesthesia_unit", "mayo_stand", "supply_cupboard" };

        // renderer name suffix -> existing project material (the same ones HospitalRoomDressing assigned at first import)
        static readonly Dictionary<string, string> Map = new Dictionary<string, string> {
            ["M_Ivory"] = "Clean ivory housing", ["M_Sage"] = "Sage clinical door", ["M_PlasticWhite"] = "Satin ivory trim", ["M_PlasticGrey"] = "Recesses and tablet bezel",
            ["M_SteelBrushed"] = "Brushed stainless hardware", ["M_RubberBlack"] = "Castor rubber", ["M_ScreenOff"] = "Inactive display glass", ["M_Curtain"] = "Sage privacy fabric",
            ["M_Upholstery"] = "Teal clinical upholstery", ["M_Amber"] = "Amber waste lid", ["M_Chrome"] = "PatientBay chrome", ["M_Diffuser"] = "PatientBay ceiling diffuser",
            ["M_AlarmRed"] = "PatientBay alarm red", ["M_AccentBlue"] = "PatientBay gas blue"
        };

        static Material Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p); }
            Debug.LogWarning("[OtEquipmentUpgrade] material not found: " + name); return null;
        }

        public static void Apply()
        {
            foreach (var id in Ids)
            {
                var src = "Logs/RoomDressing/" + id + ".fbx"; if (!File.Exists(src)) { Debug.LogError("[OtEquipmentUpgrade] missing " + src); continue; }
                var fbx = Models + "/" + id + ".fbx"; File.Copy(src, fbx, true); AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.Refresh();
            foreach (var id in Ids)
            {
                var path = Prefabs + "/" + id + ".prefab"; var root = PrefabUtility.LoadPrefabContents(path); int n = 0, miss = 0;
                try
                {
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        string key = null; foreach (var k in Map.Keys) if (r.name.EndsWith(k)) key = k;
                        if (key == null) { miss++; continue; }
                        var m = Find(Map[key]); if (m != null) r.sharedMaterial = m;
                        r.receiveGI = ReceiveGI.Lightmaps; GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic); n++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                Debug.Log($"[OtEquipmentUpgrade] {id}: {n} renderers mapped, {miss} unmapped");
            }
            AssetDatabase.SaveAssets(); Debug.Log("[OtEquipmentUpgrade] applied");
        }
    }
}
