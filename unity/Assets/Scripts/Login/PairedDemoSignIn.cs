using System;
using System.Collections;
using ProcSense.VR.Auth;

namespace ProcSense.VR.Login
{
    /// <summary>
    /// The sign-in of the networked demonstration (P3-NET-11), without any drawing: a headset paired with the laptop that types the fixed demonstration account signs in as the synthetic
    /// demonstration user through <see cref="AuthService.SignInPairedDevice"/>; the view then moves the game flow on (MenuFlow.SignedIn). When the laptop cannot be used and the build
    /// allows the offline demonstration, the player still gets in without a service identity (as before). Real accounts never take this road.
    /// </summary>
    public static class PairedDemoSignIn
    {
        /// <summary>True when the typed address is the fixed demonstration account and this headset holds a device token.</summary>
        public static bool Applies(string email) =>
            string.Equals((email ?? "").Trim(), DemoLoginSettings.DemoEmail, StringComparison.OrdinalIgnoreCase) && DeviceTokenStore.IsPaired;

        /// <summary>Runs the paired sign-in. <paramref name="finished"/> is called once the person may enter (laptop identity, or the offline demonstration); otherwise <paramref name="message"/> says why not.</summary>
        public static IEnumerator Run(Func<AuthService> factory, bool offlineAllowed, Action<string> message, Action finished)
        {
            message("Signing in through the laptop...");
            SignInResult result = null;
            yield return factory().SignInPairedDevice(r => result = r);
            if (result != null && result.Ok) { finished(); yield break; }
            string reason = result != null ? result.Message : "Sign-in failed.";
            if (offlineAllowed)
            {
                AuthService.Current?.SignOut();   // no service identity: sessions stay on the headset
                finished();
                yield break;
            }
            message(reason);
        }
    }
}
