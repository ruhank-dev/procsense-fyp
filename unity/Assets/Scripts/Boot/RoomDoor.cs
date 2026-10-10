using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.VR.Boot
{
    public sealed class RoomDoor : MonoBehaviour
    {
        public string Destination;
        public string Title = "Corridor";
        public bool Restricted;
        private UnityEngine.UI.Button _button;
        private TMPro.TMP_Text _status;
        private RoomLoader _loader;
        private ProcSense.VR.XR.SafeTeleportProvider _teleport;

        private void Start()
        {
            var canvas = UiKit.WorldCanvas("Door panel", transform, new Vector2(440, 240), 0.002f);
            UiKit.Label(canvas.transform, "Title", Title, new Vector2(24, 20), new Vector2(392, 60), 34);
            _status = UiKit.Label(canvas.transform, "Status", Restricted ? "No trainee access" : "Trainee navigation", new Vector2(24, 90), new Vector2(392, 60), 24);
            if (!Restricted)
            {
                _button = UiKit.MakeButton(canvas.transform, "Enter", new Vector2(24, 160), new Vector2(392, 58), 28);
                _button.onClick.AddListener(Enter);
            }
        }

        public void Enter()
        {
            if (Restricted) return;
            if (_loader == null) _loader = FindAnyObjectByType<RoomLoader>();
            if (_loader != null) _loader.RequestRoom(Destination);
        }

        private void Update()
        {
            if (Restricted || _button == null) return;
            if (_loader == null) _loader = FindAnyObjectByType<RoomLoader>();
            if (_teleport == null) _teleport = FindAnyObjectByType<ProcSense.VR.XR.SafeTeleportProvider>();
            bool moving = (_loader != null && _loader.Busy) || (_teleport != null && _teleport.Busy);
            _button.interactable = _loader != null && !moving;
            string text = _loader == null ? "Navigation unavailable" : moving ? "Moving…" : string.IsNullOrEmpty(_loader.LastError) ? "Trainee navigation" : _loader.LastError;
            if (_status.text != text) _status.text = text;
        }
    }
}
