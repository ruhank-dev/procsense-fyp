using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>A tablet or monitor button that sends one action to Core when pressed.</summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonEmitter : ActionEmitter
    {
        /// <summary>A panel button: the gateway tags its actions as tablet actions (P3-SES-04).</summary>
        protected override ProcSense.VR.Bridge.ActionSource Source => ProcSense.VR.Bridge.ActionSource.Tablet;

        private void OnEnable()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
            RegisterFallback();
        }

        private void OnDisable()
        {
            GetComponent<Button>().onClick.RemoveListener(OnClick);
            UnregisterFallback();
        }

        private void OnClick() => Fire();
    }
}
