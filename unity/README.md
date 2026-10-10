# unity/

Unity 6000.5.10f1 (URP) project for Meta Quest 2/3, using OpenXR and XR Interaction Toolkit.
Add this `unity` directory in Unity Hub, keeping it inside the repository: the shared simulation engine is a local package at `../src/ProcSense.Core`.

## Open and edit

Use **ProcSense → Open Patient Bay**. It opens the visible `PatientBay` scene together with `Sys_Core` (simulation services) and `DevOnly` (Editor XR simulator), and positions the Scene view inside the room.

The **Scene** tab shows the editable room. The **Game** tab shows the running camera view after pressing **Play**. Play uses Unity's normal behaviour and runs the currently open scenes. `Boot` remains the first scene in application builds. Double-clicking PatientBay, Corridor or OT and pressing Play automatically loads missing Sys_Core services and the Editor simulator, with one player rig and camera. Use the workspace menu when you want the supporting scenes visible during editing too.

## Asset folders

| Folder | Contents |
|---|---|
| `Assets/Scenes` | Boot, Sys_Core, PatientBay, Corridor, OT and DevOnly |
| `Assets/Scripts` | Runtime bridge, interactions, UI and content access |
| `Assets/Prefabs/XR` | ProcSense PlayerRig variant with controller visual feedback wired |
| `Assets/Editor` | Editor workspace, scene generation and project/build tools |
| `Assets/Data` | Room manifest and initial session configuration |
| `Assets/Materials/Prototype` | Greybox room/equipment materials |
| `Assets/Settings` | Quest render-pipeline settings |
| `Assets/Tests` | EditMode and PlayMode tests |
| `Assets/Samples`, `TextMesh Pro`, `XR`, `XRI` | Imported package samples and resources/settings |
| `Assets/StreamingAssets` | Build-time copy of the medical content |

Create `Models`, `Textures`, `Audio` and `Animations` folders as their first assets are added. `PlayerRig` is a prefab variant of the imported toolkit rig, so application-specific wiring lives outside third-party samples. The missing sample controller FBX has been restored from the installed toolkit with its original GUID metadata.

## Desktop movement

In Play mode, focus the Game view and use the XR Device Simulator's normal FPS mode (no individual device selected). WASD now moves the application rig along the floor at 1.5 m/s through XRI's collision-constrained body transformer. Walls, the bed and stands block walking; looking up/down does not make walking fly. The application capsule uses a 0.25 m radius, 0.025 m skin and 0.2 m step limit. Controller pose manipulation remains available for interaction testing. Individual device pose manipulation is a developer tool and does not model physical collision-safe walking; return to FPS mode for navigation. The adapter lives in `DevOnly` and disables itself outside the Editor.

The current room and UI remain prototypes. Final visual quality, animations, seated comfort and Quest 3 performance require their own verification and review; Editor checks do not close the headset gate.

## Room navigation

`Sys_Core` owns the one Session, EventSystem, PlayerRig and RoomLoader. It remains loaded while room scenes are loaded/unloaded additively. `DevOnly` remains available in the Editor. A fresh Boot load starts a new session; room navigation preserves it. The bay has an **Enter** panel on its right wall marked **CORRIDOR**. In the corridor, **PATIENT BAY** is on the left near the arrival end and **OPERATING THEATRE** is on the right at the far end. The OT return panel is on its front wall. Click Enter with the mouse or controller UI ray. Use WASD to approach panels.

Each transition fades to black over 0.3 seconds, validates a room arrival anchor, places the same rig and unloads the previous room, then fades back in. Movement and repeated requests are held during the transition. Missing/blocked arrivals keep the previous room and show a message on its panels. CT/MRI and ultrasound doors show **No trainee access** and stay blocked by solid walls. These are trainee navigation controls; they issue no clinical action and do not transfer the patient. The OT table is a greybox placeholder.

## Editing and regeneration

Ordinary Inspector and Scene edits work normally. Save scene edits with Ctrl+S. Changes made during Play are normally temporary.

**ProcSense → Tools → Rebuild Prototype Scenes** is a separate, destructive regeneration command with a confirmation dialog: it replaces the four prototype scenes from `Assets/Data/room_manifest_bay.json`. It is not needed to open or run the project. Put layout changes in the manifest if they must survive regeneration, or keep them as saved scene edits and do not regenerate those scenes. Prototype UI panels are constructed by runtime view scripts, so they appear during Play rather than as fully authored Canvas hierarchies in Edit mode.

## Validation and work log

Run `task unity-test` from the repository root with this project's Editor closed (or use the live Editor Test Runner). `task unity-build` builds the Quest APK when Android modules are installed.

Sannan's assignments, verification evidence and approval rules are in [sannan-execution-plan.md](sannan-execution-plan.md). Every implementation requires self-verification, Sannan's manual review and separate approval of the exact commit message before committing.
