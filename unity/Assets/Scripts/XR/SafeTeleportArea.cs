using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ProcSense.VR.XR
{
    public sealed class SafeTeleportArea : TeleportationArea
    {
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (teleportationProvider == null) teleportationProvider = FindAnyObjectByType<SafeTeleportProvider>();
            if (!base.IsSelectableBy(interactor) || !(teleportationProvider is SafeTeleportProvider provider)) return false;
            return interactor is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit) && provider.CanTeleport(hit.point);
        }
        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit, ref TeleportRequest request) =>
            base.GenerateTeleportRequest(interactor, hit, ref request) && teleportationProvider is SafeTeleportProvider provider && provider.CanTeleport(request.destinationPosition);
    }
}
