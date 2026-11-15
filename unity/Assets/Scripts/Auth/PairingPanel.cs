using System;
using System.Collections;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Auth
{
    /// <summary>
    /// The pairing keypad (P3-NET-07): the laptop shows a six-digit code, the player types it here once. Built in code from <see cref="UiKit"/> (one canvas, the default UI material, a single panel colour),
    /// so no prefab asset is needed. Entry point for Settings or the main menu: <c>PairingPanel.Open(parent, baseUrl, paired => ...)</c>.
    /// </summary>
    public sealed class PairingPanel : MonoBehaviour
    {
        private readonly PairingEntry _entry = new PairingEntry();
        private TMP_Text _code, _status;
        private string _baseUrl;
        private Action<bool> _closed;
        private bool _busy;
        public Func<IHttp> Http = LanHttp.Default;

        public static PairingPanel Open(Transform parent, string baseUrl, Action<bool> closed = null)
        {
            var go = new GameObject("Pairing panel");
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<PairingPanel>();
            panel._baseUrl = baseUrl; panel._closed = closed; panel.Build();
            return panel;
        }

        private void Build()
        {
            var canvas = UiKit.WorldCanvas("Pairing keypad", transform, new Vector2(420, 560), 0.0012f);
            UiKit.Panel(canvas.transform, "Background", Vector2.zero, new Vector2(420, 560), UiKit.PanelColor);
            UiKit.Label(canvas.transform, "Title", "Pair with the laptop", new Vector2(20, 14), new Vector2(380, 36), 28f, TextAlignmentOptions.Center);
            _code = UiKit.Label(canvas.transform, "Code", _entry.Display, new Vector2(20, 60), new Vector2(380, 56), 44f, TextAlignmentOptions.Center);
            _status = UiKit.Label(canvas.transform, "Status", "Type the six digits shown on the laptop.", new Vector2(20, 120), new Vector2(380, 48), 18f, TextAlignmentOptions.Center, UiKit.DimTextColor);
            for (int i = 0; i < 9; i++) { int digit = i + 1; Key(canvas.transform, digit.ToString(), i % 3, i / 3, () => Press(digit)); }
            Key(canvas.transform, "<", 0, 3, () => { _entry.Back(); Refresh(); });
            Key(canvas.transform, "0", 1, 3, () => Press(0));
            Key(canvas.transform, "OK", 2, 3, Submit);
            UiKit.MakeButton(canvas.transform, "Cancel", new Vector2(20, 500), new Vector2(380, 44), 22f).onClick.AddListener(() => Close(false));
        }

        private static void Key(Transform canvas, string label, int col, int row, Action onClick)
            => UiKit.MakeButton(canvas, label, new Vector2(30 + col * 130, 180 + row * 78), new Vector2(120, 68), 32f).onClick.AddListener(() => onClick());

        private void Press(int digit) { _entry.Press(digit); Refresh(); }
        private void Refresh() { _code.text = _entry.Display; }

        private void Submit()
        {
            if (_busy) return;
            if (!_entry.Complete) { _status.text = "Six digits are needed."; return; }
            StartCoroutine(Exchange());
        }

        private IEnumerator Exchange()
        {
            _busy = true; _status.text = "Pairing...";
            var outcome = PairOutcome.Failed;
            yield return PairingClient.Exchange(Http(), _baseUrl, _entry.Code, SystemInfo.deviceModel, o => outcome = o);
            _busy = false;
            switch (outcome)
            {
                case PairOutcome.Paired: case PairOutcome.NotRequired: Close(true); yield break;
                case PairOutcome.WrongCode: _entry.Clear(); Refresh(); _status.text = "That code is not right. Check the laptop."; break;
                case PairOutcome.Locked: _status.text = "Too many wrong codes. Wait a minute."; break;
                case PairOutcome.Offline: _status.text = "The laptop was not reached. Check the address and Wi-Fi."; break;
                default: _status.text = "Pairing failed. Try again."; break;
            }
        }

        private void Close(bool paired) { _closed?.Invoke(paired); Destroy(gameObject); }
    }
}
