using System;
using System.Collections;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;

namespace ProcSense.VR.Sync
{
    /// <summary>What <see cref="PackageSessionStarter"/> did: it started a generated scenario, or it did not and says why (the caller then starts an authored case, as it always could).</summary>
    public sealed class PackageStart
    {
        public bool Started;
        public string Hash = "";
        public string CaseId = "";
        public string Message = "";
        /// <summary>True when the authored cases are the right thing to play next (offline, nothing in the pool, assessment mode). False for a fault worth showing (a package that did not match its hash, or one the engine refused).</summary>
        public bool PlayAuthoredInstead;
    }

    /// <summary>
    /// Fetch a pre-generated scenario and start it (plan p1-live-scenario-generation-s10): <see cref="PackageClient.FetchNext"/>, then <see cref="SimulationHost.StartFromPackage"/>. Assessment mode never fetches: a generated package is practice-only and the engine's mode gate would refuse it,
    /// so the starter does not even ask. Every failure leaves the host as it was (no session is half-started) and the authored cases playable. A button or menu entry that calls this is scene work (Sannan, DC-02).
    /// </summary>
    public static class PackageSessionStarter
    {
        public static IEnumerator Run(AuthService auth, SimulationHost host, string scenario, string level, ulong seed, SessionMode mode, Action<PackageStart> done, IContentReader real = null)
        {
            if (mode == SessionMode.Assessment) { done(new PackageStart { Message = "Assessment uses approved authored cases only.", PlayAuthoredInstead = true }); yield break; }
            FetchedPackage fetched = null;
            yield return PackageClient.FetchNext(auth, scenario, level, r => fetched = r);
            if (!fetched.Ok) { done(new PackageStart { Message = fetched.Error, PlayAuthoredInstead = fetched.PlayAuthoredInstead }); yield break; }
            PackageStart result;
            try
            {
                string hash = host.StartFromPackage(fetched.Text, seed, mode, real);
                result = new PackageStart { Started = true, Hash = hash, CaseId = fetched.CaseId };
            }
            catch (ContentException e)
            {
                result = new PackageStart { Message = "The scenario was refused: " + e.Message };
            }
            done(result);
        }
    }
}
