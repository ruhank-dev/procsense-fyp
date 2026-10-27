using ProcSense.VR.ActionEmitters;

namespace ProcSense.VR.UI.Tablet
{
    /// <summary>
    /// The emitter of a tablet form's Send button. Unlike UIButtonEmitter it is not wired to the click: the tablet fires it once and then closes the form,
    /// so a click can never send twice or race the form closing. It registers no tablet fallback (it has no label).
    /// </summary>
    public sealed class FormSendEmitter : ActionEmitter
    {
    }
}
