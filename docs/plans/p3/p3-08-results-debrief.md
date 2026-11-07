# p3-08-results-debrief — Results screen, debrief and what next (RES)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented. Owner decision D4 (7 Nov 2026 UTC, ADR-0032): score out of 100, letter grade (A 90-100, B 80-89, C 70-79, D 60-69, F below 60, configurable), pass/fail, correct, incorrect, missed, critical failures, hints, time, personal feedback and the debrief; the existing scoring engine is reused; a critical safety failure decides the verdict whatever the number.

## 1. Objective and final user-facing behaviour

When a session ends, the room dims softly into a darker, translucent backdrop (no blur on Quest, see section 12), the ambience ducks, and a floating glass results panel rises in front of the player: final result (Passed, Passed with delay, Not passed, or Not completed), the score and grade, the end state in plain words, completed steps, missed steps, incorrect decisions, critical safety errors (highlighted first), hints used, time taken, and three to five improvement recommendations with their clinical explanation and source. Buttons: **Review details** (the full debrief with timeline and evidence), **Retry this scenario**, **New scenario**, **Main menu**. The numbers shown are exactly the engine's numbers.

## 2. Current implementation status

- `Scorer` produces `ScoreReport`: end state, `Result` (SUCCESS, SUCCESS_WITH_DELAY, FAIL), items, criticals, category scores (weights provisional), lateness, communication credit (experimental rubric, DT-245). **Verified.**
- `Debrief` (Core) and Unity `DebriefView`, `TimelineView`, `EvidenceDrawer`, `Citations` exist; the debrief station is placed on Sannan's branch and shows after Finish. **Not integrated on main.**
- Server `/debrief/.../rewrite` (AI wording, local model) exists with the template as fallback. **Verified (desktop).**
- No results screen, no grade, no retry/new/menu flow. **Missing.**

## 3. Existing code, assets, scenes and systems to reuse

`Scorer`, `ScoreReport.ProcessTotal/CommunicationTotal`, `Debrief.cs`, `content/debrief`, `content/score_items.csv`, `content/score_bands.csv`, `DebriefView`, `TimelineView`, `EvidenceDrawer`, `Citations` (`content/sources/citations.json`), `ResultsClient`, `SessionSync`, server session report pages.

## 4. Missing functionality

A result summary built only from the engine; a grade rule (D4); the transition; the results panel; the link to the detailed debrief; retry and new scenario; the verified badge.

## 5. Architecture and integrations

`ResultSummary.From(session, content)` (Core, pure): verdict, end state label, points earned/possible and percentage if D4 allows, grade, criticals, completed and missed goals, incorrect decisions, hints (count and stages), stage times, top recommendations (from the debrief's ordered items), seed and case id. The results panel renders it; the detailed debrief renders the existing `Debrief`. A test compares every number on the panel with `Scorer`. The optional AI rewrite changes wording only and is labelled as such; the template text is always available.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-RES-01 | Owner decision D4: what the panel shows as score and grade (verdict only; verdict plus percentage of the process score; or bands from `content/score_bands.csv`) and the wording | Owner | — | Decision row recorded | Done |
| P3-RES-02 | Core `ResultSummary.From` with xUnit proving equality with `Scorer` and `Debrief` for every golden session; adds `Score100`, `Grade` and `Passed` from P3-RES-10 | Ruhan | P3-RES-01, P3-EXM-08, P3-RES-10 | Golden summaries committed and checked | Planned |
| P3-RES-03 | Results transition: input frozen, environment dimmed with a stereo-safe fullscreen fade quad (no post-processing), ambience ducked by mixer snapshot, panel rises | Moiz | P3-SES-05, P3-UIX-03, P3-SND-01 | Transition costs no extra render pass (frame debugger check) | Planned |
| P3-RES-04 | Results panel: verdict badge, score ring, grade, sections (criticals first), hints, time, recommendations, seed and case id, buttons | Moiz | P3-RES-02, P3-UIX-05 | Panel equals summary (PlayMode test) | Planned |
| P3-RES-05 | "Review details" opens the existing debrief views restyled with the kit, inside the results space | Moiz | P3-RES-04, P3-SES-01 | Timeline and evidence drawer work from the results | Planned |
| P3-RES-06 | Retry (same case and seed), New scenario (new seed, same mode and procedure), Main menu, Quit | Moiz | P3-SES-07, P3-SCN-09 | Each path starts clean (leak test of P3-SES-07) | Planned |
| P3-RES-07 | Optional AI wording of recommendations through `/debrief/.../rewrite` with template fallback and a "reworded" label | Ruhan | P3-RES-02 | Offline shows the template; online shows the label | Planned |
| P3-RES-08 | Upload and server verification badge ("Verified by the server" when the replay matches; nothing shown offline) | Moiz | P3-RES-04 | Badge only after the server's replay result | Planned |
| P3-RES-09 | Tests: numbers equal Scorer for all goldens, transition, buttons, offline | Moiz | P3-RES-04, P3-RES-06 | Green in CI | Planned |
| P3-RES-10 | 100-point score and grade (D4) as a pure function of the existing `ScoreReport`: `Score100 = clamp(round(100 × (credits − penalties) / credits_of_the_case's_correct_path) − 5 × hints, 0, 100)` (the correct-path credits come from the case's golden correct script, so the number never depends on a second rubric); grade from `content/scoring/grades.json` (A ≥ 90, B ≥ 80, C ≥ 70, D ≥ 60, else F; `pass_mark` 60); **Passed = engine result not FAIL, no critical item, Score100 ≥ pass_mark**; with a critical failure the panel shows "Not passed: critical safety error" and the grade F whatever the number; validator for the thresholds | Ruhan | P3-RES-01 | xUnit over every golden: correct path gives 100, an empty session gives 0, a critical error gives Not passed with any number; thresholds validated (ordered, 0-100) | Planned |

## 7. Dependencies and execution order

D4 → summary → transition and panel → debrief link → buttons → optional rewrite and badge → tests.

## 8. Frontend components

Fade quad, results panel, verdict badge, score ring with count-up (P3-ANI-09), section lists, recommendation cards with citation chips, button row, verified badge.

## 9. Backend and API changes

None required; the session upload already returns verification status. P3-API-06 adds summary fields to the stored session report so the dashboard and the headset agree.

## 10. AI and voice integration

Voice commands on the results screen are not needed; the AI rewrite is optional wording only.

## 11. Unity objects, prefabs, animations and assets

Results prefab, fade sphere/quad material (stereo-safe), score ring mesh, icons, stings (P3-SND-05).

## 12. Performance

The dim is one transparent fullscreen quad per eye at fixed alpha (or the rooms' renderers switched off behind an opaque dark backdrop), no blur pass and no post-processing on Quest. The desktop tier may add a blur (P3-UIX-09).

## 13. Error handling and edge cases

Session with no actions; abandoned session; summary fails to build (fallback shows verdict and a message, log still saved); upload offline (queued); retry when the case is no longer in the pool (explain and offer new).

## 14. Testing

Core xUnit goldens; PlayMode equality and button flows; captures for review.

## 15. Acceptance criteria

1. The panel's numbers are identical to the engine's for every golden session.
2. Criticals always appear first.
3. Retry reproduces the same scenario; New gives a different one.

## 16. Definition of Done

All RES tasks Done; owner review of the panel captures; headset readability in DC-14.
