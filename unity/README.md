# unity/

Unity 6000.5.10f1 (URP) project for Meta Quest 2/3, using OpenXR and XR Interaction Toolkit.
Add this `unity` directory in Unity Hub, keeping it inside the repository: the shared simulation engine is a local package at `../src/ProcSense.Core`.

Use the repository's pinned Editor and committed package manifest/lock file. Unity Pipeline is included for live Editor tooling; the application scripts do not require its APIs. Core package metadata is versioned to preserve Unity GUIDs across team checkouts. The setup commit also retains the sample-prefab serialization and URP settings used by the verified local Editor. A fresh-Library reproduction check has not yet been completed; Editor tests on the working checkout do not establish clean-machine or Quest acceptance.

## Open and edit

Use **ProcSense → Open Patient Bay**. It opens the visible `PatientBay` scene together with `Sys_Core` (simulation services) and `DevOnly` (Editor XR simulator), and positions the Scene view inside the room.

The **Scene** tab shows the editable room. The **Game** tab shows the running camera view after pressing **Play**. Play uses Unity's normal behaviour and runs the currently open scenes. `Boot` remains the first scene in application builds. Double-clicking PatientBay, Corridor or OT and pressing Play automatically loads missing Sys_Core services and the Editor simulator, with one player rig and camera. Use the workspace menu when you want the supporting scenes visible during editing too.

## Asset folders

Procedural equipment pilots are under `Assets/Models/Equipment`, `Assets/Materials/Equipment` and `Assets/Prefabs/Equipment`. Use **ProcSense → Equipment → Open Pilot Review** for the separate review scene. Build/import instructions, provisional dimensions and remaining interaction limits are in [the Blender pipeline README](../tools/blender/README.md).

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

Physical monitoring is now installed beside the bed. Grab the probe or cuff from the sensor tray and release it at the matching finger/arm placeholder. The probe reveals live permitted readings while attached. Press **START BP** on the monitor to request a timed BP cycle; removing the cuff cancels an unfinished cycle. Tablet attach/remove buttons remain synchronized with the props. The arm/finger geometry is a temporary stand-in, not the final patient character. See the [sensor workflow and remaining limits](../tools/blender/README.md#bedside-sensor-extension).

In Play mode, focus the Game view and use the XR Device Simulator's normal FPS mode (no individual device selected). WASD now moves the application rig along the floor at 1.5 m/s through XRI's collision-constrained body transformer. Walls, the bed and stands block walking; looking up/down does not make walking fly. The application capsule uses a 0.25 m radius, 0.025 m skin and 0.2 m step limit. Controller pose manipulation remains available for interaction testing. Individual device pose manipulation is a developer tool and does not model physical collision-safe walking; return to FPS mode for navigation. The adapter lives in `DevOnly` and disables itself outside the Editor.

The current room and UI remain prototypes. Final visual quality, animations, seated comfort and Quest 3 performance require their own verification and review; Editor checks do not close the headset gate.

## Teleport and comfort controls

Blue pads are teleport destinations at the bay tablet, bedside and exit, and at the OT station/exit. The corridor floor permits teleporting within clear supported space. Controller teleport rays use the Starter Assets teleport input; aim at a pad or the corridor floor and release/select to travel. Invalid targets cannot be selected. A short fade covers movement, and destinations are rechecked for obstruction before relocation. Teleports and room transitions cannot overlap.

Controller locomotion defaults to teleport and 45° snap-turn; 180° turnaround and smooth-turn/movement actions are disabled. Desktop collision-safe WASD walking is retained. With Game focused, **F7** cycles the current room's pads, **[ / ]** turn left/right, and **F6** toggles seated calibration. The **MOVEMENT** panel in each room offers **Calibrate seated / Use standing height** and **30° / 45°** snap-turn selection through mouse or controller UI rays. Sit first, then calibrate: it adjusts the camera/controller floor offset to a virtual eye height of 1.6 m, without moving the clinical patient or changing Core state. Returning to standing restores the previous offset; settings survive room navigation. Recalibrate after changing physical posture or tracking setup.

These are Editor-verified foundations. The headset comfort comparison and questionnaire with at least three testers, seated reach/readability and Quest frame-time measurements remain pending device access.

## Room navigation

`Sys_Core` owns the one Session, EventSystem, PlayerRig and RoomLoader. It remains loaded while room scenes are loaded/unloaded additively. `DevOnly` remains available in the Editor. A fresh Boot load starts a new session; room navigation preserves it. The bay has an **Enter** panel on its right wall marked **CORRIDOR**. In the corridor, **PATIENT BAY** is on the left near the arrival end and **OPERATING THEATRE** is on the right at the far end. The OT return panel is on its front wall. Click Enter with the mouse or controller UI ray. Use WASD to approach panels.

Each transition fades to black over 0.3 seconds, validates a room arrival anchor, places the same rig and unloads the previous room, then fades back in. Movement and repeated requests are held during the transition. Missing/blocked arrivals keep the previous room and show a message on its panels. CT/MRI and ultrasound doors show **No trainee access** and stay blocked by solid walls. These are trainee navigation controls; they issue no clinical action and do not transfer the patient. The OT table is a greybox placeholder.

## Editing and regeneration

Ordinary Inspector and Scene edits work normally. Save scene edits with Ctrl+S. Changes made during Play are normally temporary.

**ProcSense → Tools → Rebuild Prototype Scenes** is a separate, destructive regeneration command with a confirmation dialog: it replaces the four prototype scenes from `Assets/Data/room_manifest_bay.json`. It is not needed to open or run the project. Put layout changes in the manifest if they must survive regeneration, or keep them as saved scene edits and do not regenerate those scenes. Prototype UI panels are constructed by runtime view scripts, so they appear during Play rather than as fully authored Canvas hierarchies in Edit mode.

## Interaction input and tablet fallbacks

World selections and UI clicks use a 0.35-second debounce by default. Input before a Session exists does not consume that cooldown, and a replacement Session starts fresh. Core still decides whether an action is accepted.

The tablet's **More** tab follows currently enabled emitters with fallback labels. Disabling, destroying or unloading an emitter removes its extra button; re-enabling it restores the button. Reconfiguring its label/action updates the tab. Equivalent actions share one extra button while at least one source remains, using the complete verb, target, channel and parameters for comparison. Built-in Identity/History/Observe controls stay available through room changes and are not duplicated in More.

Dynamic fallback clicks resolve a live source at click time and use that source's debounce. A stale button cannot send an old action after its source is removed or reconfigured. The `TabletView.Press` helper is deterministic test/desktop dispatch; it temporarily bypasses a built-in button's cooldown and restores it afterward. Normal pointer/controller clicks retain UI gating and debounce.

`XRSelectEmitter` supports an existing XRSimpleInteractable or XRGrabInteractable without adding a second interactable. Its **Route** selects the input:

- **Any** preserves existing bay selection proxies.
- **Grab** accepts hand/ray selection of a grabbable prop, excluding socket and poke sources.
- **Socket** accepts only the assigned **Required Socket**. A missing or different socket emits nothing.
- **Poke** accepts XRPokeInteractor world-object selection. XRI poke filters/colliders and physical reach still need to be configured on the final equipment.

Add/configure the XRI interactable before adding the emitter. Configure the existing Core verb, target and parameters on the emitter and a fallback label as needed. Selection sends one debounced request; release alone sends no clinical action. Equipment-specific detach/administration behavior must be wired explicitly through Core rather than inferred from dropping or animating a prop. The current saved bay remains greybox selection proxies; adapter tests use real XRI manager selection with grab/socket/poke types. Final equipment integration and physical headset acceptance remain later work.

## Diagnostics development preview

In Play mode, choose **ProcSense > Development > Diagnostics Preview (Play Mode)**. Use Patient trip or Lab sample, then the **Inject** buttons to review the proposed status/report presentation. Inject an attention stop during processing: progression pauses until Resume fixture. Reset or switch route to clear the synthetic report. Close preview or stop Play to remove it. This is an Editor-only fixture, not an operational investigation screen; no Core state, time or log is changed. See [the contract handoff](diagnostics-contract-handoff.md) for the missing runtime dependencies.

## Validation and work log

Run `task unity-test` from the repository root with this project's Editor closed (or use the live Editor Test Runner). `task unity-build` builds the Quest APK when Android modules are installed.

Sannan's assignments, verification evidence and approval rules are in [sannan-execution-plan.md](sannan-execution-plan.md). Every implementation requires self-verification, Sannan's manual review and separate approval of the exact commit message before committing.

## Physical notes folder

In PatientBay, grab the folder on the notes table and press the held object's Activate/trigger to open or close it. Pickup alone reveals nothing. Accepted opening shows the current permitted name, DOB and MRN on the page; reopening adds no duplicate read action. Tablet Open notes remains an accessibility fallback and opens this same folder. A new session closes it and clears the visible facts. Multi-page records/report insertion and physical hand-driven cover dragging remain later work.

## Permanent MPFB patient

PatientBay now contains the MPFB patient for ordinary app launch, corridor return and builds. Use **ProcSense -> Open Application**, then Play, to sign in and enter this bay. **ProcSense -> Open Patient Bay** and **Characters -> Open MPFB Patient** open the editable main bay directly for development.

The CGTrader model, texture and material are retired from Unity. Its procedural wristband was moved into MPFBPatient with its GUID preserved. The fitted gown, blanket, pillow, mattress, rig and native facial shapes are the permanent patient setup. Sensor sockets/wristband remain bone mounted; clinical animation, audio and demographic binding remain separate work. The old MPFBPatientReview scene is retained only as a development snapshot and is not the app destination or a build scene. Recipes and provenance are recorded in [the MPFB recipe README](Tools/MPFB/README.md).

## Sign-in with the ProcSense service (14 October 2026)

The login screen now signs in real accounts. Typing an email and password does this, in `Assets/Scripts/Auth/AuthService.cs`: it asks the service (`/config.json`, which also wakes the free host), sends the password once, straight to the account system (Supabase Auth), keeps the access token in memory only, reads the person and organisation (`/me`) and the privacy question (`/privacy/consent`), and only then opens the workspace. The password is cleared after sign-in and never stored or logged. Messages on the form: wrong password, no connection, no organisation, and "open the web page and accept the privacy choices first" (the headset cannot answer that question for anyone).

- **The service address** is the build setting `Assets/Resources/ProcSenseService.asset` (**ProcSense -> Service settings (create or select)**). It is `https://procsense.onrender.com`; https only (http only for localhost). Nothing secret is in it.
- **The fixed demo account still works**, offline, and sends nothing to the service (its sessions stay on the device). It is the setting `Allow Offline Demo Account` in `Assets/Settings/DemoLoginSettings.asset`: switch it off for a release build, and the demo account is then refused.
- `AuthService.Current` holds the signed-in person (pseudonym, organisation, role) for the upload client and the results panel that come next; `AuthService.Authorized(...)` makes a call with the token, renews it on a 401 and signs the person out if renewing is refused.
- Tests: 12 in `Assets/Tests/EditMode/AuthServiceTests.cs` (a scripted service, no network) and 5 in `Assets/Tests/PlayMode/LoginServiceTests.cs` (the screen), run in batch mode with Unity 6000.5.10f1; Sannan's two demo-login tests still pass. Not done: the device-code view, the encrypted token store in the Android keystore and a remembered login for offline starts, and a check of the keyboard and reach on a Quest.

## Demo launch login

The app's Boot scene shows a demo sign-in screen before loading the clinical workspace. Backend authentication remains a teammate TODO; this gate verifies only the displayed fixed demo account:

- Email: `demo@procsense.app`
- Password: `ProcSenseDemo!`

Use **ProcSense -> Development -> Preview login** to run the screen once. **ProcSense -> Development -> Bypass login in Editor** is unchecked by default, so normal Boot launches require signing in. Check it when you want to skip login during development. This preference is local to your Editor. Directly opening PatientBay still uses normal room Play behavior for development.

`Assets/Settings/DemoLoginSettings.asset` controls Show On Launch and the optional Development Build bypass. Regular builds show login when enabled; the Editor bypass does not disable login in a player build. Login creates one temporary rig/input system and destroys it before loading Sys_Core and the permanent MPFB PatientBay. No clinical session runs behind the form. Invalid/empty inputs stay on the form; the entered password is cleared on success and never logged or saved. The form supports mouse/keyboard and is presented as a tracked world-space panel when XR is active; Quest keyboard/reach acceptance needs device testing.

The authored Canvas/TMP prefab is `Assets/Prefabs/UI/LoginScreen.prefab`. The builder preserves existing UI and refuses to replace an authored login prefab. The backend owner's integration point is `Assets/Scripts/Login/DemoLoginView.cs` Submit; replace the explicit demo credential check with the real authentication service and complete the launch only after service acceptance.

The old floating wristband card is disabled in the main bay and retained MPFB snapshot. Read identity from the fitted physical band; controller selection still issues Core's read_wristband action. DevOnly suppresses vibration feedback only while virtual controllers exist, while real-device haptic settings remain unchanged. The monitor grab and START BP control own separate collider lists.
