using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core.Logging;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    [Serializable] public class OrderOptionDto { public string id, label; }
    [Serializable] public class OrderNeedDto { public string param, question; public OrderOptionDto[] options; }
    [Serializable] public class OrderHearDto
    {
        public string status, intent, label, read_back, source, interpretation, ticket, error, interpreter, interpreter_model, interpreter_reported_model, fallback_reason;
        public string[] options;
        public bool dose_removed;
        public int interpreter_ms;
        public OrderNeedDto[] needs;
    }
    [Serializable] public class OrderPairDto { public string k, v; }
    [Serializable] public class OrderConfirmDto { public string outcome, verb, reason; public OrderPairDto[] action_pairs; }

    /// <summary>A detail the engine needs before it can take an order (the oxygen device, how the patient is moved): the question and the closed list the content allows.</summary>
    public sealed class OrderNeed
    {
        public string Param = "", Question = "";
        public List<KeyValuePair<string, string>> Options = new List<KeyValuePair<string, string>>();
    }

    /// <summary>What the order route made of one sentence (POST /orders/organisations/{id}/hear, POST .../details): a proposal to confirm (with its signed single-use ticket), a question to answer, or nothing to do.</summary>
    public sealed class OrderHearing
    {
        public bool FromService;
        public string Status = "", Intent = "", Label = "", ReadBack = "", Source = "", Interpretation = "", Ticket = "", Error = "", Reason = "";
        public string Interpreter = "offline_grammar", InterpreterModel = "", ReportedModel = "", FallbackReason = "";
        public int Ms;
        public bool DoseRemoved;
        public List<string> Options = new List<string>();
        public List<OrderNeed> Needs = new List<OrderNeed>();

        /// <summary>Something for the trainee to confirm: a read-back with a ticket. Nothing has been sent to the engine.</summary>
        public bool IsProposal => FromService && (Status == "confirm" || Status == "removed_dose_confirm") && !string.IsNullOrEmpty(Ticket);
        /// <summary>The route asks which of two or more items, or what exactly: no ticket exists and nothing can be confirmed yet.</summary>
        public bool IsClarification => FromService && Status == "clarify";
        /// <summary>The interpreter understood that the trainee asked for something the simulator does not have.</summary>
        public bool IsUnsupported => FromService && Status == "menu" && Interpretation == "unsupported";
        public bool ByGemma => Interpreter == "gemma_cloud";
        /// <summary>Which system read the sentence, in words for the panel: Gemma 4 31B Cloud, or the conservative offline grammar and why it answered.</summary>
        public string InterpreterText => ByGemma ? "Read by Gemma Cloud" : "Read by the offline grammar" + (string.IsNullOrEmpty(FallbackReason) ? "" : " (" + FallbackReason + ")");
    }

    /// <summary>The answer to "yes" or "no" for a ticket: released (the action named by the ticket, now to be sent to the engine), cancelled, still open, or refused (forged, expired, used, not yours).</summary>
    public sealed class OrderConfirmation
    {
        public bool FromService;
        public string Outcome = "", Verb = "", Reason = "";
        public Dictionary<string, string> Params = new Dictionary<string, string>();
        public bool Released => FromService && Outcome == "released";
        public bool Cancelled => FromService && Outcome == "cancelled";
        public bool Open => FromService && Outcome == "open";
        /// <summary>Released but the simulator has no verb for it (calling a surgeon, a nurse or a porter): it is read back and logged only.</summary>
        public bool NoEngineVerb => Released && string.IsNullOrEmpty(Verb);
    }

    /// <summary>
    /// The Unity half of the spoken MEDICAL orders (ADR-0028, 27 Oct 2026): the words the recogniser heard go to the order route with a short, identifier-free description of the scene; the route answers with a proposal (a read-back and a signed single-use
    /// ticket), a clarifying question, or "not an order". It starts nothing: only <see cref="Confirm"/> with the trainee's own yes releases the action the ticket names, and the engine still validates it. Never throws: no sign-in, no network, a refusal or an
    /// unreadable answer is "not an order" and the tablet and the ordinary speech path remain.
    /// </summary>
    public static class SpokenOrderClient
    {
        public const string Base = "/orders/organisations/";

        public static string HearBody(string text, string context)
        {
            var sb = new StringBuilder("{\"text\":");
            JsonText.Quote(sb, text.Length > 300 ? text.Substring(0, 300) : text);
            sb.Append(",\"context\":");
            JsonText.Quote(sb, context ?? "");
            return sb.Append('}').ToString();
        }

        public static string ConfirmBody(string ticket, string spoken)
        {
            var sb = new StringBuilder("{\"ticket\":");
            JsonText.Quote(sb, ticket);
            sb.Append(",\"spoken\":");
            JsonText.Quote(sb, spoken);
            return sb.Append('}').ToString();
        }

        public static string DetailsBody(string ticket, string param, string value)
        {
            var sb = new StringBuilder("{\"ticket\":");
            JsonText.Quote(sb, ticket);
            sb.Append(",\"choices\":{");
            JsonText.Quote(sb, param);
            sb.Append(':');
            JsonText.Quote(sb, value);
            return sb.Append("}}").ToString();
        }

        public static OrderHearing ParseHearing(string body)
        {
            OrderHearDto dto = null;
            try { dto = JsonUtility.FromJson<OrderHearDto>(body); } catch (ArgumentException) { }
            if (dto == null || string.IsNullOrEmpty(dto.status)) return new OrderHearing { Reason = "UNREADABLE" };
            var h = new OrderHearing
            {
                FromService = true, Status = dto.status, Intent = dto.intent ?? "", Label = dto.label ?? "", ReadBack = dto.read_back ?? "", Source = dto.source ?? "", Interpretation = dto.interpretation ?? "",
                Ticket = dto.ticket ?? "", Error = dto.error ?? "", Interpreter = string.IsNullOrEmpty(dto.interpreter) ? "offline_grammar" : dto.interpreter, InterpreterModel = dto.interpreter_model ?? "",
                ReportedModel = dto.interpreter_reported_model ?? "", FallbackReason = dto.fallback_reason ?? "", Ms = dto.interpreter_ms, DoseRemoved = dto.dose_removed,
            };
            if (dto.options != null) h.Options = dto.options.Where(o => !string.IsNullOrWhiteSpace(o)).Take(3).ToList();
            if (dto.needs != null)
                foreach (var n in dto.needs)
                    if (n != null && !string.IsNullOrEmpty(n.param) && n.options != null)
                        h.Needs.Add(new OrderNeed { Param = n.param, Question = n.question ?? "", Options = n.options.Where(o => o != null && !string.IsNullOrEmpty(o.id)).Take(4).Select(o => new KeyValuePair<string, string>(o.id, string.IsNullOrEmpty(o.label) ? o.id : o.label)).ToList() });
            return h;
        }

        public static OrderConfirmation ParseConfirmation(string body)
        {
            OrderConfirmDto dto = null;
            try { dto = JsonUtility.FromJson<OrderConfirmDto>(body); } catch (ArgumentException) { }
            if (dto == null || string.IsNullOrEmpty(dto.outcome)) return new OrderConfirmation { Reason = "UNREADABLE" };
            var c = new OrderConfirmation { FromService = true, Outcome = dto.outcome, Verb = dto.verb ?? "", Reason = dto.reason ?? "" };
            if (dto.action_pairs != null) foreach (var p in dto.action_pairs) if (p != null && !string.IsNullOrEmpty(p.k)) c.Params[p.k] = p.v ?? "";
            return c;
        }

        public static IEnumerator Hear(AuthService auth, string text, string context, Action<OrderHearing> done)
        {
            if (auth == null || !auth.IsSignedIn || string.IsNullOrWhiteSpace(text)) { done(new OrderHearing { Reason = "NOT_SIGNED_IN" }); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("POST", Base + auth.OrganisationId + "/hear", HearBody(text.Trim(), context), r => answer = r);
            done(answer.Ok ? ParseHearing(answer.Body) : new OrderHearing { Reason = answer.NetworkError ? "NO_NETWORK" : "HTTP_" + answer.Status });
        }

        public static IEnumerator Details(AuthService auth, string ticket, string param, string value, Action<OrderHearing> done)
        {
            if (auth == null || !auth.IsSignedIn) { done(new OrderHearing { Reason = "NOT_SIGNED_IN" }); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("POST", Base + auth.OrganisationId + "/details", DetailsBody(ticket, param, value), r => answer = r);
            done(answer.Ok ? ParseHearing(answer.Body) : new OrderHearing { Reason = answer.NetworkError ? "NO_NETWORK" : "HTTP_" + answer.Status });
        }

        public static IEnumerator Confirm(AuthService auth, string ticket, string spoken, Action<OrderConfirmation> done)
        {
            if (auth == null || !auth.IsSignedIn) { done(new OrderConfirmation { Reason = "NOT_SIGNED_IN" }); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("POST", Base + auth.OrganisationId + "/confirm", ConfirmBody(ticket, spoken), r => answer = r);
            done(answer.Ok ? ParseConfirmation(answer.Body) : new OrderConfirmation { Reason = answer.NetworkError ? "NO_NETWORK" : "HTTP_" + answer.Status });
        }
    }
}
