using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.VR.XR
{
    public sealed class ComfortPanel : MonoBehaviour
    {
        private UnityEngine.UI.Button _seat, _turn;
        private TMPro.TMP_Text _seatLabel, _turnLabel;
        private ComfortControls _controls;
        private SafeTeleportProvider _teleport;
        private ProcSense.VR.Boot.RoomLoader _rooms;
        private void Start()
        {
            var canvas = UiKit.WorldCanvas("Comfort panel", transform, new Vector2(420, 280), 0.002f);
            UiKit.Label(canvas.transform, "Title", "MOVEMENT", new Vector2(24, 18), new Vector2(372, 45), 32);
            UiKit.Label(canvas.transform, "Help", "Sit first, then calibrate.", new Vector2(24, 70), new Vector2(372, 36), 22);
            _seat = UiKit.MakeButton(canvas.transform, "Calibrate seated", new Vector2(24, 122), new Vector2(372, 56), 25);
            _turn = UiKit.MakeButton(canvas.transform, "Snap turn: 45°", new Vector2(24, 200), new Vector2(372, 56), 25);
            _seatLabel = _seat.GetComponentInChildren<TMPro.TMP_Text>();
            _turnLabel = _turn.GetComponentInChildren<TMPro.TMP_Text>();
            _seat.onClick.AddListener(() => { if (_controls != null) _controls.SetSeated(!_controls.Seated); });
            _turn.onClick.AddListener(() => { if (_controls != null) _controls.SetTurnAngle(_controls.TurnAngle == 45 ? 30 : 45); });
        }
        private void Update()
        {
            if (_seat == null) return;
            if (_controls == null) _controls = FindAnyObjectByType<ComfortControls>();
            if (_teleport == null) _teleport = FindAnyObjectByType<SafeTeleportProvider>();
            if (_rooms == null) _rooms = FindAnyObjectByType<ProcSense.VR.Boot.RoomLoader>();
            bool ready = _controls != null && _teleport != null && _rooms != null && !_teleport.Busy && !_rooms.Busy;
            _seat.interactable = _turn.interactable = ready;
            if (_controls == null) return;
            string seat = _controls.Seated ? "Use standing height" : "Calibrate seated";
            string turn = _controls.TurnAngle == 45 ? "Snap turn: 45°" : "Snap turn: 30°";
            if (_seatLabel.text != seat) _seatLabel.text = seat;
            if (_turnLabel.text != turn) _turnLabel.text = turn;
        }
    }
}
