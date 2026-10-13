using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ProcSense.VR.XR
{
    public sealed class SafeTeleportAnchor : TeleportationAnchor
    {
        private SafeTeleportProvider Provider
        {
            get { if (teleportationProvider == null) teleportationProvider = FindAnyObjectByType<SafeTeleportProvider>(); return teleportationProvider as SafeTeleportProvider; }
        }
        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            Provider != null && base.IsSelectableBy(interactor) && teleportationProvider is SafeTeleportProvider provider &&
            teleportAnchorTransform != null && provider.CanTeleport(teleportAnchorTransform.position);
        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit, ref TeleportRequest request) =>
            base.GenerateTeleportRequest(interactor, hit, ref request) && teleportationProvider is SafeTeleportProvider provider && provider.CanTeleport(request.destinationPosition);
    }
}
