using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Adds the packages the project needs through Unity's Package Manager, so versions are the ones that fit this editor.
    /// A package that ships scripts makes Unity reload its scripts, which wipes static fields; progress is therefore kept in
    /// SessionState and the installer resumes itself after every reload.
    /// Batch mode: Unity.exe -batchmode -nographics -projectPath unity -executeMethod ProcSense.EditorTools.PackageInstaller.Install -logFile log.txt
    /// </summary>
    [InitializeOnLoad]
    public static class PackageInstaller
    {
        // The VR stack from plan p0-vr-platform-s01: URP, UI, OpenXR + Meta Quest support, XR Interaction Toolkit, tests.
        private static readonly string[] Packages =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.ugui",
            "com.unity.test-framework",
            "com.unity.inputsystem",
            "com.unity.xr.management",
            "com.unity.xr.openxr",
            "com.unity.xr.interaction.toolkit",
        };

        private const string RunningKey = "ProcSense.PackageInstaller.Running";
        private const string NextKey = "ProcSense.PackageInstaller.Next";
        private const string FailedKey = "ProcSense.PackageInstaller.Failed";

        private static AddRequest _current;

        static PackageInstaller()
        {
            if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Poll;   // resume after a script reload
        }

        public static void Install()
        {
            SessionState.SetBool(RunningKey, true);
            SessionState.SetInt(NextKey, 0);
            SessionState.SetBool(FailedKey, false);
            EditorApplication.update += Poll;
            Debug.Log("[PackageInstaller] starting, " + Packages.Length + " packages");
        }

        private static void Poll()
        {
            if (_current != null)
            {
                if (!_current.IsCompleted) return;
                if (_current.Status == StatusCode.Success)
                {
                    Debug.Log("[PackageInstaller] added " + _current.Result.name + " " + _current.Result.version);
                    SessionState.SetInt(NextKey, SessionState.GetInt(NextKey, 0) + 1);
                }
                else
                {
                    Debug.LogError("[PackageInstaller] FAILED " + _current.Error?.message);
                    SessionState.SetBool(FailedKey, true);
                    SessionState.SetInt(NextKey, SessionState.GetInt(NextKey, 0) + 1);
                }
                _current = null;
                return;
            }
            int next = SessionState.GetInt(NextKey, 0);
            if (next < Packages.Length)
            {
                Debug.Log("[PackageInstaller] adding " + Packages[next]);
                _current = Client.Add(Packages[next]);   // adding an already installed package is harmless
                return;
            }
            EditorApplication.update -= Poll;
            SessionState.SetBool(RunningKey, false);
            bool failed = SessionState.GetBool(FailedKey, false);
            Debug.Log("[PackageInstaller] done, failed=" + failed);
            EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
