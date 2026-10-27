using System.IO;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Draws a world-space canvas to a picture file in artifacts/unity (git-ignored), so a person can look at a panel's layout without a headset.</summary>
    public static class Snapshots
    {
        public static string Out(string name)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "unity"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, name);
        }

        public static void Photograph(Canvas canvas, string file)
        {
            // a test that runs no frame never starts the buttons' colour fade, so set each button's colour now
            foreach (var b in canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                if (b.targetGraphic != null) b.targetGraphic.CrossFadeColor(b.interactable ? b.colors.normalColor : b.colors.disabledColor, 0f, true, true);
            Canvas.ForceUpdateCanvases();
            var camGo = new GameObject("snapshot camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            var rect = (RectTransform)canvas.transform;
            float width = rect.sizeDelta.x * rect.localScale.x, height = rect.sizeDelta.y * rect.localScale.y;
            camGo.transform.position = canvas.transform.position - canvas.transform.forward * 1f;   // the canvas is centred on its own position
            camGo.transform.rotation = canvas.transform.rotation;
            cam.orthographicSize = height / 2f * 1.04f;
            cam.aspect = width / height;
            var texture = new RenderTexture(1800, 1200, 24);
            cam.targetTexture = texture;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = texture;
            var picture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            picture.Apply();
            File.WriteAllBytes(file, picture.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(picture);
            UnityEngine.Object.DestroyImmediate(camGo);
        }
    }
}
