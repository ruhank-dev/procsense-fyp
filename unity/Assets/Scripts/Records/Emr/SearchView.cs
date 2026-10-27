using System;
using System.Collections.Generic;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Records.Emr
{
    /// <summary>
    /// The EMR's search box with its world-space keypad (p0-patient-records-emr-s03): letters, digits and the characters a record number or a date needs, poked or pointed at with the controller ray.
    /// Search sends what was typed; the quick list under it means most trainees never type at all.
    /// </summary>
    public sealed class SearchView
    {
        private static readonly string[] Rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL-", "ZXCVBNM/ " };
        private const int MaxLength = 32;

        private readonly TMP_Text _box;
        public string Query { get; private set; } = "";
        public event Action<string> SearchPressed;
        public IReadOnlyList<Button> Keys => _keys;
        private readonly List<Button> _keys = new List<Button>();

        public SearchView(Transform parent, Vector2 topLeft)
        {
            UiKit.Panel(parent, "Search box", topLeft, new Vector2(300, 44), Color.white);
            _box = UiKit.Label(parent, "Query", "", topLeft + new Vector2(8, 0), new Vector2(284, 44), 20, TextAlignmentOptions.MidlineLeft, EmrLook.Text);
            for (int r = 0; r < Rows.Length; r++)
                for (int c = 0; c < Rows[r].Length; c++)
                {
                    char ch = Rows[r][c];
                    var key = UiKit.MakeButton(parent, ch == ' ' ? "space" : ch.ToString(), topLeft + new Vector2(c * 30, 52 + r * 44), new Vector2(ch == ' ' ? 58 : 28, 40), ch == ' ' ? 13f : 18f);
                    var label = key.GetComponentInChildren<TMP_Text>();
                    label.overflowMode = TextOverflowModes.Overflow;      // a key is narrower than the button's text margins: let a wide letter (W, M) show in full
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    key.onClick.AddListener(() => Type(ch.ToString()));
                    _keys.Add(key);
                }
            var y = 52 + Rows.Length * 44;
            UiKit.MakeButton(parent, "Delete", topLeft + new Vector2(0, y), new Vector2(96, 40), 16f).onClick.AddListener(Backspace);
            UiKit.MakeButton(parent, "Clear", topLeft + new Vector2(102, y), new Vector2(96, 40), 16f).onClick.AddListener(Clear);
            UiKit.MakeButton(parent, "Search", topLeft + new Vector2(204, y), new Vector2(96, 40), 16f).onClick.AddListener(() => SearchPressed?.Invoke(Query));
        }

        public void Type(string text)
        {
            Query = (Query + text).Length > MaxLength ? Query : Query + text;
            _box.text = Query;
        }

        public void Backspace()
        {
            if (Query.Length > 0) Query = Query.Substring(0, Query.Length - 1);
            _box.text = Query;
        }

        public void Clear()
        {
            Query = "";
            _box.text = "";
        }
    }
}
