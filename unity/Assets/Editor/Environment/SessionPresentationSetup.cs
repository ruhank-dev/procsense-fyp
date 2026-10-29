using System;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ProcSense.EditorTools
{
    public static class SessionPresentationSetup
    {
        [MenuItem("ProcSense/Setup/Install session wait and speech presenter")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve unsaved scenes and stop Play/baking before installing presentation.");
            var scene=SceneManager.GetSceneByName("Sys_Core");
            if (!scene.isLoaded) throw new InvalidOperationException("Open Sys_Core first.");
            if(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SessionPresentation>(true)).Any())
                throw new InvalidOperationException("Session presenter already exists; preserve it.");
            var root=new GameObject("Session wait and speech presenter");SceneManager.MoveGameObjectToScene(root,scene);
            Undo.RegisterCreatedObjectUndo(root,"Install session presentation");root.AddComponent<SessionPresentation>();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
