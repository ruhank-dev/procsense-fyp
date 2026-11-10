# Quest 2 + laptop server + Ollama Cloud: demo setup guide

_For Ruhan, Moiz and Sannan. Written 7 November 2026 (UTC) from owner decision D2 (ADR-0032) and plan [p3-18-networked-quest-runtime](../plans/p3/p3-18-networked-quest-runtime.md). The Quest runs the standalone APK and renders everything itself; the laptop runs the ProcSense server; the laptop talks to Ollama Cloud. No Quest Link, Air Link or SteamVR._

**Status of this procedure.** Steps 1-5 work today. Steps marked **(needs P3-NET-nn)** describe the target and become usable when that task is Done; until then the workaround under the step applies. Do not report a step as working on the headset until DC-15 has been run.

```
Quest 2 (APK)  ──Wi-Fi──►  laptop :8000 (ProcSense server: ASR, TTS, AI gateway)  ──internet──►  Ollama Cloud (Gemma 4 31B)
```

## 1. Clone and install (laptop)

```powershell
git clone https://github.com/ruhank-dev/procsense-fyp
cd procsense-fyp
task setup                                   # tools, hooks, checks
uv sync --project server --extra asr          # server + faster-whisper speech recognition
task voice-tools                              # Piper TTS and ffmpeg into build/voice-tools (if the task is missing, see tools/voice/README.md)
```

Install Ollama (https://ollama.com/download). On Windows it lives in `%LOCALAPPDATA%\Programs\Ollama` and does not start by itself: run `ollama serve` in its own terminal.

## 2. Ollama Cloud credentials (laptop only; never in the repository)

Choose **one**:

- **A. Signed-in local Ollama (simplest, used so far):** `ollama signin` once. The server's default `OLLAMA_URL=http://127.0.0.1:11434` then relays cloud models named `<model>-cloud` (the profile uses `gemma4:31b-cloud`). No key is stored by the project.
- **B. Direct API key:** create a key at https://ollama.com/settings/keys, then put it in the git-ignored `server/.env` (copied from `server/.env.example`) as `SPEECH_EVAL_API_KEY=...` with `SPEECH_EVAL_BASE_URL=https://ollama.com` and `SPEECH_EVAL_MODEL=gemma4:31b` (the API uses the names listed by `https://ollama.com/api/tags`). One key name for every route is P3-NET-13.

Keys do not expire; revoke a key you no longer use in the same settings page. The headset never receives a key.

Check the cloud from the laptop:

```powershell
ollama run gemma4:31b-cloud "Say ready"      # route A
task demo-orders-check                        # the project's own check of the cloud route and its fallbacks
```

Plans and limits (Ollama's pricing page, read 7 Nov 2026): billing per token; 3 concurrent requests on the free pay-as-you-go and Pro plans, 10 on Max and Team; requests beyond that queue and are refused when the queue is full. Ollama says cloud prompts are not used for training. Use invented people only in demonstrations.

## 3. Start the server on the laptop

Today (local only):

```powershell
task server-demo                              # synthetic demonstration profile, binds 127.0.0.1:8000
```

Then open http://127.0.0.1:8000/health and http://127.0.0.1:8000/ai/health in a browser on the laptop.

**(needs P3-NET-04)** `task demo-laptop` binds the Wi-Fi interface (`0.0.0.0:8000`), prints the laptop's LAN address, a pairing code and a QR code, and runs the checklist (Ollama, cloud model, ASR model, Piper, ffmpeg). Workaround today: `uv run --project server uvicorn server.main:app --host 0.0.0.0 --port 8000` with the demo profile variables set in the terminal.

## 4. Windows firewall and network

- Use a **private** network: your home Wi-Fi set to "Private" in Windows (Settings → Network & internet → Wi-Fi → your network → Private), or a phone hotspot. Venue and campus Wi-Fi often isolate devices from each other; the hotspot avoids that.
- Allow the port on the Private profile only (run PowerShell as administrator):

```powershell
New-NetFirewallRule -DisplayName "ProcSense laptop server" -Direction Inbound -Protocol TCP -LocalPort 8000 -Profile Private -Action Allow
# remove it after the demo:
Remove-NetFirewallRule -DisplayName "ProcSense laptop server"
```

  **(needs P3-NET-05)** `tools/setup/firewall_rule.ps1` does this and refuses on a Public network.
- Find the laptop's address: `ipconfig` → "Wireless LAN adapter Wi-Fi" → IPv4 Address (for example `192.168.1.20`). Never give the Quest `localhost` or `127.0.0.1` for the laptop: on the headset that means the headset itself (the only exception is the USB fallback below).
- Nothing is exposed to the internet: no port forwarding, no public tunnel.

## 5. Install the APK on the Quest

```powershell
adb devices                                   # the Quest, in developer mode, connected by USB
adb install -r artifacts\unity\ProcSense-demo.apk
```

Build it with `task unity-build -- demo` (the Editor closed), then `task unity-desktop` to put the Editor back on Windows.

## 6. Connect the Quest to the laptop

Both on the same Wi-Fi.

- **(needs P3-NET-02, P3-NET-03, P3-NET-06)** In the app: Settings → Server. The laptop appears in the list (beacon), or type its address once (`http://192.168.1.20:8000`); it is remembered. Today the build accepts only `https://` or `http://127.0.0.1` / `localhost` addresses (`ServiceSettings.IsAcceptable`) and the player setting forbids plain HTTP on the device, so a LAN address is refused.
- **(needs P3-NET-07)** Enter the six-digit pairing code the laptop shows.
- **USB fallback (works with today's address rule; HTTP permission to confirm in DC-15):** with the cable connected, `adb reverse tcp:8000 tcp:8000` makes `http://127.0.0.1:8000` on the headset reach the laptop's port 8000.

The status chip (P3-UIX-11) shows green "Laptop connected · Cloud AI ready", amber or red with the reason.

## 7. Sign in

**(needs P3-NET-11)** In the LAN profile the paired headset plays as the synthetic demonstration user. Today the headset sign-in uses the hosted service (device code approved on the web page); a laptop server can sign a headset in only if its `server/.env` holds the Supabase settings (the owner's secrets; ask the owner, never commit them).

## 8. Play

Main menu → Training or Test/Examination → surgery → the case loads. Menu or voice (Settings → Interaction mode). Hold the talk button and speak to the patient or a member of staff; the laptop transcribes (faster-whisper), Gemma 4 31B on Ollama Cloud interprets, the engine on the headset decides and scores, the laptop speaks the reply (Piper). Submit Patient to OT ends the examination; the results screen shows the engine's verdict, the 100-point score and grade.

## 9. When something fails

| Symptom | Check |
|---|---|
| Chip red "Laptop not found" | Same Wi-Fi? Firewall rule on the Private profile? `http://<laptop-ip>:8000/health` opens in the Quest browser? Guest Wi-Fi isolating devices (use a hotspot)? |
| Chip amber "Cloud AI unavailable" | `ollama serve` running? `ollama signin` or key set? Usage limit reached (Ollama settings page)? The laptop falls back to the grammar and the local model |
| "I didn't catch that" every time | Microphone permission on the Quest; ASR model installed (`uv sync --project server --extra asr`) |
| Version mismatch banner | Headset and laptop built from different commits: pull and rebuild one of them |
| Voice slow | Phone hotspot with poor signal; check the latency readout on the server page (DC-15 records typical times) |

## 10. Checklist for a new teammate

1. Clone and install (section 1). 2. Credentials (section 2). 3. `task server-demo` and the two health pages. 4. Firewall rule and laptop address (section 4). 5. APK installed (section 5). 6. Connect and pair (section 6). 7. Start Training, speak to the patient, hear an answer. 8. Submit and read the results. Record anything that did not work in `docs/testing/results/DC-15.md`.
