# unity/

Unity 6000.5.10f1 (URP) project for Meta Quest 2/3, using OpenXR and XR Interaction Toolkit.
Add this `unity` directory in Unity Hub, keeping it inside the repository: the shared simulation engine is a local package at `../src/ProcSense.Core`.

Use the repository's pinned Editor and committed package manifest/lock file. Unity Pipeline is included for live Editor tooling; the application scripts do not require its APIs. Core package metadata is versioned to preserve Unity GUIDs across team checkouts. The setup commit also retains the sample-prefab serialization and URP settings used by the verified local Editor. A fresh-Library reproduction check has not yet been completed; Editor tests on the working checkout do not establish clean-machine or Quest acceptance.

## Unity tests

With the Editor open, use **Window > General > Test Runner** and run both tabs. Close the Editor before running `task unity-test` from the repository root: the batch runner needs exclusive access to this project and writes `artifacts/unity/EditMode.xml` and `PlayMode.xml` plus logs. It retains graphics support for snapshot tests; no headset is required.

Normal batch startup excludes `DevOnly`. Simulator-dependent foundation, room-transition and wristband tests explicitly load it through `EditorXRTestEnvironment`, so the same input assertions run in batch and interactive Editor tests. This fixture is test-only; it does not enable developer simulation in application builds. These checks do not replace actual headset acceptance. The real-service PlayMode test requires its E2E environment credentials and is skipped when they are absent.

`CoreInUnityTests` uses the same case-file/identity-fixture content set and checked-in golden expectations as the CLI. It never regenerates expected data during a test, and verifies that changing the canonical log hash is rejected.

The simulator test fixture supplies missing keyboard/mouse devices and temporarily routes input without window focus in batch and interactive tests. Teardown unloads its simulator, removes only its own devices and restores the original input configuration. It uses a runtime settings clone; project input settings remain unchanged.

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

### Quest 3 in Editor Play

Launch Quest Link or Air Link inside the headset before pressing Play in Unity. The Meta Horizon Link PC app must be running and selected as the active OpenXR runtime. A USB/ADB connection by itself does not supply PC VR tracking. Editor Play uses the Standalone OpenXR settings even with Android selected; Oculus Touch and Quest Touch Plus controller profiles are enabled there.

Choose **ProcSense > Development > Play input > Quest headset and controllers**, **Keyboard and mouse**, or **Automatic detection** before Play. The choices are remembered locally per checkout. Headset mode excludes the simulator even if Link is unavailable; keyboard/mouse mode temporarily prevents desktop XR startup, so a connected headset cannot take over. Automatic mode uses XR when active and otherwise falls back to simulation. The original desktop XR initialization flag is restored after Play, without saving project settings. These choices do not change standalone APK input.

Choose **ProcSense > Development > Movement > Thumbsticks - left move, right snap turn** for small spaces: left stick moves at 1.5 m/s relative to head direction; right stick snap-turns 45 degrees. Collision and existing transition freezes remain active. **Room-scale and teleport** restores teleport locomotion instead of continuous stick movement. Head/hand tracking stays active in both modes. Movement is disabled on the temporary login rig. Choices apply on the next Play and cannot be switched mid-session. Keyboard/mouse FPS movement remains available in simulator mode. A standalone APK retains its existing Android XR/locomotion configuration.

Physical monitoring is now installed beside the bed. Grab the probe or cuff from the sensor tray and release it at the matching finger/arm placeholder. The probe reveals live permitted readings while attached. Press **START BP** on the monitor to request a timed BP cycle; removing the cuff cancels an unfinished cycle. Tablet attach/remove buttons remain synchronized with the props. The arm/finger geometry is a temporary stand-in, not the final patient character. See the [sensor workflow and remaining limits](../tools/blender/README.md#bedside-sensor-extension).

In Play mode, focus the Game view and use the XR Device Simulator's normal FPS mode (no individual device selected). WASD now moves the application rig along the floor at 1.5 m/s through XRI's collision-constrained body transformer. Walls, the bed and stands block walking; looking up/down does not make walking fly. The application capsule uses a 0.25 m radius, 0.025 m skin and 0.2 m step limit. Controller pose manipulation remains available for interaction testing. Individual device pose manipulation is a developer tool and does not model physical collision-safe walking; return to FPS mode for navigation. The adapter lives in `DevOnly` and disables itself outside the Editor.

The current room and UI remain prototypes. Final visual quality, animations, seated comfort and Quest 3 performance require their own verification and review; Editor checks do not close the headset gate.

## Teleport and comfort controls

Blue pads are teleport destinations at the bay tablet, bedside and exit, and at the OT station/exit. The corridor floor permits teleporting within clear supported space. Controller teleport rays use the Starter Assets teleport input; aim at a pad or the corridor floor and release/select to travel. Invalid targets cannot be selected. A short fade covers movement, and destinations are rechecked for obstruction before relocation. Teleports and room transitions cannot overlap.

Controller locomotion defaults to teleport and 45° snap-turn; 180° turnaround and smooth-turn/movement actions are disabled. Desktop collision-safe WASD walking is retained. With Game focused, **F7** cycles the current room's pads, **[ / ]** turn left/right, and **F6** toggles seated calibration. The prototype **MOVEMENT** displays have been removed from the rooms. The underlying calibration/30° or 45° configuration APIs remain; headset users currently keep the default 45° turn and standing offset until a dedicated settings surface is provided. Sit first before Editor calibration: it adjusts the camera/controller floor offset to a virtual eye height of 1.6 m, without moving the clinical patient or changing Core state. Returning to standing restores the previous offset; settings survive room navigation. Recalibrate after changing physical posture or tracking setup.

These are Editor-verified foundations. The headset comfort comparison and questionnaire with at least three testers, seated reach/readability and Quest frame-time measurements remain pending device access.

## Room navigation

`Sys_Core` owns the one Session, EventSystem, PlayerRig and RoomLoader. It remains loaded while room scenes are loaded/unloaded additively. `DevOnly` remains available in the Editor. A fresh Boot load starts a new session; room navigation preserves it. The bay has a framed door on its right wall marked **CORRIDOR**. In the corridor, **PATIENT BAY** is on the left near the arrival end and **OPERATING THEATRE** is on the right at the far end. The OT return door is on its front wall. Click the door's smaller **Enter** control with the mouse or controller UI ray. Use WASD to approach doors. Door leaves, frames, frosted observation insets, hardware and fixed signs are visual dressing over the existing solid shells; room travel still uses the fade transition.

Each transition fades to black over 0.3 seconds, validates a room arrival anchor, places the same rig and unloads the previous room, then fades back in. Movement and repeated requests are held during the transition. Missing/blocked arrivals keep the previous room and show a message on its panels. CT/MRI and ultrasound doors show **No trainee access** and stay blocked by solid walls. These are trainee navigation controls; they issue no clinical action and do not transfer the patient. The OT has a segmented padded table, overhead lamp, anaesthesia presentation unit, Mayo stand, trolley and supply cupboard. These new furniture props are static presentation assets; clinical controls/readiness remain separate work.

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

## Upload to the service (14 October 2026)

A finished session goes to the hosted service by itself (`Assets/Scripts/Sync/`). `SimulationHost` gives every session an id when it starts and saves it with the version 2 header (session id, content version, engine version): the file name holds the id, so saving again replaces the same file. `EndSession()` (called when the app closes, and callable from a "finish session" control) saves it, and queues it for the person who is signed in. A session ended while nobody is signed in stays on the device and is never queued (it has nobody to belong to), and a queued session is only ever sent as the person it was played under.

`SessionUploader` (one object that lives for the whole run) tries the queue about every 30 seconds, and at once when a session ends or someone signs in; it never blocks a frame. The queue is the file `outbox.json` under the application's data folder, so it survives closing the app. An entry leaves it only after the service confirms (`GET /sessions/{id}`) that it has the session. Answers: 200 or 202 stored; no connection, 429 and 5xx retry after 15 s, 30 s, 1 min ... up to 10 min; `consent_required` retry later (the person agrees on the web page); 401 waits for sign-in; 409, 413 and 422 are set aside ("needs attention") and not retried. `SessionSync.Describe()` gives one line for a status panel.

**Content version.** The service can only verify a session whose content version it holds. The Editor and the build script read the whole `content/` folder, whose version is the first 12 characters of its hash; the service's image holds that content too (as well as the practice and assessment packages), so these sessions verify. A case that is not approved for the session's mode is still refused. A release build that ships a packaged mode (`tools/content/package.py`) writes that package's version, which the image also holds.

Tests: 17 in `Assets/Tests/EditMode/SessionSyncTests.cs` (the queue and the sender against a scripted service) and 5 in `Assets/Tests/PlayMode/SessionSaveTests.cs` (the saved header replays to its own hash in the engine's verifier, one file per session, queued once for the right person). Not done: a "finish session" control and the panel that shows the status and the person's last results; a check on a Quest.

## The tablet's Session tab (14 October 2026)

The hand-held tablet has a fifth tab, **Session** (`Assets/Scripts/Views/SessionTabView.cs`, built by `TabletView`). It shows who is signed in (or that the person is practising offline), the upload status line (`SessionSync.Describe()`), a **Finish session and upload** button (ends the session: saves it and queues it for the signed-in person; it says where the session went), a **Refresh my results** button, and **My latest sessions on the service**: the person's last five sessions fetched from the service, one line each (date, case, "Success, score 2 (provisional)", "waiting to be checked", "could not be checked"). While a session is waiting to be checked the list refreshes every 8 seconds, and it refreshes whenever the tab is opened. It never changes the simulation and its buttons are not simulation buttons, so the tablet fallback list is unchanged.

The test `Assets/Tests/PlayMode/TabletSnapshotTests.cs` draws the tablet's tabs to `artifacts/unity/tablet-identity.png` and `tablet-session.png` (git-ignored), so the layout can be looked at without a headset or the Editor.

**Trying it in the Editor without controllers:** while the application is playing, **ProcSense -> Development -> Show tablet Session tab (Play Mode)** switches the tablet to the Session tab and frames it in the Scene view, and **ProcSense -> Development -> Finish session and upload (Play Mode)** does what the tab's Finish button does. Walk to the tablet (the blue pad "TELEPORT_Tablet", or WASD) to see it in the Game view.

A session can be finished once; to play again, restart the app. The scores shown are provisional (the weights are), and the tab says so.

**Trying the whole chain from a laptop without a headset:** the test `Assets/Tests/PlayMode/LiveServiceTests.cs` signs in, plays a little, ends the session, uploads it, waits for the service's engine to verify it and reads the result back, over a real network connection with the real `UnityWebRequest`. Set `PROCSENSE_E2E_URL`, `PROCSENSE_E2E_EMAIL` and `PROCSENSE_E2E_PASSWORD` (a local copy of the service with sample people comes from `python -m server.tests.browser_stack`; the live service works with a real account that has agreed to the privacy choices) and run it from the Test Runner or in batch mode; without them it is skipped.

## Demo launch login

The app's Boot scene shows a demo sign-in screen before loading the clinical workspace. Backend authentication remains a teammate TODO; this gate verifies only the displayed fixed demo account:

- Email: `demo@procsense.app`
- Password: `ProcSenseDemo!`

Use **ProcSense -> Development -> Preview login** to run the screen once. **ProcSense -> Development -> Bypass login in Editor** is unchecked by default, so normal Boot launches require signing in. Check it when you want to skip login during development. This preference is local to your Editor. Directly opening PatientBay still uses normal room Play behavior for development.

`Assets/Settings/DemoLoginSettings.asset` controls Show On Launch and the optional Development Build bypass. Regular builds show login when enabled; the Editor bypass does not disable login in a player build. Login creates one temporary rig/input system and destroys it before loading Sys_Core and the permanent MPFB PatientBay. No clinical session runs behind the form. Invalid/empty inputs stay on the form; the entered password is cleared on success and never logged or saved. The form supports mouse/keyboard and is presented as a tracked world-space panel when XR is active; Quest keyboard/reach acceptance needs device testing.

The authored Canvas/TMP prefab is `Assets/Prefabs/UI/LoginScreen.prefab`. The builder preserves existing UI and refuses to replace an authored login prefab. The backend owner's integration point is `Assets/Scripts/Login/DemoLoginView.cs` Submit; replace the explicit demo credential check with the real authentication service and complete the launch only after service acceptance.

The old floating wristband card is disabled in the main bay and retained MPFB snapshot. Read identity from the fitted physical band; controller selection still issues Core's read_wristband action. DevOnly suppresses vibration feedback only while virtual controllers exist, while real-device haptic settings remain unchanged. The monitor grab and START BP control own separate collider lists.

## Login validation and errors

Email validation rejects missing/malformed addresses, internal whitespace/control characters, invalid domain labels and addresses longer than 254 characters. Surrounding email whitespace is normalized and the demo account comparison remains case-insensitive. Passwords must be present, contain no control characters and fit the 128-character input limit; they are never silently trimmed and login does not invent signup password-strength rules.

Errors appear beneath the relevant field with a red border, focus moves to the first invalid input, and errors clear as the field is corrected. A syntactically valid but incorrect demo account shows a separate credentials message. Repeated successful submissions remain ignored; fields lock and the entered password clears before workspace entry. Incomplete login UI references show a safe unavailable message, disable the form and keep the workspace locked rather than throwing repeated null-reference errors. Real service/network failures remain part of the backend authentication TODO.

## Sensor placement guides

Picking up the O2 probe or BP cuff shows a translucent bubble at its matching patient target and a blue return bubble on the sensor tray. The target turns green when the held item is close enough to place. Release at the patient target to attach; lift away to remove. Release near the tray bubble to snap the item back to its original resting pose. The guides disappear when the item is released. Returning equipment to the tray sends no clinical action. Existing tablet attach/remove controls remain available.

Use ProcSense > Equipment > Configure Sensor Placement Guides to reapply the guide material and capture radii to the existing saved PatientBay; it does not rebuild the room. Current capture radii are 5.5 cm for the finger target and 8.5 cm for the cuff target; tray returns use 6.5 cm and 9 cm respectively. Headset reach/readability still requires a physical check.

## Dropping equipment and padded BP cuff

Grabbable equipment now uses gravity when released outside a patient/tray target. XRI keeps held and socketed items stable, then restores dynamic physics on release. Speculative continuous collision detection supports both states. Release does not add a throw impulse; items fall naturally and collide with room surfaces. Returning a sensor to its tray clears old velocity before placing it, so it settles rather than drifting away. Attached sensors remain on the patient and retain the same clinical action flow.

The loose cuff is now a curved, padded fabric piece with bound edges, a fastening patch, retaining loop and hose connector. Its fitted wrapped state and original patient anchors are retained. Generate/import instructions are in Tools/Equipment/README.md. This is a rigid presentation model, not cloth or inflation simulation.

## Room furniture and privacy

PatientBay now has a wheeled bed frame, head/foot boards and lowered foot rails
around the existing MPFB patient, soft mattress and bedding. A gathered privacy
curtain is parked along the left side of an overhead L track, leaving the patient
and bedside sensor access visible. Handrub and pedal-bin fixtures dress the bay
and OT. The corridor has a waiting bench and linen trolley along the right wall,
with the centre route, room arrival points and teleport destinations preserved.
The curtain and new theatre equipment are static presentation props.

All three rooms reuse the existing baked lighting and one real-time key each.
Simple furniture colliders block walking and support dropped items where
appropriate. The original bed/table collision identities remain. The sensor
cables have empty initial geometry in Edit mode and receive their normal thin
17-point shapes during Play. See [environment reproduction notes](Tools/Environment/README.md).

Review in Play: walk around both sides of the bed and inspect the feet/headboard;
grab and return the cuff/probe, including their patient targets. Travel through
the corridor to OT and back; test the teleport pads and Enter controls. Expect
clear destinations and solid furniture, with diagnostic doors still blocked.
Actual Quest reach, appearance and frame timing require headset review.

## Clinical room fixtures and finishes

The bay has a handwashing cabinet and basin, glove/towel dispensers, wall sharps
container, standing charting counter with inactive computer, visitor chair,
wall protection and a decorative clock. Sage washable lower panels, a muted
wood-effect floor and subtle woven fabric normals refine the existing room.
The corridor reuses the clock/protective rails and muted flooring. OT is
unchanged. These are static presentation fixtures; the tablet remains the
interactive client and the clock is not a simulation timer.

The chair uses separate seat/back/leg colliders and the sink retains a recessed
basin collision shape. Review dropped-item contact, walking clearance, bedside
cuff/probe access and room navigation in Play. The new 256px fabric normal is
mipmapped with an Android ASTC override. Actual Quest appearance, comfort and
frame timing remain device-review work.

The bedside sensor surface now has a compact wheeled equipment table with
stainless legs and a lower shelf. The original floating pad is hidden and its
collider disabled; the table supplies the full worktop/rim and frame collisions.
Cuff/probe return and patient attachment points keep their existing positions.

The MPFB patient now has clearer fitted torso breathing (up to 14mm chest lift),
blinks and gentle head/eye movement. The gown and opaque navy shorts follow
breathing; relaxed arms, wrists and fingers make small shifts with the fitted
sensors following their bones. Legs, ankles and toes wiggle in occasional
alternating bursts with quiet pauses. Shorts cover the pelvis from low foot-end
views. The blanket is
removed from the bay. Disable `PatientIdleView` on `MPFB patient` for a static pose.
Breathing is cosmetic pending a continuous Core respiratory presentation
contract, not a clinical RR measurement. See [patient idle notes](Tools/MPFB/README.md#fitted-patient-idle).
