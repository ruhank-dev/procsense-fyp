# ADR-0032 — P3 owner decisions D1-D7: every case in both modes, a laptop-and-cloud network demo, voice in both modes, a 100-point grade, costed hints, submit ends the examination, and a Quest render tier

**Date:** 7 November 2026 (UTC) · **Status:** Accepted (7 November 2026, owner Ruhan Kamran, prompt 233 in `docs/previous_prompt.md`) · **Related:** ADR-0005, ADR-0006, ADR-0025, ADR-0026, ADR-0028, ADR-0031; `docs/plans/p3/P3_FINAL_GAME_INTEGRATION_ROADMAP.md` section 6; P3-SCN-01, P3-VOX-01, P3-EXM-02, P3-RES-01, P3-NET-01, P3-PRF-04; DT-271 to DT-281

## Context
The P3 plan of 7 November 2026 left seven decisions to the owner (roadmap section 6, first version): the examination case pool (no case could start: all 63 playable cases were APPROVED_FOR_TRAINING and only P0 cases could ever be APPROVED_FOR_ASSESSMENT), the network profile, voice in the examination, the grade, hint costs, the end of the examination and the Quest optimisations. The recommendations then were: owner-attested promotion of the 16 P0 cases only; cloud-first demo with an offline profile; voice allowed; verdict plus a provisional percentage; 1 point per hint, at most 3; end at Submit Patient to OT; decide performance after measurement.

## Decision
1. **D1:** every supported scenario (all 63 playable cases, subject to the existing validity checks) may be played in Training and Examination. The owner states that the clinical reviewer verified the scenario content and authorises its use in the academic examination simulation. The approval records will cite that authorisation; no clinician name or signature is invented. The P0-only assessment rule is lifted for this use (P3-SCN-07). DRAFT, CLINICAL_REVIEW and generated cases stay out of the examination.
2. **D2:** the demonstration is network-connected. Standalone Quest APK (renders everything, runs the engine) ⇄ Wi-Fi/LAN ⇄ laptop running the existing ProcSense server (ASR, TTS, AI gateway, fallbacks) ⇄ Ollama Cloud (text model). No Quest Link, Air Link, SteamVR or PC rendering. The cloud key stays on the laptop (environment variable or a signed-in local Ollama). The Quest never uses `localhost` to mean the laptop (except through `adb reverse`). Plan: p3-18 (NET).
3. **D3:** voice works in both modes, selectable in Settings, and every voice request is validated exactly like its menu action through the same gateway; speech never earns credit for a critical physical action (ADR-0025 unchanged).
4. **D4:** results show a score out of 100, a letter grade (A 90-100, B 80-89, C 70-79, D 60-69, F below 60, configurable), pass or fail, correct, incorrect and missed steps, critical failures, hints, time, personal feedback and the debrief. The 100-point score is a normalisation of the existing `Scorer` result, not a new scoring system. A critical safety failure or a FAIL end state is "Not passed" whatever the number (P3-RES-10).
5. **D5:** at most three hints per examination, five points each from the 100-point score, configurable; free in training; identical for voice and glass.
6. **D6:** the confirmed Submit Patient to OT (patient menu or voice) ends the examination; the engine assesses everything; submission never implies a pass.
7. **D7:** safe, visual-preserving Quest optimisations may be investigated and applied on an isolated branch; anything that changes the look (an image-based exterior, foveation level, light removal) needs comparison captures and the owner's approval first. Implemented on `quest-perf/d7-optimisations` (7 Nov 2026): an Android-only pipeline asset whose renderer lets URP render straight into the eye buffer (desktop asset unchanged), stereo-safe shaft and dust shaders with the effects restored on Quest, the OpenXR foveation feature available at level 0, diagnostic APKs with run-time toggles and CSV logging.

## Alternatives
- D1 promotion of the 16 P0 cases only: rejected by the owner (too little variety; the reviewer verified all content).
- D2 fully offline demo: kept only as the degraded fallback (P3-VOX-11).
- D2 PC-rendered VR (Link/Air Link): rejected by the owner; the APK must stand alone.
- D4 verdict only or a provisional percentage: replaced by the 100-point score and grade.
- D7 applying every optimisation at once: rejected; measure, then change one thing at a time.

## Consequences
- Examination play needs P3-SCN-07 (content and loader change, assessment packages, tests). The documentation gap that the signed review is not in the repository is DT-276 (P3-SCN-12).
- The APK must accept private-network HTTP addresses in Dev/Demo builds (P3-NET-02), pair with the laptop and show a connection chip (P3-NET-07, P3-UIX-11).
- Examination voice is switched on only after the voice/menu equivalence test (P3-VOX-13).
- `content/scoring/grades.json` (thresholds, pass mark, hint penalty and limit) becomes content with a validator.
- Quest measurements (DC-13) decide every visible optimisation; the shafts and dust are back on Quest by default, their cost is measured with configuration B3.

## Revisit if
A named clinician or the university requires independent clinical sign-off for assessment use; the venue's Wi-Fi blocks local traffic (then the USB `adb reverse` path or a phone hotspot); Ollama changes its cloud terms, limits or models; DC-13 shows the shafts and dust cannot fit the 72 FPS budget.
