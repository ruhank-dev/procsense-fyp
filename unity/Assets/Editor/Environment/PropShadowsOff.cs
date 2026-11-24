using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>Quest has no realtime shadows (NoRealtimeLightingTests): the loose bedside props, the backrest and the handover desk cast none. Idempotent. Batch: -executeMethod ProcSense.EditorTools.PropShadowsOff.Run (Unity closed).</summary>
    public static class PropShadowsOff
    {
        [MenuItem("ProcSense/Environment/Props cast no shadows")]
        public static void Run()
        {
            Fix("Assets/Scenes/PatientBay.unity", "Oxygen props", "Bed backrest");
            Fix("Assets/Scenes/OT.unity", "Theatre handover point");
        }

        private static void Fix(string path, params string[] groups)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                if (System.Array.IndexOf(groups, root.name) >= 0)
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; EditorUtility.SetDirty(r); n++; }
            Debug.Log($"[PropShadowsOff] {path}: {n} renderers");
            EditorSceneManager.SaveScene(scene);
        }
    }
}
