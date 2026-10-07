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