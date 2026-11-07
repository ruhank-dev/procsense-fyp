# p3-16-qa-release — Acceptance testing and release of the final game (QA)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

The final build installs on a Quest 2 from a fresh clone, starts at the menu, and lets a player complete training and examination sessions for both procedures in both input modes, with results that match the engine, at a stable frame rate. Every claim in the release notes is backed by a test result or a headset result file.

## 2. Current implementation status

`task ci` 202 checks green; GitHub CI 5 jobs green on `641f6b27`; fresh-clone restore verified (4,061 files); Unity EditMode 204/204, PlayMode 239 passed, 7 skipped by design (fresh clone, 4 Nov 2026); Dev APK builds; headset checks DC-01 to DC-12 prepared for Sannan, none recorded for the new environment; release procedure p0-evaluation-performance-s08 open; usability study s05 open. **Partial.**

## 3. Existing code, assets, scenes and systems to reuse

`tools/ci/run_all.py`, `.github/workflows/ci.yml`, `tools/unity/run_unity.py`, `tools/setup/run_unity_tests.ps1`, `tools/setup/restore_environment.ps1`, `tools/setup/compare_captures.py`, `unity/Tools/Rooms/env_captures.ps1`, `docs/testing/external_testing.json` and `tools/plans/external_testing.py`, `docs/RELEASE.md`, golden logs.

## 4. Missing functionality

A P3 acceptance matrix; automated end-to-end playthroughs (both procedures, both modes, both input modes); headset items for performance (DC-13) and the P3 end-to-end run (DC-14); visual regression captures for new screens; release candidate and readiness review.

## 5. Architecture and integrations

**Acceptance matrix (P3-QA-01 keeps it as a table in this folder):**

| Area | Automated (Editor/CI) | Headset (Sannan) |
|---|---|---|
| Fresh clone, restore, import, package resolution | restore script, CI | APK from the fresh clone |
| Scene loading, menu navigation | PlayMode flow tests | DC-14 |
| Training S1/S2, menu input | PlayMode golden playthroughs | DC-14 |
| Examination S1/S2, menu input | PlayMode goldens (after D1) | DC-14 |
| Voice input (practice, and examination per D3) | PlayMode with fake recogniser; server contract tests | DC-08 to DC-11, DC-14 |
| Mode switching mid-session | State-hash invariance test | DC-14 |
| Randomised scenarios and retry | Picker tests, retry determinism | — |
| Characters, animation | Presenter tests, captures | DC-12, DC-14 |
| Equipment interactions | Per-interaction PlayMode tests, matrix check | DC-14 |
| Checklist, hints, scoring, debrief | Core goldens, numbers-equal tests | DC-14 |
| Settings, audio | Persistence and snapshot tests | DC-14 |
| Performance | Desktop profiler only (not a Quest figure) | DC-13 |
| Visual regression | Fixed-camera captures compared with `compare_captures.py` | DC-12 |

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-QA-01 | Acceptance matrix page with links to each test and DC item, kept current | Ruhan | — | Page in this folder, linked from the roadmap | Planned |
| P3-QA-02 | Automated golden playthroughs in PlayMode: S1 and S2 × training and examination × menu-only and voice-only (fake recogniser); verdicts equal the Core goldens | Moiz | P3-INT-10, P3-EXM-04, P3-RES-04, P3-VOX-12 | Eight playthroughs green in CI | Planned |
| P3-QA-03 | Fresh-clone rehearsal: restore script, Unity import, all tests, APK build, recorded in `docs/qa/` | Ruhan | P3-AST-09 | Report written with commit hash | Planned |
| P3-QA-04 | Headset items: DC-13 (Quest 2 performance benchmark, prepared now) and DC-14 (P3 end-to-end on the headset) in `docs/testing/external_testing.json` | Ruhan | — | Items generated in `SANNAN_EXTERNAL_TESTING.md` | Planned |
| P3-QA-05 | Visual regression: fixed cameras for every P3 screen and the three rooms; compare against approved captures in CI-independent review | Ruhan | P3-UIX-10 | Comparison sheet in `docs/qa/` | Planned |
| P3-QA-06 | Release candidate: Demo APK from a tagged commit, checksum, release notes listing tested and untested items | Moiz | P3-QA-02, P3-PRF-14, p0-evaluation-performance-s08 | Tag and artefact exist | Planned |
| P3-QA-07 | Usability session with at least five participants (think-aloud, tasks from the matrix), findings fixed or registered | Team | P3-QA-06, p0-evaluation-performance-s05 | Findings report and fixes | Planned |
| P3-QA-08 | Release readiness review by the owner against section 15 | Owner | P3-QA-03, P3-QA-07 | Decision recorded | Planned |

## 7. Dependencies and execution order

Matrix and DC items now; playthroughs once the flows exist; fresh-clone rehearsal after the asset work; release candidate after performance acceptance; usability; readiness review.

## 8. Frontend components

None.

## 9. Backend and API changes

None beyond P3-API.

## 10. AI and voice integration

Voice is tested with recorded and synthetic audio on desktop (labelled as such); real speech on the headset is DC-08 to DC-11 and DC-14. Synthetic results are never reported as independent validation.

## 11. Unity objects, prefabs, animations and assets

Capture cameras for regression (`env_captures.ps1` pattern).

## 12. Performance

DC-13 is the only source of Quest frame-rate numbers; desktop profiler numbers are labelled desktop.

## 13. Error handling and edge cases

Flaky timing tests on a busy machine (run CI on a quiet machine, as recorded in the team notes); headset firmware differences (record firmware version in results).

## 14. Testing

This plan is the testing plan; every other plan's section 14 feeds the matrix.

## 15. Acceptance criteria

Release readiness: (1) all P3 tasks Done or explicitly deferred by the owner; (2) CI green on the release commit; (3) eight golden playthroughs green; (4) DC-13 shows the agreed frame-rate target in the measured views; (5) DC-14 passed; (6) no open critical usability finding; (7) licences complete; (8) clinical status unchanged and stated honestly (owner-attested, not clinician validated).

## 16. Definition of Done

Readiness decision recorded; release artefact and notes published by the owner; `docs/context/ProcSense_Current_State.md` updated.
