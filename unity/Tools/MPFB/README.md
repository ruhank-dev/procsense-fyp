# Permanent MPFB patient build

MPFB is the permanent PatientBay patient for app launch, builds and corridor return. Open it with ProcSense > Open Patient Bay or Characters > Open MPFB Patient. Boot requires the demo login by default. The earlier MPFBPatientReview scene is retained only as an out-of-build development snapshot; the imported CGTrader graphics are retired.

The character uses MPFB 2.0.18 with the GameEngine rig, CC0 system assets, Faceunits 01 and Visemes 02. The source has a valid Unity Humanoid avatar, 53 bones, 67 body facial/speech shapes and separate skin, eyes, eyelashes, brows, teeth, tongue, hair and a derived short-sleeve patient gown. Both arms are posed through the rig. PatientBay adds the fitted idle layer described below; clinical signs, speech playback and ragdoll remain separate work. The Animator stays disabled to preserve the fitted bed pose; its valid Avatar remains available for later animation integration. Facial shapes are native SkinnedMeshRenderer controls and are tested independently from Core.

## Fitted patient idle

`PatientIdleView` on `MPFB patient` adds gentle breathing, coordinated bilateral
blinks, small gaze changes and head motion under 2.1 degrees. Three generated
meshes (`patient_body_idle`, `patient_gown_idle`, `patient_shorts_idle`) share
the same world-space torso lift profile: at most 14mm at the chest and 9mm at the abdomen, tapering to
zero at the back, hands, feet and neckline. The visible blanket and its unused
idle mesh are removed. Original blanket source assets remain for history.

The saved arms rest closer to the body with softer elbows and slightly curled
fingers. Small shoulder/forearm/wrist/finger movements add life without large
gestures; they ease down while sensor placement guides are visible. The cuff,
probe and wristband follow the corresponding bones. `SensorAttachment` aligns
accepted mounted equipment after the idle pose and before cable rendering;
it does not send another clinical action as the patient moves.

Opaque navy shorts cover the pelvis beneath the gown, including low foot-end
views. `build_patient_shorts.py` derives them from the existing CC0 MPFB trousers:
824 vertices, 1652 triangles, weighted to the patient's existing skeleton with
a 1.8mm hem/thickness. `PatientCoverageSetup.Install()` imports and wires this
layer once through AssetDatabase, rejecting Play, baking, dirty scenes and an
existing shorts object. The gown fitting recipe samples the shorts waistband
as an inner surface to keep the gown outside it.

Eight thigh/calf/ankle/toe channels add alternating 3.8-second wiggle bursts
with pauses. The left starts at 4 seconds, the right at 10, repeating every
18 seconds; ankle movement is limited to 2.3 degrees and toes to 3.5 degrees.
This is restrained presentation animation, with no ragdoll or clinical actions.

`patient_gown_relaxed` refits the skirt that was previously hidden under the
blanket, retaining the original topology, UVs and skin weights. The Blender
recipe `refit_idle_gown.py` reads/writes snapshots in ignored
`Logs/PatientArmsReview`. `PatientArmSetup.Prepare()` performs initial pose,
blanket removal and idle wiring once; it rejects existing arm-motion setup,
Play/baking and dirty scenes. `ImportGown()` imports the fitted output through
AssetDatabase and refreshes the breathing surfaces with preserved GUIDs.
For later refits, `ExportFit()` snapshots the original gown in the current saved
pose; run the pinned Blender with `-b --factory-startup --python-exit-code 1 -P
unity/Tools/MPFB/refit_idle_gown.py` from the repository root, then `ImportGown()`
in the connected Editor. `FitRestPose()` reapplies the supported arm directions
and refreshes motion axes without adding another finger curl. Inspect palm/thumb
contact as well as the wrist joint; a joint above the bed can still hide fingers
intersecting the mattress.

The body idle copy has 15 channels (ten eye controls, four reserved brow/mouth
controls and breathing). The original FBX retains all 67 facial/speech shapes;
later expression and lip-sync work can select the channels it needs from that
source. The original body, draped gown and blanket meshes are preserved.

Breathing is currently a **cosmetic idle loop**, not a clinically countable RR.
Core's observable contract supplies a sampled RR only after `count_rr`; continuous
visible respiration and clinical behaviour mapping remain a Core handoff TODO.
This view neither reads PatientState nor sends actions or changes clinical time.
It freezes on Core error/end and Unity pause, restores controlled shapes/head
and limb poses on disable, and resets its presentation time when the session changes.
It uses no global random state and does no runtime mesh generation.

To disable the layer, untick `PatientIdleView` on the patient. Change `Idle Breath
Seconds` only for art review; it is not a physiological setting. `PatientIdleSetup`
authors the initial meshes and scene wiring through the connected Editor and
AssetDatabase. `UpdateSurfaces()` regenerates only the configured idle meshes from
the existing bed pose, preserving their GUIDs; it rejects Play, baking or dirty
scenes. It does not redo the patient pose or fabric draping. Do not run the
original promotion/import scripts to inspect the patient.

Review both open/closed eyes, side/reverse torso, exposed skirt/legs and hands;
attach and remove both sensors, return them to the table, and leave/re-enter the
bay. Actual Quest appearance and frame timing still need device review.

## Local setup and reproducibility

All third-party source, packs, preferences and native Blender working files are in locally ignored build/mpfb, not in Blender's global configuration. dependencies.json records the exact tag and downloaded archive SHA-256 values. Graphical core/system and facial packs are CC0; the MPFB software itself is GPL-3.0-or-later and is not redistributed with the Unity model.

From repository root, set BLENDER_USER_RESOURCES to the absolute build/mpfb/blender-user path for the Blender command only. Run the pinned executable with -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/build_patient.py. It registers a local source extension repo in that isolated process, loads asset-library, reads patient-preset.json and emits FBX, native .blend, texture bundle and validation report under build/mpfb/output. MPFB generation does not depend on the retired CGTrader files.

In the connected Unity Editor, run ImportMpfbPatient.Main with the labelled unity-cli run_script command targeting this checkout. This imports through AssetDatabase, preserves existing GUIDs, remaps to URP Lit and configures Android ASTC 6x6. Mesh readability is enabled during editor calibration. CreateMpfbReview.Main now opens and preserves the authored PatientBay; it no longer clones a CGTrader fallback. Refit remains an explicit calibration operation using bone-weighted bounds. The installed height is a provisional 1.75m; preserve manual scene edits.

SampleMpfbCover.Main writes a temporary geometric leg-surface snapshot, excluding arm/finger triangles. Run build_linens.py to produce the pillow and fitted cover OBJ files, then AddMpfbLinens.Main to import them and dress the main bed. Set the FBX importer isReadable=false and reimport once editing is finished. Preserve metadata on subsequent imports. The initial skin-bound scale and gown-intersection issues were corrected during development; do not replace this process with coordinate-based body stretching.

Current limitations: this is one visual adult male preset, not case demographic binding. Stock skin/hair can be refined after visual review; skin does not yet reflect authoritative clinical signs. Facial capabilities do not establish lip-sync/audio integration. No headset performance/readability or medical appearance acceptance is claimed. Earlier checkpoints remain recoverable in Git history.

## Fabric correction

PatientBay uses a separate fitted, skinned gown and a rounded mattress above a visible frame. The earlier continuous blanket has been removed from the active bay; original gown/cover assets are preserved but disabled. The old Bed renderer is hidden; its collision remains intact. Fabric meshes are fitted for this bedside pose, with no runtime cloth or new clinical behavior. The gown retains all 53 bone bindings; larger future poses require a separate cloth fit review. The following original draping tools are historical authoring steps and will recreate the old blanket if explicitly rerun; the current idle/arm setup above preserves the uncovered presentation.

With the saved PatientBay open in Edit Mode, run ExportFabricFit.Main to snapshot the actual body, original gown and bed. It temporarily enables mesh readability and restores the importer setting afterward. Run the pinned Blender against drape_fabric.py and build_mattress.py, then ImportDrapedFabric.Main through the connected Editor. This welds garment export seams, interpolates the original bone weights, fits the garment against the torso, and spans the legs with restrained folds and draped bed edges. A free cloth simulation was rejected because it produced excessive shoulder crumpling. Mesh updates explicitly replace vertex buffers while preserving asset GUIDs. PoseFabricReview.Main is an explicit scene-authoring operation that rests the forearms beside the blanket; do not run it merely to open the scene.

The refined pose lowers the arms, flattens the hands, rests the heels and relaxes the ankles. After changing the pose, take a fresh snapshot and refit. Foot-end height and turn position come from all foot-region skin vertices, including the toe/ball skin; the recipe checks every such vertex against the triangulated blanket and writes fabric-coverage.json. The upper 18 cm follows the gown/mattress contour with conservative neighboring samples to avoid side intersections. Inspect both low side views, the foot, overhead and head-side close-up; a single three-quarter view missed the original toe breakthrough and waist gap. The stock eye atlas contains an alpha-zero cornea patch, so high-poly eyes also require alpha clipping in the URP material to reveal the irises.

The shared patient_wristband.obj is now under Assets/Models/Characters/MPFBPatient. Regenerate it with unity/Tools/make_patient_wristband.py, then import through AssetDatabase preserving its metadata. The retired CGTrader downloads, importer and static fitter have been removed.
