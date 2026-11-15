using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    public enum ConsentStatus { Unknown, Checking, Needed, Saving, Agreed, Declined, WebOnly, Unreachable, Failed }

    /// <summary>One question the service asks before sessions may be stored (the server's own title and text, never a copy kept on the headset).</summary>
    public sealed class ConsentQuestion
    {
        public string Kind = "", Title = "", Text = "";
        public bool Current;
    }

    /// <summary>
    /// The consent step between sign-in and the main menu (owner decision D2, 15 Nov 2026): the signed-in identity, the paired LAN demonstration user included, answers the service's required
    /// questions itself through the normal route (GET and POST /privacy/consent, through <see cref="AuthService.Authorized"/>, so the device token or the access token goes with it). Nothing is
    /// agreed automatically: Agree records "yes" to each required question that is not yet current, Decline records "no", and the service refuses a session upload (403 consent_required) until
    /// the latest answer is yes to the current text. Pure logic over <see cref="AuthService"/>, so it is tested without a network.
    /// </summary>
    public sealed class ConsentStep
    {
        public const string Path = "/privacy/consent";

        [Serializable] private sealed class KindDto { public string kind; public string title; public string text; public bool required; public bool current; }
        [Serializable] private sealed class StateDto { public string version; public bool needs_consent; public KindDto[] kinds; }
        [Serializable] private sealed class AnswerDto { public string kind; public bool granted; }

        private readonly AuthService _auth;
        private readonly List<ConsentQuestion> _questions = new List<ConsentQuestion>();

        public ConsentStep(AuthService auth) { _auth = auth; }

        public ConsentStatus Status { get; private set; } = ConsentStatus.Unknown;
        public string Version { get; private set; } = "";
        public string Message { get; private set; } = "";
        public IReadOnlyList<ConsentQuestion> Questions => _questions;

        /// <summary>True once the service says nothing more is needed: the menu may open.</summary>
        public bool Satisfied => Status == ConsentStatus.Agreed;

        /// <summary>True while a request is out (the buttons wait).</summary>
        public bool Busy => Status == ConsentStatus.Checking || Status == ConsentStatus.Saving;

        /// <summary>Asks the service what it needs. Agreed when the identity already agreed to the current text; Needed with the questions otherwise.</summary>
        public IEnumerator Load(Action done = null)
        {
            Status = ConsentStatus.Checking;
            Message = "";
            HttpResult answer = default;
            yield return _auth.Authorized("GET", Path, null, r => answer = r);
            Take(answer, afterAnswer: false);
            done?.Invoke();
        }

        /// <summary>Records the person's answer to every required question that is not yet current: yes to all of them, or no. The service's reply decides what happens next.</summary>
        public IEnumerator Answer(bool agree, Action done = null)
        {
            if (Busy) { done?.Invoke(); yield break; }
            Status = ConsentStatus.Saving;
            Message = "";
            HttpResult answer = default;
            var kinds = new List<string>();
            foreach (var q in _questions) if (!agree || !q.Current) kinds.Add(q.Kind);
            if (kinds.Count == 0) kinds.Add("results");
            foreach (var kind in kinds)
            {
                yield return _auth.Authorized("POST", Path, JsonUtility.ToJson(new AnswerDto { kind = kind, granted = agree }), r => answer = r);
                if (!answer.Ok) break;
            }
            Take(answer, afterAnswer: true, agreed: agree);
            done?.Invoke();
        }

        private void Take(HttpResult answer, bool afterAnswer, bool agreed = true)
        {
            if (answer.NetworkError) { Fail(ConsentStatus.Unreachable, "Cannot reach the service to " + (afterAnswer ? "save your answer" : "check your privacy answers") + ". Check the connection and try again."); return; }
            if (answer.Status == 403 && (answer.Body ?? "").Contains("web_sign_in_required"))
            { Fail(ConsentStatus.WebOnly, "This account answers the privacy questions on the web: open " + _auth.BaseUrl + " on a phone or computer, sign in, answer them, then sign in here again."); return; }
            if (answer.Status == 401) { Fail(ConsentStatus.Failed, "You are signed out. Sign in again."); return; }
            if (!answer.Ok) { Fail(ConsentStatus.Failed, "The service could not " + (afterAnswer ? "save your answer" : "give the privacy questions") + " (" + answer.Status + ")."); return; }
            StateDto dto;
            try { dto = JsonUtility.FromJson<StateDto>(answer.Body); } catch (ArgumentException) { dto = null; }
            if (dto == null || dto.kinds == null) { Fail(ConsentStatus.Failed, "The service's privacy answer was not understood."); return; }
            Version = dto.version ?? "";
            _questions.Clear();
            foreach (var k in dto.kinds)
                if (k != null && k.required) _questions.Add(new ConsentQuestion { Kind = k.kind ?? "", Title = k.title ?? "", Text = k.text ?? "", Current = k.current });
            if (!dto.needs_consent) { Status = ConsentStatus.Agreed; Message = ""; return; }
            if (afterAnswer && !agreed)
            {
                Status = ConsentStatus.Declined;
                Message = "You declined. Your answer was recorded. Without your agreement your sessions cannot be stored on the server, so training as this user cannot continue. "
                        + "Choose Review again to change your answer, or Quit.";
                return;
            }
            Status = ConsentStatus.Needed;
            Message = afterAnswer ? "The service still needs an answer to the current text." : "";
        }

        private void Fail(ConsentStatus status, string message) { Status = status; Message = message; }
    }
}
