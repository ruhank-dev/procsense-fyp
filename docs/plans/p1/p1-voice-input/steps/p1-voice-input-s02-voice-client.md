# p1-voice-input-s02 — Quest voice client

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Push-to-talk (hold B), mic capture (RECORD_AUDIO permission, 16 kHz mono, ≤ 15 s), async upload, transcript bubble + 'Say again', playback on the speaker's avatar + lip sync, FallbackController (health every 30 s, 2.5 s timeout → offline banner + menu/pre-rendered), settings (server URL, voice on/off).

## Why
Voice on the headset must never block play: every request has a timeout, the server's health is polled, and the menu/pre-rendered path takes over instantly when offline.

## How to implement
1. `Scripts/Voice/*` (MicCapture, AIClient Http with cancellation, FallbackController).
2. Mute playback while recording (echo).
3. Interrupt policy: talking while a character speaks queues the clip (T-VC4).
4. Talk-while-speaking policy: queue the clip until the character finishes (T-VC4).

## Verification
T-VC1–T-VC5 pass on the Quest.

## Files changed
`unity/Assets/_Project/Scripts/Voice/{MicCapture,AIHttpClient,FallbackController,TranscriptBubble}.cs`, Android manifest (RECORD_AUDIO), settings panel.

## Dependencies
p1-voice-input-s01.

## Common issues
Microphone silent on Quest → RECORD_AUDIO in the manifest and runtime permission; echo → mute playback while recording.

## Sources
Implementation Guide B-S4 Steps 1–6, T-VC1–T-VC5.
