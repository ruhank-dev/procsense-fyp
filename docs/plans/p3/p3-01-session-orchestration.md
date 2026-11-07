# p3-01-session-orchestration — Game flow and one authoritative action path (SES)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented. Audit: [p3-audit.md](p3-audit.md). Roadmap: [P3_FINAL_GAME_INTEGRATION_ROADMAP.md](P3_FINAL_GAME_INTEGRATION_ROADMAP.md).

## 1. Objective and final user-facing behaviour

The player moves through one continuous game: menu → mode → procedure → scenario → loading → play → (pause) → submit to theatre → results → debrief → retry, new scenario or menu, without restarting the app. Every action, whether from a contextual menu, the tablet, a physical gesture or a spoken sentence, goes through **one gateway** into the deterministic engine, gets **one outcome** (accepted, or rejected with a reason the player can read), and can never be applied twice by accident. If something fails (a scene, the server, a model), the player sees a short explanation and the session state is unchanged.

## 2. Current implementation status

- `SimulationHost` (`unity/Assets/Scripts/Bridge/SimulationHost.cs`) owns the one Core `Session`; `Apply(SimAction)` is the only door into the engine; `EndSession()` writes the log and queues the upload. **Verified** (EditMode/PlayMode).
- Emitters (`ActionEmitters/*`), the tablet (`Views/TabletView.cs`), the spoken-action router and the spoken-order flow each build their own `SimAction` and call `SimulationHost.Apply` directly. The spoken-order flow has its own 8-second duplicate guard; the others have none. **Partial.**
- `BootLoader` goes Boot → login → Sys_Core → PatientBay; `RoomLoader` loads all three rooms together. There is no game-flow state machine, no pause flow, no restart without reloading the app. **Missing.**
- Core cannot answer "would this action be accepted now?" without applying it. **Missing.**
- Sannan's unmerged branch adds the session wait/voice presenter, records workstation, clinical panel stations and the nurse visit. **Not integrated.**

## 3. Existing code, assets, scenes and systems to reuse

`SimulationHost`, `ModeChoice`/`ModeGate`, `SessionConfig`, `SessionTabView` (finish), `UploadQueue`/`SessionUploader`, `RoomLoader`, `LocomotionFade`, `TabletFallbackRegistry`, `HintTexts`, Core `Session`, `ObservableState`, `SimAction`, `VerbRegistry`, engine `pause`/`resume` verbs, Sys_Core scene, Sannan's `ClinicalPanelStation` lifecycle hooks (branch).

## 4. Missing functionality

1. A game-flow state machine that owns scenes, session start/end and screens.
2. One action gateway with request ids, duplicate protection, channel tagging and a single outcome event.
3. A side-effect-free engine query: can action X be applied now, and if not, which reason code.
4. A feedback event stream (accepted, rejected, goal completed, score item, alarm, end state) for UI, audio and animation.
5. Session teardown and restart inside the running app; pause and resume; interrupted-session recovery.

## 5. Architecture and integrations

```
 Input sources                         Unity (headset)                                      Engine (C# Core, deterministic)
 ─────────────                         ───────────────                                      ───────────────────────────────
 Contextual panel (P3-CTX) ─┐
 Tablet (existing)          ├─► ActionRequest ─► ActionGateway ─► Session.Check(action) ──► reason code / OK   (no state change)
 Physical emitters          │    {requestId,      │  dedupe 8 s     │
 Voice (P3-VOX) ────────────┘     verb,args,      │  mode gate      └─► SimulationHost.Apply ─► Session.Apply ─► ObservableState
                                  source,entity}  ▼                                                  │
                                            ActionOutcome ◄────────────────────────────────────────────┘
                                                  │
                                                  ▼
                                   SimFeedback bus ─► views, checklist (P3-TRN), audio (P3-SND), animation (P3-ANI)

 GameFlowDirector (Sys_Core): Boot → Login → MainMenu → ModeSelect → ProcedureSelect → ScenarioPrep → Loading → Briefing
                               → Playing ⇄ Paused → SubmitConfirm → Ending → Results → Debrief → {Retry | NewScenario | MainMenu | Quit}
```

- **Authority:** the engine on the headset decides every state change and score (ADR-0005). The server never decides; it interprets speech and verifies uploaded logs by replay.
- **Gateway rules:** one in-flight request per `requestId`; the same `(verb, args)` from any source inside 8 s after an accepted one is answered `DUPLICATE` without reaching the engine (the spoken-order rule generalised); the mode gate (practice-only aids) stays in `ModeGate`.
- **Session request:** `SessionRequest {mode, procedure (S1|S2), caseId, seed, level, source (authored|generated), interactionMode}` is the only way a session starts; it is written into the session log header so a replay reproduces it.
- **Design pattern:** state machine (GameFlowDirector) plus mediator (ActionGateway). Chosen because the flow has fixed states and illegal transitions, and every input must meet the same checks in one place.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-SES-01 | Merge `sannan47/doors-bedside-monitoring` into `main` (Sannan, `task ci` green, PlayMode affected groups green) | Sannan | — | `main` contains the records workstation, panel stations, voice presenter, nurse visit, doors and thermometer; CI green | Planned |
| P3-SES-02 | Core `Session.Check(SimAction)`: the same validation as `Apply` without any state change, returning `Accepted` or the reason code; engine version raised; golden hashes unchanged | Ruhan | — | Unit tests prove Check never changes the replay hash and agrees with Apply on every golden log action | Planned |
| P3-SES-03 | `ActionGateway` in `Scripts/Bridge/`: `Submit(ActionRequest) → ActionOutcome`, request ids, 8 s duplicate window, source tag (menu, tablet, physical, voice), mode gate | Moiz | P3-SES-02 | EditMode tests: duplicate, rejected, accepted, no session, paused | Planned |
| P3-SES-04 | Route the tablet, `UIButtonEmitter`, `XRSelectEmitter`, `ContactActionEmitter`, `SpokenActionRouter` and `SpokenOrderFlow` through the gateway (no direct `SimulationHost.Apply` left outside it) | Moiz | P3-SES-03, P3-SES-01 | A repository check fails on any new direct `SimulationHost.Instance.Apply` call outside the gateway | Planned |
| P3-SES-05 | `GameFlowDirector` state machine in Sys_Core: states and transitions of section 5, scene loading through `RoomLoader`, one active screen at a time | Moiz | P3-SES-01 | PlayMode test walks every legal transition and refuses every illegal one | Planned |
| P3-SES-06 | `SessionRequest` and `SessionFactory`: start a session only from a request (case, seed, mode, level, source); the request is written to the log header | Ruhan | P3-SES-05 | Replay of a saved log rebuilds the same request and the same hash | Planned |
| P3-SES-07 | Teardown and restart without reloading the app: unlock `ModeChoice`, clear views and owned UI, reset patient presentation, transport, rooms and pools | Moiz | P3-SES-05 | 20 start/finish cycles in PlayMode leave no duplicated objects and no growth in live object count | Planned |
| P3-SES-08 | Pause and resume: pause menu and the headset system menu (focus lost) send the engine `pause`/`resume`; input frozen while paused | Moiz | P3-SES-05 | Clock does not advance while paused; actions are refused with `SESSION_PAUSED` | Planned |
| P3-SES-09 | Interrupted session: the log is flushed to the upload queue on pause/quit; on next launch the menu offers "Resume" (replay of the saved log to the last action) or "Discard" | Ruhan | P3-SES-07 | Kill-and-relaunch test resumes to the same observable state hash | Planned |
| P3-SES-10 | `SimFeedback` event stream: action accepted/rejected (with hint text), goal completed, score item raised, alarm, end state, phase change | Moiz | P3-SES-03 | Checklist, audio and animation subscribe without polling `ObservableState` every frame | Planned |
| P3-SES-11 | Tests for the flow and the gateway (EditMode and PlayMode) and the repository rule of P3-SES-04 in `tools/ci/repo_checks.py` | Moiz | P3-SES-04, P3-SES-07, P3-SES-08, P3-SES-10 | Tests in CI; `docs/CI.md` row added | Planned |

## 7. Dependencies and execution order

P3-SES-01 first (avoids a second copy of scene edits). P3-SES-02 can start at once in parallel (engine only). Then P3-SES-03 → P3-SES-04 and P3-SES-05 → P3-SES-06/07/08 → P3-SES-09; P3-SES-10 after the gateway. Every other P3 plan depends on P3-SES-03 or P3-SES-05.

## 8. Frontend components

No new visible screen in this plan except a generic "something went wrong" toast and the "Resume previous session?" dialog (styled by P3-UIX). The pause screen is in P3-MNU.

## 9. Backend and API changes

None required. The session log header gains the `SessionRequest` fields; the server's replay verification already reads the header, and P3-API-06 adds the fields to the upload schema as optional.

## 10. AI and voice integration

Voice sentences become `ActionRequest`s with `source = voice` after the existing interpretation; the gateway treats them exactly like a menu press. The rule "a critical physical action never gets credit from speech" stays in the engine and is not touched.

## 11. Unity objects, prefabs, animations and assets

`GameFlowDirector` and `ActionGateway` objects in `Sys_Core.unity`; no new art.

## 12. Performance

The gateway adds one dictionary lookup per action. `SimFeedback` is event based; views stop polling the full `ObservableState` in `Update` where they can (the tablet and `PracticeOnly.Update` currently poll every frame).

## 13. Error handling and edge cases

Double press, two sources at once (voice and button for the same action), action during scene load, action while paused, action after the end state, session replaced by a generated package, host destroyed mid-request, a scene that fails to load (return to menu with a message, session not started), quitting from the headset menu during a write.

## 14. Testing

EditMode: gateway rules, request serialisation, Check/Apply agreement (Core xUnit). PlayMode: flow transitions, teardown leak test, pause, resume after kill. CI: repository rule against direct Apply calls.

## 15. Acceptance criteria

1. From the menu, two complete sessions in a row (one S1, one S2) run without restarting the app, and the second starts clean.
2. No action reaches the engine twice within 8 s from any combination of sources.
3. Every rejected action shows a readable reason (a `HintTexts` entry or the code).
4. A killed session resumes to the same observable state hash.

## 16. Definition of Done

All SES tasks Done; tests in CI; `docs/CI.md`, `docs/context/ProcSense_Current_State.md` and the audit updated; desktop playthrough recorded; headset run listed in DC-14.
