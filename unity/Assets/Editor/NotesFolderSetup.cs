using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class NotesFolderSetup
    {
        [MenuItem("ProcSense/Equipment/Install Notes Folder")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            var scene = SceneManager.GetSceneByPath(WorkspaceMenu.BayPath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Open and save PatientBay first; existing edits will not be overwritten.");
            var bay = scene.GetRootGameObjects().Single(r=>r.name=="PatientBay");
            if (bay.transform.Find("Physical notes folder") != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/patient_folder.prefab");
            if (prefab == null) throw new InvalidOperationException("Import the notes folder first.");
            var folder = (GameObject)PrefabUtility.InstantiatePrefab(prefab,bay.transform);
            folder.name = "Physical notes folder";
            folder.transform.localPosition = new Vector3(-1.2f,.91f,.4f);
            foreach (string name in new[] { "Notes", "Notes view" })
            {
                var old=bay.transform.Find(name);
                if(old != null) old.gameObject.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
