using System.Collections;
using UnityEngine;

namespace ProcSense.VR.XR
{
    public sealed class LocomotionFade : MonoBehaviour
    {
        private CanvasGroup _group;
        public float Alpha { get => _group != null ? _group.alpha : 0; set { Ensure(); _group.alpha = value; } }
        private void Ensure()
        {
            if (_group != null) return;
            var camera = GetComponent<Unity.XR.CoreUtils.XROrigin>().Camera;
            var go = new GameObject("LocomotionFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = Vector3.forward * Mathf.Max(0.15f, camera.nearClipPlane + 0.05f);
            ((RectTransform)go.transform).sizeDelta = new Vector2(4, 4);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
            go.GetComponent<UnityEngine.UI.Image>().color = Color.black;
            go.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            _group = go.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0;
        }
        public IEnumerator Fade(float target, float duration)
        {
            Ensure();
            float start = _group.alpha;
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            { _group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, time / duration)); yield return null; }
            _group.alpha = target;
        }
    }
}
