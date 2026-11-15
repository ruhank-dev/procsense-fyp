using System.Collections.Generic;
using ProcSense.VR.UI.Theme;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.EditorTools.UI
{
    /// <summary>
    /// P3-UIX-05: brings prefabs whose UI was baked by an older UiKit (a world canvas with a flat "Background", buttons with a coloured Image) to the glass look UiKit builds now,
    /// in place: the existing objects keep their file ids and the prefab its GUID, only the "Glass" children are added and the flat colours cleared. Run once per baked prefab
    /// (menu ProcSense/UI/Migrate baked UI to glass, or ProcSense.EditorTools.UI.UiGlassPrefabMigration.Run()).
    /// </summary>
    public static class UiGlassPrefabMigration
    {
        public static readonly string[] Prefabs = { "Assets/Prefabs/Equipment/patient_folder.prefab" };

        [MenuItem("ProcSense/UI/Migrate baked UI to glass")]
        public static void Run()
        {
            foreach (var path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = Migrate(root);
                    if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[ProcSense] " + path + ": " + changed + " canvas or button(s) moved to glass");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds the glass the current UiKit makes to every baked canvas and button under the root; returns how many it changed.</summary>
        public static int Migrate(GameObject root)
        {
            int n = 0;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                var bg = canvas.transform.Find("Background");
                if (bg == null || canvas.transform.Find("Glass") != null || bg.GetComponent<Image>() == null) continue;
                var size = ((RectTransform)canvas.transform).sizeDelta;
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
                var glass = GlassPanel.Add(canvas.transform, "Glass", Vector2.zero, size);
                glass.Radius = Mathf.Min(UiTheme.Current.Radius, Mathf.Min(size.x, size.y) / 4f);
                glass.transform.SetSiblingIndex(bg.GetSiblingIndex());
                bg.GetComponent<Image>().color = UiKit.Clear;
                n++;
            }
            var buttons = new List<Button>(root.GetComponentsInChildren<Button>(true));
            foreach (var b in buttons)
            {
                var image = b.GetComponent<Image>();
                if (image == null || b.transform.Find("Glass") != null || !b.name.StartsWith("Button - ")) continue;
                var size = ((RectTransform)b.transform).sizeDelta;
                image.color = new Color(1f, 1f, 1f, 0f);
                var glass = GlassPanel.Add(b.transform, "Glass", Vector2.zero, size, GlassPanel.Style.Solid);
                glass.Radius = Mathf.Min(14f, size.y / 2f);
                var r = glass.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = r.offsetMax = Vector2.zero;
                glass.transform.SetSiblingIndex(0);
                b.targetGraphic = glass;
                n++;
            }
            return n;
        }
    }
}
