# unity/

Unity 6000.5.10f1 (URP) project for Meta Quest 2/3, using OpenXR and XR Interaction Toolkit.
Add this `unity` directory in Unity Hub, keeping it inside the repository: the shared simulation engine is a local package at `../src/ProcSense.Core`.

## Open and edit

Use **ProcSense → Open Patient Bay**. It opens the visible `PatientBay` scene together with `Sys_Core` (simulation services) and `DevOnly` (Editor XR simulator), and positions the Scene view inside the room.

The **Scene** tab shows the editable room. The **Game** tab shows the running camera view after pressing **Play**. Play uses Unity's normal behaviour and runs the currently open scenes; the workspace includes the simulation services and XR simulator it needs. `Boot` remains the first scene in application builds and loads these scenes at runtime. You can also double-click `Assets/Scenes/PatientBay.unity` to inspect the room by itself; use the menu to load its supporting scenes before playing.

## Asset folders

| Folder | Contents |
|---|---|
| `Assets/Scenes` | Boot, Sys_Core, PatientBay and DevOnly |
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

## Editing and regeneration

Ordinary Inspector and Scene edits work normally. Save scene edits with Ctrl+S. Changes made during Play are normally temporary.

**ProcSense → Tools → Rebuild Prototype Scenes** is a separate, destructive regeneration command with a confirmation dialog: it replaces the four prototype scenes from `Assets/Data/room_manifest_bay.json`. It is not needed to open or run the project. Put layout changes in the manifest if they must survive regeneration, or keep them as saved scene edits and do not regenerate those scenes. Prototype UI panels are constructed by runtime view scripts, so they appear during Play rather than as fully authored Canvas hierarchies in Edit mode.

## Validation and work log

Run `task unity-test` from the repository root with this project's Editor closed (or use the live Editor Test Runner). `task unity-build` builds the Quest APK when Android modules are installed.

Sannan's assignments, verification evidence and approval rules are in [sannan-execution-plan.md](sannan-execution-plan.md). Every implementation requires self-verification, Sannan's manual review and separate approval of the exact commit message before committing.
