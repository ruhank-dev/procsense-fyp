# p3-05-voice-mode — Voice-based mode with contextual reference panels (VOX)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented. Owner decisions (7 Nov 2026 UTC, ADR-0032): D2 network-connected demonstration through the laptop server and Ollama Cloud ([p3-18](p3-18-networked-quest-runtime.md)); D3 voice in **both** Training and Examination, validated exactly like the menu action.

## 1. Objective and final user-facing behaviour

When the player switches to voice mode in Settings, every persistent action menu disappears. The player holds the talk button and speaks naturally ("Could you tell me your full name and date of birth?", "Nurse, can we get a fresh wristband?", "I'd like a full blood count and LFTs", "Can I have a hint?", "Submit the patient to theatre"). A small listening ring shows when the microphone is open, the recognised sentence is echoed, and the person spoken to answers. Pointing at a person still opens the same glass panel, but in voice mode it is a **reference card**: what you can ask this person, example sentences and relevant information. The large **Submit Patient to OT** button stays clickable. When something goes wrong, the game answers politely: "I didn't quite understand that. Could you repeat the request?", "That action isn't available at this stage of the patient's preparation.", "The nurse can't do that; ask the doctor.", "Please confirm: …". If the speech service is unreachable, a banner says so and offers menu mode; the session never stops.

## 2. Current implementation status

- Push-to-talk, recogniser client, speech-action route (`/speech/organisations/{id}/action`), spoken orders with read-back and single-use tickets (`/orders/.../hear`, `/confirm`, `/details`), staff and patient dialogue, Gemma speech evaluation: **Verified (desktop and server tests)**. Headset checks DC-08 to DC-11 not run. **Blocked (external).**
- Voice is **practice-only**: `SpokenActionRouter` (line 89) and `SpokenOrderFlow` (line 105) refuse when `ModeGate.PracticeAids` is false.
- There is no "voice mode" (menus never hide), no addressee selection beyond the router's patient/staff split, no unified failure phrasing. **Missing.**
- Runtime needs the laptop server on the same network: ASR (faster-whisper `small.en`) and the offline grammar are local; Gemma 4 31B runs through Ollama Cloud and needs the internet; the local Qwen3.5 4B is the dialogue fallback. **Verified (desktop).**

## 3. Existing code, assets, scenes and systems to reuse

`PushToTalk`, `AsrClient`, `WavEncoder`, `SpokenActionClient`/`Router`, `SpokenOrderClient`/`Flow`, `OrderPanelView` (read-back, yes/no, clarification), `TeamConversation`, `TeamDialogueClient`, `TtsClient`, `VoiceHealth`, `SpeechEvaluation`, server `asr/ speech_action/ orders/ dialogue/ evaluate/ tts/`, `content/ai/model_routes.json`, demonstration profile `server/demo/synthetic-fyp-demo.env`.

## 4. Missing functionality

An interaction-mode state and switch; hiding menus; reference panels; addressee targeting; the examination path (D3); hint and submit intents; a phrase table for failures; a listening indicator; an availability banner and automatic fallback; the offline profile (D2).

## 5. Architecture and integrations

```
Talk button ─► PushToTalk ─► /asr ─► text ─► SpokenActionRouter
                                              ├─ addressee = selected entity | gaze target (20° cone, ≤ 4 m) | name said ("Nurse, …")
                                              ├─ /speech/.../action  (tablet actions)      ─┐
                                              ├─ /orders/.../hear → read-back → spoken/button yes → ticket ─┤─► ActionRequest(source = voice) ─► ActionGateway ─► engine
                                              └─ otherwise dialogue + communication evaluation (existing)     ┘
Outcome (gateway or service) ─► VoiceFeedback phrase table ─► subtitle + TTS/pre-rendered line + SimFeedback
```

- **Same authority:** a voice request becomes the same `ActionRequest` as a menu press. Critical physical actions keep needing the physical or button path; speech never grants them credit (ADR-0025, unchanged).
- **Interaction mode is presentation only:** it is not engine state, it does not change scoring, and switching keeps the session as it is. It is written to the session header as metadata for analysis.
- **Examination (D3, decided: allowed):** the router and order flow drop the practice gate in assessment but keep every confirmation and show no hint text; voice hint and submit requests use the same gateway requests, limits and costs as the glass buttons; P3-VOX-13 proves voice and menu give identical engine outcomes first.
- **Network (D2):** every voice stage runs on the laptop (ASR faster-whisper, Piper TTS) except the text model (Gemma 4 31B on Ollama Cloud through the laptop); see [p3-18](p3-18-networked-quest-runtime.md) for discovery, pairing, time budgets and reconnection.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-VOX-01 | Owner decisions D2 (network profile for the demonstration) and D3 (voice in the examination) recorded with an ADR amendment to ADR-0028 | Owner | — | Decision rows and ADR text exist | Done |
| P3-VOX-02 | `InteractionMode` service (Menu, Voice), persisted, switchable mid-session, raising a change event; written to the session header | Moiz | P3-SES-05 | Switching does not change the observable state hash | Planned |
| P3-VOX-03 | Voice mode hides persistent menus (tablet, contextual action lists, ward panel buttons) and turns contextual panels into reference cards | Moiz | P3-VOX-02, P3-CTX-04 | PlayMode test: no action button visible except the allowed exceptions | Planned |
| P3-VOX-04 | Reference card layout: "You can ask…" from the catalogue's voice examples, relevant information, the clickable exceptions (Submit Patient to OT, confirmation Yes/No) | Moiz | P3-VOX-03, P3-CTX-01 | Every entity type shows at least three examples | Planned |
| P3-VOX-05 | Addressee targeting: selected entity, else gaze cone, else a name in the sentence; sent as `addressee` to the speech routes | Moiz | P3-CTX-02, P3-API-02 | Test: "Nurse, …" while looking at the patient goes to the nurse | Planned |
| P3-VOX-06 | Examination voice per D3: remove the practice gate in `SpokenActionRouter` and `SpokenOrderFlow` for assessment, keep every confirmation and read-back, suppress hint text, route hint and submit by voice through the same gateway requests (same limits and costs as the glass buttons) | Ruhan | P3-VOX-01, P3-SES-04, P3-VOX-13 | Assessment test plays the identity, allergy and consent steps by voice with no hint shown and the same engine outcome as the menu | Planned |
| P3-VOX-07 | `content/ui/voice_feedback.json` phrase table (misunderstood, ambiguous, unavailable, wrong person, refused by the engine with its reason, recogniser failed, needs confirmation, service offline) with pre-rendered clips where available and subtitle always | Ruhan | P3-SES-10 | Every outcome code maps to a phrase (validator) | Planned |
| P3-VOX-08 | Hint and submit intents ("hint", "help", "submit the patient to theatre/OT") added to the approved action catalogue of the interpreter and mapped to the same gateway requests as the buttons | Ruhan | P3-EXM-01, P3-EXM-04 | Held-out style check on a small fixed sentence list; the frozen evaluator prompt is not retuned (only the catalogue changes, as ADR-0028 allows) | Planned |
| P3-VOX-09 | Listening and processing indicator: ring on the controller and near the view, transcript echo for 3 s, processing spinner, 8 s timeout | Moiz | P3-UIX-07 | Indicator states tested with a fake microphone | Planned |
| P3-VOX-10 | Availability: poll `VoiceHealth`; on loss show a banner, offer menu mode with one button, never block the session | Moiz | P3-VOX-02, P3-API-03 | Server stopped mid-session: banner within 5 s, menus available | Planned |
| P3-VOX-11 | Degraded profile per D2: cloud unreachable or out of quota → the laptop answers with the local fallbacks (grammar orders, local Qwen dialogue, templates) and says so once; laptop unreachable → menu mode with the status chip; `task demo-degraded-check` | Ruhan | P3-VOX-01, P3-VOX-10, P3-NET-13 | The check passes with the laptop's internet unplugged and again with the laptop's Wi-Fi off | Planned |
| P3-VOX-12 | Tests: server contract tests for `addressee` and the intents; PlayMode with a fake recogniser for the voice-only S1 identity and consent steps | Ruhan | P3-VOX-05, P3-VOX-07, P3-VOX-08 | Green in CI | Planned |
| P3-VOX-13 | Voice/menu equivalence before examination voice is switched on: for every examination-relevant action of the catalogue, a fixed sentence set through the fake recogniser and the same action through the menu give the identical engine outcome, score items and refusal codes; critical physical actions never credited from speech | Ruhan | P3-VOX-08, P3-SES-04 | Equivalence test green in CI; mismatches fixed in the catalogue, never by retuning the frozen evaluator | Planned |

## 7. Dependencies and execution order

D2/D3 first (owner). P3-VOX-02 after the flow; hide/reference after the contextual panel; addressee needs P3-API-02; intents need the engine hint and submit work.

## 8. Frontend components

Mode toggle (Settings and wrist), listening ring, transcript echo, reference card, availability banner, confirmation chips.

## 9. Backend and API changes

Optional `addressee`, `interaction_mode` and `session_mode` fields on `/speech/.../action`, `/orders/.../hear` and `/dialogue/...` (P3-API-02); hint and submit entries in the approved action catalogue; no change to scoring on the server.

## 10. AI and voice integration

Uses the existing interpreter, evaluator and dialogue routes. The evaluator prompt and scoring semantics are frozen (INSTRUCTIONS §8j.13); P3 adds catalogue entries and context fields only. The grammar fallback must handle "hint" and "submit to theatre" deterministically.

## 11. Unity objects, prefabs, animations and assets

Listening ring prefab, reference card prefab, voice feedback clips (P3-SND-08), talk-button hint on the controller model.

## 12. Performance

Audio capture only while the button is held; one request in flight; no model runs on the headset.

## 13. Error handling and edge cases

Silence; very long speech (cut at 15 s); background noise; two people named in one sentence; addressee in another room; a ticket expired before yes; the server answers after the player switched to menu mode (late answers are dropped); cloud consent missing (local fallback, said once).

## 14. Testing

Server pytest contracts; Unity EditMode for the phrase table and addressee rules; PlayMode with recorded or synthetic audio; headset checks DC-08 to DC-11 plus the voice part of DC-14.

## 15. Acceptance criteria

1. S1 identity, allergy, history and consent steps can be completed by voice alone in practice (and in the examination if D3 allows).
2. Every failure case gives a spoken or subtitled explanation and leaves the state unchanged.
3. With the server down, the session continues in menu mode.

## 16. Definition of Done

All VOX tasks Done; ADR amendment; demo checks pass; headset items recorded by Sannan.
