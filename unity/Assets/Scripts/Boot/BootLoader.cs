using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProcSense.VR.Login;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProcSense.VR.Boot
{
    /// <summary>
    /// Boot scene: loads Sys_Core (session, player rig and room loader) first, then the bay and the Editor simulator.
    /// Sys_Core goes first so the Session already exists when the views wake up.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] private string[] scenes = { "Sys_Core", "PatientBay" };
        [SerializeField] private string editorOnlyScene = "DevOnly";
        [SerializeField] private DemoLoginSettings loginSettings;
        [SerializeField] private GameObject loginPrefab;
        [SerializeField] private GameObject loginRigPrefab;

        public void ConfigureLogin(DemoLoginSettings settings, GameObject view, GameObject rig)
        { loginSettings = settings; loginPrefab = view; loginRigPrefab = rig; }

        private IEnumerator Start()
        {
            if (loginSettings != null && loginSettings.ShouldShow()) yield return Login();
            foreach (var scene in scenes) yield return Load(scene);
            if (Application.isEditor && !Application.isBatchMode && ProcSense.VR.XR.EditorInputSettings.ShouldUseSimulator
                && !string.IsNullOrEmpty(editorOnlyScene)) yield return Load(editorOnlyScene);
        }

        private IEnumerator Login()
        {
            if (loginSettings != null) DemoLoginSettings.OfflineDemoAllowed = loginSettings.AllowOfflineDemoAccount;
            if (loginPrefab == null || loginRigPrefab == null)
            {
                Debug.LogError("Login launch assets are missing; the workspace has not been started.");
                while (true) yield return null;
            }
            var shell = new GameObject("Login shell");
            shell.transform.SetParent(transform, false);
            // Register UI first; near/far interactors otherwise auto-create a second EventSystem.
            var events = new GameObject("Login EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            events.transform.SetParent(shell.transform, false);
            var rig = Instantiate(loginRigPrefab, shell.transform);
            // The launch scene has no floor. Keep physical head/hand tracking,
            // but never run the application's movement or gravity here.
            LoginPresentation.FreezeRig(rig);
            foreach (var comfort in rig.GetComponentsInChildren<ProcSense.VR.XR.ComfortPanel>(true)) comfort.enabled = false;
            bool headset = Application.platform == RuntimePlatform.Android || UnityEngine.XR.XRSettings.isDeviceActive
                || (Application.isEditor && ProcSense.VR.XR.EditorInputSettings.Mode == ProcSense.VR.XR.EditorInputSettings.InputMode.Headset);
            if (!headset)
            {
                // Desktop text fields need mouse/keyboard focus without competing XR pointers.
                events.GetComponent<XRUIInputModule>().enabled = false;
                var desktopInput = events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                desktopInput.AssignDefaultActions();
            }
            var camera = rig.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                Debug.LogError("Login rig camera is missing; workspace remains locked.");
                while (true) yield return null;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .07f, .095f);
            var view = Instantiate(loginPrefab, shell.transform).GetComponent<DemoLoginView>();
            if (view == null || !view.IsReady)
            {
                if (view == null) Debug.LogError("Login prefab has no form; workspace remains locked.");
                while (true) yield return null;
            }
            view.gameObject.AddComponent<LoginPresentation>().Configure(camera, view, headset);
            while (!view.Completed) yield return null;
            Destroy(shell);
            // Tear down the launch rig/EventSystem before loading the application rig.
            yield return null;
        }

        private static IEnumerator Load(string scene)
        {
            if (SceneManager.GetSceneByName(scene).isLoaded) yield break;
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
        }
    }
}
