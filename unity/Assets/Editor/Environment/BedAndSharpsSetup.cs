using System.Linq;
using ProcSense.VR.Interaction;
using ProcSense.VR.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Puts the bed's head-up control beside the bed and the sharps-bin behaviour on the wall bin in PatientBay. Idempotent: the control is named "Bed control". The positions are a first guess from the bed hinge
    /// and the bin's bounds: look at them in the Editor and nudge them.
    /// </summary>
    public static class BedAndSharpsSetup
    {
        private const string Scene = "Assets/Scenes/PatientBay.unity";

        [MenuItem("ProcSense/Environment/Add bed control and sharps bin")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var hinge = Object.FindAnyObjectByType<BedBackrest>(FindObjectsInactive.Include);
            if (hinge == null) Debug.LogError("BedAndSharpsSetup: no BedBackrest in PatientBay");
            else if (GameObject.Find("Bed control") == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Bed control";
                go.transform.position = hinge.transform.position + new Vector3(-.62f, .25f, 0f);
                go.transform.localScale = new Vector3(.14f, .09f, .03f);
                go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.12f, .35f, .6f) };
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<XRSimpleInteractable>();
                var bed = go.AddComponent<BedControl>();
                var text = new GameObject("Label").AddComponent<TextMeshPro>();
                text.transform.SetParent(go.transform, false);
                text.transform.localPosition = new Vector3(0, 1.1f, -1.2f);   // above the button, toward the viewer (the cube is scaled, so these are in its own units)
                text.transform.localScale = new Vector3(1f / .14f, 1f / .09f, 1f / .03f) * .01f;
                text.fontSize = 36; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(30, 12);
                bed.Configure(text);
                Debug.Log("BedAndSharpsSetup: bed control placed at " + go.transform.position);
            }

            var bin = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name.StartsWith("sharps_container"));
            if (bin == null) Debug.LogError("BedAndSharpsSetup: no sharps_container in PatientBay");
            else if (bin.GetComponent<SharpsBin>() == null)
            {
                var rends = bin.GetComponentsInChildren<Renderer>();
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                bin.gameObject.AddComponent<SharpsBin>();
                var trigger = bin.gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = bin.InverseTransformPoint(new Vector3(b.center.x, b.max.y - .04f, b.center.z));   // over the opening at the top
                trigger.size = bin.InverseTransformVector(new Vector3(b.size.x * .8f, .08f, b.size.z * .8f)).Abs();
                Debug.Log("BedAndSharpsSetup: sharps bin ready");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
