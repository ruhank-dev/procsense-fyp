using System.IO;
using ProcSense.Core.Content;
using UnityEngine;
using UnityEngine.Networking;

namespace ProcSense.VR.Content
{
    /// <summary>
    /// Gives Core its content files. In the Editor it reads the repository's content/ folder directly (nothing to keep in sync); in a
    /// build it reads the copy the build script placed in StreamingAssets/content (inside the APK on Quest, hence the web request).
    /// Core parses the text itself, so no JSON library is involved.
    /// </summary>
    public sealed class UnityContentReader : IContentReader
    {
        private readonly string _root;

        public UnityContentReader(string root) { _root = root; }

        public static UnityContentReader ForThisPlatform()
        {
#if UNITY_EDITOR
            return new UnityContentReader(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content")));
#else
            return new UnityContentReader(Application.streamingAssetsPath + "/content");
#endif
        }

        public string ReadText(string relativePath)
        {
            string path = _root + "/" + relativePath;
            if (!path.Contains("://")) return File.ReadAllText(path);

            using (var request = UnityWebRequest.Get(path))
            {
                var op = request.SendWebRequest();
                while (!op.isDone) { }   // tiny local files; a blocking read keeps Session creation in Awake (plan: Observable never null on frame one)
                if (request.result != UnityWebRequest.Result.Success) throw new FileNotFoundException("content file not found: " + path);
                return request.downloadHandler.text;
            }
        }
    }
}
