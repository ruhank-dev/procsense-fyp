# Front-end load distribution: the open Unity and Blender steps

**Status (27 October 2026):** a proposal by Abdul Moiz, to be confirmed by Ruhan Kamran and Sannan Shakeel. Until it is confirmed the owners in `docs/plans/reconciliation.json` stay as they are (every step below is still listed there under Sannan).

**Why:** on 27 October 2026 Ruhan reported that the engine, content, AI and backend work is built and verified ([ruhan-moiz-inventory.md](../ruhan-moiz-inventory.md): unfinished implementation 0). The plan stands at 220 of 279 approved steps. What remains is front-end work (Unity scenes, interaction and UI, Blender equipment, characters) and front-end testing on the Meta Quest 3. This page splits that work across the three members so it can be done in parallel; an agent working on a step reads its row here, then the step file.

## 1. Who does what

### Abdul Moiz (13 steps): Screen and logic Unity work that talks to the engine and the backend

| Step | Title | Wave | State |
|---|---|---|---|
| [p0-vr-interaction-ui-s03](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s03-tablet-shell.md) | tablet shell | 1 | open |
| [p0-vr-interaction-ui-s05](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s05-results-phone.md) | results phone | 1 | open |
| [p0-vr-interaction-ui-s08](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s08-checklist-boards.md) | checklist boards | 2 | open |
| [p0-vr-interaction-ui-s09](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s09-debrief-ui.md) | debrief ui | 2 | open |
| [p0-patient-records-emr-s02](../p0/p0-patient-records-emr/steps/p0-patient-records-emr-s02-physical-folder.md) | physical folder | 1 | open |
| [p0-patient-records-emr-s03](../p0/p0-patient-records-emr/steps/p0-patient-records-emr-s03-emr-shell-search-tabs.md) | emr shell search tabs | 1 | open |
| [p0-patient-records-emr-s06](../p0/p0-patient-records-emr/steps/p0-patient-records-emr-s06-records-usability.md) | records usability | 2 | open |
| [p0-voice-audio-s03](../p0/p0-voice-audio/steps/p0-voice-audio-s03-subtitles-playback.md) | subtitles playback | 2 | open |
| [p0-diagnostic-transitions-s06](../p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s06-wait-overlay-usability.md) | wait overlay usability | 2 | open |
| [p0-vr-platform-s06](../p0/p0-vr-platform/steps/p0-vr-platform-s06-scene-director.md) | scene director | 1 | open |
| [p0-vr-platform-s07](../p0/p0-vr-platform/steps/p0-vr-platform-s07-quest-builds-profiling-ci.md) | quest builds profiling ci | 2 | open |
| [p0-vr-platform-s08](../p0/p0-vr-platform/steps/p0-vr-platform-s08-unity-mcp-experiment.md) | unity mcp experiment | 2 | open |
| [p1-theatre-extension-s04](../p1/p1-theatre-extension/steps/p1-theatre-extension-s04-p1-equipment-scripts.md) | p1 equipment scripts | 2 | open |

### Sannan Shakeel (25 steps): 3D, physical interaction and everything that needs the headset

| Step | Title | Wave | State |
|---|---|---|---|
| [p0-characters-s01](../p0/p0-characters/steps/p0-characters-s01-base-human-set.md) | base human set | 2 | open |
| [p0-characters-s02](../p0/p0-characters/steps/p0-characters-s02-patient-models.md) | patient models | 2 | open |
| [p0-characters-s03](../p0/p0-characters/steps/p0-characters-s03-clinical-signs.md) | clinical signs | 2 | open |
| [p0-characters-s04](../p0/p0-characters/steps/p0-characters-s04-staff-avatars.md) | staff avatars | 2 | open |
| [p0-characters-s05](../p0/p0-characters/steps/p0-characters-s05-animation-behaviour-mapping.md) | animation behaviour mapping | 2 | open |
| [p0-characters-s06](../p0/p0-characters/steps/p0-characters-s06-movement-escort.md) | movement escort | 2 | open |
| [p0-characters-s07](../p0/p0-characters/steps/p0-characters-s07-performance-pass.md) | performance pass | 2 | open |
| [p0-characters-s08](../p0/p0-characters/steps/p0-characters-s08-nurse-entry-and-wristband-replacement.md) | nurse entry and wristband replacement | 1 | open |
| [p0-environment-rooms-s02](../p0/p0-environment-rooms/steps/p0-environment-rooms-s02-import-supervisor-model.md) | import supervisor model | 2 | open |
| [p0-environment-rooms-s03](../p0/p0-environment-rooms/steps/p0-environment-rooms-s03-optimise-lighting.md) | optimise lighting | 2 | open |
| [p0-environment-rooms-s04](../p0/p0-environment-rooms/steps/p0-environment-rooms-s04-anchors-manifest-placement.md) | anchors manifest placement | 1 | open |
| [p0-environment-rooms-s05](../p0/p0-environment-rooms/steps/p0-environment-rooms-s05-patient-bay-variants.md) | patient bay variants | 2 | open |
| [p0-environment-rooms-s06](../p0/p0-environment-rooms/steps/p0-environment-rooms-s06-ot-layout.md) | ot layout | 2 | open |
| [p0-vr-interaction-ui-s02](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s02-interaction-framework.md) | interaction framework | 1 | open |
| [p0-vr-interaction-ui-s06](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s06-bedside-interactions.md) | bedside interactions | 1 | open |
| [p0-vr-interaction-ui-s07](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s07-ot-interactions.md) | ot interactions | 2 | open |
| [p0-vr-interaction-ui-s12](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s12-wristband-nurse-interaction.md) | wristband nurse interaction | 1 | open |
| [p0-voice-audio-s04](../p0/p0-voice-audio/steps/p0-voice-audio-s04-lip-sync.md) | lip sync | 2 | open |
| [p0-diagnostic-transitions-s02](../p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s02-doors-blocking.md) | doors blocking | 1 | open |
| [p0-diagnostic-transitions-s03](../p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s03-patient-transport-visuals.md) | patient transport visuals | 2 | open |
| [p0-evaluation-performance-s01](../p0/p0-evaluation-performance/steps/p0-evaluation-performance-s01-skeleton1-quest-replay-check.md) | skeleton1 quest replay check | 2 | open |
| [p0-evaluation-performance-s03](../p0/p0-evaluation-performance/steps/p0-evaluation-performance-s03-weekly-profiling.md) | weekly profiling | 2 | open |
| [p0-evaluation-performance-s05](../p0/p0-evaluation-performance/steps/p0-evaluation-performance-s05-usability-study.md) | usability study | 2 | open |
| [p0-evaluation-performance-s08](../p0/p0-evaluation-performance/steps/p0-evaluation-performance-s08-release-v1.md) | release v1 | 2 | open |
| [p0-vr-interaction-ui-s11](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s11-usability-pass.md) | usability pass | 2 | open |

### Ruhan Kamran (14 steps): Scripted Blender equipment and engine-driven or clinical Unity work

| Step | Title | Wave | State |
|---|---|---|---|
| [p0-blender-equipment-s06](../p0/p0-blender-equipment/steps/p0-blender-equipment-s06-identity-documents.md) | identity documents | 1 | open |
| [p0-blender-equipment-s07](../p0/p0-blender-equipment/steps/p0-blender-equipment-s07-bedside-monitoring.md) | bedside monitoring | 1 | open |
| [p0-blender-equipment-s08](../p0/p0-blender-equipment/steps/p0-blender-equipment-s08-sampling-tests.md) | sampling tests | 1 | open |
| [p0-blender-equipment-s09](../p0/p0-blender-equipment/steps/p0-blender-equipment-s09-iv-oxygen-airway-bedside.md) | iv oxygen airway bedside | 1 | open |
| [p0-blender-equipment-s10](../p0/p0-blender-equipment/steps/p0-blender-equipment-s10-furniture-transport.md) | furniture transport | 2 | open |
| [p0-blender-equipment-s11](../p0/p0-blender-equipment/steps/p0-blender-equipment-s11-ot-machinery-1.md) | ot machinery 1 | 2 | open |
| [p0-blender-equipment-s12](../p0/p0-blender-equipment/steps/p0-blender-equipment-s12-ot-machinery-2.md) | ot machinery 2 | 2 | open |
| [p0-blender-equipment-s13](../p0/p0-blender-equipment/steps/p0-blender-equipment-s13-ot-trolleys-storage-hygiene.md) | ot trolleys storage hygiene | 2 | open |
| [p0-blender-equipment-s14](../p0/p0-blender-equipment/steps/p0-blender-equipment-s14-boards-doors-signs.md) | boards doors signs | 2 | open |
| [p0-blender-equipment-s15](../p0/p0-blender-equipment/steps/p0-blender-equipment-s15-lod-performance-pass.md) | lod performance pass | 2 | open |
| [p0-vr-interaction-ui-s04](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s04-monitor-traces-alarms.md) | monitor traces alarms | 1 | open |
| [p0-vr-interaction-ui-s10](../p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s10-accessibility-readability.md) | accessibility readability | 2 | open |
| [p0-voice-audio-s05](../p0/p0-voice-audio/steps/p0-voice-audio-s05-ambience-alarms.md) | ambience alarms | 2 | open |
| [p0-evaluation-performance-s06](../p0/p0-evaluation-performance/steps/p0-evaluation-performance-s06-clinical-realism-review.md) | clinical realism review | 2 | open |

Total in this split: 52 steps. Also open and not part of the split: `p0-evaluation-performance-s09` (demo material; Ruhan's tool and outline, Sannan's headset recording) and `p2-content-extensions-s04` (blocked on a pharmacist's list).

## 2. Order of work

**Wave 1, for the FYP-1 final evaluation:** S1 playable on the Quest up to the WHO Time Out. The interaction framework comes first because the other interaction steps build on it; then the tablet, the patient record, the monitor, the results phone, the bedside interactions and the wristband with the nurse; the identity, bedside, sampling and IV equipment; the room anchors and the scene director.

**Wave 2, FYP-2:** the theatre (OT layout, OT interactions, OT machinery, checklist boards), characters and animation, lip-sync, lighting and performance passes, the usability study and the release.

## 3. Who edits which files (to avoid merge conflicts)

Unity scene files merge badly. Each area has one owner; anyone else asks the owner or works on a prefab the scene references.

| Owner | Files and scenes |
|---|---|
| Abdul Moiz | Tablet, results phone, EMR and checklist UI prefabs and their scripts (`unity/Assets/Scripts/Views`, the tablet and phone prefabs), the debrief UI, the build and CI scripts |
| Sannan Shakeel | Room scenes (`PatientBay.unity`, `Corridor.unity`, `OT.unity`), lighting, characters, the XR rig and the physical interaction prefabs |
| Ruhan Kamran | Blender recipes and imported equipment (`tools/blender`, `unity/Assets/Models/Equipment`, equipment prefabs), the monitor traces and alarm views |

## 4. How a step is done

1. Read the step file and its phase README; check what already exists (parts of several steps were started by Ruhan or Sannan; `docs/plans/reconciliation.json` and `git log` show it). Do not rebuild what exists.
2. Work on a branch of your own (`myt1x54/<step>`, `sannan47/<step>`, `ruhan/<step>`), never on `main`.
3. Unity: Editor 6000.5.10f1; open the project with **ProcSense > Open Patient Bay**; never run *Rebuild Prototype Scenes* to open the project (it replaces authored layouts). Scene, prefab and asset changes go through the running Editor (the Unity CLI and the Pipeline package), not by editing `.unity` files as text; keep every `.meta` file; assets over 10 MB go to Git LFS. The Editor adds a WebGL entry to `Assets/XR/Settings/OpenXR Package Settings.asset` when it opens; do not commit it.
4. Blender: 4.5.14 LTS (`task blender-doctor`); equipment is built by the recipes in `tools/blender` (`uv run --project tools tools/blender/run.py all <asset>`), rendered for review, then imported with the Equipment menu in Unity. No downloaded models.
5. Tests: `task ci`, and the Unity EditMode and PlayMode tests for Unity work (in the open Editor through its Test Runner, or `task unity-test` with the Editor closed). Add a test with every behaviour.
6. Look at the result: a screenshot or a render from inside the room for every visual change; on the Quest when the step needs the headset (section 5).
7. Explain the step to the team, get the approval, record it in the decision log (`docs/context/ProcSense_Change_Report.md`, section G.1), tick the step file, run `task plans-index`, then merge to `main` (the pre-push hook runs the full checks). Commit messages carry no co-author lines (a commit-msg hook refuses them).

## 5. Front-end testing on the Meta Quest 3

The headset checks are listed with their steps and pass marks on [SANNAN_EXTERNAL_TESTING.md](../../testing/SANNAN_EXTERNAL_TESTING.md): DC-01 to DC-11 (section 3) and the Unity, 3D and VR acceptance items UX-P0-* (section 4). The team has one Quest, so the holder runs these; every wave 1 step ends with a short check on the Quest before it is approved. Figures still to record: the upload test with the network dropped (DT-236).
