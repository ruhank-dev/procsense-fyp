using System.Collections.Generic;
using ProcSense.VR.ActionEmitters;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// Builds the poke panel of an item from its `panel` entries: one small pressable pad per entry, shown only in the states the entry lists. Each pad is a <see cref="XRSelectEmitter"/> set to the poke
    /// route, so the press sends the entry's one action and the tablet shows the same action as a button (T-IN5). The pads sit in a row under <c>parent</c>, 6 cm apart.
    /// </summary>
    public static class PanelBuilder
    {
        public const float PadWidth = .05f, PadHeight = .03f, Gap = .01f;

        /// <summary>Makes a pad per entry that exists in the controller's current state and returns them. Call again after a state change to rebuild.</summary>
        public static List<GameObject> Build(EquipmentController item, Transform parent)
        {
            var pads = new List<GameObject>();
            if (item == null || item.Machine == null || parent == null) return pads;
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name.StartsWith("PANEL_")) Object.Destroy(parent.GetChild(i).gameObject);
            float x = 0;
            foreach (var entry in item.Machine.PanelNow())
            {
                var pad = new GameObject("PANEL_" + entry.Id);
                pad.transform.SetParent(parent, false);
                pad.transform.localPosition = new Vector3(x, 0, 0);
                pad.AddComponent<BoxCollider>().size = new Vector3(PadWidth, PadHeight, .01f);
                var emitter = pad.AddComponent<XRSelectEmitter>();
                emitter.Route = XRSelectEmitter.SelectionRoute.Poke;
                emitter.ConfigureParams(entry.Verb, entry.Params, entry.Target, entry.Label);
                var text = new GameObject("Label").AddComponent<TextMeshPro>();
                text.transform.SetParent(pad.transform, false);
                text.transform.localPosition = new Vector3(0, 0, -.006f);
                text.text = entry.Label;
                text.fontSize = .25f;
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(PadWidth, PadHeight);
                pads.Add(pad);
                x += PadWidth + Gap;
            }
            return pads;
        }
    }
}
