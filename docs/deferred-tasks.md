# Deferred tasks and dependencies

One register for everything that was **postponed, left outstanding, blocked or depends on something else** — so nothing is forgotten.
Maintained under INSTRUCTIONS.md §8g. Update it in the same change whenever a task is deferred, started, finished, blocked, unblocked or re-scoped.

**Last updated:** 09 October 2026 (UTC) · **Walking skeleton 1 position:** steps 1–7 approved (content, clock/RNG, session/log, loader/state, observable/CLI/replay, identity/allergy, history questions); step 7 (history questions) approved; step 8 (sensors and vitals) is built and awaiting approval.

## Status meanings

| Status | Meaning |
|---|---|
| **Pending** | Planned (usually in a named step file), not started |
| **In progress** | Work has started but is not finished or not yet approved by the owner |
| **Completed** | Done **and** verified or approved. Writing a plan or a note is never enough on its own |
| **Blocked** | Cannot start or finish yet. The blocker is named in the row (another step, an install, or a decision from the owner/hospital) |

## Register

### A. Wristband, identity and Step 1 follow-ups

| ID | Task | Status | Planned in | Depends on / blocker | Notes |
|---|---|---|---|---|---|
| DT-001 | Robust spoken identity validation: normalise answers, extract names and dates across phrasings, report Match / Mismatch / Missing / Ambiguous | Pending | [p0-simulation-workflow-s19](plans/p0/p0-simulation-workflow/steps/p0-simulation-workflow-s19-robust-spoken-identity.md) | — (step 6 approved) | Today's check is a known shortcut in `IdentityCheck.cs` (name + spelled-out date words). Ambiguous dates are reported, never guessed |
| DT-002 | Trust/anxiety ±1 values review (`SR-0035`) | **Completed** | — | — | Owner accepted them as simulation mechanics on 09 Oct 2026; register row updated. Reopen only if the owner asks |
| DT-003 | Critical scoring for rejected safety-critical actions (tests/medicines/transfer before identity; confirming despite a mismatch) | Blocked | [p0-scoring-debrief-s01](plans/p0/p0-scoring-debrief/steps/p0-scoring-debrief-s01-score-items-rubrics.md), s02 | Scoring engine not built (needs p0-clinical-logic-s01) | Step 6 already logs the rejection (`IDENTITY_NOT_CONFIRMED`, `ST1_MISMATCH`); no score item exists yet |
| DT-004 | Minor score items from Step 1 flags: skipped introduction, unnecessary mismatch report, closed-question identity check | Blocked | p0-scoring-debrief-s01 | Same as DT-003 | Flags already set by Step 6: `introduced`, `unnecessary_mismatch_report`, `identity_asked_closed` |
| DT-005 | Patient-location checks (`PATIENT_ELSEWHERE`, test T-K4) | Pending | [p0-simulation-workflow-s13](plans/p0/p0-simulation-workflow/steps/p0-simulation-workflow-s13-locations-transport-escort.md) | Locations model | Today the patient's location is the constant `patient_bay`; every verb assumes she is present |
| DT-006 | Nurse-assisted wristband replacement after a mismatch is reported (engine: nurse events, corrected band at once, re-read and re-confirm) | Pending | [p0-simulation-workflow-s18](plans/p0/p0-simulation-workflow/steps/p0-simulation-workflow-s18-wristband-mismatch-nurse-resolution.md) | — (step 6 approved) | `report_mismatch` currently only records a flag. Owner decision: instant, no delay (ISS-0002 closed) |
| DT-007 | 3D nurse who enters the room from outside and replaces the wristband | Blocked | [p0-characters-s08](plans/p0/p0-characters/steps/p0-characters-s08-nurse-entry-and-wristband-replacement.md) | DT-006; Unity/Quest install (DT-024); bay doorway and anchors in p0-environment-rooms; p0-characters-s04/s05/s06 | Greybox capsule nurse can come earlier in the walking skeleton |
| DT-008 | VR interaction for the wristband workflow + **tablet fallback** for a band that cannot be grabbed (T01-5) | Blocked | [p0-vr-interaction-ui-s12](plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s12-wristband-nurse-interaction.md) (base read in s01) | DT-006, DT-007, DT-024 | Tablet and hand paths must give the same log hash |
| DT-009 | Nurse dialogue lines and subtitles for the workflow | Blocked | [p0-voice-audio-s06](plans/p0/p0-voice-audio/steps/p0-voice-audio-s06-wristband-nurse-lines.md) | DT-006; voice catalogue steps s01–s04 | Lines are procedural; wording authored at implementation and flagged for review |
| DT-010 | Missing-wristband scenario (no band on the wrist, nurse applies one after verification; allergy-band variant) | Pending | p0-simulation-workflow-s18 (engine); characters-s08, vr-interaction-ui-s12, voice-audio-s06 (presentation) | — (step 6 approved) | Owner decision 09 Oct: band applied instantly after verification (ISS-0002 closed); request before verification is rejected `VERIFY_IDENTITY_FIRST` |
| DT-011 | Test-only fixture cases `TEST-ID-MISMATCH`, `TEST-ID-MISSING-BAND`, `TEST-ID-MISSING-BAND-ALLERGY` and their golden scripts | Pending | p0-simulation-workflow-s18 | DT-006 | Not among the 17 P0 cases. Step 6 already uses an in-test mismatch card |
| DT-016 | Practice-mode red banner "Identity not confirmed" and green tick | Pending | p0-vr-interaction-ui-s01 / s12 | Unity install | Core already exposes `IdentityConfirmed` and `LastRejection` |
| DT-017 | Emergency identification route for an unconscious or unknown patient (S2) | Pending | p0-scenario-s2 / workflow | — (policy question closed by the owner's approval, 09 Oct 2026) | Use the walkthrough route (wristband + notes + relative + interpreter); life-saving treatment must not be withheld |
| DT-018 | Hospital-policy values for Step 1: identifier set, allergy-band colour, unknown-patient procedure, triage system | **Completed** | docs/research (Part I decisions) | — | Closed by the owner on 09 Oct 2026 (approval covers the 15 institutional decisions). The walkthrough's values are used (name, DOB, MRN; escalation route per walkthrough). The allergy-band colour is not given in the walkthrough: it is chosen and proposed to the owner when the asset is built |

### B. Skeleton 1 and engine follow-ups

| ID | Task | Status | Planned in | Depends on / blocker | Notes |
|---|---|---|---|---|---|
| DT-012 | **Official golden runs** for skeleton 1 (scripts + expected key facts + expected log hash for seed 42; `task golden`; prove a changed action breaks the hash) | Pending | [p0-simulation-workflow-s04](plans/p0/p0-simulation-workflow/steps/p0-simulation-workflow-s04-golden-run-skeleton1.md) | Steps 7 and 8 (history, vitals) | `tests/golden/*.actions.json` today are **sample scripts only**, not validated golden runs |
| DT-013 | Observable shows vitals only for attached sensors | In progress | [p0-simulation-workflow-s03](plans/p0/p0-simulation-workflow/steps/p0-simulation-workflow-s03-step3-sensors-vitals.md) | Owner approval of step 8 | Built 09 Oct 2026 (69 tests pass); becomes Completed on approval |
| DT-014 | Replace the placeholder `order_blood_test` verb with the real investigation-request verbs, keeping the identity gate | Pending | p0-clinical-logic-s06 / s11 | Clinical-logic phase | Placeholder lives in `IdentitySubsystem.Init` and is marked PLACEHOLDER |
| DT-015 | Wrong-blood consequence when `risk.wrong_blood` is armed | Pending | p0-physiology-events-behaviour-s09, p0-diagnostic-transitions-s04 | Events and lab-sample steps | Step 6 only arms the flag |
| DT-021 | EXP-CORE-1: identical hashes on PC and Quest | Blocked | p0-core-engine README | Quest + Unity | |
| DT-022 | EXP-CORE-3: zero allocation per tick; cost of building the observable on every query | Pending | p0-core-engine-s05 | — | Observable is built on demand, not cached |
| DT-023 | Check C# 9 records (`IsExternalInit` shim) and the JSON reader under Unity 6 IL2CPP | Blocked | p0-vr-platform-s02 | Unity install (DT-024) | |

### C. Environment, tooling and documentation

| ID | Task | Status | Planned in | Depends on / blocker | Notes |
|---|---|---|---|---|---|
| DT-019 | ISS-0001: BP cuff cycle (walkthrough 30–40 s vs Guide 20 s) | **Completed** | docs/research/issues.md | — | Closed by the owner 09 Oct 2026; walkthrough value stands (SR-0034 now VERIFIED) |
| DT-024 | Install Unity 6 LTS (Android build support) and set up the Quest | Blocked | p0-vr-platform-s01, p0-foundation-s04 | Owner's machine | Gates skeleton steps 10–15 |
| DT-025 | Install go-task (`winget install Task.Task`) so `task test` / `task sim` work | Pending | Taskfile.yml exists | Owner's machine | Raw `dotnet` / `uv` commands work meanwhile |
| DT-026 | Fix `tools/plans/milestones.py` crash on Windows consoles (`UnicodeEncodeError`) | Pending | tools/plans | — | Workaround: `PYTHONUTF8=1` |
| DT-027 | AGENTS.md routes to `docs/invariants/*.md` and `docs/docs/index.md`, which do not exist | Pending | AGENTS.md | Decide: create them or fix the routing | INSTRUCTIONS.md is used as the rule set meanwhile |
| DT-028 | Foundation steps s01–s04 (remote/LFS/backup, CI, templates, dev-environment guide + `task doctor`) | Pending | p0-foundation | — | The remote repo exists (ruhank-dev/procsense-fyp); LFS, CI and backup are not set up |
| DT-029 | ADR-0001 still says "Proposed" although the code follows it | Pending | docs/decisions | Owner confirms | |
| DT-030 | Skeleton step 6 (identity/allergy) approval and commit | **Completed** | p0-simulation-workflow-s01 | — | Approved 09 Oct 2026; committed as 9e71540 |
| DT-037 | Test-run output files `result.json` and `result.session.jsonl` were committed in the repo root (and `bad.json`, `bad.session.jsonl` sit untracked); remove them from git and add them to `.gitignore` | Pending | .gitignore | Owner runs `git rm --cached result.json result.session.jsonl` | Created by running the CLI examples in the project root; use `--out "$TEMP/…"` or a scratch folder instead |
| DT-038 | Ponytail removal from git: local history rewrite done (backup branch `backup-before-history-rewrite`); needs a check, the local skill files restored, and a force-push | In progress | .gitignore | Owner runs the commands in the hand-off (the agent's shell was blocked after the rewrite) | `.gitignore` entry added. `skills-lock.json` was left in the repo (only the skill folder was named) |
| DT-039 | Step 7 (history questions) | **Completed** | [p0-simulation-workflow-s02](plans/p0/p0-simulation-workflow/steps/p0-simulation-workflow-s02-step2-history-disclosure.md) | — | Approved 09 Oct 2026; 58 tests pass. Left for later: fasting-rule evaluation (clinical-logic), pregnancy eligibility and private-ask option (behaviour), `ask` for allergies stays the Step 1 verb `ask_allergies` |
| DT-040 | Pulse regularity (palpating the radial pulse) and reading durations (RR count, temperature) for Step 3 | Pending | p0-simulation-workflow-s03 follow-up / p0-clinical-logic | Content values for durations; physiology | `heart_rhythm` is in the case but no verb shows it yet; readings are instant (marked `shortcut` in `ObservationsSubsystem`) |
| DT-031 | Earlier history (6–7 Oct 2026: when the plans, ADR-0001 and phase review were written) is not in any session record | Blocked | docs/context/ProcSense_Change_Report.md §G | Owner to supply or confirm | Documented from the plan files only; nothing invented |

DT-034, DT-035 and DT-036 were owner confirmations needed to keep the documentation honest (034 and 035 answered 09 Oct 2026):

| ID | Task | Status | Planned in | Depends on / blocker | Notes |
|---|---|---|---|---|---|
| DT-034 | Confirm whether the 09 Oct approval statement also closes the 15 institutional decisions (Change Report §F, Team Board "Decisions needed") | **Completed** | docs/context | — | Owner confirmed 09 Oct 2026: it closes them (and ISS-0001 / ISS-0002). Team Board items ticked |
| DT-035 | Confirm whether the Team Board dates (gates, 31 Dec submission) still bind, given "full scope, no deadlines" in the plans | **Completed** | docs/context, docs/plans/README.md | — | Owner confirmed 09 Oct 2026: no deadlines; the dates are kept only as history |
| DT-036 | Keep `docs/context/` and `docs/previous_prompt.md` in sync after every decision / prompt (permanent rule INSTRUCTIONS §8f, §8h) | In progress | INSTRUCTIONS.md | — | Ongoing duty, not a one-off task; first full sync done 09 Oct 2026 |

### D. Later-phase hooks created by this work

| ID | Task | Status | Planned in | Depends on / blocker | Notes |
|---|---|---|---|---|---|
| DT-032 | `p1-voice-input` and `p1-ai-language` must call the spoken-identity extractor on recognised/generated text | Pending | p1-voice-input, p1-ai-language | DT-001 | |
| DT-033 | Urdu / multilingual language tables for spoken identity | Pending | p2-ai-extensions | DT-001 | |

## Change log

| Date | Change |
|---|---|
| 09 October 2026 (later) | Owner decisions: step 6 approved (DT-030); approval statement closes the 15 institutional decisions (DT-034, DT-018) and ISS-0001 / ISS-0002 (DT-019); no deadlines (DT-035); wristband applied instantly after verification (DT-006, DT-010 re-scoped, no delay); DT-017 unblocked; times now in UTC. |
| 09 October 2026 | Register created; context-folder sync and prompt history created (DT-034–DT-036 added).  DT-002 completed (owner accepted the ±1 values; `SR-0035` updated). DT-001, DT-006–DT-011 planned as new step files (workflow s18/s19, characters s08, vr-interaction-ui s12, voice-audio s06). Other items collected from the plans, the step 6 hand-off and the engine steps. |
