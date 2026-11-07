# p3-02-main-menu-and-flow — Main menu, mode and procedure selection, loading, briefing and pause (MNU)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

After signing in, the player stands in a calm, lit space facing a floating glass menu with two large choices, **TRAINING MODE** and **TEST / EXAMINATION MODE**, and smaller **Settings** and **Quit**. Choosing a mode slides in the procedure cards: **Laparoscopic cholecystectomy (gallbladder removal)** and **Appendicectomy (appendix removal)**. Each card says plainly that the task is the **pre-operative preparation** of the patient, not the operation. Training also offers a level (beginner, intermediate, advanced). A short loading screen fades in, then a briefing card introduces the patient and the task, and the player starts in the bay. The pause menu (controller menu button) offers resume, settings, restart, return to menu and quit, each with a confirmation that says what happens to the session.

## 2. Current implementation status

- `MainMenuController` (Start, Quit, "Generated scenario (practice)") and `ModeChooserView` (Practice / Assessment) exist as flat canvases; `MainMenu.unity` is in the build list but is never loaded: the app starts at `Boot.unity`. **Not integrated.**
- Login (`Login/*`, `DemoLoginView`) is the first thing shown and works. **Verified (Editor).**
- No procedure selection, no level selection, no loading screen, no briefing, no pause menu. **Missing.**

## 3. Existing code, assets, scenes and systems to reuse

`MainMenuController`, `ModeChooserView`, `ModeChoice`, `GeneratedScenarioRequest`, `BootLoader` login sequence, `LoginPresentation` (rig freezing, camera background), `LocomotionFade`, `UiKit` (until P3-UIX replaces its look), `Difficulty.cs` levels, corridor reception assets for the menu backdrop.

## 4. Missing functionality

Menu as the entry point after login; procedure cards; level choice; scenario briefing; loading screen with real progress; pause menu; confirmations; menu music and transitions; "Resume previous session" entry (P3-SES-09).

## 5. Architecture and integrations

Screens are states of `GameFlowDirector` (P3-SES-05). Each screen is a prefab built from the P3-UIX kit and owned by a `ScreenPresenter` (show, hide, focus first button). Selections fill a `SessionRequest` (P3-SES-06); nothing clinical happens until the director starts the session. Scene order after the change: Boot (service shell) → Login → MainMenu (additive, lightweight backdrop) → unload MainMenu → Sys_Core + rooms → session.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-MNU-01 | Boot sequence: after login load `MainMenu` instead of the bay; the bay loads only when the director starts a session | Moiz | P3-SES-05 | Fresh launch on desktop and APK lands on the menu | Planned |
| P3-MNU-02 | Menu backdrop scene: lightweight lit space (reception-style, reuse corridor assets, own small bake, no characters required) | Sannan | P3-SES-01 | Menu scene renders under 1 ms extra GPU on desktop profiler and has no realtime shadows | Planned |
| P3-MNU-03 | Main menu screen: Training, Test / Examination, Settings, Quit, "Resume previous session" when one exists | Moiz | P3-UIX-04, P3-SES-05 | All buttons reachable by ray and by poke; keyboard/mouse in the Editor | Planned |
| P3-MNU-04 | Procedure selection: two cards (S1, S2) with the pre-operative wording, case count available in the chosen mode, disabled card with reason when the pool is empty | Moiz | P3-MNU-03, P3-SCN-02 | With D1 unresolved, Test shows "No cases are approved for examination yet" instead of failing | Planned |
| P3-MNU-05 | Level selection (training only): beginner, intermediate, advanced mapped to Core `TrainingLevel` | Moiz | P3-MNU-04, P3-SCN-04 | The level reaches the session header | Planned |
| P3-MNU-06 | Loading screen: fade to a glass card with progress from the async loads, a tip line, never longer than the load itself | Moiz | P3-SES-05, P3-UIX-04 | No black frames longer than 100 ms between fade and first room frame (desktop) | Planned |
| P3-MNU-07 | Briefing card: patient name, age, setting (ward or emergency department), reason for admission (trainee-visible facts only), the task statement; training adds the objective list, test does not | Moiz | P3-SCN-05 | Briefing never shows a hidden finding (test compares with the observable contract) | Planned |
| P3-MNU-08 | Pause menu on the controller menu button: Resume, Settings, Restart scenario, Return to main menu, Quit | Moiz | P3-SES-08, P3-SET-02 | Pause stops the engine clock (P3-SES-08) | Planned |
| P3-MNU-09 | Confirmation dialogs for restart, return and quit with the consequence (practice saved and uploaded; examination ends as abandoned) | Moiz | P3-MNU-08, P3-EXM-06 | Each path tested in PlayMode | Planned |
| P3-MNU-10 | Menu transitions and audio hooks (open, select, back, music start/stop) | Moiz | P3-SND-04, P3-ANI-09 | Hooks fire once per transition | Planned |
| P3-MNU-11 | Tests: PlayMode menu → selection → briefing → bay for S1 and S2, both modes | Moiz | P3-MNU-07, P3-MNU-09 | Green in CI | Planned |

## 7. Dependencies and execution order

P3-MNU-01 after the flow director; P3-MNU-02 (art) in parallel; screens after the UI kit; briefing after the scenario picker; pause after pause support.

## 8. Frontend components

Main menu panel, mode cards (large, icon, one-line explanation), procedure cards (image, title, "pre-operative preparation" subtitle, case count), level segmented control, loading card, briefing card, pause panel, confirmation dialog.

## 9. Backend and API changes

None. Generated scenarios use the existing `/scenario-packages/next` route through P3-SCN-06.

## 10. AI and voice integration

The menu is controller-driven; voice is not used in menus (avoids accidental commands). Voice mode settings are reachable from Settings.

## 11. Unity objects, prefabs, animations and assets

`MainMenu.unity` rebuilt as a backdrop; screen prefabs under `Assets/Prefabs/UI/Screens/`; icons for modes and procedures (P3-UIX-06); card images rendered from the bay (static PNG, 512 px, ASTC).

## 12. Performance

Menu scene is small; the hospital rooms are not loaded while the menu shows. Card images are compressed textures; panels use the single glass material.

## 13. Error handling and edge cases

No case available for the chosen mode/procedure; generation service down (fall back to authored, message); scene load failure (back to menu, message); double press during transition (input locked while the director is busy); headset taken off on the menu (no session to pause).

## 14. Testing

PlayMode navigation tests; screenshot captures of every screen at fixed camera for visual review; readability check with `Readability.cs` rules.

## 15. Acceptance criteria

1. Launch → login → menu → Training → S1 → beginner → briefing → bay works on desktop and in the APK.
2. Test → S2 either starts an approved case or explains why none is available.
3. Pause, restart, return and quit each do what the dialog said.

## 16. Definition of Done

All MNU tasks Done, tests green, captures reviewed by the owner, headset readability and comfort in DC-14.
