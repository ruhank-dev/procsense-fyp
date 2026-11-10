# p3-18-networked-quest-runtime — Standalone Quest, laptop server, Ollama Cloud (NET)

Status: Planned (P3, 7 November 2026 UTC). Owner decision D2 (7 November 2026): the demonstration is a **network-connected simulation**. The Quest 2 runs the standalone Android APK and renders everything itself; for AI, dialogue, scenario generation and voice it talks over Wi-Fi to a laptop that runs the existing ProcSense server, and the laptop calls Ollama Cloud. No Quest Link, Air Link, SteamVR or PC-rendered VR. Nothing in this plan is implemented yet. The setup guide for the team is [../../setup/quest_laptop_cloud_demo.md](../../setup/quest_laptop_cloud_demo.md).

## 1. Objective and final user-facing behaviour

```
Quest 2 (standalone APK: scenes, engine, scoring, UI, audio)  ⇄  Wi-Fi / LAN (private network)  ⇄  Laptop: ProcSense server (FastAPI) + ASR + TTS  ⇄  internet  ⇄  Ollama Cloud (Gemma 4 31B)
```

A team member clones the repository, puts their own Ollama key in a git-ignored file, runs one command on the laptop, and the laptop prints its address and a pairing code. On the headset the player opens Settings → Server, the laptop is found (or the address is typed once and remembered), the status chip turns green ("Laptop connected · Cloud AI ready"), and Training or Examination starts. Speech goes to the laptop, the laptop transcribes it, Gemma reads it, the headset's engine decides and scores it, and the patient answers with a voice made on the laptop. If Wi-Fi drops, the chip turns amber, voice pauses with a polite message, the session carries on in menu mode, and nothing in the clinical session is lost or changed. When the laptop comes back the chip turns green again by itself.

## 2. Current implementation status

| Part | Status | Evidence |
|---|---|---|
| Server with every AI route: `/asr`, `/speech/.../action`, `/orders/.../hear|confirm|details`, `/dialogue/.../patient|staff|team`, `/evaluate`, `/tts/.../speak` (NDJSON stream), `/debrief/.../rewrite`, `/scenario-packages/next|generate`, `/ai/health`, `/health` | **Verified (desktop, server tests)** | `server/routers/*.py`, about 480 pytest tests |
| Ollama use: local Ollama at `ollama_url` relays `gemma4:31b-cloud` when signed in, or the direct API (`speech_eval_base_url = https://ollama.com`, key in `SPEECH_EVAL_API_KEY`); per-task routing in `content/ai/model_routes.json`; local fallbacks (Qwen3.5 4B, offline grammar for orders) | **Verified (desktop)** | `server/config.py`, `server/llm/routing.py`, ADR-0026, ADR-0028 |
| ASR (faster-whisper `small.en`) and TTS (Piper, sentence-streamed Ogg) on the server machine | **Verified (desktop)** | `server/asr/service.py`, `server/tts/engine.py` |
| Demonstration profile (synthetic people only, cloud routing on) | **Verified** | `server/demo/synthetic-fyp-demo.env`, `task server-demo`, `server/DEMO.md` |
| Headset → server address | **Partial:** one build-time address in `Resources/ProcSenseService` (`ServiceSettings.BaseUrl`); **`ServiceSettings.IsAcceptable` accepts only `https://` or `http://localhost`/`127.0.0.1`**, so a laptop address such as `http://192.168.1.20:8000` is refused today | `unity/Assets/Scripts/Auth/ServiceSettings.cs` |
| Plain HTTP from the APK | **Blocked by setting:** `insecureHttpOption: 0` (Not allowed) in `ProjectSettings.asset`, so Unity refuses `http://` on the device | `unity/ProjectSettings/ProjectSettings.asset` |
| Sign-in | **Partial:** the device-code sign-in needs the server's Supabase settings; a laptop server without `server/.env` Supabase values cannot sign a headset in | `server/routers/device_auth.py`, `server/routers/pages.py` (`/config.json`) |
| Discovery, pairing, connection chip, reconnect, version check | **Missing** | — |
| WebSocket or server push | **Missing** (every route is request/response; TTS streams NDJSON over one HTTP response) | — |

**Ollama Cloud facts (official pages read 7 November 2026 UTC, research log R-060 to R-064):** the native API is `https://ollama.com/api` (`/api/chat`, `/api/tags`) and the OpenAI-compatible one `https://ollama.com/v1`. Both need `Authorization: Bearer $OLLAMA_API_KEY` (key from ollama.com/settings/keys; keys do not expire and can be revoked). Alternatively a local Ollama signed in with `ollama signin` relays `<model>-cloud` names without a key in the server. `gemma4` is listed as a cloud model (31b among its sizes). Chat streams NDJSON by default, and `format` takes a JSON schema. The pay-as-you-go and Pro plans allow 3 concurrent requests (Max and Team 10); extra requests queue and are refused when the queue is full. Billing is per token, and no numeric hourly or weekly limit is published. Ollama documents `/v1/audio/transcriptions` and `/v1/audio/speech` for the **local** server; their availability on the cloud API is **not stated**. So ASR and TTS stay on the laptop (faster-whisper, Piper), and the cloud does the text model only. Ollama says cloud prompts are not used for training, that models are hosted mainly in the US, and that requests may route to Europe.

## 3. Existing code, assets, scenes and systems to reuse

Server: every router above, `server/config.py` (pydantic settings, `.env`), `server/llm/client.py` and `gateway.py` (queue, timeouts, fallback signal), `server/llm/routing.py`, `server/demo/synthetic-fyp-demo.env`, `task server-demo`, `task demo-orders-check`, `server/RUNBOOK.md`, `server/DEMO.md`. Unity: `ServiceSettings`, `AuthService`, `UnityHttp`, `VoiceHealth`, `AsrClient`, `SpokenActionClient`, `SpokenOrderClient`, `TeamDialogueClient`, `TtsClient`, `PackageSessionStarter`, the session upload queue, `PracticeOnly`/`ModeGate`. Engine: unchanged; it runs on the headset and stays authoritative.

## 4. Missing functionality

A LAN-safe address policy and HTTP permission for development and demo builds; a runtime server address with discovery, manual entry and memory; pairing; a laptop start command that binds the LAN interface, prints the address and checks the cloud; a firewall helper; a connection-status model and chip; reconnect and retry rules; request time budgets per route; version compatibility; a demo sign-in path that does not need the owner's Supabase secrets; an optional push channel; the team setup guide and its check command.

## 5. Architecture and integrations

**Where things run (decided by D2, unchanged principles):**

| Runs on the Quest | Runs on the laptop | Runs in Ollama Cloud |
|---|---|---|
| Scenes, rendering, audio playback, UI; the deterministic engine (state, rules, scoring, end state, debrief data); the session log; menu interaction; recording speech (16 kHz mono WAV while the talk button is held) | The ProcSense FastAPI server; ASR (faster-whisper); Piper TTS; the AI gateway (queue, timeouts, validation, fallbacks); the local fallback model (Qwen3.5 4B, optional); scenario package pool and generator; session upload and verification (optional, needs a database) | Gemma 4 31B for speech evaluation, spoken orders, dialogue wording and debrief rewording (per `content/ai/model_routes.json`) |

The headset never receives a cloud key. The laptop holds it (`OLLAMA_API_KEY` or a signed-in local Ollama), so a lost headset or a captured APK exposes nothing.

**Request flow for one spoken sentence (each stage separate):**

```
Quest: talk button held → WAV → POST /asr/organisations/{org}/transcribe (laptop: faster-whisper) → text
Quest: text + context (case id, phase, addressee, session_mode, interaction_mode, request_id)
   → POST /speech/.../action  or  /orders/.../hear (laptop → Ollama Cloud Gemma; offline grammar fallback)  → proposed action or order + read-back
Quest: confirmation where required (button or "yes") → ActionRequest(source = voice) → ActionGateway → engine on the headset (accept/refuse, score)
Quest: reply text needed? → POST /dialogue/... (laptop → Gemma, validated by the gateway) → POST /tts/.../speak (laptop Piper, NDJSON per sentence) → play + subtitle
```

**Session synchronisation:** the session lives on the headset. Each AI request carries the minimal context the route needs (it already does) plus `session_id`, `request_id` and versions, so the laptop is stateless between requests and a laptop restart loses nothing. The finished log is uploaded once (existing idempotent upload); if the laptop has no database the log waits in the headset's queue.

**Real-time channel:** request/response stays the default. A WebSocket `/ws/headset/{session_id}` is added only for server push that has no request: generation progress, a laptop shutting down, cloud quota exhausted. The headset works the same without it (P3-NET-12).

**Discovery and address:** (1) the address saved last time; (2) a UDP beacon from the laptop on the local network (`PROCSENSE/1 http://<lan-ip>:8000 <server-id>` every 2 s on port 47800), listened to only while the server screen is open; (3) manual entry on a keypad; (4) USB fallback: `adb reverse tcp:8000 tcp:8000` makes `http://127.0.0.1:8000` on the headset reach the laptop over the cable. The headset never uses `localhost` to mean the laptop except through `adb reverse`.

**Security on the LAN:** HTTP is allowed only in Dev and Demo builds and only to private addresses (10/8, 172.16/12, 192.168/16, 127/8). The Study build keeps https only. The laptop binds its private Wi-Fi interface, the Windows firewall rule is limited to the Private profile and the one port, and a pairing code (six digits shown by the laptop, entered once on the headset, then a random device token) is required on every AI route in the LAN profile. Nothing is exposed to the internet: no port forwarding and no public tunnel. The optional HTTPS step (a self-signed certificate pinned by fingerprint on the headset) is P3-NET-15.

**Sign-in in the LAN profile:** the demo profile uses the existing synthetic demonstration identity (`cloud_synthetic_deployment`). The headset pairs with the laptop and plays as a demonstration user, and uploads go to the laptop's local database or wait in the queue. Real accounts keep the hosted service and the device-code sign-in (unchanged).

**Time budgets (first values, tuned with DC-14 measurements):** ASR 6 s, speech action/orders 8 s (the server already falls back to grammar after 15 s; the headset gives up first and offers the menu), dialogue 10 s, TTS first sentence 4 s, health 2 s. One request in flight per kind, with a monotonically increasing `request_id`. A late answer for an old request id is dropped. Retries happen once, only for idempotent reads and for ASR when the connection failed before the upload completed. An action proposal is never retried automatically.

**Failure behaviour (session state never changes because of a network failure):**

| Situation | What the player sees | What happens |
|---|---|---|
| Laptop unreachable at start | Chip red "Laptop not found", button "Play in menu mode" | Menu mode only; voice button hidden |
| Wi-Fi drops mid-session | Chip amber "Reconnecting…", voice says "I've lost the connection to the server. You can carry on with the menus." | Health poll with back-off 1, 2, 4, then 5 s; green again without input |
| Cloud down, quota reached or key missing | Chip amber "Cloud AI unavailable · local fallback" | Laptop falls back per route (grammar orders, local model or templates); the answer carries `fallback_reason` |
| ASR failure | "I didn't catch that. Could you say it again?" | Nothing sent to the engine |
| Version mismatch | Red banner "Laptop server is version X; headset needs Y" | AI routes disabled; menu mode works |
| Laptop restarts | Amber, then green | Pairing token remembered; nothing to redo |

**Version compatibility:** `/health` gains `api_version`, `engine_versions` and `content_versions` (the settings fields already exist). The headset sends `X-ProcSense-Build`, `X-ProcSense-Engine` and `X-ProcSense-Content` on every request. One compatibility table (`content/ai/compat.json`) is checked on both sides.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-NET-01 | Record D2 in an ADR (network-connected demonstration, laptop server, Ollama Cloud, Quest renders locally; headset still works without the server in menu mode) and amend ADR-0026/ADR-0028 references | Ruhan | — | ADR merged; roadmap D2 row points to it | Planned |
| P3-NET-02 | Address policy: `ServiceSettings.IsAcceptable` accepts `http://` private-range addresses in Dev/Demo builds only; `insecureHttpOption` set to development-and-demo use (build script sets it per variant, Study stays https); EditMode tests for the ranges and the Study refusal | Moiz | — | Tests prove 192.168.x.x accepted in Demo, refused in Study; public `http://` refused everywhere | Planned |
| P3-NET-03 | Runtime server address: `ServerEndpoint` service (saved address, manual keypad entry, last-known-good), used by every client instead of the build-time asset when set | Moiz | P3-NET-02 | Every client reads the same endpoint (code search test) | Planned |
| P3-NET-04 | Laptop start command `task demo-laptop`: binds `0.0.0.0:8000` (or the chosen interface), prints the LAN addresses, the pairing code and a QR of the address, checks Ollama (`/ai/health`), ASR model, Piper, ffmpeg and the cloud key, and refuses to start with the demo profile beside `APP_ENV=prod` | Ruhan | P3-NET-01 | Run on a fresh laptop prints a green checklist or the exact missing item | Planned |
| P3-NET-05 | Windows firewall helper `tools/setup/firewall_rule.ps1` (inbound TCP 8000 and UDP 47800, Private profile only, named rule, remove option) and network-profile check (refuses on a Public network with an explanation) | Ruhan | P3-NET-04 | Rule created and removed cleanly on Windows 11; documented | Planned |
| P3-NET-06 | Discovery beacon: laptop UDP broadcast (port 47800, only in the LAN profile), headset listener on the server screen; manual entry and `adb reverse` remain | Moiz | P3-NET-03, P3-NET-04 | Headset lists the laptop within 5 s on the same Wi-Fi (DC-15) | Planned |
| P3-NET-07 | Pairing: six-digit code shown by the laptop, exchanged once for a device token stored on the headset; every AI route requires it in the LAN profile; the token is revocable from the laptop | Ruhan | P3-NET-04 | pytest: route refuses without token; Unity test stores and sends it | Planned |
| P3-NET-08 | Connection model `ServiceStatus` (laptop reachable, paired, versions compatible, cloud ready or fallback) with back-off polling, feeding the status chip (P3-UIX-11) and the voice banner (P3-VOX-10) | Moiz | P3-API-03, P3-NET-03 | Chip follows a stopped laptop within 5 s and a restarted one within 10 s (PlayMode with a fake server) | Planned |
| P3-NET-09 | Time budgets, single in-flight request per kind, request ids, late-answer dropping and the retry rules of section 5 in every Unity client | Ruhan | P3-API-04, P3-NET-08 | Fault-injection tests (delay, drop, duplicate, out-of-order) for ASR, action, orders, dialogue, TTS | Planned |
| P3-NET-10 | Version compatibility: `/health` fields, request headers, `content/ai/compat.json` and checks on both sides | Ruhan | P3-NET-08 | Mismatch test shows the banner and disables AI routes only | Planned |
| P3-NET-11 | Demo identity in the LAN profile: paired headset plays as the synthetic demonstration user; upload to the laptop's local database (Docker Postgres) or kept in the queue; real accounts untouched | Ruhan | P3-NET-07 | Session played over the LAN uploads and verifies on the laptop | Planned |
| P3-NET-12 | Optional WebSocket `/ws/headset/{session_id}` for generation progress, shutdown notice and quota notice; the headset works identically without it | Ruhan | P3-NET-08 | Contract test; headset test with the socket blocked | Planned |
| P3-NET-13 | Ollama Cloud configuration: one documented key name (`OLLAMA_API_KEY`, read through `Settings`, `SecretStr`), direct API or signed-in local relay, model names checked against `https://ollama.com/api/tags` at start, concurrency limit (3) in the gateway queue, per-route fallback kept | Ruhan | P3-NET-04 | Start check names the model and the mode; secret never logged (test) | Planned |
| P3-NET-14 | Scenario packages over the LAN: the headset asks the laptop for the next package (`/scenario-packages/next`), keeps the last good package cached on the device, and falls back to the authored pool | Ruhan | P3-SCN-06, P3-NET-09 | Offline start uses the cache or the authored pool with a message | Planned |
| P3-NET-15 | Optional HTTPS on the LAN: self-signed certificate made by `task demo-laptop --tls`, fingerprint pinned on the headset at pairing | Ruhan | P3-NET-07 | Pinned connection works; a different certificate is refused | Planned |
| P3-NET-16 | Team setup guide and check: `docs/setup/quest_laptop_cloud_demo.md` kept current and `task demo-laptop-check` (runs the laptop checklist and a scripted headset-simulator request through ASR → action → TTS) | Ruhan | P3-NET-04, P3-NET-07 | A teammate follows the guide on a fresh clone without help (recorded) | Planned |
| P3-NET-17 | Headset network test DC-15: discovery, pairing, voice round trip times (ASR, Gemma, TTS first sentence) on the real Wi-Fi, disconnect and reconnect mid-session, cloud key removed | Sannan | P3-NET-06, P3-NET-08, P3-NET-09 | Result file with times and pass/fail per row | Planned |

## 7. Dependencies and execution order

P3-NET-01 (decision record) → address policy (02) and laptop command (04) in parallel → endpoint (03), firewall (05), pairing (07), cloud configuration (13) → discovery (06), connection model (08) → budgets (09), versions (10), identity (11), packages (14) → optional socket (12) and HTTPS (15) → guide and check (16) → headset test DC-15 (17). Voice in both modes (P3-VOX-06) needs 08 and 09 before it is tested on the headset.

## 8. Frontend components

Status chip (top of the wrist panel and the main menu; green, amber, red with one-line reason), Settings → Server page (found laptops, manual keypad, pairing code entry, "Forget this laptop", latency readout), voice banner, version-mismatch banner (P3-UIX-11).

## 9. Backend and API changes

`task demo-laptop`, LAN profile settings (`lan_profile`, `pairing_required`, `beacon_enabled`, `bind_host`), pairing routes (`POST /pair/start` on the laptop console only, `POST /pair/claim`), `/health` version fields, optional `/ws/headset/{session_id}`, header checks. All new settings go through `server/config.py`; no secret in code (INSTRUCTIONS §5).

## 10. AI and voice integration

ASR, the text model and TTS are three separate stages (section 5). Only the text model goes to the cloud. The frozen speech evaluator prompt and scoring semantics are unchanged (INSTRUCTIONS §8j.13); voice in both modes is decided in [p3-05-voice-mode](p3-05-voice-mode.md) (D3). Gemma 4 is documented as multimodal with audio, but using it for recognition would change the approved architecture and is not planned.

## 11. Unity objects, prefabs, animations and assets

Status chip prefab, server settings page, keypad, pairing dialog (all from the P3-UIX kit).

## 12. Performance

The headset only records and sends audio and small JSON; no model runs on it. Polling is one small `/health` call every 5 s while connected. Audio upload is at most 15 s × 32 kB/s.

## 13. Error handling and edge cases

Section 5 table, plus: two laptops beaconing (the list shows both with their names); laptop on a guest Wi-Fi with client isolation (discovery fails; the guide says use a phone hotspot or the USB fallback); IP address changes (rediscovered by server id); headset sleeps (requests cancelled, status re-checked on wake); cloud answers after the session ended (dropped).

## 14. Testing

pytest for settings, pairing, headers and the start checks; Unity EditMode for the address policy and the status model; PlayMode with a fake server for disconnect and late answers; `task demo-laptop-check`; DC-15 on the real network.

## 15. Acceptance criteria

1. A teammate on a fresh clone gets from clone to a voice exchange with the patient on the Quest by following the guide only.
2. Unplugging the laptop's Wi-Fi mid-session leaves the engine state unchanged and the session playable in menu mode; reconnection needs no input.
3. No cloud key is in the APK, the repository or any log.
4. Nothing listens on a public network or through a public tunnel in the LAN profile.

## 16. Definition of Done

All NET tasks Done; ADR recorded; DC-15 recorded by Sannan; the guide verified by a teammate.
