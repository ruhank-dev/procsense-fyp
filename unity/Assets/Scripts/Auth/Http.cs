using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace ProcSense.VR.Auth
{
    /// <summary>What came back from one request. `NetworkError` means no answer at all (offline, timeout, the host asleep and not yet awake); otherwise `Status` is the HTTP status.</summary>
    public readonly struct HttpResult
    {
        public readonly long Status;
        public readonly string Body;
        public readonly bool NetworkError;
        public HttpResult(long status, string body, bool networkError = false) { Status = status; Body = body ?? ""; NetworkError = networkError; }
        public bool Ok => !NetworkError && Status >= 200 && Status < 300;
    }

    /// <summary>One HTTP request. A small seam so the sign-in and upload code can be tested without a network (a test supplies a scripted one).</summary>
    public interface IHttp
    {
        IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done);

        /// <summary>A request whose body is bytes (a recorded clip). A transport that cannot send bytes answers "no answer" so the caller takes its safe path; <see cref="UnityHttp"/> and the test transports that need it override this.</summary>
        IEnumerator SendBytes(string method, string url, byte[] body, string contentType, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
        {
            done(new HttpResult(0, "this transport does not send bytes", true));
            yield break;
        }
    }

    /// <summary>The real thing: UnityWebRequest. Runs on the main thread as a coroutine and never blocks a frame.</summary>
    public sealed class UnityHttp : IHttp
    {
        public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Math.Max(1, timeoutSeconds);
                if (jsonBody != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (headers != null) foreach (var pair in headers) request.SetRequestHeader(pair.Key, pair.Value);
                yield return request.SendWebRequest();
                bool noAnswer = request.result == UnityWebRequest.Result.ConnectionError;   // a protocol error still has a status and a body worth reading
                done(new HttpResult(request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : "", noAnswer));
            }
        }

        public IEnumerator SendBytes(string method, string url, byte[] body, string contentType, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Math.Max(1, timeoutSeconds);
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.SetRequestHeader("Content-Type", contentType ?? "application/octet-stream");
                }
                if (headers != null) foreach (var pair in headers) request.SetRequestHeader(pair.Key, pair.Value);
                yield return request.SendWebRequest();
                bool noAnswer = request.result == UnityWebRequest.Result.ConnectionError;
                done(new HttpResult(request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : "", noAnswer));
            }
        }
    }
}
