using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Gives the visiting nurse in PatientBay her Rocketbox body and the idle and walk controllers. Idempotent.</summary>
    public static class NurseModelSetup
    {
        [MenuItem("ProcSense/Setup/Assign nurse model")]
        public static void Assign()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var nurse = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NurseWristbandController>(true)).Single();
            nurse.Model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Female_01.prefab");
            nurse.IdleController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Characters/RB_Idle_Loop.controller");
            nurse.WalkController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Characters/RB_Walk_Loop.controller");
            Debug.Assert(nurse.Model != null && nurse.IdleController != null && nurse.WalkController != null, "nurse assets missing");
            EditorUtility.SetDirty(nurse); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("ProcSense/Setup/Assign transport staff models")]
        public static void AssignTransportStaff()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var view = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PatientTransportView>(true)).Single();
            GameObject Body(string n) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/" + n + ".prefab");
            view.Staff = new StaffModels
            {
                Porter = Body("RB_Male_Adult_08"), Nurse = Body("RB_Medical_Female_02"), Doctor = Body("RB_Medical_Male_03"),
                Idle = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Characters/RB_Idle_Loop.controller"),
                Walk = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Characters/RB_Walk_Loop.controller"),
            };
            Debug.Assert(view.Staff.Porter != null && view.Staff.Nurse != null && view.Staff.Doctor != null && view.Staff.Idle != null && view.Staff.Walk != null, "staff assets missing");
            EditorUtility.SetDirty(view); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
