using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    public enum SignInOutcome { Signed, WrongCredentials, Unreachable, ServiceNotSet, NoOrganisation, ConsentNeeded, Failed, NotPaired }

    public sealed class SignInResult
    {
        public SignInOutcome Outcome;
        public string Message = "";
        public bool Ok => Outcome == SignInOutcome.Signed;
    }

    /// <summary>
    /// Signing in to the hosted service (p1-backend-s10). The person types an email and password on the headset; the password goes straight to the account system (Supabase Auth) and is never stored or logged.
    /// The access token lives in memory only; it is renewed with the refresh token a minute before it runs out and once more if the service answers 401. Expiry is counted on the device's own timer
    /// (`Time.realtimeSinceStartup`) from the `expires_in` the service gave, never on the device's date, which can be wrong.
    /// The headset cannot answer the privacy question for anyone: when the person has not agreed yet, sign-in stops and says where to do it.
    /// </summary>
    public sealed class AuthService
    {
        // The service everything else talks to. Tests replace the factory with a scripted one.
        public static AuthService Current { get; private set; }
        public static Func<AuthService> Factory = CreateDefault;

        /// <summary>The service at the one server address (P3-NET-03): the laptop entered on the headset, else the build's own.</summary>
        public static AuthService CreateDefault() => new AuthService(LanHttp.Default(), ServerEndpoint.Current, ServerEndpoint.BuildTimeout()) { RemembersGood = true };

        /// <summary>Records the address as the last one that answered (the app's own service only, not the tests' scripted ones).</summary>
        public bool RemembersGood { get; set; }

        public static void Clear() { Current = null; }

        [Serializable] private class ConfigDto { public string supabase_url; public string supabase_publishable_key; }
        [Serializable] private class TokenDto { public string access_token; public string refresh_token; public int expires_in; }
        [Serializable] private class MembershipDto { public string organisation_id; public string organisation_name; public string slug; public string role; public string status; }
        [Serializable] private class MeDto { public string user_id; public string pseudonym; public string display_name; public MembershipDto[] memberships; }
        [Serializable] private class DemoIdentityDto { public string user_id; public string pseudonym; public string display_name; public string organisation_id; public string organisation_name; public string role; }
        [Serializable] private class ConsentDto { public bool needs_consent; }
        [Serializable] private class CredentialsDto { public string email; public string password; }
        [Serializable] private class RefreshDto { public string refresh_token; }

        private readonly IHttp _http;
        private readonly string _baseUrl;
        private readonly int _timeout;
        private string _supabaseUrl = "", _publishableKey = "";
        private string _accessToken, _refreshToken;
        private float _expiresAt;

        public AuthService(IHttp http, string baseUrl, int timeoutSeconds = 100)
        {
            _http = http;
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _timeout = timeoutSeconds;
        }

        public bool IsSignedIn => _accessToken != null;
        public string BaseUrl => _baseUrl;
        public string UserId { get; private set; } = "";
        public string Pseudonym { get; private set; } = "";
        public string DisplayName { get; private set; } = "";
        public string OrganisationId { get; private set; } = "";
        public string OrganisationName { get; private set; } = "";
        public string Role { get; private set; } = "";

        // ---- signing in -------------------------------------------------------------------------------------------------------------------------------------------------------------

        public IEnumerator SignIn(string email, string password, Action<SignInResult> done)
        {
            if (!ServiceSettings.IsAcceptable(_baseUrl)) { done(Fail(SignInOutcome.ServiceNotSet, "The service address is not set in this build.")); yield break; }

            HttpResult config = default;
            yield return _http.Send("GET", _baseUrl + "/config.json", null, null, _timeout, r => config = r);   // also wakes the free host
            if (config.NetworkError) { done(Fail(SignInOutcome.Unreachable, "Cannot reach the service.")); yield break; }
            if (RemembersGood && config.Ok) ServerEndpoint.MarkGood(_baseUrl);
            var configDto = config.Ok ? SafeParse<ConfigDto>(config.Body) : null;
            if (configDto == null || string.IsNullOrEmpty(configDto.supabase_url) || string.IsNullOrEmpty(configDto.supabase_publishable_key))
            { done(Fail(SignInOutcome.Failed, "The service did not give its sign-in settings.")); yield break; }
            if (!ServiceSettings.IsAcceptable(configDto.supabase_url)) { done(Fail(SignInOutcome.Failed, "The sign-in address is not secure.")); yield break; }
            _supabaseUrl = configDto.supabase_url.TrimEnd('/');
            _publishableKey = configDto.supabase_publishable_key;

            HttpResult token = default;
            var body = JsonUtility.ToJson(new CredentialsDto { email = email, password = password });
            yield return _http.Send("POST", _supabaseUrl + "/auth/v1/token?grant_type=password", body, AuthHeaders(), _timeout, r => token = r);
            if (token.NetworkError) { done(Fail(SignInOutcome.Unreachable, "Cannot reach the sign-in service.")); yield break; }
            if (!token.Ok) { done(Fail(token.Status == 400 || token.Status == 401 || token.Status == 422 ? SignInOutcome.WrongCredentials : SignInOutcome.Failed,
                token.Status == 400 || token.Status == 401 || token.Status == 422 ? "Email or password is not right." : "Sign-in failed (" + token.Status + ").")); yield break; }
            if (!TakeTokens(token.Body)) { done(Fail(SignInOutcome.Failed, "The sign-in answer was not understood.")); yield break; }

            HttpResult me = default;
            yield return Authorized("GET", "/me", null, r => me = r);
            if (!me.Ok) { SignOut(); done(Fail(me.NetworkError ? SignInOutcome.Unreachable : SignInOutcome.Failed, me.NetworkError ? "Cannot reach the service." : "The service did not accept the sign-in (" + me.Status + ").")); yield break; }
            var meDto = SafeParse<MeDto>(me.Body);
            var membership = FirstActive(meDto);
            if (membership == null) { SignOut(); done(Fail(SignInOutcome.NoOrganisation, "This account is not in an organisation yet. Ask your administrator to invite it.")); yield break; }

            HttpResult consent = default;
            yield return Authorized("GET", "/privacy/consent", null, r => consent = r);
            var consentDto = consent.Ok ? SafeParse<ConsentDto>(consent.Body) : null;
            if (consentDto == null) { SignOut(); done(Fail(consent.NetworkError ? SignInOutcome.Unreachable : SignInOutcome.Failed, "The service did not answer the privacy question.")); yield break; }
            if (consentDto.needs_consent)
            {
                SignOut();
                done(Fail(SignInOutcome.ConsentNeeded, "Before you use ProcSense, open " + _baseUrl + " on a phone or computer, sign in and accept the privacy choices. Then sign in here again."));
                yield break;
            }

            UserId = meDto.user_id ?? "";
            Pseudonym = meDto.pseudonym ?? "";
            DisplayName = meDto.display_name ?? "";
            OrganisationId = membership.organisation_id;
            OrganisationName = membership.organisation_name ?? "";
            Role = membership.role ?? "";
            Current = this;
            done(new SignInResult { Outcome = SignInOutcome.Signed, Message = "Signed in." });
        }

        /// <summary>
        /// The networked demonstration (P3-NET-11): a headset paired with the laptop plays as the synthetic demonstration user. The laptop answers <see cref="DemoIdentityPath"/> for a valid
        /// device token with the identity (user, organisation); nothing is typed and no account password is involved. The device token stands in for the access token, so the uploads
        /// and the other calls made through <see cref="Authorized"/> carry it (as the Bearer value; the laptop also reads X-ProcSense-Device). A real account never takes this road.
        /// </summary>
        public const string DemoIdentityPath = "/pairing/demo-identity";

        public IEnumerator SignInPairedDevice(Action<SignInResult> done)
        {
            if (!ServiceSettings.IsAcceptable(_baseUrl)) { done(Fail(SignInOutcome.ServiceNotSet, "The service address is not set in this build.")); yield break; }
            string deviceToken = DeviceTokenStore.Get();
            if (string.IsNullOrEmpty(deviceToken)) { done(Fail(SignInOutcome.NotPaired, "This headset is not paired with the laptop.")); yield break; }

            HttpResult answer = default;
            yield return _http.Send("GET", _baseUrl + DemoIdentityPath, null, new Dictionary<string, string> { { "X-ProcSense-Device", deviceToken } }, _timeout, r => answer = r);
            if (answer.NetworkError) { done(Fail(SignInOutcome.Unreachable, "Cannot reach the laptop.")); yield break; }
            if (answer.Status == 401 || answer.Status == 403)
            {
                DeviceTokenStore.Clear();   // the laptop forgot this headset (revoked): pair again
                done(Fail(SignInOutcome.NotPaired, "The laptop no longer knows this headset. Pair it again."));
                yield break;
            }
            var dto = answer.Ok ? SafeParse<DemoIdentityDto>(answer.Body) : null;
            if (dto == null || string.IsNullOrEmpty(dto.user_id) || string.IsNullOrEmpty(dto.organisation_id))
            { done(Fail(SignInOutcome.Failed, answer.Status == 404 ? "The laptop is not in demonstration mode." : "The laptop did not give the demonstration identity.")); yield break; }

            _accessToken = deviceToken;
            _refreshToken = null;
            _expiresAt = float.MaxValue;
            UserId = dto.user_id;
            Pseudonym = dto.pseudonym ?? "";
            DisplayName = dto.display_name ?? "";
            OrganisationId = dto.organisation_id;
            OrganisationName = dto.organisation_name ?? "";
            Role = string.IsNullOrEmpty(dto.role) ? "trainee" : dto.role;
            IsDemoDevice = true;
            Current = this;
            done(new SignInResult { Outcome = SignInOutcome.Signed, Message = "Signed in as the demonstration user." });
        }

        public bool IsDemoDevice { get; private set; }

        public void SignOut()
        {
            _accessToken = _refreshToken = null;
            _expiresAt = 0;
            IsDemoDevice = false;
            if (Current == this) Current = null;
        }

        // ---- calls to the service ---------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>A call to the service with the person's token. The token is renewed first when it is about to run out, and once more when the service says 401; if renewing fails the person is signed out.</summary>
        public IEnumerator Authorized(string method, string path, string jsonBody, Action<HttpResult> done, string contentType = null)
        {
            if (!IsSignedIn) { done(new HttpResult(401, "")); yield break; }
            if (Time.realtimeSinceStartup >= _expiresAt - 60f) yield return Renew();
            if (!IsSignedIn) { done(new HttpResult(401, "")); yield break; }
            HttpResult result = default;
            yield return _http.Send(method, _baseUrl + path, jsonBody, BearerHeaders(contentType), _timeout, r => result = r);
            if (result.Status == 401 && !result.NetworkError)
            {
                yield return Renew();
                if (!IsSignedIn) { done(result); yield break; }
                yield return _http.Send(method, _baseUrl + path, jsonBody, BearerHeaders(contentType), _timeout, r => result = r);
            }
            done(result);
        }

        /// <summary>The same as <see cref="Authorized"/> for a request whose body is bytes (a recorded clip).</summary>
        public IEnumerator AuthorizedBytes(string method, string path, byte[] body, string contentType, Action<HttpResult> done)
        {
            if (!IsSignedIn) { done(new HttpResult(401, "")); yield break; }
            if (Time.realtimeSinceStartup >= _expiresAt - 60f) yield return Renew();
            if (!IsSignedIn) { done(new HttpResult(401, "")); yield break; }
            HttpResult result = default;
            yield return _http.SendBytes(method, _baseUrl + path, body, contentType, BearerHeaders(null), _timeout, r => result = r);
            if (result.Status == 401 && !result.NetworkError)
            {
                yield return Renew();
                if (!IsSignedIn) { done(result); yield break; }
                yield return _http.SendBytes(method, _baseUrl + path, body, contentType, BearerHeaders(null), _timeout, r => result = r);
            }
            done(result);
        }

        private IEnumerator Renew()
        {
            if (_refreshToken == null) { SignOut(); yield break; }
            HttpResult refreshed = default;
            yield return _http.Send("POST", _supabaseUrl + "/auth/v1/token?grant_type=refresh_token", JsonUtility.ToJson(new RefreshDto { refresh_token = _refreshToken }), AuthHeaders(), _timeout, r => refreshed = r);
            if (refreshed.NetworkError) yield break;                       // no network now: keep the token, try again at the next call
            if (!refreshed.Ok || !TakeTokens(refreshed.Body)) SignOut();    // the account system refused: the person has to sign in again
        }

        // ---- helpers ----------------------------------------------------------------------------------------------------------------------------------------------------------------

        private bool TakeTokens(string json)
        {
            var dto = SafeParse<TokenDto>(json);
            if (dto == null || string.IsNullOrEmpty(dto.access_token)) return false;
            _accessToken = dto.access_token;
            _refreshToken = string.IsNullOrEmpty(dto.refresh_token) ? _refreshToken : dto.refresh_token;
            _expiresAt = Time.realtimeSinceStartup + Mathf.Max(30, dto.expires_in);
            return true;
        }

        private Dictionary<string, string> AuthHeaders() => new Dictionary<string, string> { { "apikey", _publishableKey } };

        private Dictionary<string, string> BearerHeaders(string contentType)
        {
            var headers = new Dictionary<string, string> { { "Authorization", "Bearer " + _accessToken } };
            if (contentType != null) headers["Content-Type"] = contentType;
            return headers;
        }

        private static MembershipDto FirstActive(MeDto me)
        {
            if (me == null || me.memberships == null) return null;
            foreach (var m in me.memberships) if (m != null && m.status == "active") return m;
            return null;
        }

        private static T SafeParse<T>(string json) where T : class
        {
            try { return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }

        private static SignInResult Fail(SignInOutcome outcome, string message) => new SignInResult { Outcome = outcome, Message = message };
    }
}
