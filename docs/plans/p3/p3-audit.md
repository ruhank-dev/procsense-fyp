# P3 audit: what exists, what is connected and what is missing

_Written 7 November 2026 (UTC) at `main` = `641f6b27`, after `git fetch` (no incoming commits on `main`). Teammate branches that are not merged were read without being checked out or merged: `origin/sannan47/doors-bedside-monitoring` (`93d8423d`, 4 Nov 2026, 79 files, about 4,950 added lines; it contains the earlier `sannan47/main-sync-*` and nurse work). This audit read source code, scenes, project settings, content and tests. A ticked plan box was not taken as proof. Where a claim rests on a document and not on code it says so._

## 0. How to read the classes

| Class | Meaning |
|---|---|
| **Verified** | Fully implemented and covered by automated tests that ran green on the stated commit (Editor/desktop only unless a headset result file exists) |
| **Not integrated** | Implemented and tested in isolation, but not reachable in the normal player journey (not placed in a scene, not called by the flow, or only on an unmerged branch) |
| **Partial** | Some of the behaviour exists; named parts are missing |
| **Planned** | A P0-P2 step or register item describes it; no code |
| **Missing** | No step, no code |
| **Blocked (external)** | Built, but acceptance needs the headset, a clinician, a human-authored set or an owner decision |

No result in this file comes from a Quest headset. Every "Verified" means Editor, desktop or CI evidence.

## 1. Subsystem classification

| # | Subsystem | Class | Evidence (paths) | What is missing for the final game |
|---|---|---|---|---|
| 1 | Deterministic engine (state, rules, scoring, replay) | Verified | `src/ProcSense.Core` (version 0.0.17), about 100 registered verbs (`Actions/VerbRegistry.cs`, subsystems in `Workflow/Steps/`), replay goldens `tests/golden*`, CI green on `641f6b27` | A side-effect-free "can this action be applied now, and if not why" query for menus (P3-SES-02); a hint-request verb with a scored cost for assessment (P3-EXM-01) |
| 2 | Practice / assessment modes | Partial | `Session.Mode`, `Session.PracticeAids`; Unity `Bridge/ModeChoice.cs`, `Bridge/ModeGate.cs`, `Views/ModeChooserView.cs` | **No case can start in assessment**: all 63 playable case files are `APPROVED_FOR_TRAINING` (`content/case_approvals.json`, `content/case_files/*.json`), and `ContentGate` admits only `APPROVED_FOR_ASSESSMENT` in assessment (`src/ProcSense.Core/Content/ContentGate.cs`). Owner decision D1 |
| 3 | Main menu | Not integrated | `Views/MainMenuController.cs` (Start, Quit, Generated scenario), scene `Assets/Scenes/MainMenu.unity` is in the build list | The app starts at `Boot.unity` (first build scene) → login → the bay; the menu is never shown. No surgery selection, no polished presentation, no loading or briefing screens |
| 4 | Surgery (procedure) selection | Missing | `SessionConfig.caseId` is fixed to `VAR-S1-A` (`Bridge/SessionConfig.cs`) | Choice of S1 (laparoscopic cholecystectomy) or S2 (appendicectomy), case pool per procedure and mode |
| 5 | Scenario generation and variation | Partial | Server `server/generation` (Stage B composer, quality gates, sealed packages), `routers/scenario_packages.py`, Unity `Sync/PackageSessionStarter.cs`, `Views/GeneratedScenarioLauncher.cs` (practice only, falls back to the authored case); 65 case files (63 playable: S1 29, S2 34; 2 DRAFT); seeds `content/case_seeds.csv`; `Difficulty/Difficulty.cs` | Random selection over the authored pool with a recorded seed (no chooser exists), an offline path when the server is absent, an assessment pool (D1), per-case patient appearance |
| 6 | Session lifecycle / orchestration | Partial | `Bridge/SimulationHost.cs` (one session, `Apply`, `EndSession`, follow-up), `Views/SessionTabView.cs` ("Finish session and upload"), `Boot/BootLoader.cs`, `Boot/RoomLoader.cs` (all three rooms loaded together) | A game-flow state machine (menu → selection → loading → play → pause → submit → results → retry/menu), session cleanup and restart without reloading the app, interrupted-session handling |
| 7 | Menu interaction (tablet) | Verified (Editor) | `Views/TabletView.cs` (641 lines), `content/ui/tablet.json` (11 tabs, 144 entries), `Fallback/TabletFallbackRegistry.cs`, `ActionEmitters/*` | Contextual per-character and per-equipment panels (tablet is one global list), availability filtering from engine state, glass presentation |
| 8 | Physical VR interactions | Partial | Sensors, wristband, notes folder, doors, transport; Sannan's branch adds modeled doors, a physical thermometer (`ContactActionEmitter`), nurse wristband visit, records workstation | ECG, auscultation, glucose, sampling, IV, oxygen, consent form signing, OT stations (open P0 steps vr-interaction-ui s02, s06, s07, s12) |
| 9 | Voice input (ASR → action) | Verified (desktop), practice only | `Voice/PushToTalk.cs`, `AsrClient.cs`, `SpokenActionRouter.cs`, `SpokenOrderFlow.cs`, `TeamConversation.cs`; server `asr/`, `speech_action/`, `orders/`, `dialogue/`, `evaluate/` | Voice is refused in assessment (`SpokenActionRouter.cs:89`, `SpokenOrderFlow.cs:105`); no voice-mode presentation (menus hidden), no unified failure phrasing, needs the laptop server; headset checks DC-08 to DC-11 not run |
| 10 | Local / cloud language models | Verified (desktop) | `content/ai/model_routes.json`, `server/llm/routing.py`; Gemma 4 31B through Ollama Cloud (internet) for speech reading and voices, Qwen3.5 4B local fallback, offline grammar for orders | An explicit offline demonstration profile for P3 (decision D2), health indicator in the headset |
| 11 | Patient and staff dialogue | Partial | `TeamVoicesView`, `TeamDialogueClient`, `VoicePlayer`, `SubtitleView`, `NurseSubtitles`; `content/voice/`; rendered voice clips are not in the repository (subtitle fallback) | Production voice clips or live TTS on the device path, lip sync (p0-voice-audio-s04) |
| 12 | Characters | Partial | Rocketbox avatars (MIT) in corridor and OT (`Assets/Characters/Rocketbox`, 17 FBX), patient `Medical_Male_04` in the bay with `PatientMotion` (breathing, blinking, gaze); Sannan's greybox nurse prototype | **The bay patient is male but the default case is Mrs Ayesha Khan, 42 F**; 31 of the 63 playable cases have a female patient, ages across all cases run from 16 to 74. No doctor, anaesthetist, receptionist or porter that the trainee can address; no per-case patient appearance |
| 13 | Animation | Partial | Quaternius UAL clips, `StaffIdleView`, `SeatedPerson`, `PatientIdleView`, `PatientMotion`, `PatientSpeechFace`, `GownSecondaryMotion`, `CurtainCloth` | Talking/listening states driven by dialogue events, gestures, look-at-player, examination responses, escort/transport motion, UI and transition animations |
| 14 | Training checklist / goals | Partial | Core goals `ObservableState.Goals` (practice only), `Tracker.Hint()` for beginner level, ward checklist panel and WHO board (`UI/Checklist/*`, placed on Sannan's branch) | A trainee-facing bulletin board of objectives with live ticks, completion animation and sound, conditional steps |
| 15 | Examination hint and submit | Missing | — | Lightbulb hint with penalty and count; "Submit patient to OT" with confirmation that ends the examination |
| 16 | Scoring | Verified | `Scoring/Scorer.cs` (SUCCESS / SUCCESS_WITH_DELAY / FAIL, criticals, categories, provisional weighted total), `CommunicationScorer` (experimental rubric, DT-245) | A grade/percentage presentation rule (decision D4), hint penalty items |
| 17 | Debrief / results | Not integrated (main) / Partial | `UI/Debrief/*` (items, timeline, evidence drawer, citations); placed as a station on Sannan's branch only | A results screen (score, grade, pass/fail, hints, timing) with retry/new/menu; dimmed transition |
| 18 | Records (EMR, folder, results, phone) | Not integrated on main | `Records/*`, `UI/Results/*`; placed on Sannan's branch (records workstation) | Merge of Sannan's branch; headset usability |
| 19 | Settings and accessibility | Partial | `UI/Accessibility/*` (panel size, subtitle size, hand, seated/standing), `XR/ComfortControls.cs` | Volume sliders, interaction-mode switch, subtitle toggle, turn style, pause menu, return to menu, quit with confirmation, persistence |
| 20 | Audio | Partial | `Audio/ToneBank.cs` (synthesised tones), `RoomAudio`, `AlarmAudioView`, `VoicePlayer` | Music, ambience recordings, UI sounds, footsteps, equipment sounds, mixer and volume groups |
| 21 | UI visual design | Partial | `Views/UiKit.cs` (flat world canvases, TextMeshPro), login artwork (`Login/RoundedGraphic.cs`) | One design system, glass panel material that works on Quest, transitions |
| 22 | Backend (accounts, sessions, verification, dashboard) | Verified | `server/` (about 480 tests), Docker image, session replay verification | Nothing new is required for the game loop; optional result upload already exists |
| 23 | Quest builds | Partial | `Editor/Setup/BuildScript.cs` (Dev/Demo/Study, desktop), APK builds (DC-12 prepared) | No measured frame rate; teammate reports 20 to 30 FPS (unverified); no Quest render tier (see `docs/qa/quest2_performance_analysis.md`) |
| 24 | Automated tests and CI | Verified | `task ci` 202 checks; GitHub CI 5 jobs green on `641f6b27`; Unity EditMode 204, PlayMode 239 passed + 7 skipped on a fresh clone (`docs/qa/unity_environment_validation.md`) | Tests for every P3 flow (P3-QA plan) |
| 25 | Clinical validation | Blocked (external) | Owner-attested approvals (INSTRUCTIONS §8i), AI clinical review accepted by the owner for the educational prototype (`docs/research/ai_clinical_review.md`) | Independent clinician review stays external; P3 does not change any approval label |

## 2. Unmerged teammate work that P3 must build on, not redo

| Branch work (Sannan, `origin/sannan47/doors-bedside-monitoring`) | Effect on P3 |
|---|---|
| Records workstation (EMR, results viewer, phone, report tray) placed in PatientBay | P3-INT uses it; do not rebuild |
| Ward checklist panel, WHO board in OT, debrief station gated on Finish | P3-TRN and P3-RES restyle and re-host these views, not replace them |
| Session wait overlay, voice and subtitle presentation in Sys_Core | P3-VOX and P3-SND reuse the one presenter |
| Visible nurse wristband visit (greybox, NavMesh, contact-gated swap) | P3-ANI/P3-AST replace the greybox art only |
| Modeled door repair, physical thermometer (`measure_temperature`) | P3-INT counts the thermometer as done in code |
| Patient bindings to the Rocketbox body, ward/debrief placement | P3-AST patient replacement must keep these bindings |

P3-SES-01 makes merging this branch into `main` (by Sannan, with `task ci`) the first P3 task, so that P3 does not fork the scene files.

## 3. Findings that change the plan

1. **Assessment cannot start today (confirmed by code):** no case file has `APPROVED_FOR_ASSESSMENT`, and only P0-tier cases may hold it (`CaseFileLoader.cs:329`). P0 has 16 playable cases (S1 9, S2 7). Decision D1.
2. **Voice is practice-only today (confirmed by code).** The request wants voice in the examination too. Decision D3.
3. **The game does not start at a menu (confirmed by build settings).** `Boot.unity` is scene 0.
4. **The bay patient does not match most cases (confirmed by content).** Patient appearance must follow `identity.sex` and age.
5. **Sunlight shafts and dust are switched off on Android on purpose (confirmed by code):** `DesktopOnly.Awake` deactivates their host, and DC-12 says they must not appear in the headset. Details in `docs/qa/quest2_missing_light_effects.md`.
6. **The URP asset is shared by desktop and Quest (confirmed by settings):** one `URP_Quest.asset` for every quality level; the renderer forces an intermediate texture (`m_IntermediateTextureMode: 1`, Always), which also blocks Quest foveated rendering. Details in `docs/qa/quest2_performance_analysis.md`.
7. **All three rooms are loaded together (confirmed by code):** `RoomLoader` loads PatientBay, Corridor and OT additively, so every realtime light and renderer of the three rooms is live at once.

8. **The headset cannot reach a laptop on the local network today (confirmed by code, added 7 Nov 2026 later the same day for D2):** `ServiceSettings.IsAcceptable` accepts only `https://` or `http://localhost`/`127.0.0.1`, the player setting `insecureHttpOption: 0` forbids plain HTTP on the device, `task server-demo` binds `127.0.0.1`, and headset sign-in needs the server's Supabase settings. Plan: [p3-18-networked-quest-runtime](p3-18-networked-quest-runtime.md).

## 4. Open P0 steps that P3 depends on (not duplicated)

The 59 unapproved P0-P2 steps stay where they are. P3 tasks name them in "Depends on" when they need them: interaction framework (p0-vr-interaction-ui-s02), bedside and OT interactions (s06, s07), nurse and wristband (s12), checklist boards (s08), debrief UI (s09), accessibility (s10), characters (p0-characters-s01 to s08), lip sync (p0-voice-audio-s04), ambience (s05), Quest builds and profiling (p0-vr-platform-s07), weekly profiling (p0-evaluation-performance-s03), release (s08). Their owners stay as in `docs/plans/frontend/front_end_load_distribution.md`.
