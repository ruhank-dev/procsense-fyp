using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Sync
{
    [Serializable] internal class PackageOfferDto { public string package_hash, scenario, level, case_id, spine; }

    /// <summary>What fetching a generated scenario package gave: the package text ready for <see cref="ProcSense.VR.Bridge.SimulationHost.StartFromPackage"/>, or a plain sentence saying why not and whether an authored case should be played instead.</summary>
    public sealed class FetchedPackage
    {
        public bool Ok;
        public string Text = "";
        public string Hash = "";
        public string CaseId = "";
        public string Error = "";
        /// <summary>True when the trainee can simply play an authored case: no network, no pool, nothing left in the pool. False for an answer that is a fault (a package that does not match its hash).</summary>
        public bool PlayAuthoredInstead;
    }

    /// <summary>
    /// Fetches the next pre-generated scenario package from the service (plan p1-live-scenario-generation-s10, DT-199): `POST /scenario-packages/next` names a package from the pool, `GET /scenario-packages/{hash}` returns its bytes, and the client refuses
    /// anything whose SHA-256 is not the address it asked for. It never decides what may be played: the engine's mode gate does (a generated package is refused in assessment mode), and every failure leaves the authored cases exactly as they were.
    /// </summary>
    public static class PackageClient
    {
        public static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n")));
                var sb = new StringBuilder(64);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static IEnumerator FetchNext(AuthService auth, string scenario, string level, Action<FetchedPackage> done)
        {
            if (auth == null || !auth.IsSignedIn) { done(new FetchedPackage { Error = "Sign in to fetch a new scenario.", PlayAuthoredInstead = true }); yield break; }
            HttpResult offer = default;
            yield return auth.Authorized("POST", "/scenario-packages/next", "{\"scenario\":\"" + scenario + "\",\"level\":\"" + level + "\"}", r => offer = r);
            if (offer.NetworkError) { done(new FetchedPackage { Error = "Cannot reach the service right now.", PlayAuthoredInstead = true }); yield break; }
            if (offer.Status == 503 || offer.Status == 404) { done(new FetchedPackage { Error = "The service has no new scenario ready.", PlayAuthoredInstead = true }); yield break; }
            if (!offer.Ok) { done(new FetchedPackage { Error = "The service could not offer a scenario (" + offer.Status + ").", PlayAuthoredInstead = true }); yield break; }
            PackageOfferDto dto = null;
            try { dto = JsonUtility.FromJson<PackageOfferDto>(offer.Body); } catch (ArgumentException) { }
            if (dto == null || string.IsNullOrEmpty(dto.package_hash) || dto.package_hash.Length != 64) { done(new FetchedPackage { Error = "The service's answer was not understood." }); yield break; }
            HttpResult bytes = default;
            yield return auth.Authorized("GET", "/scenario-packages/" + dto.package_hash, null, r => bytes = r);
            if (bytes.NetworkError) { done(new FetchedPackage { Error = "Cannot reach the service right now.", PlayAuthoredInstead = true }); yield break; }
            if (!bytes.Ok) { done(new FetchedPackage { Error = "The service could not send the scenario (" + bytes.Status + ").", PlayAuthoredInstead = true }); yield break; }
            if (Sha256Hex(bytes.Body) != dto.package_hash) { done(new FetchedPackage { Error = "The scenario that arrived does not match its hash and was not used." }); yield break; }
            done(new FetchedPackage { Ok = true, Text = bytes.Body, Hash = dto.package_hash, CaseId = dto.case_id });
        }
    }
}
