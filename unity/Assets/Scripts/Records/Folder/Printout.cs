using System.Linq;
using ProcSense.Core.Records;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.VR.Records.Folder
{
    /// <summary>
    /// A printed result lying on the desk (p0-patient-records-emr-s02): a sheet the trainee picks up, reads (its title and the first lines of the report, from Core) and drops into the folder's report pocket,
    /// which files it (file_document). Greybox: a thin white sheet; it stays where it is let go, because the ward has no desk collider yet.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class Printout : MonoBehaviour
    {
        public string DocumentId { get; private set; } = "";
        public string Text => _text != null ? _text.text : "";
        private TMP_Text _text;

        public static Printout Create(Transform parent, RecordDocument doc, Vector3 localPosition)
        {
            var go = new GameObject("Printout " + doc.Id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<BoxCollider>().size = new Vector3(0.21f, 0.003f, 0.297f);
            var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sheet.name = "Sheet";
            Object.DestroyImmediate(sheet.GetComponent<BoxCollider>());     // now, before the grab collects its colliders
            sheet.transform.SetParent(go.transform, false);
            sheet.transform.localScale = new Vector3(0.21f, 0.003f, 0.297f);
            sheet.GetComponent<Renderer>().material.color = new Color(0.97f, 0.97f, 0.94f);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var grab = go.GetComponent<XRGrabInteractable>() ?? go.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            var printout = go.AddComponent<Printout>();
            printout.DocumentId = doc.Id;
            var canvas = UiKit.WorldCanvas("Printout text", go.transform, new Vector2(630, 900), 0.0003f);
            canvas.transform.localPosition = new Vector3(0, 0.0016f, 0);
            canvas.transform.localRotation = Quaternion.Euler(90, 0, 0);
            canvas.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(0.97f, 0.97f, 0.94f);
            string firstLines = string.Join("\n", doc.Body.Split('\n').Take(8));
            printout._text = UiKit.Label(canvas.transform, "Text", doc.Title + "\n\n" + firstLines, new Vector2(35, 35), new Vector2(560, 830), 30, TextAlignmentOptions.TopLeft, Color.black);
            return printout;
        }
    }

    /// <summary>The folder's report pocket (SOCKET_Report): a socket that takes printouts only, so a wristband or a probe that passes near the folder is never swallowed.</summary>
    public sealed class ReportPocket : XRSocketInteractor
    {
        public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable interactable) =>
            base.CanHover(interactable) && interactable.transform.GetComponent<Printout>() != null;

        public override bool CanSelect(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable) =>
            base.CanSelect(interactable) && interactable.transform.GetComponent<Printout>() != null;
    }
}
