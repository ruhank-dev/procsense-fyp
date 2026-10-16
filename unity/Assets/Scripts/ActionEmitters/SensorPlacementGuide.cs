using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>Non-interactive visual targets; only SensorAttachment can place/send actions.</summary>
    internal sealed class SensorPlacementGuide
    {
        private readonly GameObject _patient, _home;
        private readonly Renderer[] _patientRenderers, _homeRenderers;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        public bool Visible => _patient != null && _patient.activeSelf;

        public SensorPlacementGuide(XRSocketInteractor socket, Transform home, Material material, float returnRadius)
        {
            var trigger = socket.GetComponent<SphereCollider>();
            float radius = trigger != null ? trigger.radius : .065f;
            _patient = Make("Patient placement bubble", socket.attachTransform != null ? socket.attachTransform : socket.transform, radius, material);
            _home = Make("Tray return bubble", home, returnRadius, material);
            _patientRenderers = _patient.GetComponentsInChildren<Renderer>();
            _homeRenderers = _home.GetComponentsInChildren<Renderer>();
            Hide();
        }

        private static GameObject Make(string name, Transform target, float radius, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(target, false);
            var scale = target.lossyScale;
            root.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Highlight bubble";
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localScale = Vector3.one * (radius * 2);
            var collider = bubble.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            var mesh = bubble.GetComponent<Renderer>();
            mesh.sharedMaterial = material;
            mesh.shadowCastingMode = ShadowCastingMode.Off;
            mesh.receiveShadows = false;
            for (int ring = 0; ring < 2; ring++)
            {
                var go = new GameObject("Target outline");
                go.transform.SetParent(root.transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 48;
                line.widthMultiplier = .0018f;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / line.positionCount;
                    line.SetPosition(i, ring == 0 ? new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius
                        : new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            return root;
        }

        public void Refresh(bool held, bool patientReady, bool homeReady)
        {
            if (!held) { Hide(); return; }
            Paint(_patient, _patientRenderers, patientReady ? new Color(.25f, 1, .55f) : new Color(.1f, .85f, 1));
            Paint(_home, _homeRenderers, homeReady ? new Color(.25f, 1, .55f) : new Color(.4f, .65f, 1));
        }
        private void Paint(GameObject marker, Renderer[] renderers, Color color)
        {
            if (marker == null) return;
            marker.SetActive(true);
            foreach (var renderer in renderers)
            {
                color.a = renderer is LineRenderer ? .85f : .12f + .025f * Mathf.Sin(Time.unscaledTime * 3);
                _properties.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_properties);
            }
        }
        public void Hide()
        {
            if (_patient != null) _patient.SetActive(false);
            if (_home != null) _home.SetActive(false);
        }
        public void Dispose()
        {
            if (_patient != null) Object.Destroy(_patient);
            if (_home != null) Object.Destroy(_home);
        }
    }
}
