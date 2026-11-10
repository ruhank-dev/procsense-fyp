# Session wait and speech review

Verified 2026-10-29 23:51 Asia/Karachi in Procsense-fresh. This batch is uncommitted.

## What changed

Sys_Core now owns one SessionPresentation for the current session. It reuses the existing WaitOverlay and VoicePlayer, follows the active camera without abandoning its ownership hierarchy, and clears its UI/audio/listeners on session replacement, host replacement, finish or disable. Temporary review placement reuses the presenter; its fallback HUD also remains owned and is cleaned up on repeated placement.

Patient speech uses the real bedside head; visible M2 transport speech uses the actual patient/nurse/porter/doctor heads. Laboratory speech uses the records phone anchor. Absent/unbound speakers retain subtitles without inventing a spatial origin. VoicePlayer separates subtitle positioning from pooled audio sources, follows moving heads and no longer advances subtitle duration twice per frame. No Core/content/timing/scoring or package changes.

## Exact manual review

1. Use ProcSense > Open Patient Bay, then normal Play and start Practice VAR-S1-A. Ask name/date of birth from the tablet. Expect one subtitle below the face; with the current missing audio resources, expect a subtitle-only warning. Repeating Review > Place review panels must not duplicate the wait/speech UI.
2. Request a CT journey using the existing identification/escort workflow. Once Core reports arrival, expect the full wait display, then its small corner note. Keep using records/tablet controls: the display must not block input. Existing permitted time skip/result progression should produce the results-returned note.
3. Finish through Session > Finish session and upload. Expect wait/speech to clear immediately. Start another case: expect one fresh presenter, no old subtitle/voice queue or old result note.
4. Optional automated reproduction: live Test Runner, PlayMode filter SessionPresentationSceneTests. Expect 6 passes, covering missing clip/head, minimum subtitle duration, moving source, duplicate/review fallback, camera/session/host replacement, result notification and actual transport escort heads.

The current return-time display remains an estimate from content; authoritative Core ETA is DT-265. unity/Assets/Audio/Voice/Resources/Voice and build/voice are absent. Synthetic silent test clips establish playback mechanics, not production speech quality. Actual voice-pack, headset spatial audio/readability/reach/performance and lip sync acceptance remain outstanding. Missing final staff avatars keep subtitle-only fallback.

## Evidence

- Live EditMode: 152/152 passed, no failures/skips.
- Focused PlayMode: 16/16 across session presenter 6, voice 2, wait 2, clinical stations 2 and records 4. Final strengthened presenter rerun: 6/6, 20.5s, no failures/skips.
- Logs/SessionPresentation: scene-verified.json, voice-regression.json, wait-regression.json, panels-regression.json, records-regression.json, editmode.json, scene-preservation.json, editor-final.json and source-manifest.json.
- Four rendered captures inspected under ../artifacts/unity/: hud-wait-full.png, hud-wait-corner.png, hud-subtitles.png and hud-result-note.png. Desktop views clear; these do not establish headset acceptance.
- Sys_Core serialized blocks 22 -> 25; no original removed, only original SceneRoots changed. Other original blocks identical. Protected milestones/recovery/ignored profile hashes unchanged. Editor stopped, all four loaded scenes clean, zero missing scripts and no compilation failure.

## Commit gate and scope

Previous accepted checklist/debrief batch committed as e9cf36c57fdab0f8f27dd3c80cf8c4dc44b9d562, subject feat(unity): place checklist and debrief stations, no body. Author/committer Sannan <sannanshakeel04@gmail.com>, branch sannan47/main-sync-20261029; required task ci passed 198/198, exit0. Nothing pushed.

Current 16-file scope: SessionPresentationSetup.cs/meta, CameraHudAnchor.cs/meta, SessionPresentation.cs/meta, SessionPresentationSceneTests.cs/meta, ReviewPanels.cs, Sys_Core.unity, VoicePlayer.cs, PatientIdleView.cs, ClinicalPanelSceneTests.cs and this handoff/current-work-handoff.md/sannan-execution-plan.md. All remain uncommitted for acceptance. Preserve separate docs/plans/milestones.md, generated LiberationSans fallback font and three recovery files; never stage ignored AGENTS.md.

Proposed exact commit subject: feat(unity): integrate session wait and voice presentation
No body. Await explicit implementation acceptance and exact-message approval, then fresh task ci and staged-diff review. After this batch, audit the remaining physical S1 interaction/wristband route before implementing another bounded step.
Accepted by Sannan and exact proposed subject approved — 2026-10-30 00:29 Asia/Karachi. Fresh required task ci198/198 passed, exit0; exact16-file staged scope reviewed. Commit identity Sannan <sannanshakeel04@gmail.com>, branch sannan47/main-sync-20261029. No body or push. Physical identity audit remains excluded.
