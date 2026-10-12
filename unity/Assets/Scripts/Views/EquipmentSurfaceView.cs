using System.Linq;
using ProcSense.Core.Observation;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Small equipment surfaces display only permitted Core observations.</summary>
    public sealed class EquipmentSurfaceView : StateView
    {
        public enum Surface { Monitor, Pump, Identity }
        [SerializeField] private Surface surface;
        [SerializeField] private TMP_Text label;
        public string DisplayText => label != null ? label.text : "";
        public Surface Kind => surface;

        public void Configure(Surface value, TMP_Text text)
        {
            surface = value;
            label = text;
        }

        protected override void Render(ObservableState state)
        {
            if (surface == Surface.Pump)
            {
                SetText(label, "STANDBY");
                return;
            }
            if (surface == Surface.Identity)
            {
                SetText(label, state.Wristband == null ? "READ WRISTBAND" :
                    state.Wristband.NameText + "\n" + state.Wristband.DobText + "\n" + state.Wristband.MrnText);
                return;
            }
            string Reading(string name, string unit)
            {
                var value = state.Vitals.FirstOrDefault(v => v.Name == name);
                return value == null ? "--" : value.Value + unit;
            }
            SetText(label, "SpO2   " + Reading("spo2", "%") + "\nPULSE  " + Reading("heart_rate", " bpm") +
                "\nBP     " + Reading("systolic_bp", "") + "/" + Reading("diastolic_bp", " mmHg") +
                "\nTEMP   " + Reading("temperature_c", " C"));
        }
    }
}
