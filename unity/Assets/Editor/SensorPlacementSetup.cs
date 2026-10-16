using System;
using System.Linq;
using ProcSense.VR.ActionEmitters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class SensorPlacementSetup
    {
        public const string MaterialPath = "Assets/Materials/Equipment/M_SensorPlacement.mat";

        [MenuItem("ProcSense/Equipment/Configure Sensor Placement Guides")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            var scene = SceneManager.GetSceneByPath(WorkspaceMenu.BayPath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Open and save PatientBay first.");
            var sensors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SensorAttachment>(true)).ToArray();
            if (sensors.Length != 2) throw new InvalidOperationException("Expected the two existing bedside sensors.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_SensorPlacement" };
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetColor("_BaseColor", new Color(.1f, .85f, 1, .15f));
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            foreach (var sensor in sensors)
            {
                Undo.RecordObject(sensor, "Configure sensor placement guide");
                sensor.ConfigurePlacementHighlight(material);
                EditorUtility.SetDirty(sensor);
                var trigger = sensor.Destination.GetComponent<SphereCollider>();
                Undo.RecordObject(trigger, "Make sensor target easier to reach");
                trigger.radius = sensor.BuildAction().Params["sensor"] == "spo2" ? .055f : .085f;
                EditorUtility.SetDirty(trigger);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
