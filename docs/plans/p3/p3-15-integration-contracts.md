# p3-15-integration-contracts — Frontend, engine, backend, AI and voice working together (API)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

Whatever the player does, by button or by voice, online or offline, the game reacts within a predictable time, never applies something twice, and explains failures in one sentence. The headset can finish a whole session without the server; with the server it adds voice, generated scenarios, AI wording and verification.

## 2. Current implementation status

Engine on the headset is authoritative (`SimulationHost`); the server interprets speech (`/speech/.../action`, `/orders/.../hear|confirm|details`), runs dialogue (`/dialogue/.../patient|staff|team`), evaluates communication (`/evaluate/...`), transcribes (`/asr/...`), speaks (`/tts/...`), offers packages (`/scenario-packages/next|generate`), rewrites debriefs, receives and replays sessions (`/organisations/{id}/sessions`), reports health (`/health`, `/ai/health`). Device sign-in (`/auth/device/*`). **Verified (server tests, desktop).** No shared contract document for the P3 flows; no idempotency key for non-order actions; no addressee field. **Partial.**

## 3. Existing code, assets, scenes and systems to reuse

Server routers and schemas (`server/routers`, `server/schemas`), Unity clients (`AsrClient`, `SpokenActionClient`, `SpokenOrderClient`, `TeamDialogueClient`, `TtsClient`, `PackageClient`, `ResultsClient`, `SessionSync`), `VoiceHealth`, server contract tests `tests/orders/unity_contract`.

## 4. Missing functionality

Written contracts for every P3 flow; DTOs shared by the flows; optional request fields (addressee, interaction mode, session mode); timeouts and cancellation everywhere; request ids end to end; an error taxonomy mapped to player messages; contract tests on both sides.

## 5. Architecture and integrations

**Data contracts (C# records in `ProcSense.VR.Bridge.Contracts`, JSON for the server; field names in snake_case on the wire):**

```
SessionRequest   { mode: practice|assessment, procedure: S1|S2, case_id, seed, level, source: authored|generated, interaction_mode: menu|voice }
ActionRequest    { request_id (GUID), verb, args{}, source: menu|tablet|physical|voice, entity_id?, confirm_token? }
ActionOutcome    { request_id, accepted, reason_code?, hint_text?, duplicate, engine_step }
SimFeedbackEvent { kind: action_accepted|action_rejected|goal_completed|goal_not_needed|score_item|alarm|phase|end_state, ids[], text?, step }
HintResult       { tier: 1|2, text, goal_id, hints_used, cost }               (from request_hint)
SubmitResult     { end_state, result, abandoned }                              (from submit_patient)
ResultSummary    { verdict, end_state_label, points?, percent?, grade?, criticals[], completed[], missed[], wrong_decisions[], hints{count, stages[]}, times{}, recommendations[], case_id, seed }
VoiceTurn        { request_id, text, addressee?, interaction_mode, session_mode, context{} }  → server
VoiceAnswer      { kind: action|order_proposal|question|dialogue|unclear|unavailable, action?, ticket?, read_back?, reply_text?, reason_code? }
```

**Event flows:**

```
Start scenario:  Menu ─SessionRequest─► GameFlowDirector ─(generated?)─► POST /scenario-packages/next ─► package ─► engine replay check
                                                        └─(authored)─► ScenarioPicker ─► ContentMode.Load ─► Session ─► briefing ─► Playing
Menu action:     Panel ─ActionRequest─► Gateway ─Check─► Apply ─► ObservableState ─► ActionOutcome + SimFeedbackEvent ─► UI/audio/animation/checklist
Voice action:    PTT ─wav─► POST /asr ─text─► POST /speech/.../action (VoiceTurn) ─► VoiceAnswer(action) ─► ActionRequest(source=voice) ─► Gateway (as above)
Spoken order:    text ─► POST /orders/.../hear ─► read-back + ticket ─► player "yes" ─► POST /confirm ─► ActionRequest(confirm_token) ─► Gateway
Dialogue:        text ─► POST /dialogue/.../team ─► reply_text ─► TTS or clip ─► VoicePlayer + subtitles ─► POST /evaluate (communication credit, engine scores)
Hint:            lightbulb / "hint" ─► ActionRequest(request_hint) ─► Gateway ─► HintResult ─► hint card (+ score item in assessment)
Submit:          button / "submit" ─► confirmation ─► ActionRequest(submit_patient) ─► Gateway ─► SubmitResult ─► Ending ─► ResultSummary ─► Results
Upload:          EndSession ─► log file ─► UploadQueue ─► POST /organisations/{id}/sessions ─► server replay ─► verified badge
Back to menu:    Results/pause ─► GameFlowDirector teardown (P3-SES-07) ─► MainMenu
```

**Timeouts and fallbacks:** ASR 6 s; speech action and order interpretation 4 s live budget (existing) then grammar; dialogue 4 s then the approved line or "The nurse nods." subtitle; TTS 3 s then subtitle only; package fetch 8 s then authored case; upload in the background with retries (existing). A late answer whose `request_id` is no longer current is dropped.

**Idempotency:** `request_id` per press or utterance; gateway duplicate window; order tickets single-use (existing); `submit_patient` accepted once per session by the engine.

**Error taxonomy → message:** `ENGINE_REJECTED(reason)` → `HintTexts`; `NOT_UNDERSTOOD`, `AMBIGUOUS`, `WRONG_ADDRESSEE`, `UNAVAILABLE_NOW`, `SERVICE_OFFLINE`, `TIMEOUT`, `ASR_FAILED`, `NEEDS_CONFIRMATION`, `SCENE_LOAD_FAILED`, `CONTENT_INTEGRITY` → `content/ui/voice_feedback.json` and a general UI message table.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-API-01 | Contracts as C# records (Unity Bridge) with JSON round-trip tests, and this page kept as their documentation | Ruhan | P3-SES-03 | Records used by gateway, flow and clients | Planned |
| P3-API-02 | Optional `addressee`, `interaction_mode`, `session_mode`, `request_id` fields on `/speech/.../action`, `/orders/.../hear` and `/dialogue/...` request schemas (Pydantic, backward compatible); `/scenario-packages/next` accepts `procedure` and `level` if it does not already | Ruhan | — | Server tests: old clients still pass; new fields reach the interpreter context | Planned |
| P3-API-03 | `ServiceStatus` in Unity from `/health` and `/ai/health` (reachable, cloud routing on/off, model in use) feeding the voice banner and settings | Moiz | P3-API-01 | Banner state follows a stopped server within 5 s | Planned |
| P3-API-04 | Timeouts, cancellation and late-answer dropping in every Unity client | Ruhan | P3-API-01 | Fault-injection tests for each client | Planned |
| P3-API-05 | End-to-end idempotency: request ids logged by the server for speech and orders; duplicate submission refused by gateway and engine | Ruhan | P3-API-02, P3-EXM-05 | Duplicate tests on both sides | Planned |
| P3-API-06 | Session upload: header fields of `SessionRequest`, interaction mode and the `ResultSummary` stored with the verified session; the dashboard shows hints used and the verdict from the server's replay, never the client's numbers | Ruhan | P3-RES-02, P3-SES-06 | Server replay result equals the headset summary for the goldens | Planned |
| P3-API-07 | Error taxonomy and message tables (voice and UI) with a validator that every code has a message | Ruhan | P3-VOX-07 | Validator in CI | Planned |
| P3-API-08 | Contract tests: server pytest fixtures and Unity EditMode fixtures read the same JSON examples (`tests/contracts/p3/*.json`) | Ruhan | P3-API-02, P3-API-04 | Both suites green in CI | Planned |

## 7. Dependencies and execution order

Contracts after the gateway; server fields in parallel; then status, timeouts, idempotency, upload, taxonomy, contract tests.

## 8. Frontend components

Service status banner, error toasts.

## 9. Backend and API changes

As P3-API-02, P3-API-05, P3-API-06 (all backward compatible; no new route is required).

## 10. AI and voice integration

The new context fields go into the interpreter's prompt context only as data; the frozen evaluator prompt and scoring semantics are not changed (INSTRUCTIONS §8j.13). Gemma never decides a simulator fact.

## 11. Unity objects, prefabs, animations and assets

None beyond the status banner.

## 12. Performance

One in-flight request per kind; JSON payloads under 4 kB except audio; no polling faster than every 5 s for health.

## 13. Error handling and edge cases

Server restarts mid-session; token expired (device refresh exists); clock skew; partial uploads; a package with a content version the headset does not have (refused, authored fallback).

## 14. Testing

Pytest contract tests, Unity EditMode contract tests, fault injection, the existing demo checks (`task demo-orders-check`) plus P3-VOX-11.

## 15. Acceptance criteria

1. Every flow of section 5 has a contract test on both sides.
2. No P3 flow can apply an action twice.
3. With the server off, a complete menu-mode session runs and uploads later.

## 16. Definition of Done

All API tasks Done; `server/README.md` and `docs/context` updated; CI green.
