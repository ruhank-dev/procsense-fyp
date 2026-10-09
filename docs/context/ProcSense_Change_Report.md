# ProcSense — Change Report (v2.0, 6 Oct 2026)

Rebuild of the project documentation around **ProcSense_Medical_Walkthrough_v5_1.pdf** (master medical source). The walkthrough was read in full (12,598 lines, ≈228 pages; the file is UTF-8 text with a .pdf extension).

## A. Files

| File | Change |
|---|---|
| ProcSense_Master_Plan.pdf / .txt | Rewritten: 15 steps with verbatim walkthrough specs, architecture changes, scenarios, what-ifs, numbers, schedule, validation, traceability |
| ProcSense_Implementation_Guide.pdf / .txt | Rewritten: step guides 01–15 + 50 build guides (44 reviewed, 6 new) |
| ProcSense_Data_Source_Inventory.csv | 85 → 297 rows; 20 columns incl. status, walkthrough ref, data kind, evidence type |
| ProcSense_Team_Board.md | Rewritten for the 15 steps, P0 cases and revised schedule |
| ProcSense_Content/ (NEW) | Machine-readable walkthrough content, curated config tables, traceability_matrix.csv |
| ProcSense_Change_Report.md (NEW) | This file |
| ProcSense_Clinical_Research.txt | REMOVED — superseded by the walkthrough; no active references remain |
| ProcSense_Tracker_Tasks.json, tracker HTML | REMOVED — Notion board is the single task system |

## B. Architecture changes

| Change | Old limitation | Revised design | Affected |
|---|---|---|---|
| 1. 15 canonical Steps (was 14 stages) | Vitals and examination were one stage; Sign Out was 'Stage 14'. The walkthrough defines 15 steps with a 21-question template each. | Stage model = WT Part 11 Steps 1–15 (Step 3 vitals, Step 4 examination, Step 15 Sign Out optional). Every step stores the 21 answers verbatim. | Master Plan, Guide 01–15, Team Board, B-R12 |
| 2. Two-level flow: Steps + 20-stage scenario maps | One linear path; no parallel tracks; deterioration only as events. | Scenario master maps (Parts F/G) order steps per scenario, run tracks in parallel (theatre prep; S2 optimisation) and allow deterioration/emergency stages anywhere. | B-R12, flow_S1/S2_20_stages.csv |
| 3. Five decision outcomes + master decision diagram | Decision points were ad-hoc option lists. | DecisionOutcome {SAFE, INVESTIGATE, OPTIMISE, REVIEW, EMERGENCY} re-evaluated whenever information changes (WT Part 3, Step 8). | B-R12, B-R17 |
| 4. Five end states | Outcomes were ready-stable / ready-with-issues / not-ready / harmed. | READY_SAFE, POSTPONED_APPROPRIATELY (success), REDIRECTED_APPROPRIATELY, SAFE_BUT_DELAYED, SAFETY_FAILURE (WT Part 16). | B-M15, B-S5 |
| 5. Problem registry, priority ladder, clash checker | No representation of several simultaneous problems. | Problems typed (6 types), ranked by the 7-step ladder (14B.1) and Part D P1–P4; treatment clashes (14B.3) and three drug checks. | B-R17 (new), B-M19 |
| 6. Role workflow: request → confirm → administer | Trainee could 'administer' treatments. | Trainee recognises/initiates; clinician NPC confirms drug+dose; nurse NPC administers and challenges unsafe requests (beginner mode) or reaction occurs (advanced). | B-R11, B-M14, B-R4, B-M7 |
| 7. Evidence labels, number kinds, dose policy | Single '[CLINICAL VALIDATION REQUIRED]' flag. | Every content item: evidence type A/B/C/D, number kind (reference/abnormal/concern/critical/simulator trigger), dose policy (EDUCATIONAL EXAMPLE vs PATIENT-SPECIFIC vs INSTITUTIONAL). | B-M11, B-M12, B-R2 |
| 8. Realistic laboratory subsystem | Results generator only; no artefacts or critical calls. | Sample quality flags, lab comments, repeat with different tube, previous/baseline values, units, critical-result phone call with read-back, acknowledgement blocker, Type D turnaround. | B-M13 |
| 9. Early-warning & sepsis engine | NEWS2 only as a number. | NEWS2 (Scale 1/2, single-3), NG253 tiers with timers and reassessment intervals, maternity exception, lateness computed for debrief. | B-R18 (new) |
| 10. Deterioration clocks | Generic deterioration event. | Type D clock (HR +5–10, NEWS2 +1 per ~30 min untreated); speed set by the most dangerous untreated problem. | B-R13, B-R17 |
| 11. Hidden facts & 'surfaces later' consequences | Hidden information existed but no late-surfacing rules. | Facts revealed only if asked; if never asked they surface at the WT moment (e.g. anaesthetist at Sign In → case stops). | B-R3, B-M19 |
| 12. WHO checklist exact wording + extensions | Paraphrased items. | WHO 2009 wording; ProcSense extensions flagged and displayed separately; green lights; repeated skip stops case. | B-R15, B-S10 |
| 13. Content types for walkthrough structures | Generic blocks only. | Step, FlowStage, WhatIf (47), VR card (24), StackedCase (20 sections), Patient (namespaced IDs), Medicine, LabResult, Problem. | B-M11, B-M18 (new), B-M19 (new) |
| 14. Scope extension (P1) | Ended at Time Out; Stage 14 optional. | Optional intra-operative training events (labelled extension), Sign Out and recovery handover, debrief stage. | B-S12 (new), B-R15 |
| 15. Institutional adaptation layer | 'LOCAL-v1' answers. | 15 institutional decisions (Part I) held in one configuration; Type C values marked placeholders until decided. | B-M17, B-M11 |
| 16. Expanded NPC roster | Nurse, porter, anaesthetist, surgeon, scrub. | Adds triage nurse, theatre reception, lab caller, radiologist, gynaecology/obstetrics, pharmacist, gastroenterology, cardiology, critical care, interpreter, guardian, recovery nurse. | B-R4, B-S3 |

Old-design events not in the walkthrough were removed: vasovagal faint → walkthrough 14.4 anaphylaxis-after-medicine; 'OT calls early' → Step 12 'theatre calls for the patient'.

## C. Walkthrough traceability (summary by part)

Full matrix: `ProcSense_Content/traceability_matrix.csv` (310 rows = every walkthrough section + all 40 S1/S2 master-map stages) and the Master Plan appendix.

| Walkthrough part | Sections | Requirements | Guides | Status |
|---|---|---|---|---|
| Front | 2 | Content labels A–D, number taxonomy, decision principle, safety statement, version log | B-M12, B-M18 | Specified |
| PART I | 6 | Clinician validation & institutional adaptation | B-M17 | Specified — awaiting decisions |
| Part 1 | 5 | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | B-R12, B-R17, B-R4, B-S3 | Specified |
| Part 2 | 5 | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | B-R12, B-R17, B-R4, B-S3 | Specified |
| Part 3 | 2 | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | B-R12, B-R17, B-R4, B-S3 | Specified |
| Part 4 | 14 | Vital signs, NEWS2, sepsis, patterns | B-R13, B-R18, B-S10 | Specified |
| Part 5 | 38 | Laboratory tests, artefacts, critical results | B-M13, B-M20 | Specified |
| Part 6 | 6 | ECG and imaging interpretation | B-M13, B-S10 | Specified |
| Part 7 | 9 | Scores and risk tools | B-M20, B-R12 | Specified |
| Part 8 | 5 | Medicines, anaesthesia, dose policy, role convention | B-M14, B-R17, B-R2 | Specified |
| Part 9 | 3 | Fasting rules; WHAT-IF branch (11 fields) | B-M14, B-M19, B-R14, B-R17 | Specified |
| Part 10 | 2 | Consent | B-M3, B-R3, B-S10 | Specified |
| Part 11 | 17 | Step 1 21-question specification; Step 10 21-question specification; Step 11 21-question specification; Step 12 21-question specification; Step 13 21-question s | B-M11, B-M12, B-M13, B-M14, B-M15, B-M18, B-M19, B-M20, B-M3, B-R11, B-R12, B-R13, B-R14, B-R15, B-R16, B-R17, B-R18, B- | Specified; Specified (P1) |
| Part 12 | 2 | Scenario stories / patient variants | B-M1, B-M19 | Specified |
| Part 13 | 2 | Scenario stories / patient variants | B-M1, B-M19 | Specified |
| Part 14 | 59 | WHAT-IF branch (11 fields); What-if method / ABCDE | B-M19, B-R14, B-R17 | Specified |
| Part 15 | 2 | WHO checklist + extensions | B-R15, B-S10 | Specified |
| Part 16 | 16 | Player view; Player view per stage / end states | B-M15, B-S10, B-S7, B-S9 | Specified |
| Part 17 | 2 | All numbers | B-M11, B-M12, B-M18 | Specified |
| Part 18 | 2 | Dictionary | B-M18 | Specified |
| PART A | 1 | Section content | B-M18 | Specified |
| Part 14B | 26 | Compact stacked case; Stacking method (ladder, rules, clashes, recipe) | B-M19, B-R17 | Specified; Specified (P2) |
| PART B | 3 | Clinical accuracy refinements + consistency audit | B-M12, B-M18 | Specified |
| PART C | 25 | VR reasoning card; VR reasoning cards / event table | B-M3, B-R17, B-S10, B-S12 | Specified; Specified (P1 extension) |
| PART D | 10 | Full playable stacked case (20 sections); Stacked cases | B-M15, B-M19, B-R17 | Specified; Specified (P0/P1 per patient_library.csv) |
| PART E | 1 | Equipment inventory | B-R15, B-S1, B-S9 | Specified |
| PART F | 21 | S1 master scenario (20 stages); Scenario flow stage (10 fields) | B-M15, B-R12, B-R14, B-R18, B-S12, B-S5 | Specified; Specified (P1 extension) |
| PART G | 21 | S2 master scenario (20 stages); Scenario flow stage (10 fields) | B-M15, B-R12, B-R14, B-R18, B-S12, B-S5 | Specified; Specified (P1 extension) |
| PART H | 2 | Evidence audit | B-M17 | Specified |
| Part 28 | 1 | Sources | B-M17 | Specified |

## D. Data inventory changes

- Original rows: 85 · Final rows: 297
- NEW 212 · UPDATED 34 · unchanged (ACTIVE) 49 · OBSOLETE 2
- Obsolete: D23 (local protocol — split into 15 institutional rows D400–D414); D47 (Synthea priors — walkthrough defines the patient population).
- Unresolved: 63 rows (cited guidelines whose URLs were not verified; institutional placeholders; Type D design values). No links were invented.
- Verified links added 6 Oct 2026: NICE NG253; UK Kidney Association hyperkalaemia; CPOC diabetes.

## E. Consistency checks performed

All passed on the final build:

- Master Plan and Guide PDF/TXT agree (generated from the same source; key identifiers checked in both).
- All 15 step sections in both documents; 50 build guides rendered.
- Every build guide referenced by steps, board and traceability exists.
- Traceability complete; every row has files + guides.
- Inventory: valid CSV, 20 columns every row, unique IDs, referenced content files exist, no invented links.
- Board matches the 15 steps and guide IDs.
- No active reference to ProcSense_Clinical_Research.txt; no old 'Stage N' numbering (the walkthrough's own Part 16 labels excepted); no walkthrough-absent events.

## F. Outstanding decisions

### Institutional decisions (walkthrough Part I.2)

1. Laboratory reference ranges, units and critical-value list — *Hospital laboratory*
2. Antibiotic protocol: prophylaxis agent/dose/redose; appendicitis regimen; penicillin-allergy alternative (incl. cefazolin after anaphylaxis) — *Microbiology / surgeon*
3. Consent/legal workflow: form, witness, age/guardian, mature minors, emergency treatment, family involvement — *Institution legal / surgeon*
4. Fasting policy details (clear 2 h vs 1 h; GLP-1 and SGLT2 policy choice) — *Anaesthesia department*
5. Local anaesthetic protocols (insulin, steroid cover, K acceptance 3.0–5.5 and INR limits as defaults) — *Anaesthesia department*
6. Emergency escalation: early-warning tool, responders, sepsis tool — *Hospital*
7. Equipment availability (MRCP, ERCP, CT, gastric ultrasound, capnography, warming) — *Hospital*
8. Documentation: ward checklist, handover form, WHO version/additions, site marking — *Theatre governance*
9. Blood-bank procedure (G&S/crossmatch, labelling) — *Transfusion lab*
10. Patient identification policy (identifiers, band colours) — *Hospital*
11. Transfer escort/monitoring rule — *Hospital*
12. Post-operative/day-case discharge criteria — *Surgeon / anaesthetist*
13. Choice of appendicitis score (AIR default vs AAS) and imaging pathway for high-score young patients — *Surgeon*
14. Unit-specific complication rates for consent — *Surgeon*
15. Realism of simulator design values (deterioration clocks, turnaround times) — *Surgeon + anaesthetist*

### Walkthrough issues found

| ID | Issue | Handling | Needs |
|---|---|---|---|
| WT-I01 | ID collisions: 'Example Patient B' (Farzana) vs variant S1-B (Tariq); 'Example Patient C/D' (Hamza/Usman) vs S2-C?/S2-D (Zainab); Part D stacked S1-A..D/S2-A..D reuse variant letters; two different 'Mr Imran' patients. | ProcSense namespaces IDs: VAR- (Parts 12–13), STK- (Part D), SC- (Part 14B); WT label kept in a column. | Supervisor to confirm naming; no medical change |
| WT-I02 | Hamza symptom duration: Step 1 triage example '20 h' vs Part 13 triage note '14 h' (CRP described as '20 h of symptoms'). | Content uses Part 13 values; flagged. | Surgeon to confirm |
| WT-I03 | Part C event table cites 'Contract Act s.11' for minor consent — jurisdiction-specific despite v5 international scope. | Not built into logic; consent law is a Type C institutional item. | Legal/surgeon |
| WT-I04 | Temperature display zone (normal 36.1–37.9; fever 38.0–39.0) differs from NEWS2 scoring (0 = 36.1–38.0; 1 = 38.1–39.0). | Display zones and NEWS2 scoring kept as separate tables (same approach as HR). | Anaesthetist |
| WT-I05 | Albumin bands: 5.15 low 25–34 vs Part 17 mild 30–34 / significant 25–29. | Part 17 used for bands; 5.15 text shown in teaching. | Lab/clinician |
| WT-I06 | CRP mild band: 5.4 '5–49' vs Part 17 '10–49'. | Both noted; lab ranges are Type C anyway. | Lab |
| WT-I07 | Sodium bands split differently (5.5 'High 146–155' vs Part 17 146–150 / 151–155). | Part 17 split used; compatible. | Lab |
| WT-I08 | INR 'severe >2.0' and GGT 'mild ≤3×' appear only in Part 17. | Used as display bands only. | Clinician |
| WT-I09 | Stage numbering differs: Part 3 (14 boxes), Part 11 (15 steps), Part 16 (Stages 1–14, 13–14 combined), Parts F/G (20 stages, different order). | Part 11 steps = canonical step IDs; F/G stages = scenario flow maps referencing steps. | None |
| WT-I10 | Turnaround times only partially specified ('FBC 45 min, imaging 60–120 min, etc.'). | Others marked UNRESOLVED (Type D) in data inventory. | Surgeon + anaesthetist |
| WT-I11 | BTS 'reservoir mask 15 L/min' not re-verified in v5.1 (Part H). | Shown only as cited text; not a scoring rule. | Anaesthetist |
| WT-I12 | Fever thresholds per variant need surgeon confirmation (4.3). | Defaults from WT. | Surgeon |
| WT-I13 | Source check (6 Oct 2026): NICE NG253 management algorithm resources state an initial fluid bolus of 250 mL ideally over 10–15 min; WT's sepsis example uses NICE CG174's 500 mL bolus over <15 min. | WT value kept (display-only educational example); flagged — not changed. | Anaesthetist/surgeon to confirm which example to display |
| WT-I14 | Source check (6 Oct 2026): UK Kidney Association hyperkalaemia guideline has a version updated July 2026; WT cites the 2023 version. | WT values kept; flagged for re-check before release (WT itself asks sources to be re-checked). | Clinician re-check |

### Schedule (needs your approval)

- **Scope added by the walkthrough:** 15 steps × 21 questions; 25 variants; 8 full + 20 compact stacked cases; 47 what-ifs; 2 × 20-stage maps; 50 build guides (was 44).
- **Honest assessment:** Building every case and branch as P0 by 4 Dec is not realistic for three students. The schedule structure and gates are kept, but scope is tiered.
- **Adjustment 1 — data-driven engine:** Cases, what-ifs and stacked cases are CONTENT, not code. Once the engine supports a content type, extra cases are additions to the content package.
- **Adjustment 2 — P0 case set:** 17 cases: VAR-S1-A, -PB, -B(+B2), -C(+C2), -D(a/b), -E, -G, -H, -J; VAR-S2-PC, -PD, -B, -D, -F, -G; STK-S1-D; STK-S2-B.
- **Adjustment 3 — P1/P2:** P1: remaining 10 variants + 6 stacked cases (content-only after freeze if validated and no new code needed); Step 15 + intra-op extension (decided at Gate 6). P2: SC-1..SC-20.
- **Adjustment 4 — what-ifs:** P0 = what-ifs bound to P0 cases; the rest are loaded and testable but not polished.
- **Adjustment 5 — clinician time:** CRG sessions now include the 15 institutional decisions; packs limited to ≤45 min by prioritising Type C and P0 items.

## G. Project decisions and changes since v2.0 (living log)

Sections A–F above are the v2.0 baseline (6 Oct 2026) and are left as written. This section is the running record of everything decided or changed afterwards, kept up to date under INSTRUCTIONS.md §8h. Each row gives the previous state, the new state, and the reason **where one is recorded**.

**Evidence and gaps.** Dates and reasons come from (a) files in this repository (plans, ADR, phase review, register, README) and (b) the Claude Code session records of 8–9 October 2026 (see `docs/previous_prompt.md`). **No session record exists for 6–7 October 2026**, when the plans, ADR-0001 and the phase review were written; those rows are reconstructed from the plan files only, with reasons marked "not recorded" rather than guessed (open item DT-031 in `docs/deferred-tasks.md`).

### G.1 Decision log

| # | Date | Area | Previous | New | Why | Evidence |
|---|---|---|---|---|---|---|
| 1 | 07 Oct 2026 | Plan structure and schedule | Master Plan / Team Board: 14-week schedule with gates, P0/P1/P2 scoped to the 31 Dec deadline | Plans as tier → phase → step files (P0/P1/P2 = build order, "full scope, no deadlines", nothing cut); 216 steps in milestones M0–M13 | Not recorded in the files | `docs/plans/README.md`, `milestones.md` |
| 2 | 07 Oct 2026 | Core engine location | Guide B-R11: engine files inside `unity/Assets/_Project/Core/`, linked into a `sim/` project | `src/ProcSense.Core` (netstandard2.1, C# 9), built and tested with plain `dotnet`; Unity consumes it as a local package | Engine can be built and tested before Unity exists; one copy of the code | `docs/decisions/ADR-0001-core-location.md` (status still "Proposed" — DT-029) |
| 3 | 07 Oct 2026 | Walkthrough medical status | Walkthrough and Team Board: "READY FOR CLINICIAN VALIDATION — not approved" | Walkthrough treated as approved by a medical professional (per the project owner); recorded in every register row | Owner statement | `docs/research/source_register.csv` (approval column) |
| 4 | 07 Oct 2026 | Environment scope | Every location modelled | Category A (fully modelled: OT, one pre-op patient bay, walkable corridor, desk + computer), B (doors: CT, imaging, X-ray, lab, MRCP/ERCP — send, wait, report back, EMR updates), C (computer/UI only) | Owner direction "fully model only what the trainee interacts with" | `docs/plans/README.md`, `phase-review.md` A1–A5 |
| 5 | 07 Oct 2026 | 3D assets | Open: asset pack, AI text-to-3D, importing free models | Supervisor's room shells; every equipment item is our own Blender Python script; free models only as visual references; no AI text-to-3D | Owner decisions | `docs/plans/README.md`, `working-model.md` |
| 6 | 07 Oct 2026 | Characters | Avatars for every role | Avatars only for people physically present; phone/report roles are voice/text only | Same "model only what is used" principle | `phase-review.md` A7 |
| 7 | 07 Oct 2026 | Phase list | Separate `p0-medical-research` and `p0-datasets` | Merged into `p0-medical-research-data`; new phases `p0-diagnostic-transitions` and `p0-patient-records-emr` added | Owner direction on doors, folder vs computer | `phase-review.md` A1, A2, P2 |
| 8 | 07 Oct 2026 | Build order | Phases built independently | Walking skeleton first (greybox loop before art) | Proof before polish | `phase-review.md` P3, `plans/p0/README.md` |
| 9 | 07 Oct 2026 | Phase-review proposals | — | Approved: P1 one patient bay, P2 merge datasets, P3 skeleton first, P6 recovery reuses the bay. Rejected: P4 and P5 (Pulse and composer stay in P1), P7 (corridor is walkable, B-room doors do not open) | Owner decisions | `phase-review.md` |
| 10 | 07 Oct 2026 | Backend and AI | Backend/AI as required parts | Backend optional (P1), headset works without it; every AI path has a non-AI fallback | Offline Quest use, medical safety | `docs/plans/README.md` |
| 11 | 07 Oct 2026 | Repository | Earlier prototype repository | History restarted; foundation step s00 approved | Not recorded | `README.md`, `✅ p0-foundation-s00…` |
| 12 | 08 Oct 2026 | Working files | — | `AGENTS.md` carries routing and meta rules; `INSTRUCTIONS.md` carries working-style rules; both are read first every session | Owner prompts "cache agents.md and instruction.md", "use and follow the seperate instruction.md file" | `docs/previous_prompt.md` #1–#3 |
| 13 | 09 Oct 2026 | Coding style | Ponytail named as active in INSTRUCTIONS §9 | Ponytail skill installed in the project (`.claude/skills/ponytail`) and used for all coding (smallest working change) | Owner prompt | `docs/previous_prompt.md` #5 |
| 14 | 09 Oct 2026 | .NET | Projects target net9.0; only .NET 8 and 10 on the machine | .NET 9 SDK installed | Tests could not run | session record |
| 15 | 09 Oct 2026 | Remote repository | `github.com/Myt1x54/procsense-fyp` | `github.com/ruhank-dev/procsense-fyp` (ownership transferred); local remote and README updated | Owner transferred the repository | `docs/previous_prompt.md` #16–#17 |
| 16 | 09 Oct 2026 | Hand-off format | Proposed commit message after approval | Every hand-off includes the full `git add` / `git commit` / `git push` commands with the message; the agent never commits unless told | Owner prompt | INSTRUCTIONS §3, `previous_prompt.md` #11 |
| 17 | 09 Oct 2026 | Engine design (skeleton steps 2–6) | Guide: Session built from content package + case card; JSON via a library | Session takes the case card, builds the patient and starts at the case clock time; **own JSON reader** (no library; Unity IL2CPP safe); C# 9 records with a shim; observable snapshot built on demand; saved session = header line + canonical JSONL with SHA-256; identity gate in the first pipeline subsystem; trust/anxiety values read from `content/behaviour.json` | Determinism, Unity compatibility, no clinical numbers in code | step docs `p0-core-engine-s01…s04`, `p0-simulation-workflow-s01` |
| 18 | 09 Oct 2026 | Step 1 mismatch rule | Guide L1-01: mismatch state; critical item if the trainee confirms anyway | Confirming with a mismatch is **rejected** (`ST1_MISMATCH`, logged with the differences); the critical score item waits for the scoring phase | Scoring engine not built yet | `IdentitySubsystem`, DT-003 |
| 19 | 09 Oct 2026 | Trust/anxiety ±1 (`SR-0035`) | Register: "Pending" | Accepted as a simulation mechanic | Owner statement | `source_register.csv`, DT-002 |
| 20 | 09 Oct 2026 | Clinical approval status | Walkthrough-only approval (row 3) | All existing medical research and clinical workflows treated as clinician-approved. **Not settled by it:** hospital-specific values (Part I decisions, band colours, unknown-patient procedure) and the open source conflicts ISS-0001 / ISS-0002 | Owner statement | `docs/plans/README.md`, DT-018, DT-034 |
| 21 | 09 Oct 2026 | Nurse wristband workflow | Only in the source documents (L1-04, T01-4, walkthrough); in no step file | Planned: engine s18, 3D nurse entering from outside s08, VR interaction + tablet fallback s12, nurse voice s06; includes the missing-wristband scenario | Owner request | `p0-simulation-workflow-s18`, `p0-characters-s08`, `p0-vr-interaction-ui-s12`, `p0-voice-audio-s06` |
| 22 | 09 Oct 2026 | Spoken identity check | Token shortcut (name + spelled-out date words) | Robust normaliser/extractor planned (s19); ambiguous dates reported, never guessed | Owner request | `p0-simulation-workflow-s19` |
| 23 | 09 Oct 2026 | Documentation rules | — | Permanent: prompt history (`docs/previous_prompt.md`), deferred-task register (`docs/deferred-tasks.md`), `docs/context/` kept in sync | Owner request | INSTRUCTIONS §8f–§8h |
| 24 | 09 Oct 2026 | Memory-log time zone | Log recorded `UTC-08:00` | Machine zone "PST" is Pakistan Standard Time, **UTC+05:00**; log corrected | Checked against session timestamps | `.memory/` log |
| 25 | 09 Oct 2026 | Step 6 (identity/allergy) | Built, awaiting approval | **Approved**; committed as `9e71540` | Owner | `docs/previous_prompt.md` #23 |
| 26 | 09 Oct 2026 | Scope of the approval statement | Row 20: did not settle the 15 institutional decisions or ISS-0001 / ISS-0002 | Statement **closes everything**: the 15 institutional decisions (§F), ISS-0001 (walkthrough 30–40 s stands) and ISS-0002 | Owner clarification | DT-034, DT-019, DT-018 |
| 27 | 09 Oct 2026 | Deadlines | Row 1 recorded the conflict between Team Board dates and "no deadlines" | **No deadlines**; Team Board dates kept only as history | Owner confirmation | DT-035 |
| 28 | 09 Oct 2026 | Missing / wrong wristband timing (ISS-0002) | Walkthrough "apply after verification" vs Guide T01-4 "after delay" | **Applied instantly after verification.** Agent proposal (extends the same rule to the mismatch case): **no simulated-time delay for the whole nurse workflow** (cosmetic walk-in/replacement paced by the view; no invented timing numbers; no timer state machine) | Owner decision (band instantly after verification); the wider no-delay design is the agent's proposal | `p0-simulation-workflow-s18` |
| 29 | 09 Oct 2026 | Time standard | Memory log and prompt history showed local time and UTC | **UTC only** (12-hour AM/PM + "UTC") in `.memory/` and `docs/previous_prompt.md` | Owner instruction | INSTRUCTIONS §8e |
| 30 | 09 Oct 2026 | Step 2 history design (step 7) | Guide L2-01…L2-05 and the step doc left open how the main complaint is volunteered and how the note is ordered | The patient volunteers the main complaint when history-taking starts (first `ask` or `summarise`); the clerking note lists facts in the order they were revealed; `summarise` pays out once, only after at least 5 topics (counting the volunteered complaint and Step 1 answers); history topics and the sensitive-pregnancy flag come from `content/history_topics.json`; trust/understanding values from `content/behaviour.json` (SR-0036, SR-0037) | Fills the gaps in the Guide wording; easy to change in `HistorySubsystem` | `p0-simulation-workflow-s02` |
| 32 | 09 Oct 2026 | Step 3 sensors and vitals design (step 8) | Guide L3-01/L3-02 and the step doc left open how readings are shown and how long the cuff takes | Live sensor (SpO2 probe) shows SpO2, oxygen and pulse only while attached; BP, temperature, RR, ACVPU and pain are one-off readings kept with their clock time; the cuff cycle length is drawn once per cycle from the 30–40 s range in `content/devices.json` on the seeded `devices` stream (ISS-0001 closed: walkthrough value); taking the cuff off cancels the cycle; readings other than BP are instant; no identity gate; a `wait` verb lets a script let time pass; clock text shows half seconds as `.5` | Fills the gaps in the Guide wording without inventing numbers | `p0-simulation-workflow-s03` |
| 33 | 09 Oct 2026 | Golden runs (step 9) | Sample scripts lived in `tests/golden/`; hash checks only in unit tests | `tests/golden/` holds only official golden runs: script + `.expected.json` (case, seed, mode, log hash, final-state hash, key facts). One check (`GoldenRun`) backs both the xUnit tests and the CLI `golden` command; `--update` rewrites expected files deliberately. Sample scripts moved to `tests/samples/` | Keeps "golden" meaning reviewed and pinned; the same check will compare Quest sessions later | `p0-simulation-workflow-s04` |
| 34 | 09 Oct 2026 | CI/CD | No automated checks; foundation step s02 only planned | GitHub Actions CI (build with warnings as errors, tests, golden runs, replay round trip on Linux and Windows, format, content and source-register validation, plan-index check, project-rule checks, dependency audit) and a tag-driven release (headless simulator zip + checksums). `docs/CI.md` lists every check and the roadmap; **new rule: every step adds or updates its CI check and `docs/CI.md`** (INSTRUCTIONS §6b). Branch protection and code scanning not enabled (private repo, free plan; DT-042) | Owner request | `p0-foundation-s02`, `docs/CI.md` |
| 31 | 09 Oct 2026 | Ponytail skill in the repository | Skill files committed in the repo (row 13) | Skill kept on the developer machine only: `.claude/skills/ponytail/` added to `.gitignore` and the files removed from earlier commits by a history rewrite (local backup branch `backup-before-history-rewrite`; the remote needs a force-push) | Owner request | `.gitignore`, DT-038 |

### G.2 Build progress (walking skeleton 1)

Approved on 09 Oct 2026: step 1 (VAR-S1-A content), core-engine s01 (clock + RNG), s02 (actions/session/log), s03 (content loader + patient state), s04 (observable, CLI, replay), p0-simulation-workflow-s01 (Step 1 identity and allergy). p0-simulation-workflow-s02 (Step 2 history questions) approved. p0-simulation-workflow-s03 (Step 3 sensors and vitals) approved. Built and awaiting approval: p0-simulation-workflow-s04 (official golden run). Outstanding work: `docs/deferred-tasks.md`.

### G.3 Context folder review (09 Oct 2026)

| File | Affected? | What was done |
|---|---|---|
| `ProcSense_Change_Report.md` | Yes | This section |
| `ProcSense_Team_Board.md` | Yes | Status addendum, current-position table, notes on dates and decisions, two added decision items |
| `ProcSense_Medical_Walkthrough_v5_1.md` | Yes (status) | Banner only; body unchanged |
| `ProcSense_Master_Plan_from_TXT.md`, `…_from_PDF.md` | Yes (structure, schedule) | Banner only; body unchanged |
| `ProcSense_Implementation_Guide_from_TXT.md`, `…_from_PDF.md` | Yes (Core location, Step 1 rules) | Banner only; body unchanged |
| `ProcSense_Data_Source_Inventory.md` | Yes (registers) | Banner only; body unchanged |
| `CONVERSION_REPORT.md` | Yes (records the banners) | Addendum |

The converted documents are format-only copies of the original PDFs/TXT/CSV; their bodies are intentionally not edited, so decisions live here and in the Team Board, and the banners point to them.

### G.4 Open or unconfirmed

- 6–7 Oct 2026 history is reconstructed from files only (DT-031) — still open.
- *Resolved 09 Oct 2026:* whether the approval statement closes the 15 institutional decisions (yes, DT-034); whether the Team Board dates bind (no, DT-035).
