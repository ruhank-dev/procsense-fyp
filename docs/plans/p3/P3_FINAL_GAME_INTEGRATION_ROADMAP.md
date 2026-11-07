# P3 — Final game integration roadmap

_Written 7 November 2026 (UTC) at `main` = `641f6b27`. Owner: Ruhan (planning). This is the last major planning phase: when every P3 task is Done, ProcSense is a complete, playable VR pre-operative training and examination simulator. P3 connects and finishes what exists; it does not rebuild it. Planning only: nothing in P3 is implemented by this document (INSTRUCTIONS §8j.1)._

**Read in this order:** [p3-audit.md](p3-audit.md) (what exists) → this roadmap (where we go, decisions, milestones, order) → the 17 plans → [p3-task-index.md](p3-task-index.md) (generated: every task in dependency waves). Quest performance: `docs/qa/quest2_performance_analysis.md` and its three companion reports.

## 1. What P3 delivers

A player puts on the Quest 2, signs in, and sees a polished glass main menu. They choose **Training** or **Test / Examination**, then **laparoscopic cholecystectomy** or **appendicectomy**. A clinically valid patient is drawn at random. They prepare that patient for theatre (identity, history, observations, examination, investigations and results, consent, fasting and medicines, ward checklist, escalation and handover) by contextual menus **or** by voice, switchable in Settings. Training guides them with a live checklist and free hints. The examination gives no guidance, offers costed hints behind a lightbulb, and ends when they **Submit Patient to OT**. A results screen shows the engine's verdict, score, missed and unsafe items, hints and recommendations, and leads to the detailed debrief, a retry, a new scenario or the menu. All of it runs at a stable frame rate on the headset with the approved hospital look, including the sunlight shafts and dust.

**Scope guard:** ProcSense trains **pre-operative preparation and patient readiness**, never the operation itself. Every plan keeps this wording.

## 2. Current status in one table (from the audit)

| Complete and verified (desktop/CI) | Implemented, not connected | Partial | Missing |
|---|---|---|---|
| Deterministic engine, about 100 verbs, scoring, debrief data, replay; backend (accounts, sessions, verification); voice pipeline (practice); tablet with 144 actions; approved URP environment; fresh-clone restore | Main menu and mode chooser (never shown); generated scenarios (practice, not in a flow); records workstation, ward/WHO/debrief stations, voice presenter, nurse visit, thermometer (Sannan's unmerged branch) | Modes (assessment cannot start: no assessment-approved case), physical interactions, characters (patient sex mismatch), animation, audio, settings, UI look, Quest build (unmeasured) | Game-flow state machine, procedure selection, contextual per-entity menus, voice mode presentation, training board, examination hint and submit, results screen, design system, sound set, Quest render tier |

## 3. The complete player journey (target)

```
Launch ─► Login (existing) ─► MAIN MENU ── Training ──┐
                                 │                     ├─► PROCEDURE: Cholecystectomy | Appendicectomy ─► (Training) LEVEL
                                 ├── Test/Examination ─┘
                                 ├── Settings  └── Quit
   ─► Scenario drawn (seeded; authored pool or generated in training) ─► LOADING ─► BRIEFING (patient, setting, task)
   ─► PLAY in PatientBay / Corridor / OT
         input: contextual glass panels (menu mode)  |  push-to-talk + reference cards (voice mode)   → one ActionGateway → engine
         Training: bulletin board, live ticks, free hints          Examination: no guidance, lightbulb hints (cost), no board
         Pause: resume · settings · restart · menu · quit
   ─► SUBMIT PATIENT TO OT (button or voice, confirmed) ─► engine closes the case and scores everything left undone
   ─► RESULTS (dimmed room, verdict, score, criticals, missed, hints, time, recommendations)
   ─► Review details (debrief, timeline, evidence) · Retry same · New scenario · Main menu
```

How each part works is specified in the plans: flow and gateway [p3-01](p3-01-session-orchestration.md), menus [p3-02](p3-02-main-menu-and-flow.md), scenarios [p3-03](p3-03-scenario-selection-generation.md), menu interaction [p3-04](p3-04-contextual-interaction-menus.md), voice [p3-05](p3-05-voice-mode.md), training [p3-06](p3-06-training-mode.md), examination [p3-07](p3-07-examination-mode.md), results [p3-08](p3-08-results-debrief.md), contracts [p3-15](p3-15-integration-contracts.md).

## 4. Architecture principles (binding for every P3 task)

1. **The engine decides** state, acceptance and score (ADR-0005). Menus, voice and gestures only ask.
2. **One door:** every input becomes an `ActionRequest` to the `ActionGateway` (P3-SES-03); there is no voice-only or menu-only clinical logic.
3. **Input mode is presentation:** switching between menu and voice never changes the session.
4. **Practice aids live behind `ModeGate`:** nothing practice-only may appear in the examination (P3-EXM-07 test).
5. **Speech never earns credit for a critical physical action** (ADR-0025); the evaluator prompt and scoring semantics stay frozen (INSTRUCTIONS §8j.13).
6. **The headset works without the server;** the server adds voice, generated cases, AI wording and verification.
7. **The approved environment is not redesigned;** performance work renders the same content more efficiently (p3-17).
8. **Clinical labels stay honest:** owner-attested approvals are called that; nothing is called clinician-validated.

## 5. Plans and task counts

| Plan | Code | Tasks | Main owners |
|---|---|---|---|
| [p3-01-session-orchestration](p3-01-session-orchestration.md) | SES | 11 | Moiz, Ruhan, Sannan |
| [p3-02-main-menu-and-flow](p3-02-main-menu-and-flow.md) | MNU | 11 | Moiz, Sannan |
| [p3-03-scenario-selection-generation](p3-03-scenario-selection-generation.md) | SCN | 11 | Ruhan |
| [p3-04-contextual-interaction-menus](p3-04-contextual-interaction-menus.md) | CTX | 8 | Moiz, Ruhan, Sannan |
| [p3-05-voice-mode](p3-05-voice-mode.md) | VOX | 12 | Ruhan, Moiz |
| [p3-06-training-mode](p3-06-training-mode.md) | TRN | 8 | Moiz, Ruhan |
| [p3-07-examination-mode](p3-07-examination-mode.md) | EXM | 9 | Ruhan, Moiz |
| [p3-08-results-debrief](p3-08-results-debrief.md) | RES | 9 | Moiz, Ruhan |
| [p3-09-interaction-coverage](p3-09-interaction-coverage.md) | INT | 10 | Sannan, Ruhan |
| [p3-10-characters-animation](p3-10-characters-animation.md) | ANI | 12 | Sannan, Moiz |
| [p3-11-assets-environment](p3-11-assets-environment.md) | AST | 10 | Sannan |
| [p3-12-ui-design-system](p3-12-ui-design-system.md) | UIX | 10 | Moiz |
| [p3-13-settings-accessibility](p3-13-settings-accessibility.md) | SET | 8 | Moiz, Sannan |
| [p3-14-sound-design](p3-14-sound-design.md) | SND | 11 | Moiz, Sannan |
| [p3-15-integration-contracts](p3-15-integration-contracts.md) | API | 8 | Ruhan, Moiz |
| [p3-16-qa-release](p3-16-qa-release.md) | QA | 8 | Ruhan, Moiz, Team, Owner |
| [p3-17-quest-performance](p3-17-quest-performance.md) | PRF | 14 | Ruhan, Sannan, Owner |
| **Total** | | **170** | the generated index has the per-owner totals and the 12 dependency waves |

Task ids are `P3-<CODE>-NN`. They are distinct from the older backlog items `P3-01` to `P3-05` in [../p3-backlog.md](../p3-backlog.md), which stay as they are (not part of this phase). Status per task lives in its plan's table (Planned, In progress, Done, Blocked); `tools/plans/p3_plans.py --check` (CI) validates every table, dependency and the index. Owners follow the confirmed split in [../frontend/front_end_load_distribution.md](../frontend/front_end_load_distribution.md): Moiz screens and engine-facing Unity logic, Sannan 3D, physical interaction, characters and the headset, Ruhan engine, content, backend and AI.

## 6. Decisions the owner must take

| ID | Decision | Options | Recommendation | Blocks |
|---|---|---|---|---|
| D1 | Which cases may be played in the examination? Today none (all 63 playable cases are `APPROVED_FOR_TRAINING`; only P0-tier cases may be `APPROVED_FOR_ASSESSMENT`) | (a) Owner-attested promotion of the 16 P0 cases (S1 9, S2 7) to `APPROVED_FOR_ASSESSMENT`, recorded as owner-attested, not clinician-validated; (b) a new examination-demo status for P0-P2 cases; (c) keep the gate and wait for a clinician | (a): smallest change, keeps the engine rule, enough variety for random examination draws; the label stays honest | P3-SCN-07, the whole examination |
| D2 | Network profile for the demonstration | (a) Cloud-first demo profile as today (Gemma via Ollama Cloud, needs internet) with local fallbacks; (b) fully offline (local ASR, grammar orders, local Qwen dialogue) | Keep (a) as the default demo and make (b) a tested profile (P3-VOX-11) so the demo survives without internet | P3-VOX-11 |
| D3 | Voice in the examination | (a) Allowed, with every confirmation kept and no hint text; (b) menu only in the examination | (a): the request asks for both modes in both; the engine still decides and confirmations prevent mis-hearing | P3-VOX-06 |
| D4 | What the results show as score/grade | (a) Verdict only; (b) verdict plus process score percentage (weights are provisional); (c) bands from `content/score_bands.csv` | (b) with the word "provisional" until weights are approved; the verdict always comes from the engine | P3-RES-02 |
| D5 | Examination hints | Cost per hint, maximum, whether tier 2 costs more | 1 point-equivalent penalty per hint, maximum 3, tier 2 double; shown before use | P3-EXM-01 |
| D6 | Where the examination ends | (a) At **Submit Patient to OT** (pre-operative scope); (b) after the theatre WHO steps | (a); theatre steps stay a training extension | P3-EXM-05 |
| D7 | Which performance options to apply | From `docs/qa/quest2_optimization_options.md` after DC-13 measurements | Decide after measurements; tier separation and intermediate texture Auto first | P3-PRF-05 onwards |

## 7. Milestones and implementation order

| Milestone | Content (task groups) | Exit check |
|---|---|---|
| **P3-M0 Foundations** (waves 1-2) | P3-SES-01 merge of Sannan's branch; P3-SES-02 engine Check; owner decisions D1-D6; P3-SCN-02 catalogue; P3-CTX-01 interaction catalogue; P3-TRN-01/02 guide data; P3-UIX-01 tokens; P3-SND-01 mixer; P3-PRF-01 diagnostic APK and P3-PRF-02 baseline measurement; P3-QA-01/04 matrix and headset items | CI green; decisions recorded; DC-13 part A recorded |
| **P3-M1 Playable loop (menu mode, S1)** | Gateway and flow (SES-03 to SES-08), menu screens (MNU-01 to MNU-07), picker (SCN-03 to SCN-05), contextual panels (CTX-02 to CTX-06), glass shader and kit (UIX-02 to UIX-05), engine submit (EXM-05) and results (RES-02 to RES-06) | Desktop: launch → menu → S1 → submit → results → retry without restart |
| **P3-M2 Both procedures, both modes** | Examination pool (SCN-07), hints (EXM-01, EXM-03), submit UI (EXM-04), abandon (EXM-06), leak audit (EXM-07), training board (TRN-03 to TRN-07), S2 bay variant (AST-08), patient profiles and swap (AST-02, AST-03) | Eight golden playthroughs pass in menu mode |
| **P3-M3 Voice mode** | VOX-02 to VOX-12, API-02 to API-05 | Voice-only S1 and S2 playthroughs pass (practice; examination per D3) |
| **P3-M4 Presence and polish** | Staff and interactions (INT, AST-04/05), animation (ANI), sound (SND), settings (SET) | Coverage check green; owner approves captures and clips |
| **P3-M5 Quest performance** | PRF-03 to PRF-14 | DC-13 part C: stable 72 FPS in the benchmark views, or an owner-accepted exception |
| **P3-M6 Release** | QA-02, QA-03, QA-05 to QA-08, API-06 to API-08 | Release readiness decision |

The full ordered list with every dependency is the generated [p3-task-index.md](p3-task-index.md) (12 waves; wave 1 has 25 tasks that need no other P3 task). Critical path: merge → gateway → flow → menu/picker → submit/results → examination pool → voice → playthroughs → release candidate.

## 8. Remaining creative assets (summary)

- **3D:** four more patient avatars (female adult, older female, older male, young female), final nurse, doctor, anaesthetist, receptionist, porter; glucometer, ECG machine, sample tubes and labels, specimen bag, oxygen mask/cannula, consent clipboard and pen, gown/jewellery/stockings props, theatre reception handover point, menu backdrop, ED bay variant, UI meshes (p3-11).
- **Animation:** standard controller, look-at, speaking/listening/acknowledge states, lip sync, patient reactions (flinch, offer arm, nod, sign, sit up), staff actions (walk, hand item, write, push bed), equipment motions, UI motion presets, transitions (p3-10).
- **Sound:** mixer, menu music and stings, four room ambiences, full UI set, training and examination cues, voice cues and failure phrases, foley, equipment sounds (p3-14).
- **UI:** tokens, font, glass shader, component kit, icon atlas, feedback components, every screen of p3-02, p3-04 to p3-08 and p3-13 (p3-12).

## 9. Validation of this plan (phase 16 of the request)

Checked on 7 November 2026:

| Check | Result |
|---|---|
| Every requested final-game feature has tasks | Yes: menu (MNU), modes (SES, EXM, TRN), procedures (MNU-04, SCN), scenario generation (SCN), orchestration (SES), both input modes (CTX, VOX), mode switching (VOX-02, SET-04), training board (TRN), examination hint and submit (EXM), results (RES), interactions (INT), animation (ANI), assets (AST), UI (UIX), settings (SET), sound (SND), integration (API), QA (QA), Quest performance (PRF) |
| Dependencies | Machine-checked: 170 tasks, every dependency exists (P3 task or existing P0 step), no cycle (`tools/plans/p3_plans.py --check`, tested by `tools/ci/test_p3_plans.py`) |
| Duplicated functionality | Existing systems are reused by name in each plan's section 3; the tablet is kept as the full list rather than replaced; Sannan's branch is merged, not redone; open P0 steps are depended on, not copied |
| Contradictions | Examination ends at submission (D6) consistently in EXM, RES, INT; voice in examination only per D3 in VOX and QA; no plan changes clinical labels; desktop look unchanged in PRF and UIX |
| Frontend/backend/AI/voice connections | Every flow has a contract and an event flow in p3-15 |
| Clinical workflow coverage | Coverage matrix in p3-09 maps every engine verb group to an interaction; P3-INT-10 makes it a CI check |
| Training and examination end to end | Journey in section 3; eight golden playthroughs (P3-QA-02) prove it once built |
| Assets, animations, sounds, UI elements | Listed in section 8 with owners |
| Compatibility with the scenes and Quest deployment | Rooms, bake and lighting unchanged; new objects follow the Quest budgets of p3-17; glass and shafts are stereo-safe by plan |
| Acceptance criteria testable | Each plan's section 15 is measurable; headset items DC-13 and DC-14 exist |

## 10. What P3 does not change

Clinical approval status; the frozen speech evaluator; the deterministic engine's existing rules (only additive verbs: `request_hint`, `submit_patient`, and the side-effect-free `Check`); the approved environment, lighting, materials and assets; the HDRP experiment (cancelled; never merged into the Quest build); the team's direct-to-main workflow.
