# P3 release candidate 1 (P3-QA-06)

Status: **candidate, not released.** Built on 16 November 2026 from the commit tagged `p3-rc1` (local tag on branch `myt1x54/p3-on-main`, not pushed). The release needs the Quest measurements below first.

| Artefact | File | SHA-256 |
|---|---|---|
| Demo APK | `artifacts/unity/ProcSense-demo.apk` (git-ignored) | `521c5a99820ca8b1997c1eaef64d9125ec6a76767ecbab232b50fc71ae76f41e` |

## Tested (automatically, on the build machine)

- Engine: 1,439 Core tests, including every golden run (`dotnet test tests/ProcSense.Core.Tests`).
- Unity EditMode: 440 tests; PlayMode: every class green, 20 capture and benchmark tools skipped (they need a person or the headset).
- Playthroughs (P3-QA-02): S1 and S2, training and examination, by menu only and by voice only (scripted recogniser): the engine's verdict, end state and completed goals; the menu runs also give the golden log hash.
- Contextual panels, results card, settings, sounds, laptop discovery beacon (server and headset sides), generated scenario fallback, intermediate level: their own tests.
- Repository checks run here: action gateway, sound list, interaction catalogue and matrix, plan index and step audit.

## Not tested yet (needs the headset or a person)

| Item | Row | Who |
|---|---|---|
| Stable 72 FPS in every benchmark view, warm (DC-13 part C) | P3-PRF-14 | Sannan |
| Glass panels render in both eyes (multiview scene of the diagnostic APK) | P3-UIX-03 | Moiz |
| Audio CPU time on the Quest (DC-13) | P3-SND-09 | Moiz |
| The headset lists the laptop within 5 s on the same Wi-Fi (DC-15) | P3-NET-06 | Moiz |
| Owner approval of the interface captures | P3-UIX-10 | Moiz |
| The generated sounds and the voice-mode examples heard and read in the headset | P3-SND-02, P3-VOX-04 | team |
| A real speech recogniser and interpreter in the voice playthroughs (the tests use a scripted one) | P3-QA-02 | team |

## How it was built

`Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod ProcSense.EditorTools.BuildScript.BuildQuest -variant demo` (the same as `task unity-build -- demo`). Install with `adb install -r artifacts/unity/ProcSense-demo.apk`.
