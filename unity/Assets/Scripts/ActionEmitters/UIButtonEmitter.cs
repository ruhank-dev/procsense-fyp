using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>A tablet or monitor button that sends one action to Core when pressed.</summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonEmitter : ActionEmitter
    {
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
