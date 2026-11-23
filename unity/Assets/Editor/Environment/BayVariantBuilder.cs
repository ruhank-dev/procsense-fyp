using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// P3-AST-08: builds the emergency department dressing of the patient bay (a red "Emergency Department, Resus bay" sign hanging above the foot of the bed, facing the trainee, and a triage chart on the east wall, as plain text on panels) and the
    /// <see cref="BayVariant"/> that shows it for emergency cases. The calm wall art stays in every variant: it carries baked shadows, so hiding it would leave dark patches. Idempotent.
    /// Menu: ProcSense > Environment > Build ED bay variant.
    /// </summary>
    public static class BayVariantBuilder
    {
        private static Material Panel(string path, Color colour)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", colour); m.SetFloat("_Smoothness", .15f); EditorUtility.SetDirty(m); return m;
        }

        [MenuItem("ProcSense/Environment/Build ED bay variant")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/PatientBay.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            foreach (var old in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "ED dressing" || t.name == "Bay variant").ToList()) Object.DestroyImmediate(old.gameObject);
            var red = Panel("Assets/Materials/Environment/ED_Sign.mat", new Color(.55f, .08f, .08f)); var white = Panel("Assets/Materials/Environment/ED_Chart.mat", new Color(.92f, .92f, .9f));

            var root = new GameObject("ED dressing");
            Sign(root.transform, "ED sign", new Vector3(0f, 2.12f, .6f), Quaternion.identity, new Vector2(.95f, .34f), red,new[] { ("EMERGENCY DEPARTMENT", .068f, Color.white, .06f), ("Resus bay 4", .04f, new Color(1f, .85f, .85f), -.075f) });
            Sign(root.transform, "ED triage chart", new Vector3(2.44f, 1.78f, 1.35f), Quaternion.Euler(0f, 90f, 0f), new Vector2(.62f, .62f), white, new[] { ("TRIAGE", .06f, new Color(.55f, .08f, .08f), .23f), ("1  Immediate", .036f, new Color(.7f, 0f, 0f), .12f), ("2  Very urgent", .036f, new Color(.85f, .35f, 0f), .045f), ("3  Urgent", .036f, new Color(.55f, .45f, 0f), -.03f), ("4  Standard", .036f, new Color(0f, .45f, .15f), -.105f), ("5  Non-urgent", .036f, new Color(0f, .3f, .6f), -.18f) });
            var host = new GameObject("Bay variant"); var v = host.AddComponent<BayVariant>(); v.quiet = new List<GameObject>(); v.emergency = new List<GameObject> { root };
            root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[BayVariant] built the ED dressing (shown for emergency department cases)");
        }

        /// <summary>A flat panel with centred text lines, readable from the side it faces (the viewer looks along the sign's +z).</summary>
        private static void Sign(Transform parent, string name, Vector3 at, Quaternion rot, Vector2 size, Material mat, (string text, float height, Color colour, float y)[] lines)
        {
            var sign = new GameObject(name); sign.transform.SetParent(parent, false); sign.transform.SetPositionAndRotation(at, rot);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "panel"; Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(sign.transform, false); quad.transform.localScale = new Vector3(size.x, size.y, 1f); quad.GetComponent<MeshRenderer>().sharedMaterial = mat;      // a quad faces -z: the viewer's side
            foreach (var (text, height, colour, y) in lines)
            {
                var t = new GameObject("text " + text).AddComponent<TextMeshPro>(); t.transform.SetParent(sign.transform, false);
                t.transform.localPosition = new Vector3(0f, y, -.005f); t.transform.localScale = Vector3.one * .1f;                                   // the text is laid out in tenths of a metre
                t.rectTransform.sizeDelta = new Vector2(size.x * 10f, height * 10f * 2f);
                t.text = text; t.fontSize = height * 10f * 8f; t.color = colour; t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold; t.enableWordWrapping = false; t.enableAutoSizing = false;
            }
        }
    }
}
