# Hospital lighting pass

Each room is baked separately because the three additive room shells occupy
the same world coordinates. Never bake all of them together.

The static shell and finishes receive non-directional baked lightmaps from
Progressive CPU: 24 texels/m, 1024 atlas maximum, 32 direct/128 indirect samples,
three bounces and local baked ambient occlusion. Ceiling panels provide broad
baked illumination; PatientBay also has a cool rectangular window emitter.
Corridor/OT ceiling lighting is cooler to balance the wood's warm bounce.

One low-intensity real-time directional light per active room provides dynamic
shadows. The ceiling does not cast shadows from this overhead key. Main-light
soft shadows are enabled in URP_Quest; additional-light shadow settings are
unchanged. The overhead ceiling/grid/fixture geometry does not cast from this
vertical key, avoiding artificial fixture silhouettes across the floor.
Old directional and point fill lights remain in their original
objects, disabled. Ceiling diffuser/window emission is explicitly enabled.

PatientBay has 60 light probes; Corridor and OT each have 108. The static
BakedProbeRefresh runtime helper requests retetrahedralization when additive
scene loading/unloading changes the probe sets. No simulation state is changed.
RoomLoader activates the visible room at startup so Boot's service scene does
not retain ownership of ambient/probe settings; room activation also requests
a probe refresh.
Each room also uses a 128-pixel baked reflection probe, captured after its
lightmap bake. There are no real-time cubemap captures.

The room's lighting data and lightmaps are saved beside its scene under
Assets/Scenes/PatientBay, Corridor and OT. Keep all native data and .meta files.
The Unity-local .gitattributes stores these native binary lighting assets and
EXR captures in Git LFS; they must not use the root YAML .asset merge rule.
Room-specific settings and explicit reflection captures are in this folder.

Preparation is implemented by HospitalLighting.Prepare(roomName) in the Editor
Environment folder. It refuses unsaved scenes and an existing lighting root.
For later static-layout changes, open only the affected room, review/save the
intended changes, then use the existing room lighting settings to bake again:
HospitalLighting.BeginBake(), wait until Lightmapping.isRunning is false, then
HospitalLighting.Finish(roomName). This saves the room's completed lighting and
reflection capture and makes a review image. Preparation is not a reset command.

New fixed geometry must have valid secondary UVs and ContributeGI enabled.
Movable equipment and the skinned patient remain dynamic and use probes.
Existing combined finish meshes were given secondary UVs through Unity's
Unwrapping API without changing asset GUIDs, colliders or scene layout.

Before/after previews are in the ignored Temp/LightingReview directory.
Quest frame timing, texture memory and headset visual acceptance remain pending
actual device verification.
