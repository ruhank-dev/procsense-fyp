using System;
using System.Collections;
using ProcSense.Core;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Sync;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>What the main menu asked for: a pre-generated scenario instead of the configured case, for practice only. The menu sets it, the launcher in the persistent scene reads it once a session exists.</summary>
    public static class GeneratedScenarioRequest
    {
        public static bool Wanted { get; private set; }
        public static string Scenario { get; private set; } = "S1";
        public static string Level { get; private set; } = "beginner";
        /// <summary>The last thing the launcher did, in a sentence for the tablet or the menu; empty before any request.</summary>
        public static string Message { get; set; } = "";

        public static bool Ask(SessionMode mode, string scenario = "S1", string level = "beginner")
        {
            if (mode == SessionMode.Assessment) { Message = "Assessment uses approved authored cases only."; Wanted = false; return false; }
            Wanted = true; Scenario = scenario; Level = level; Message = "";
            return true;
        }

        public static void Done() { Wanted = false; }
        public static void Reset() { Wanted = false; Scenario = "S1"; Level = "beginner"; Message = ""; }
    }

    /// <summary>
    /// Plays the generated scenario the menu asked for (plan p1-live-scenario-generation-s10): once the persistent scene has a session, it fetches the next package through <see cref="PackageSessionStarter"/> and, when the package
    /// is accepted, replaces the authored session that was just opened. Any failure leaves that authored session running and says why in <see cref="GeneratedScenarioRequest.Message"/>. It never applies a clinical action.
    /// </summary>
    public sealed class GeneratedScenarioLauncher : MonoBehaviour
    {
        /// <summary>Replaced by tests so no service is called: starts the package and reports what happened.</summary>
        public Func<SimulationHost, string, string, Action<PackageStart>, IEnumerator> Runner;

        public PackageStart Last { get; private set; }
        private bool _running;

        private void Update()
        {
            var host = SimulationHost.Instance;
            if (!GeneratedScenarioRequest.Wanted || _running || host == null || host.Session == null) return;
            // the trainee has done nothing yet (the log holds actions only), so replacing the session loses nothing
            if (host.Session.Log.Count > 0) { GeneratedScenarioRequest.Done(); GeneratedScenarioRequest.Message = "A session is already running."; return; }
            StartCoroutine(Run(host));
        }

        public IEnumerator Run(SimulationHost host)
        {
            _running = true;
            var mode = host.Session.Mode;
            string scenario = GeneratedScenarioRequest.Scenario, level = GeneratedScenarioRequest.Level;
            GeneratedScenarioRequest.Message = "Fetching a scenario...";
            PackageStart result = null;
            Action<PackageStart> done = r => result = r;
            if (Runner != null) yield return Runner(host, scenario, level, done);
            else yield return PackageSessionStarter.Run(AuthService.Current ?? AuthService.Factory(), host, scenario, level, (ulong)DateTime.UtcNow.Ticks, mode, done);
            Last = result;
            GeneratedScenarioRequest.Message = result == null ? "No answer from the scenario service; the authored case is playing."
                : result.Started ? "Playing generated scenario " + result.CaseId + "."
                : result.PlayAuthoredInstead ? result.Message + " The authored case is playing." : "Could not start the generated scenario: " + result.Message + " The authored case is playing.";
            GeneratedScenarioRequest.Done();
            _running = false;
        }
    }
}
