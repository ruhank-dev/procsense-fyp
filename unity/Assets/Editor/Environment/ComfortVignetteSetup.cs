using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace ProcSense.EditorTools
{
    /// <summary>P3-SET-05: puts the toolkit's tunnelling vignette under the player rig's camera and wires it to every locomotion provider. Idempotent. The settings panel's "Comfort vignette" switches it on and off.</summary>
    public static class ComfortVignetteSetup
    {
        private const string Rig = "Assets/Prefabs/XR/PlayerRig.prefab";
        private const string Vignette = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";

        [MenuItem("ProcSense/Environment/Add comfort vignette to the rig")]
        public static void Run()
        {
            var vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Vignette);
            if (vignettePrefab == null) { Debug.LogError("ComfortVignetteSetup: the toolkit's TunnelingVignette prefab is missing"); return; }
            var root = PrefabUtility.LoadPrefabContents(Rig);
            try
            {
                var camera = root.GetComponentInChildren<Camera>(true);
                if (camera == null) { Debug.LogError("ComfortVignetteSetup: no camera in the rig"); return; }
                var controller = root.GetComponentInChildren<TunnelingVignetteController>(true);
                if (controller == null)
                {
                    var made = (GameObject)PrefabUtility.InstantiatePrefab(vignettePrefab, camera.transform);
                    made.transform.localPosition = Vector3.zero; made.transform.localRotation = Quaternion.identity;
                    controller = made.GetComponentInChildren<TunnelingVignetteController>(true);
                }
                var providers = controller.locomotionVignetteProviders;
                foreach (var p in root.GetComponentsInChildren<LocomotionProvider>(true))
                {
                    if (!(p is UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider
                          || p is UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.ContinuousTurnProvider
                          || p is UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider
                          || p is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider)) continue;
                    if (providers.Any(x => x.locomotionProvider == p)) continue;
                    providers.Add(new LocomotionVignetteProvider { locomotionProvider = p, enabled = true });
                }
                PrefabUtility.SaveAsPrefabAsset(root, Rig);
                Debug.Log("ComfortVignetteSetup: vignette wired to " + providers.Count + " providers");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
