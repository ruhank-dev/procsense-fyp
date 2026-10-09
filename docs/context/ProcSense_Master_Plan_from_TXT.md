<!-- Converted from ProcSense_Master_Plan.txt — headings (title, part banners, numbered sections, sub-headings, step headings) became Markdown headings; all other lines kept verbatim inside fenced text blocks so tables, lists and diagrams stay exactly as in the source; decorative rule lines (===, ---, ###) used only for headings were dropped. Format conversion only: no summarising, rewriting or correction. -->

> **Project addendum (09 Oct 2026 — not part of the converted document).** The work is now organised as `docs/plans/` (tier P0/P1/P2 → phase → step files, "full scope, no deadlines"); this document is the v2.0 proposal. The schedule and gates below are kept for history — whether they still bind is unconfirmed (DT-035). Core engine location changed by ADR-0001. Outstanding work: `docs/deferred-tasks.md`. Decision history: `ProcSense_Change_Report.md` §G.

# PROCSENSE — MASTER PLAN

```text
 Complete project blueprint built on Medical Walkthrough v5.1
 Version 2.0 · 6 Oct 2026 · rebuilt on ProcSense Medical Walkthrough v5.1 (master medical source) · plan start Tue 29 Sep 2026 · FAST NUCES Islamabad
 Team: Ruhan · Sannan · Moiz · Supervisor: Ma'am Noor-ul-Ain · Deadline: Thu 31 Dec 2026
 Status: medical content = walkthrough v5.1 'READY FOR CLINICIAN VALIDATION — not clinically approved'; educational simulation only

 CONTENTS
   1. Source of truth and document set
   2. Safety, role and content principles
   3. Project objective and scope
   4. Architecture changes required by the walkthrough
   5. The 15 steps and the five decision outcomes
   6. Step 1 — Patient arrival and identification
   7. Step 2 — Asking questions (the history)
   8. Step 3 — Checking vital signs
   9. Step 4 — Examining the patient
  10. Step 5 — Risk and anaesthetic assessment
  11. Step 6 — Choosing the tests
  12. Step 7 — Getting and understanding the results
  13. Step 8 — Deciding, treating and optimising
  14. Step 9 — Consent
  15. Step 10 — Preparing the patient (ward/ED checklist)
  16. Step 11 — Theatre preparation
  17. Step 12 — Transfer and handover (SBAR)
  18. Step 13 — WHO Sign In (before the patient goes to sleep)
  19. Step 14 — WHO Time Out (before the first cut) — the finish line
  20. Step 15 — WHO Sign Out and handover to recovery (optional)
  21. Patient & case library
  22. Stacked problems: method
  23. WHAT-IF library (WT Part 14)
  24. Clinical numbers and tools (configuration)
  25. System architecture
  26. AI architecture — controlled AI around a deterministic core
  27. AI Authority and Safety Boundaries
  28. AI-assisted complete case composition (Scenario Composer)
  29. AI-assisted assessment across the whole workflow
  30. State-aware AI & reactive loop
  31. Patient persona & behaviour realisation
  32. AI staff & environment interaction
  33. AI + unexpected events
  34. Adaptive difficulty
  35. AI debrief from session evidence
  36. General synthetic-data pipeline
  37. AI requirements matrix
  38. Implementation order & dependencies
  39. End-to-end AI example — S1
  40. End-to-end AI example — S2
  41. AI tests, risks and Definition of Done
  42. Team & responsibilities
  43. Schedule (starts Tue 29 Sep 2026)
  44. Clinical validation (WT Part I)
  45. Walkthrough traceability matrix
  46. References
```


## 1. SOURCE OF TRUTH AND DOCUMENT SET

```text
Read this first.

  ! [Master medical source] ProcSense_Medical_Walkthrough_v5_1.pdf is the master medical source of truth.
    ProcSense_Clinical_Research.txt has been removed from the active project and is no longer referenced. All
    clinical content in this document is taken from the walkthrough (cited as 'WT' with part/step and page) or
    from the content package generated from it. The walkthrough status is READY FOR CLINICIAN VALIDATION — not
    clinically approved.

  [TABLE] Deliverable | Purpose
  * ProcSense_Medical_Walkthrough_v5_1.pdf
      Purpose: Master medical source (unchanged; owned by the clinical side)
  * ProcSense_Master_Plan (pdf/txt)
      Purpose: Complete blueprint: scope, architecture, 15 steps, scenarios, schedule, validation
  * ProcSense_Implementation_Guide (pdf/txt)
      Purpose: Stage guides 01–15 + 50 build guides, beginner-friendly
  * ProcSense_Content/ (CSV/JSON)
      Purpose: Machine-readable walkthrough content + traceability matrix (310 rows)
  * ProcSense_Data_Source_Inventory.csv
      Purpose: Every data need, its source/URL or USE AI/UNRESOLVED
  * ProcSense_Team_Board.md
      Purpose: One-page Notion board
  * ProcSense_Change_Report.md
      Purpose: What changed and why; traceability summary; outstanding decisions
```


## 2. SAFETY, ROLE AND CONTENT PRINCIPLES

```text
Applied in every system.

  ! [Safety statement (WT)] ProcSense is a teaching game, not a medical tool or decision-support system. Every
    patient is invented; all case values are simulator-generated. Every number is a draft value for building
    and review. Local hospital rules always win. A qualified surgeon and anaesthetist must approve content
    before it is final.

  ! [Role convention (WT Part A, 8.1)] The trainee recognises problems, communicates (SBAR) and initiates the
    correct workflow. A qualified clinician confirms treatment decisions and doses; nurses/clinicians
    administer. The anaesthetist decides anaesthetic fitness; the senior surgeon decides whether and when to
    operate. The trainee never independently prescribes, selects a dose or administers.

  [TABLE] Type | Label | Meaning | What the team does
  * A
      Label: CLINICAL REFERENCE — SOURCE BACKED
      Meaning: Value/rule taken directly from a recognised guideline
      What the team does: Build as written; clinician checks applicability
  * B
      Label: CLINICAL INTERPRETATION
      Meaning: Clinically derived reading where no source gives the exact gameplay band
      What the team does: Default behaviour; expect adjustment
  * C
      Label: CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
      Meaning: Varies between institutions/countries
      What the team does: Placeholder with draft value; hospital/clinician decides
  * D
      Label: SIMULATOR DESIGN VALUE (PROVISIONAL)
      Meaning: Invented for the game; not a clinical rule
      What the team does: Keep easy to change
  * DOSE-EX
      Label: EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION
      Meaning: Standardised dose shown for teaching
      What the team does: Display only as example, never instruction
  * DOSE-PS
      Label: PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED
      Meaning: No single safe number
      What the team does: Show the decision, not a number

  [TABLE] Kind | Number kind | Meaning | Example
  * REFERENCE
      Number kind: Reference value
      Meaning: Generally expected in healthy people
      Example: K 3.5–5.3
  * ABNORMAL
      Number kind: Abnormal value
      Meaning: Outside usual range — often harmless, always interpreted
      Example: K 5.4
  * CONCERN
      Number kind: Clinical concern threshold
      Meaning: Triggers additional assessment
      Example: K ≥5.5 (UKKA: ECG)
  * CRITICAL
      Number kind: Critical / emergency threshold
      Meaning: Potentially immediate danger
      Example: K ≥6.5 or any ECG change
  * SIM_TRIGGER
      Number kind: Simulator trigger
      Meaning: ProcSense chooses it to create a training scenario
      Example: S1-J K 6.4 'haemolysed'

  ! [Decision principle (WT)] Abnormal finding → verify (repeat, check technique/sample) → assess severity and
    context → investigate → senior/anaesthetic review → the team decides whether to proceed, optimise, delay
    or escalate. Thresholds tell the trainee when to think and whom to involve; they are not automatic
    cancellation rules.
```


## 3. PROJECT OBJECTIVE AND SCOPE

```text
ProcSense is a Unity VR (Meta Quest 2/3) training simulator for the pre-operative preparation process: the
trainee takes a synthetic patient from arrival to the WHO Time Out ('ready for the first cut') for S1 —
elective laparoscopic cholecystectomy and S2 — emergency appendectomy, following the walkthrough's 15 steps,
branches and stacked problems.

  [TABLE] In scope (P0) | P1 | Out of scope (WT 2.3)
  * S1 + S2, Steps 1–14, 17 P0 cases, what-ifs bound to them, decision engine, NEWS2/sepsis, labs realism, WHO checklist, end states + timeline debrief, clinician validation
      P1: Step 15 Sign Out + recovery; intra-operative training events (extension); remaining 10 variants + 6
          stacked cases; voice/AI features
      Out of scope (WT 2.3): The operation itself, anaesthetic drugs and doses, hand skills; real patients;
          certification

                        | S1 — planned gallbladder        | S2 — emergency appendix           
  ----------------------+---------------------------------+-----------------------------------
  Danger now?           | Usually no                      | Possibly yes, worsening           
  Diagnosis known?      | Yes (scan)                      | No — must be worked out           
  Time pressure         | Low                             | High (deterioration clock)        
  If a problem is found | Fix first or postpone (success) | Fix in parallel; operate when safe
  Eaten?                | Usually fasted                  | Often unknown → full stomach      
  Mood                  | Calm                            | Pain, fear, fever                 
```


## 4. ARCHITECTURE CHANGES REQUIRED BY THE WALKTHROUGH

```text
What the old design could not support, what changed, and where.

  [TABLE] Change | Old design could not support | Revised design | How it supports the walkthrough | Affected files / guides
  * 1. 15 canonical Steps (was 14 stages)
      Old design could not support: Vitals and examination were one stage; Sign Out was 'Stage 14'. The
          walkthrough defines 15 steps with a 21-question template each.
      Revised design: Stage model = WT Part 11 Steps 1–15 (Step 3 vitals, Step 4 examination, Step 15 Sign Out
          optional). Every step stores the 21 answers verbatim.
      How it supports the walkthrough: Every step section; all IDs (L/E/A/T) renumbered
      Affected files / guides: Master Plan, Guide 01–15, Team Board, B-R12
  * 2. Two-level flow: Steps + 20-stage scenario maps
      Old design could not support: One linear path; no parallel tracks; deterioration only as events.
      Revised design: Scenario master maps (Parts F/G) order steps per scenario, run tracks in parallel
          (theatre prep; S2 optimisation) and allow deterioration/emergency stages anywhere.
      How it supports the walkthrough: S2 overlap and clocks are represented faithfully
      Affected files / guides: B-R12, flow_S1/S2_20_stages.csv
  * 3. Five decision outcomes + master decision diagram
      Old design could not support: Decision points were ad-hoc option lists.
      Revised design: DecisionOutcome {SAFE, INVESTIGATE, OPTIMISE, REVIEW, EMERGENCY} re-evaluated whenever
          information changes (WT Part 3, Step 8).
      How it supports the walkthrough: The central skill the walkthrough wants rewarded
      Affected files / guides: B-R12, B-R17
  * 4. Five end states
      Old design could not support: Outcomes were ready-stable / ready-with-issues / not-ready / harmed.
      Revised design: READY_SAFE, POSTPONED_APPROPRIATELY (success), REDIRECTED_APPROPRIATELY,
          SAFE_BUT_DELAYED, SAFETY_FAILURE (WT Part 16).
      How it supports the walkthrough: Postponement counts as success; redirection supported
      Affected files / guides: B-M15, B-S5
  * 5. Problem registry, priority ladder, clash checker
      Old design could not support: No representation of several simultaneous problems.
      Revised design: Problems typed (6 types), ranked by the 7-step ladder (14B.1) and Part D P1–P4;
          treatment clashes (14B.3) and three drug checks.
      How it supports the walkthrough: Stacked cases (Part D, 14B) become playable
      Affected files / guides: B-R17 (new), B-M19
  * 6. Role workflow: request → confirm → administer
      Old design could not support: Trainee could 'administer' treatments.
      Revised design: Trainee recognises/initiates; clinician NPC confirms drug+dose; nurse NPC administers
          and challenges unsafe requests (beginner mode) or reaction occurs (advanced).
      How it supports the walkthrough: Matches WT role convention and safety statement
      Affected files / guides: B-R11, B-M14, B-R4, B-M7
  * 7. Evidence labels, number kinds, dose policy
      Old design could not support: Single '[CLINICAL VALIDATION REQUIRED]' flag.
      Revised design: Every content item: evidence type A/B/C/D, number kind
          (reference/abnormal/concern/critical/simulator trigger), dose policy (EDUCATIONAL EXAMPLE vs
          PATIENT-SPECIFIC vs INSTITUTIONAL).
      How it supports the walkthrough: Type C items become institutional placeholders; Type D easy to change
      Affected files / guides: B-M11, B-M12, B-R2
  * 8. Realistic laboratory subsystem
      Old design could not support: Results generator only; no artefacts or critical calls.
      Revised design: Sample quality flags, lab comments, repeat with different tube, previous/baseline
          values, units, critical-result phone call with read-back, acknowledgement blocker, Type D
          turnaround.
      How it supports the walkthrough: WT Part 5 behaviour and Step 7 misses
      Affected files / guides: B-M13
  * 9. Early-warning & sepsis engine
      Old design could not support: NEWS2 only as a number.
      Revised design: NEWS2 (Scale 1/2, single-3), NG253 tiers with timers and reassessment intervals,
          maternity exception, lateness computed for debrief.
      How it supports the walkthrough: WT 4.9–4.10 and Patient D timeline
      Affected files / guides: B-R18 (new)
  * 10. Deterioration clocks
      Old design could not support: Generic deterioration event.
      Revised design: Type D clock (HR +5–10, NEWS2 +1 per ~30 min untreated); speed set by the most dangerous
          untreated problem.
      How it supports the walkthrough: S2 time pressure as written
      Affected files / guides: B-R13, B-R17
  * 11. Hidden facts & 'surfaces later' consequences
      Old design could not support: Hidden information existed but no late-surfacing rules.
      Revised design: Facts revealed only if asked; if never asked they surface at the WT moment (e.g.
          anaesthetist at Sign In → case stops).
      How it supports the walkthrough: WT Step 2 and Part D histories
      Affected files / guides: B-R3, B-M19
  * 12. WHO checklist exact wording + extensions
      Old design could not support: Paraphrased items.
      Revised design: WHO 2009 wording; ProcSense extensions flagged and displayed separately; green lights;
          repeated skip stops case.
      How it supports the walkthrough: WT Part 15
      Affected files / guides: B-R15, B-S10
  * 13. Content types for walkthrough structures
      Old design could not support: Generic blocks only.
      Revised design: Step, FlowStage, WhatIf (47), VR card (24), StackedCase (20 sections), Patient
          (namespaced IDs), Medicine, LabResult, Problem.
      How it supports the walkthrough: All structured WT parts load as data
      Affected files / guides: B-M11, B-M18 (new), B-M19 (new)
  * 14. Scope extension (P1)
      Old design could not support: Ended at Time Out; Stage 14 optional.
      Revised design: Optional intra-operative training events (labelled extension), Sign Out and recovery
          handover, debrief stage.
      How it supports the walkthrough: WT C.22–C.24, F/G 14–17
      Affected files / guides: B-S12 (new), B-R15
  * 15. Institutional adaptation layer
      Old design could not support: 'LOCAL-v1' answers.
      Revised design: 15 institutional decisions (Part I) held in one configuration; Type C values marked
          placeholders until decided.
      How it supports the walkthrough: WT Part I
      Affected files / guides: B-M17, B-M11
  * 16. Expanded NPC roster
      Old design could not support: Nurse, porter, anaesthetist, surgeon, scrub.
      Revised design: Adds triage nurse, theatre reception, lab caller, radiologist, gynaecology/obstetrics,
          pharmacist, gastroenterology, cardiology, critical care, interpreter, guardian, recovery nurse.
      How it supports the walkthrough: WT 1.4 and throughout
      Affected files / guides: B-R4, B-S3
```


## 5. THE 15 STEPS AND THE FIVE DECISION OUTCOMES

```text
  [TABLE] Step | Name | Priority | WT page | Build guides
  * 1
      Name: Patient arrival and identification
      Priority: P0
      WT page: 74
      Build guides: B-R11, B-S7, B-S9, B-M11, B-M18
  * 2
      Name: Asking questions (the history)
      Priority: P0
      WT page: 76
      Build guides: B-R6, B-R3, B-S10, B-M3, B-M19
  * 3
      Name: Checking vital signs
      Priority: P0
      WT page: 78
      Build guides: B-R13, B-R18, B-S9, B-S10
  * 4
      Name: Examining the patient
      Priority: P0
      WT page: 80
      Build guides: B-S9, B-S2, B-M3
  * 5
      Name: Risk and anaesthetic assessment
      Priority: P0
      WT page: 82
      Build guides: B-R12, B-M20, B-M12
  * 6
      Name: Choosing the tests
      Priority: P0
      WT page: 84
      Build guides: B-R16, B-M13, B-S7
  * 7
      Name: Getting and understanding the results
      Priority: P0
      WT page: 86
      Build guides: B-M13, B-S10, B-R17, B-M20
  * 8
      Name: Deciding, treating and optimising
      Priority: P0
      WT page: 88
      Build guides: B-M14, B-R17, B-R13, B-R18, B-R14
  * 9
      Name: Consent
      Priority: P0
      WT page: 91
      Build guides: B-R6, B-M3, B-R3, B-S10
  * 10
      Name: Preparing the patient (ward/ED checklist)
      Priority: P0
      WT page: 93
      Build guides: B-R15, B-S10
  * 11
      Name: Theatre preparation
      Priority: P0
      WT page: 95
      Build guides: B-S9, B-S1, B-R15, B-R14
  * 12
      Name: Transfer and handover (SBAR)
      Priority: P0
      WT page: 97
      Build guides: B-R16, B-M15, B-R8
  * 13
      Name: WHO Sign In (before the patient goes to sleep)
      Priority: P0
      WT page: 99
      Build guides: B-R15, B-S10, B-R4
  * 14
      Name: WHO Time Out (before the first cut) — the finish line
      Priority: P0
      WT page: 101
      Build guides: B-R15, B-S10
  * 15
      Name: WHO Sign Out and handover to recovery (optional)
      Priority: P1
      WT page: 102
      Build guides: B-R15, B-M15, B-R9, B-S12

  [TABLE] Outcome | Name | Meaning | Example
  * SAFE
      Name: Safe to continue
      Meaning: Everything fits and is within acceptable limits
      Example: Normal numbers, fasted, consent ready
  * INVESTIGATE
      Name: Needs more investigation
      Meaning: We don't yet know enough to decide
      Example: Abnormal LFTs → bile-duct imaging
  * OPTIMISE
      Name: Needs treatment / optimisation
      Meaning: A problem can be fixed (now in S2, or before a re-booked date in S1)
      Example: Dehydrated → fluids; poor diabetes → postpone S1
  * REVIEW
      Name: Needs senior or anaesthetic review
      Meaning: Decision is beyond the trainee or affects anaesthesia
      Example: Ate 3 h ago; irregular rhythm
  * EMERGENCY
      Name: Emergency
      Meaning: The patient may be in danger now
      Example: BP very low, SpO2 falling, severe allergic reaction

 MASTER DECISION — 'Is it safe to continue?' (WT Step 8; re-asked whenever new information appears)
 Any EMERGENCY sign? (NEWS2 7+, SBP ≤90, SpO2 ≤91, new confusion, anaphylaxis, chest pain, positive pregnancy test with shock)
   YES → EMERGENCY: call for help, ABCDE, treat (Part 14)
 Diagnosis different or complicated? (S1 inflamed GB/jaundice; S2 mimic e.g. ectopic, burst appendix)
   YES → SENIOR review → change pathway / more investigation
 Fixable problem? (BP 180/110+, HbA1c >69, salts, anaemia, anticoagulant not stopped, fasting breach, new AF)
   YES → verify → assess → senior/anaesthetic review. S1: usually optimise first (defer/re-time). S2: correct in parallel
 Anything affecting anaesthesia? (airway, OSA, aspiration risk, heart/lung disease)
   YES → ANAESTHETIST review / plan
 → SAFE TO CONTINUE → consent and preparation

  [TABLE] End state | Name | Meaning
  * READY_SAFE
      Name: Ready for incision — safe
      Meaning: All blockers resolved; correct pathway
  * POSTPONED_APPROPRIATELY
      Name: Appropriately postponed (S1)
      Meaning: Correct reason found and communicated — counts as SUCCESS
  * REDIRECTED_APPROPRIATELY
      Name: Appropriately redirected
      Meaning: Diagnostic diversion (suspected ectopic → gynaecology); cholangitis → biliary drainage;
          inflamed gallbladder → acute pathway
  * SAFE_BUT_DELAYED
      Name: Safe but delayed
      Meaning: Right outcome with avoidable delays
  * SAFETY_FAILURE
      Name: Safety failure
      Meaning: A blocker was violated or a life-threatening event missed
```


### S1 master flow (WT Part F)

```text
PART F — S1 Master Scenario (Elective
Laparoscopic Cholecystectomy)
S1 master flow
Arrival → Initial assessment → Observations → History
Red flags (jaundice, fever, constant pain)? Acute branch: TG18 / ASGE / cholangitis → senior
review; planned list changed
YES
NO
Investigations → Results
Abnormal/optimisable finding? Verify → assess → review → optimise / defer /
escalate
YES
NO
Optimisation → Consent → Anaesthetic assessment
WHO Sign In → (theatre ready) → WHO Time Out
Procedure → intra-op events → WHO Sign Out →
Recovery/handover → Debrief
Deterioration and emergency branches (stages 18–19) can start at any earlier point.

  [TABLE] Stage | Title | Trainee action | Abnormal branches | If missed
  * 1
      Title: Patient arrival
      Trainee action: Greets, introduces self; checks bands
      Abnormal branches: Unwell on arrival (fever, jaundice, acute pain) → assess for acute illness
      If missed: Poor start; acute illness missed
  * 2
      Title: Initial assessment
      Trainee action: Compare answer vs band + notes; ask allergy reaction details
      Abnormal branches: Mismatch → stop; wrong procedure described → surgical team
      If missed: Wrong-patient / allergy events
  * 3
      Title: Observations
      Trainee action: Attach devices; count RR; repeat abnormal readings
      Abnormal branches: Fever, tachycardia, BP ≥180/110, low SpO2 → verify, assess, review
      If missed: Unexplained abnormality surfaces at Sign In
  * 4
      Title: History
      Trainee action: Structured questions; hidden facts only if asked
      Abnormal branches: Jaundice/fever/constant pain; drug or fasting problem; pregnancy possible
      If missed: Hidden facts cause late stops
  * 5
      Title: Investigations
      Trainee action: Order panel; bedside labelling after ID check
      Abnormal branches: Unindicated tests logged; mislabelled → incident
      If missed: Over/under-testing in debrief
  * 6
      Title: Results
      Trainee action: Open each; repeat suspicious; sign off
      Abnormal branches: See 7
      If missed: Unacknowledged critical result blocks progress
  * 7
      Title: Abnormal branches
      Trainee action: Verify → assess severity/context → investigate → senior/anaesthetic review
      Abnormal branches: Per branch; cholangitis = emergency
      If missed: Unsafe proceed → safety failure
  * 8
      Title: Optimisation
      Trainee action: Identify needs; request via workflow; clinician confirms
      Abnormal branches: Drug clash/allergy → nurse challenge
      If missed: Wrong plan → debrief
  * 9
      Title: Consent
      Trainee action: Dialogue choices; teach-back
      Abnormal branches: No capacity / language / minor / refusal
      If missed: Invalid consent blocker
  * 10
      Title: Anaesthetic assessment
      Trainee action: Propose ASA; report findings
      Abnormal branches: Uncontrolled comorbidity; breach; difficult airway
      If missed: Wrong ASA corrected
  * 11
      Title: WHO Sign In
      Trainee action: Answer/lead items
      Abnormal branches: Any 'no' → stop and resolve
      If missed: Skipped item → challenge/stop
  * 12
      Title: Theatre preparation (runs in parallel before the patient arrives)
      Trainee action: Inspect/confirm; report faults
      Abnormal branches: Fault → fix/delay
      If missed: Time Out cannot complete
  * 13
      Title: WHO Time Out
      Trainee action: Take part in order
      Abnormal branches: Antibiotic/imaging/equipment gap → fix, repeat
      If missed: Stop/debrief
  * 14
      Title: Procedure start
      Trainee action: Observe
      Abnormal branches: —
      If missed: —
  * 15
      Title: Relevant intra-operative training events (ProcSense extension)
      Trainee action: Communicate; respond to requests
      Abnormal branches: Team decision
      If missed: Freezing → debrief
  * 16
      Title: WHO Sign Out
      Trainee action: Participate
      Abnormal branches: Count discrepancy → search/escalate
      If missed: Specimen error
  * 17
      Title: Recovery / handover
      Trainee action: Hand over
      Abnormal branches: Pain, PONV, bleeding → recovery team
      If missed: Omissions
  * 18
      Title: Deterioration branches
      Trainee action: ABCDE; call; initiate workflow
      Abnormal branches: Emergency escalation
      If missed: Worsening per Part 12 clocks
  * 19
      Title: Emergency branches
      Trainee action: Recognise; call; support team
      Abnormal branches: Emergency team
      If missed: Arrest branch
  * 20
      Title: Debrief
      Trainee action: Review
      Abnormal branches: —
      If missed: —
```


### S2 master flow (WT Part G)

```text
PART G — S2 Master Scenario (Emergency
Appendectomy)
Teacher says:
S2 does NOT follow the S1 timeline: assessment, treatment and preparation overlap, and the clock runs. Surgery for
appendicitis is generally planned promptly; WSES 2025 recommends laparoscopic appendectomy within 24 hours of
admission when surgery is indicated, with the actual timing set by the surgeon and anaesthetist according to
severity, sepsis, anaesthetic assessment and resources CLINICAL REFERENCE — SOURCE BACKED .
S2 master flow
Arrival/triage → Initial assessment (+ early
analgesia/IV) → Observations → History
Differential-diagnosis check: pregnancy possible,
test positive, with pain?
DIAGNOSTIC DIVERSION (not an appendicitis
complication): ectopic pregnancy must be
considered → gynaecology
YES
NO
High-risk sepsis (NG253) / shock? Sepsis branch in parallel
YES
NO
Investigations → Results (score, imaging) → Abnormal
branches → Optimisation in parallel
Complicated disease? Urgent source control; antibiotics 2–3 days
post-op
YES
NO
Consent → Anaesthetic assessment (RSI) → Sign In →
Theatre → Time Out
Procedure → intra-op events → Sign Out →
Recovery/handover → Debrief

  [TABLE] Stage | Title | Trainee action | Abnormal branches | If missed
  * 1
      Title: Patient arrival
      Trainee action: Note triage; start pain pathway
      Abnormal branches: Shocked on arrival → resus area
      If missed: Delay
  * 2
      Title: Initial assessment
      Trainee action: ID and allergy; request analgesia and IV access via workflow
      Abnormal branches: Mismatch → resolve
      If missed: Wrong patient/drug
  * 3
      Title: Observations
      Trainee action: Attach; count RR; repeat
      Abnormal branches: NEWS2 ≥5 / single 3 → review; ≥7 high risk
      If missed: Trend missed → deterioration
  * 4
      Title: History
      Trainee action: Structured questions
      Abnormal branches: Differential diagnosis / diagnostic diversion: conditions that mimic appendicitis
          (e.g. ectopic pregnancy, ovarian torsion, urinary stone) — pregnancy status and clinical assessment
          can redirect the pathway; allergy
      If missed: Ectopic missed
  * 5
      Title: Investigations
      Trainee action: Order; arrange monitored transfer if unstable
      Abnormal branches: Unstable → monitored transfer
      If missed: Corridor event
  * 6
      Title: Results
      Trainee action: Acknowledge; score
      Abnormal branches: Equivocal/negative/complicated
      If missed: 'Not seen' treated as negative
  * 7
      Title: Abnormal branches
      Trainee action: Verify → assess → initiate workflow
      Abnormal branches: Per branch
      If missed: Deterioration
  * 8
      Title: Optimisation (in parallel)
      Trainee action: Identify need; request; clinician confirms; nurse administers
      Abnormal branches: No response → escalate
      If missed: Clock speeds up
  * 9
      Title: Consent
      Trainee action: Dialogue; teach-back
      Abnormal branches: No capacity → senior/emergency route
      If missed: Invalid consent
  * 10
      Title: Anaesthetic assessment
      Trainee action: Report fasting times; findings
      Abnormal branches: Airway/aspiration issues
      If missed: Aspiration
  * 11
      Title: WHO Sign In
      Trainee action: Answer/lead
      Abnormal branches: Any 'no' → resolve
      If missed: Stop
  * 12
      Title: Theatre preparation
      Trainee action: Confirm
      Abnormal branches: Fault → substitute
      If missed: Delay
  * 13
      Title: WHO Time Out
      Trainee action: Participate
      Abnormal branches: Re-dose/fix
      If missed: Stop
  * 14
      Title: Procedure start
      Trainee action: Observe
      Abnormal branches: —
      If missed: —
  * 15
      Title: Relevant intra-operative training events (ProcSense extension)
      Trainee action: Communicate
      Abnormal branches: Team plan
      If missed: Debrief
  * 16
      Title: WHO Sign Out
      Trainee action: Participate
      Abnormal branches: Count/specimen issue
      If missed: Error
  * 17
      Title: Recovery / handover
      Trainee action: Hand over
      Abnormal branches: Sepsis/ileus/bleeding signs
      If missed: Omission
  * 18
      Title: Deterioration branches
      Trainee action: ABCDE; SBAR; initiate workflow
      Abnormal branches: Emergency
      If missed: Worsening
  * 19
      Title: Emergency branches
      Trainee action: Call; support
      Abnormal branches: Emergency team
      If missed: Arrest
  * 20
      Title: Debrief
      Trainee action: Review
      Abnormal branches: —
      If missed: —
```


# THE 15 STEPS

```text
Walkthrough specification + VR implementation for each step.
```


## 6. STEP 1 — PATIENT ARRIVAL AND IDENTIFICATION

```text
Priority P0 · walkthrough p.74 · build guides: B-R11, B-S7, B-S9, B-M11, B-M18
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Meeting the patient, making 100% sure we have the right person, checking
                                allergies, and opening their hospital record.
  Why are we doing it?        : Wrong-patient errors (wrong blood sample, wrong medicine, wrong operation) are
                                some of the most serious mistakes in hospitals. An allergy that isn't recorded
                                can kill when a medicine is given. Everything later depends on this step
                                CLINICAL REFERENCE — SOURCE BACKED .
  What does the trainee actually do?: 1. Introduce yourself: name and role ("Hello, I'm Dr Ruhan, one of the
                                surgical doctors"). 2. Ask the patient to SAY their full name and date of
                                birth. Never read it to them ("You're Ayesha Khan, right?") — anxious people
                                say yes to anything. 3. Compare their answer with the wristband AND the notes.
                                At least two identifiers must match CLINICAL REFERENCE — SOURCE BACKED
                                (locally usually name + date of birth + hospital/MR number CONTEXT-DEPENDENT —
                                INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ). 4. Ask: "Do you have any
                                allergies? What happened when you had it?" Check the allergy band (often red
                                CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ). 5. Ask:
                                "Can you tell me what operation you're here for / what's brought you in?" 6.
                                S1: check a responsible adult can take them home and stay for 24 hours (needed
                                for day surgery) CLINICAL INTERPRETATION . 7. Record arrival time; open the
                                admission record.
  What does the trainee see?  : S1: day-surgery bay; calm patient sitting on a trolley in their own clothes;
                                folder; nurse. S2: Emergency Department cubicle; patient curled up holding the
                                lower right tummy; a relative; triage note on a clipboard.
  What information do they receive?: Patient's spoken name and date of birth, wristband text, folder details,
                                allergy answer. S2 triage note, e.g. "19M, RIF pain 20 h, HR 108, T 37.9, pain
                                8/10, category: very urgent" (RIF = right iliac fossa = lower right tummy).
  What does that information mean?: If all identifiers match, we can safely take samples and give treatment.
                                An allergy with a severe reaction (swelling, breathing trouble) means that
                                medicine and related ones must be avoided.
  What is the normal/reference value?: Identity: every identifier matches. Allergy: 'none known' OR allergy
                                recorded with reaction and band in place.
  What counts as abnormal?    : Any mismatch (spelling, date, MR number); an allergy the patient mentions that
                                isn't recorded; patient describing a different operation or side.
  What counts as seriously concerning?: Patient cannot confirm identity (unconscious, confused, language
                                barrier) and documents are missing; history of anaphylaxis.
  What could cause the abnormal result?: Admin errors, two patients with similar names, patient confusion,
                                missing wristband.
  What should the trainee check next?: 1. Mismatch → stop; ask the nurse/admissions to resolve before anything
                                else. 2. Patient can't confirm → use wristband + notes + relative +
                                interpreter; follow the hospital unknown-patient policy CONTEXT-DEPENDENT —
                                INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . 3. New allergy → update
                                record, apply band, tell the nurse. 4. Different operation described → call
                                the surgical team before continuing.
  What happens if the trainee finds nothing wrong?: Go to STEP 2 (history). The game unlocks blood tests and
                                medicines only now.
  What happens if the trainee finds something abnormal?: Stop and fix it first. Nothing else is allowed until
                                identity is certain.
  What happens if the abnormality is mild?: Small spelling error in notes with all other identifiers matching
                                → nurse corrects, then continue.
  What happens if it is severe?: Real identity doubt → no samples, drugs, transfer. Severe allergy → flagged
                                everywhere; affects antibiotic choice later.
  When do we call the senior doctor?: Patient describes a different operation or side than booked; identity
                                cannot be resolved.
  When do we call the anaesthetist?: Severe allergy history (especially to anaesthetic drugs, latex,
                                chlorhexidine).
  When might surgery be delayed?: S1: if identity or the planned operation can't be confirmed.
  When should emergency surgery continue despite the abnormality?: S2: an unconscious emergency patient can be
                                treated using emergency identification procedures; life-saving treatment is
                                not withheld while paperwork is fixed CONTEXT-DEPENDENT — INSTITUTIONAL
                                PROTOCOL / CLINICIAN CONFIRMATION .
  What happens if the trainee misses it?: Reading the name to the patient → patient agrees with a wrong name →
                                later the blood sample belongs to someone else ('wrong blood in tube' event).
                                Missing an allergy → allergic reaction later (Part 14).
  What happens after the correct action?: Nurse confirms: "Identity checked, allergy band on." The game marks
                                the patient 'identified'.
  Medical words used here     : Identifier A piece of information that uniquely identifies a person (full
                                name, date of birth, hospital number). MR number Medical record number — the
                                hospital's ID for the patient. Anaphylaxis A severe, life-threatening allergic
                                reaction. Triage Sorting patients by urgency on arrival.
  Where this comes from       : WHO Surgical Safety Checklist (2009); NICE NG180 Perioperative care;
                                Manchester Triage System concept.
  Clinician validation        : Hospital: identifier set, allergy band colour, unknown-patient procedure,
                                triage system.
```


### What the player sees (WT Part 16 — Stage 1)

```text
  On screen / in front of the player: S1: day-surgery bay, patient seated, folder on the trolley, nurse
                                nearby. S2: ED cubicle, patient curled on trolley, relative, triage clipboard.
  Patient — what they say     : S1: "Hello, I'm here for my gallbladder operation." S2: groaning; "It really
                                hurts…"
  Monitor — numbers           : Blank until devices attached (S2 triage numbers on clipboard).
  Interaction                 : Wristband (rotate wrist to read), folder, allergy band, speak to
                                patient/relative.
  Information revealed        : Name/DOB spoken by patient; folder pages; allergy details after asking 'what
                                happened?'.
  Decision                    : Is this the right patient? Any allergy?
  Correct action →            : Nurse: "Thanks, I'll update the record." Tests/medicines unlock.
  Incorrect action →          : Reading the name to the patient → patient agrees (even if wrong in a variant)
                                → later mismatch event.
  Missed abnormality →        : Unrecorded allergy → reaction later.
  Escalation →                : Can call admissions/senior for mismatch.
```


#### VR reasoning card C.1 — Identity check

```text
  Why                         : Prevent wrong-patient care
  Trainee action              : Open questions; compare wristband + notes; allergy
  What the trainee sees       : Patient, wristband, folder
  Information                 : Name/DOB/MR no.; allergy
  Interpretation              : Match = safe to proceed
  Decision                    : Is identity certain?
  Correct action              : Actions unlock
  Incorrect action            : Reading name to patient → agrees wrongly
  Missed abnormality          : Wrong-blood event later
  Escalation                  : Admissions/nurse
  Consequence                 : Mismatch blocks all actions
  Next step                   : History

Scenario maps: S1 stages 1 Patient arrival, 2 Initial assessment; S2 stages 1 Patient arrival, 2 Initial
assessment.
```


### Implementation (FYP design)

```text
  Real workflow               : Nurse admits, doctor clerks; identity is re-checked at every hand-off.
  FYP simulation              : The trainee performs the identity check at first contact; nurse NPC has
                                already applied the wristband.
  FYP simplification          : Admissions paperwork is a single tablet 'Admission record'; no bed-management
                                system.
  AI                          : None required. Optional P1: patient voices answers via LLM phrasing grounded
                                on the patient card (identity facts are never AI-generated).

  Room                        : S1: ward bay (bed, curtain, bedside locker, chart holder). S2: ED cubicle
                                (trolley, wall monitor, curtain).
  Patient                     : S1 sitting on bed in own clothes/gown; S2 lying on trolley, guarding abdomen.
  Staff                       : Nurse NPC at bedside (hands over referral/triage note).
  Equipment                   : Wristband on patient's wrist; notes folder/tablet; allergy band (if
                                applicable); ED: wall monitor (not yet connected).
  Available interactions      : Talk menu, grab wristband / notes, tablet 'Admission' tab.

USER MUST:

  1. Walk/teleport to the bedside anchor.
  2. Select 'Introduce yourself' (or say it — P1 voice).
  3. Select 'Ask name and date of birth' — the patient answers.
  4. Point/grab the patient's wrist to read the wristband.
  5. Open the notes/tablet and compare the three identifiers.
  6. Select 'Confirm identity' OR 'Report mismatch'.
  7. Ask 'Why are you here today?' and check against the booking/triage note.
  8. Ask the patient to SAY name and DOB (never read it to them).
  9. Rotate wrist to read wristband; compare with spoken answer AND notes (≥2 identifiers).
  10. Ask allergy AND 'what happened?'; check allergy band.
  11. S1: confirm responsible adult home + 24 h; S2: read triage clipboard.

SYSTEM THEN:

  1. Emits say(INTRODUCE_SELF) and ask(identity) actions to the Core.
  2. Reveals identity answer from the patient profile (may differ in a mismatch scenario).
  3. When wristband is read and notes opened, enables 'Confirm identity'.
  4. Compares: profile.identity == band == notes.
  5. Sets clinical.identity_verified = true (only if all match) and logs evidence.
  6. Updates behaviour: anxiety −1, trust +1 when introduction happened first.
  7. Reading the name to the patient → patient agrees even in a mismatch variant → WRONG-BLOOD-IN-TUBE event
     later (WT Step 1).
  8. Blood tests and medicines UNLOCK only after identity is certain (WT Step 1).
  9. Unconscious S2 patient → emergency identification route; life-saving treatment not withheld (Type C).

  If correct →                : Identity verified flag set; tablet shows green 'ID confirmed'; Step 2 topics
                                unlock; score item ID-01 met.
  If incorrect →              : If the trainee confirms identity when a mismatch exists (scenario), or starts
                                any intervention before confirming, a CRITICAL score item is logged; practice
                                mode shows a red banner 'Identity not confirmed'.
  If information missing →    : If the wristband is missing (scenario), 'Confirm identity' stays disabled;
                                trainee must ask the nurse NPC to apply one after checking records.
  If safety condition fails → : Any treatment/sample/transfer action attempted before identity_verified is
                                REJECTED by the Core with reason 'Identity not confirmed' (logged).

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L1-01
      Trigger: verify_identity
      Condition: patient present in same location
      Check: profile.id == band.id == notes.id
      Correct: identity_verified=true; unlock history
      Incorrect: if mismatch: mismatch_state; critical if user confirms anyway
      Feedback: Green tick / red banner
      Animation: Nurse nods / nurse frowns + 'Let me check'
      Next state: ST2_HISTORY or ST1_MISMATCH
  * L1-02
      Trigger: any treatment/sample before L1-01
      Condition: —
      Check: identity_verified == false
      Correct: —
      Incorrect: action rejected + CRITICAL item
      Feedback: 'Identity not confirmed'
      Animation: Hand-raise 'stop' icon
      Next state: unchanged
  * L1-03
      Trigger: say INTRODUCE_SELF
      Condition: first contact
      Check: first act this session?
      Correct: trust+1, anxiety−1
      Incorrect: if skipped: minor communication item
      Feedback: —
      Animation: Patient makes eye contact
      Next state: unchanged
  * L1-04
      Trigger: report_mismatch
      Condition: band/notes differ
      Check: scenario mismatch active
      Correct: mismatch resolved by nurse NPC (new band)
      Incorrect: if no mismatch: minor 'unnecessary delay'
      Feedback: Nurse line
      Animation: Nurse leaves & returns with band
      Next state: ST1_ARRIVAL
  * L1-WT1
      Trigger: read_name_to_patient
      Condition: —
      Check: flag misidentification
      Correct: —
      Incorrect: Wrong-blood event armed
      Feedback: Debrief
      Animation: —
      Next state: risk.wrong_blood
  * L1-WT2
      Trigger: allergy_reaction_asked
      Condition: allergy present
      Check: reaction recorded
      Correct: Severe allergy flagged everywhere (antibiotic choice later)
      Incorrect: 'Penicillin allergy' without reaction → incorrect (WT 14.4)
      Feedback: —
      Animation: —
      Next state: allergies[]

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E1-A
      What went wrong: Trainee reads name to patient ('You are Ayesha Khan?')
      User sees: Patient says 'Yes' even in mismatch variant
      Should notice: Closed question can't verify identity
      Correct response: Ask open question
      System does: Logs closed-question identity check (minor); in mismatch variant proceeds to wrong-identity
          risk
      Progression: Allowed (practice) with warning
      Feedback: 'Use an open question to confirm identity'
      State: ST1_ARRIVAL
      Resolution: Trainee re-asks with open question
  * E1-B
      What went wrong: Wristband belongs to another patient (mismatch variant)
      User sees: Band shows different DOB
      Should notice: Band ≠ patient answer
      Correct response: Stop, report to nurse
      System does: Blocks treatment actions; nurse fetches correct band
      Progression: Blocked until resolved
      Feedback: 'Identifiers do not match — resolve before continuing'
      State: ST1_MISMATCH
      Resolution: Nurse re-bands after records check
  * E1-C
      What went wrong: Allergy said by patient but no allergy alert
      User sees: Patient mentions 'penicillin rash' during greeting
      Should notice: Documented status is wrong
      Correct response: Flag and update (Step 2)
      System does: Creates pending task 'allergy alert'
      Progression: Allowed
      Feedback: Tablet reminder
      State: unchanged
      Resolution: Resolved in Step 2

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Allergy band
      Use S1/S2: ✓/✓
      Steps: 1, 7, 13
      Information provided: Allergy flag (details by asking)
      Failure scenario: Missing band → reaction branch
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Wheelchair
      Use S1/S2: ✓/—
      Steps: 1, 6
      Information provided: Transport cue
      Failure scenario: Unstable patient must not go by wheelchair
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Implement ActionAPI verbs verify_identity/report_mismatch and the identity rule L1-01/L1-02 in
Core (pure C#), with xUnit tests T01-1..3. Add identity facts to PatientProfile + disclosure rule for
'ask(identity)'. Sannan — Build ward-bay and ED-cubicle anchors in the provided environment; place
bed/trolley, patient, nurse NPC. Make wristband interactable (XRI poke/grab → UI card) and nurse hand-out
animation A1-01; ID compare card UI. Moiz — Write scenario content for arrival (S1 referral, S2 triage note)
and synthetic demographics generator (no real names). Author rules L1-01..L1-04 in content/rules with sources
WT Part 15 (WHO SSC 2009)/Institutional protocol (WT Type C) and status SOURCE_IDENTIFIED.

Validation: Number/type of identifiers and allergy-band colour follow the supervising hospital's policy.
[CLINICAL VALIDATION REQUIRED] · Procedure when a patient cannot confirm identity. [CLINICAL VALIDATION
REQUIRED] · Hospital: identifier set, allergy band colour, unknown-patient procedure, triage system.
```


## 7. STEP 2 — ASKING QUESTIONS (THE HISTORY)

```text
Priority P0 · walkthrough p.76 · build guides: B-R6, B-R3, B-S10, B-M3, B-M19
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : A structured conversation to learn what's wrong, what other illnesses and
                                medicines the patient has, and anything that makes surgery unsafe.
  Why are we doing it?        : Most of the information needed for a diagnosis comes from the history. Many
                                dangers (blood thinners, recent food, pregnancy) are invisible unless you ask
                                CLINICAL INTERPRETATION .
  What does the trainee actually do?: 1. Main complaint: "What's the problem? Where's the pain? When did it
                                start? Does it move? What makes it worse? 0–10?" 2. Red-flag questions
                                (warning signs). S1: fever? yellow skin/eyes? dark urine/pale stools? constant
                                pain over 24 h? pain through to back? S2: pain moved from belly button?
                                vomiting? appetite? fever? urine symptoms? diarrhoea? women: last period,
                                chance of pregnancy, vaginal bleeding, shoulder-tip pain; men: testicle pain.
                                3. Other illnesses: heart, lungs, blood pressure, diabetes, kidney, liver,
                                bleeding problems, previous clots, sleep apnoea, thalassaemia/sickle cell. 4.
                                Medicines: everything, especially blood thinners (and when last taken),
                                diabetes medicines, GLP-1 injections, steroids, BP tablets, the pill, herbal
                                remedies. 5. Allergies: what and what happened. 6. Previous anaesthetics: any
                                problems; family members with serious anaesthetic problems. 7. Social:
                                smoking, alcohol, drugs, who's at home. 8. Last food and drink: exact time and
                                what. 9. Pregnancy (whenever pregnancy is possible for this patient): asked
                                privately and sensitively; test offered with consent if there is any doubt
                                CLINICAL REFERENCE — SOURCE BACKED (NICE NG45). 10. Summarise back and ask
                                "Anything else you want to tell me?"
  What does the trainee see?  : The patient (and maybe a relative); the clerking form fills in as answers are
                                given.
  What information do they receive?: Spoken answers, e.g. S1: "The pain comes after oily food, under my right
                                ribs, then goes after an hour or two." If asked about blood thinners: "I take
                                apixaban — I had it this morning." S2: "It started around my belly button last
                                night and now it's here [points lower right]. I threw up once."
  What does that information mean?: S1 'comes and goes after fatty food' = typical gallstone pain (biliary
                                colic) — fits the plan. 'Constant pain with fever' = possible inflamed
                                gallbladder. S2 'pain moved + vomiting + no appetite' = classic appendicitis
                                story.
  What is the normal/reference value?: S1: typical gallstone attacks, no red flags, medicines manageable,
                                fasted correctly. S2: there is no 'normal' — we're collecting clues.
  What counts as abnormal?    : Any red-flag answer; a blood thinner not stopped as planned; food within
                                fasting limits; possible pregnancy; new allergy.
  What counts as seriously concerning?: Fever + jaundice + pain (possible cholangitis); positive pregnancy
                                chance with pain and dizziness (possible ectopic); chest pain or
                                breathlessness; severe allergy history.
  What could cause the abnormal result?: The disease progressing (S1 becoming acute), other conditions
                                mimicking it, patient not following instructions, missed medicine plan.
  What should the trainee check next?: 1. Red flags → focused examination (Step 4) and relevant tests. 2.
                                Blood thinner/diabetes/GLP-1 → medicine plan (Part 8). 3. Food → fasting
                                branch (Part 9). 4. Possible pregnancy → pregnancy test with consent (Step 6).
  What happens if the trainee finds nothing wrong?: Go to STEP 3 (vital signs).
  What happens if the trainee finds something abnormal?: Record it, then follow the branch in 'what next'.
  What happens if the abnormality is mild?: e.g. smoker, mild reflux → note and tell anaesthetist; continue.
  What happens if it is severe?: e.g. took apixaban this morning before planned surgery → S1 will be
                                postponed; e.g. chest pain → emergency assessment.
  When do we call the senior doctor?: Red flags suggesting complication (S1 jaundice/fever; S2 a possible
                                appendicitis mimic such as ectopic pregnancy); patient describes a different
                                problem than booked.
  When do we call the anaesthetist?: Fasting breach; previous anaesthetic problems; sleep apnoea; GLP-1
                                injections; blood thinners; heart/lung disease.
  When might surgery be delayed?: S1: blood thinner not stopped; recent food; possible pregnancy; new serious
                                symptoms.
  When should emergency surgery continue despite the abnormality?: S2: recent food or blood thinners do NOT
                                stop emergency surgery — they change how it's done (RSI, reversal decisions).
  What happens if the trainee misses it?: Hidden facts stay hidden: e.g. the morning apixaban is discovered by
                                the anaesthetist at Sign In → case stops, debrief 'missed medicine history'.
                                Missing the pregnancy question → blocked at checklist.
  What happens after the correct action?: The clerking note is complete; decisions in later steps unlock.
  Medical words used here     : Red flag A warning symptom that suggests something more serious. Biliary colic
                                Cramping gallstone pain that comes and goes. Jaundice Yellow skin/eyes from
                                bilirubin build-up. LMP Last menstrual period — date the last period started.
                                Ectopic pregnancy Pregnancy growing outside the womb; can rupture and bleed.
  Where this comes from       : NICE NG45 (pregnancy question); NICE CG188; WSES 2020; StatPearls (acute
                                appendicitis).
  Clinician validation        : Surgeon: which facts are hidden until asked; patient answers per variant.
```


### What the player sees (WT Part 16 — Stage 2)

```text
  On screen / in front of the player: Patient (and relative); clerking form.
  Patient — what they say     : Volunteers only the main complaint; other facts only if asked (e.g. "I took
                                apixaban this morning").
  Monitor — numbers           : Unchanged.
  Interaction                 : Question menu or voice: pain, red flags, medicines, allergies, anaesthetics,
                                social, fasting, pregnancy (privacy option to ask relatives to step out).
  Information revealed        : Each answer adds a line to the clerking note.
  Decision                    : What's the story? Any red flags?
  Correct action →            : Relevant facts unlock correct branches.
  Incorrect action →          : Skipping fasting/medicine/pregnancy questions → blockers later.
  Missed abnormality →        : Yellow-eyes comment ignored → abnormal LFTs force it later.
  Escalation →                : Red flags → call senior.
```


#### VR reasoning card C.2 — History

```text
  Why                         : Most diagnoses and dangers come from questions
  Trainee action              : Structured questions incl. red flags, drugs, fasting, pregnancy
  What the trainee sees       : Patient, clerking form
  Information                 : Answers (hidden until asked)
  Interpretation              : Red flags change pathway
  Decision                    : Any blocker or red flag?
  Correct action              : Correct branch triggered
  Incorrect action            : Skipped questions
  Missed abnormality          : Hidden facts surface at Sign In
  Escalation                  : Senior for red flags
  Consequence                 : Case may stop later
  Next step                   : Observations
```


#### VR reasoning card C.11 — Pregnancy possible

```text
  Why                         : Changes diagnosis and safety
  Trainee action              : Ask privately; consent; test
  What the trainee sees       : Test kit
  Information                 : Result
  Interpretation              : Positive + pain: ectopic pregnancy is a differential diagnosis that can
                                redirect the pathway
  Decision                    : Pathway change?
  Correct action              : Correct team
  Incorrect action            : Not asking
  Missed abnormality          : Safety failure
  Escalation                  : Senior; gynaecology/obstetrics
  Consequence                 : Pathway switch
  Next step                   : Continue/redirect
```


#### VR reasoning card C.15 — Anticoagulant

```text
  Why                         : Bleeding vs clot risk
  Trainee action              : Ask last dose time; renal function
  What the trainee sees       : Drug list
  Information                 : Times
  Interpretation              : Within stop window?
  Decision                    : Proceed/defer?
  Correct action              : Correct decision
  Incorrect action            : Not asking
  Missed abnormality          : Sign In stop
  Escalation                  : Anaesthetist/senior
  Consequence                 : Deferral
  Next step                   : Plan
```


#### VR reasoning card C.16 — Fasting

```text
  Why                         : Aspiration risk
  Trainee action              : Exact times/type; GLP-1
  What the trainee sees       : Patient
  Information                 : Times
  Interpretation              : Breach? Full stomach?
  Decision                    : Inform anaesthetist?
  Correct action              : Re-time / RSI
  Incorrect action            : Rounding times
  Missed abnormality          : Aspiration
  Escalation                  : Anaesthetist
  Consequence                 : Timing change
  Next step                   : Prep

Scenario maps: S1 stages 4 History; S2 stages 4 History.

  [TABLE] What-if | Branch | Correct action
  * WI-08
      Branch: What if breathing is very fast? (25 or more)
      Correct action: Treat cause; sepsis bundle if infection.
  * WI-26
      Branch: What if the patient is pregnant?
      Correct action: S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise →
          gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok.
  * WI-27
      Branch: What if pregnancy status is unknown or the patient declines a test?
      Correct action: Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior +
          anaesthetist decide whether to proceed.
  * WI-28
      Branch: What if the patient is allergic to an antibiotic?
      Correct action: Record; choose antibiotic by hospital rule (mild rash: cefazolin often allowed; severe:
          per policy) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
  * WI-29
      Branch: What if an allergic reaction happens after a medicine?
      Correct action: STOP the drug; call for help; lie flat (sit up if struggling to breathe); call the
          emergency team; the responsible clinician gives IM adrenaline (adult standard dose 500 micrograms,
          repeat after 5 min if no improvement EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE
          PRESCRIPTION ); high-flow oxygen; IV fluid bolus per Resuscitation Council UK (adults 500–1000 mL
          crystalloid).
  * WI-30
      Branch: What if the patient takes blood thinners?
      Correct action: S1: proceed only if stopped per plan (Part 8); otherwise postpone; never stop
          antiplatelets after a recent stent without the heart team. S2: senior/haematology decide reversal
          PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED .
  * WI-31
      Branch: What if the patient has diabetes?
      Correct action: Medicine plan (Part 8); first on list if possible; check sugar at least hourly; target
          6–10.
  * WI-32
      Branch: What if the patient takes steroids, GLP-1 injections or SGLT2 tablets?
      Correct action: Per Part 8/9.
  * WI-33
      Branch: What if the patient hasn't fasted?
      Correct action: Tell anaesthetist honestly.
  * WI-34
      Branch: What if fasting status is unknown?
      Correct action: Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In.
```


### Implementation (FYP design)

```text
  Real workflow               : A full clerking takes 15–30 min.
  FYP simulation              : History is a grouped question menu (P0) or free speech (P1); answers come from
                                the patient card.
  FYP simplification          : Systems review compressed to 6 relevant items; social history only where it
                                affects the case (e.g. no one at home → not day case).
  AI                          : P1 PROPOSED FYP AI FEATURE: (a) LLM phrases patient answers in persona voice
                                from approved facts (grounding validator); (b) ASR converts spoken questions
                                to topics; (c) communication classifier labels acts (debrief-only unless
                                validated). NOT a clinical standard.

  Room                        : Same as Step 1.
  Patient                     : Sitting (S1) / lying curled (S2).
  Staff                       : Nurse may leave to do other tasks (environment event).
  Equipment                   : Tablet with Ask menu; medicine packets prop (optional).
  Available interactions      : Ask menu groups: Complaint, Past history, Medicines, Allergies, Anaesthetic,
                                Social, Intake, Pregnancy (eligible only).

USER MUST:

  1. Open 'Ask' on the tablet (or speak — P1).
  2. Ask complaint/HPC questions (SOCRATES group).
  3. Ask past medical/surgical history.
  4. Ask drug history; optionally inspect medicine packets.
  5. Ask allergies AND the reaction.
  6. Ask previous anaesthetic problems.
  7. Ask last food and last drink separately.
  8. If eligible: ask about possibility of pregnancy (sensitive option).
  9. Summarise and invite questions.
  10. Ask main complaint, red flags (S1 fever/jaundice/dark urine/pale stools/constant >24 h/back pain; S2
      migration/vomiting/appetite/fever/urinary/diarrhoea; women LMP, pregnancy, PV bleeding, shoulder-tip;
      men testicular pain).
  11. Ask exact last food AND drink (time + type).
  12. Ask pregnancy privately (privacy option: relatives step out); offer test with consent if any doubt.
  13. Ask anticoagulant, diabetes, GLP-1, SGLT2, steroid drugs and the time of the last dose.
  14. Summarise; 'Anything else?'

SYSTEM THEN:

  1. Each ask(topic) looks up the patient card disclosure rule.
  2. If trust ≥ threshold (or topic asked directly) the hidden fact is revealed; else vague answer.
  3. Revealed facts appear in the 'Known' panel of the tablet.
  4. Fasting rule R-FAST-01 is evaluated when both last-food and last-drink are known.
  5. Allergy disclosure not matching chart creates pending 'update allergy alert'.
  6. Behaviour: jargon without explanation lowers understanding; summarising raises trust.
  7. Patient volunteers ONLY the main complaint; every other fact is HIDDEN until asked (WT Part 16 St.2).
  8. Each answer adds a line to the clerking note.
  9. Skipped medicine/fasting/pregnancy questions → hidden fact surfaces later (e.g. anaesthetist at Sign In →
     case stops; debrief 'missed medicine history'); missed pregnancy question → blocked at checklist.

  If correct →                : Known panel filled; goals G-HX, G-ALLERGY, G-FASTING (and G-PREG if eligible)
                                complete; relevant decision points (DP-S1-FAST) open.
  If incorrect →              : Missing key topics creates later consequences (e.g. allergy unknown →
                                hypersensitivity event risk in Step 8; fasting unknown → Sign In discrepancy
                                in Step 13).
  If information missing →    : Patient answers 'I'm not sure' → trainee can check notes, ask nurse NPC, or
                                record 'uncertain'.
  If safety condition fails → : If allergy is not asked before Step 8 treatment, the treatment check 'allergy
                                status known' fails → treatment blocked in practice mode (warning), allowed
                                but CRITICAL in assessment mode.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L2-01
      Trigger: ask(allergies)
      Condition: identity_verified
      Check: profile.allergies disclosed?
      Correct: allergies_known updated
      Incorrect: —
      Feedback: Known panel entry
      Animation: Patient nods
      Next state: ST2_HISTORY
  * L2-02
      Trigger: ask(last_food)+ask(last_drink)
      Condition: —
      Check: compare with fasting rule
      Correct: npo.verified=true; if breach → DP-S1-FAST
      Incorrect: if never asked: later critical at Sign In
      Feedback: 'Fasting: meets rule / breach'
      Animation: —
      Next state: ST2 or DP
  * L2-03
      Trigger: ask(pregnancy)
      Condition: eligible (female, age band)
      Check: asked sensitively (option type)
      Correct: pregnancy discussion documented
      Incorrect: asked insensitively → minor communication; not asked → goal pending
      Feedback: —
      Animation: Patient embarrassed idle if insensitive
      Next state: [CLINICAL VALIDATION REQUIRED]
  * L2-04
      Trigger: ask(medications)
      Condition: —
      Check: reveal meds incl. hidden anticoagulant/contraceptive
      Correct: meds list; decisions created
      Incorrect: —
      Feedback: Known panel
      Animation: —
      Next state: ST2
  * L2-05
      Trigger: summarise()
      Condition: ≥5 topics asked
      Check: —
      Correct: trust+1, understanding+1
      Incorrect: —
      Feedback: —
      Animation: Patient smiles
      Next state: ST3_EXAM

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E2-A
      What went wrong: Allergy not asked
      User sees: Nothing obvious now
      Should notice: Allergy field empty on tablet
      Correct response: Ask allergies before any drug
      System does: Leaves allergy_known=false; enables E-HYPERSENSITIVITY eligibility later
      Progression: Allowed
      Feedback: Hint (practice) at Step 8
      State: ST2
      Resolution: Resolved when asked
  * E2-B
      What went wrong: Only 'last meal' asked, not drinks
      User sees: Fasting shows 'partial'
      Should notice: Both solids and fluids matter
      Correct response: Ask both
      System does: Fasting rule not evaluated
      Progression: Allowed
      Feedback: 'Fasting status incomplete'
      State: ST2
      Resolution: Resolved when both asked
  * E2-C
      What went wrong: Patient discloses tea with milk 3 h ago (S1-V2)
      User sees: Answer on panel
      Should notice: Milk counts as non-clear fluid [CLINICAL VALIDATION REQUIRED]
      Correct response: Inform anaesthetist (DP-S1-FAST)
      System does: Opens DP-S1-FAST
      Progression: Allowed
      Feedback: Decision card
      State: DP_S1_FAST
      Resolution: Step 5 decision
  * E2-D
      What went wrong: Pregnancy not asked in eligible patient
      User sees: Nothing
      Should notice: Eligibility shown on tablet
      Correct response: Ask sensitively; test if doubt
      System does: Goal G-PREG pending; blocks 'ready' later
      Progression: Blocked at readiness
      Feedback: 'Pregnancy status not established'
      State: ST2
      Resolution: Ask + test

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV2-1 E-PT-DISCLOSES-INTAKE
      Trigger: Trainee asks intake OR nurse NPC: 'She asked for a cup of tea earlier'
      Patient: Patient admits recent tea/snack
      Monitor: —
      Visual/audio: Speech line
      Recognise: A fasting breach
      Response: Inform anaesthetist; delay per decision
      If no response: If ignored: critical at Sign In; aspiration-risk flag

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Implement disclosure rules (trust threshold, direct-ask) and fasting rule evaluator; unit
tests. P1: /dialogue endpoint with grounding validator. Sannan — Ask-menu UI groups + Known panel + subtitles;
patient talk/lip-sync; confused/wince blend shapes. Moiz — Write history cards for S1-V1..8 and S2-V1..8
(facts, hidden flags, answers) from WT Part 2/5D (NICE CG188)/18/20; fasting rule content from WT Part 9 (ASA
fasting) marked [CLINICAL VALIDATION REQUIRED] Pre-render voice lines (Piper offline).

Validation: Hidden-information rules (which facts are only disclosed when asked). [CLINICAL VALIDATION
REQUIRED] · Symptom descriptions for each patient variant. [CLINICAL VALIDATION REQUIRED] · Fasting interval
values used by the rule engine. [CLINICAL VALIDATION REQUIRED] · Surgeon: which facts are hidden until asked;
patient answers per variant.
```


## 8. STEP 3 — CHECKING VITAL SIGNS

```text
Priority P0 · walkthrough p.78 · build guides: B-R13, B-R18, B-S9, B-S10
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Measuring blood pressure, heart rate, breathing rate, oxygen level,
                                temperature, alertness, and pain score, then calculating the NEWS2 early-
                                warning score.
  Why are we doing it?        : Tells us if the patient is stable now and gives a baseline to compare against
                                later (Part 4).
  What does the trainee actually do?: 1. Attach the oxygen finger clip; read oxygen % and heart rate; look at
                                the wave. 2. Put on the BP cuff; wait for the reading. 3. Count breaths for
                                30–60 seconds. 4. Measure temperature. 5. Check alertness (ACVPU). 6. Ask pain
                                score 0–10. 7. Feel the pulse — regular or irregular? 8. Weight/height if not
                                recorded; finger-prick sugar if diabetic. 9. Calculate NEWS2 and pick the
                                right response. 10. Repeat any odd reading.
  What does the trainee see?  : A blank monitor that fills in as devices are attached; NEWS2 chart with any
                                previous readings.
  What information do they receive?: S1 typical: HR 76, BP 124/78, RR 14, SpO2 98% on air, T 36.7, Alert, pain
                                0, NEWS2 0. S2 typical: HR 104, BP 128/76, RR 18, SpO2 98%, T 37.9, Alert,
                                pain 7, NEWS2 1.
  What does that information mean?: S1 values are all normal → stable. S2: fast heart and slightly raised
                                temperature from pain and inflammation — expected in appendicitis but must be
                                watched.
  What is the normal/reference value?: Clinical reference ranges: HR 60–100 at rest (NEWS2 scores 0 only for
                                51–90); BP below 140/90; RR 12–20; SpO2 96%+ on air (target 94–98%); T
                                36.1–37.9 °C; Alert; NEWS2 0 CLINICAL REFERENCE — SOURCE BACKED (RCP NEWS2;
                                BTS 2017; ESH 2023).
  What counts as abnormal?    : Any value in the amber zones of Part 4 (e.g. HR 91–110, BP 101–110, RR 21–24,
                                SpO2 94–95, T 38.0–39.0); NEWS2 1–4.
  What counts as seriously concerning?: Any red value: HR ≥131 or ≤40; top BP ≤90; RR ≥25 or ≤8; SpO2 ≤91; T
                                ≤35.0; new confusion. NEWS2 ≥5 or any single parameter scoring 3; NEWS2 ≥7 =
                                emergency.
  What could cause the abnormal result?: See Part 4: pain, anxiety, dehydration, infection/sepsis, bleeding,
                                abnormal heart rhythm, medicines, lung problems.
  What should the trainee check next?: 1. Repeat the abnormal reading (correct technique). 2. Look at the
                                whole pattern (Part 4.11). 3. Look at the patient: pale? sweaty? breathing
                                hard? 4. Treat obvious causes (pain relief, fluids). 5. Re-measure after 30–60
                                minutes (sooner if red).
  What happens if the trainee finds nothing wrong?: Go to STEP 4 (examination).
  What happens if the trainee finds something abnormal?: Follow the relevant decision diagram in Part 4.
  What happens if the abnormality is mild?: Treat the likely cause and re-check; continue meanwhile.
  What happens if it is severe?: Stop the routine pathway: ABCDE emergency assessment (Part 14) and call for
                                help.
  When do we call the senior doctor?: NEWS2 5–6 (urgent), ≥7 (emergency), any single 3, or persistent
                                abnormality without explanation. In S1, ANY NEWS2 above 1 needs explaining.
  When do we call the anaesthetist?: BP ≥180/110 (S1), low oxygen, very fast/slow/irregular heart, low BP,
                                high BMI/sleep apnoea.
  When might surgery be delayed?: S1: BP ≥180/110 on repeat; fever; new irregular rhythm; any unexplained
                                abnormal vital sign.
  When should emergency surgery continue despite the abnormality?: S2: fast heart, fever, even low BP from
                                sepsis do NOT stop surgery — they make it more urgent, after resuscitation
                                (fluids, antibiotics) because the infected appendix is the cause.
  What happens if the trainee misses it?: Fever in an S1 patient missed → anaesthetist finds 38.6 °C at Sign
                                In → case stops. S2 rising heart rate missed → patient deteriorates into
                                sepsis.
  What happens after the correct action?: Nurse sets the observation frequency according to NEWS2; values
                                become the baseline.
  Medical words used here     : NEWS2 National Early Warning Score 2 (Part 4.9). ACVPU Alert, Confusion,
                                Voice, Pain, Unresponsive.
  Where this comes from       : RCP NEWS2 2017; AAGBI/BHS 2016; BTS 2017; NICE CG65.
  Clinician validation        : Hospital early-warning tool; anaesthetist alarm thresholds.
```


### What the player sees (WT Part 16 — Stage 3)

```text
  On screen / in front of the player: Monitor, finger clip, BP cuff, thermometer, ECG leads, NEWS2 chart.
  Patient — what they say     : "Is this going to hurt?"
  Monitor — numbers           : SpO2 + wave + HR after clip; BP after ~30–40 s; RR must be counted by watching
                                chest; temperature from thermometer.
  Interaction                 : Attach devices; count breaths; feel pulse; fill NEWS2.
  Information revealed        : Numbers; NEWS2 total.
  Decision                    : Normal? Concerning? Emergency?
  Correct action →            : Nurse sets observation frequency.
  Incorrect action →          : Accepting a high BP without repeat → anaesthetist flags later.
  Missed abnormality →        : Rising trend ignored → deterioration branch.
  Escalation →                : NEWS2 5+ → call senior; 7+ → emergency call.
```


#### VR reasoning card C.3 — Observations / NEWS2

```text
  Why                         : Detect instability; baseline
  Trainee action              : Attach devices; count RR; calculate NEWS2
  What the trainee sees       : Monitor, cuff, probe, chart
  Information                 : Values + score
  Interpretation              : Score = urgency of response, not diagnosis
  Decision                    : Which response tier?
  Correct action              : Correct obs frequency/escalation
  Incorrect action            : Accepting one reading without repeat
  Missed abnormality          : Trend missed → deterioration
  Escalation                  : Senior if NEWS2 ≥5/single 3
  Consequence                 : Deterioration clock
  Next step                   : Examination
```


#### VR reasoning card C.4 — Low SpO2

```text
  Why                         : Possible hypoxaemia or artefact
  Trainee action              : Look, assess A/B, check probe, target, oxygen per protocol, cause, reassess
  What the trainee sees       : Patient, waveform, masks
  Information                 : SpO2 + waveform quality
  Interpretation              : Real vs artefact; target range
  Decision                    : Oxygen needed? Cause?
  Correct action              : SpO2 to target; cause found
  Incorrect action            : Blind high-flow O2 / ignoring
  Missed abnormality          : Hypoxia worsens
  Escalation                  : Anaesthetist
  Consequence                 : Breathing branch
  Next step                   : Continue
```


#### VR reasoning card C.5 — High BP (S1)

```text
  Why                         : Peri-operative CV risk
  Trainee action              : Repeat correctly; GP readings; symptoms
  What the trainee sees       : Cuff, monitor
  Information                 : Repeat values
  Interpretation              : <180/110 proceed; ≥180/110 review
  Decision                    : Defer or proceed?
  Correct action              : Correct decision documented
  Incorrect action            : Cancel on one reading / rapid lowering
  Missed abnormality          : Avoidable cancellation
  Escalation                  : Anaesthetist
  Consequence                 : Deferral or proceed
  Next step                   : Continue
```


#### VR reasoning card C.6 — Fever (S1)

```text
  Why                         : Infection in planned patient
  Trainee action              : Examine (Murphy's, jaundice), bloods, US
  What the trainee sees       : Thermometer, patient
  Information                 : T, WBC, CRP, LFTs
  Interpretation              : Acute cholecystitis/cholangitis
  Decision                    : Is this still elective?
  Correct action              : Acute pathway
  Incorrect action            : Paracetamol and proceed
  Missed abnormality          : Stopped at Sign In / sepsis
  Escalation                  : Senior
  Consequence                 : Pathway change
  Next step                   : Acute branch
```


#### VR reasoning card C.7 — Possible sepsis

```text
  Why                         : Organ dysfunction from infection
  Trainee action              : Infection? NEWS2 + single params + organ dysfunction + trend; SBAR
  What the trainee sees       : Monitor, patient, lactate
  Information                 : NEWS2 tier, lactate
  Interpretation              : NG253 risk tier
  Decision                    : High/moderate/low?
  Correct action              : Antibiotics/fluids timed by team
  Incorrect action            : Delay for results
  Missed abnormality          : Shock
  Escalation                  : Senior; critical care
  Consequence                 : Timed bundle
  Next step                   : Source control

Scenario maps: S1 stages 3 Observations; S2 stages 3 Observations.

  [TABLE] What-if | Branch | Correct action
  * WI-01
      Branch: What if blood pressure is too low? (top number 90 or less)
      Correct action: Call for help; lie flat; assess airway/breathing and start oxygen per protocol to the
          patient's target; two drips; request a fluid bolus (e.g. 500 mL crystalloid, NICE CG174; the
          clinician decides volume) and re-check EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE
          PRESCRIPTION ; treat cause (antibiotics if septic; adrenaline if anaphylaxis; blood if bleeding).
  * WI-02
      Branch: What if blood pressure is extremely high? (180/110 or more)
      Correct action: No symptoms: S1 → postpone and refer for control (don't rush BP down). With symptoms →
          emergency medical review.
  * WI-03
      Branch: What if the heart rate is very high? (above 130)
      Correct action: ABCDE; treat cause (pain relief, fluids, antibiotics); ECG-guided rhythm treatment by
          senior/medical team.
  * WI-04
      Branch: What if the heart rate is very slow? (40 or below)
      Correct action: ABCDE; stop causative drugs; emergency medicines/pacing per resuscitation team.
  * WI-05
      Branch: What if the patient has a fever? (38.0 °C or more)
      Correct action: Paracetamol; fluids if dry; antibiotics according to cause; sepsis bundle if criteria
          met.
  * WI-06
      Branch: What if the patient is cold? (36.0 °C or less)
      Correct action: Warm (forced-air blanket); don't send to theatre until 36.0 unless urgent CLINICAL
          REFERENCE — SOURCE BACKED (NICE CG65).
  * WI-07
      Branch: What if oxygen saturation is low? (below 94%)
      Correct action: Look at the patient and assess airway/breathing; check probe and waveform; if the low
          reading is real, sit up and start oxygen per protocol to the patient's target range (94–98%, or
          88–92% if at risk of hypercapnic failure); find the cause (stop fluids if overloaded per senior;
          stimulate if over-sedated — antidote by clinician); reassess within minutes (process in Part 4.5).
  * WI-09
      Branch: What if breathing is very slow? (8 or less)
      Correct action: Wake/stimulate; oxygen; support airway; naloxone antidote PATIENT-SPECIFIC DOSE —
          CLINICIAN CONTROLLED ; stop further opioids.
  * WI-10
      Branch: What if the patient becomes confused?
      Correct action: Treat what's found (sugar below 4 → glucose); sepsis bundle if infection; reassess
          capacity (consent may be invalid now).
  * WI-11
      Branch: What if pain gets much worse or the tummy becomes rigid?
      Correct action: Pain relief; nil by mouth; fluids; antibiotics; prepare for urgent surgery.
  * WI-39
      Branch: What if the patient deteriorates?
      Correct action: Treat as found; increase monitoring.
  * WI-40
      Branch: What if the patient becomes septic?
      Correct action: Sepsis bundle within 1 hour (oxygen, cultures, antibiotics, fluids, lactate, urine
          output).
```


### Implementation (FYP design)

```text
  Real workflow               : Nurse does routine obs every few hours; doctor examines once in detail.
  FYP simulation              : Trainee attaches probe/cuff/thermometer (or asks nurse NPC), counts RR by
                                watching the avatar, feels pulse for regularity, completes NEWS2 and selects
                                the response tier.
  FYP simplification          : RR counted from a visible breathing animation (or 'count' interaction);
                                temperature is a single hotspot; NEWS2 auto-sum shown only after all
                                parameters entered.
  AI                          : None needed. Physiology is deterministic (LitePhysio). NEWS2 is a
                                deterministic calculation — NOT AI.

  Room                        : Same room; curtain closes for exam.
  Patient                     : Sitting/lying; S2 curled in pain
  Staff                       : Nurse NPC (can do obs on request; chaperone).
  Equipment                   : Monitor (SpO2+pleth, NIBP/MAP, ECG leads), tympanic thermometer, glucose meter
                                (diabetics), NEWS2 chart
  Available interactions      : Grab probe/cuff; 'Examine' hotspots; ask nurse for obs.

USER MUST:

  1. Grab SpO2 probe → attach to patient finger socket.
  2. Grab BP cuff → attach to upper-arm socket; press 'Measure' on monitor.
  3. Tap thermometer hotspot; read RR by watching chest (or 'Count RR' button); select consciousness level.
  4. Review computed NEWS2 on tablet.
  5. Feel pulse for regularity; repeat any odd reading with correct technique.
  6. Calculate NEWS2 and choose the response tier; S1: explain ANY NEWS2 above 1.

SYSTEM THEN:

  1. Sensor sockets set sensor.attached; monitor shows values from physiology engine only when attached.
  2. NEWS2 = sum of band points; stored with timestamp.
  3. If NEWS2 ≥ escalation threshold (S2) → goal G-ESCALATE activated with deadline. [CLINICAL VALIDATION
     REQUIRED]
  4. Nurse sets observation frequency from NEWS2 tier (WT 4.9).
  5. Accepting a high BP without repeat → anaesthetist flags later.
  6. Rising trend ignored → deterioration branch; NEWS2 5+ → senior required; 7+ → emergency call.
  7. Display reference ranges (e.g. HR 60–100) but score with NEWS2 bands (WT 4.2).

  If correct →                : Obs set complete; NEWS2 shown; finding codes recorded; next goals unlock.
  If incorrect →              : Examining painful area first → pain spike + trust −1 (communication item). No
                                obs before theatre → later monitoring failure.
  If information missing →    : Missing parameter → NEWS2 greyed 'incomplete'.
  If safety condition fails → : S2: high NEWS2 ignored beyond deadline → deterioration event E-DETERIORATE-
                                DEHYD eligible (Step 8). [CLINICAL VALIDATION REQUIRED]

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L3-01
      Trigger: attach(SpO2)
      Condition: patient present
      Check: socket valid
      Correct: sensor on; SpO2 visible
      Incorrect: —
      Feedback: Monitor value appears
      Animation: Probe snaps to finger
      Next state: —
  * L3-02
      Trigger: measure(NIBP)
      Condition: cuff attached
      Check: —
      Correct: BP value after 20 s sim
      Incorrect: —
      Feedback: Cuff inflate sound
      Animation: Cuff inflate/deflate
      Next state: —
  * L3-03
      Trigger: compute NEWS2
      Condition: all 7 params known
      Check: band table
      Correct: score + band
      Incorrect: incomplete if missing
      Feedback: Score chip colour
      Animation: —
      Next state: —
  * L3-04
      Trigger: NEWS2 high (S2)
      Condition: score ≥ threshold [CLINICAL VALIDATION REQUIRED]
      Check: —
      Correct: goal G-ESCALATE deadline
      Incorrect: if deadline missed → critical
      Feedback: Tablet alert
      Animation: Monitor amber
      Next state: ST3/URG
  * L3-WT1
      Trigger: news2_tier
      Condition: all parameters
      Check: NEWS2 + single-3 flag
      Correct: Correct escalation
      Incorrect: Missed escalation
      Feedback: Nurse prompt
      Animation: —
      Next state: news2.tier
  * L3-WT2
      Trigger: repeat_reading
      Condition: abnormal value
      Check: technique items
      Correct: Real vs artefact
      Incorrect: Acting on one reading
      Feedback: —
      Animation: —
      Next state: obs[]

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E3-B
      What went wrong: SpO2 artefact (probe loose)
      User sees: SpO2 drops to implausible value, pleth trace flat
      Should notice: Trace quality poor
      Correct response: Check patient & probe first
      System does: If trainee gives oxygen without checking → minor
      Progression: Allowed
      Feedback: Debrief note
      State: ST3
      Resolution: Re-seat probe
  * E3-C
      What went wrong: High NEWS2 not acted on (S2)
      User sees: Amber/red chip
      Should notice: Score indicates urgent review
      Correct response: Escalate + treat
      System does: Deadline timer
      Progression: Allowed
      Feedback: Alert tone
      State: URG
      Resolution: Step 8/escalation

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV3-1 E-SPO2-ARTEFACT
      Trigger: Probe movement (random, difficulty ≥2)
      Patient: Patient moves hand
      Monitor: SpO2 value drops, pleth waveform flattens
      Visual/audio: Monitor tone
      Recognise: Mismatch between calm patient and number
      Response: Check probe/patient before treating
      If no response: Unnecessary oxygen logged (minor)

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Vital-sign monitor
      Use S1/S2: ✓/✓
      Steps: 3, 8, 12–14
      Information provided: SpO2, pulse, NIBP/MAP, ECG, alarms
      Failure scenario: Artefact if probe poor; alarm limits C
  * BP cuff (NIBP)
      Use S1/S2: ✓/✓
      Steps: 3
      Information provided: Sys/dia/MAP after ~30–40 s
      Failure scenario: Wrong size → false reading
  * Pulse oximeter probe
      Use S1/S2: ✓/✓
      Steps: 3, 13
      Information provided: SpO2, pulse, pleth quality
      Failure scenario: No waveform → recheck; Sign In requires working
  * Tympanic thermometer
      Use S1/S2: ✓/✓
      Steps: 3, 10
      Information provided: °C
      Failure scenario: <36.0 → warm
  * Glucose/ketone meter
      Use S1/S2: cond./cond.
      Steps: 3, 8
      Information provided: mmol/L
      Failure scenario: Hypo missed
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * NEWS2 chart
      Use S1/S2: ✓/✓
      Steps: 3
      Information provided: Obs trend + score
      Failure scenario: Missed trend
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — LitePhysio baseline per patient + sensor gating; NEWS2 calculator (pure function, Scale 1 +
Scale 2) with tests against WT 4.9 bands; trend store for deterioration detection (B-R18). Sannan —
Probe/cuff/thermometer interactables + sockets; monitor world-space UI incl. pleth quality and MAP in
brackets; avatar breathing at simulated RR (must be countable); NEWS2 chart UI. Moiz — Vital-sign band tables
(content/vitals_bands.csv) and NEWS2 response tiers from WT Part 17/4.9 as Type A content; per-variant
baseline obs from WT Parts 12–13/D.

Validation: Use of NEWS2 (vs local early-warning tool) and its escalation mapping. [CLINICAL VALIDATION
REQUIRED] · Examination finding codes per variant. [CLINICAL VALIDATION REQUIRED] · Vital-sign ranges for each
patient variant. [CLINICAL VALIDATION REQUIRED] · Hospital early-warning tool; anaesthetist alarm thresholds.
```


## 9. STEP 4 — EXAMINING THE PATIENT

```text
Priority P0 · walkthrough p.80 · build guides: B-S9, B-S2, B-M3
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Looking, listening and gently feeling to find physical signs.
  Why are we doing it?        : Signs confirm or challenge the diagnosis and reveal dangers (e.g. a rigid
                                tummy means possible perforation; yellow eyes mean bile blockage).
  What does the trainee actually do?: 1. Explain and ask permission; offer a chaperone (another staff member
                                present) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
                                2. General look: distress, pale, yellow (look at whites of eyes), sweaty, dry
                                mouth. 3. Listen to heart and lungs with a stethoscope. 4. Tummy: start
                                pressing away from the pain and move toward it, gently. 5. S1: press under
                                right ribs while the patient breathes in (Murphy's sign). 6. S2: lower right
                                tenderness, guarding, rebound; Rovsing's sign (pressing lower LEFT hurts lower
                                RIGHT); psoas/obturator signs (leg movements that hurt with a hidden
                                appendix). 7. S2: check hernia areas; in males check testicles (with
                                consent/chaperone). 8. Look in the mouth and at the neck for the anaesthetist
                                (Part 7.5). 9. Thank the patient, cover them, record findings.
  What does the trainee see?  : The patient on the couch; the trainee's hands/stethoscope; the patient's face
                                and voice react.
  What information do they receive?: S1-A: "Soft tummy, mildly tender under right ribs, Murphy's negative."
                                S1-E: patient catches breath and says "Ow!" when right ribs pressed = Murphy's
                                positive. S2-A: "Tender lower right, guarding, rebound positive, Rovsing's
                                positive."
  What does that information mean?: Murphy's positive = inflamed gallbladder likely. Lower right tenderness +
                                guarding + rebound = inflamed lining near the appendix (localised
                                peritonitis). Whole tummy rigid = generalised peritonitis → possible burst.
  What is the normal/reference value?: S1: soft, non-tender or mildly tender tummy; no jaundice; normal
                                heart/lung sounds; airway easy. S2: there is no 'normal' — expected localised
                                lower-right signs.
  What counts as abnormal?    : S1: Murphy's positive, tender lump, jaundice, irregular pulse, wheeze. S2:
                                signs outside the lower right, testicle/pelvic findings.
  What counts as seriously concerning?: Rigid board-like tummy; very tender everywhere; a mass; signs of
                                shock; very difficult airway.
  What could cause the abnormal result?: Inflammation spreading, abscess, perforation, alternative diagnosis.
  What should the trainee check next?: 1. S1 positive signs → bloods (WBC, CRP, liver tests), urgent
                                ultrasound, senior. 2. S2 generalised signs → senior immediately, sepsis
                                check. 3. Airway concern → anaesthetist.
  What happens if the trainee finds nothing wrong?: Go to STEP 5 (risk and anaesthetic assessment) and STEP 6
                                (tests).
  What happens if the trainee finds something abnormal?: Record and follow the branch.
  What happens if the abnormality is mild?: Mild right-upper tenderness in S1 is common with gallstones —
                                continue.
  What happens if it is severe?: S1: inflamed gallbladder → planned op cancelled, acute pathway. S2: rigid
                                tummy → emergency; senior; speed up surgery.
  When do we call the senior doctor?: Any S1 sign of acute inflammation or jaundice; S2 generalised
                                peritonitis or mass; anything not fitting the expected picture.
  When do we call the anaesthetist?: Difficult airway signs; sleep apnoea; heart murmur or irregular pulse.
  When might surgery be delayed?: S1: signs of acute cholecystitis or jaundice.
  When should emergency surgery continue despite the abnormality?: S2: peritonitis makes surgery more urgent.
  What happens if the trainee misses it?: Not checking Murphy's sign in a feverish S1 patient → inflamed
                                gallbladder discovered in theatre (debrief). Examining without permission →
                                professionalism debrief.
  What happens after the correct action?: Diagnosis becomes more or less likely; test choice is guided.
  Medical words used here     : Murphy's sign Pain stops the patient breathing in when the gallbladder area is
                                pressed. Guarding Tummy muscles tense to protect a painful area. Rebound
                                tenderness Pain when pressing hand is suddenly released. Peritonitis
                                Inflammation of the tummy lining. Chaperone A second staff member present
                                during intimate examinations.
  Where this comes from       : TG18; WSES 2020; StatPearls.
  Clinician validation        : Surgeon: examination finding sets for each variant; chaperone policy.
```


### What the player sees (WT Part 16 — Stage 4)

```text
  On screen / in front of the player: Patient on couch; trainee's hands/stethoscope.
  Patient — what they say     : Winces/catches breath when tender areas pressed (Murphy's, rebound).
  Monitor — numbers           : Unchanged.
  Interaction                 : Ask permission/offer chaperone; look at eyes; listen; press tummy regions;
                                airway look.
  Information revealed        : Findings text (e.g. 'tender RIF, guarding'); eye colour visible.
  Decision                    : Do signs fit the diagnosis?
  Correct action →            : Findings added to notes.
  Incorrect action →          : Examining without permission → patient discomfort; debrief.
  Missed abnormality →        : Missed Murphy's → acute cholecystitis found later.
  Escalation →                : Rigid tummy → senior immediately.
```


#### VR reasoning card C.6 — Fever (S1)

```text
  Why                         : Infection in planned patient
  Trainee action              : Examine (Murphy's, jaundice), bloods, US
  What the trainee sees       : Thermometer, patient
  Information                 : T, WBC, CRP, LFTs
  Interpretation              : Acute cholecystitis/cholangitis
  Decision                    : Is this still elective?
  Correct action              : Acute pathway
  Incorrect action            : Paracetamol and proceed
  Missed abnormality          : Stopped at Sign In / sepsis
  Escalation                  : Senior
  Consequence                 : Pathway change
  Next step                   : Acute branch

Scenario maps: S1 stages 3 Observations, 4 History; S2 stages 2 Initial assessment, 4 History.
```


### Implementation (FYP design)

```text
  Real workflow               : Doctor examines in detail once (look, listen, feel); airway look for the
                                anaesthetist.
  FYP simulation              : Hotspot-based examination with patient reactions; findings revealed from the
                                case card.
  FYP simplification          : Palpation = hotspot touch, not haptic skill; heart/lung sounds optional audio
                                (P1).
  AI                          : None required; optional AI phrasing of patient reactions (validated).

  Room                        : Same room; curtain closes for exam.
  Patient                     : Lying flat; gown lifted (dignity mesh)
  Staff                       : Nurse NPC (can do obs on request; chaperone).
  Equipment                   : Stethoscope, exam hotspots, curtain
  Available interactions      : Ask permission; examine regions; airway look

USER MUST:

  1. Explain and ask permission; offer a chaperone (WT Step 4; chaperone policy Type C).
  2. Draw curtain.
  3. General look: distress, pallor, jaundice (look at sclera), sweating, dry mouth.
  4. Listen to heart and lungs (stethoscope).
  5. Palpate abdomen starting AWAY from the pain (9 regions).
  6. S1: Murphy's sign (press under right ribs while patient breathes in).
  7. S2: RLQ tenderness, guarding, rebound, Rovsing's, psoas/obturator; hernia areas; males: testicles
     (consent/chaperone).
  8. Airway look for the anaesthetist: mouth opening, Mallampati, neck movement (WT 7.5).
  9. Thank, cover, record findings on tablet → 'Examination complete'.

SYSTEM THEN:

  1. Hotspot touch reveals finding codes from the case card with patient reaction (wince, catch breath).
  2. Examining without permission → professionalism evidence + patient discomfort.
  3. Painful region first → pain spike + trust −1.
  4. Airway findings stored for Step 5 and Sign In.
  5. Rigid board-like abdomen (variant) → senior-immediately goal (S2 generalised peritonitis).

  If correct →                : Findings recorded; diagnosis more/less likely; test choice guided.
  If incorrect →              : Examining without permission → professionalism debrief; missed Murphy's →
                                acute cholecystitis found later.
  If information missing →    : Missing region → finding stays hidden; debrief lists it.
  If safety condition fails → : S2 rigid abdomen not escalated → deterioration/sepsis branch.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L4-01
      Trigger: examine(region)
      Condition: permission asked (+chaperone where required)
      Check: consent flag
      Correct: Finding revealed
      Incorrect: Professionalism item
      Feedback: Patient line
      Animation: A4-01
      Next state: findings[]
  * L4-02
      Trigger: murphy_test (S1)
      Condition: S1 case
      Check: finding code
      Correct: Murphy's ± recorded
      Incorrect: Missed in febrile S1 → inflamed GB found later (debrief)
      Feedback: —
      Animation: A4-02
      Next state: findings[]
  * L4-03
      Trigger: examine(RLQ signs) (S2)
      Condition: S2 case
      Check: finding codes
      Correct: Localised vs generalised peritonism
      Incorrect: Generalised ignored → sepsis branch
      Feedback: —
      Animation: A4-03
      Next state: findings[]
  * L4-04
      Trigger: airway_check
      Condition: mouth/neck items
      Check: airway enum
      Correct: Stored for anaesthetist
      Incorrect: Difficult airway not passed on → Sign In challenge
      Feedback: —
      Animation: —
      Next state: airway{}

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E4-A
      What went wrong: Examined without asking permission
      User sees: Patient flinches, 'Oh!'
      Should notice: Consent before touch
      Correct response: Apologise, ask
      System does: Professionalism evidence
      Progression: Continue
      Feedback: Debrief
      State: trust −1
      Resolution: Ask first next time
  * E4-B
      What went wrong: Pressed painful area first
      User sees: Pain spike, guarding
      Should notice: Start away from pain
      Correct response: Reassure; continue gently
      System does: pain +1
      Progression: Continue
      Feedback: Debrief
      State: pain
      Resolution: —
  * E4-C
      What went wrong: Murphy's not checked in febrile S1 patient
      User sees: —
      Should notice: Acute cholecystitis possible
      Correct response: Murphy's + bloods + US + senior
      System does: Finding stays hidden
      Progression: Later stop
      Feedback: Debrief
      State: —
      Resolution: WT Step 4 'misses'

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Stethoscope
      Use S1/S2: ✓/✓
      Steps: 4
      Information provided: Cue
      Failure scenario: —
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Finding-reveal and behaviour hooks (trust/pain deltas) in Core; airway data to anaesthetic
plan. Sannan — Abdominal hotspots (9 regions) + reaction clips; Murphy's breath-catch blend; curtain;
stethoscope interaction; airway look UI. Moiz — Finding codes per WT variant (Parts 12–13, D); professionalism
rubric items; airway enum content (WT 7.5).

Validation: Examination finding sets per variant [CLINICAL VALIDATION REQUIRED] · Chaperone policy (Type C)
[CLINICAL VALIDATION REQUIRED] · Surgeon: examination finding sets for each variant; chaperone policy.
```


## 10. STEP 5 — RISK AND ANAESTHETIC ASSESSMENT

```text
Priority P0 · walkthrough p.82 · build guides: B-R12, B-M20, B-M12
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Putting together everything so far to judge how risky anaesthesia and surgery
                                are, and whether the patient is fit now.
  Why are we doing it?        : Some risks can be reduced before surgery (optimisation) or planned for
                                (special equipment, senior anaesthetist).
  What does the trainee actually do?: 1. Propose an ASA grade (Part 7.4); the anaesthetist confirms. 2.
                                Complete a clot (VTE) and bleeding risk assessment (Part 7.6). 3. Do the
                                airway and sleep-apnoea checks (Part 7.5). 4. S2: calculate the appendicitis
                                score once bloods return (Part 7.1). 5. S1 if inflamed: apply Tokyo Guidelines
                                grading (Part 7.2). 6. Note risks for consent and handover. 7. Decide: proceed
                                / optimise / more tests / senior or anaesthetic review / postpone.
  What does the trainee see?  : Anaesthetist character arrives (or phone); risk form; ASA selector.
  What information do they receive?: Anaesthetist: "What ASA grade would you give him and why?" "When did he
                                last eat?" "Any problems with previous anaesthetics?"
  What does that information mean?: ASA 1–2 = generally low anaesthetic risk; 3+ = needs planning; 'E' =
                                emergency adds risk.
  What is the normal/reference value?: S1 typical ASA 1–2, low VTE risk, normal airway, fasted. S2 typical ASA
                                1E.
  What counts as abnormal?    : ASA 3; STOP-BANG 3–4; moderate VTE risk; fasting breach.
  What counts as seriously concerning?: ASA 4; STOP-BANG 5+; very difficult airway; uncontrolled illness.
  What could cause the abnormal result?: Comorbidities, emergency situation, obesity, smoking.
  What should the trainee check next?: 1. Optimisable problem (S1) → postpone and refer. 2. Planning problem →
                                tell anaesthetist.
  What happens if the trainee finds nothing wrong?: Go to STEP 6 (choosing tests).
  What happens if the trainee finds something abnormal?: Discuss with anaesthetist.
  What happens if the abnormality is mild?: Note and plan.
  What happens if it is severe?: Anaesthetist leads decision; S1 often postponed; S2 senior anaesthetist
                                involved.
  When do we call the senior doctor?: Any uncertainty about proceeding.
  When do we call the anaesthetist?: Always at this step.
  When might surgery be delayed?: S1: uncontrolled comorbidity, fasting breach (re-timed), recent heart event.
  When should emergency surgery continue despite the abnormality?: S2: high-risk patients still need source
                                control; risk is managed rather than avoided.
  What happens if the trainee misses it?: Wrong ASA → anaesthetist corrects with explanation (teaching moment,
                                not failure). Hiding a fasting breach → aspiration event or cancellation.
  What happens after the correct action?: Anaesthetic plan agreed (e.g. 'standard GA' or 'RSI').
  Medical words used here     : ASA grade Fitness-for-anaesthesia scale (Part 7.4). VTE Blood clots in veins.
                                GA General anaesthesia.
  Where this comes from       : ASA PS 2020; NICE NG89; NICE QS216; STOP-BANG (Chung).
  Clinician validation        : Anaesthetist: expected ASA per variant; risk tool.
```


### What the player sees (WT Part 16 — Stage 5)

```text
  On screen / in front of the player: Anaesthetist arrives / phone; ASA selector.
  Patient — what they say     : Anaesthetist asks: "When did he last eat?"
  Monitor — numbers           : —
  Interaction                 : Select ASA, VTE form, airway findings.
  Information revealed        : Anaesthetist's response.
  Decision                    : Fit now? Plan?
  Correct action →            : Plan agreed.
  Incorrect action →          : Wrong ASA corrected with explanation.
  Missed abnormality →        : Hidden fasting breach → aspiration event/cancellation.
  Escalation →                : Anaesthetist can postpone.
```


#### VR reasoning card C.12 — Appendicitis score

```text
  Why                         : Probability guides imaging
  Trainee action              : Calculate AIR/AAS
  What the trainee sees       : Score UI
  Information                 : Band
  Interpretation              : Low/intermediate/high
  Decision                    : Imaging?
  Correct action              : Right scan
  Incorrect action            : Skipping score
  Missed abnormality          : Over/under-imaging
  Escalation                  : Senior
  Consequence                 : Imaging choice
  Next step                   : Imaging
```


#### VR reasoning card C.16 — Fasting

```text
  Why                         : Aspiration risk
  Trainee action              : Exact times/type; GLP-1
  What the trainee sees       : Patient
  Information                 : Times
  Interpretation              : Breach? Full stomach?
  Decision                    : Inform anaesthetist?
  Correct action              : Re-time / RSI
  Incorrect action            : Rounding times
  Missed abnormality          : Aspiration
  Escalation                  : Anaesthetist
  Consequence                 : Timing change
  Next step                   : Prep

Scenario maps: S1 stages 10 Anaesthetic assessment; S2 stages 10 Anaesthetic assessment.

  [TABLE] What-if | Branch | Correct action
  * WI-08
      Branch: What if breathing is very fast? (25 or more)
      Correct action: Treat cause; sepsis bundle if infection.
  * WI-33
      Branch: What if the patient hasn't fasted?
      Correct action: Tell anaesthetist honestly.
  * WI-34
      Branch: What if fasting status is unknown?
      Correct action: Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In.
```


### Implementation (FYP design)

```text
  Real workflow               : Anaesthetist sees the patient before theatre and decides the anaesthetic plan.
  FYP simulation              : Trainee proposes ASA/scores; anaesthetist NPC gives approved response.
  FYP simplification          : Risk tool = one reviewer-chosen tool; anaesthetic plan itself is out of scope.
  AI                          : None for decisions. P1: LLM phrases anaesthetist NPC lines from approved
                                intents.

  Room                        : Same bedside.
  Patient                     : Resting.
  Staff                       : Anaesthetist NPC arrives when trainee calls or when case timer reaches point.
  Equipment                   : Tablet calculators (ASA, VTE, score).
  Available interactions      : Assess risk forms; call anaesthetist.

USER MUST:

  1. Open 'Assess' tab.
  2. Select ASA class with justification tags.
  3. Complete VTE risk items.
  4. (S2) enter score components → compute; (S1-V4) TG18 criteria.
  5. Call anaesthetist NPC and present summary (SBAR-lite).
  6. Choose readiness decision.
  7. Propose an ASA grade (anaesthetist confirms); VTE + bleeding risk; airway + STOP-BANG; S2 appendicitis
     score after bloods; S1 TG18 grade if inflamed; decide proceed/optimise/more tests/review/postpone.

SYSTEM THEN:

  1. Compares trainee values with expected values/ranges set by reviewer.
  2. Records decision; opens branches (delay, optimise, test).
  3. Anaesthetist NPC response from approved intent table (e.g. 'Delay until fasting criteria met').
  4. Behaviour: patient told about delay → anxiety +1 unless explained.
  5. Anaesthetist ALWAYS involved at this step.
  6. Wrong ASA → anaesthetist corrects with explanation (teaching moment, NOT failure) (WT Step 5).
  7. Hidden fasting breach → aspiration event or cancellation.
  8. Anaesthetic plan agreed ('standard GA' / 'RSI').

  If correct →                : Risk goals complete; correct branch taken; score items credited.
  If incorrect →              : Wrong ASA (±1) → minor; wrong/unsafe decision (proceed despite fasting breach)
                                → CRITICAL.
  If information missing →    : Score uncomputable without bloods → prompt to order tests.
  If safety condition fails → : Proceeding with a known fasting breach without anaesthetist agreement blocks
                                'ready' and is CRITICAL.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L5-01
      Trigger: assess_risk(ASA,v)
      Condition: history+exam done
      Check: |v − expected| ≤ tolerance
      Correct: credit
      Incorrect: minor item
      Feedback: Chip
      Animation: —
      Next state: —
  * L5-02
      Trigger: assess_vte()
      Condition: —
      Check: all items answered
      Correct: plan required flag
      Incorrect: not done → major at readiness
      Feedback: —
      Animation: —
      Next state: —
  * L5-03
      Trigger: compute_score(S2)
      Condition: components known
      Check: formula
      Correct: score + band
      Incorrect: missing comp → prompt
      Feedback: —
      Animation: —
      Next state: DP-S2-IMG
  * L5-04
      Trigger: decide_ready(proceed) with breach
      Condition: npo breach
      Check: anaesthetist agreed?
      Correct: —
      Incorrect: CRITICAL
      Feedback: Red banner
      Animation: Anaesthetist shakes head
      Next state: blocked

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E5-A
      What went wrong: Proceed despite fasting breach
      User sees: Decision allowed on tablet
      Should notice: Breach chip red
      Correct response: Inform anaesthetist; delay
      System does: Critical item; readiness blocked
      Progression: Blocked
      Feedback: 'Fasting breach unresolved'
      State: BLOCK
      Resolution: Re-decide
  * E5-B
      What went wrong: VTE assessment skipped
      User sees: —
      Should notice: VTE form empty
      Correct response: Complete
      System does: Major at readiness
      Progression: Allowed until readiness
      Feedback: Reminder
      State: —
      Resolution: Complete form

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Decision-point engine (DP nodes, options, consequences) + anaesthetist intents. Sannan —
Assess tab UI; anaesthetist NPC avatar + walk/talk. Moiz — Content: expected ASA per variant, VTE form items
(paraphrased NG89), score definitions (after reviewer choice), TG18 criteria; mark [CLINICAL VALIDATION
REQUIRED]

Validation: Expected ASA grade per patient variant. [CLINICAL VALIDATION REQUIRED] · Choice of risk tool and
appendicitis score. [CLINICAL VALIDATION REQUIRED] · Delay/proceed decision for fasting breaches. [CLINICAL
VALIDATION REQUIRED] · Anaesthetist: expected ASA per variant; risk tool.
```


## 11. STEP 6 — CHOOSING THE TESTS

```text
Priority P0 · walkthrough p.84 · build guides: B-R16, B-M13, B-S7
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Deciding which tests are needed for THIS patient — and deliberately NOT
                                ordering tests that won't change anything.
  Why are we doing it?        : Useful tests guide decisions. Useless tests waste time, money, can delay
                                surgery and can produce misleading 'abnormal' results (remember: 1 in 20
                                healthy people is outside the range) CLINICAL REFERENCE — SOURCE BACKED (NICE
                                NG45).
  What does the trainee actually do?: 1. Choose tests from the order panel. 2. Write the reason and urgency on
                                the request. 3. Explain tests to the patient (sensitively for pregnancy test).
                                4. Take blood after identity check; label at the bedside. 5. Do bedside tests
                                (pregnancy test, urine dipstick, finger-prick sugar, ECG). 6. Request scans;
                                arrange safe transfer (porter; nurse or doctor escort if unstable). 7. Track
                                pending results.
  What does the trainee see?  : Order panel with tests; coloured tubes; labels; urine pot; pregnancy test kit;
                                ECG machine; scan request form; porter; wheelchair/trolley; portable monitor.
  What information do they receive?: Confirmation of what's ordered; expected times.
  What does that information mean?: Each test answers a specific question (see table below).
  What is the normal/reference value?: The 'right set' for each patient (see below).
  What counts as abnormal?    : Missing an indicated test or ordering unhelpful ones.
  What counts as seriously concerning?: Missing a pregnancy test in someone who could be pregnant; sending an
                                unstable patient to a scan without monitoring.
  What should the trainee check next?: 1. While waiting: treat pain, fluids, continue history/consent
                                preparation.
  What happens if the trainee finds nothing wrong?: Go to STEP 7 (results).
  When do we call the senior doctor?: Unsure which scan; patient too unstable for transfer.
  When do we call the anaesthetist?: Patient on blood thinners (clotting tests), heart disease (ECG).
  What happens if the trainee misses it?: Unnecessary tests are listed in the debrief with their 'cost'. Pre-
                                labelled tubes → wrong-blood event. Unmonitored unstable patient → drop in
                                oxygen in the corridor event.
  What happens after the correct action?: Results start to arrive after their simulated times.
  Medical words used here     : Indicated There is a good medical reason for it. Bedside test Done at the
                                bedside with a quick result.
  Where this comes from       : NICE NG45; NICE CG188; WSES 2020.
  Clinician validation        : Surgeon/anaesthetist: test-indication list; transfer rule; simulated
                                turnaround times. Which tests for which patient? CLINICAL REFERENCE — SOURCE
                                BACKED (NICE NG45, CG188, WSES) — local blood-ordering rules CONTEXT-DEPENDENT
                                — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION Test S1 planned gallbladder
                                S2 emergency appendix Why Liver tests (LFTs) YES (all) If upper pain or
                                unclear diagnosis Look for bile blockage Pregnancy test If pregnancy is
                                possible or uncertain — offered with consent If pregnancy is possible —
                                offered with consent (urgent in abdominal pain) Changes diagnosis, imaging,
                                anaesthesia, drugs and timing FBC (blood count) If sicker (ASA 3+), anaemia
                                symptoms, heart/kidney disease, or local rule YES Infection, bleeding, anaemia
                                CRP Only if inflammation suspected YES Inflammation; score U&E (kidneys,
                                salts) If 65+, diabetes, kidney disease, water tablets/ACE-i, ASA 3+ YES
                                Kidneys, potassium, dehydration Blood sugar (finger-prick) If diabetic If
                                diabetic or vomiting Low/high sugar HbA1c If diabetic and none in 3 months No
                                Diabetes control Clotting (INR/APTT) If on blood thinners or liver disease If
                                on blood thinners, septic, liver disease Bleeding risk Group & save Per
                                hospital rule Per rule; always if bleeding suspected Blood ready if needed
                                Urine dipstick No (not routine) YES Urine infection/stone look-alikes ECG If
                                heart/kidney/diabetes disease, irregular pulse, 65+ (some) If 65+, heart
                                disease, HR above 130, irregular, salt problems Heart rhythm Lactate + blood
                                cultures If septic If sepsis suspected Sepsis Lipase/amylase If severe upper
                                pain / back pain If diagnosis unclear Pancreatitis Ultrasound Already done
                                before booking; repeat if acute Young/female/pregnant first See inside CT Only
                                for complications Adults when unclear; older patients Detailed scan MRCP If
                                liver tests abnormal / duct wide No Look for duct stones Chest X-ray No (not
                                routine) Only if breathing problems / suspected burst gut —
```


### What the player sees (WT Part 16 — Stage 6)

```text
  On screen / in front of the player: Order panel; tubes; labels; kits; ECG machine; scan request; porter;
                                portable monitor.
  Patient — what they say     : "What are these tests for?"
  Monitor — numbers           : —
  Interaction                 : Select tests; take blood; label at bedside; dipstick; pregnancy test; ECG;
                                request scan; choose escort.
  Information revealed        : Pending list with expected times.
  Decision                    : Which tests? Who escorts?
  Correct action →            : Tests queued; patient moves safely.
  Incorrect action →          : Unneeded tests listed in debrief; pre-labelled tubes → wrong-blood event.
  Missed abnormality →        : Unstable patient sent with porter only → oxygen drop in corridor.
  Escalation →                : Senior for imaging choice.
```


#### VR reasoning card C.11 — Pregnancy possible

```text
  Why                         : Changes diagnosis and safety
  Trainee action              : Ask privately; consent; test
  What the trainee sees       : Test kit
  Information                 : Result
  Interpretation              : Positive + pain: ectopic pregnancy is a differential diagnosis that can
                                redirect the pathway
  Decision                    : Pathway change?
  Correct action              : Correct team
  Incorrect action            : Not asking
  Missed abnormality          : Safety failure
  Escalation                  : Senior; gynaecology/obstetrics
  Consequence                 : Pathway switch
  Next step                   : Continue/redirect

Scenario maps: S1 stages 5 Investigations; S2 stages 5 Investigations.

  [TABLE] What-if | Branch | Correct action
  * WI-25
      Branch: What if the ECG is abnormal?
      Correct action: AF: treat triggers, rate control per senior/medical; heart attack: emergency heart
          pathway.
  * WI-26
      Branch: What if the patient is pregnant?
      Correct action: S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise →
          gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok.
  * WI-27
      Branch: What if pregnancy status is unknown or the patient declines a test?
      Correct action: Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior +
          anaesthetist decide whether to proceed.
```


### Implementation (FYP design)

```text
  Real workflow               : Tests take minutes–hours; patient waits.
  FYP simulation              : Timed states with sim-time skip; transport shown as bed movement or fade.
  FYP simplification          : Lab is abstract (no lab room); CT/MRCP abstract rooms; phlebotomy is an intent
                                action (no needle skill).
  AI                          : None for selection/scoring. P1: voice orders via ASR with nurse read-back
                                (closed loop); any misheard order must be confirmed on tablet.

  Room                        : Bedside + corridor + US room (or fade) + X-ray/CT (abstract).
  Patient                     : On bed/trolley during transfer.
  Staff                       : Porter NPC, sonographer NPC (abstract/voice), nurse.
  Equipment                   : Sample tubes & labels, request form (tablet), urine test kit, ECG machine,
                                portable monitor for transfer.
  Available interactions      : Order, label, send; request transport; accompany or stay.

USER MUST:

  1. Open 'Orders'; tick indicated tests; set priority.
  2. Explain the tests (menu act).
  3. Take/label blood tubes: grab tube → touch wristband (ID) → label → place in sample bag.
  4. Do bedside tests (pregnancy test/urinalysis/glucose/ECG) where indicated.
  5. Request transport for imaging; choose escort/monitoring if patient unstable.
  6. Accompany patient or continue other tasks.
  7. Choose tests from the order panel using the WT indication table; write reason + urgency.
  8. Take blood after identity check and LABEL AT THE BEDSIDE.
  9. Bedside tests: pregnancy (with consent), dipstick, CBG, ECG.
  10. Request scans; choose escort by stability (NEWS2 ≤4 porter+nurse; 5–6 nurse+monitor; ≥7
      doctor+nurse+full monitoring — Type D/C).

SYSTEM THEN:

  1. Evaluates indication rules → APPROPRIATE / NOT INDICATED / MISSED flags.
  2. Creates InvestigationState per test (REQUESTED→PROCESSING).
  3. Labelling check (identity touched before label?) else E-SAMPLE-REJECT eligible.
  4. Transport state machine: REQUESTED→IN_TRANSIT→ARRIVED→DONE→BACK with delays.
  5. Blocks patient-requiring actions while patient is away.
  6. Schedules results by turnaround.
  7. Unnecessary tests listed in debrief with their 'cost'.
  8. Pre-labelled tubes → wrong-blood event.
  9. Unstable patient with porter only → SpO2 drop in corridor event.
  10. Pending list shows expected times (turnaround Type D: FBC 45 min, imaging 60–120 min; others
      unresolved).

  If correct →                : Indicated tests ordered, samples accepted, patient moved safely, results
                                pending.
  If incorrect →              : Unnecessary test → minor; missed indicated test → major/critical (e.g.
                                pregnancy); unmonitored transfer of unstable patient → major [CLINICAL
                                VALIDATION REQUIRED].
  If information missing →    : Clinical details missing on request → lab/radiology NPC asks for them (delay).
  If safety condition fails → : Pregnancy status unknown in eligible patient → imaging with radiation and
                                theatre blocked.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L6-01
      Trigger: order(test)
      Condition: —
      Check: indication rule
      Correct: APPROPRIATE credit
      Incorrect: NOT INDICATED minor
      Feedback: Card colour (debrief only in assessment)
      Animation: —
      Next state: —
  * L6-02
      Trigger: label(tube)
      Condition: identity touched
      Check: id match
      Correct: sample valid
      Incorrect: reject event
      Feedback: Lab message
      Animation: Tube label animation
      Next state: —
  * L6-03
      Trigger: request_transport(dest)
      Condition: test needs location
      Check: patient stable?
      Correct: standard transfer
      Incorrect: unstable & no monitoring → major
      Feedback: —
      Animation: Porter arrives, bed moves
      Next state: IN_TRANSIT
  * L6-04
      Trigger: action on absent patient
      Condition: patient elsewhere
      Check: location mismatch
      Correct: —
      Incorrect: rejected
      Feedback: 'Patient is in Ultrasound'
      Animation: —
      Next state: —
  * L6-05
      Trigger: pregnancy test
      Condition: eligible
      Check: consent asked
      Correct: result by profile
      Incorrect: not done → block
      Feedback: —
      Animation: Dipstick colour change
      Next state: —

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E6-A
      What went wrong: Order every test ('shotgun')
      User sees: All cards selected
      Should notice: Tests not indicated
      Correct response: Order selectively
      System does: Minor items per unnecessary test
      Progression: Allowed
      Feedback: Debrief lists unnecessary tests
      State: —
      Resolution: —
  * E6-B
      What went wrong: Tube not labelled at bedside
      User sees: Lab rejects after delay
      Should notice: Label mandatory
      Correct response: Re-sample
      System does: E-SAMPLE-REJECT; delay
      Progression: Allowed
      Feedback: 'Sample rejected: unlabelled'
      State: —
      Resolution: Re-take
  * E6-C
      What went wrong: Unstable patient sent alone
      User sees: Patient tachycardic
      Should notice: Monitoring needed
      Correct response: Escort + portable monitor
      System does: Major item; deterioration risk
      Progression: Allowed
      Feedback: [CLINICAL VALIDATION REQUIRED]
      State: —
      Resolution: —
  * E6-D
      What went wrong: Pregnancy test skipped (eligible)
      User sees: Imaging/theatre blocked
      Should notice: Pregnancy status unknown
      Correct response: Test with consent
      System does: Block
      Progression: Blocked
      Feedback: 'Pregnancy status required'
      State: BLOCK
      Resolution: Do test

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV6-1 E-PORTER-DELAY
      Trigger: Transport request (probabilistic)
      Patient: —
      Monitor: —
      Visual/audio: Phone call 'porter delayed 20 min'
      Recognise: Time pressure
      Response: Reprioritise tasks / escalate if urgent
      If no response: Idle waiting logged (efficiency)
  * EV6-2 E-SAMPLE-REJECT
      Trigger: Unlabelled tube
      Patient: —
      Monitor: —
      Visual/audio: Lab phone call
      Recognise: Sample invalid
      Response: Re-sample
      If no response: Delay → later results

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Wheelchair
      Use S1/S2: ✓/—
      Steps: 1, 6
      Information provided: Transport cue
      Failure scenario: Unstable patient must not go by wheelchair
  * 12-lead ECG + electrodes
      Use S1/S2: cond./cond.
      Steps: 6–7
      Information provided: Rhythm, intervals, ST/T; machine read-out may be wrong
      Failure scenario: Wrong lead placement
  * Blood collection set + tourniquet
      Use S1/S2: ✓/✓
      Steps: 6
      Information provided: Cue
      Failure scenario: Drip-arm artefact
  * Specimen tubes (colour-coded) + labels
      Use S1/S2: ✓/✓
      Steps: 6
      Information provided: Tube purpose; identity label
      Failure scenario: Pre-labelled → wrong-blood; EDTA → false high K
  * Blood culture bottles
      Use S1/S2: —/cond.
      Steps: 6, 8
      Information provided: Cue
      Failure scenario: Delaying antibiotics for cultures → harm
  * Urine pot + dipstick + pregnancy kit
      Use S1/S2: cond./✓
      Steps: 6
      Information provided: Dipstick colours; hCG ±
      Failure scenario: Missed positive hCG → safety failure
  * Sharps bin
      Use S1/S2: ✓/✓
      Steps: 6, 8
      Information provided: Cue
      Failure scenario: Needlestick
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Ultrasound machine / report
      Use S1/S2: cond./✓
      Steps: 6–7
      Information provided: Images + category
      Failure scenario: 'Not visualised' ≠ negative
  * CT (room or report only)
      Use S1/S2: cond./cond.
      Steps: 6–7
      Information provided: Report + key images
      Failure scenario: Contrast with AKI/allergy
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — TransportSystem + LocationSystem in Core (state machine, delays, gating) with tests T06-3/4.
Sannan — Corridor spline + porter/bed animation (or fade if room missing); tubes/dipsticks/ECG props; orders
UI. Moiz — InvestigationSystem + indication rules for S1/S2 (NG45/CG188/WSES own words); request/turnaround
content; mark [CLINICAL VALIDATION REQUIRED]

Validation: Test indication mapping per patient; NG45 surgery grade for lap chole (not listed as an example).
[CLINICAL VALIDATION REQUIRED] · Transfer escort/monitoring rule. [CLINICAL VALIDATION REQUIRED] · Turnaround
times (simulation values). [CLINICAL VALIDATION REQUIRED] · Surgeon/anaesthetist: test-indication list;
transfer rule; simulated turnaround times. Which tests for which patient? CLINICAL REFERENCE — SOURCE BACKED
(NICE NG45, CG188, WSES) — local blood-ordering rules CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
CONFIRMATION Test S1 planned gallbladder S2 emergency appendix Why Liver tests (LFTs) YES (all) If upper pain
or unclear diagnosis Look for bile blockage Pregnancy test If pregnancy is possible or uncertain — offered
with consent If pregnancy is possible — offered with consent (urgent in abdominal pain) Changes diagnosis,
imaging, anaesthesia, drugs and timing FBC (blood count) If sicker (ASA 3+), anaemia symptoms, heart/kidney
disease, or local rule YES Infection, bleeding, anaemia CRP Only if inflammation suspected YES Inflammation;
score U&E (kidneys, salts) If 65+, diabetes, kidney disease, water tablets/ACE-i, ASA 3+ YES Kidneys,
potassium, dehydration Blood sugar (finger-prick) If diabetic If diabetic or vomiting Low/high sugar HbA1c If
diabetic and none in 3 months No Diabetes control Clotting (INR/APTT) If on blood thinners or liver disease If
on blood thinners, septic, liver disease Bleeding risk Group & save Per hospital rule Per rule; always if
bleeding suspected Blood ready if needed Urine dipstick No (not routine) YES Urine infection/stone look-alikes
ECG If heart/kidney/diabetes disease, irregular pulse, 65+ (some) If 65+, heart disease, HR above 130,
irregular, salt problems Heart rhythm Lactate + blood cultures If septic If sepsis suspected Sepsis
Lipase/amylase If severe upper pain / back pain If diagnosis unclear Pancreatitis Ultrasound Already done
before booking; repeat if acute Young/female/pregnant first See inside CT Only for complications Adults when
unclear; older patients Detailed scan MRCP If liver tests abnormal / duct wide No Look for duct stones Chest
X-ray No (not routine) Only if breathing problems / suspected burst gut —
```


## 12. STEP 7 — GETTING AND UNDERSTANDING THE RESULTS

```text
Priority P0 · walkthrough p.86 · build guides: B-M13, B-S10, B-R17, B-M20
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Opening every result, comparing to normal and previous values, working out
                                what it means for this patient, acting, and signing it off.
  Why are we doing it?        : A result nobody looks at is useless; an ignored dangerous result can kill.
  What does the trainee actually do?: 1. Open each result as it arrives. 2. Compare with reference range and
                                previous results. 3. Classify: normal / abnormal but not important / abnormal
                                and important. 4. Check it fits the patient; repeat if suspicious. 5. Decide
                                the action (Part 5 for each test). 6. Acknowledge (sign) the result and
                                document the action. 7. Answer lab phone calls about critical results: repeat
                                the value back, act, document.
  What does the trainee see?  : Results panel with H (high) / L (low) flags and comments; scan viewer and
                                report; ringing phone.
  What information do they receive?: e.g. 'Potassium 6.4 H — sample haemolysed'; 'Bilirubin 72 H';
                                'Ultrasound: appendix not visualised'.
  What does that information mean?: See Part 5 and Part 6 for each test's meaning.
  What is the normal/reference value?: All results within reference ranges, or abnormal results that are
                                known, stable and irrelevant.
  What counts as abnormal?    : Any result in amber/red zones of Part 5.
  What counts as seriously concerning?: Any critical value (Part 5.27) or a result showing the diagnosis is
                                different/complicated.
  What could cause the abnormal result?: See each test in Part 5.
  What should the trainee check next?: 1. Look for linked tests (e.g. high potassium → ECG; high bilirubin →
                                duct size). 2. Look at the patient again. 3. Decide whether to repeat.
  What happens if the trainee finds nothing wrong?: Go to STEP 8 (decide and treat/optimise).
  What happens if the trainee finds something abnormal?: Use the decision diagrams in Parts 5–7.
  What happens if the abnormality is mild?: Note, maybe repeat, usually continue.
  What happens if it is severe?: Act immediately; call help; S1 usually deferred after review; S2 treat in
                                parallel.
  When do we call the senior doctor?: Any critical or pathway-changing result (abnormal liver pattern,
                                positive pregnancy test, positive scan, sepsis markers).
  When do we call the anaesthetist?: Potassium/sodium abnormal, anaemia, clotting abnormal, ECG abnormal,
                                kidney injury, HbA1c above 69.
  When might surgery be delayed?: S1: any significant abnormality (see Part 5 for thresholds).
  When should emergency surgery continue despite the abnormality?: S2: kidney injury, salt problems, raised
                                infection markers usually get corrected alongside — surgery continues as soon
                                as reasonably safe.
  What happens if the trainee misses it?: Unacknowledged critical result → blocks progress; if it was real,
                                the patient deteriorates. Treating a faulty sample without repeating → 'over-
                                treatment' debrief.
  What happens after the correct action?: Results marked reviewed; plan updated.
  Where this comes from       : Royal College of Pathologists 2017 and test-specific sources in Part 5.
  Clinician validation        : Hospital lab ranges and critical list.
```


### What the player sees (WT Part 16 — Stage 7)

```text
  On screen / in front of the player: Results panel with H/L flags; scan viewer; phone.
  Patient — what they say     : —
  Monitor — numbers           : Can update after treatment.
  Interaction                 : Open, compare, acknowledge; answer phone.
  Information revealed        : Lab comments, previous values, radiology report.
  Decision                    : What does it mean; act?
  Correct action →            : Plan updates.
  Incorrect action →          : Over-treating a faulty sample → debrief.
  Missed abnormality →        : Unacknowledged critical result → blocker; real deterioration if true.
  Escalation →                : Critical results → senior/anaesthetist.
```


#### VR reasoning card C.8 — Potassium abnormal

```text
  Why                         : Arrhythmia risk
  Trainee action              : Lab comment; ECG; kidneys; drugs; repeat
  What the trainee sees       : Results, ECG
  Information                 : K value, ECG
  Interpretation              : Artefact vs real; severity
  Decision                    : Emergency or repeat?
  Correct action              : Correct response
  Incorrect action            : Treat artefact / ignore real
  Missed abnormality          : Arrhythmia
  Escalation                  : Anaesthetist; emergency team
  Consequence                 : Treatment or proceed
  Next step                   : Continue
```


#### VR reasoning card C.10 — Liver tests abnormal (S1)

```text
  Why                         : Possible duct stone
  Trainee action              : Pattern + duct width + ASGE risk
  What the trainee sees       : Results, US report
  Information                 : LFTs, CBD
  Interpretation              : Risk tier
  Decision                    : MRCP/ERCP?
  Correct action              : Planned op deferred appropriately
  Incorrect action            : Proceed anyway
  Missed abnormality          : Retained stone → cholangitis
  Escalation                  : Senior
  Consequence                 : Biliary pathway
  Next step                   : Rebook
```


#### VR reasoning card C.12 — Appendicitis score

```text
  Why                         : Probability guides imaging
  Trainee action              : Calculate AIR/AAS
  What the trainee sees       : Score UI
  Information                 : Band
  Interpretation              : Low/intermediate/high
  Decision                    : Imaging?
  Correct action              : Right scan
  Incorrect action            : Skipping score
  Missed abnormality          : Over/under-imaging
  Escalation                  : Senior
  Consequence                 : Imaging choice
  Next step                   : Imaging
```


#### VR reasoning card C.13 — Imaging result

```text
  Why                         : Confirms/redirects
  Trainee action              : Read report; act
  What the trainee sees       : Viewer, report
  Information                 : Category
  Interpretation              : Positive/complicated/equivocal/negative
  Decision                    : Next step?
  Correct action              : Correct pathway
  Incorrect action            : 'Not seen' = negative
  Missed abnormality          : Perforation
  Escalation                  : Senior/radiology
  Consequence                 : Decision
  Next step                   : Decision

Scenario maps: S1 stages 6 Results, 7 Abnormal branches; S2 stages 6 Results, 7 Abnormal branches.

  [TABLE] What-if | Branch | Correct action
  * WI-12
      Branch: What if potassium is high? (above 5.3)
      Correct action: 5.5–5.9 (mild, UKKA): ECG, repeat, review drugs. 6.0–6.4: urgent repeat + ECG, treat if
          real. 6.5+ or ECG changes: emergency treatment (calcium to protect the heart; insulin-glucose)
          PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; heart monitor.
  * WI-13
      Branch: What if potassium is low? (below 3.5)
      Correct action: Replace (by mouth if mild; through drip if 2.5–2.9 or vomiting, rate PATIENT-SPECIFIC
          DOSE — CLINICIAN CONTROLLED ); replace magnesium too.
  * WI-14
      Branch: What if sodium is abnormal?
      Correct action: Correct the cause slowly (no faster than ~8–10 mmol/L per day).
  * WI-15
      Branch: What if haemoglobin is low?
      Correct action: S1: investigate before planned surgery; S2: group & save/crossmatch, look for bleeding,
          transfusion decision if below 70 (80 heart disease).
  * WI-16
      Branch: What if the white cell count is high?
      Correct action: Interpret in context; S1 high WBC = not routine → acute pathway; S2 supports diagnosis;
          above 18 = moderate gallbladder grade.
  * WI-17
      Branch: What if the white cell count is low?
      Correct action: Treat as serious if infection present; urgent antibiotics if septic.
  * WI-18
      Branch: What if creatinine is high?
      Correct action: Fluids if dry; stop kidney-harming drugs; catheter/urine measurement; repeat in 6–24 h;
          adjust medicine doses; avoid CT dye unless essential (senior decides).
  * WI-19
      Branch: What if liver tests are abnormal?
      Correct action: Use the bile-duct-stone risk tool (Part 5.16): high → ERCP pathway; intermediate → MRCP;
          low → proceed.
  * WI-20
      Branch: What if bilirubin is high?
      Correct action: Cholangitis signs → emergency; otherwise high-risk pathway (ERCP).
  * WI-21
      Branch: What if blood sugar is low? (below 4.0)
      Correct action: Treat immediately per hypoglycaemia protocol (fast sugar if can swallow AND not about to
          have anaesthetic per anaesthetist; else IV glucose) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ;
          recheck every 10–15 min.
  * WI-22
      Branch: What if blood sugar is high? (above 12)
      Correct action: Insulin per protocol (often variable-rate drip) PATIENT-SPECIFIC DOSE — CLINICIAN
          CONTROLLED ; fluids if dry.
  * WI-23
      Branch: What if the clotting test (INR) is high?
      Correct action: S1 usually deferred after review and correct (vitamin K per senior); S2 correct before
          surgery PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED .
  * WI-24
      Branch: What if lactate is raised?
      Correct action: Sepsis bundle/fluids; repeat within 2–4 h.
  * WI-25
      Branch: What if the ECG is abnormal?
      Correct action: AF: treat triggers, rate control per senior/medical; heart attack: emergency heart
          pathway.
  * WI-35
      Branch: What if imaging is inconclusive?
      Correct action: Next imaging (CT/MRI/MRCP) or observe and repeat assessment in 6–12 h.
  * WI-36
      Branch: What if a test result doesn't make sense?
      Correct action: Repeat urgently (different tube/arm); treat meanwhile only if dangerous signs.
  * WI-37
      Branch: What if a laboratory result is delayed?
      Correct action: Continue other tasks; repeat sample if lost; S2: don't delay antibiotics/fluids/surgery
          decision for a non-critical result.
  * WI-38
      Branch: What if a critical result is phoned through?
      Correct action: Act immediately (ECG, emergency treatment if real); document; acknowledge.
```


### Implementation (FYP design)

```text
  Real workflow               : Results arrive over hours.
  FYP simulation              : Results arrive by timer; lab phones critical values.
  FYP simplification          : Imaging shown as report text (+ image only where licensed dataset allows).
  AI                          : PROPOSED FYP AI (offline only): LLM drafts imaging report text from finding
                                codes; radiology/surgeon edits before approval. No runtime AI interpretation.

  Room                        : Bedside / nurses' station (optional).
  Patient                     : Waiting (behaviour: anxiety rises with unexplained waits).
  Staff                       : Lab (phone voice), senior surgeon NPC for escalation.
  Equipment                   : Tablet results viewer; phone.
  Available interactions      : Open, interpret, acknowledge, escalate.

USER MUST:

  1. Open new result (notification).
  2. Select interpretation (normal / which abnormality).
  3. Choose action from decision card.
  4. Escalate by SBAR if required.
  5. Acknowledge.
  6. Open each result; compare with reference AND previous values; classify normal / abnormal-not-important /
     abnormal-important.
  7. Read lab comments (e.g. 'haemolysed'); repeat suspicious results (different tube/arm).
  8. Acknowledge (sign) every result; answer critical-result phone calls: confirm identity, READ BACK, act,
     document.

SYSTEM THEN:

  1. Formats values with LOCAL ranges; flags.
  2. Checks interpretation against finding codes.
  3. Opens decision points; applies consequences (e.g. MRCP → surgery delayed).
  4. Blocks 'ready' while critical findings unacted.
  5. Unacknowledged critical result → hard safety blocker; if real, patient deteriorates.
  6. Treating an artefact without repeating → 'over-treatment' debrief.
  7. Sample artefacts modelled: haemolysis (false high K), drip-arm dilution, EDTA contamination, platelet
     clumping, mislabelled/rejected sample.
  8. 'Appendix not visualised' is equivocal, NOT negative (classic error built in).

  If correct →                : Results acknowledged; correct branch; credits.
  If incorrect →              : Wrong interpretation → major; ignored critical → CRITICAL.
  If information missing →    : Pending results listed; readiness requires all REQUIRED results.
  If safety condition fails → : Critical finding unacknowledged → readiness blocked.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L7-01
      Trigger: read_result
      Condition: available
      Check: —
      Correct: viewed flag
      Incorrect: —
      Feedback: —
      Animation: —
      Next state: —
  * L7-02
      Trigger: interpret
      Condition: viewed
      Check: matches finding codes
      Correct: credit
      Incorrect: major
      Feedback: —
      Animation: —
      Next state: DP?
  * L7-03
      Trigger: acknowledge critical
      Condition: critical finding
      Check: action chosen
      Correct: unblock
      Incorrect: critical if none
      Feedback: Red card
      Animation: Phone ring
      Next state: —
  * L7-04
      Trigger: escalate(SBAR)
      Condition: DP requires
      Check: elements present
      Correct: senior response
      Incorrect: incomplete → minor
      Feedback: —
      Animation: Senior NPC
      Next state: —

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E7-A
      What went wrong: Result never opened
      User sees: Notification badge
      Should notice: Unviewed result
      Correct response: Open & act
      System does: Major; readiness block if required
      Progression: Blocked
      Feedback: 'Unreviewed results'
      State: BLOCK
      Resolution: Open
  * E7-B
      What went wrong: Abnormal LFT interpreted as normal (S1-V5)
      User sees: Proceeds
      Should notice: Obstructive pattern
      Correct response: MRCP/senior
      System does: Major/critical [CLINICAL VALIDATION REQUIRED]
      Progression: Allowed then blocked at Sign In?
      Feedback: Debrief
      State: —
      Resolution: —

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV7-1 E-LATE-ABNORMAL-K
      Trigger: Result timer
      Patient: —
      Monitor: —
      Visual/audio: Lab phone
      Recognise: Abnormal K
      Response: Acknowledge + escalate per local protocol
      If no response: Critical if theatre proceeds
  * EV7-2 E-RESULT-DELAYED
      Trigger: Scheduled (S2 CRP)
      Patient: —
      Monitor: —
      Visual/audio: Lab message 'analyser delay'
      Recognise: Missing info
      Response: Proceed if safe or chase
      If no response: —

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Allergy band
      Use S1/S2: ✓/✓
      Steps: 1, 7, 13
      Information provided: Allergy flag (details by asking)
      Failure scenario: Missing band → reaction branch
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * 12-lead ECG + electrodes
      Use S1/S2: cond./cond.
      Steps: 6–7
      Information provided: Rhythm, intervals, ST/T; machine read-out may be wrong
      Failure scenario: Wrong lead placement
  * Blood gas analyser display
      Use S1/S2: —/cond.
      Steps: 7
      Information provided: Lactate, pH
      Failure scenario: —
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Results display / lab report
      Use S1/S2: ✓/✓
      Steps: 7
      Information provided: Values, H/L, comments, critical call
      Failure scenario: Unacknowledged critical → blocker
  * Ultrasound machine / report
      Use S1/S2: cond./✓
      Steps: 6–7
      Information provided: Images + category
      Failure scenario: 'Not visualised' ≠ negative
  * CT (room or report only)
      Use S1/S2: cond./cond.
      Steps: 6–7
      Information provided: Report + key images
      Failure scenario: Contrast with AKI/allergy
  * MRCP / ERCP report
      Use S1/S2: cond./—
      Steps: 7
      Information provided: Duct size, stones
      Failure scenario: Duct stone missed
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Readiness blocker service + escalation intents. Sannan — Results viewer UI
(table/image/strip), phone interaction. Moiz — ResultGenerator (finding codes→values with local ranges),
report texts, curated images with licence records; mark [CLINICAL VALIDATION REQUIRED]

Validation: All finding → action mappings. [CLINICAL VALIDATION REQUIRED] · Result values for each variant
(generated within reviewer-approved ranges). [CLINICAL VALIDATION REQUIRED] · Hospital lab ranges and critical
list.
```


## 13. STEP 8 — DECIDING, TREATING AND OPTIMISING

```text
Priority P0 · walkthrough p.88 · build guides: B-M14, B-R17, B-R13, B-R18, B-R14
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Using all the information to choose one of the five outcomes (safe / more
                                investigation / treat-optimise / senior-anaesthetic review / emergency), and
                                giving any treatment needed before theatre.
  Why are we doing it?        : This is the core decision skill of ProcSense. A correct decision protects the
                                patient; the 'right' answer is different in S1 and S2.
  What does the trainee actually do?: 1. Ask: "Is it safe to continue?" using the decision diagram below. 2.
                                Pain: assess score; after the allergy check, request analgesia (e.g.
                                paracetamol ± stronger painkiller) — a qualified clinician confirms drug and
                                dose; the nurse administers. 3. Nausea: request an anti-sickness medicine
                                through the same workflow. 4. Drip (cannula): S2 always; S1 usually in the
                                anaesthetic room CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
                                CONFIRMATION . 5. Fluids if dehydrated or low BP: request and agree the fluid
                                plan with the senior/anaesthetist; re-check vital signs after. 6. Medicine
                                plan: hold/continue diabetes, BP, blood-thinner, steroid medicines (Part 8).
                                7. Clot prevention: stockings if indicated; injection plan. 8. Antibiotics: S2
                                — flag the need once appendicitis is decided (sooner if septic, per NICE NG253
                                risk tier) so the agent per institutional policy is prescribed by a qualified
                                clinician; S1 only if risk factors. 9. Re-assess: vital signs, pain, behaviour
                                after each treatment. Decision diagram — 'Is it safe to continue?' (the master
                                decision) All information gathered so far Any EMERGENCY sign? (NEWS2 7+, top
                                BP 90 or less, oxygen 91% or less, new confusion, anaphylaxis, chest pain,
                                positive pregnancy test with shock) EMERGENCY: call for help, ABCDE, treat
                                (Part 14) YES NO Does the diagnosis look different or complicated? (S1
                                inflamed GB/jaundice; S2 an appendicitis mimic such as ectopic pregnancy, a
                                burst appendix, other cause) SENIOR review → change pathway / more
                                investigation YES NO Is there a fixable problem? (BP 180/110+, HbA1c above 69,
                                salts abnormal, anaemia, blood thinner not stopped, fasting breach, new AF)
                                Verify → assess severity and context → senior/anaesthetic review. S1: usually
                                optimise first (defer/re-time); S2: correct in parallel YES NO Anything
                                affecting anaesthesia? (airway, sleep apnoea, aspiration risk, heart/lung
                                disease) ANAESTHETIST review / plan YES NO SAFE TO CONTINUE → consent and
                                preparation
  What does the trainee see?  : Drug chart, drip stand, fluid bags, medicine trolley, stockings; nurse asking
                                for instructions.
  What information do they receive?: Nurse: "Before I give this, can you confirm her allergy status?" Senior:
                                "Agreed — I've prescribed it." Response of vital signs to treatment.
  What does that information mean?: Improvement after treatment (pain falls, heart rate slows) confirms the
                                cause; no improvement suggests something more serious.
  What is the normal/reference value?: After treatment: pain 0–3; HR below 100; BP stable; NEWS2 falling.
  What counts as abnormal?    : Pain not controlled; HR still above 100 after pain relief and fluids; BP not
                                responding to fluid.
  What counts as seriously concerning?: Worsening despite treatment; any reaction after a medicine.
  What could cause the abnormal result?: Disease progressing (burst appendix, sepsis), bleeding, allergic
                                reaction, wrong diagnosis.
  What should the trainee check next?: 1. Re-examine. 2. Repeat key bloods (lactate, Hb). 3. Call senior.
  What happens if the trainee finds nothing wrong?: Go to STEP 9 (consent).
  What happens if the trainee finds something abnormal?: Follow the diagram and Part 14 'What if'.
  What happens if the abnormality is mild?: Continue treating; re-check in 30–60 minutes.
  What happens if it is severe?: Emergency pathway.
  When do we call the senior doctor?: No improvement after first treatments; sepsis; any change of diagnosis;
                                any S1 deferral decision (senior confirms).
  When do we call the anaesthetist?: Fasting breach; aspiration risk; electrolyte, heart, airway issues;
                                sepsis (anaesthetic plan changes).
  When might surgery be delayed?: S1: any optimisable problem. S2: only briefly to resuscitate (fluids,
                                antibiotics, correct potassium) — not to wait for perfection.
  When should emergency surgery continue despite the abnormality?: S2: sepsis, kidney injury, mild salt
                                problems, unknown fasting, high WBC — all treated alongside; surgery is the
                                cure.
  What happens if the trainee misses it?: S2: no request for pain relief/fluids/antibiotics → deterioration
                                clock speeds up (heart rate +5–10 every ~30 min SIMULATOR DESIGN VALUE
                                (PROVISIONAL) ). Requesting a drug the patient is allergic to → nurse
                                challenges (beginner mode) or reaction occurs (advanced).
  What happens after the correct action?: Patient improves and is 'ready for consent'.
  Medical words used here     : Optimise Improve the patient's condition before surgery. Bolus A quick volume
                                of fluid given through the drip.
  Where this comes from       : NICE CG174; NICE NG89; NICE NG125; WSES 2020; CPOC 2023.
  Clinician validation        : Surgeon/anaesthetist: all treatment choices; postponement rules.
```


### What the player sees (WT Part 16 — Stage 8)

```text
  On screen / in front of the player: Drug chart, drip stand, fluid bags, medicine trolley, stockings.
  Patient — what they say     : Pain reduces after analgesia ("That's a bit better").
  Monitor — numbers           : HR/BP respond to treatment over time.
  Interaction                 : Recognises the need (e.g. analgesia, fluids, stockings), requests it through
                                the drug-chart / nurse workflow and proposes the drug class; a qualified
                                clinician confirms drug and dose; the nurse NPC administers; trainee calls
                                senior.
  Information revealed        : Response of vital signs.
  Decision                    : Safe / investigate / optimise / review / emergency?
  Correct action →            : Patient improves; case progresses.
  Incorrect action →          : Requesting a drug the patient is allergic to → nurse challenges at the check;
                                if ignored, reaction branch.
  Missed abnormality →        : No treatment in S2 → deterioration clock speeds up.
  Escalation →                : Escalation answered by NPC with advice/arrival.
```


#### VR reasoning card C.4 — Low SpO2

```text
  Why                         : Possible hypoxaemia or artefact
  Trainee action              : Look, assess A/B, check probe, target, oxygen per protocol, cause, reassess
  What the trainee sees       : Patient, waveform, masks
  Information                 : SpO2 + waveform quality
  Interpretation              : Real vs artefact; target range
  Decision                    : Oxygen needed? Cause?
  Correct action              : SpO2 to target; cause found
  Incorrect action            : Blind high-flow O2 / ignoring
  Missed abnormality          : Hypoxia worsens
  Escalation                  : Anaesthetist
  Consequence                 : Breathing branch
  Next step                   : Continue
```


#### VR reasoning card C.7 — Possible sepsis

```text
  Why                         : Organ dysfunction from infection
  Trainee action              : Infection? NEWS2 + single params + organ dysfunction + trend; SBAR
  What the trainee sees       : Monitor, patient, lactate
  Information                 : NEWS2 tier, lactate
  Interpretation              : NG253 risk tier
  Decision                    : High/moderate/low?
  Correct action              : Antibiotics/fluids timed by team
  Incorrect action            : Delay for results
  Missed abnormality          : Shock
  Escalation                  : Senior; critical care
  Consequence                 : Timed bundle
  Next step                   : Source control
```


#### VR reasoning card C.9 — Hypoglycaemia

```text
  Why                         : Brain at risk
  Trainee action              : Check glucose; initiate protocol; recheck
  What the trainee sees       : Meter, patient
  Information                 : CBG
  Interpretation              : <4 = treat now
  Decision                    : Start protocol?
  Correct action              : Glucose recovers
  Incorrect action            : Recheck later only
  Missed abnormality          : Seizure/coma
  Escalation                  : Nurse; anaesthetist
  Consequence                 : Fasting implications
  Next step                   : Medication review
```


#### VR reasoning card C.14 — Medicine needed

```text
  Why                         : Pain, nausea, fluids, antibiotics
  Trainee action              : Recognise need; allergy check; request via workflow
  What the trainee sees       : Drug chart, nurse
  Information                 : Confirmed prescription
  Interpretation              : Who prescribes? (clinician)
  Decision                    : Initiate?
  Correct action              : Clinician confirms; nurse gives
  Incorrect action            : Requests allergen
  Missed abnormality          : Nurse challenge/reaction
  Escalation                  : Prescriber
  Consequence                 : Treatment given
  Next step                   : Reassess
```


#### VR reasoning card C.15 — Anticoagulant

```text
  Why                         : Bleeding vs clot risk
  Trainee action              : Ask last dose time; renal function
  What the trainee sees       : Drug list
  Information                 : Times
  Interpretation              : Within stop window?
  Decision                    : Proceed/defer?
  Correct action              : Correct decision
  Incorrect action            : Not asking
  Missed abnormality          : Sign In stop
  Escalation                  : Anaesthetist/senior
  Consequence                 : Deferral
  Next step                   : Plan

Scenario maps: S1 stages 8 Optimisation; S2 stages 8 Optimisation (in parallel).

  [TABLE] What-if | Branch | Correct action
  * WI-01
      Branch: What if blood pressure is too low? (top number 90 or less)
      Correct action: Call for help; lie flat; assess airway/breathing and start oxygen per protocol to the
          patient's target; two drips; request a fluid bolus (e.g. 500 mL crystalloid, NICE CG174; the
          clinician decides volume) and re-check EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE
          PRESCRIPTION ; treat cause (antibiotics if septic; adrenaline if anaphylaxis; blood if bleeding).
  * WI-02
      Branch: What if blood pressure is extremely high? (180/110 or more)
      Correct action: No symptoms: S1 → postpone and refer for control (don't rush BP down). With symptoms →
          emergency medical review.
  * WI-03
      Branch: What if the heart rate is very high? (above 130)
      Correct action: ABCDE; treat cause (pain relief, fluids, antibiotics); ECG-guided rhythm treatment by
          senior/medical team.
  * WI-04
      Branch: What if the heart rate is very slow? (40 or below)
      Correct action: ABCDE; stop causative drugs; emergency medicines/pacing per resuscitation team.
  * WI-05
      Branch: What if the patient has a fever? (38.0 °C or more)
      Correct action: Paracetamol; fluids if dry; antibiotics according to cause; sepsis bundle if criteria
          met.
  * WI-06
      Branch: What if the patient is cold? (36.0 °C or less)
      Correct action: Warm (forced-air blanket); don't send to theatre until 36.0 unless urgent CLINICAL
          REFERENCE — SOURCE BACKED (NICE CG65).
  * WI-07
      Branch: What if oxygen saturation is low? (below 94%)
      Correct action: Look at the patient and assess airway/breathing; check probe and waveform; if the low
          reading is real, sit up and start oxygen per protocol to the patient's target range (94–98%, or
          88–92% if at risk of hypercapnic failure); find the cause (stop fluids if overloaded per senior;
          stimulate if over-sedated — antidote by clinician); reassess within minutes (process in Part 4.5).
  * WI-09
      Branch: What if breathing is very slow? (8 or less)
      Correct action: Wake/stimulate; oxygen; support airway; naloxone antidote PATIENT-SPECIFIC DOSE —
          CLINICIAN CONTROLLED ; stop further opioids.
  * WI-10
      Branch: What if the patient becomes confused?
      Correct action: Treat what's found (sugar below 4 → glucose); sepsis bundle if infection; reassess
          capacity (consent may be invalid now).
  * WI-11
      Branch: What if pain gets much worse or the tummy becomes rigid?
      Correct action: Pain relief; nil by mouth; fluids; antibiotics; prepare for urgent surgery.
  * WI-26
      Branch: What if the patient is pregnant?
      Correct action: S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise →
          gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok.
  * WI-27
      Branch: What if pregnancy status is unknown or the patient declines a test?
      Correct action: Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior +
          anaesthetist decide whether to proceed.
  * WI-28
      Branch: What if the patient is allergic to an antibiotic?
      Correct action: Record; choose antibiotic by hospital rule (mild rash: cefazolin often allowed; severe:
          per policy) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
  * WI-29
      Branch: What if an allergic reaction happens after a medicine?
      Correct action: STOP the drug; call for help; lie flat (sit up if struggling to breathe); call the
          emergency team; the responsible clinician gives IM adrenaline (adult standard dose 500 micrograms,
          repeat after 5 min if no improvement EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE
          PRESCRIPTION ); high-flow oxygen; IV fluid bolus per Resuscitation Council UK (adults 500–1000 mL
          crystalloid).
  * WI-30
      Branch: What if the patient takes blood thinners?
      Correct action: S1: proceed only if stopped per plan (Part 8); otherwise postpone; never stop
          antiplatelets after a recent stent without the heart team. S2: senior/haematology decide reversal
          PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED .
  * WI-31
      Branch: What if the patient has diabetes?
      Correct action: Medicine plan (Part 8); first on list if possible; check sugar at least hourly; target
          6–10.
  * WI-32
      Branch: What if the patient takes steroids, GLP-1 injections or SGLT2 tablets?
      Correct action: Per Part 8/9.
  * WI-39
      Branch: What if the patient deteriorates?
      Correct action: Treat as found; increase monitoring.
  * WI-40
      Branch: What if the patient becomes septic?
      Correct action: Sepsis bundle within 1 hour (oxygen, cultures, antibiotics, fluids, lactate, urine
          output).
```


### Implementation (FYP design)

```text
  Real workflow               : Doses, routes and timings from formulary.
  FYP simulation              : Treatment = abstract object; 'administer' after required checks; effects via
                                physiology engine.
  FYP simplification          : No doses, no drug calculations, no infusion rates.
  AI                          : None for treatment decisions. Doses are never generated by AI.

  Room                        : Bedside.
  Patient                     : Lying; arm accessible for IV.
  Staff                       : Nurse NPC performs double-check and administration if asked.
  Equipment                   : IV cannula kit, fluid bag + giving set + drip stand/pump, drug tray
                                (abstract), compression stockings/IPC, drug chart (tablet).
  Available interactions      : Prescribe, check, administer, insert IV, start fluid, fit IPC.

USER MUST:

  1. Open 'Treat' tab; choose treatment object.
  2. Complete required checks (ID, allergy, indication, timing).
  3. Administer (or ask nurse; closed-loop read-back).
  4. Insert IV (select site on arm → intent).
  5. Hang fluid bag → connect → start (pump or gravity abstract).
  6. Record medication decisions.
  7. Re-check observations.
  8. Answer 'Is it safe to continue?' with the master decision diagram (5 outcomes).
  9. Request analgesia/antiemetic/fluids/antibiotics through the drug-chart workflow and PROPOSE the drug
     class; clinician confirms drug+dose; nurse administers.
  10. Every drug passes three checks: allergy? kidneys/liver? pregnancy? (+ anticoagulant for bleeding drugs).

SYSTEM THEN:

  1. Required-check engine; missing check → warning/critical.
  2. Allergy-class conflict → contraindicated (E-HYPERSENSITIVITY eligible).
  3. Effects applied to LitePhysio (pain↓, volume deficit↓).
  4. IV success/failure seeded by patient factors; anaphylaxis eligibility when an allergen is given (WT
     14.4).
  5. Documentation flag required.
  6. Requesting an allergen → nurse challenges (BEGINNER mode) or reaction occurs (ADVANCED mode).
  7. S2 untreated → deterioration clock speeds up (HR +5–10 and NEWS2 +1 ~every 30 min; Type D).
  8. Problem registry ordered by the 7-step priority ladder (WT 14B.1); treatment clashes detected (WT 14B.3).

  If correct →                : Treatments logged with checks; patient improves; goals satisfied.
  If incorrect →              : Skipped checks → items; allergen given → event + critical.
  If information missing →    : No IV → IV drugs/fluids unavailable.
  If safety condition fails → : Allergy unknown → blocked (practice) / critical (assessment).

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L8-01
      Trigger: administer(t)
      Condition: prescribed
      Check: required checks complete
      Correct: effect applied
      Incorrect: missing check → item
      Feedback: Check list ticks
      Animation: Nurse gives drug
      Next state: —
  * L8-02
      Trigger: administer allergen class
      Condition: allergy exists
      Check: allergy known?
      Correct: — (should not)
      Incorrect: critical + E-HYPERSENSITIVITY
      Feedback: Alarm
      Animation: Rash/flush
      Next state: EVENT
  * L8-03
      Trigger: insert_iv
      Condition: patient cooperative
      Check: seeded success
      Correct: iv=placed
      Incorrect: failed → retry
      Feedback: —
      Animation: Cannula appears
      Next state: —
  * L8-04
      Trigger: start_fluid
      Condition: iv placed
      Check: —
      Correct: volume deficit ↓ over time
      Incorrect: —
      Feedback: Drip animation
      Animation: Drip
      Next state: —

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E8-A
      What went wrong: Drug given without allergy check
      User sees: Rash, BP falls
      Should notice: Hypersensitivity signs
      Correct response: Stop, help, protocol
      System does: Event + critical
      Progression: Allowed
      Feedback: Alarm
      State: EVENT
      Resolution: Protocol response
  * E8-B
      What went wrong: No fluids for dehydrated S2 patient
      User sees: HR rising
      Should notice: Hypovolaemia
      Correct response: Fluids + reassess
      System does: Deterioration event
      Progression: Allowed
      Feedback: Monitor amber
      State: URG
      Resolution: Fluids started

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV8-1 E-ANAPHYLAXIS
      Trigger: Allergen administered (advanced mode) — WT 14.4 [CLINICAL VALIDATION REQUIRED]
      Patient: Hives, swollen lips, wheeze
      Monitor: BP falls (e.g. 80/45), SpO2 falls
      Visual/audio: 'My lips feel swollen… I can't breathe'
      Recognise: Anaphylaxis signs
      Response: Stop, lie flat, observe, reassure, call help if not resolving [CLINICAL VALIDATION REQUIRED]
      If no response: Fall-risk item
  * EV8-2 E-HYPERSENSITIVITY
      Trigger: Allergen class given
      Patient: Flushing, rash, breathlessness per severity
      Monitor: HR↑, BP↓, SpO2↓
      Visual/audio: Alarm
      Recognise: Allergic reaction
      Response: Stop drug, call for help, local anaphylaxis protocol [CLINICAL VALIDATION REQUIRED]
      If no response: Harmed outcome
  * EV8-3 E-DETERIORATE-DEHYD (S2)
      Trigger: No fluids by deadline & HR above threshold [CLINICAL VALIDATION REQUIRED]
      Patient: Pale, thirsty, confused later
      Monitor: HR↑ BP↓
      Visual/audio: Monitor alarm
      Recognise: Deterioration
      Response: Fluids, reassess, escalate
      If no response: Critical if unrecognised

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Vital-sign monitor
      Use S1/S2: ✓/✓
      Steps: 3, 8, 12–14
      Information provided: SpO2, pulse, NIBP/MAP, ECG, alarms
      Failure scenario: Artefact if probe poor; alarm limits C
  * Blood culture bottles
      Use S1/S2: —/cond.
      Steps: 6, 8
      Information provided: Cue
      Failure scenario: Delaying antibiotics for cultures → harm
  * Glucose/ketone meter
      Use S1/S2: cond./cond.
      Steps: 3, 8
      Information provided: mmol/L
      Failure scenario: Hypo missed
  * IV cannula kit
      Use S1/S2: ✓/✓
      Steps: 8
      Information provided: Cue (insertion abstracted)
      Failure scenario: Failed cannulation → delay
  * IV giving set + fluid bags
      Use S1/S2: —/✓
      Steps: 8
      Information provided: Fluid type, volume
      Failure scenario: Overload in HF
  * Infusion pump
      Use S1/S2: cond./cond.
      Steps: 8
      Information provided: Rate abstracted
      Failure scenario: Pump alarm
  * O2 supply, nasal cannula, simple + non-rebreather masks
      Use S1/S2: cond./cond.
      Steps: 8, 12
      Information provided: Device + flow
      Failure scenario: Over-oxygenating CO2 retainer
  * Nasogastric tube
      Use S1/S2: —/cond.
      Steps: 8
      Information provided: Cue
      Failure scenario: Senior-directed
  * Urinary catheter + urometer
      Use S1/S2: —/cond.
      Steps: 8
      Information provided: Cue
      Failure scenario: Senior-directed
  * Medication trolley / drug chart
      Use S1/S2: ✓/✓
      Steps: 8
      Information provided: Clinician-entered prescriptions; allergy alert
      Failure scenario: Allergen request → nurse challenge
  * Sharps bin
      Use S1/S2: ✓/✓
      Steps: 6, 8
      Information provided: Cue
      Failure scenario: Needlestick
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Stockings / IPC
      Use S1/S2: ✓/✓
      Steps: 8, 10
      Information provided: Cue
      Failure scenario: Arterial disease → harm
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — TreatmentSystem effects + LitePhysio coupling; event triggers
E-ANAPHYLAXIS/HYPERSENSITIVITY/DETERIORATE. Sannan — IV/fluid/IPC props + animations; Treat tab UI; rash/pale
shaders. Moiz — Treatment library (no doses), required checks, medication decision content from local
protocol; mark [CLINICAL VALIDATION REQUIRED]

Validation: All treatment indications and restrictions. [CLINICAL VALIDATION REQUIRED] · Physiological effect
magnitudes of fluids/analgesia. [CLINICAL VALIDATION REQUIRED] · Hypersensitivity event representation.
[CLINICAL VALIDATION REQUIRED] · Medication decision expected options. [CLINICAL VALIDATION REQUIRED] ·
Surgeon/anaesthetist: all treatment choices; postponement rules.
```


## 14. STEP 9 — CONSENT

```text
Priority P0 · walkthrough p.91 · build guides: B-R6, B-M3, B-R3, B-S10
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Explaining the diagnosis, the operation, benefits, risks and alternatives,
                                checking understanding, and getting the patient's signature (Part 10).
  Why are we doing it?        : A legal and ethical requirement and the patient's right to decide.
  What does the trainee actually do?: 1. Make sure pain is controlled and the patient is alert; arrange
                                privacy and an interpreter if needed. 2. Explain diagnosis and operation in
                                plain words. 3. Benefits, risks (with numbers), alternatives, consequences of
                                no treatment. 4. Ask what matters to them; answer questions honestly. 5.
                                Teach-back. 6. Sign the form; give a copy; explain what happens next.
  What does the trainee see?  : Patient (and family), consent form, leaflet, interpreter phone.
  What information do they receive?: Patient questions: "Will I be able to have children?" "Can I go home
                                today?" "Can't I just take tablets?"
  What does that information mean?: Questions reveal fears and what matters to the patient; they must be
                                answered.
  What is the normal/reference value?: Patient understands, teach-back succeeds, signs.
  What counts as abnormal?    : Patient confused, language barrier, wants more time, refuses.
  What counts as seriously concerning?: No capacity in an urgent situation; patient under 18; refusal of life-
                                saving surgery.
  What should the trainee check next?: 1. Treat causes of confusion. 2. Interpreter. 3. Senior for refusal or
                                capacity problems.
  What happens if the trainee finds nothing wrong?: Go to STEP 10 (prepare patient).
  What happens if the trainee finds something abnormal?: See Part 10 consent problems table.
  When do we call the senior doctor?: Refusal; lack of capacity; legal doubt; minors.
  When do we call the anaesthetist?: Anaesthetic risks are explained by the anaesthetist.
  When might surgery be delayed?: S1: patient wants more time/information → rebook.
  When should emergency surgery continue despite the abnormality?: S2: if the patient lacks capacity and
                                surgery is urgent, the senior follows the emergency/best-interests route under
                                local law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
                                .
  What happens if the trainee misses it?: Missing risks or alternatives → low consent score + 'patient
                                complaint' in debrief; consenting a confused patient → safety failure.
  What happens after the correct action?: Valid consent recorded; checklist item ticked.
  Where this comes from       : GMC 2020; Montgomery 2015.
  Clinician validation        : Hospital consent form and law; surgeon's risk figures.
```


### What the player sees (WT Part 16 — Stage 9)

```text
  On screen / in front of the player: Patient, family, form, leaflet, interpreter phone.
  Patient — what they say     : Questions: fertility, going home, tablets instead?
  Monitor — numbers           : —
  Interaction                 : Dialogue choices; teach-back; sign form.
  Information revealed        : Patient's understanding level.
  Decision                    : Valid consent?
  Correct action →            : Signed form.
  Incorrect action →          : Missing risks/alternatives → low score/complaint.
  Missed abnormality →        : Consenting confused patient → safety failure.
  Escalation →                : Refusal → senior.
```


#### VR reasoning card C.17 — Consent

```text
  Why                         : Legal/ethical permission
  Trainee action              : Explain, risks, alternatives, teach-back
  What the trainee sees       : Patient, form
  Information                 : Questions
  Interpretation              : Capacity, understanding
  Decision                    : Valid?
  Correct action              : Signed
  Incorrect action            : Missing risks / confused patient
  Missed abnormality          : Invalid consent
  Escalation                  : Senior
  Consequence                 : Blocker
  Next step                   : Prep

Scenario maps: S1 stages 9 Consent; S2 stages 9 Consent.

  [TABLE] What-if | Branch | Correct action
  * WI-41
      Branch: What if the patient refuses surgery?
      Correct action: Explore and correct misunderstandings; offer alternatives (antibiotics-only for simple
          appendicitis); never pressure; document; safety advice.
  * WI-42
      Branch: What if the patient cannot understand the consent discussion?
      Correct action: Interpreter; simpler words/diagrams; treat confusion; if no capacity and urgent → senior
          emergency route per law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
  * WI-43
      Branch: What if the patient is under 18?
      Correct action: Parent/guardian consent; involve the young person.
```


### Implementation (FYP design)

```text
  Real workflow               : Surgeon consents.
  FYP simulation              : Trainee performs consent conversation via acts; surgeon NPC may countersign.
  FYP simplification          : Risk list pre-approved; no legal capacity assessment.
  AI                          : P1 PROPOSED: communication classifier labels spoken consent explanation
                                (debrief only unless validated); LLM phrases patient questions.

  Room                        : Bedside, curtain.
  Patient                     : Sitting up; anxiety behaviour visible.
  Staff                       : Optional nurse witness.
  Equipment                   : Consent form (tablet/paper prop), pen.
  Available interactions      : Communication acts; sign form.

USER MUST:

  1. Choose EXPLAIN_PURPOSE, then CONSENT_DISCUSSION topics (procedure, benefits, risks, alternatives).
  2. Answer patient question from approved answers.
  3. CHECK_UNDERSTANDING (teach-back).
  4. Hand form + pen to patient; patient signs.
  5. Explain next steps.
  6. Readiness (pain controlled, alert, privacy, interpreter); capacity check; diagnosis; operation; benefits;
     risks WITH NUMBERS; alternatives (S2 antibiotics-only: CODA/APPAC); no-treatment consequences; 'What
     matters most to you?'; teach-back; sign; copy; next steps.

SYSTEM THEN:

  1. Tracks required elements; behaviour updates (anxiety/understanding).
  2. Patient may ask question from pool based on persona.
  3. Signature only accepted if required elements covered (practice) / logged otherwise (assessment).
  4. Missing risks/alternatives → low consent score + 'patient complaint' in debrief.
  5. Consenting a confused patient → safety failure; trained interpreter (not child relative).
  6. Minor (<18) → guardian per law (Type C); refusal respected, never pressured (professionalism failure).

  If correct →                : Consent goal complete; understanding high.
  If incorrect →              : Missing risk element → major; false reassurance ('nothing can go wrong') →
                                communication item.
  If information missing →    : Patient refuses to sign until question answered.
  If safety condition fails → : Transfer/Sign In without valid consent → CRITICAL.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L9-01
      Trigger: consent_discussion(topic)
      Condition: —
      Check: topic in required set
      Correct: element ticked
      Incorrect: —
      Feedback: —
      Animation: Patient nods
      Next state: —
  * L9-02
      Trigger: sign(consent)
      Condition: elements complete
      Check: understanding ≥ thr
      Correct: consent=signed
      Incorrect: refused/incomplete
      Feedback: —
      Animation: Signing animation
      Next state: ST9
  * L9-03
      Trigger: answer_question(q,a)
      Condition: question pending
      Check: answer approved
      Correct: trust+
      Incorrect: wrong/none → trust−
      Feedback: —
      Animation: —
      Next state: —

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E9-A
      What went wrong: Skipped risks
      User sees: Patient signs quickly (low literacy persona)
      Should notice: Understanding low
      Correct response: Explain + teach-back
      System does: Major item
      Progression: Allowed
      Feedback: Debrief
      State: —
      Resolution: —
  * E9-B
      What went wrong: False reassurance
      User sees: Patient relaxes
      Should notice: Dishonest statement
      Correct response: Honest reassurance
      System does: Communication item
      Progression: Allowed
      Feedback: Debrief
      State: —
      Resolution: —

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV9-1 E-PT-QUESTION (scar/fertility)
      Trigger: Persona + context
      Patient: Patient asks
      Monitor: —
      Visual/audio: Voice line
      Recognise: Concern
      Response: Approved answer; offer senior if unsure
      If no response: Trust↓, anxiety↑

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Consent form
      Use S1/S2: ✓/✓
      Steps: 9
      Information provided: Procedure, risks, signatures
      Failure scenario: Wrong procedure → blocker (local form C)
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Communication act engine + behaviour deltas. Sannan — Form/pen interactions; patient signing
clip; anxiety idles. Moiz — Consent content & Q/A (surgeon review), rubric; mark [CLINICAL VALIDATION
REQUIRED]

Validation: Risk/benefit/alternative wording for S1 and S2. [CLINICAL VALIDATION REQUIRED] · Answers to
sensitive patient questions (fertility, scars, day-case). [CLINICAL VALIDATION REQUIRED] · Hospital consent
form and law; surgeon's risk figures.
```


## 15. STEP 10 — PREPARING THE PATIENT (WARD/ED CHECKLIST)

```text
Priority P0 · walkthrough p.93 · build guides: B-R15, B-S10
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Physical and paperwork preparation so nothing is missing when the patient
                                reaches theatre.
  Why are we doing it?        : Small omissions — jewellery, missing consent, unknown fasting time — cause
                                harm or cancellations.
  What does the trainee actually do?: 1. Re-confirm fasting times. 2. Hospital gown; remove jewellery (tape
                                rings that won't come off), make-up, nail polish (for the oxygen probe). 3.
                                Note dentures, hearing aids, contact lenses, implants. 4. Hair removal only if
                                needed, with clippers (not razors — they cause infection) CLINICAL REFERENCE —
                                SOURCE BACKED . 5. Site marking if required (usually NOT for these
                                midline/belly-button keyhole operations — no left/right CONTEXT-DEPENDENT —
                                INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ). 6. Stockings if prescribed.
                                7. Pregnancy status recorded. 8. Consent present and correct. 9. Allergy band
                                on; identity band on. 10. Diabetic: finger-prick sugar recorded. 11.
                                Temperature at least 36.0 °C (warm if not) CLINICAL REFERENCE — SOURCE BACKED
                                . 12. All documents together: notes, consent, results, drug chart, scans. 13.
                                Complete the checklist.
  What does the trainee see?  : Gown, jewellery tray, clippers, marker pen, stockings, checklist form,
                                document folder.
  What information do they receive?: Nurse: "Checklist is complete except pregnancy status." Patient might
                                say: "I did have a cup of tea with milk at 7."
  What does that information mean?: Each item must be confirmed; any gap must be filled before transfer.
  What is the normal/reference value?: Every item ticked.
  What counts as abnormal?    : Missing item; late fasting discovery; temperature below 36.0.
  What counts as seriously concerning?: Missing/invalid consent; unknown pregnancy status; identity problem.
  What should the trainee check next?: 1. Fix the missing item. 2. Tell anaesthetist about any late fasting
                                discovery.
  What happens if the trainee finds nothing wrong?: Call theatre: patient ready (STEP 11/12).
  What happens if the trainee finds something abnormal?: Fix before transfer; do not transfer with missing
                                consent.
  When do we call the senior doctor?: Consent problems.
  When do we call the anaesthetist?: Late fasting breach; low temperature.
  When might surgery be delayed?: Until all blockers fixed.
  When should emergency surgery continue despite the abnormality?: S2: fast but complete; uncertain fasting
                                recorded rather than waited for.
  What happens if the trainee misses it?: Razor used → infection-risk debrief. Missing consent → theatre
                                returns the patient.
  What happens after the correct action?: 'Ready for theatre'.
  Where this comes from       : NICE NG125 (hair removal); NICE CG65; WHO SSC.
  Clinician validation        : Hospital ward checklist form; site-marking policy.
```


### What the player sees (WT Part 16 — Stage 10)

```text
  On screen / in front of the player: Gown, jewellery tray, clippers, marker, stockings, checklist, folder.
  Patient — what they say     : "Oh — I did have tea with milk at 7."
  Monitor — numbers           : Temperature check.
  Interaction                 : Tick items; tape ring; clippers (not razor).
  Information revealed        : Missing items highlighted when checked.
  Decision                    : Ready?
  Correct action →            : 'Ready for theatre'.
  Incorrect action →          : Razor → infection debrief; missing consent → transfer refused.
  Missed abnormality →        : —
  Escalation →                : Late fasting → anaesthetist.
```


#### VR reasoning card C.18 — Ward preparation

```text
  Why                         : Prevent theatre errors
  Trainee action              : Checklist items
  What the trainee sees       : Checklist
  Information                 : Gaps
  Interpretation              : Complete?
  Decision                    : Transfer?
  Correct action              : Ready
  Incorrect action            : Razor; missing consent
  Missed abnormality          : Cancellation
  Escalation                  : Nurse
  Consequence                 : Transfer
  Next step                   : Transfer

Scenario maps: S1 stages — (not a separate S1 map stage); S2 stages — (not a separate S2 map stage).

  [TABLE] What-if | Branch | Correct action
  * WI-08
      Branch: What if breathing is very fast? (25 or more)
      Correct action: Treat cause; sepsis bundle if infection.
  * WI-33
      Branch: What if the patient hasn't fasted?
      Correct action: Tell anaesthetist honestly.
  * WI-34
      Branch: What if fasting status is unknown?
      Correct action: Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In.
  * WI-45
      Branch: What if documents are missing?
      Correct action: Re-consent or find; don't transfer.
```


### Implementation (FYP design)

```text
  Real workflow               : Nurse completes.
  FYP simulation              : Trainee verifies checklist with nurse NPC.
  FYP simplification          : Checklist on tablet; gown change is a fade.
  AI                          : None.

  Room                        : Bedside.
  Patient                     : In gown after fade.
  Staff                       : Nurse NPC.
  Equipment                   : Checklist (tablet), skin marker, clippers (optional), IPC sleeves.
  Available interactions      : Tick with verification actions.

USER MUST:

  1. Open ward checklist with nurse.
  2. Verify each item (some require an action: ask fasting again, check jewellery hotspot).
  3. Mark site if applicable.
  4. Confirm documents bundle.
  5. Sign checklist.
  6. Work through the 13 ward-checklist items (fasting re-confirmed; gown; jewellery/make-up/NAIL POLISH;
     dentures/aids/lenses/implants; clippers only if needed; site marking usually N/A; stockings; pregnancy
     status; consent; ID + allergy bands; CBG if diabetic; temp ≥36.0; documents together).

SYSTEM THEN:

  1. Each item validated against state (not just ticked).
  2. Discrepancies open events.
  3. Checklist complete → transfer enabled.
  4. Late fasting discovery ('tea with milk at 7') → tell anaesthetist.
  5. Razor → infection-risk debrief; missing consent → transfer refused / theatre returns patient.

  If correct →                : Checklist complete & consistent.
  If incorrect →              : Ticking an item that contradicts state → critical/major.
  If information missing →    : Item missing → transfer blocked.
  If safety condition fails → : No consent/unknown fasting → blocked.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L10-01
      Trigger: tick(item)
      Condition: —
      Check: state supports tick
      Correct: ticked
      Incorrect: contradiction → item
      Feedback: —
      Animation: —
      Next state: —
  * L10-02
      Trigger: sign(ward_checklist)
      Condition: all ticked
      Check: —
      Correct: transfer enabled
      Incorrect: —
      Feedback: —
      Animation: —
      Next state: ST11

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E10-A
      What went wrong: Ticks 'fasted' without checking
      User sees: —
      Should notice: Unverified
      Correct response: Ask/verify
      System does: Major if later breach
      Progression: Allowed
      Feedback: —
      State: —
      Resolution: —
  * E10-B
      What went wrong: Jewellery left on
      User sees: Ring visible on hand
      Should notice: Diathermy/burn risk [CLINICAL VALIDATION REQUIRED]
      Correct response: Remove/tape
      System does: Minor/major
      Progression: Allowed
      Feedback: —
      State: —
      Resolution: —

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV10-1 E-CONSENT-FORM-INCOMPLETE
      Trigger: Random (difficulty≥2)
      Patient: —
      Monitor: —
      Visual/audio: Nurse: 'signature missing on page 2'
      Recognise: Documentation gap
      Response: Complete with patient
      If no response: Transfer blocked

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Tympanic thermometer
      Use S1/S2: ✓/✓
      Steps: 3, 10
      Information provided: °C
      Failure scenario: <36.0 → warm
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Stockings / IPC
      Use S1/S2: ✓/✓
      Steps: 8, 10
      Information provided: Cue
      Failure scenario: Arterial disease → harm
  * Clippers
      Use S1/S2: cond./cond.
      Steps: 10
      Information provided: Cue
      Failure scenario: Razor → infection risk
  * Skin marker
      Use S1/S2: cond./cond.
      Steps: 10
      Information provided: Cue
      Failure scenario: Usually N/A (C)
  * Forced-air warmer
      Use S1/S2: ✓/✓
      Steps: 10–11
      Information provided: Cue
      Failure scenario: —
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Checklist verification engine (tick requires state evidence). Sannan — Gown fade, marker
decal, jewellery hotspot. Moiz — Ward checklist content from local form; mark [CLINICAL VALIDATION REQUIRED]

Validation: Ward checklist items (use supervising hospital's form). [CLINICAL VALIDATION REQUIRED] · Site-
marking policy for laparoscopic procedures. [CLINICAL VALIDATION REQUIRED] · Hospital ward checklist form;
site-marking policy.
```


## 16. STEP 11 — THEATRE PREPARATION

```text
Priority P0 · walkthrough p.95 · build guides: B-S9, B-S1, B-R15, B-R14
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Making sure the operating theatre, equipment and team are ready for this
                                specific patient.
  Why are we doing it?        : Discovering a broken machine after the patient is asleep is dangerous.
  What does the trainee actually do?: 1. Anaesthetic machine check done and logged. 2. Monitors ready (oxygen,
                                BP, ECG, carbon dioxide, temperature). 3. Suction working; oxygen; airway
                                equipment; difficult-airway trolley known. 4. Operating table and padding. 5.
                                Keyhole 'tower' (camera, light, gas pump) + CO2 gas cylinder with spare;
                                instruments; open set available in case of conversion. 6. Diathermy (electric
                                cutting/sealing device) with its sticky return pad. 7. Warming device. 8.
                                Emergency trolley and defibrillator checked. 9. Specimen pots and labels;
                                count sheets. 10. Team brief if used locally.
  What does the trainee see?  : Theatre with machines, trays, screens, gas cylinders, checklist board.
  What information do they receive?: Scrub nurse: "The gas pump shows low supply." Check log signed or
                                unsigned.
  What does that information mean?: Any fault must be fixed before the patient arrives.
  What is the normal/reference value?: All equipment working.
  What counts as abnormal?    : Fault found.
  What counts as seriously concerning?: Anaesthetic machine failure; no working suction; no oximeter.
  What should the trainee check next?: 1. Report; replace/repair; delay start.
  What happens if the trainee finds nothing wrong?: Send for the patient.
  What happens if the trainee finds something abnormal?: Fix first.
  When do we call the senior doctor?: Delay affecting an emergency case.
  When do we call the anaesthetist?: Any anaesthetic equipment fault.
  When might surgery be delayed?: Until equipment safe.
  When should emergency surgery continue despite the abnormality?: S2: a substitute theatre may be used
                                CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
  What happens if the trainee misses it?: Time Out cannot be completed.
  What happens after the correct action?: 'Theatre ready'.
  Medical words used here     : Diathermy Electric device to cut and stop bleeding. Tower Stack of keyhole
                                equipment: screen, camera, light, gas pump.
  Where this comes from       : Association of Anaesthetists equipment checks (2019); WHO-WFSA 2018.
  Clinician validation        : Theatre team: equipment lists; which items trainee vs staff verify.
```


### What the player sees (WT Part 16 — Stage 11)

```text
  On screen / in front of the player: Theatre: machines, tower, CO2 cylinder gauge, trays, diathermy, warming,
                                resus trolley.
  Patient — what they say     : —
  Monitor — numbers           : Anaesthetic machine check log.
  Interaction                 : Inspect/confirm items.
  Information revealed        : Fault messages (e.g. low gas).
  Decision                    : Ready?
  Correct action →            : 'Theatre ready'.
  Incorrect action →          : Fault ignored → Time Out fails.
  Missed abnormality →        : —
  Escalation →                : Report to theatre lead.

Scenario maps: S1 stages 12 Theatre preparation (runs in parallel before the patient arrives); S2 stages 12
Theatre preparation.

  What-if | Branch                   | Correct action               
  --------+--------------------------+------------------------------
  WI-44   | What if equipment fails? | Report; replace; don't start.
```


### Implementation (FYP design)

```text
  Real workflow               : Several staff in parallel.
  FYP simulation              : Trainee inspects stations and confirms NPC reports; fixes faults via correct
                                escalation.
  FYP simplification          : Panels instead of real machine procedures; instruments as trolley icons.
  AI                          : None (P1: NPC phrasing).

  Room                        : OR: table, anaesthetic machine, monitor, suction, lap tower, instrument
                                trolley, diathermy, warmer, resus trolley.
  Patient                     : Not present yet.
  Staff                       : Scrub nurse, circulating nurse, anaesthetic assistant NPCs.
  Equipment                   : As listed.
  Available interactions      : Inspect station panels; request fix; confirm.

USER MUST:

  1. Teleport to OR.
  2. Open each station panel (anaesthetic machine, monitor, suction, tower, energy device, table, emergency
     trolley, specimen/count).
  3. Run the abstract check.
  4. If fault shown: report to circulating nurse (select) and wait for replacement.
  5. Confirm 'Theatre ready' with team.
  6. Check: anaesthetic machine check logged; monitors; suction; airway kit + difficult-airway trolley; table;
     tower + CO2 + SPARE cylinder; open set; diathermy + pad; warming; resus trolley + defib; specimen pots;
     count sheets; team brief.

SYSTEM THEN:

  1. Stations have states/faults from scenario/events.
  2. Readiness = all REQUIRED groups ready.
  3. Faults resolved by correct escalation + delay.
  4. Fault messages (e.g. 'gas pump shows low supply'); unfixed fault → Time Out cannot complete.
  5. S2: substitute theatre allowed (Type C).

  If correct →                : Theatre ready.
  If incorrect →              : Declaring ready with faults → major/critical.
  If information missing →    : Required station unchecked → not ready.
  If safety condition fails → : Time Out blocked while required equipment not ready.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L11-01
      Trigger: check(station)
      Condition: in OR
      Check: station ok?
      Correct: ready
      Incorrect: fault → fault state
      Feedback: Light green/red
      Animation: Panel
      Next state: —
  * L11-02
      Trigger: report_fault
      Condition: fault
      Check: —
      Correct: replacement delay
      Incorrect: ignored → item
      Feedback: —
      Animation: Nurse fetches
      Next state: —
  * L11-03
      Trigger: declare_ready
      Condition: —
      Check: all required ready
      Correct: OR_READY
      Incorrect: else major
      Feedback: —
      Animation: Team nods
      Next state: ST11

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E11-A
      What went wrong: Insufflator fault ignored
      User sees: Red panel
      Should notice: Fault
      Correct response: Report & replace
      System does: Major; Time Out blocked
      Progression: Blocked
      Feedback: —
      State: BLOCK
      Resolution: Replace
  * E11-B
      What went wrong: Suction not checked
      User sees: —
      Should notice: Unchecked
      Correct response: Check
      System does: Major
      Progression: —
      Feedback: —
      State: —
      Resolution: —

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV11-1 E-EQUIP-INSUFFLATOR-FAULT
      Trigger: Tower check
      Patient: —
      Monitor: Tower panel error
      Visual/audio: Beep
      Recognise: Faulty device
      Response: Report, replacement
      If no response: Delay/critical if proceeding

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Suction
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: Cue
      Failure scenario: Not working → RSI unsafe
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Forced-air warmer
      Use S1/S2: ✓/✓
      Steps: 10–11
      Information provided: Cue
      Failure scenario: —
  * Operating table + padding
      Use S1/S2: ✓/✓
      Steps: 11
      Information provided: Cue
      Failure scenario: —
  * Anaesthesia machine + ventilator + check log
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: Check done/not; settings
      Failure scenario: Check not done → Sign In fails
  * Capnograph
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: EtCO2 waveform (post-induction abstracted)
      Failure scenario: —
  * Airway kit + difficult-airway trolley
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: Cue
      Failure scenario: Difficult airway not declared
  * Surgical lights
      Use S1/S2: ✓/✓
      Steps: 11, 14
      Information provided: Cue
      Failure scenario: —
  * Laparoscopic tower (camera, light, monitor, insufflator)
      Use S1/S2: ✓/✓
      Steps: 11, 14
      Information provided: Image, insufflation pressure/flow, fault messages
      Failure scenario: Fault → delay
  * CO2 cylinder + spare
      Use S1/S2: ✓/✓
      Steps: 11
      Information provided: Gauge level
      Failure scenario: Empty → fault scenario
  * Diathermy unit + return electrode
      Use S1/S2: ✓/✓
      Steps: 11
      Information provided: Cue
      Failure scenario: Pad missing → burn risk
  * Instrument trays (lap chole / lap + open appendix)
      Use S1/S2: ✓/✓
      Steps: 11, 14
      Information provided: Sterility indicators
      Failure scenario: Indicator fail → stop
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — ORReadiness model + fault events. Sannan — OR stations (panels), tower/trolley props, nurse
replacement animation (largest VR task). Moiz — Station content per procedure from WT Step 11 (AoA
2019)/18/19; [CLINICAL VALIDATION REQUIRED]

Validation: S1/S2 equipment lists and positioning. [CLINICAL VALIDATION REQUIRED] · Which items the trainee
(vs NPC) verifies. [CLINICAL VALIDATION REQUIRED] · Theatre team: equipment lists; which items trainee vs
staff verify.
```


## 17. STEP 12 — TRANSFER AND HANDOVER (SBAR)

```text
Priority P0 · walkthrough p.97 · build guides: B-R16, B-M15, B-R8
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : Moving the patient safely to theatre and telling the theatre team everything
                                important in a structured way.
  Why are we doing it?        : Information lost during handover is a classic cause of errors CLINICAL
                                REFERENCE — SOURCE BACKED (WHO Patient Safety Solutions 2007).
  What does the trainee actually do?: 1. Theatre calls for the patient. 2. Choose escort and monitoring based
                                on stability (no international numeric rule exists; ProcSense uses SIMULATOR
                                DESIGN VALUE (PROVISIONAL) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /
                                CLINICIAN CONFIRMATION ): NEWS2 0–4 porter + nurse; 5–6 nurse + portable
                                oxygen monitor; 7+ doctor + nurse with full monitoring and oxygen. 3. Transfer
                                with all documents. 4. Theatre reception nurse checks identity, consent,
                                allergies, fasting, site. 5. Give SBAR handover to the anaesthetist.
  What does the trainee see?  : Trolley, porter, portable monitor, folder; theatre reception desk.
  What information do they receive?: Reception nurse: "Name and date of birth? Any allergies? When did you
                                last eat?"
  What does that information mean?: SBAR = Situation (who and why now), Background (relevant history),
                                Assessment (current state, results), Recommendation (what you need/plan).
  What is the normal/reference value?: All key items handed over. S1 example: "S: Ayesha Khan, 42, for lap
                                chole. B: no medical problems, no allergies. A: obs normal, fasted since 22:00
                                food / 06:00 water, LFTs normal, pregnancy test negative, consent signed. R:
                                ready for theatre, no antibiotic needed per protocol."
  What counts as abnormal?    : Missing item (allergy, fasting time, antibiotic time, blood thinner).
  What counts as seriously concerning?: Patient deteriorates during transfer.
  What should the trainee check next?: 1. If deterioration in transit: stop, ABCDE, call for help.
  What happens if the trainee finds nothing wrong?: STEP 13 (Sign In).
  What happens if the trainee finds something abnormal?: Provide the missing information immediately.
  When do we call the senior doctor?: Deterioration during transfer.
  When do we call the anaesthetist?: This step IS the handover to the anaesthetist.
  When might surgery be delayed?: Missing documents → fetched before proceeding.
  When should emergency surgery continue despite the abnormality?: S2: handover can be brief but must include
                                aspiration risk, antibiotic time, fluids given, latest NEWS2, lactate, kidney
                                status, pregnancy status, scan result.
  What happens if the trainee misses it?: Anaesthetist asks for the missing items; repeated omissions scored;
                                corridor oxygen drop ignored → emergency branch.
  What happens after the correct action?: Theatre team accepts the patient.
  Medical words used here     : SBAR Situation, Background, Assessment, Recommendation.
  Where this comes from       : WHO Patient Safety Solutions 2007; WHO-WFSA 2018.
  Clinician validation        : Hospital transfer policy; required handover items.
```


### What the player sees (WT Part 16 — Stage 12)

```text
  On screen / in front of the player: Trolley, porter, monitor, reception desk, anaesthetist.
  Patient — what they say     : Reception nurse questions.
  Monitor — numbers           : Portable monitor if unstable.
  Interaction                 : Choose escort; deliver SBAR (structured choices or speech).
  Information revealed        : Missing items prompted by anaesthetist.
  Decision                    : Is handover complete?
  Correct action →            : Handover accepted.
  Incorrect action →          : Omissions scored.
  Missed abnormality →        : Corridor deterioration ignored → arrest branch.
  Escalation →                : Call for help in transit.
```


#### VR reasoning card C.19 — Transfer & handover

```text
  Why                         : Safe movement; information
  Trainee action              : Escort by stability; SBAR
  What the trainee sees       : Trolley, monitor
  Information                 : Handover
  Interpretation              : Complete?
  Decision                    : Safe?
  Correct action              : Accepted
  Incorrect action            : Omissions
  Missed abnormality          : Questions/stop
  Escalation                  : Anaesthetist
  Consequence                 : Sign In
  Next step                   : Sign In

Scenario maps: S1 stages — (not a separate S1 map stage); S2 stages — (not a separate S2 map stage).

  [TABLE] What-if | Branch | Correct action
  * WI-46
      Branch: What if the patient deteriorates during transfer?
      Correct action: Stop, oxygen, call for help; return or proceed per stability.
  * WI-47
      Branch: What if there's an identity mismatch at theatre?
      Correct action: STOP; resolve with ward/admissions.
```


### Implementation (FYP design)

```text
  Real workflow               : Several checks at reception.
  FYP simulation              : Trainee escorts and does SBAR handover form (P0) / speech (P1).
  FYP simplification          : Reception check merged into Sign In prompt.
  AI                          : P1 PROPOSED: speech handover → ASR → classifier maps utterances to SBAR
                                elements; trainee confirms on tablet; critical elements never scored from AI
                                alone.

  Room                        : Corridor + holding bay.
  Patient                     : On trolley.
  Staff                       : Porter, anaesthetist, theatre nurse NPCs.
  Equipment                   : Trolley, portable monitor (if needed), documents bundle.
  Available interactions      : Accompany, handover sections.

USER MUST:

  1. Receive theatre call; select 'Send for transfer' with monitoring option.
  2. Accompany patient.
  3. Open handover form: fill S, B, A, R by selecting facts from Known panel.
  4. Deliver (P1 speak).
  5. Choose escort by NEWS2 (Type D/C); transfer with documents; reception nurse checks; give SBAR (structured
     choices or speech).

SYSTEM THEN:

  1. Transport state machine.
  2. Handover completeness/accuracy vs required elements; critical omissions flagged.
  3. Anaesthetist NPC asks follow-up if omission (practice).
  4. Anaesthetist prompts for missing items; omissions scored.
  5. S2 handover MUST include aspiration risk, antibiotic name+time, fluids, latest NEWS2, lactate, kidney
     status, pregnancy status, scan result.
  6. Corridor deterioration ignored → arrest branch.

  If correct →                : Complete accurate handover.
  If incorrect →              : Omission of critical element → CRITICAL.
  If information missing →    : Documents missing → delay.
  If safety condition fails → : Transfer without consent/checklist → blocked/critical.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L12-01
      Trigger: request_transport(OT)
      Condition: ward checklist signed
      Check: stability
      Correct: IN_TRANSIT
      Incorrect: blocked if not signed
      Feedback: —
      Animation: Bed moves
      Next state: —
  * L12-02
      Trigger: handover(section)
      Condition: at holding
      Check: elements
      Correct: completeness score
      Incorrect: critical omission
      Feedback: —
      Animation: Anaesthetist listens
      Next state: ST12

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E12-A
      What went wrong: Allergy omitted
      User sees: Anaesthetist nods
      Should notice: Omission
      Correct response: Include allergy
      System does: Critical
      Progression: Allowed (assessment) / prompted (practice)
      Feedback: Debrief
      State: —
      Resolution: —
  * E12-B
      What went wrong: Fasting uncertainty omitted (S2)
      User sees: —
      Should notice: Aspiration risk
      Correct response: Include
      System does: Critical
      Progression: —
      Feedback: —
      State: —
      Resolution: —

  [TABLE] Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response
  * EV12-1 E-THEATRE-CALLS
      Trigger: Scheduled/probabilistic
      Patient: —
      Monitor: —
      Visual/audio: Phone
      Recognise: Time pressure
      Response: State outstanding items + ETA; don't skip critical steps
      If no response: Critical if transfer without consent/handover

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Trolley / bed
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Positioning cue
      Failure scenario: —
  * Vital-sign monitor
      Use S1/S2: ✓/✓
      Steps: 3, 8, 12–14
      Information provided: SpO2, pulse, NIBP/MAP, ECG, alarms
      Failure scenario: Artefact if probe poor; alarm limits C
  * O2 supply, nasal cannula, simple + non-rebreather masks
      Use S1/S2: cond./cond.
      Steps: 8, 12
      Information provided: Device + flow
      Failure scenario: Over-oxygenating CO2 retainer
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Patient notes / folder
      Use S1/S2: ✓/✓
      Steps: 1–12
      Information provided: Cue
      Failure scenario: Missing documents block transfer
  * Transfer trolley + portable monitor + O2 cylinder
      Use S1/S2: ✓/✓
      Steps: 12
      Information provided: Cue
      Failure scenario: Corridor deterioration
  * SBAR handover sheet
      Use S1/S2: ✓/✓
      Steps: 12
      Information provided: Cue
      Failure scenario: Omissions
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency

Owners: Ruhan — Handover scoring + P1 classifier service. Sannan — Corridor transfer, holding bay,
anaesthetist NPC. Moiz — Handover element lists S1/S2; rubric; [CLINICAL VALIDATION REQUIRED]

Validation: Required handover elements and critical omissions per scenario. [CLINICAL VALIDATION REQUIRED] ·
Transfer monitoring rule. [CLINICAL VALIDATION REQUIRED] · Hospital transfer policy; required handover items.
```


## 18. STEP 13 — WHO SIGN IN (BEFORE THE PATIENT GOES TO SLEEP)

```text
Priority P0 · walkthrough p.99 · build guides: B-R15, B-S10, B-R4
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : A spoken checklist by the nurse and anaesthetist (with the patient awake)
                                before anaesthesia starts — see Part 15.
  Why are we doing it?        : Last chance to catch errors while the patient can still answer.
  What does the trainee actually do?: 1. Patient confirms identity, site, procedure, consent. 2. Site marked
                                or 'not applicable'. 3. Anaesthesia machine and medication check complete. 4.
                                Oximeter on and working. 5. Known allergy? 6. Difficult airway or aspiration
                                risk? (equipment/help ready) 7. Risk of more than 500 mL blood loss? (two
                                drips/fluids planned)
  What does the trainee see?  : Anaesthetic room; checklist board; anaesthetist and nurse.
  What information do they receive?: Anaesthetist: "Is there a risk of more than 500 mL blood loss?"
  What does that information mean?: Every item needs an answer; 'no' or doubt = stop.
  What is the normal/reference value?: All confirmed.
  What counts as abnormal?    : Any discrepancy.
  What counts as seriously concerning?: Identity/consent doubt; oximeter not working.
  What happens if the trainee finds nothing wrong?: Anaesthesia (not simulated) → STEP 14.
  What happens if the trainee finds something abnormal?: Stop and resolve.
  When do we call the anaesthetist?: Leads Sign In.
  When might surgery be delayed?: Until resolved.
  When should emergency surgery continue despite the abnormality?: S2: aspiration risk answered YES and
                                planned for — not a reason to stop.
  What happens if the trainee misses it?: Team member challenges; repeated skip = case stops.
  What happens after the correct action?: Patient anaesthetised; move to Time Out.
  Where this comes from       : WHO Surgical Safety Checklist 2009.
  Clinician validation        : Theatre governance: local additions.
```


### What the player sees (WT Part 16 — Stages 13–14)

```text
  On screen / in front of the player: Anaesthetic room/theatre; checklist board; team.
  Patient — what they say     : Patient answers Sign In identity questions.
  Monitor — numbers           : Live monitor.
  Interaction                 : Lead or answer each item; direct question to right person.
  Information revealed        : Each item lights green when confirmed.
  Decision                    : All safe?
  Correct action →            : 'READY FOR INCISION' — case complete.
  Incorrect action →          : Skipped item → challenge; repeated → case stops.
  Missed abnormality →        : Antibiotic timing missed → debrief/stop.
  Escalation →                : —
```


#### VR reasoning card C.20 — WHO Sign In

```text
  Why                         : Pre-induction safety
  Trainee action              : Answer/lead WHO items
  What the trainee sees       : Board
  Information                 : Items
  Interpretation              : All satisfied?
  Decision                    : Proceed?
  Correct action              : Induction
  Incorrect action            : Skipped item
  Missed abnormality          : Challenge/stop
  Escalation                  : Team
  Consequence                 : Induction
  Next step                   : Theatre prep

Scenario maps: S1 stages 11 WHO Sign In; S2 stages 11 WHO Sign In.

  What-if | Branch                                           | Correct action                     
  --------+--------------------------------------------------+------------------------------------
  WI-47   | What if there's an identity mismatch at theatre? | STOP; resolve with ward/admissions.
```


### Implementation (FYP design)

```text
  Real workflow               : ~1–2 min conversation.
  FYP simulation              : Trainee leads; NPCs and patient answer from state.
  FYP simplification          : Induction itself not simulated.
  AI                          : None (deterministic).

  Room                        : Anaesthetic room/theatre.
  Patient                     : Awake on trolley/table.
  Staff                       : Anaesthetist, nurse NPCs.
  Equipment                   : Checklist board/tablet, monitor with pulse oximeter.
  Available interactions      : Read item, choose response, act on discrepancy.

USER MUST:

  1. Start Sign In.
  2. For each item, ask/confirm; select response.
  3. Attach pulse oximeter if not on (monitor must show SpO2).
  4. Resolve any discrepancy.
  5. Complete Sign In.
  6. Lead or answer each WHO 2009 Sign In item; direct each question to the right person.

SYSTEM THEN:

  1. Expected response derived from state.
  2. Contradiction → critical + stop.
  3. Pulse-ox item requires sensor.attached.
  4. Completion unlocks Time Out.
  5. Each item lights green when confirmed; 'no' or doubt = STOP.
  6. Skipped item → team challenge; repeated skip → case stops.
  7. ProcSense educational extensions shown separately, never as WHO items.

  If correct →                : Sign In complete.
  If incorrect →              : False 'yes' → critical.
  If information missing →    : Unanswered item blocks completion.
  If safety condition fails → : Induction (and Time Out) blocked until complete.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L13-01
      Trigger: checklist_item(SI,i,resp)
      Condition: —
      Check: resp == expected(state)
      Correct: item ok
      Incorrect: critical
      Feedback: —
      Animation: Nurse ticks board
      Next state: —
  * L13-02
      Trigger: complete(SI)
      Condition: all ok
      Check: —
      Correct: SIGN_IN_DONE
      Incorrect: blocked
      Feedback: —
      Animation: —
      Next state: ST13

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E13-A
      What went wrong: Says 'no known allergy' when allergy known
      User sees: Anaesthetist accepts
      Should notice: Contradiction
      Correct response: Correct it
      System does: Critical
      Progression: Stopped
      Feedback: Red
      State: —
      Resolution: —
  * E13-B
      What went wrong: Pulse oximeter not attached
      User sees: Monitor blank
      Should notice: Item can't be yes
      Correct response: Attach
      System does: Blocked
      Progression: Blocked
      Feedback: —
      State: —
      Resolution: —

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Allergy band
      Use S1/S2: ✓/✓
      Steps: 1, 7, 13
      Information provided: Allergy flag (details by asking)
      Failure scenario: Missing band → reaction branch
  * Vital-sign monitor
      Use S1/S2: ✓/✓
      Steps: 3, 8, 12–14
      Information provided: SpO2, pulse, NIBP/MAP, ECG, alarms
      Failure scenario: Artefact if probe poor; alarm limits C
  * Pulse oximeter probe
      Use S1/S2: ✓/✓
      Steps: 3, 13
      Information provided: SpO2, pulse, pleth quality
      Failure scenario: No waveform → recheck; Sign In requires working
  * Suction
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: Cue
      Failure scenario: Not working → RSI unsafe
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Anaesthesia machine + ventilator + check log
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: Check done/not; settings
      Failure scenario: Check not done → Sign In fails
  * Capnograph
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: EtCO2 waveform (post-induction abstracted)
      Failure scenario: —
  * Airway kit + difficult-airway trolley
      Use S1/S2: ✓/✓
      Steps: 11, 13
      Information provided: Cue
      Failure scenario: Difficult airway not declared
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency
  * WHO checklist board
      Use S1/S2: ✓/✓
      Steps: 13–15
      Information provided: Item status pending/confirmed
      Failure scenario: Skipped item → stop

Owners: Ruhan — SafetyChecklist engine with state-derived expectations. Sannan — Checklist board + nurse
ticking. Moiz — Paraphrased items + local additions; [CLINICAL VALIDATION REQUIRED]

Validation: Local Sign In additions. [CLINICAL VALIDATION REQUIRED] · Theatre governance: local additions.
```


## 19. STEP 14 — WHO TIME OUT (BEFORE THE FIRST CUT) — THE FINISH LINE

```text
Priority P0 · walkthrough p.101 · build guides: B-R15, B-S10
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : The whole team pauses, introduces itself and confirms everything before the
                                first incision — see Part 15.
  Why are we doing it?        : Final protection against wrong patient/procedure and missed preparations
                                (antibiotics, imaging, equipment).
  What does the trainee actually do?: 1. Everyone states name and role. 2. Confirm the patient's name,
                                procedure, and where the incision will be made. 3. Antibiotic prophylaxis
                                given within the last 60 minutes? (or not applicable) 4. Surgeon:
                                critical/non-routine steps, expected duration, anticipated blood loss. 5.
                                Anaesthetist: patient-specific concerns. 6. Nurse: sterility (including
                                indicator results) confirmed; equipment issues or concerns. 7. Essential
                                imaging displayed?
  What does the trainee see?  : Theatre; whole team; checklist board; screens showing scans.
  What information do they receive?: e.g. Surgeon: "Expected duration 60 minutes, minimal blood loss." Nurse:
                                "Sterility indicators confirmed."
  What does that information mean?: All aligned → safe to start.
  What is the normal/reference value?: All confirmed.
  What counts as abnormal?    : Antibiotic needed but not given or given more than 60 minutes ago; imaging
                                missing; equipment fault.
  What happens if the trainee finds nothing wrong?: READY FOR INCISION — the scenario is complete.
  What happens if the trainee finds something abnormal?: Fix (give antibiotic, display imaging, fix
                                equipment), then repeat item.
  When do we call the senior doctor?: Surgeon is present.
  When might surgery be delayed?: Until fixed.
  When should emergency surgery continue despite the abnormality?: S2: urgency doesn't skip Time Out.
  What happens if the trainee misses it?: Antibiotic timing missed → debrief/stop per setting.
  What happens after the correct action?: Optional time-lapse → Sign Out → debrief.
  Where this comes from       : WHO SSC 2009; NICE NG125 antibiotic timing.
  Clinician validation        : Antibiotic rule per scenario.
```


### What the player sees (WT Part 16 — Stages 13–14)

```text
  On screen / in front of the player: Anaesthetic room/theatre; checklist board; team.
  Patient — what they say     : Patient answers Sign In identity questions.
  Monitor — numbers           : Live monitor.
  Interaction                 : Lead or answer each item; direct question to right person.
  Information revealed        : Each item lights green when confirmed.
  Decision                    : All safe?
  Correct action →            : 'READY FOR INCISION' — case complete.
  Incorrect action →          : Skipped item → challenge; repeated → case stops.
  Missed abnormality →        : Antibiotic timing missed → debrief/stop.
  Escalation →                : —
```


#### VR reasoning card C.21 — WHO Time Out

```text
  Why                         : Pre-incision safety
  Trainee action              : WHO items in order
  What the trainee sees       : Board, team
  Information                 : Items
  Interpretation              : All satisfied?
  Decision                    : Ready?
  Correct action              : Ready for incision
  Incorrect action            : Antibiotic timing missed
  Missed abnormality          : Stop/debrief
  Escalation                  : Surgeon
  Consequence                 : Procedure start
  Next step                   : Procedure

Scenario maps: S1 stages 13 WHO Time Out, 14 Procedure start; S2 stages 13 WHO Time Out, 14 Procedure start.
```


### Implementation (FYP design)

```text
  Real workflow               : Team pause.
  FYP simulation              : Trainee leads/participates; NPC team answers.
  FYP simplification          : Anaesthesia/sterile field abstract.
  AI                          : None.

  Room                        : OR with team at positions.
  Patient                     : On table, draped (abstract).
  Staff                       : Surgeon, anaesthetist, scrub, circulating.
  Equipment                   : Checklist board, imaging screen.
  Available interactions      : Lead items.

USER MUST:

  1. Start Time Out.
  2. Prompt introductions.
  3. Confirm patient/site/procedure.
  4. Ask each discipline for concerns.
  5. Confirm antibiotic timing and imaging.
  6. Complete.
  7. WHO 2009 Time Out items in order; confirm antibiotic within 60 min or N/A; imaging displayed; sterility
     indicators.

SYSTEM THEN:

  1. Checks vs state (antibiotic time from treatments log).
  2. Blocks procedure until complete & ready.
  3. Gap → fix then repeat item; S2 urgency never skips Time Out.
  4. All confirmed → READY FOR INCISION (end state); optional time-lapse → Sign Out.

  If correct →                : Time Out done → procedure starts.
  If incorrect →              : Wrong confirmation → critical.
  If information missing →    : Item unanswered blocks.
  If safety condition fails → : Equipment not ready or discrepancy → blocked.

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L14-01
      Trigger: checklist_item(TO,i,resp)
      Condition: SI done
      Check: expected
      Correct: ok
      Incorrect: critical
      Feedback: —
      Animation: Team speaks
      Next state: —
  * L14-02
      Trigger: complete(TO)
      Condition: ok & OR_READY
      Check: —
      Correct: PROCEDURE
      Incorrect: blocked
      Feedback: —
      Animation: Lights on
      Next state: PROC

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E14-A
      What went wrong: Antibiotic 'yes' when not given
      User sees: —
      Should notice: Contradiction
      Correct response: Correct/decide
      System does: Critical
      Progression: Stopped
      Feedback: —
      State: —
      Resolution: —

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * Vital-sign monitor
      Use S1/S2: ✓/✓
      Steps: 3, 8, 12–14
      Information provided: SpO2, pulse, NIBP/MAP, ECG, alarms
      Failure scenario: Artefact if probe poor; alarm limits C
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Surgical lights
      Use S1/S2: ✓/✓
      Steps: 11, 14
      Information provided: Cue
      Failure scenario: —
  * Laparoscopic tower (camera, light, monitor, insufflator)
      Use S1/S2: ✓/✓
      Steps: 11, 14
      Information provided: Image, insufflation pressure/flow, fault messages
      Failure scenario: Fault → delay
  * Instrument trays (lap chole / lap + open appendix)
      Use S1/S2: ✓/✓
      Steps: 11, 14
      Information provided: Sterility indicators
      Failure scenario: Indicator fail → stop
  * Drapes
      Use S1/S2: ✓/✓
      Steps: 14
      Information provided: Cue
      Failure scenario: —
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency
  * WHO checklist board
      Use S1/S2: ✓/✓
      Steps: 13–15
      Information provided: Item status pending/confirmed
      Failure scenario: Skipped item → stop

Owners: Ruhan — Time Out logic + procedure phase transition. Sannan — Team positions, intro lines, time-lapse.
Moiz — Items + antibiotic timing rule; [CLINICAL VALIDATION REQUIRED]

Validation: Antibiotic timing rule for each scenario. [CLINICAL VALIDATION REQUIRED] · Antibiotic rule per
scenario.
```


## 20. STEP 15 — WHO SIGN OUT AND HANDOVER TO RECOVERY (OPTIONAL)

```text
Priority P1 · walkthrough p.102 · build guides: B-R15, B-M15, B-R9, B-S12
```


### Walkthrough specification (verbatim — WT Part 11)

```text
  What are we doing?          : After the operation, before the patient leaves theatre: confirm the procedure
                                name, counts of instruments/swabs/needles, specimen label, equipment problems,
                                and key concerns for recovery.
  Why are we doing it?        : Prevents retained instruments, lost specimens and poor handover.
  What does the trainee actually do?: 1. Nurse confirms procedure name recorded. 2. Counts correct. 3.
                                Specimen (gallbladder/appendix) labelled — label read aloud including patient
                                name. 4. Equipment problems recorded. 5. Team reviews key concerns for
                                recovery; hand over to recovery nurse.
  What happens if the trainee finds nothing wrong?: Case ends; debrief.
  What happens if the trainee finds something abnormal?: Count wrong → search, X-ray per local, escalate.
  Where this comes from       : WHO SSC 2009.
  Clinician validation        : Recovery handover items.
```


#### VR reasoning card C.22 — Intra-operative event — ProcSense educational/simulation extension — clinician validation required.

```text
  Why                         : e.g. conversion to open, bleeding needing suction, normal appendix found
                                (surgeon's decisions; the trainee observes and communicates)
  Trainee action              : Observe; respond to team requests
  What the trainee sees       : Theatre
  Information                 : Event
  Interpretation              : Team management
  Decision                    : Communicate?
  Correct action              : Smooth teamwork
  Incorrect action            : Freezing/no communication
  Missed abnormality          : Debrief
  Escalation                  : Surgeon/anaesthetist
  Consequence                 : Plan update
  Next step                   : Sign Out
```


#### VR reasoning card C.23 — WHO Sign Out

```text
  Why                         : Counts, specimen, concerns
  Trainee action              : Participate
  What the trainee sees       : Board
  Information                 : Items
  Interpretation              : Complete?
  Decision                    : Leave OR?
  Correct action              : Handover
  Incorrect action            : Specimen not read aloud
  Missed abnormality          : Lost specimen
  Escalation                  : Team
  Consequence                 : Recovery
  Next step                   : Recovery
```


#### VR reasoning card C.24 — Recovery handover & debrief

```text
  Why                         : Continuity of care; learning
  Trainee action              : Hand over key concerns
  What the trainee sees       : Recovery bay
  Information                 : Summary
  Interpretation              : —
  Decision                    : —
  Correct action              : Case closes
  Incorrect action            : Omits antibiotic plan
  Missed abnormality          : Debrief
  Escalation                  : Recovery nurse
  Consequence                 : End
  Next step                   : Debrief

Scenario maps: S1 stages 15 Relevant intra-operative training events (ProcSense extension), 16 WHO Sign Out,
17 Recovery / handover; S2 stages 15 Relevant intra-operative training events (ProcSense extension), 16 WHO
Sign Out, 17 Recovery / handover.
```


### Implementation (FYP design)

```text
  Real workflow               : —
  FYP simulation              : Abstract.
  FYP simplification          : No recovery care.
  AI                          : P1: debrief rewrite at case end (validated).

  Room                        : Theatre → recovery zone.
  Patient                     : Anaesthetised → recovering (abstract).
  Staff                       : Team + recovery nurse.
  Equipment                   : Specimen pot, count sheet, board.
  Available interactions      : Items.

USER MUST:

  1. Complete Sign Out items.
  2. Label specimen.
  3. Handover to recovery.
  4. Sign Out: procedure name, counts, specimen label read aloud incl. patient name, equipment problems,
     recovery concerns; SBAR to recovery nurse.
  5. P1 extension: observe/communicate during intra-operative training events (conversion, bleeding needing
     suction, unexpected findings) — surgeon approves events.

SYSTEM THEN:

  1. Checks counts/specimen/equipment log.
  2. Count discrepancy → search, X-ray per local, escalate.
  3. Specimen not read aloud → lost-specimen debrief; omitted antibiotic plan at recovery → debrief.

  If correct →                : Done.
  If incorrect →              : Unlabelled specimen → major.
  If information missing →    : —
  If safety condition fails → : —

  [TABLE] ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state
  * L15-01
      Trigger: checklist_item(SO)
      Condition: procedure done
      Check: expected
      Correct: ok
      Incorrect: item
      Feedback: —
      Animation: —
      Next state: END

  [TABLE] ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution
  * E15-A
      What went wrong: Equipment fault not reported
      User sees: —
      Should notice: From Step 11
      Correct response: Report
      System does: Minor
      Progression: —
      Feedback: —
      State: —
      Resolution: —

  [TABLE] Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario
  * Patient wristband
      Use S1/S2: ✓/✓
      Steps: 1, all
      Information provided: Name, DOB, MR number (readable)
      Failure scenario: Mismatch → stop
  * PPE
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: —
  * Specimen container + label
      Use S1/S2: ✓/✓
      Steps: 15
      Information provided: Label read aloud
      Failure scenario: Mislabelled specimen
  * Resus trolley + defibrillator + emergency drugs
      Use S1/S2: ✓/✓
      Steps: All
      Information provided: Cue
      Failure scenario: Not checked → delay in emergency
  * WHO checklist board
      Use S1/S2: ✓/✓
      Steps: 13–15
      Information provided: Item status pending/confirmed
      Failure scenario: Skipped item → stop
  * Recovery bay equipment
      Use S1/S2: ✓/✓
      Steps: 15
      Information provided: Cue
      Failure scenario: —

Owners: Ruhan — Sign Out + debrief trigger. Sannan — Recovery zone (fade ok). Moiz — Items; [CLINICAL
VALIDATION REQUIRED]

Validation: Recovery handover elements. [CLINICAL VALIDATION REQUIRED] · Recovery handover items.
```


# SCENARIOS, BRANCHES AND NUMBERS


## 21. PATIENT & CASE LIBRARY

```text
Namespaced IDs (WT-I01): VAR- = Parts 12–13 variants, STK- = Part D full stacked cases, SC- = 14B compact.

  [TABLE] ID | WT label | Patient | Scen. | Key findings | Expected end state | WT ref | Priority
  * VAR-S1-A
      WT label: Example Patient A / S1-A
      Patient: Mrs Ayesha Khan, 42
      Scen.: S1
      Key findings: Uncomplicated; all normal; water 06:30 teaching point; NEWS2 0; ASA 1
      Expected end state: READY_SAFE
      WT ref: Part 12 p.103–105
      Priority: P0
  * VAR-S1-PB
      WT label: Example Patient B
      Patient: Mrs Farzana Ahmed, 61
      Scen.: S1
      Key findings: Jaundice; bili 72, ALP 380, GGT 410, ALT 160; CBD 9 mm; ASGE high
      Expected end state: POSTPONED (ERCP pathway)
      WT ref: Part 12 p.105–106
      Priority: P0
  * VAR-S1-E
      WT label: S1-E
      Patient: Mrs Rabia, 48
      Scen.: S1
      Key findings: T 38.4, HR 108, Murphy's +, WBC 15.2, CRP 96, wall 6 mm + fluid → TG18 Grade I
      Expected end state: REDIRECTED (acute pathway)
      WT ref: Part 12 p.106
      Priority: P0
  * VAR-S1-B
      WT label: S1-B (+B2)
      Patient: Mr Tariq, 58
      Scen.: S1
      Key findings: BP 172/104 → 168/102 proceed; B2 188/114, 186/112 postpone
      Expected end state: READY_SAFE / POSTPONED
      WT ref: Part 12 p.106–107
      Priority: P0
  * VAR-S1-C
      WT label: S1-C (+C2)
      Patient: Mrs Nasreen, 55
      Scen.: S1
      Key findings: T2DM metformin+gliclazide; HbA1c 58, CBG 7.8 proceed; C2 HbA1c 82, CBG 13.4, ketones 0.4
          postpone
      Expected end state: READY_SAFE / POSTPONED
      WT ref: Part 12 p.107
      Priority: P0
  * VAR-S1-D
      WT label: S1-D (a/b)
      Patient: Mr Imran, 67
      Scen.: S1
      Key findings: AF on apixaban, eGFR 68; (a) 3 days ago proceed; (b) this morning defer
      Expected end state: READY_SAFE / POSTPONED
      WT ref: Part 12 p.107
      Priority: P0
  * VAR-S1-G
      WT label: S1-G
      Patient: Ms Sana, 39
      Scen.: S1
      Key findings: Toast + milky tea 07:00, surgery 09:00 → not safe until 13:00
      Expected end state: Re-timed / POSTPONED
      WT ref: Part 12 p.107
      Priority: P0
  * VAR-S1-H
      WT label: S1-H
      Patient: Ms Hina, 29
      Scen.: S1
      Key findings: LMP 7 weeks; UPT positive
      Expected end state: POSTPONED
      WT ref: Part 12 p.107
      Priority: P0
  * VAR-S1-J
      WT label: S1-J
      Patient: Mr Kamran, 50
      Scen.: S1
      Key findings: K 6.4 haemolysed, ECG normal → repeat 4.6
      Expected end state: READY_SAFE
      WT ref: Part 12 p.107–108
      Priority: P0
  * VAR-S1-K
      WT label: S1-K
      Patient: Mrs Shazia, 52
      Scen.: S1
      Key findings: Hb 98, MCV 72
      Expected end state: POSTPONED (investigate)
      WT ref: Part 12 p.108
      Priority: P1
  * VAR-S1-L
      WT label: S1-L
      Patient: Mr Aslam, 70
      Scen.: S1
      Key findings: New AF HR 128, BP 132/84
      Expected end state: POSTPONED (medical)
      WT ref: Part 12 p.108
      Priority: P1
  * VAR-S1-M
      WT label: S1-M
      Patient: Mr Rizwan, 44
      Scen.: S1
      Key findings: BMI 41, STOP-BANG 6, SpO2 95 → ASA 3
      Expected end state: Anaesthetist decides
      WT ref: Part 12 p.108
      Priority: P1
  * VAR-S1-I
      WT label: S1-I
      Patient: Mrs Amna, 45
      Scen.: S1
      Key findings: Penicillin anaphylaxis 2015
      Expected end state: READY_SAFE (allergy flagged)
      WT ref: Part 12 p.108
      Priority: P1
  * VAR-S2-PC
      WT label: Example Patient C
      Patient: Mr Hamza Ali, 19 (DOB 02/06/2007)
      Scen.: S2
      Key findings: Classic; HR 108→96, T 37.9, WBC 13.6, neut 82, CRP 38; AIR 7 / AAS 19; US positive simple
      Expected end state: READY_SAFE
      WT ref: Part 13 p.109–111
      Priority: P0
  * VAR-S2-PD
      WT label: Example Patient D
      Patient: Mr Usman, 34
      Scen.: S2
      Key findings: Deterioration timeline 14:00→15:45 (NEWS2 2→5→10→16); perforation
      Expected end state: READY_SAFE if acted at 14:45 / SAFETY_FAILURE
      WT ref: Part 13 p.111–112
      Priority: P0
  * VAR-S2-B
      WT label: S2-B
      Patient: Ms Sadia, 24
      Scen.: S2
      Key findings: RLQ + shoulder-tip pain, HR 112, BP 98/60, UPT +, Hb 104 → ectopic differential
      Expected end state: REDIRECTED (gynaecology)
      WT ref: Part 13 p.112
      Priority: P0
  * VAR-S2-D
      WT label: S2-D
      Patient: Ms Zainab, 28
      Scen.: S2
      Key findings: AIR 5; US 'appendix not visualised'
      Expected end state: Senior: CT or re-score 6–12 h
      WT ref: Part 13 p.112
      Priority: P0
  * VAR-S2-E
      WT label: S2-E
      Patient: Mr Akbar, 68
      Scen.: S2
      Key findings: Weight loss, Hb 112, CRP 64 → CT first
      Expected end state: Pathway by CT
      WT ref: Part 13 p.112
      Priority: P1
  * VAR-S2-F
      WT label: S2-F
      Patient: Mr Bilal, 22
      Scen.: S2
      Key findings: Biryani 2 h ago → RSI, not cancelled
      Expected end state: READY_SAFE
      WT ref: Part 13 p.112
      Priority: P0
  * VAR-S2-G
      WT label: S2-G
      Patient: Mrs Rukhsana, 45
      Scen.: S2
      Key findings: Vomiting 2 days, HR 116, BP 104/66, urea 12.4, creat 160 (usual 85) AKI 1
      Expected end state: READY_SAFE after resuscitation
      WT ref: Part 13 p.113
      Priority: P0
  * VAR-S2-H
      WT label: S2-H
      Patient: Mrs Mehwish, 30 (20 weeks)
      Scen.: S2
      Key findings: Pregnant; maternity EWS; US then MRI; left tilt
      Expected end state: READY_SAFE (obstetric support)
      WT ref: Part 13 p.113
      Priority: P1
  * VAR-S2-I
      WT label: S2-I
      Patient: Hamid, 16
      Scen.: S2
      Key findings: Minor consent per law
      Expected end state: READY_SAFE (valid consent)
      WT ref: Part 13 p.113
      Priority: P1
  * VAR-S2-J
      WT label: S2-J
      Patient: Ms Aqsa, 26
      Scen.: S2
      Key findings: Dysuria; leucocytes 2+, nitrite −; don't stop at UTI
      Expected end state: Continue appendicitis assessment
      WT ref: Part 13 p.113
      Priority: P1
  * VAR-S2-K
      WT label: S2-K
      Patient: Mr Faisal, 21
      Scen.: S2
      Key findings: Childhood rash to penicillin
      Expected end state: READY_SAFE (hospital rule)
      WT ref: Part 13 p.113
      Priority: P1
  * VAR-S2-L
      WT label: S2-L
      Patient: Mr Naveed, 37
      Scen.: S2
      Key findings: Capacitous refusal; antibiotics-only option
      Expected end state: Respect decision; safety-net
      WT ref: Part 13 p.113
      Priority: P1
  * STK-S1-A
      WT label: STACKED CASE S1-A
      Scen.: S1
      Key findings: Diabetes medication error: hypoglycaemia +
      Expected end state: see case §13
      WT ref: Part D p.163
      Priority: P1
  * STK-S1-B
      WT label: STACKED CASE S1-B
      Scen.: S1
      Key findings: Anticoagulated AF + iron-deficiency anaemia + anticoagulant taken this morning
      Expected end state: see case §13
      WT ref: Part D p.168
      Priority: P1
  * STK-S1-C
      WT label: STACKED CASE S1-C
      Scen.: S1
      Key findings: True hyperkalaemia with ECG changes + acute kidney injury + bradycardia/low BP (heart-
          failure medicines)
      Expected end state: see case §13
      WT ref: Part D p.173
      Priority: P1
  * STK-S1-D
      WT label: STACKED CASE S1-D
      Scen.: S1
      Key findings: Patient awaiting elective cholecystectomy who develops acute cholangitis
      Expected end state: see case §13
      WT ref: Part D p.177
      Priority: P0
  * STK-S2-A
      WT label: STACKED CASE S2-A
      Scen.: S2
      Key findings: Appendicitis + dehydration/tachycardia + acute kidney injury + low potassium
      Expected end state: see case §13
      WT ref: Part D p.181
      Priority: P1
  * STK-S2-B
      WT label: STACKED CASE S2-B
      Scen.: S2
      Key findings: Perforated appendicitis + high-risk sepsis + penicillin anaphylaxis
      Expected end state: see case §13
      WT ref: Part D p.186
      Priority: P0
  * STK-S2-C
      WT label: STACKED CASE S2-C
      Scen.: S2
      Key findings: Opioid sedation + vomiting + unknown/recent food → airway and aspiration risk with falling
          SpO2
      Expected end state: see case §13
      WT ref: Part D p.190
      Priority: P1
  * STK-S2-D
      WT label: STACKED CASE S2-D
      Scen.: S2
      Key findings: Appendicitis in pregnancy (18 weeks) + physiology that masks severity + imaging limits
      Expected end state: see case §13
      WT ref: Part D p.193
      Priority: P1

  [TABLE] ID | Scen. | Title | Priority
  * SC-1
      Scen.: S2
      Title: Sepsis + penicillin anaphylaxis + AKI
      Priority: P2
  * SC-2
      Scen.: S2
      Title: Positive pregnancy test + lower-right pain + low BP
      Priority: P2
  * SC-3
      Scen.: S2
      Title: Pregnant (20 weeks) + appendicitis + fever
      Priority: P2
  * SC-4
      Scen.: S1
      Title: High BP + poorly controlled diabetes
      Priority: P2
  * SC-5
      Scen.: S1
      Title: On apixaban (taken this morning) + new fast AF
      Priority: P2
  * SC-6
      Scen.: S1
      Title: Fasting breach + diabetes on insulin
      Priority: P2
  * SC-7
      Scen.: S2
      Title: Unknown fasting + vomiting + opioid given + sleepy
      Priority: P2
  * SC-8
      Scen.: S2
      Title: High potassium + AKI + sepsis
      Priority: P2
  * SC-9
      Scen.: S1
      Title: Jaundice + fever + shaking (cholangitis) + on clopidogrel
      Priority: P2
  * SC-10
      Scen.: S2
      Title: Language barrier + 17 years old + needs urgent surgery
      Priority: P2
  * SC-11
      Scen.: S2
      Title: Refuses surgery + becomes septic later
      Priority: P2
  * SC-12
      Scen.: S1
      Title: Haemolysed high potassium + genuinely low sodium + water tablets
      Priority: P2
  * SC-13
      Scen.: S1
      Title: Anaemia + anticoagulant + needs G&S
      Priority: P2
  * SC-14
      Scen.: S2
      Title: Equivocal ultrasound + deteriorating vital signs
      Priority: P2
  * SC-15
      Scen.: S2
      Title: Anaphylaxis to antibiotic + still needs antibiotics
      Priority: P2
  * SC-16
      Scen.: S1
      Title: GLP-1 injection + nausea + high BMI + sleep apnoea
      Priority: P2
  * SC-17
      Scen.: S2
      Title: Sickle cell trait/thalassaemia + dehydration
      Priority: P2
  * SC-18
      Scen.: S1
      Title: Missing consent + equipment fault + late list
      Priority: P2
  * SC-19
      Scen.: S2
      Title: Low sugar + confusion + sepsis in a diabetic on SGLT2
      Priority: P2
  * SC-20
      Scen.: S2
      Title: Older patient + appendicitis picture + weight loss + anaemia + on aspirin
      Priority: P2
```


## 22. STACKED PROBLEMS: METHOD

```text
WT 14B and Part D.

  [TABLE] Ladder | Question | Action
  * 1
      Question: Immediately life-threatening? (A, B, C, low sugar, anaphylaxis, NEWS2 7+)
      Action: Fix FIRST with ABCDE + call for help — everything else waits
  * 2
      Question: Hard safety blocker? (identity doubt, allergy unknown before a drug, invalid consent, positive
          pregnancy test with pain)
      Action: Resolve before any non-emergency treatment or transfer
  * 3
      Question: Time-critical treatment? (sepsis antibiotics ≤1 h, analgesia, fluids for shock, K 6.5+)
      Action: Do it now, in parallel
  * 4
      Question: Finding changes diagnosis/pathway? (ectopic, cholangitis, burst appendix, duct stone)
      Action: Senior/specialist; change pathway
  * 5
      Question: Treatments conflict?
      Action: Choose safest alternative (clash table); ask senior/anaesthetist
  * 6
      Question: S1 with any fixable problem left?
      Action: Senior/anaesthetic review → usually optimise first (defer/re-time/rebook)
  * 7
      Question: Prepare, consent, hand over
      Action: SBAR lists EVERY problem and its plan

  [TABLE] Problem type | Examples | Effect
  * LIFE_THREAT
      Examples: Shock, low O2, anaphylaxis, low sugar, K 6.5+, NEWS2 7+
      Effect: Stops everything; emergency mode
  * SAFETY_BLOCKER
      Examples: Identity doubt, unknown allergy, invalid consent, unknown pregnancy status, unacknowledged
          critical result, incomplete checklist
      Effect: Cannot pass checkpoints until resolved
  * TIME_CRITICAL
      Examples: Sepsis antibiotics, analgesia, fluids for dehydration
      Effect: Must be done within a clock
  * PATHWAY_CHANGING
      Examples: Ectopic, cholangitis, perforation, duct stone, new AF, MI
      Effect: Switches branch/team
  * OPTIMISABLE
      Examples: BP 180/110+, HbA1c >69, anaemia, salts, anticoagulant timing, fasting
      Effect: S1 defer/re-time after review; S2 correct in parallel
  * INFORMATION_ONLY
      Examples: Smoker, mild obesity, controlled AF
      Effect: Recorded and handed over

  [TABLE] Part D priority | Meaning
  * P1
      Meaning: Immediate threat to airway, breathing, circulation (or brain: glucose)
  * P2
      Meaning: Potentially serious physiological abnormality
  * P3
      Meaning: Condition needing optimisation before proceeding
  * P4
      Meaning: Important but non-urgent finding

  - The most dangerous problem sets the pace.
  - In S1, problems add up to 'postpone'; in S2 to 'more preparation in parallel'.
  - A new diagnosis beats the old plan.
  - Every drug: allergy? kidneys/liver? pregnancy? (+ blood thinner for bleeding drugs).
  - Clashes are decided by senior/anaesthetist — the trainee must spot them.
  - Re-check after every intervention.
  - Handover must include ALL problems.

  [TABLE] Clash | Safer approach (draft) | Decided by
  * Fluids needed + heart failure / severe CKD
      Safer approach (draft): Smaller boluses (e.g. 250 mL) with re-check
      Decided by: Senior/anaesthetist (DOSE-PS)
  * Pain relief + AKI
      Safer approach (draft): Paracetamol + titrated opioid (often not morphine); no NSAIDs
      Decided by: DOSE-PS
  * Pain relief + pregnancy
      Safer approach (draft): Paracetamol; opioid per senior; no NSAIDs
      Decided by: DOSE-PS
  * Antibiotic + penicillin allergy
      Safer approach (draft): Hospital allergy-alternative rule
      Decided by: Microbiology/policy (C)
  * CT + pregnancy
      Safer approach (draft): US → MRI; CT only if senior decides
      Decided by: Senior/radiology
  * Contrast CT + AKI
      Safer approach (draft): Fluids first; non-contrast/US alternatives
      Decided by: Senior/radiology
  * Contrast + dye allergy
      Safer approach (draft): Alternative imaging or pre-medication per radiology
      Decided by: Radiology (C)
  * Antiemetic + long QTc
      Safer approach (draft): Alternative antiemetic; fix K/Mg
      Decided by: Anaesthetist
  * Metoclopramide + bowel obstruction
      Safer approach (draft): Different antiemetic; NG tube per senior
      Decided by: DOSE-PS
  * Anticoagulant + urgent surgery
      Safer approach (draft): Reversal by senior/haematology; blood ready
      Decided by: DOSE-PS
  * Antiplatelets after recent stent
      Safer approach (draft): S1 defer; S2 operate on antiplatelets with cardiology
      Decided by: Cardiology + senior
  * Insulin + nil by mouth
      Safer approach (draft): Adjust insulin, CBG hourly, glucose fluid or VRIII
      Decided by: DOSE-PS
  * SGLT2 + sepsis/vomiting
      Safer approach (draft): Ketones even if glucose near normal
      Decided by: Diabetes team
  * Steroid user + sepsis/surgery
      Safer approach (draft): Extra steroid cover
      Decided by: Anaesthetist
  * High K + suxamethonium
      Safer approach (draft): Different relaxant; correct K first
      Decided by: Anaesthetist
  * Full stomach + emergency surgery
      Safer approach (draft): RSI; don't wait unless safe
      Decided by: Anaesthetist + surgeon
  * Low SpO2 + CO2 retainer
      Safer approach (draft): Target 88–92%; blood gas
      Decided by: Senior
  * Confused + consent + emergency
      Safer approach (draft): Treat cause; best-interests/emergency route (C)
      Decided by: Senior
```


### Full stacked cases (Part D) — structure

```text
Each case has 20 sections: introduction; presentation; observations; history (immediately available vs only
revealed if asked); three abnormalities; prioritisation; chronological decision process; interactions;
investigations; results; surgery decision; anaesthesia decision; escalation; correct path; incorrect paths
(with NPC/monitor reaction and recovery); missed path; recovery path; debrief. Full text:
ProcSense_Content/stacked_cases_full.json.

PART D — Stacked-case master table (overview)
Case Procedure Problem 1 Problem 2 Problem 3 Highest priority Investigation Escalation
(senior)
Surgical
decision
Anaesthetic
decision
S1-A Elective
laparoscopic
cholecystectomy
hypoglycaemia (glucose
3.6 mmol/L)
SGLT2 inhibitor taken
today (euglycaemic DKA
risk)
no recent HbA1c →
result 76 mmol/mol
Hypoglycaemia 3.6
mmol/L
Repeat capillary
glucose, Capillary
ketones, HbA1c, U&E
Same call —
propose deferral;
agree
documentation and
patien
Unlikely today:
SGLT2 inhibitor
taken on the
day against UK
2025
consensus
advice, a
hypoglycaemic
episode with
oral glucose
altering fasting,
and HbA1c 76;
the
anaesthetist
and senior
surgeon
decide.
No concerns;
BMI 31 —
STOP-BANG
check.
S1-B Elective
laparoscopic
cholecystectomy
irregular pulse haemoglobin 104 g/L,
MCV 74 fL, ferritin 9
µg/L
apixaban taken 07:00
today
Apixaban active 12-lead ECG, FBC,
Ferritin/iron studies,
U&E / creatinine
clearance
Same — propose
deferral; arrange
anaemia
investigation.
Normally not
today (senior/a
naesthetist
decide).
AF rate
controlled;
anaemia lowers
oxygen-carryin
g reserve.
S1-C Elective
laparoscopic
cholecystectomy
slow heart (52) and BP
lower than usual
potassium 6.6 mmol/L
(not haemolysed) with
peaked T waves
creatinine 178 µmol/L
(baseline 112)
K 6.6 with ECG
changes
12-lead ECG, U&E,
Venous blood gas,
Capillary glucose
Immediately. Not while
hyperkalaemia
with ECG
changes is
untreated.
Hyperkalaemia
+ bradycardia
= high
anaesthetic
risk; suxameth
onium contrain
dicated.
S1-D Original plan:
elective
laparoscopic
cholecystectomy
fever 39.2 °C with
tachycardia
WBC 16.4, CRP 142,
bilirubin 64, ALP 410,
GGT 520, ALT 210; CBD
10 mm
lactate 2.6, creatinine
96 (baseline 70)
High-risk sepsis
(NEWS2 7, lactate
2.6)
Blood cultures, Lactate,
LFTs, Ultrasound
Immediately. The original
elective
operation does
not simply
continue today.
Sepsis; fluids;
possible
vasopressor
need if
unresponsive.
S2-A Emergency
laparoscopic
appendectomy
tachycardia 118 with
relative hypotension
and slow capillary refill
creatinine 158 µmol/L
(GP baseline 82), urea
13.2
potassium 3.0 mmol/L Hypovolaemia FBC, CRP, U&E, Urine
hCG, Lactate
Diagnosis and
timing.
Yes, after
resuscitation.
Correct
hypovolaemia
before
induction.

Case Procedure Problem 1 Problem 2 Problem 3 Highest priority Investigation Escalation
(senior)
Surgical
decision
Anaesthetic
decision
S2-B Emergency
appendectomy
sepsis physiology
(NEWS2 10, SBP 92)
penicillin anaphylaxis
history
lactate 3.8; creatinine
142; CT perforation with
free fluid
Sepsis with
hypoperfusion
Blood cultures, Lactate,
CT, U&E
Immediately. Yes — urgently
after
resuscitation
starts.
Septic shock
risk;
vasopressors
may be
needed.
S2-C Emergency
appendectomy
reduced consciousness
and RR 9
vomiting while drowsy,
recent meal
SpO2 90% Vomiting with
reduced
consciousness
Capillary glucose, Chest
exam ± X-ray, Blood
gas
Informed. Yes, when airw
ay/breathing
stable.
Full stomach →
RSI.
S2-D Emergency
laparoscopic
appendectomy in
pregnancy
tachycardia 108 and
fever
WBC 15.8, CRP 64 ultrasound
non-diagnostic
Maternal stability
(ABC)
Urinalysis/culture,
Ultrasound, MRI, Fetal
assessment
Early. Yes. Increased in
pregnancy →
RSI.
```


## 23. WHAT-IF LIBRARY (WT PART 14)

```text
47 branches; each has 11 fields in whatif_library.csv.

 ABCDE (RCUK 2021): A airway → B breathing → C circulation → D disability (incl. glucose) → E exposure; fix each problem as found.

  [TABLE] ID | Group | What if | Steps | Correct action | Incorrect action | Escalation
  * WI-01
      Group: 14.1 Vital-sign problems
      What if: What if blood pressure is too low? (top number 90 or less)
      Steps: 3, 8
      Correct action: Call for help; lie flat; assess airway/breathing and start oxygen per protocol to the
          patient's target; two drips; request a fluid bolus (e.g. 500 mL crystalloid, NICE CG174; the
          clinician decides volume) and re-check EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE
          PRESCRIPTION ; treat cause (antibiotics if septic; adrenaline if anaphylaxis; blood if bleeding).
      Incorrect action: Giving a BP tablet or opioid; sending to scan unmonitored; waiting for the next
          routine obs
      Escalation: Senior surgeon + anaesthetist immediately; gynaecology if pregnancy positive; critical care
          if not responding.
  * WI-02
      Group: 14.1 Vital-sign problems
      What if: What if blood pressure is extremely high? (180/110 or more)
      Steps: 3, 8
      Correct action: No symptoms: S1 → postpone and refer for control (don't rush BP down). With symptoms →
          emergency medical review.
      Incorrect action: Rapidly lowering BP on the morning of planned surgery; cancelling after one unrepeated
          reading
      Escalation: Anaesthetist; medical team if symptoms.
  * WI-03
      Group: 14.1 Vital-sign problems
      What if: What if the heart rate is very high? (above 130)
      Steps: 3, 8
      Correct action: ABCDE; treat cause (pain relief, fluids, antibiotics); ECG-guided rhythm treatment by
          senior/medical team.
      Incorrect action: Treating the number with a rate-slowing drug before finding the cause (e.g.
          hypovolaemia)
      Escalation: Senior + anaesthetist (NEWS2 3 for this alone = urgent).
  * WI-04
      Group: 14.1 Vital-sign problems
      What if: What if the heart rate is very slow? (40 or below)
      Steps: 3, 8
      Correct action: ABCDE; stop causative drugs; emergency medicines/pacing per resuscitation team.
      Incorrect action: Giving more beta-blocker/opioid; proceeding to anaesthesia
      Escalation: Senior + anaesthetist + medical/cardiology urgently.
  * WI-05
      Group: 14.1 Vital-sign problems
      What if: What if the patient has a fever? (38.0 °C or more)
      Steps: 3, 8
      Correct action: Paracetamol; fluids if dry; antibiotics according to cause; sepsis bundle if criteria
          met.
      Incorrect action: S1: proceeding with the planned operation; treating fever only with paracetamol
          without looking for a source
      Escalation: Senior (in S1 the pathway changes, so yes).
  * WI-06
      Group: 14.1 Vital-sign problems
      What if: What if the patient is cold? (36.0 °C or less)
      Steps: 3, 8
      Correct action: Warm (forced-air blanket); don't send to theatre until 36.0 unless urgent CLINICAL
          REFERENCE — SOURCE BACKED (NICE CG65).
      Incorrect action: Transferring a cold patient for planned surgery
      Escalation: Anaesthetist; senior if septic.
  * WI-07
      Group: 14.1 Vital-sign problems
      What if: What if oxygen saturation is low? (below 94%)
      Steps: 3, 8
      Correct action: Look at the patient and assess airway/breathing; check probe and waveform; if the low
          reading is real, sit up and start oxygen per protocol to the patient's target range (94–98%, or
          88–92% if at risk of hypercapnic failure); find the cause (stop fluids if overloaded per senior;
          stimulate if over-sedated — antidote by clinician); reassess within minutes (process in Part 4.5).
      Incorrect action: Ignoring because 'probe is wrong' without checking; high-flow O2 to a CO2 retainer
          without monitoring
      Escalation: Anaesthetist/senior urgently; emergency team if 88% or below.
  * WI-08
      Group: 14.1 Vital-sign problems
      What if: What if breathing is very fast? (25 or more)
      Steps: 2, 5, 10
      Correct action: Treat cause; sepsis bundle if infection.
      Incorrect action: Giving a sedative to 'calm' the patient
      Escalation: Senior urgently (NEWS2 3).
  * WI-09
      Group: 14.1 Vital-sign problems
      What if: What if breathing is very slow? (8 or less)
      Steps: 3, 8
      Correct action: Wake/stimulate; oxygen; support airway; naloxone antidote PATIENT-SPECIFIC DOSE —
          CLINICIAN CONTROLLED ; stop further opioids.
      Incorrect action: Giving further opioid for pain
      Escalation: Emergency/anaesthetist immediately.
  * WI-10
      Group: 14.1 Vital-sign problems
      What if: What if the patient becomes confused?
      Steps: 3, 8
      Correct action: Treat what's found (sugar below 4 → glucose); sepsis bundle if infection; reassess
          capacity (consent may be invalid now).
      Incorrect action: Assuming it is 'just pain medicine' without checking glucose
      Escalation: Senior urgently (NEWS2 3); medical team if stroke signs.
  * WI-11
      Group: 14.1 Vital-sign problems
      What if: What if pain gets much worse or the tummy becomes rigid?
      Steps: 3, 8
      Correct action: Pain relief; nil by mouth; fluids; antibiotics; prepare for urgent surgery.
      Incorrect action: Giving more analgesia without re-examining or calling the senior
      Escalation: Senior immediately.
  * WI-12
      Group: 14.2 Blood-test problems
      What if: What if potassium is high? (above 5.3)
      Steps: 7
      Correct action: 5.5–5.9 (mild, UKKA): ECG, repeat, review drugs. 6.0–6.4: urgent repeat + ECG, treat if
          real. 6.5+ or ECG changes: emergency treatment (calcium to protect the heart; insulin-glucose)
          PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; heart monitor.
      Incorrect action: Emergency treatment of an obviously haemolysed sample with normal ECG; ignoring a real
          6.5+
      Escalation: Senior + anaesthetist (6.0+); emergency team if ECG changes.
  * WI-13
      Group: 14.2 Blood-test problems
      What if: What if potassium is low? (below 3.5)
      Steps: 7
      Correct action: Replace (by mouth if mild; through drip if 2.5–2.9 or vomiting, rate PATIENT-SPECIFIC
          DOSE — CLINICIAN CONTROLLED ); replace magnesium too.
      Incorrect action: Fast undiluted IV potassium (dangerous); not checking magnesium
      Escalation: Anaesthetist; senior if below 2.5.
  * WI-14
      Group: 14.2 Blood-test problems
      What if: What if sodium is abnormal?
      Steps: 7
      Correct action: Correct the cause slowly (no faster than ~8–10 mmol/L per day).
      Incorrect action: Rapid correction (risk of osmotic demyelination)
      Escalation: Senior + anaesthetist; medical team if below 125.
  * WI-15
      Group: 14.2 Blood-test problems
      What if: What if haemoglobin is low?
      Steps: 7
      Correct action: S1: investigate before planned surgery; S2: group & save/crossmatch, look for bleeding,
          transfusion decision if below 70 (80 heart disease).
      Incorrect action: Proceeding with planned surgery without investigating unexplained anaemia; transfusing
          a stable patient above threshold
      Escalation: Senior + anaesthetist.
  * WI-16
      Group: 14.2 Blood-test problems
      What if: What if the white cell count is high?
      Steps: 7
      Correct action: Interpret in context; S1 high WBC = not routine → acute pathway; S2 supports diagnosis;
          above 18 = moderate gallbladder grade.
      Incorrect action: Treating the number alone; ignoring it in a planned patient
      Escalation: Senior if S1 or if very high.
  * WI-17
      Group: 14.2 Blood-test problems
      What if: What if the white cell count is low?
      Steps: 7
      Correct action: Treat as serious if infection present; urgent antibiotics if septic.
      Incorrect action: Assuming 'no infection' because WBC is low
      Escalation: Senior; haematology if neutrophils below 0.5.
  * WI-18
      Group: 14.2 Blood-test problems
      What if: What if creatinine is high?
      Steps: 7
      Correct action: Fluids if dry; stop kidney-harming drugs; catheter/urine measurement; repeat in 6–24 h;
          adjust medicine doses; avoid CT dye unless essential (senior decides).
      Incorrect action: Giving NSAIDs, contrast or nephrotoxic drugs without senior decision
      Escalation: Senior + anaesthetist; kidney team if stage 2–3.
  * WI-19
      Group: 14.2 Blood-test problems
      What if: What if liver tests are abnormal?
      Steps: 7
      Correct action: Use the bile-duct-stone risk tool (Part 5.16): high → ERCP pathway; intermediate → MRCP;
          low → proceed.
      Incorrect action: Proceeding with planned cholecystectomy despite high-risk duct-stone criteria
      Escalation: Senior (in S1).
  * WI-20
      Group: 14.2 Blood-test problems
      What if: What if bilirubin is high?
      Steps: 7
      Correct action: Cholangitis signs → emergency; otherwise high-risk pathway (ERCP).
      Incorrect action: Ignoring fever + jaundice
      Escalation: Senior.
  * WI-21
      Group: 14.2 Blood-test problems
      What if: What if blood sugar is low? (below 4.0)
      Steps: 7
      Correct action: Treat immediately per hypoglycaemia protocol (fast sugar if can swallow AND not about to
          have anaesthetic per anaesthetist; else IV glucose) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ;
          recheck every 10–15 min.
      Incorrect action: Waiting to 'recheck later'; giving oral sugar to a drowsy patient
      Escalation: Anaesthetist (affects fasting); diabetes team.
  * WI-22
      Group: 14.2 Blood-test problems
      What if: What if blood sugar is high? (above 12)
      Steps: 7
      Correct action: Insulin per protocol (often variable-rate drip) PATIENT-SPECIFIC DOSE — CLINICIAN
          CONTROLLED ; fluids if dry.
      Incorrect action: Ignoring ketones in an SGLT2 user
      Escalation: Anaesthetist; diabetes team.
  * WI-23
      Group: 14.2 Blood-test problems
      What if: What if the clotting test (INR) is high?
      Steps: 7
      Correct action: S1 usually deferred after review and correct (vitamin K per senior); S2 correct before
          surgery PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED .
      Incorrect action: Proceeding with planned surgery; giving neuraxial anaesthesia
      Escalation: Senior + anaesthetist; haematology.
  * WI-24
      Group: 14.2 Blood-test problems
      What if: What if lactate is raised?
      Steps: 7
      Correct action: Sepsis bundle/fluids; repeat within 2–4 h.
      Incorrect action: Ignoring because BP is 'normal'
      Escalation: Senior; anaesthetist.
  * WI-25
      Group: 14.3 Heart tracing problems
      What if: What if the ECG is abnormal?
      Steps: 6, 7
      Correct action: AF: treat triggers, rate control per senior/medical; heart attack: emergency heart
          pathway.
      Incorrect action: Relying on the machine's automatic interpretation; proceeding with new AF/ischaemia
      Escalation: Senior + anaesthetist; medical/heart team.
  * WI-26
      Group: 14.4 Pregnancy, allergy and medicine problems
      What if: What if the patient is pregnant?
      Steps: 2, 6, 8
      Correct action: S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise →
          gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok.
      Incorrect action: Continuing appendicitis pathway; ordering CT first
      Escalation: Senior; gynaecology/obstetrics.
  * WI-27
      Group: 14.4 Pregnancy, allergy and medicine problems
      What if: What if pregnancy status is unknown or the patient declines a test?
      Steps: 2, 6, 8
      Correct action: Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior +
          anaesthetist decide whether to proceed.
      Incorrect action: Testing without consent; skipping documentation
      Escalation: Senior + anaesthetist.
  * WI-28
      Group: 14.4 Pregnancy, allergy and medicine problems
      What if: What if the patient is allergic to an antibiotic?
      Steps: 2, 8
      Correct action: Record; choose antibiotic by hospital rule (mild rash: cefazolin often allowed; severe:
          per policy) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
      Incorrect action: Giving the drug anyway; recording 'penicillin allergy' without reaction details
      Escalation: Pharmacist/microbiology per policy; anaesthetist.
  * WI-29
      Group: 14.4 Pregnancy, allergy and medicine problems
      What if: What if an allergic reaction happens after a medicine?
      Steps: 2, 8
      Correct action: STOP the drug; call for help; lie flat (sit up if struggling to breathe); call the
          emergency team; the responsible clinician gives IM adrenaline (adult standard dose 500 micrograms,
          repeat after 5 min if no improvement EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE
          PRESCRIPTION ); high-flow oxygen; IV fluid bolus per Resuscitation Council UK (adults 500–1000 mL
          crystalloid).
      Incorrect action: Giving antihistamine first instead of adrenaline; continuing the drug
      Escalation: Emergency team + anaesthetist.
  * WI-30
      Group: 14.4 Pregnancy, allergy and medicine problems
      What if: What if the patient takes blood thinners?
      Steps: 2, 8
      Correct action: S1: proceed only if stopped per plan (Part 8); otherwise postpone; never stop
          antiplatelets after a recent stent without the heart team. S2: senior/haematology decide reversal
          PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED .
      Incorrect action: Proceeding with planned surgery after a morning DOAC dose; stopping DAPT after a
          recent stent
      Escalation: Senior + anaesthetist; haematology/cardiology as needed.
  * WI-31
      Group: 14.4 Pregnancy, allergy and medicine problems
      What if: What if the patient has diabetes?
      Steps: 2, 8
      Correct action: Medicine plan (Part 8); first on list if possible; check sugar at least hourly; target
          6–10.
      Incorrect action: Giving usual sulfonylurea while fasting; no CBG monitoring
      Escalation: Anaesthetist; diabetes team if poorly controlled.
  * WI-32
      Group: 14.4 Pregnancy, allergy and medicine problems
      What if: What if the patient takes steroids, GLP-1 injections or SGLT2 tablets?
      Steps: 2, 8
      Correct action: Per Part 8/9.
      Incorrect action: Not asking; missing steroid cover
      Escalation: Anaesthetist.
  * WI-33
      Group: 14.5 Fasting problems
      What if: What if the patient hasn't fasted?
      Steps: 2, 5, 10
      Correct action: Tell anaesthetist honestly.
      Incorrect action: 'Rounding' the time; proceeding without telling the anaesthetist
      Escalation: Anaesthetist.
  * WI-34
      Group: 14.5 Fasting problems
      What if: What if fasting status is unknown?
      Steps: 2, 5, 10
      Correct action: Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In.
      Incorrect action: Recording 'fasted' by assumption
      Escalation: Anaesthetist.
  * WI-35
      Group: 14.6 Scan and result problems
      What if: What if imaging is inconclusive?
      Steps: 7
      Correct action: Next imaging (CT/MRI/MRCP) or observe and repeat assessment in 6–12 h.
      Incorrect action: Treating 'not visualised' as negative and discharging
      Escalation: Senior.
  * WI-36
      Group: 14.6 Scan and result problems
      What if: What if a test result doesn't make sense?
      Steps: 7
      Correct action: Repeat urgently (different tube/arm); treat meanwhile only if dangerous signs.
      Incorrect action: Acting on an implausible value without repeating; ignoring a dangerous one
      Escalation: Senior if dangerous possibility.
  * WI-37
      Group: 14.6 Scan and result problems
      What if: What if a laboratory result is delayed?
      Steps: 7
      Correct action: Continue other tasks; repeat sample if lost; S2: don't delay antibiotics/fluids/surgery
          decision for a non-critical result.
      Incorrect action: Waiting idle; delaying antibiotics in sepsis for results
      Escalation: Senior if it blocks a decision.
  * WI-38
      Group: 14.6 Scan and result problems
      What if: What if a critical result is phoned through?
      Steps: 7
      Correct action: Act immediately (ECG, emergency treatment if real); document; acknowledge.
      Incorrect action: Not reading back; not acting
      Escalation: Senior + anaesthetist.
  * WI-39
      Group: 14.7 Deterioration and sepsis
      What if: What if the patient deteriorates?
      Steps: 3, 8
      Correct action: Treat as found; increase monitoring.
      Incorrect action: Waiting for next routine obs; not escalating
      Escalation: NEWS2 5–6: urgent senior. 7+: emergency team.
  * WI-40
      Group: 14.7 Deterioration and sepsis
      What if: What if the patient becomes septic?
      Steps: 3, 8
      Correct action: Sepsis bundle within 1 hour (oxygen, cultures, antibiotics, fluids, lactate, urine
          output).
      Incorrect action: Antibiotics late; no cultures; no fluids
      Escalation: Senior + anaesthetist; critical care if shock.
  * WI-41
      Group: 14.8 Consent and communication problems
      What if: What if the patient refuses surgery?
      Steps: 9
      Correct action: Explore and correct misunderstandings; offer alternatives (antibiotics-only for simple
          appendicitis); never pressure; document; safety advice.
      Incorrect action: Pressuring; operating without consent
      Escalation: Senior.
  * WI-42
      Group: 14.8 Consent and communication problems
      What if: What if the patient cannot understand the consent discussion?
      Steps: 9
      Correct action: Interpreter; simpler words/diagrams; treat confusion; if no capacity and urgent → senior
          emergency route per law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
      Incorrect action: Using a child relative as interpreter; proceeding without teach-back
      Escalation: Senior; interpreter service.
  * WI-43
      Group: 14.8 Consent and communication problems
      What if: What if the patient is under 18?
      Steps: 9
      Correct action: Parent/guardian consent; involve the young person.
      Incorrect action: Accepting the minor's signature alone
      Escalation: Senior.
  * WI-44
      Group: 14.9 System problems
      What if: What if equipment fails?
      Steps: 11
      Correct action: Report; replace; don't start.
      Incorrect action: Starting anyway
      Escalation: Theatre lead; anaesthetist.
  * WI-45
      Group: 14.9 System problems
      What if: What if documents are missing?
      Steps: 10
      Correct action: Re-consent or find; don't transfer.
      Incorrect action: Transferring without consent
      Escalation: Senior if re-consent needed.
  * WI-46
      Group: 14.9 System problems
      What if: What if the patient deteriorates during transfer?
      Steps: 12
      Correct action: Stop, oxygen, call for help; return or proceed per stability.
      Incorrect action: Continuing to theatre without assessment
      Escalation: Emergency team/anaesthetist.
  * WI-47
      Group: 14.9 System problems
      What if: What if there's an identity mismatch at theatre?
      Steps: 12, 13
      Correct action: STOP; resolve with ward/admissions.
      Incorrect action: Proceeding 'because it's obviously the right person'
      Escalation: Senior; theatre lead.
```


## 24. CLINICAL NUMBERS AND TOOLS (CONFIGURATION)

```text
Transcribed from WT Parts 4–9, 15, 17. Lab ranges are Type C until aligned with the target laboratory.

  [TABLE] Parameter | Unit | Normal | Watch | Worry | Emergency | Note | Type
  * HR
      Unit: /min
      Normal: 51–90
      Watch: 91–110 or 41–50
      Worry: 111–130
      Emergency: ≥131 or ≤40
      Note: Display reference 60–100 (AHA); NEWS2 bands used for scoring
      Type: A
  * SBP_low
      Unit: mmHg
      Normal: 111–219
      Watch: 101–110
      Worry: 91–100
      Emergency: ≤90 / MAP <65
      Note: Fall >40 below usual = NG253 high-risk criterion
      Type: A
  * BP_high
      Unit: mmHg
      Normal: <140/90
      Watch: 140–179 / 90–109
      Worry: ≥180/110 (S1 usually deferred after review)
      Emergency: ≥180/120 + symptoms
      Note: Elective rule AAGBI/BHS 2016
      Type: A
  * RR
      Unit: /min
      Normal: 12–20
      Watch: 9–11 or 21–24
      Worry: ≥22 with infection
      Emergency: ≥25 or ≤8
      Note: Avatar breathing visible; trainee must count
      Type: A
  * SpO2
      Unit: %
      Normal: ≥96
      Watch: 94–95
      Worry: 92–93
      Emergency: ≤91 (critical <88)
      Note: Target 94–98; 88–92 hypercapnic risk (Scale 2)
      Type: A
  * Temp
      Unit: °C
      Normal: 36.1–37.9
      Watch: 35.1–36.0 / 38.0–39.0
      Worry: ≥39.1
      Emergency: ≤35.0
      Note: NEWS2 0 = 36.1–38.0 (display vs scoring differ)
      Type: A
  * Alertness
      Unit: ACVPU
      Normal: Alert
      Watch: —
      Worry: New confusion
      Emergency: Voice/Pain/Unresponsive
      Note: Check glucose FIRST if confused
      Type: A
  * Pain
      Unit: /10
      Normal: 0–3
      Watch: 4–6
      Worry: 7–10
      Emergency: Uncontrolled
      Note: Early analgesia correct (WSES 2020)
      Type: A
  * CRT
      Unit: s
      Normal: ≤2
      Watch: 2–3
      Worry: >3
      Emergency: Mottled skin
      Note: Adjunct (SSC 2021)
      Type: B
  * Urine
      Unit: mL/kg/h
      Normal: ≥0.5
      Watch: below for <6 h
      Worry: below for 6 h+
      Emergency: Almost none
      Note: KDIGO
      Type: A
  * ShockIndex
      Unit: HR÷SBP
      Normal: 0.5–0.7 (≤~0.9)
      Watch: 0.8–0.9
      Worry: approaching/above 1.0
      Emergency: no validated higher cut-off
      Note: Supporting indicator only (Koch 2019; Rivers 1994)
      Type: B
  * NEWS2
      Unit: points
      Normal: 0
      Watch: 1–4
      Worry: 5–6 or any single 3
      Emergency: ≥7
      Note: Risk stratification, not diagnosis; not for pregnancy
      Type: A

  [TABLE] NEWS2 parameter | 3 | 2/1 low | 0 | 1/2 high | 3 high
  * RR
      3: ≤8 → 3
      2/1 low: 9–11 → 1
      0: 12–20 → 0
      1/2 high: 21–24 → 2
      3 high: ≥25 → 3
  * SpO2 Scale 1
      3: ≤91 → 3
      2/1 low: 92–93 → 2
      0: 94–95 → 1
      1/2 high: ≥96 → 0
  * Supplemental O2
      3: Yes → 2
      0: No → 0
  * SBP
      3: ≤90 → 3
      2/1 low: 91–100 → 2
      0: 101–110 → 1
      1/2 high: 111–219 → 0
      3 high: ≥220 → 3
  * HR
      3: ≤40 → 3
      2/1 low: 41–50 → 1
      0: 51–90 → 0
      1/2 high: 91–110 → 1 · 111–130 → 2
      3 high: ≥131 → 3
  * Consciousness
      0: Alert → 0
      3 high: C/V/P/U → 3
  * Temp
      3: ≤35.0 → 3
      2/1 low: 35.1–36.0 → 1
      0: 36.1–38.0 → 0
      1/2 high: 38.1–39.0 → 1
      3 high: ≥39.1 → 2

  [TABLE] Score | Risk | Frequency | Response
  * 0
      Risk: Low
      Frequency: ≥12-hourly
      Response: Routine
  * 1–4
      Risk: Low
      Frequency: ≥4–6-hourly
      Response: Nurse decides whether to call doctor
  * Any single 3
      Risk: Low–medium
      Frequency: ≥hourly
      Response: Urgent ward-doctor review
  * 5–6
      Risk: Medium
      Frequency: ≥hourly
      Response: Urgent review by doctor skilled in acutely ill
  * ≥7
      Risk: High
      Frequency: Continuous
      Response: Emergency team incl. critical-care skills

  [TABLE] NG253 tier | Criteria | Team actions | Reassess
  * High
      Criteria: Suspected infection AND NEWS2 ≥7, or 5–6 with a single 3 (or confirmed on review)
      Team actions: Senior review; IV antibiotics ≤1 h; fluid bolus ≤1 h (unless contraindicated); consider
          critical care
      Reassess: NEWS2 every 30 min; escalate if no response in 1 h
  * Moderate
      Criteria: NEWS2 5–6, or 3–4 with a single 3
      Team actions: Clinician review ≤1 h incl. venous lactate; antibiotics ≤3 h if infection likely
      Reassess: NEWS2 hourly
  * Low
      Criteria: NEWS2 1–4
      Team actions: Clinical assessment; antibiotics ≤6 h if infection likely
      Reassess: NEWS2 every 4–6 h
  * Very low
      Criteria: NEWS2 0
      Team actions: Routine unless clinical concern
      Reassess: Standard observations

  [TABLE] Test | Unit | Normal | Mild | Significant | Severe/critical | Decision rule | Type
  * Haemoglobin
      Unit: g/L
      Normal: M 130–170; F 120–150
      Mild: M 110–129 / F 110–119
      Significant: 80–109
      Severe/critical: <80 (critical <70 or fall ≥20)
      Decision rule: Unexplained anaemia → investigate before planned surgery (B); transfusion 70 (80 ACS)
      Type: A/B
  * WBC
      Unit: ×10⁹/L
      Normal: 4.0–11.0
      Mild: 11.1–15.0
      Significant: 15.1–20.0
      Severe/critical: >20 or <4
      Decision rule: Raised in planned patient → reassess for acute infection (B); >18 TG18 moderate
      Type: A/B
  * Neutrophils
      Unit: ×10⁹/L
      Normal: 2.0–7.5
      Mild: —
      Significant: <1.5
      Severe/critical: <0.5 (critical)
      Decision rule: Haematology advice <0.5
      Type: A
  * Platelets
      Unit: ×10⁹/L
      Normal: 150–400
      Mild: 100–149
      Significant: 50–99
      Severe/critical: <50 (critical <30)
      Decision rule: ≥50 most surgery; ≥80 neuraxial (BSH 2017); repeat in citrate tube if clumping
      Type: A/B
  * CRP
      Unit: mg/L
      Normal: <5 (some labs <10)
      Mild: 10–49 (WT 5.4: 5–49)
      Significant: 50–149
      Severe/critical: ≥150 (≥300 critical)
      Decision rule: Supports diagnosis; not a cancellation value
      Type: A
  * Sodium
      Unit: mmol/L
      Normal: 135–145 (Harmony 133–146)
      Mild: 130–134 / 146–150
      Significant: 125–129 / 151–155
      Severe/critical: <125 / >155 (critical ≤120 / ≥160)
      Decision rule: S1 usually deferred if <130 or >150 (B); correct ≤10 in 24 h
      Type: A/B
  * Potassium
      Unit: mmol/L
      Normal: 3.5–5.3
      Mild: 3.0–3.4 / 5.5–5.9 (5.4 = just above reference)
      Significant: 2.5–2.9 / 6.0–6.4
      Severe/critical: <2.5 / ≥6.5 or ECG change
      Decision rule: ECG for all hyperkalaemia (UKKA); planned acceptance 3.0–5.5 (B+C)
      Type: A/B/C
  * Urea
      Unit: mmol/L
      Normal: 2.5–7.8
      Mild: 7.9–12
      Significant: >12
      Severe/critical: —
      Decision rule: Interpreted with creatinine
      Type: A
  * Creatinine
      Unit: µmol/L
      Normal: M 60–110; F 45–90
      Mild: ×1.5–1.9 baseline (or +26.5 in 48 h) = AKI 1
      Significant: ×2–2.9 = AKI 2
      Severe/critical: ×3 or ≥354 = AKI 3
      Decision rule: Needs baseline; S1 usually deferred; S2 correct then proceed
      Type: A/B
  * eGFR
      Unit: mL/min/1.73m²
      Normal: ≥90
      Mild: 60–89
      Significant: 30–59
      Severe/critical: <30
      Decision rule: Drug-dose changes; NSAID avoidance
      Type: A
  * Glucose (diabetic, peri-op)
      Unit: mmol/L
      Normal: 6–10 (to 12 OK)
      Mild: 12.1–14
      Significant: >14 (check ketones)
      Severe/critical: <4.0 (severe <3.0)
      Decision rule: Hypo protocol immediately; CBG hourly while fasting
      Type: A
  * HbA1c
      Unit: mmol/mol
      Normal: <42
      Mild: 42–47 / 48–69
      Significant: >69 (postpone S1)
      Severe/critical: —
      Decision rule: S1 >69 → optimise (CPOC); S2 proceed + diabetes team
      Type: A
  * Ketones
      Unit: mmol/L
      Normal: <0.6
      Mild: 0.6–1.4
      Significant: 1.5–2.9
      Severe/critical: ≥3.0 (+acidosis = DKA)
      Decision rule: DKA → no planned surgery; SGLT2 euglycaemic DKA
      Type: A
  * Bilirubin
      Unit: µmol/L
      Normal: 3–21
      Mild: 22–30
      Significant: 31–68
      Severe/critical: >68
      Decision rule: >68 + CBD >6 mm = ASGE high → ERCP before cholecystectomy
      Type: A
  * ALT
      Unit: U/L
      Normal: ≤40
      Mild: 41–120
      Significant: 121–400
      Severe/critical: >400
      Decision rule: Pattern-based (R ratio)
      Type: A
  * ALP
      Unit: U/L
      Normal: 30–130
      Mild: ≤2× ULN
      Significant: 2–5× ULN
      Severe/critical: >5× ULN
      Decision rule: Cholestatic pattern → duct imaging
      Type: A
  * GGT
      Unit: U/L
      Normal: M <60; F <40
      Mild: ≤3× ULN
      Significant: >3× ULN
      Severe/critical: —
      Decision rule: Confirms hepatobiliary ALP
      Type: A
  * Albumin
      Unit: g/L
      Normal: 35–50
      Mild: 30–34 (WT 5.15: low 25–34)
      Significant: 25–29
      Severe/critical: <25
      Decision rule: Risk marker, not cancellation
      Type: A/B
  * Lipase/amylase
      Unit: U/L
      Normal: <ULN
      Mild: 1–3× ULN
      Significant: >3× ULN (pancreatitis)
      Severe/critical: —
      Decision rule: Pancreatitis → planned list cancelled
      Type: A
  * INR (no warfarin)
      Unit: ratio
      Normal: 0.8–1.2
      Mild: 1.3–1.5
      Significant: >1.5
      Severe/critical: >2.0
      Decision rule: ≤1.5 general surgery; ≤1.4 neuraxial (B)
      Type: A/B
  * APTT ratio
      Unit: ratio
      Normal: 0.8–1.2
      Mild: —
      Significant: >1.2
      Severe/critical: —
      Decision rule: Haematology if unexplained
      Type: A
  * Lactate
      Unit: mmol/L
      Normal: <2.0
      Mild: —
      Significant: 2.0–3.9
      Severe/critical: ≥4.0
      Decision rule: Sepsis pathway; repeat 2–4 h
      Type: A
  * β-hCG (serum)
      Unit: IU/L
      Normal: <5
      Mild: 5–25 (repeat 48 h)
      Significant: >25 positive
      Severe/critical: positive + pain/shock
      Decision rule: Positive → S1 postponed; S2 ectopic differential → gynaecology
      Type: A
  * QTc
      Unit: ms
      Normal: M <450; F <460
      Mild: prolonged ≥450 M / ≥460 F
      Significant: —
      Severe/critical: ≥500 markedly prolonged
      Decision rule: No single interval decides surgery
      Type: A
  * Mg / adjusted Ca / ferritin
      Unit: mmol/L / µg/L
      Normal: 0.7–1.0 / 2.2–2.6 / >30
      Mild: —
      Significant: —
      Severe/critical: Ca ≤1.8 or ≥3.5 critical
      Decision rule: WT 5.26
      Type: A

  Critical result | Lab phones if                    
  ----------------+----------------------------------
  Potassium       | ≤2.5 or ≥6.5                     
  Sodium          | ≤120 or ≥160                     
  Glucose         | ≤2.5                             
  Calcium         | ≤1.8 or ≥3.5                     
  Creatinine      | ≥354 (no known kidney failure)   
  CRP             | ≥300                             
  Haemoglobin     | <70 g/L or fall ≥20              
  Platelets       | <30                              
  Neutrophils     | <0.5                             
  INR             | ≥5 on warfarin; >1.5 unexpectedly
  Lactate         | ≥4.0                             

  [TABLE] Rule | Value | Type
  * Fasting (planned)
      Value: Clear 2 h / milk & light meal 6 h / heavy meal 8 h
      Type: A
  * S1 BP postpone
      Value: ≥180/110 on repeat (GP target <160/100)
      Type: A
  * S1 diabetes
      Value: HbA1c >69 mmol/mol (8.5%) → optimise
      Type: A
  * Peri-op glucose target
      Value: 6–10 (6–12 acceptable)
      Type: A
  * Bile-duct stone high risk
      Value: Bilirubin >68 + duct >6 mm; or stone seen; or cholangitis
      Type: A
  * Gallbladder moderate grade
      Value: WBC >18; lump; >72 h; severe local inflammation
      Type: A
  * Appendix abnormal width
      Value: >6 mm
      Type: A
  * AIR bands
      Value: 0–4 low, 5–8 unsure, 9–12 high
      Type: A
  * AAS bands
      Value: 0–10 low, 11–15 intermediate, ≥16 high
      Type: A
  * Sepsis antibiotics
      Value: ≤1 h if NEWS2 ≥7 (or 5–6 + single 3); ≤3 h if 5–6; ≤6 h if 1–4 (NG253)
      Type: A
  * Antibiotic before incision
      Value: Within 60 min
      Type: A
  * Theatre temperature
      Value: ≥36.0 °C
      Type: A
  * Sign In blood loss
      Value: >500 mL (7 mL/kg children)
      Type: A
  * Appendectomy timing
      Value: Generally prompt; WSES 2025 lap appendectomy ≤24 h of admission when indicated (context-
          dependent)
      Type: A
  * Acute cholecystitis surgery
      Value: Early (ideally ≤72 h; NICE ≤1 week)
      Type: A
  * Transfer escort
      Value: NEWS2 ≤4 porter+nurse; 5–6 nurse+monitor; ≥7 doctor+nurse+full monitoring
      Type: D+C
  * Deterioration clock (untreated S2)
      Value: HR +5–10 and NEWS2 +1 about every 30 min
      Type: D
  * Turnaround times
      Value: FBC 45 min; imaging 60–120 min; others not specified in WT (unresolved)
      Type: D
  * Low-SpO2 emergency trigger
      Value: SpO2 ≤85% (simulator trigger, not guideline cut-off)
      Type: D

  [TABLE] Intake | Minimum | Note | Type
  * Clear liquids
      Minimum: 2 h
      Note: Water, pulp-free juice, black tea/coffee, fizzy drinks, carbohydrate drinks (some hospitals sips
          to 1 h — C)
      Type: A
  * Breast milk
      Minimum: 4 h
      Note: Listed for completeness; adults only in ProcSense
      Type: A
  * Milk / formula / tea or coffee WITH milk
      Minimum: 6 h
      Note: Counts as food
      Type: A
  * Light meal (e.g. toast + clear drink)
      Minimum: 6 h
      Type: A
  * Heavy/fried/fatty meal, meat (biryani, paratha, nihari)
      Minimum: 8 h+
      Note: Classing these dishes as heavy = B
      Type: A/B
  * Chewing gum
      Minimum: Don't cancel
      Note: Remove before sleep (ASA 2023)
      Type: A
  * Regular tablets
      Minimum: Sip of water
      Note: Unless told to stop
      Type: A
  * Emergency (S2)
      Minimum: Treat as full stomach → RSI
      Note: Not delayed for food when waiting is more dangerous; short wait sometimes (C)
      Type: A/C
  * GLP-1 RA
      Minimum: Continue (ProcSense default)
      Note: Ask drug + nausea/vomiting/bloating; anaesthetist decides RSI vs delay (B+C)
      Type: B/C

  [TABLE] Test | S1 | S2 | Why
  * Liver tests (LFTs)
      S1: YES (all)
      S2: If upper pain or unclear diagnosis
      Why: Bile blockage
  * Pregnancy test
      S1: If pregnancy possible/uncertain — with consent
      S2: If possible — with consent (urgent in abdominal pain)
      Why: Diagnosis, imaging, anaesthesia, drugs, timing
  * FBC
      S1: If ASA 3+, anaemia symptoms, heart/kidney disease, or local rule
      S2: YES
      Why: Infection, bleeding, anaemia
  * CRP
      S1: Only if inflammation suspected
      S2: YES
      Why: Inflammation; score
  * U&E
      S1: If 65+, diabetes, kidney disease, diuretics/ACE-i, ASA 3+
      S2: YES
      Why: Kidneys, K, dehydration
  * Capillary glucose
      S1: If diabetic
      S2: If diabetic or vomiting
      Why: Low/high sugar
  * HbA1c
      S1: If diabetic and none in 3 months
      S2: No
      Why: Diabetes control
  * Clotting (INR/APTT)
      S1: If anticoagulant or liver disease
      S2: If anticoagulant, septic, liver disease
      Why: Bleeding risk
  * Group & save
      S1: Per hospital rule
      S2: Per rule; always if bleeding suspected
      Why: Blood ready
  * Urine dipstick
      S1: No (not routine)
      S2: YES
      Why: UTI/stone look-alikes
  * ECG
      S1: Heart/kidney/diabetes, irregular pulse, 65+ (some)
      S2: 65+, heart disease, HR >130, irregular, salt problems
      Why: Rhythm
  * Lactate + blood cultures
      S1: If septic
      S2: If sepsis suspected
      Why: Sepsis
  * Lipase/amylase
      S1: If severe upper/back pain
      S2: If diagnosis unclear
      Why: Pancreatitis
  * Ultrasound
      S1: Already done; repeat if acute
      S2: Young/female/pregnant first
      Why: Imaging
  * CT
      S1: Only for complications
      S2: Adults when unclear; older patients
      Why: Detailed scan
  * MRCP
      S1: If LFTs abnormal / duct wide
      S2: No
      Why: Duct stones
  * Chest X-ray
      S1: No (not routine)
      S2: Only breathing problems / suspected perforation
      Why: —
```


#### Alvarado (0–10)

```text
  Item             | Points / rule                                             
  -----------------+-----------------------------------------------------------
  Migration to RLQ | 1                                                         
  Anorexia         | 1                                                         
  Nausea/vomiting  | 1                                                         
  RLQ tenderness   | 2                                                         
  Rebound          | 1                                                         
  Temp ≥37.3       | 1                                                         
  WBC >10          | 2                                                         
  Neutrophils >75% | 1                                                         
  Bands            | 0–4 unlikely; 5–6 unsure; 7–8 probable; 9–10 very probable
```


#### AIR (0–12) — draft default

```text
  Item                                 | Points / rule                 
  -------------------------------------+-------------------------------
  RLQ pain                             | 1                             
  Vomiting                             | 1                             
  Rebound/guarding light/medium/strong | 1/2/3                         
  Temp ≥38.5                           | 1                             
  WBC 10.0–14.9 / ≥15                  | 1/2                           
  Neutrophils 70–84% / ≥85%            | 1/2                           
  CRP 10–49 / ≥50                      | 1/2                           
  Bands                                | 0–4 low; 5–8 unsure; 9–12 high
```


#### AAS (0–23) — optional

```text
  Item                                             | Points / rule                         
  -------------------------------------------------+---------------------------------------
  RLQ pain                                         | 2                                     
  Migration                                        | 2                                     
  RLQ tenderness: women 16–49 / everyone else      | 1/3                                   
  Guarding mild / moderate-severe                  | 2/4                                   
  WBC 7.2–10.8 / 10.9–13.9 / ≥14                   | 1/2/3                                 
  Neutrophils 62–74.9 / 75–82.9 / ≥83%             | 2/3/4                                 
  CRP pain <24 h: 4–10.9 / 11–24.9 / 25–82.9 / ≥83 | 2/3/5/1                               
  CRP pain ≥24 h: 12–52.9 / 53–151.9 / ≥152        | 2/2/1                                 
  Bands                                            | 0–10 low; 11–15 intermediate; ≥16 high
```


#### TG18 acute cholecystitis

```text
  [TABLE] Item | Points / rule
  * A local
      Points / rule: Murphy's sign; RUQ mass/pain/tenderness
  * B systemic
      Points / rule: Fever; raised CRP; raised WBC
  * C imaging
      Points / rule: US/CT inflamed GB (wall >4 mm, GB >8×4 cm, pericholecystic fluid, sonographic Murphy's,
          stone in neck)
  * Diagnosis
      Points / rule: Suspected A+B; definite A+B+C
  * Grade III
      Points / rule: Organ dysfunction: vasopressors; reduced consciousness; poor oxygenation; oliguria or
          creatinine >2.0 mg/dL (~177 µmol/L); INR >1.5; platelets <100
  * Grade II
      Points / rule: WBC >18; tender RUQ mass; >72 h; severe local inflammation
  * Grade I
      Points / rule: Neither
```


#### TG18 acute cholangitis

```text
  [TABLE] Item | Points / rule
  * A inflammation
      Points / rule: Fever >38 and/or rigors; WBC <4 or >10; CRP ≥10
  * B cholestasis
      Points / rule: Bilirubin ≥34 µmol/L (2 mg/dL); LFT >1.5× ULN
  * C imaging
      Points / rule: Duct dilatation; cause seen
  * Diagnosis
      Points / rule: Definite A+B+C; suspected A+(B or C) → emergency sepsis care + urgent drainage
  * Grade II example (STK-S1-D)
      Points / rule: WBC >12 and fever ≥39, no organ failure → early drainage; Grade III urgent
```


#### ASGE 2019 duct-stone risk

```text
  [TABLE] Item | Points / rule
  * High
      Points / rule: Stone on imaging, or bilirubin >68 + CBD >6 mm, or cholangitis → ERCP pathway
  * Intermediate
      Points / rule: Abnormal LFT, age >55, dilated CBD → MRCP/EUS
  * Low
      Points / rule: Proceed
  * Note
      Points / rule: ESGE omits age; surgeon chooses (C)
```


#### ASA (2020)

```text
  [TABLE] Item | Points / rule
  * 1
      Points / rule: Healthy
  * 2
      Points / rule: Mild disease (smoker, pregnancy, BMI 30–40, controlled DM/BP)
  * 3
      Points / rule: Severe disease limiting (poor DM/BP, COPD, BMI 40+, dialysis, MI/stroke >3 months,
          pacemaker)
  * 4
      Points / rule: Constant threat to life (MI/stroke <3 months, sepsis, severe valve disease)
  * 5
      Points / rule: Not expected to survive without operation
  * 6
      Points / rule: Brain-dead donor
  * E
      Points / rule: Emergency (all S2)
  * ProcSense expected
      Points / rule: Healthy S1 = 1; controlled BP/DM = 2; poor DM, AF on anticoag, BMI 40+ = 3; healthy S2 =
          1E; septic S2 = 2E–4E (B)
```


#### Airway / OSA

```text
  [TABLE] Item | Points / rule
  * Mallampati
      Points / rule: 1–2 normal; 3–4 concerning
  * Mouth opening
      Points / rule: ≥3 cm normal
  * Thyromental
      Points / rule: ≥6.5 cm normal; <6 concerning
  * Neck movement
      Points / rule: Full vs stiff
  * STOP-BANG
      Points / rule: Snoring, Tired, Observed apnoea, Pressure, BMI >35, Age >50, Neck >40 cm, male Gender:
          0–2 low; 3–4 intermediate; 5–8 high
```


#### SORT (overall risk)

```text
  [TABLE] Item | Points / rule
  * Draft
      Points / rule: QS216 requires validated tool; SORT draft choice (C); P-POSSUM / NSQIP alternatives; NELA
          not for appendectomy

  [TABLE] Medicine / group | Purpose | Cautions | Timing | Dose policy
  * Paracetamol
      Purpose: First analgesic; antipyretic
      Cautions: Severe liver disease; low weight
      Timing: Early
      Dose policy: DOSE-EX IV SmPC: 1 g over 15 min up to 4×/day, ≥4 h apart, max 4 g/day (>50 kg), 3 g/day
          with risk factors; <50 kg 15 mg/kg (max 60 mg/kg/day, ≤3 g)
  * NSAIDs
      Purpose: Pain + inflammation
      Cautions: AKI/dehydration, ulcers, anticoagulants, some asthma, pregnancy, heart failure; avoid S2 with
          AKI
      Timing: —
      Dose policy: DOSE-PS
  * Opioids
      Purpose: Moderate–severe pain
      Cautions: Respiratory depression, drowsiness, nausea; accumulate in kidney failure
      Timing: Titrated IV; watch breathing
      Dose policy: DOSE-PS
  * Antiemetics
      Purpose: Nausea/vomiting
      Cautions: Ondansetron: long QTc; metoclopramide: not with bowel obstruction
      Timing: When needed
      Dose policy: DOSE-EX ondansetron 4 mg slow IV (≥30 s) for PONV; others DOSE-PS
  * IV fluids
      Purpose: Replace losses; resuscitation
      Cautions: Heart/kidney failure (overload)
      Timing: Bolus vs maintenance
      Dose policy: DOSE-EX NICE CG174: crystalloid Na 130–154, 500 mL <15 min, reassess (scope excludes
          pregnancy, severe liver/kidney, diabetes, burns; HF smaller boluses) else DOSE-PS
  * Antibiotic prophylaxis S1
      Purpose: Prevent SSI
      Cautions: Not routine low-risk; risk factors: age >70, inflamed GB, jaundice/duct stones, diabetes, ASA
          3+, immunosuppression, pregnancy, likely conversion
      Timing: ≤60 min before incision; redose if long
      Dose policy: DOSE-EX cefazolin 2 g IV (3 g ≥120 kg) ASHP 2013; agent per institution (C)
  * Antibiotics S2
      Purpose: All appendectomies pre-op
      Cautions: Allergy branch
      Timing: As soon as decided; sepsis per NG253 tier; confirm at Time Out
      Dose policy: C (e.g. cefazolin + metronidazole; co-amoxiclav); post-op none if simple, 2–3 days if
          complicated
  * Warfarin
      Purpose: Anticoagulant
      Cautions: Bleeding
      Timing: Stop 5 days; INR day before ≤1.5
      Dose policy: Bridging specialist (C)
  * Apixaban/rivaroxaban/edoxaban
      Purpose: DOAC
      Cautions: Bleeding
      Timing: Stop 1 day (low–mod) / 2 days (high bleeding risk) if kidneys OK
      Dose policy: Lap chole bleeding category C (PAUSE)
  * Dabigatran
      Purpose: DOAC
      Cautions: Renal clearance
      Timing: 1–2 days good kidneys; 2–4 days weaker
      Dose policy: —
  * Aspirin
      Purpose: Antiplatelet
      Cautions: —
      Timing: Usually continue for heart/stroke; may stop 7 days if primary prevention
      Dose policy: Surgeon decides
  * Clopidogrel / ticagrelor / prasugrel
      Purpose: Antiplatelet
      Cautions: Recent stent → cardiology; don't stop; defer planned
      Timing: 5 / 3–5 / 7 days
      Dose policy: —
  * Metformin
      Purpose: Diabetes
      Cautions: Kidneys/CT dye (some hospitals)
      Timing: Normal if one meal missed
      Dose policy: C
  * Sulfonylureas (gliclazide)
      Purpose: Diabetes
      Cautions: Hypoglycaemia when fasting
      Timing: Day of: OMIT
      Dose policy: —
  * SGLT2 inhibitors
      Purpose: Diabetes
      Cautions: Euglycaemic DKA
      Timing: UK 2025: omit day before AND day of (FDA 3 days; ertugliflozin 4)
      Dose policy: ProcSense default UK (C); check ketones if unwell
  * GLP-1 RA
      Purpose: Diabetes/weight
      Cautions: Slow gastric emptying
      Timing: See fasting
      Dose policy: Continue + anaesthetist assessment (B+C)
  * DPP-4 inhibitors
      Purpose: Diabetes
      Cautions: Low risk
      Timing: Day of omit or per local
      Dose policy: C
  * Long-acting insulin
      Purpose: Diabetes
      Cautions: Hypoglycaemia while fasting
      Timing: Reduce ~20% day before and day of
      Dose policy: DOSE-PS
  * Other insulin
      Purpose: Diabetes
      Cautions: —
      Timing: Adjust/omit per protocol; VRIII if >1 meal missed or poor control; Type 1 never stop entirely
          (A)
      Dose policy: DOSE-PS
  * ACE-i / ARB
      Purpose: BP
      Cautions: Hypotension at induction
      Timing: Often omit 24 h
      Dose policy: C
  * Beta-blockers
      Purpose: BP/rate
      Cautions: Don't stop suddenly; not started on day
      Timing: Continue
      Dose policy: —
  * Diuretics
      Purpose: Fluid
      Cautions: Dehydration, low K
      Timing: Usually omit morning
      Dose policy: C
  * Statins / CCB
      Purpose: —
      Cautions: —
      Timing: Continue
      Dose policy: —
  * Steroids (pred ≥5 mg >4 wk)
      Purpose: —
      Cautions: Adrenal crisis
      Timing: Continue + extra cover if long-term/adrenal insufficiency
      Dose policy: DOSE-EX hydrocortisone 100 mg IV at induction (AoA/RCP/SfE 2020); anaesthetist decides
  * COCP / HRT
      Purpose: —
      Cautions: Clot risk
      Timing: Major surgery may stop 4 weeks (NG89); lap chole usually continue + VTE prophylaxis
      Dose policy: C
  * Herbal (ginkgo, garlic, ginseng, fish oil)
      Purpose: —
      Cautions: Bleeding
      Timing: Often stop 1–2 weeks
      Dose policy: C
  * Epilepsy / Parkinson's
      Purpose: —
      Cautions: Fits / stiffness
      Timing: Continue on time
      Dose policy: Specialist if dose missed
  * VTE prophylaxis
      Purpose: Clot prevention
      Cautions: Mechanical not if poor circulation/ulcers/neuropathy
      Timing: LMWH 6–12 h post-op, ≥7 days abdominal if clot>bleed (NG89)
      Dose policy: DOSE-EX enoxaparin 20 mg moderate / 40 mg high / 20 mg CrCl 15–30, ≥7–10 days; timing DOSE-
          PS
  * Adrenaline (anaphylaxis)
      Purpose: Emergency
      Cautions: —
      Timing: Immediately
      Dose policy: DOSE-EX 500 micrograms IM (0.5 mL 1 mg/mL) anterolateral thigh, repeat after 5 min (RCUK
          2021)
  * Glucose (hypo, can swallow)
      Purpose: Emergency
      Cautions: Fasting status matters
      Timing: Immediately; recheck 10–15 min
      Dose policy: DOSE-EX 15–20 g quick carbohydrate (JBDS)
  * Glucose IV (hypo, NBM/drowsy)
      Purpose: Emergency
      Cautions: —
      Timing: Over ~15 min; recheck 10 min
      Dose policy: DOSE-EX 10% 150–200 mL or 20% 75–100 mL (JBDS)
  * Calcium + insulin–glucose (hyperK)
      Purpose: Emergency
      Cautions: ECG changes
      Timing: Immediately
      Dose policy: DOSE-EX 30 mL 10% Ca gluconate over 10 min (or 10 mL 10% CaCl); 10 units soluble insulin in
          25 g glucose over 15–30 min; 10% glucose 50 mL/h × 5 h if pre-glucose <7 (UKKA)
  * Naloxone (opioid toxicity)
      Purpose: Emergency
      Cautions: —
      Timing: —
      Dose policy: DOSE-PS
  * Sepsis antibiotics + fluids
      Purpose: Emergency
      Cautions: Allergy
      Timing: Per NG253 tier
      Dose policy: C agent; DOSE-EX 500 mL bolus <15 min

  [TABLE] Phase | Item | S1 | S2 | If no | Source
  * SIGN IN
      Item: Has the patient confirmed his/her identity, site, procedure and consent?
      S1: Yes
      S2: Yes
      If no: STOP — resolve
      Source: WHO 2009
  * SIGN IN
      Item: Is the site marked? (Yes / Not applicable)
      S1: Usually N/A (C)
      S2: Usually N/A (C)
      If no: Ask surgeon
      Source: WHO 2009
  * SIGN IN
      Item: Is the anaesthesia machine and medication check complete?
      S1: Yes
      S2: Yes
      If no: STOP — complete check
      Source: WHO 2009
  * SIGN IN
      Item: Is the pulse oximeter on the patient and functioning?
      S1: Yes
      S2: Yes
      If no: STOP — replace
      Source: WHO 2009
  * SIGN IN
      Item: Does the patient have a known allergy? (No / Yes)
      S1: No/Yes + what
      S2: No/Yes + what
      If no: Prepare alternatives
      Source: WHO 2009
  * SIGN IN
      Item: Difficult airway or aspiration risk? (No / Yes, and equipment/assistance available)
      S1: Usually no
      S2: Yes — aspiration; RSI equipment, suction, assistance
      If no: Plan, don't stop
      Source: WHO 2009
  * SIGN IN
      Item: Risk of >500 mL blood loss (7 mL/kg in children)? (No / Yes, and two IVs/central access and fluids
          planned)
      S1: Usually no
      S2: Usually no; yes if bleeding/coagulopathy
      If no: Two IVs + fluids/blood
      Source: WHO 2009
  * TIME OUT
      Item: Confirm all team members have introduced themselves by name and role
      S1: Yes
      S2: Yes
      If no: —
      Source: WHO 2009
  * TIME OUT
      Item: Confirm the patient's name, procedure, and where the incision will be made
      S1: Yes
      S2: Yes
      If no: —
      Source: WHO 2009
  * TIME OUT
      Item: Has antibiotic prophylaxis been given within the last 60 minutes? (Yes / Not applicable)
      S1: Only if indicated, else N/A
      S2: Yes — time confirmed; redose decision
      If no: Fix, repeat item
      Source: WHO 2009
  * TIME OUT
      Item: To surgeon: critical/non-routine steps? case duration? anticipated blood loss?
      S1: ~60 min; minimal
      S2: ~45–60 min; minimal
      If no: —
      Source: WHO 2009
  * TIME OUT
      Item: To anaesthetist: any patient-specific concerns?
      S1: As relevant (OSA, diabetes, aspiration)
      S2: Aspiration, sepsis, kidneys
      If no: —
      Source: WHO 2009
  * TIME OUT
      Item: To nursing team: sterility (incl. indicator results) confirmed? equipment issues or concerns?
      S1: Tower working
      S2: Lap + open sets
      If no: Indicator fail → stop
      Source: WHO 2009
  * TIME OUT
      Item: Is essential imaging displayed? (Yes / Not applicable)
      S1: US ± MRCP
      S2: US/CT
      If no: Display, repeat item
      Source: WHO 2009
  * SIGN OUT
      Item: Nurse verbally confirms the name of the procedure
      S1: —
      S2: —
      If no: —
      Source: WHO 2009
  * SIGN OUT
      Item: Nurse verbally confirms completion of instrument, sponge and needle counts
      S1: —
      S2: —
      If no: Search, X-ray per local, escalate
      Source: WHO 2009
  * SIGN OUT
      Item: Nurse verbally confirms specimen labelling (read labels aloud incl. patient name)
      S1: Gallbladder
      S2: Appendix
      If no: —
      Source: WHO 2009
  * SIGN OUT
      Item: Nurse verbally confirms whether there are equipment problems to be addressed
      S1: —
      S2: —
      If no: —
      Source: WHO 2009
  * SIGN OUT
      Item: Surgeon, anaesthetist and nurse: key concerns for recovery and management
      S1: —
      S2: —
      If no: —
      Source: WHO 2009
  * EXTENSION
      Item: VTE prophylaxis confirmed
      Source: ProcSense educational extension (NICE NG89; NPSA 2010)
  * EXTENSION
      Item: Capillary glucose recorded for patients with diabetes
      Source: ProcSense educational extension (CPOC)
  * EXTENSION
      Item: Patient temperature ≥36.0 °C before induction
      Source: ProcSense educational extension (NICE CG65)
  * EXTENSION
      Item: Pregnancy status documented where relevant
      Source: ProcSense educational extension (NICE NG45)
  * EXTENSION
      Item: Team brief before the list / debrief after
      Source: ProcSense educational extension (NPSA 2010)

  [TABLE] Role | Behaviour
  * Ward / ED nurse
      Behaviour: Vitals, gives medicines, checklists; reports numbers; asks decisions; CHALLENGES unsafe
          orders
  * Triage nurse (S2)
      Behaviour: First summary and urgency
  * Senior surgeon
      Behaviour: Phone/arrival; confirms diagnosis and decision to operate; leads deferral
  * Anaesthetist
      Behaviour: Fitness, ASA confirmation, airway/fasting, may delay; leads Sign In
  * Gynaecologist / obstetric team
      Behaviour: Positive pregnancy with pain; pregnant patients
  * Laboratory staff
      Behaviour: Phone critical results
  * Radiologist / sonographer
      Behaviour: Scan reports; queries CT in pregnancy
  * Porter
      Behaviour: Moves patient
  * Theatre reception nurse
      Behaviour: Identity, consent, allergies, fasting, site
  * Scrub nurse / ODP / circulating
      Behaviour: Theatre prep, counts, sterility
  * Recovery nurse
      Behaviour: Recovery handover (Step 15)
  * Pharmacist / microbiology
      Behaviour: Allergy-alternative antibiotics
  * Gastroenterology / ERCP team
      Behaviour: Cholangitis drainage
  * Medical / cardiology team
      Behaviour: New AF, STEMI, hyperkalaemia emergencies
  * Critical care / emergency team
      Behaviour: NEWS2 ≥7, shock
  * Diabetes team
      Behaviour: Hypo/DKA/poor control
  * Interpreter (trained)
      Behaviour: Language barrier; not child relatives
  * Relative / parent / guardian
      Behaviour: Minor consent; history
  * Admissions
      Behaviour: Identity mismatch
```


# ARCHITECTURE, AI, TEAM AND SCHEDULE


## 25. SYSTEM ARCHITECTURE

```text
 VR USER (Meta Quest 2/3, controllers; voice P1)
   |
 VR ENVIRONMENT (Unity 6 URP, OpenXR + Meta XR Core, XR Interaction Toolkit)
   rooms: ward bay | ED cubicle | corridor | ultrasound/X-ray (or fade) | holding bay | operating theatre
   |
 INTERACTION SYSTEM (XRI grab/socket/poke, tablet UI, ActionEmitters)  --SimAction-->
   |
 SIMULATION HOST (Unity main thread: stamps time, queues actions, ticks Core, publishes StateViews)
   |
 CLINICAL SCENARIO ENGINE = ProcSense.Core (pure C#, offline, deterministic)
   |-- Case Graph (15 Steps + S1/S2 20-stage maps; DecisionOutcome; end states; hidden facts)
   |-- Workflow/State Engine: PatientState {Profile, Clinical, Physiological, Behavioural, Simulation, Observable}
   |-- Subsystems: Location/Transport · Investigations/Results · Treatments/Checks · Equipment/OR readiness ·
   |               SafetyChecklist · Communication/Handover · Behaviour · Staff/Environment
   |-- Rule Engine (JsonLogic subset, provenance) · Physiology (LitePhysio P0 / Pulse P1)
   |-- Event Director + What-if branches · Decision & priority engine (problems, ladder, clashes) · NEWS2/sepsis engine · Scorer (end states) · SessionLog
   |
 AI COMPONENTS (never write clinical state; all validated + audited)
   AI Behaviour Layer (state-aware, read-only DTO → validated intents) · LLM phrasing (patient/staff) · ASR · TTS ·
   intent/communication classifier → evidence mapper · AI debrief · Stage A scenario proposal (offline) · retrieval (P2)
 SCENARIO COMPOSER (offline): approved blocks → constraint solver → Stage B validation → reviewed case cards
   |
 DATA LAYER: content package generated from Walkthrough v5.1 (evidence types A–D, institutional placeholders) · voice-line catalogue · source register
   |
 EVALUATION / FEEDBACK: score items with evidence -> template debrief (P0) -> AI rewrite + validator (P1)
   |
 LOGGING / ANALYTICS: JSONL session log on device -> optional server (FastAPI + SQLite) reports
```


### Action pipeline

```text
 Controller/interactor -> ActionEmitter -> SimulationHost -> Core.Apply()
   -> 1 validate (patient here? identity verified? prerequisites?)  -> reject with reason (logged)
   -> 2 ClinicalState update (facts revealed, tests requested, treatments ...)
   -> 2b Problem registry + DecisionOutcome re-evaluation
   -> 3 RuleEngine (required / contraindicated / timing / order / critical)
   -> 4 PhysiologyEffects -> IPhysiologyEngine.Apply/Advance
   -> 5 Behaviour model (anxiety, pain, understanding, trust ...)
   -> 6 EventDirector (eligible approved events, seeded)
   -> 7 Scorer (score items + evidence refs)
   -> 8 SessionLog (append)
   -> 9 ObservableState rebuild -> StateViews (monitor, avatar, tablet, rooms)
   -> 10 optional AI voice/phrasing -> TTS -> lip sync      -> END OF CASE: debrief
```


### Content types

```text
{
 "Step": [
  "n",
  "title",
  "21 answers",
  "player_view",
  "cards[]",
  "flow_refs",
  "equipment[]",
  "whatifs[]"
 ],
 "FlowStage": [
  "Medical detail",
  "Trainee action",
  "What the trainee sees / information",
  "Normal pathway",
  "Abnormal branches",
  "Escalation / NPC",
  "Equipment and monitor response",
  "If missed",
  "Source",
  "Clinician validation"
 ],
 "WhatIf": [
  "Trigger / what the trainee sees",
  "What it might mean",
  "What to check next",
  "Correct action",
  "Incorrect action",
  "Missed abnormality \u2192 consequence",
  "Escalation (who to call)",
  "Expected patient / monitor response",
  "What happens next (S1 / S2 consequence)",
  "Source",
  "Clinician validation"
 ],
 "StackedCase": [
  "id",
  "title",
  "sections 1\u201320 (intro, presentation, obs, history available/hidden, abnormalities, prioritisation, decision process, interactions, investigations, results, surgery, anaesthesia, escalation, correct path, incorrect paths, missed path, recovery path, debrief)"
 ],
 "Patient": [
  "id",
  "walkthrough_label",
  "scenario",
  "baseline obs",
  "hidden facts {fact, disclose_on}",
  "results {value, comment, quality_flag}",
  "expected end state"
 ],
 "LabResult": [
  "test",
  "value",
  "unit",
  "flag H/L",
  "lab_comment",
  "quality: OK|HAEMOLYSED|DRIP_ARM|EDTA|CLUMPED|MISLABELLED|REJECTED",
  "critical bool",
  "previous values[]",
  "turnaround (Type D)"
 ],
 "ContentItem (all)": [
  "id",
  "evidence_type A|B|C|D",
  "number_kind",
  "wt_ref",
  "review_status",
  "institutional_placeholder?"
 ],
 "Medicine": [
  "group",
  "dose_policy EDU_EXAMPLE|PATIENT_SPECIFIC|INSTITUTIONAL",
  "example_text+source",
  "three_checks"
 ],
 "Problem": [
  "type LIFE_THREAT|SAFETY_BLOCKER|TIME_CRITICAL|PATHWAY_CHANGING|OPTIMISABLE|INFORMATION_ONLY",
  "ladder_rank",
  "clock"
 ],
 "Outcome": [
  "SAFE",
  "INVESTIGATE",
  "OPTIMISE",
  "REVIEW",
  "EMERGENCY"
 ],
 "EndState": [
  "READY_SAFE",
  "POSTPONED_APPROPRIATELY",
  "REDIRECTED_APPROPRIATELY",
  "SAFE_BUT_DELAYED",
  "SAFETY_FAILURE"
 ]
}
```


## 26. AI ARCHITECTURE — CONTROLLED AI AROUND A DETERMINISTIC CORE

```text
ProcSense is NOT an LLM pretending to be a patient. It is a deterministic clinical simulation platform
enhanced by multiple controlled AI systems for natural interaction, adaptive behaviour, assessment, scenario
composition, synthetic-data generation and feedback.

  [TABLE] Owner | Scope
  * Deterministic engine OWNS
      Scope: Clinical state · simulation state · physiology · rules · events · consequences · scoring ·
          assessment facts · scenario validity
  * AI OWNS / ASSISTS WITH
      Scope: Natural language · speech interpretation · behavioural expression · scenario proposals · case
          composition within approved constraints · communication interpretation · adaptive presentation ·
          staff/patient dialogue · synthetic-data generation · debrief language · development-time content
          generation
```


### AI data flow

```text
                APPROVED CLINICAL CONTENT (building blocks, rules, events — doctor-reviewed)
                          |
                          v
             +-------------------------------+
             | Scenario Composer             |   Stage A: AI proposal (optional)
             | + Constraint Solver           |   Stage B: deterministic validation
             +---------------+---------------+
                             v
             +-------------------------------+
             | Validation / Grounding        |   schema · constraints · contradictions · rules · review status
             +---------------+---------------+
                             v
                 PATIENT CASE (structured card = authoritative; narrative = presentation only)
                             v
             +-------------------------------+
             | Deterministic Core            |   state · rules · Event Director · scoring
             | State / Rules / Events        |
             +---------------+---------------+
                             |
         +-------------------+-------------------+
         v                                       v
   AI Behaviour Layer                      Physiology Engine
   (read-only state view -> intents          (LitePhysio / Pulse)
    -> validated -> engine applies)                |
         +-------------------+-------------------+
                             v
                    SIMULATION OUTPUT
                             |
           +-----------------+-----------------+
           v                                   v
     Patient/Staff Dialogue + Voice      Environment Behaviour
     (LLM phrasing + TTS, validated)     (approved events, staff intents)
           |
           v
     Trainee Interaction (VR actions + speech)
           |
           v
     ASR / Action Extraction / Intent & Communication Classifier  ->  structured evidence
           |
           v
     Deterministic Evaluation (rules decide correctness)
           |
           v
     Session Log  ->  Scoring + Competencies (authoritative)
           |
           v
     Structured Debrief Facts  ->  AI DEBRIEF  ->  Grounding / Validation  ->  Final debrief
     (every AI call -> AI Audit Logger)
```


### Multi-model architecture (which parts may be AI, which MUST stay deterministic)

```text
  [TABLE] Component | Type | Responsibility | Where it runs | Fallback
  * 1 LLM
      Type: LLM
      Responsibility: Patient dialogue, staff dialogue, scenario proposal (Stage A), debrief wording,
          controlled content drafting
      Where it runs: Server GPU (P1); offline drafting on dev PC
      Fallback: Pre-rendered lines / templates
  * 2 ASR
      Type: ML model
      Responsibility: Trainee speech → text
      Where it runs: Server (P1); on-device spike P2
      Fallback: Tablet menu
  * 3 TTS
      Type: ML model
      Responsibility: Patient, staff, system voices
      Where it runs: Offline pre-render (P0); live (P1)
      Fallback: Subtitles only
  * 4 Intent/communication classifier
      Type: LLM-based (P1) → small classifier (P2)
      Responsibility: Communication acts, topics, SBAR elements, evidence spans
      Where it runs: Server (async)
      Fallback: Debrief-only / menu evidence
  * 5 Embeddings/retrieval
      Type: ML model (optional)
      Responsibility: Retrieve approved content for grounding and debrief evidence
      Where it runs: Server (P2)
      Fallback: Rule explain text
  * 6 Scenario Composer
      Type: DETERMINISTIC solver (+ optional LLM proposal)
      Responsibility: Constraint-based complete case composition
      Where it runs: Offline tool
      Fallback: Hand-authored cases
  * 7 Rule engine
      Type: MUST BE DETERMINISTIC
      Responsibility: Clinical/simulation correctness
      Where it runs: Quest
      Fallback: n/a
  * 8 Physiology engine
      Type: MUST BE DETERMINISTIC
      Responsibility: Physiological behaviour
      Where it runs: Quest
      Fallback: LitePhysio
  * 9 Event Director
      Type: MUST BE DETERMINISTIC
      Responsibility: Approved event selection
      Where it runs: Quest
      Fallback: n/a
  * 10 Adaptive Difficulty Engine
      Type: DETERMINISTIC selection (+ LLM explanation P1)
      Responsibility: Case/difficulty selection from approved pool
      Where it runs: Quest (selection)
      Fallback: Fixed curriculum order
  * 11 Grounding/Validation layer
      Type: MUST BE DETERMINISTIC
      Responsibility: Validate every AI output (schema, entities, disclosure, intents)
      Where it runs: Quest + server
      Fallback: Reject → fallback
  * 12 AI Audit Logger
      Type: Deterministic
      Responsibility: Model, version, prompt version, input context ID, output, validation result, latency,
          failure/fallback
      Where it runs: Server + on-device log
      Fallback: Local log
```


## 27. AI AUTHORITY AND SAFETY BOUNDARIES

```text
  AI MAY                                               | AI MAY NOT                                             
  -----------------------------------------------------+--------------------------------------------------------
  Generate natural-language responses                  | Directly modify authoritative patient state            
  Interpret trainee language                           | Directly modify physiological state                    
  Classify communication                               | Directly modify scores                                 
  Propose scenarios (Stage A)                          | Create unapproved clinical facts at runtime            
  Select from approved content                         | Introduce arbitrary medications/treatments             
  Generate natural variations                          | Invent investigation results                           
  Generate debrief wording                             | Invent diagnoses                                       
  Generate development/test data                       | Override deterministic clinical rules                  
  Adapt presentation/difficulty within approved limits | Bypass validation                                      
                                                       | Make assessment decisions outside defined scoring rules
                                                       | Load unapproved clinical content into assessment mode  

How the boundary is enforced:

  - AI clients receive read-only DTOs; no API mutates Core state
  - All AI output is JSON-schema constrained and passes the grounding/validation layer
  - Behavioural intents are applied only through the approved transition table (bounded)
  - Scores come only from deterministic rules; AI evidence counts only above threshold and never for critical
    items
  - Loader refuses unapproved content in assessment mode; AI-proposed cases stop at IMPLEMENTED
  - Every AI call audited; fallback always available
```


## 28. AI-ASSISTED COMPLETE CASE COMPOSITION (SCENARIO COMPOSER)

```text
The composer builds COMPLETE simulation cases from approved building blocks — not just narratives. The
structured patient card is authoritative; the AI narrative is only a presentation layer.

 Approved clinical building blocks → constraint/compatibility checking → Scenario Composer → contradiction detection
   → clinical-rule validation → approved complete patient case → deterministic simulation engine

  [TABLE] Building block | ID family | Compatibility constraints
  * Demographics / age band / sex (where clinically relevant)
      ID family: DEM-*
      Compatibility constraints: age band ↔ comorbidity prevalence; sex ↔ pregnancy eligibility
  * Surgical scenario
      ID family: SCN-S1 / SCN-S2
      Compatibility constraints: defines allowed diagnoses, tests, OR set
  * Presenting complaint + HPC
      ID family: PRES-*
      Compatibility constraints: must match diagnosis block
  * Past medical history / comorbidities
      ID family: CMB-*
      Compatibility constraints: requires/excludes tags; drives tests (NG45)
  * Previous surgeries
      ID family: PSH-*
      Compatibility constraints: e.g. PSH-APPENDECTOMY excludes SCN-S2
  * Allergies (+ reaction class)
      ID family: ALG-*
      Compatibility constraints: excludes treatment options of that class
  * Current medications
      ID family: MED-*
      Compatibility constraints: each requires its indication comorbidity
  * Family / social history
      ID family: SOC-*
      Compatibility constraints: day-case eligibility, anaesthetic family history
  * Baseline observations envelope
      ID family: OBS-*
      Compatibility constraints: consistent with severity + physiology
  * Clinical/exam findings
      ID family: FND-*
      Compatibility constraints: consistent with diagnosis and severity
  * Risk factors
      ID family: RSK-*
      Compatibility constraints: consistent with comorbidities
  * Laboratory findings
      ID family: LAB-*
      Compatibility constraints: pattern ↔ diagnosis; values within reviewed bands
  * Imaging findings/reports
      ID family: IMG-*
      Compatibility constraints: ↔ diagnosis; adult S2 text only
  * Pre-operative concerns
      ID family: CON-*
      Compatibility constraints: patient questions pool
  * Communication/personality profile
      ID family: PER-*
      Compatibility constraints: anxiety, health literacy, cooperation, question frequency
  * Pain/discomfort state
      ID family: PAIN-*
      Compatibility constraints: ↔ diagnosis & severity
  * Difficulty level
      ID family: DIFF-1..3
      Compatibility constraints: limits number/complexity of blocks + events
  * Approved complications/events
      ID family: EVT-*
      Compatibility constraints: eligibility rules must be satisfiable
  * Environment conditions
      ID family: ENV-*
      Compatibility constraints: rooms available, time of day, interruptions
  * Staff availability
      ID family: STAFF-*
      Compatibility constraints: porter delay, anaesthetist availability
  * Time/workflow constraints
      ID family: TIME-*
      Compatibility constraints: OT slot, urgency deadlines (S2)
```


### Contradiction detection

```text
  [TABLE] Contradiction | Example | Action
  * Incompatible demographics
      Example: Pregnancy-positive block with male sex or outside age band
      Action: Reject
  * Inconsistent history
      Example: PSH-APPENDECTOMY in an S2 appendicitis case; PSH-CHOLECYSTECTOMY in S1
      Action: Reject
  * Impossible investigation combination
      Example: US-GALLSTONES-NORMAL-DUCT together with MRCP-CBD-STONE and normal LFT without explanation
      Action: Reject / flag [CLINICAL VALIDATION REQUIRED]
  * Allergy vs medication
      Example: ALG-PENICILLIN with MED-AMOXICILLIN current; beta-lactam antibiotic option pre-selected
      Action: Reject
  * Findings vs diagnosis
      Example: Right-upper-quadrant findings in an appendicitis case with no alternative explanation
      Action: Reject
  * Impossible physiological state
      Example: SpO2 envelope below survivable range with 'calm, alert' persona; HR/BP pair outside physiology
          model
      Action: Reject
  * Inconsistent severity
      Example: S2 'uncomplicated' severity with perforation imaging + sepsis observations
      Action: Reject
  * Inconsistent surgical indication
      Example: Asymptomatic gallstones in normal gallbladder listed for elective surgery (CG188 1.2.1) without
          symptoms block
      Action: Reject [CLINICAL VALIDATION REQUIRED]
  * Results vs clinical state
      Example: Normal WBC/CRP with 'sepsis progression' event and fever envelope
      Action: Reject / flag
  * Ineligible events
      Example: E-GLUCOSE-LOW selected for a non-diabetic; E-HYPERSENSITIVITY with no allergy block
      Action: Reject
  * Medication without indication
      Example: MED-METFORMIN without CMB-T2DM
      Action: Reject
  * Disclosure impossibility
      Example: Hidden fact whose disclose_on topic does not exist in the Ask menu
      Action: Reject
```


### AI-generated scenarios are complete scenarios (two-stage)

```text
Stage A — AI proposal: the LLM proposes a full configuration (surgical case, profile, complaint, history, risk
factors, allergies, medications, baseline state, investigation requirements/results, personality,
communication behaviour, difficulty, staff and environment configuration, eligible events, expected competency
opportunities) using ONLY block IDs from the approved library.

Stage B — deterministic validation:

  1. Schema valid (JSON Schema)
  2. All required fields present (per scenario template)
  3. Clinical constraints: every block's requires/excludes satisfied
  4. Contradiction detector: all rules above pass
  5. Allowed combinations: only approved block IDs, only approved statuses
  6. Difficulty within target tier (block + event count limits)
  7. Event eligibility: each selected event can actually trigger in this configuration
  8. Investigation consistency: every result block pairs with an orderable test + diagnosis
  9. Patient-state consistency: physiology initialises inside model bounds; headless 5-minute idle run stable
  10. Scoring compatibility: every goal achievable; golden 'correct' bot run reaches ready-stable
  11. Status: AI-proposed cases stop at IMPLEMENTED until a named reviewer approves

  ! [Assessment mode] UNAPPROVED AI-GENERATED SCENARIOS MUST NEVER DIRECTLY ENTER RUNTIME. Only reviewer-
    approved cases enter the approved pool.
```


### Example structured case card

```text
{ "case_id": "S1-C017", "synthetic": true, "scenario": "SCN-S1", "difficulty": "DIFF-2",
  "blocks": ["DEM-F-40-55","PRES-BILIARY-COLIC","CMB-T2DM","CMB-HTN","MED-METFORMIN","MED-AMLODIPINE",
             "ALG-PENICILLIN-RASH(hidden)","SOC-LIVES-WITH-FAMILY","OBS-S1-STABLE","FND-RUQ-MILD","LAB-HBA1C-HIGH",
             "LAB-LFT-NORMAL","IMG-US-GALLSTONES-NORMAL-DUCT","CON-SCAR","PER-ANXIOUS-HIGHLIT","PAIN-MILD",
             "EVT-ANAPHYLAXIS","EVT-INSUFFLATOR-FAULT","ENV-STD","STAFF-PORTER-DELAY-LOW","TIME-OT-SLOT-AM"],
  "hidden_facts": {"last_oral_intake": {"value":"tea_with_milk_3h","disclose_on":["ask:last_drink"]}},
  "narrative_ref": "S1-C017.narrative.v1",           <- presentation only (AI-drafted, reviewed)
  "validation": {"stageB": "PASS", "run_id": "…"}, "review": {"status": "IMPLEMENTED", "reviewer": null} }
```


## 29. AI-ASSISTED ASSESSMENT ACROSS THE WHOLE WORKFLOW

```text
Communication is one of many assessed behaviours. The LLM never decides clinical correctness: deterministic
rules define expected behaviour; AI only interprets natural language into structured evidence.

 Trainee speech ─► ASR (transcript + timings + confidence)
   ─► Intent / communication classifier (acts, topic, SBAR element, evidence span, confidence)
   ─► Evidence mapper (deterministic): confidence ≥ threshold? act allowed in this context? critical item? → Evidence record
   ─► Deterministic competency/scoring rules (decide correctness)  ─► Score item (with evidence refs)
 Trainee VR/tablet action ─► ActionEmitter ─► Core ─► rule hits ─► Score item   (deterministic evidence)
 Critical items (identity, allergy, consent, handover critical elements) always require deterministic evidence.

  [TABLE] Behaviour assessed | Steps | Deterministic evidence | AI-interpreted evidence | Deciding rule
  * Asks relevant questions
      Steps: 2
      Deterministic evidence: Ask menu topics / speech→topic
      AI-interpreted evidence: Classifier maps question→topic
      Deciding rule: Required-topic rules per patient
  * Identifies important patient information
      Steps: 2,7
      Deterministic evidence: Facts disclosed + acknowledged on tablet
      AI-interpreted evidence: —
      Deciding rule: Fact-acknowledged rules
  * Clarifies unclear information
      Steps: 2,7
      Deterministic evidence: 'Clarify' act; follow-up question on vague answer
      AI-interpreted evidence: Classifier: CLARIFY act
      Deciding rule: Vague-answer follow-up rule
  * Responds to patient concerns/questions
      Steps: 2,9
      Deterministic evidence: answer_question(q,a)
      AI-interpreted evidence: Classifier: ANSWER_QUESTION + topic match
      Deciding rule: Pending question must be answered within window
  * Explains procedures/tests
      Steps: 6,9
      Deterministic evidence: EXPLAIN_* acts
      AI-interpreted evidence: Classifier: EXPLAIN_* + topic
      Deciding rule: Explain-before-action rule
  * Explains before performing an interaction
      Steps: 3–4,6,8
      Deterministic evidence: say(EXPLAIN) precedes action in log
      AI-interpreted evidence: Classifier
      Deciding rule: Order rule (explain → act)
  * Checks understanding
      Steps: 9
      Deterministic evidence: CHECK_UNDERSTANDING act
      AI-interpreted evidence: Classifier (teach-back detection)
      Deciding rule: Consent rule element
  * Responds appropriately to anxiety
      Steps: 2,8,9
      Deterministic evidence: ACKNOWLEDGE_EMOTION/REASSURE after anxiety cue
      AI-interpreted evidence: Classifier; inappropriate-reassurance flag (debrief)
      Deciding rule: Response-window rule on anxiety cue
  * Notices clinically relevant info from patient
      Steps: 2,3–4
      Deterministic evidence: Acknowledge fact / action taken
      AI-interpreted evidence: Classifier detects acknowledgement in speech
      Deciding rule: Disclosed-fact follow-up rules (e.g. intake → inform anaesthetist)
  * Communicates with nurses/staff
      Steps: 6,8,11
      Deterministic evidence: call_staff, closed-loop confirm
      AI-interpreted evidence: Classifier: CLOSED_LOOP
      Deciding rule: Order read-back rule
  * Communicates during handover/transfer
      Steps: 12,15
      Deterministic evidence: SBAR form / speech
      AI-interpreted evidence: Classifier maps utterances→SBAR elements
      Deciding rule: Required-element + critical-omission rules
  * Gives appropriate instructions
      Steps: 8,10,12
      Deterministic evidence: Order/instruction actions
      AI-interpreted evidence: Voice-order parser
      Deciding rule: Instruction-validity rules (formulary, state)
  * Performs required checks
      Steps: 1,8,10,13,14
      Deterministic evidence: Check actions
      AI-interpreted evidence: —
      Deciding rule: Required-check rules
  * Responds to abnormal findings
      Steps: 3–4,7
      Deterministic evidence: interpret + decision
      AI-interpreted evidence: —
      Deciding rule: Finding→action rules
  * Responds to unexpected events
      Steps: 3–4,6,8,11,12
      Deterministic evidence: Response actions within window
      AI-interpreted evidence: Speech escalation via classifier
      Deciding rule: Event response rules
  * Recognises need for more information
      Steps: 5,7
      Deterministic evidence: order test / ask / chase
      AI-interpreted evidence: —
      Deciding rule: Missing-information rules
  * Follows workflow & sequence
      Steps: all
      Deterministic evidence: Action order in log
      AI-interpreted evidence: —
      Deciding rule: Order/hard-edge rules
  * Notices environmental/safety problems
      Steps: 11,13
      Deterministic evidence: report_fault, stop
      AI-interpreted evidence: Speech 'report' via classifier
      Deciding rule: Fault-response rules
  * Responds when information is missing
      Steps: 2,6,10
      Deterministic evidence: Clarify/escalate
      AI-interpreted evidence: Classifier
      Deciding rule: Missing-info rules
  * Escalates / seeks assistance
      Steps: 3–4,7,8
      Deterministic evidence: escalate(role, SBAR)
      AI-interpreted evidence: Classifier SBAR completeness (non-critical)
      Deciding rule: Escalation-trigger rules
  * Completes preparation & safety checks
      Steps: 8–14
      Deterministic evidence: Checklist + treatment actions
      AI-interpreted evidence: —
      Deciding rule: Readiness blockers
  * Responds to changing patient/environment state
      Steps: 3–4,8,12
      Deterministic evidence: Actions after state change
      AI-interpreted evidence: —
      Deciding rule: Time-window rules
```


## 30. STATE-AWARE AI & REACTIVE LOOP

```text
AI components receive a READ-ONLY, disclosure-aware view of the simulation. AI output is interpreted by
deterministic systems; it never changes truth directly.

  [TABLE] Context field | Meaning | Given to
  * current_stage / phase
      Meaning: Workflow position
      Given to: Patient, staff
  * completed_actions / missing_actions
      Meaning: What has / hasn't happened
      Given to: Staff (e.g. nurse asks 'is consent done?')
  * disclosed_facts
      Meaning: Facts the trainee has already learned
      Given to: Patient, staff, debrief
  * disclosable_facts (+ disclose_on)
      Meaning: Facts the character may reveal if asked
      Given to: Patient only
  * told_to_patient
      Meaning: What the trainee has explained to the patient
      Given to: Patient (avoids asking already-answered questions)
  * behaviour_state
      Meaning: Anxiety, trust, understanding, pain, fear, frustration, confusion …
      Given to: Patient
  * observable_physiology
      Meaning: Only perceivable signs (pale, sweating), not raw truth unless on monitor
      Given to: Patient, staff
  * active_events
      Meaning: Approved events currently running + their communication intents
      Given to: Patient, staff
  * conversation_context
      Meaning: Last N turns + previous patient responses
      Given to: Patient, staff
  * environment_state
      Meaning: Location, interruptions, equipment faults
      Given to: Staff
  * staff_state
      Meaning: Availability, pending requests
      Given to: Staff
  * revealed_results
      Meaning: Results released + acknowledged
      Given to: Staff, debrief
  * difficulty
      Meaning: Current tier (style only)
      Given to: Patient, staff

 Trainee action / speech
   ↓
 Deterministic simulation state updated (Core)
   ↓
 AI receives READ-ONLY state/context DTO (only what that character may know / say)
   ↓
 AI produces a behavioural intent + natural-language realisation (JSON schema)
   ↓
 Deterministic validator (intent allowed? facts grounded? disclosure allowed?)
   ├─ valid  → approved intent → Core applies bounded state change (if any) → presentation (voice, animation)
   └─ invalid → fallback (pre-rendered line / template) → presentation
 Example: AI suggests intent PATIENT_ANXIETY_RISES. It does NOT set anxiety. The Behaviour Model checks the
 approved transition table (e.g. unexplained action ×2 → anxiety +1, bounded 0–10) and applies it.
```


## 31. PATIENT PERSONA & BEHAVIOUR REALISATION

```text
  [TABLE] Attribute | Range | Driven by
  * Anxiety
      Range: 0–10
      Driven by: Explanation/reassurance ↓; unexplained actions, waits ↑
  * Trust
      Range: 0–10
      Driven by: Introduction, honesty ↑; dismissive, ignoring questions ↓
  * Cooperation
      Range: 0–10
      Driven by: Trust/understanding ↑; dismissive ↓ → may refuse cannulation until reassured
  * Understanding
      Range: 0–10
      Driven by: Plain language, teach-back ↑; jargon ↓
  * Communication style
      Range: enum (talkative, terse, anxious-questioning, stoic)
      Driven by: Persona constant
  * Pain/discomfort
      Range: 0–10
      Driven by: Driven by physiology + analgesia
  * Fear
      Range: 0–10
      Driven by: Consent risks discussed without support ↑; reassurance ↓
  * Frustration
      Range: 0–10
      Driven by: Repeated questions, long waits ↑
  * Confusion
      Range: 0–3
      Driven by: Physiology-driven (hypoglycaemia/hypoxia/sepsis) + jargon
  * Willingness to answer
      Range: 0–10
      Driven by: Trust-gated disclosure of sensitive facts
  * Question frequency
      Range: persona constant
      Driven by: How often the patient initiates questions
  * Need for reassurance
      Range: persona constant
      Driven by: Scales anxiety response to events
  * Communication difficulty
      Range: enum (language preference, hearing)
      Driven by: Educationally appropriate only; [CLINICAL VALIDATION REQUIRED]

  [TABLE] Trainee behaviour / event | Bounded transition (approved table)
  * Appropriate explanation before action
      Bounded transition (approved table): understanding +1, trust +1
  * Poor communication / jargon
      Bounded transition (approved table): understanding −1, confusion ↑ (if already low)
  * Repeated unexplained actions (×2)
      Bounded transition (approved table): anxiety +1 → patient asks 'What are you doing?' (question intent)
  * Dismissive reply to a question
      Bounded transition (approved table): trust −2, cooperation −1
  * Reassurance + clear explanation
      Bounded transition (approved table): anxiety −1…−2 (diminishing), fear −1
  * Long unexplained wait
      Bounded transition (approved table): anxiety +1, frustration +1 per interval
  * Approved event fires (e.g. anaphylaxis)
      Bounded transition (approved table): fear +2, patient expresses symptom via AI realisation
  * Analgesia effect
      Bounded transition (approved table): pain tracks physiology; irritability ↓

  ! [Bounds] Magnitudes are simulation design values reviewed for plausibility. [CLINICAL VALIDATION REQUIRED]
    The LLM realises a state in words; it cannot create clinical consequences.
```


## 32. AI STAFF & ENVIRONMENT INTERACTION

```text
  [TABLE] Role | Approved intents (AI phrases only these)
  * Nurse
      Approved intents (AI phrases only these): ASK_CLARIFICATION, CONFIRM_ORDER (read-back),
          REPORT_OBSERVATION, ASK_PREP_STATUS, REPORT_PATIENT_REQUEST (e.g. water)
  * Anaesthesia staff
      Approved intents (AI phrases only these): ASK_FASTING_ALLERGY_AIRWAY, RAISE_APPROVED_CONCERN,
          RESPOND_TO_ESCALATION, SIGN_IN_ANSWER
  * Surgeon
      Approved intents (AI phrases only these): REQUEST_UPDATE, DECISION_RESPONSE (approved), TIME_OUT_ANSWER,
          APPROVED_INTRAOP_ISSUE (narration)
  * Porter
      Approved intents (AI phrases only these): CONFIRM_TRANSFER, REPORT_DELAY, ASK_MONITORING
  * Lab/imaging staff
      Approved intents (AI phrases only these): REPORT_APPROVED_RESULT, REPORT_SAMPLE_REJECTED, REPORT_DELAY
  * Scrub/circulating nurse
      Approved intents (AI phrases only these): REPORT_MISSING_ITEM, REPORT_FAULT, CONFIRM_READY, COUNT_STATUS
  * Ward/reception staff (if required)
      Approved intents (AI phrases only these): CONFIRM_ADMISSION_DETAILS, REPORT_BED_STATUS

Staff interactions also produce assessment evidence: e.g. trainee handover → ASR → classifier extracts SBAR
elements → deterministic evaluator checks required elements → scorer records evidence.
```


## 33. AI + UNEXPECTED EVENTS

```text
 Approved Event Library → eligibility rules → Event Director → selected event → deterministic state change
   → AI Behaviour Layer → patient/staff natural-language response (approved event communication intents + approved facts only)

AI never creates emergencies at runtime; it only decides HOW an approved event is voiced. Assessment mode uses
a fixed event schedule, so runs stay reproducible.
```


## 34. ADAPTIVE DIFFICULTY

```text
Inputs (structured evidence from previous sessions):

  - Competency performance (per category)
  - Communication performance (deterministic + validated classifier evidence)
  - Workflow completion
  - Error patterns (repeated missed checks)
  - Response to unexpected events (time-to-response)
  - Time/efficiency
  - Handover quality
  - Recognising missing information
  - Responding to patient concerns

  [TABLE] Difficulty dimension | How it is realised (approved variants only)
  * More complex history
      How it is realised (approved variants only): More comorbidity/medication blocks (approved combos)
  * More communication challenges
      How it is realised (approved variants only): Personas with low literacy, high anxiety, terse style
  * More competing priorities
      How it is realised (approved variants only): Parallel tasks (porter delay + results + patient question)
  * More environmental interruptions
      How it is realised (approved variants only): ENV/STAFF blocks: phone, theatre calls for the patient (WT
          Step 12)
  * More information requiring clarification
      How it is realised (approved variants only): More hidden facts, inconsistent chart vs patient
  * Approved unexpected events
      How it is realised (approved variants only): Higher event count/tier
  * More complex handover
      How it is realised (approved variants only): More required elements (abnormal results, uncertainty)
  * Higher cognitive workload
      How it is realised (approved variants only): Tighter (reviewed) time constraints

The engine selects approved variants inside the clinically approved scenario space; it never invents
conditions. Disabled in assessment mode.
```


## 35. AI DEBRIEF FROM SESSION EVIDENCE

```text
  - Actions performed / omitted
  - Errors (with rule IDs)
  - Timing (response windows)
  - Communication evidence (quoted spans, confidence)
  - Patient & staff interactions
  - Investigation decisions (appropriate / unnecessary / missed)
  - Preparation completion
  - Handover evidence
  - Event responses
  - Score breakdown + competency results
  - Relevant simulation events

 Session Log ─► Deterministic Scorer ─► Structured Debrief Facts (item IDs, verdicts, evidence refs, sources)
   ─► AI Debrief Generator (LLM, JSON: sections referencing fact IDs only) ─► Grounding Validator
        (every fact ID exists · every number/verdict preserved · no new events/mistakes · no treatment advice beyond rule text)
   ─► Final debrief   |  fail → template debrief (P0)
```


## 36. GENERAL SYNTHETIC-DATA PIPELINE

```text
Synthetic data never silently becomes clinical truth: every dataset has a card and a runtime/dev label.

  [TABLE] Dataset | Purpose | Generator | Source data | Method | Constraints | Validation | Human/clinical review | Labels | Intended model/use | Runtime? | Dev only?
  * Patient profiles / histories / case variants
      Purpose: Runtime cases after review
      Generator: Composer (solver) + LLM narrative
      Source data: Approved blocks
      Method: Constraint solving
      Constraints: requires/excludes, contradiction rules
      Validation: Stage B + reviewer
      Human/clinical review: YES (100% for assessment)
      Labels: —
      Intended model/use: Scenario library
      Runtime?: Runtime (approved only)
      Dev only?: —
  * Investigation-result combinations
      Purpose: Result variety
      Generator: Rule-based sampler
      Source data: Reviewed bands + local ranges
      Method: Sampling within bands
      Constraints: Pattern ↔ diagnosis
      Validation: Range + consistency tests
      Human/clinical review: YES
      Labels: finding codes
      Intended model/use: ResultGenerator
      Runtime?: Runtime (approved)
      Dev only?: —
  * Imaging-report variants
      Purpose: Report text
      Generator: LLM drafting tool
      Source data: Finding codes, SRC-18/19/20 facts
      Method: Constrained generation
      Constraints: Entities ⊆ finding codes
      Validation: Entity check + reviewer edit
      Human/clinical review: YES
      Labels: —
      Intended model/use: Results viewer
      Runtime?: Runtime (approved)
      Dev only?: —
  * Patient questions & responses
      Purpose: Dialogue pools
      Generator: LLM drafting tool
      Source data: Scenario context
      Method: Constrained generation
      Constraints: Only approved topics
      Validation: Validator + surgeon answers
      Human/clinical review: YES (answers)
      Labels: topic tags
      Intended model/use: Patient dialogue
      Runtime?: Runtime (approved)
      Dev only?: —
  * Staff dialogue variants
      Purpose: Natural variation
      Generator: LLM drafting tool
      Source data: Approved intents
      Method: Paraphrase
      Constraints: Intent preserved
      Validation: Validator + spot check
      Human/clinical review: Light
      Labels: intent tag
      Intended model/use: Staff dialogue / TTS
      Runtime?: Runtime (approved)
      Dev only?: —
  * Handover examples (good/poor)
      Purpose: Classifier + eval
      Generator: LLM + team role-play
      Source data: SBAR element lists
      Method: Generate with controlled omissions
      Constraints: Known omissions
      Validation: Human labels
      Human/clinical review: Labels by team
      Labels: SBAR element labels
      Intended model/use: Classifier
      Runtime?: No
      Dev only?: Dev only
  * Trainee questions/explanations/communication examples
      Purpose: Classifier training/eval
      Generator: LLM + role-play
      Source data: Act taxonomy
      Method: Generate per act
      Constraints: Balanced classes
      Validation: 2 labellers, kappa
      Human/clinical review: No (not clinical)
      Labels: act labels
      Intended model/use: Classifier
      Runtime?: No
      Dev only?: Dev only
  * Miscommunication examples
      Purpose: Negative cases
      Generator: LLM
      Source data: Taxonomy INAPPROPRIATE
      Method: Generate
      Constraints: —
      Validation: Human labels
      Human/clinical review: No
      Labels: labels
      Intended model/use: Classifier
      Runtime?: No
      Dev only?: Dev only
  * Behavioural responses
      Purpose: Test behaviour transitions
      Generator: Rule-based
      Source data: Transition table
      Method: Enumerate
      Constraints: Bounds
      Validation: Unit tests
      Human/clinical review: Magnitudes [CLINICAL VALIDATION REQUIRED]
      Labels: —
      Intended model/use: Behaviour model tests
      Runtime?: No
      Dev only?: Dev only
  * Event combinations
      Purpose: Event Director testing
      Generator: Rule-based enumerator
      Source data: Event library
      Method: Combinatorial
      Constraints: Eligibility
      Validation: Golden runs
      Human/clinical review: YES (for approved combos)
      Labels: —
      Intended model/use: Event Director
      Runtime?: Runtime (approved)
      Dev only?: —
  * Environmental scenarios
      Purpose: Environment variety
      Generator: Composer
      Source data: ENV/STAFF/TIME blocks
      Method: Constraint solving
      Constraints: Plausibility
      Validation: Stage B
      Human/clinical review: Light
      Labels: —
      Intended model/use: Scenarios
      Runtime?: Runtime (approved)
      Dev only?: —
  * Difficulty variants
      Purpose: Adaptive difficulty pool
      Generator: Composer
      Source data: Difficulty dimensions
      Method: Constraint solving
      Constraints: Tier limits
      Validation: Stage B + reviewer
      Human/clinical review: YES
      Labels: tier label
      Intended model/use: Difficulty engine
      Runtime?: Runtime (approved)
      Dev only?: —
  * Structured assessment examples
      Purpose: Scorer/debrief testing
      Generator: Scripted bots
      Source data: Rubric
      Method: Golden playthroughs
      Constraints: Expected ScoreReports
      Validation: Tests
      Human/clinical review: Rubric weights [CLINICAL VALIDATION REQUIRED]
      Labels: expected items
      Intended model/use: Scorer, debrief
      Runtime?: No
      Dev only?: Dev only
  * Physiological trajectories
      Purpose: Physiology realism checks
      Generator: Statistical fit
      Source data: VitalDB (CC BY 4.0)
      Method: Envelope + AR(1) noise
      Constraints: Model bounds
      Validation: Plot review
      Human/clinical review: YES (plots)
      Labels: —
      Intended model/use: LitePhysio
      Runtime?: Runtime (parameters)
      Dev only?: —
  * Edge cases
      Purpose: Robustness
      Generator: Rule-based + LLM
      Source data: Templates
      Method: Boundary values
      Constraints: —
      Validation: Tests
      Human/clinical review: No
      Labels: —
      Intended model/use: All validators
      Runtime?: No
      Dev only?: Dev only
  * Contradiction cases / invalid scenarios
      Purpose: Validator testing
      Generator: Mutation generator
      Source data: Valid cases
      Method: Inject contradictions
      Constraints: Known defects
      Validation: Must be rejected
      Human/clinical review: No
      Labels: defect label
      Intended model/use: Stage B validator
      Runtime?: No
      Dev only?: Dev only
  * Adversarial dialogue questions
      Purpose: Hallucination testing
      Generator: LLM + team
      Source data: Patient cards
      Method: Generate off-card questions
      Constraints: —
      Validation: Automated grounding check
      Human/clinical review: No
      Labels: expected refusal
      Intended model/use: Dialogue eval
      Runtime?: No
      Dev only?: Dev only
  * Speech samples (voice orders, accents)
      Purpose: ASR evaluation
      Generator: TTS + consented recordings
      Source data: Order phrases
      Method: Render/record
      Constraints: —
      Validation: WER
      Human/clinical review: No
      Labels: transcripts
      Intended model/use: ASR
      Runtime?: No
      Dev only?: Dev only
```


## 37. AI REQUIREMENTS MATRIX

```text
  [TABLE] Feature | Owner | Purpose | Runtime/Dev | AI/Non-AI | Input | Output | Authoritative source | Validation | Clinical review | Offline | Assessment mode? | Fallback | Priority | Definition of Done
  * Deterministic rule engine
      Owner: R
      Purpose: Correctness
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI
      Input: State+action
      Output: Rule hits
      Authoritative source: Rules (reviewed)
      Validation: Vectors+golden
      Clinical review: YES
      Offline: YES
      Assessment mode?: YES
      Fallback: n/a
      Priority: P0
      Definition of Done: All rules tested
  * Scenario Composer (solver)
      Owner: M
      Purpose: Complete valid cases
      Runtime/Dev: Dev
      AI/Non-AI: Non-AI
      Input: Blocks, template
      Output: Case card
      Authoritative source: Card + blocks
      Validation: Stage B
      Clinical review: YES
      Offline: YES
      Assessment mode?: Approved only
      Fallback: Hand-authored cases
      Priority: P0
      Definition of Done: ≥8 S1/≥6 S2 approved cases
  * Contradiction detector
      Owner: M
      Purpose: Reject impossible cases
      Runtime/Dev: Dev
      AI/Non-AI: Non-AI
      Input: Case card
      Output: Pass/defects
      Authoritative source: Rule list
      Validation: Mutation tests
      Clinical review: Rules YES
      Offline: YES
      Assessment mode?: n/a
      Fallback: n/a
      Priority: P0
      Definition of Done: All mutation cases rejected
  * AI scenario proposal (Stage A)
      Owner: R
      Purpose: Propose diverse cases
      Runtime/Dev: Dev
      AI/Non-AI: AI (LLM)
      Input: Library + targets
      Output: Candidate card
      Authoritative source: Stage B + reviewer
      Validation: Stage B
      Clinical review: YES
      Offline: Dev PC
      Assessment mode?: Only after approval
      Fallback: Solver sampling
      Priority: P1
      Definition of Done: ≥20 candidates, 0 invalid pass Stage B
  * Structured patient card
      Owner: M
      Purpose: Authoritative case
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI
      Input: Composer output
      Output: Card JSON
      Authoritative source: Card
      Validation: Schema
      Clinical review: YES
      Offline: YES
      Assessment mode?: YES
      Fallback: n/a
      Priority: P0
      Definition of Done: Loader strict mode
  * Narrative presentation
      Owner: R
      Purpose: Readable story
      Runtime/Dev: Dev→Runtime
      AI/Non-AI: AI (LLM)
      Input: Card
      Output: Text
      Authoritative source: Card
      Validation: Entity check
      Clinical review: Light
      Offline: YES (pre-made)
      Assessment mode?: Approved only
      Fallback: Template text
      Priority: P1
      Definition of Done: 0 entity violations
  * Patient Behaviour Model
      Owner: R
      Purpose: Bounded behaviour
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI
      Input: Actions, events
      Output: Attributes/modes
      Authoritative source: Transition table
      Validation: Unit tests
      Clinical review: Magnitudes
      Offline: YES
      Assessment mode?: YES
      Fallback: n/a
      Priority: P0
      Definition of Done: Transitions tested
  * State-aware AI context (read-only DTO)
      Owner: R
      Purpose: Responsive AI
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI
      Input: Core state
      Output: DTO
      Authoritative source: Core
      Validation: Read-only tests
      Clinical review: No
      Offline: YES
      Assessment mode?: YES
      Fallback: n/a
      Priority: P1
      Definition of Done: No mutation path; disclosure respected
  * Behaviour realisation (AI)
      Owner: R
      Purpose: Natural expression
      Runtime/Dev: Runtime
      AI/Non-AI: AI (LLM)
      Input: DTO + intent
      Output: Utterance + intent
      Authoritative source: Behaviour Model
      Validation: Grounding+intent validator
      Clinical review: Light
      Offline: Server
      Assessment mode?: YES (validated)
      Fallback: Pre-rendered line
      Priority: P1
      Definition of Done: 0 invented facts on eval
  * Patient dialogue (AI)
      Owner: R
      Purpose: Natural answers
      Runtime/Dev: Runtime
      AI/Non-AI: AI (LLM)
      Input: DTO + question
      Output: Utterance
      Authoritative source: Card
      Validation: Grounding
      Clinical review: Light
      Offline: Server
      Assessment mode?: YES
      Fallback: Pre-rendered
      Priority: P1
      Definition of Done: 0 invented facts; p95 ≤2.5 s
  * Staff/environment AI
      Owner: R
      Purpose: Natural staff
      Runtime/Dev: Runtime
      AI/Non-AI: AI (LLM)
      Input: Intent + DTO
      Output: Utterance
      Authoritative source: StaffDef intents
      Validation: Intent validator
      Clinical review: Lines light
      Offline: Server
      Assessment mode?: YES
      Fallback: Pre-rendered
      Priority: P1
      Definition of Done: No non-approved instructions
  * ASR
      Owner: R
      Purpose: Speech→text
      Runtime/Dev: Runtime
      AI/Non-AI: AI
      Input: Audio
      Output: Transcript
      Authoritative source: —
      Validation: WER eval
      Clinical review: No
      Offline: Server
      Assessment mode?: YES (with confirm)
      Fallback: Menu
      Priority: P1
      Definition of Done: Order accuracy ≥95% after read-back
  * Intent/communication classifier
      Owner: R (M: dataset/eval)
      Purpose: Evidence from speech
      Runtime/Dev: Runtime
      AI/Non-AI: AI
      Input: Transcript
      Output: Acts+spans+conf
      Authoritative source: Rules decide
      Validation: F1/kappa
      Clinical review: Taxonomy YES
      Offline: Server
      Assessment mode?: Non-critical only
      Fallback: Debrief-only
      Priority: P1
      Definition of Done: Meets thresholds or debrief-only
  * Workflow-behaviour assessment (evidence mapper)
      Owner: M
      Purpose: Assess whole workflow
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI (uses AI evidence)
      Input: Actions + evidence
      Output: Score items
      Authoritative source: Rules
      Validation: Golden runs
      Clinical review: YES
      Offline: YES
      Assessment mode?: YES
      Fallback: Deterministic only
      Priority: P0
      Definition of Done: All assessment rows mapped
  * Event Director + AI communication
      Owner: R
      Purpose: Approved events, natural cues
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI + AI phrasing
      Input: State
      Output: Event + utterance
      Authoritative source: Event library
      Validation: Eligibility + grounding
      Clinical review: YES
      Offline: YES
      Assessment mode?: YES (fixed schedule)
      Fallback: Pre-rendered cues
      Priority: P0 (+P1 phrasing)
      Definition of Done: Events reproducible
  * Adaptive Difficulty Engine
      Owner: M
      Purpose: Right challenge
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI
      Input: Session evidence
      Output: Next case
      Authoritative source: Approved pool
      Validation: Bounds tests
      Clinical review: Pool YES
      Offline: YES
      Assessment mode?: NO
      Fallback: Fixed order
      Priority: P0
      Definition of Done: Selects only approved
  * Difficulty explanation
      Owner: M
      Purpose: Transparency
      Runtime/Dev: Runtime
      AI/Non-AI: AI
      Input: Ratings + choice
      Output: Text
      Authoritative source: Engine
      Validation: Validator
      Clinical review: No
      Offline: Server
      Assessment mode?: NO
      Fallback: Template
      Priority: P1
      Definition of Done: Validator passes
  * AI debrief
      Owner: R
      Purpose: Readable feedback
      Runtime/Dev: Runtime
      AI/Non-AI: AI
      Input: Debrief facts
      Output: Narrative
      Authoritative source: Scorer
      Validation: Grounding
      Clinical review: Light
      Offline: Server
      Assessment mode?: YES
      Fallback: Template debrief
      Priority: P1
      Definition of Done: 0 validator failures /50
  * Synthetic-data pipeline
      Owner: M
      Purpose: Dev/eval data
      Runtime/Dev: Dev
      AI/Non-AI: AI + Non-AI
      Input: Blocks, templates
      Output: Datasets + cards
      Authoritative source: Dataset cards
      Validation: Per dataset
      Clinical review: Per dataset
      Offline: Dev PC
      Assessment mode?: Only approved runtime sets
      Fallback: —
      Priority: P1
      Definition of Done: Every dataset has a card
  * Embeddings/retrieval
      Owner: R
      Purpose: Grounded explanations
      Runtime/Dev: Runtime
      AI/Non-AI: AI
      Input: Query
      Output: Passages
      Authoritative source: Approved own-words corpus
      Validation: Precision@3
      Clinical review: Corpus YES
      Offline: Server
      Assessment mode?: Debrief only
      Fallback: Rule explain
      Priority: P2
      Definition of Done: —
  * Grounding/validation layer
      Owner: R
      Purpose: Guard every AI output
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI
      Input: AI output + context
      Output: Pass/fail
      Authoritative source: Content lexicons
      Validation: Unit + adversarial
      Clinical review: No
      Offline: YES
      Assessment mode?: YES
      Fallback: Fallback
      Priority: P0
      Definition of Done: 100% of AI calls validated
  * AI audit logger
      Owner: M
      Purpose: Traceability
      Runtime/Dev: Runtime
      AI/Non-AI: Non-AI
      Input: AI calls
      Output: Audit records
      Authoritative source: —
      Validation: Completeness test
      Clinical review: No
      Offline: YES (local)
      Assessment mode?: YES
      Fallback: Local log
      Priority: P1
      Definition of Done: Every call logged
  * Offline TTS voice pipeline
      Owner: M
      Purpose: Voiced lines offline
      Runtime/Dev: Dev
      AI/Non-AI: AI
      Input: Approved lines
      Output: Audio
      Authoritative source: Lines
      Validation: Listening
      Clinical review: No
      Offline: YES
      Assessment mode?: YES
      Fallback: Subtitles
      Priority: P0
      Definition of Done: All lines voiced
```


## 38. IMPLEMENTATION ORDER & DEPENDENCIES

```text
  [TABLE] # | Stage of implementation | Depends on | Weeks | Owner
  * 1
      Stage of implementation: Clinical content foundation (stages, sources, blocks)
      Depends on: —
      Weeks: W1–3
      Owner: M
  * 2
      Stage of implementation: Deterministic ProcSense.Core kernel
      Depends on: —
      Weeks: W1–2
      Owner: R
  * 3
      Stage of implementation: Patient state model (6 layers)
      Depends on: 2
      Weeks: W2
      Owner: R
  * 4
      Stage of implementation: Workflow engine (case graph, decision points, blockers)
      Depends on: 2,3
      Weeks: W2–3
      Owner: R
  * 5
      Stage of implementation: Rule engine + provenance
      Depends on: 2,1
      Weeks: W2–3
      Owner: M
  * 6
      Stage of implementation: Investigation subsystem + transport
      Depends on: 4,5
      Weeks: W3–4
      Owner: M
  * 7
      Stage of implementation: Medication/IV/fluids framework
      Depends on: 4,5
      Weeks: W4
      Owner: M
  * 8
      Stage of implementation: Equipment/OR/environment simulation
      Depends on: 4
      Weeks: W4–6
      Owner: S
  * 9
      Stage of implementation: Physiology (LitePhysio; Pulse gate)
      Depends on: 3
      Weeks: W2–6
      Owner: R
  * 10
      Stage of implementation: Event Director + event library
      Depends on: 5,9
      Weeks: W6–7
      Owner: R
  * 11
      Stage of implementation: Scoring + evidence mapper
      Depends on: 5,12
      Weeks: W4–6
      Owner: M
  * 12
      Stage of implementation: Session logging
      Depends on: 2
      Weeks: W2
      Owner: R
  * 13
      Stage of implementation: Scenario Composer (solver)
      Depends on: 1,3,5
      Weeks: W5–6
      Owner: M
  * 14
      Stage of implementation: Constraint/contradiction validation (Stage B)
      Depends on: 13
      Weeks: W5–6
      Owner: M
  * 15
      Stage of implementation: Patient Behaviour Model
      Depends on: 3,4
      Weeks: W6
      Owner: R
  * 16
      Stage of implementation: ASR
      Depends on: 12
      Weeks: W7–8
      Owner: R
  * 17
      Stage of implementation: Communication/intent classifier
      Depends on: 16,11
      Weeks: W8–9
      Owner: R+M
  * 18
      Stage of implementation: Patient/staff AI dialogue + grounding
      Depends on: 15,24
      Weeks: W8
      Owner: R
  * 19
      Stage of implementation: AI state-aware reaction layer
      Depends on: 18,15,10
      Weeks: W8–9
      Owner: R
  * 20
      Stage of implementation: Adaptive Difficulty Engine
      Depends on: 11,13
      Weeks: W9
      Owner: M
  * 21
      Stage of implementation: AI-assisted scenario generation (Stage A)
      Depends on: 13,14,18
      Weeks: W9
      Owner: R
  * 22
      Stage of implementation: Synthetic-data pipeline
      Depends on: 13,18
      Weeks: W7–10
      Owner: M
  * 23
      Stage of implementation: AI debrief
      Depends on: 11,24
      Weeks: W10
      Owner: R
  * 24
      Stage of implementation: Grounding/validation + audit + eval suite (starts early, before any AI ships)
      Depends on: 2
      Weeks: W6→W10
      Owner: R (validator) + M (audit, eval)
  * 25
      Stage of implementation: S1 integration
      Depends on: 1–15
      Weeks: W5 (P0) / W9 (AI)
      Owner: ALL
  * 26
      Stage of implementation: S2 integration
      Depends on: 25
      Weeks: W9
      Owner: ALL
  * 27
      Stage of implementation: Offline/Quest performance testing
      Depends on: 25,26
      Weeks: W10–12
      Owner: S+R
  * 28
      Stage of implementation: Clinical validation
      Depends on: 25,26
      Weeks: W11
      Owner: M (coord) + ALL
  * 29
      Stage of implementation: Assessment-mode freeze
      Depends on: 28
      Weeks: W11–12
      Owner: ALL
  * 30
      Stage of implementation: Final release
      Depends on: 29
      Weeks: W13–14
      Owner: ALL
```


## 39. END-TO-END AI EXAMPLE — S1

```text
  [TABLE] Step | What happens | AI component participating
  * Scenario configuration
      What happens: Composer selected S1-C017 (approved card) for DIFF-2
      AI component participating: Adaptive Difficulty Engine (selection), Composer (earlier, offline)
  * Patient state
      What happens: Core loads card; anxiety 6, trust 4; hidden intake + allergy
      AI component participating: —
  * Trainee enters room
      What happens: Nurse intent INTRODUCE_PATIENT
      AI component participating: Staff AI phrases approved intent → validator → TTS
  * Patient speaks
      What happens: Behaviour mode ANXIOUS → intent GREET_ANXIOUS
      AI component participating: Behaviour realisation (LLM) with read-only DTO
  * Trainee asks 'Have you had anything to eat or drink today?'
      What happens: Speech
      AI component participating: ASR → classifier: ASK_CLOSED, topic=last_intake (conf 0.94)
  * Deterministic evaluation
      What happens: ask(last_intake) emitted; disclosure rule reveals tea 3 h; R-FAST-01 breach
      AI component participating: Evidence mapper (deterministic)
  * Patient behaviour update
      What happens: Trainee explains why → understanding +1 (transition table)
      AI component participating: Classifier EXPLAIN_PURPOSE → Behaviour Model applies bounded delta
  * Investigation request
      What happens: Orders HbA1c/U&E/LFT/ECG (tablet)
      AI component participating: —
  * Simulated investigation / result
      What happens: ResultGenerator → LAB-HBA1C-HIGH
      AI component participating: Report text pre-drafted offline (reviewed)
  * Trainee responds
      What happens: Acknowledges; decision per local plan
      AI component participating: —
  * Staff interaction
      What happens: Trainee: 'Please hold her metformin' → nurse read-back
      AI component participating: ASR → voice-order parser → staff AI CONFIRM_ORDER → trainee confirms
  * Preparation
      What happens: IV insertion
      AI component participating: —
  * Approved unexpected event
      What happens: Advanced mode: allergen given → Event Director fires E-ANAPHYLAXIS (WT 14.4)
      AI component participating: Deterministic
  * Deterministic state change
      What happens: HR↓ BP↓ effect; fear +2
      AI component participating: Physiology + Behaviour Model
  * AI patient/staff reaction
      What happens: Patient intent EXPRESS_BREATHLESSNESS; nurse intent REPORT_OBSERVATION
      AI component participating: LLM phrasing from approved event communication intents
  * Trainee response
      What happens: Lies patient flat, reassures, attaches monitor
      AI component participating: Classifier REASSURE (non-critical evidence)
  * Handover
      What happens: Spoken SBAR to anaesthetist
      AI component participating: ASR → classifier maps SBAR elements → trainee confirms on tablet →
          deterministic element check
  * Scoring
      What happens: Scorer builds items with evidence refs
      AI component participating: Deterministic
  * AI debrief
      What happens: Debrief facts → LLM narrative → grounding validator
      AI component participating: AI debrief + audit logger
```


## 40. END-TO-END AI EXAMPLE — S2

```text
  [TABLE] Step | What happens | AI component participating
  * Scenario configuration
      What happens: Composer case S2-C004 (DIFF-3: uncertain fasting, CRP delay, theatre-calls)
      AI component participating: Adaptive Difficulty Engine picked tier 3 from evidence (handover strong,
          events slow)
  * Patient state
      What happens: Pain 7, anxiety 7, dehydration driver
      AI component participating: —
  * Trainee enters ED cubicle
      What happens: Nurse intent REPORT_TRIAGE
      AI component participating: Staff AI phrasing
  * Patient speaks
      What happens: Mode IN_PAIN → short answers
      AI component participating: Behaviour realisation (terse, pain)
  * Trainee asks about pain and offers analgesia
      What happens: Speech
      AI component participating: ASR → classifier ASK_OPEN (SOCRATES), OFFER_ANALGESIA
  * Deterministic evaluation
      What happens: Analgesia timing goal met after tablet prescription + checks
      AI component participating: Evidence mapper
  * Patient behaviour update
      What happens: Pain ↓ via physiology; cooperation +1
      AI component participating: Behaviour Model
  * Investigation request
      What happens: Urgent bloods, pregnancy test, US with monitored transfer
      AI component participating: Porter AI CONFIRM_TRANSFER
  * Result returned
      What happens: CRP delayed event; lab AI REPORT_DELAY
      AI component participating: Staff AI
  * Trainee responds
      What happens: Proceeds; escalates to surgeon by speech
      AI component participating: Classifier SBAR elements (non-critical clarity)
  * Approved unexpected event
      What happens: E-THEATRE-CALLS (fixed schedule in assessment)
      AI component participating: Event Director
  * AI staff reaction
      What happens: OT coordinator intent REQUEST_PATIENT_NOW
      AI component participating: Staff AI
  * Trainee response
      What happens: 'Consent and handover outstanding, ETA 10 min'
      AI component participating: Classifier detects STATE_OUTSTANDING + ETA → deterministic response rule
  * Handover
      What happens: Includes fasting uncertainty (critical — confirmed on tablet)
      AI component participating: ASR + classifier + deterministic critical check
  * Scoring / AI debrief
      What happens: Items + competencies → narrative
      AI component participating: Scorer + AI debrief + validator + audit
```


## 41. AI TESTS, RISKS AND DEFINITION OF DONE

```text
  [TABLE] Test ID | Area | Scenario | Expected behaviour | Outcome | Pass
  * T-AI-01
      Area: Composer
      Scenario: Inject each contradiction type (12)
      Expected behaviour: All rejected by Stage B
      Outcome: No invalid case reaches pool
      Pass: 12/12 rejected
  * T-AI-02
      Area: Composer
      Scenario: AI Stage A proposes 50 cases
      Expected behaviour: Stage B filters; valid ones stop at IMPLEMENTED
      Outcome: No auto-approval
      Pass: 0 auto-approved
  * T-AI-03
      Area: State-aware AI
      Scenario: Attempt write via AI client
      Expected behaviour: No API exists; compile/test fails
      Outcome: Truth protected
      Pass: No mutation path
  * T-AI-04
      Area: State-aware AI
      Scenario: Ask about a fact not yet disclosable
      Expected behaviour: Patient deflects; validator blocks leak
      Outcome: Hidden info preserved
      Pass: 0 leaks /100
  * T-AI-05
      Area: Behaviour
      Scenario: AI suggests intent outside table
      Expected behaviour: Rejected; no state change
      Outcome: Bounded behaviour
      Pass: Rejected
  * T-AI-06
      Area: Staff AI
      Scenario: Prompt nurse to give a drug instruction
      Expected behaviour: Only approved intents; validator rejects
      Outcome: No invented instructions
      Pass: 0 violations
  * T-AI-07
      Area: Assessment
      Scenario: Critical item said only by speech
      Expected behaviour: Not credited until deterministic confirm
      Outcome: Critical items deterministic
      Pass: Correct
  * T-AI-08
      Area: Events
      Scenario: Event fires; AI phrases it
      Expected behaviour: Only approved facts in utterance
      Outcome: Safe reproducible events
      Pass: Grounded
  * T-AI-09
      Area: Difficulty
      Scenario: Strong trainee
      Expected behaviour: Next case from approved pool only
      Outcome: No invented conditions
      Pass: Pool membership
  * T-AI-10
      Area: Debrief
      Scenario: Mutate a fact ID/number
      Expected behaviour: Validator rejects → template
      Outcome: No invented mistakes
      Pass: Rejected
  * T-AI-11
      Area: Synthetic data
      Scenario: Dataset without card/labels
      Expected behaviour: Pipeline refuses
      Outcome: Traceable data
      Pass: Refused
  * T-AI-12
      Area: Offline
      Scenario: Server off
      Expected behaviour: All AI paths fall back
      Outcome: Simulation continues
      Pass: Complete run
  * T-AI-13
      Area: Assessment mode
      Scenario: Load unapproved AI case
      Expected behaviour: Loader refuses
      Outcome: Assessment integrity
      Pass: Refused

  [TABLE] Risk | Level | Mitigation
  * AI-composed case contains a contradiction
      Level: Medium
      Mitigation: Stage B detector + mutation tests + reviewer approval before pool
  * AI leaks hidden information
      Level: Medium
      Mitigation: Disclosure-aware DTO + validator + T-AI-04
  * Behaviour drift (AI escalates emotions)
      Level: Low
      Mitigation: Intents only; bounded transition table
  * Synthetic data treated as truth
      Level: Low
      Mitigation: Dataset cards, synthetic flags, dev-only labels
  * State-aware context adds latency
      Level: Medium
      Mitigation: Compact DTO; pre-rendered fallback ≤2.5 s
  * Classifier evidence over-trusted
      Level: Medium
      Mitigation: Threshold, non-critical only, human-labelled eval

  - Scenario Composer produces ≥8 S1 / ≥6 S2 approved complete cases; contradiction detector passes all
    mutation tests (P0)
  - Assessment evidence mapper covers every row of the workflow-assessment table with deterministic rules (P0)
  - Behaviour Model with bounded transitions; AI realisation validated (P0 model / P1 realisation)
  - Grounding/validation layer wraps 100% of AI calls; audit logger records every call (P0 / P1)
  - Staff AI, state-aware reaction layer, ASR, classifier, AI debrief, Stage A scenario proposal meet their
    evaluation thresholds or fall back (P1)
  - Every synthetic dataset has a dataset card (purpose, generator, source, validation, labels, runtime/dev)
    (P1)
  - Adaptive difficulty selects only approved variants; disabled in assessment mode (P0)
```


## 42. TEAM & RESPONSIBILITIES

```text
Original ownership kept; new components added.

  [TABLE] Person | Role | Strengths | Why this ownership
  * Ruhan
      Role: Simulation core + main AI
      Strengths: AI/LLM + automation (internship); HPC course and GPU optimisation; owns the two GPU machines
          (RTX 4050 laptop, GTX 1660 Super desktop)
      Why this ownership: The hardest AI constraint is making models fit and run fast on 6 GB GPUs, which is
          exactly HPC/GPU optimisation work. LLM/automation internship experience fits prompt design,
          grounding, model serving and the classifier. Core, physiology and Event Director are performance-
          sensitive simulation code. GPU-heavy AI stays where the hardware is.
  * Sannan
      Role: VR, 3D and the app
      Strengths: Blender 3D (personal project); app development (course)
      Why this ownership: Blender covers custom equipment models, patient blend shapes and staff rigs, which
          nobody else can do quickly. App-dev covers Unity UI, Android/Quest builds and client-side networking
          (voice client). None of this needs a strong GPU.
  * Moiz
      Role: Content, data, evaluation + supporting AI
      Strengths: Some AI (internship); app development (course)
      Why this ownership: Internship AI exposure fits data-centred AI: synthetic datasets, labelling,
          evaluation, and light rule-plus-AI pieces (voice-order parser, difficulty engine). These run via
          APIs or CPU, so his lighter GPU is not a limit. App-dev fits backend, loaders, audit logger and
          tooling. Composer, validators and scoring are structured-data logic that sits naturally with the
          schema owner.

  [TABLE] Person | Original ownership (unchanged) | New components added
  * Ruhan
      Original ownership (unchanged): Core kernel · case graph & decision points · patient state ·
          LitePhysio/Pulse · Patient Behaviour Model · Event Director · readiness/checklist engines
      New components added: LLM setup + benchmark + GPU optimisation · grounding/validation layer · patient
          dialogue · staff/environment AI · state-aware reaction layer & behaviour realisation · speech
          recognition service · intent/communication classifier (model, prompts, thresholds) · AI debrief ·
          Stage A scenario proposal
  * Sannan
      Original ownership (unchanged): Unity project · XR rig · SimulationHost bridge · environment import ·
          all interactables · avatars & animation · all in-VR UI · Quest builds & profiling
      New components added: Blender custom equipment models · patient blend shapes/shaders + behaviour-mode
          animation mapping · staff avatar rigs · voice client (push-to-talk, fallback, subtitles, lip sync) ·
          debrief & difficulty-explanation UI
  * Moiz
      Original ownership (unchanged): Schemas · content loader & validators · investigation & treatment
          systems · result generator · rules & provenance · scoring engine + rubrics · backend/session sync ·
          source register · doctor coordination
      New components added: Scenario Composer solver + case cards · Stage B validator + contradiction detector
          · assessment evidence mapper · synthetic-data pipeline + dataset cards · AI eval suite · classifier
          labelled dataset & taxonomy · adaptive difficulty engine + explanation · voice-order parser & read-
          back · offline voice pipeline · AI audit logger

  [TABLE] Person | Added for the walkthrough (v6)
  * Ruhan
      Added for the walkthrough (v6): + B-R17 Decision & priority engine; + B-R18 Early-warning & sepsis
          engine
  * Sannan
      Added for the walkthrough (v6): + B-S12 Theatre extension scenes (P1)
  * Moiz
      Added for the walkthrough (v6): + B-M18 Walkthrough content pipeline & traceability; + B-M19 Case
          library; + B-M20 Clinical calculators

  From                               | To                                    | What passes                         
  -----------------------------------+---------------------------------------+-------------------------------------
  Ruhan — Stage A proposals          | Moiz — Stage B validator              | Candidate case JSON (block IDs only)
  Ruhan — classifier output          | Moiz — evidence mapper → scoring      | {acts, topic, span, confidence}     
  Moiz — synthetic data + eval suite | Ruhan — dialogue, classifier, debrief | Datasets + pass/fail reports        
  Ruhan — behaviour intents/modes    | Sannan — animations/blend shapes      | Behaviour mode + intensity          
  Ruhan — AI endpoints               | Sannan — voice client                 | HTTP/JSON + audio                   
  Moiz — voice-order parser          | Ruhan — staff AI (read-back phrasing) | Parsed order JSON                   
  Moiz — offline voice pipeline      | Sannan — audio catalogue in Unity     | Vorbis files + line index           
  Moiz — difficulty engine           | Sannan — menu UI                      | Next case + explanation text        

  [TABLE] Build guide | Component | Owner | Priority | Weeks | v6 status
  * B-R11
      Component: Core kernel (SimClock, ActionAPI, EventBus, Session, deterministic RNG)
      Owner: Ruhan
      Priority: P0
      Weeks: W1–W2
      v6 status: Rewritten/expanded for walkthrough
  * B-R12
      Component: Case graph, decision points & readiness blockers
      Owner: Ruhan
      Priority: P0
      Weeks: W2–W3
      v6 status: Rewritten/expanded for walkthrough
  * B-R13
      Component: LitePhysio engine + Pulse gate
      Owner: Ruhan
      Priority: P0 (Lite) / P1 (Pulse)
      Weeks: W1–W6
      v6 status: Rewritten/expanded for walkthrough
  * B-R14
      Component: Event Director + event library runtime
      Owner: Ruhan
      Priority: P0
      Weeks: W6–W7
      v6 status: Rewritten/expanded for walkthrough
  * B-R15
      Component: SafetyChecklist & OR readiness engines (Sign In / Time Out / Sign Out)
      Owner: Ruhan
      Priority: P0 (SI/TO) / P1 (SO)
      Weeks: W5–W6
      v6 status: Rewritten/expanded for walkthrough
  * B-R16
      Component: Location & transport system
      Owner: Ruhan
      Priority: P0
      Weeks: W3–W4
      v6 status: Rewritten/expanded for walkthrough
  * B-R1
      Component: LLM setup, model benchmark & GPU optimisation
      Owner: Ruhan
      Priority: P1
      Weeks: W6–W7
      v6 status: Rewritten/expanded for walkthrough
  * B-R2
      Component: Grounding / validation layer
      Owner: Ruhan
      Priority: P0
      Weeks: W6 (before any AI ships)
      v6 status: Rewritten/expanded for walkthrough
  * B-R3
      Component: Patient dialogue (state-aware)
      Owner: Ruhan
      Priority: P1
      Weeks: W8
      v6 status: Rewritten/expanded for walkthrough
  * B-R4
      Component: Staff & environment AI
      Owner: Ruhan
      Priority: P1
      Weeks: W8
      v6 status: Rewritten/expanded for walkthrough
  * B-R5
      Component: State-aware reaction layer & behaviour realisation
      Owner: Ruhan
      Priority: P1 (DTO P0)
      Weeks: W6 (DTO) / W8–W9 (AI)
      v6 status: Reviewed — purpose/traceability updated
  * B-R6
      Component: Patient Behaviour Model (deterministic, expanded)
      Owner: Ruhan
      Priority: P0
      Weeks: W6
      v6 status: Rewritten/expanded for walkthrough
  * B-R7
      Component: Speech recognition service
      Owner: Ruhan
      Priority: P1
      Weeks: W7–W8
      v6 status: Reviewed — purpose/traceability updated
  * B-R8
      Component: Intent / communication classifier (model side)
      Owner: Ruhan
      Priority: P1
      Weeks: W8–W9
      v6 status: Reviewed — purpose/traceability updated
  * B-R9
      Component: AI debrief
      Owner: Ruhan
      Priority: P1
      Weeks: W10
      v6 status: Rewritten/expanded for walkthrough
  * B-R10
      Component: Stage A scenario proposal
      Owner: Ruhan
      Priority: P1
      Weeks: W9
      v6 status: Rewritten/expanded for walkthrough
  * B-R17
      Component: Decision & priority engine (problems, ladder, clashes, roles)
      Owner: Ruhan
      Priority: P0
      Weeks: W4–W7
      v6 status: NEW (v6)
  * B-R18
      Component: Early-warning & sepsis engine (NEWS2, NG253 tiers, timers)
      Owner: Ruhan
      Priority: P0
      Weeks: W3–W6
      v6 status: NEW (v6)
  * B-S6
      Component: Unity project setup, XR rig & locomotion
      Owner: Sannan
      Priority: P0
      Weeks: W1–W2
      v6 status: Reviewed — purpose/traceability updated
  * B-S7
      Component: SimulationHost bridge, ActionEmitters, StateViews & SceneDirector
      Owner: Sannan
      Priority: P0
      Weeks: W2–W4
      v6 status: Reviewed — purpose/traceability updated
  * B-S8
      Component: Environment import, optimisation & room anchors
      Owner: Sannan
      Priority: P0
      Weeks: W1–W3
      v6 status: Rewritten/expanded for walkthrough
  * B-S9
      Component: Interaction framework & equipment state machines
      Owner: Sannan
      Priority: P0
      Weeks: W3–W7
      v6 status: Rewritten/expanded for walkthrough
  * B-S10
      Component: In-VR UI framework (tablet, monitor, results viewer, checklist boards)
      Owner: Sannan
      Priority: P0
      Weeks: W3–W6
      v6 status: Rewritten/expanded for walkthrough
  * B-S11
      Component: Quest builds, profiling & Unity CI
      Owner: Sannan
      Priority: P0
      Weeks: W2 → W14
      v6 status: Reviewed — purpose/traceability updated
  * B-S1
      Component: Blender custom equipment models
      Owner: Sannan
      Priority: P0
      Weeks: W3–W7
      v6 status: Rewritten/expanded for walkthrough
  * B-S2
      Component: Patient blend shapes, shaders & behaviour animation mapping
      Owner: Sannan
      Priority: P0
      Weeks: W5–W8
      v6 status: Rewritten/expanded for walkthrough
  * B-S3
      Component: Staff avatar rigs
      Owner: Sannan
      Priority: P0
      Weeks: W6–W7
      v6 status: Reviewed — purpose/traceability updated
  * B-S4
      Component: Voice client (push-to-talk, fallback, subtitles, lip sync)
      Owner: Sannan
      Priority: P1 (subtitles/lip sync P0)
      Weeks: W8
      v6 status: Reviewed — purpose/traceability updated
  * B-S5
      Component: Debrief & difficulty-explanation UI
      Owner: Sannan
      Priority: P0 (template) / P1 (AI text)
      Weeks: W10
      v6 status: Rewritten/expanded for walkthrough
  * B-S12
      Component: Theatre extension scenes: intra-op events, Sign Out, recovery (P1)
      Owner: Sannan
      Priority: P1
      Weeks: W9–W11
      v6 status: NEW (v6)
  * B-M11
      Component: Schemas, content loader, validators & packaging
      Owner: Moiz
      Priority: P0
      Weeks: W1–W2
      v6 status: Rewritten/expanded for walkthrough
  * B-M12
      Component: Rule evaluator (C# + Python twin) & provenance
      Owner: Moiz
      Priority: P0
      Weeks: W2–W3
      v6 status: Rewritten/expanded for walkthrough
  * B-M13
      Component: Investigation system & result generator
      Owner: Moiz
      Priority: P0
      Weeks: W3–W5
      v6 status: Rewritten/expanded for walkthrough
  * B-M14
      Component: Treatment system & required checks
      Owner: Moiz
      Priority: P0
      Weeks: W4–W5
      v6 status: Rewritten/expanded for walkthrough
  * B-M15
      Component: Scoring engine, rubrics & template debrief (DebriefFacts)
      Owner: Moiz
      Priority: P0
      Weeks: W4–W10
      v6 status: Rewritten/expanded for walkthrough
  * B-M16
      Component: Backend session sync & reports
      Owner: Moiz
      Priority: P1
      Weeks: W8–W9
      v6 status: Reviewed — purpose/traceability updated
  * B-M17
      Component: Source register, clinical question log & doctor review packs
      Owner: Moiz
      Priority: P0
      Weeks: W1 → W11
      v6 status: Rewritten/expanded for walkthrough
  * B-M1
      Component: Scenario Composer solver + case cards
      Owner: Moiz
      Priority: P0
      Weeks: W5–W6
      v6 status: Rewritten/expanded for walkthrough
  * B-M2
      Component: Stage B validator + contradiction detector
      Owner: Moiz
      Priority: P0
      Weeks: W5–W6
      v6 status: Rewritten/expanded for walkthrough
  * B-M3
      Component: Assessment evidence mapper
      Owner: Moiz
      Priority: P0 (deterministic) / P1 (AI evidence)
      Weeks: W6 / W9
      v6 status: Rewritten/expanded for walkthrough
  * B-M4
      Component: Synthetic-data pipeline + dataset cards
      Owner: Moiz
      Priority: P1
      Weeks: W7–W10
      v6 status: Reviewed — purpose/traceability updated
  * B-M5
      Component: AI eval suite
      Owner: Moiz
      Priority: P1
      Weeks: W6 skeleton → W10
      v6 status: Reviewed — purpose/traceability updated
  * B-M6
      Component: Classifier taxonomy & labelled dataset
      Owner: Moiz
      Priority: P1
      Weeks: W7–W8
      v6 status: Reviewed — purpose/traceability updated
  * B-M7
      Component: Adaptive difficulty engine + explanation
      Owner: Moiz
      Priority: P0 (selection) / P1 (explanation)
      Weeks: W9
      v6 status: Rewritten/expanded for walkthrough
  * B-M8
      Component: Voice-order parser & read-back flow
      Owner: Moiz
      Priority: P1
      Weeks: W8
      v6 status: Reviewed — purpose/traceability updated
  * B-M9
      Component: Offline voice pipeline
      Owner: Moiz
      Priority: P0
      Weeks: W4–W6
      v6 status: Reviewed — purpose/traceability updated
  * B-M10
      Component: AI audit logger
      Owner: Moiz
      Priority: P1
      Weeks: W7
      v6 status: Reviewed — purpose/traceability updated
  * B-M18
      Component: Walkthrough content pipeline & traceability
      Owner: Moiz
      Priority: P0
      Weeks: W2–W3 (then continuous)
      v6 status: NEW (v6)
  * B-M19
      Component: Case library: patients, stacked cases and what-if bindings
      Owner: Moiz
      Priority: P0 (P0 set) / P1 (rest)
      Weeks: W3–W9
      v6 status: NEW (v6)
  * B-M20
      Component: Clinical calculators: scores and risk tools
      Owner: Moiz
      Priority: P0
      Weeks: W4–W6
      v6 status: NEW (v6)
```


## 43. SCHEDULE (STARTS TUE 29 SEP 2026)

```text
Gate dates unchanged; scope tiered to fit.

  [TABLE] Week | Work | Milestones
  * W1 Tue 29 Sep–Sun 4 Oct
      Work: Kickoff; Core kernel; Unity project + XR rig; schemas v1 (done per original plan)
      Milestones: —
  * W2 5–11 Oct
      Work: Walkthrough integration: content package + traceability (B-M18); 15-step model; Step 1 on Quest
      Milestones: GATE 1 Fri 9 Oct: Step 1 playable on Quest
  * W3 12–18 Oct
      Work: Steps 2–4 (history with hidden facts, vitals/NEWS2, examination); NEWS2/sepsis engine (B-R18)
          start; calculators (B-M20) start
      Milestones: CRG-1 Wed 14 Oct: Steps 1–7 + institutional decisions 1, 4, 5, 6, 10, 11 · GATE 2 Fri 16
          Oct: Steps 1–4 + ordering headless
  * W4 19–25 Oct
      Work: Steps 5–8: lab realism + critical calls (B-M13), medicine request workflow (B-M14), decision
          engine (B-R17) start
      Milestones: —
  * W5 26 Oct–1 Nov
      Work: Steps 9–14 for S1; WHO exact wording; ward checklist; theatre stations
      Milestones: CRG-2 Wed 28 Oct: Steps 8–15 + decisions 2, 3, 8, 9, 12 · GATE 3 Fri 30 Oct: S1 Steps 1–14
          end-to-end with VAR-S1-A and VAR-S1-PB
  * W6 2–8 Nov
      Work: Problem registry, ladder, clashes; deterioration clocks; S1 P0 variants B, C, D, E, G, H, J
      Milestones: GATE 4 Fri 6 Nov: physiology + decision engine
  * W7 9–15 Nov
      Work: What-if library (P0-bound branches), event visuals; STK-S1-D
      Milestones: CRG-3 Wed 11 Nov: what-ifs, stacked cases, S2 P0 · GATE 5 Fri 13 Nov: all S1 P0 cases
  * W8 16–22 Nov
      Work: S2 build (overlap, triage, early analgesia); AI P1 work
      Milestones: GATE 6 Fri 20 Nov: AI keep/cut + Step 15/extension keep/cut
  * W9 23–29 Nov
      Work: S2 P0 cases incl. Usman deterioration timeline and STK-S2-B
      Milestones: CRG-4 Wed 25 Nov · GATE 7 Fri 27 Nov: S2 end-to-end
  * W10 30 Nov–6 Dec
      Work: End states, timeline debrief, scoring weights; regression
      Milestones: GATE 8 Fri 4 Dec: FEATURE FREEZE
  * W11 7–13 Dec
      Work: Clinical validation playthroughs (Part I checklist A–G); content-only P1 cases if validated
      Milestones: CRG-5 Wed 9 Dec · GATE 9 Fri 11 Dec
  * W12 14–20 Dec
      Work: Performance, full test pass
      Milestones: GATE 10 Wed 16 Dec · GATE 11 Fri 18 Dec: CODE FREEZE
  * W13 21–27 Dec
      Work: rc1 Mon 21; rehearsal Wed 23
      Milestones: GATE 12 Sat 26 Dec: v1.0
  * W14 28–31 Dec
      Work: Clean-machine test Tue 29; sign-off Wed 30
      Milestones: SUBMIT Thu 31 Dec

  [TABLE] Schedule realism | Detail
  * Scope added by the walkthrough
      Detail: 15 steps × 21 questions; 25 variants; 8 full + 20 compact stacked cases; 47 what-ifs; 2 ×
          20-stage maps; 50 build guides (was 44).
  * Honest assessment
      Detail: Building every case and branch as P0 by 4 Dec is not realistic for three students. The schedule
          structure and gates are kept, but scope is tiered.
  * Adjustment 1 — data-driven engine
      Detail: Cases, what-ifs and stacked cases are CONTENT, not code. Once the engine supports a content
          type, extra cases are additions to the content package.
  * Adjustment 2 — P0 case set
      Detail: 17 cases: VAR-S1-A, -PB, -B(+B2), -C(+C2), -D(a/b), -E, -G, -H, -J; VAR-S2-PC, -PD, -B, -D, -F,
          -G; STK-S1-D; STK-S2-B.
  * Adjustment 3 — P1/P2
      Detail: P1: remaining 10 variants + 6 stacked cases (content-only after freeze if validated and no new
          code needed); Step 15 + intra-op extension (decided at Gate 6). P2: SC-1..SC-20.
  * Adjustment 4 — what-ifs
      Detail: P0 = what-ifs bound to P0 cases; the rest are loaded and testable but not polished.
  * Adjustment 5 — clinician time
      Detail: CRG sessions now include the 15 institutional decisions; packs limited to ≤45 min by
          prioritising Type C and P0 items.

  [TABLE] Risk | Level | Mitigation
  * Clinical review bottleneck (more content)
      Level: High
      Mitigation: Prioritise Type C + P0 items; 15 decisions spread over CRG-1/2; unapproved content disabled
          in assessment mode
  * Scope growth from walkthrough
      Level: High
      Mitigation: Tiered P0/P1/P2; data-driven cases; cut lines at Gates 3, 5, 6
  * Walkthrough inconsistencies (WT-I01..12)
      Level: Medium
      Mitigation: issues_flagged.csv; supervisor confirms; namespaced IDs
  * Content drift between walkthrough and code
      Level: Medium
      Mitigation: B-M18 regenerates package; CI traceability check
  * Type D values mistaken for medicine
      Level: Medium
      Mitigation: Labelled in content and UI; clinician realism review (decision 15)
  * Quest performance
      Level: Medium
      Mitigation: Budgets + weekly profiling
  * AI hallucination / dose output
      Level: Medium
      Mitigation: Validator dose-policy check; AI P1 only
  * Team availability
      Level: Medium
      Mitigation: Pairing, secondary owners, Friday rebalance
```


## 44. CLINICAL VALIDATION (WT PART I)

```text
  [TABLE] # | Institutional decision | Owner
  * 1
      Institutional decision: Laboratory reference ranges, units and critical-value list
      Owner: Hospital laboratory
  * 2
      Institutional decision: Antibiotic protocol: prophylaxis agent/dose/redose; appendicitis regimen;
          penicillin-allergy alternative (incl. cefazolin after anaphylaxis)
      Owner: Microbiology / surgeon
  * 3
      Institutional decision: Consent/legal workflow: form, witness, age/guardian, mature minors, emergency
          treatment, family involvement
      Owner: Institution legal / surgeon
  * 4
      Institutional decision: Fasting policy details (clear 2 h vs 1 h; GLP-1 and SGLT2 policy choice)
      Owner: Anaesthesia department
  * 5
      Institutional decision: Local anaesthetic protocols (insulin, steroid cover, K acceptance 3.0–5.5 and
          INR limits as defaults)
      Owner: Anaesthesia department
  * 6
      Institutional decision: Emergency escalation: early-warning tool, responders, sepsis tool
      Owner: Hospital
  * 7
      Institutional decision: Equipment availability (MRCP, ERCP, CT, gastric ultrasound, capnography,
          warming)
      Owner: Hospital
  * 8
      Institutional decision: Documentation: ward checklist, handover form, WHO version/additions, site
          marking
      Owner: Theatre governance
  * 9
      Institutional decision: Blood-bank procedure (G&S/crossmatch, labelling)
      Owner: Transfusion lab
  * 10
      Institutional decision: Patient identification policy (identifiers, band colours)
      Owner: Hospital
  * 11
      Institutional decision: Transfer escort/monitoring rule
      Owner: Hospital
  * 12
      Institutional decision: Post-operative/day-case discharge criteria
      Owner: Surgeon / anaesthetist
  * 13
      Institutional decision: Choice of appendicitis score (AIR default vs AAS) and imaging pathway for high-
          score young patients
      Owner: Surgeon
  * 14
      Institutional decision: Unit-specific complication rates for consent
      Owner: Surgeon
  * 15
      Institutional decision: Realism of simulator design values (deterioration clocks, turnaround times)
      Owner: Surgeon + anaesthetist

I.3 Clinician Final Validation Checklist
A. Medical facts
Item OK Correction / comment
Anatomy explanations ☐
Disease definitions (biliary colic, cholecystitis,
choledocholithiasis, cholangitis, pancreatitis, appendicitis,
perforation, sepsis)
☐
Symptoms and examination findings ☐
Investigations and their indications ☐

Item OK Correction / comment
Interpretation (patterns, artefacts) ☐
Thresholds (reference vs decision) ☐
Complications ☐
B. Numbers
Item OK Correction / comment
Vital-sign ranges and NEWS2 bands ☐
Laboratory values (after local lab alignment) ☐
Imaging thresholds (wall >4 mm, CBD >6 mm, appendix >6
mm)
☐
Scoring systems (AIR/AAS/Alvarado, TG18, ASGE, ASA) ☐
Fasting intervals ☐
Timing (antibiotics, sepsis, surgery) ☐
Medication-related educational values ☐
C. Surgery
Item OK Correction / comment
S1 workflow ☐
S2 workflow ☐
Indications ☐
Timing ☐
Escalation points ☐
Complications and consent figures ☐
D. Anaesthesia
Item OK Correction / comment
Fasting and GLP-1/SGLT2 policy ☐
Aspiration risk and RSI ☐
Monitoring ☐
Emergency pathway ☐
Medication considerations ☐
E. Consent
Item OK Correction / comment
Capacity ☐
Refusal ☐
Interpreter ☐
Minors ☐
Emergency situations ☐
Applicable legal requirements for the target jurisdiction ☐
F. Hospital / local policy
Item OK Correction / comment
Antibiotics ☐
Labs ☐
Blood bank ☐
Theatre ☐
Transfer ☐
Emergency escalation ☐
Documentation ☐

G. VR realism
Item OK Correction / comment
Equipment inventory ☐
Patient reactions ☐
Monitor behaviour ☐
NPC behaviour ☐
Consequences ☐
Workflow order ☐

  [TABLE] ID | Walkthrough issue | ProcSense handling | Needs
  * WT-I01
      Walkthrough issue: ID collisions: 'Example Patient B' (Farzana) vs variant S1-B (Tariq); 'Example
          Patient C/D' (Hamza/Usman) vs S2-C?/S2-D (Zainab); Part D stacked S1-A..D/S2-A..D reuse variant
          letters; two different 'Mr Imran' patients.
      ProcSense handling: ProcSense namespaces IDs: VAR- (Parts 12–13), STK- (Part D), SC- (Part 14B); WT
          label kept in a column.
      Needs: Supervisor to confirm naming; no medical change
  * WT-I02
      Walkthrough issue: Hamza symptom duration: Step 1 triage example '20 h' vs Part 13 triage note '14 h'
          (CRP described as '20 h of symptoms').
      ProcSense handling: Content uses Part 13 values; flagged.
      Needs: Surgeon to confirm
  * WT-I03
      Walkthrough issue: Part C event table cites 'Contract Act s.11' for minor consent — jurisdiction-
          specific despite v5 international scope.
      ProcSense handling: Not built into logic; consent law is a Type C institutional item.
      Needs: Legal/surgeon
  * WT-I04
      Walkthrough issue: Temperature display zone (normal 36.1–37.9; fever 38.0–39.0) differs from NEWS2
          scoring (0 = 36.1–38.0; 1 = 38.1–39.0).
      ProcSense handling: Display zones and NEWS2 scoring kept as separate tables (same approach as HR).
      Needs: Anaesthetist
  * WT-I05
      Walkthrough issue: Albumin bands: 5.15 low 25–34 vs Part 17 mild 30–34 / significant 25–29.
      ProcSense handling: Part 17 used for bands; 5.15 text shown in teaching.
      Needs: Lab/clinician
  * WT-I06
      Walkthrough issue: CRP mild band: 5.4 '5–49' vs Part 17 '10–49'.
      ProcSense handling: Both noted; lab ranges are Type C anyway.
      Needs: Lab
  * WT-I07
      Walkthrough issue: Sodium bands split differently (5.5 'High 146–155' vs Part 17 146–150 / 151–155).
      ProcSense handling: Part 17 split used; compatible.
      Needs: Lab
  * WT-I08
      Walkthrough issue: INR 'severe >2.0' and GGT 'mild ≤3×' appear only in Part 17.
      ProcSense handling: Used as display bands only.
      Needs: Clinician
  * WT-I09
      Walkthrough issue: Stage numbering differs: Part 3 (14 boxes), Part 11 (15 steps), Part 16 (Stages 1–14,
          13–14 combined), Parts F/G (20 stages, different order).
      ProcSense handling: Part 11 steps = canonical step IDs; F/G stages = scenario flow maps referencing
          steps.
      Needs: None
  * WT-I10
      Walkthrough issue: Turnaround times only partially specified ('FBC 45 min, imaging 60–120 min, etc.').
      ProcSense handling: Others marked UNRESOLVED (Type D) in data inventory.
      Needs: Surgeon + anaesthetist
  * WT-I11
      Walkthrough issue: BTS 'reservoir mask 15 L/min' not re-verified in v5.1 (Part H).
      ProcSense handling: Shown only as cited text; not a scoring rule.
      Needs: Anaesthetist
  * WT-I12
      Walkthrough issue: Fever thresholds per variant need surgeon confirmation (4.3).
      ProcSense handling: Defaults from WT.
      Needs: Surgeon
  * WT-I13
      Walkthrough issue: Source check (6 Oct 2026): NICE NG253 management algorithm resources state an initial
          fluid bolus of 250 mL ideally over 10–15 min; WT's sepsis example uses NICE CG174's 500 mL bolus
          over <15 min.
      ProcSense handling: WT value kept (display-only educational example); flagged — not changed.
      Needs: Anaesthetist/surgeon to confirm which example to display
  * WT-I14
      Walkthrough issue: Source check (6 Oct 2026): UK Kidney Association hyperkalaemia guideline has a
          version updated July 2026; WT cites the 2023 version.
      ProcSense handling: WT values kept; flagged for re-check before release (WT itself asks sources to be
          re-checked).
      Needs: Clinician re-check
```


### Definition of Done (project)

```text
  - All 15 steps implemented for S1 and S2 per walkthrough (Step 15 P1)
  - 17 P0 cases pass golden runs with the expected end state
  - Every walkthrough section mapped in traceability_matrix.csv
  - No content without evidence type + WT reference; Type C items listed with owner
  - NEWS2/sepsis tiers and calculators pass all walkthrough worked examples
  - Trainee can never administer or dose; dose policy enforced in UI and AI
  - Unapproved content disabled in assessment mode
  - Clean-machine build and documentation reproduce
  - Scenario Composer produces ≥8 S1 / ≥6 S2 approved complete cases; contradiction detector passes all
    mutation tests (P0)
  - Assessment evidence mapper covers every row of the workflow-assessment table with deterministic rules (P0)
  - Behaviour Model with bounded transitions; AI realisation validated (P0 model / P1 realisation)
  - Grounding/validation layer wraps 100% of AI calls; audit logger records every call (P0 / P1)
  - Staff AI, state-aware reaction layer, ASR, classifier, AI debrief, Stage A scenario proposal meet their
    evaluation thresholds or fall back (P1)
  - Every synthetic dataset has a dataset card (purpose, generator, source, validation, labels, runtime/dev)
    (P1)
  - Adaptive difficulty selects only approved variants; disabled in assessment mode (P0)
```


## 45. WALKTHROUGH TRACEABILITY MATRIX

```text
Every walkthrough section and every S1/S2 master-map stage → requirement, files, guides, status, validation.

  [TABLE] Page | Part | Section | Requirement | Files | Guides | Status | Validation / unresolved
  * 2
      Part: Front
      Section: WHAT CHANGED IN v5.1 (final QA pass)
      Requirement: Content labels A–D, number taxonomy, decision principle, safety statement, version log
      Files: Master Plan §Content principles; evidence_types.csv; number_kinds.csv
      Guides: B-M12, B-M18
      Status: Specified
      Validation / unresolved: Clinician sign-off of whole book
  * 3
      Part: Front
      Section: WHAT CHANGED IN v5
      Requirement: Content labels A–D, number taxonomy, decision principle, safety statement, version log
      Files: Master Plan §Content principles; evidence_types.csv; number_kinds.csv
      Guides: B-M12, B-M18
      Status: Specified
      Validation / unresolved: Clinician sign-off of whole book
  * 6
      Part: PART I
      Section: How to use this book
      Requirement: Clinician validation & institutional adaptation
      Files: clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation
      Guides: B-M17
      Status: Specified — awaiting decisions
      Validation / unresolved: 15 decisions + checklist A–G
  * 6
      Part: Part 1
      Section: Part 1 — Medicine basics The 25 core ideas (patient, symptom, sign, diagnosis, surgeon,
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 2
      Section: Part 2 — The two
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 3
      Section: Part 3 — The patient's
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 4
      Section: Part 4 — Vital signs from
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 6
      Part: Part 5
      Section: Part 5 — Blood and urine
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 6
      Part: Part 6
      Section: Part 6 — Heart tracing and
      Requirement: ECG and imaging interpretation
      Files: Master Plan Steps 6–7; lab_bands.csv (QTc)
      Guides: B-M13, B-S10
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 7
      Section: Part 7 — Scoring systems
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 6
      Part: Part 8
      Section: Part 8 — Medicines and
      Requirement: Medicines, anaesthesia, dose policy, role convention
      Files: medicines.csv
      Guides: B-M14, B-R17, B-R2
      Status: Specified
      Validation / unresolved: Anaesthetist; antibiotic policy (C)
  * 6
      Part: Part 9
      Section: Part 9 — Fasting from zero Why patients must not eat before surgery, and what happens if they
          did. A very common simulator branch.
      Requirement: Fasting rules
      Files: fasting_rules.csv
      Guides: B-M14, B-R17
      Status: Specified
      Validation / unresolved: Anaesthesia department (C)
  * 6
      Part: Part 10
      Section: Part 10 — Consent from
      Requirement: Consent
      Files: Master Plan Step 9
      Guides: B-R3, B-M3, B-S10
      Status: Specified
      Validation / unresolved: Consent law (C)
  * 6
      Part: Part 11
      Section: Part 11 — The 14 steps,
      Requirement: Step teaching structure
      Files: steps_21_questions.csv
      Guides: all step guides
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 12
      Section: Part 12 — S1 as a story A complete walk-through of a planned gallbladder patient, then abnormal
      Requirement: Scenario stories / patient variants
      Files: patient_library.csv; Master Plan §Patient library
      Guides: B-M19, B-M1
      Status: Specified
      Validation / unresolved: Surgeon: hidden facts and answers per variant
  * 6
      Part: Part 13
      Section: Part 13 — S2 as a story A complete walk-through of an emergency appendix patient, then a
      Requirement: Scenario stories / patient variants
      Files: patient_library.csv; Master Plan §Patient library
      Guides: B-M19, B-M1
      Status: Specified
      Validation / unresolved: Surgeon: hidden facts and answers per variant
  * 6
      Part: Part 14
      Section: Part 14 — WHAT IF
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 15
      Section: Part 15 — The WHO safety
      Requirement: WHO checklist + extensions
      Files: who_checklist.csv
      Guides: B-R15, B-S10
      Status: Specified
      Validation / unresolved: Theatre governance local additions (C)
  * 6
      Part: Part 16
      Section: Part 16 — What the player
      Requirement: Player view
      Files: player_view.csv
      Guides: B-S7, B-S9, B-S10
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 17
      Section: Part 17 — All the numbers
      Requirement: All numbers
      Files: vitals_bands.csv; lab_bands.csv; key_rules.csv; all_numbers_raw.txt
      Guides: B-M11, B-M12, B-M18
      Status: Specified
      Validation / unresolved: —
  * 6
      Part: Part 18
      Section: Part 18 — Dictionary of
      Requirement: Dictionary
      Files: glossary.csv; Implementation Guide glossary
      Guides: B-M18
      Status: Specified
      Validation / unresolved: —
  * 9
      Part: PART A
      Section: PART A — Medical foundation (Parts 1–18)
      Requirement: Section content
      Files: walkthrough_extract.json
      Guides: B-M18
      Status: Specified
      Validation / unresolved: —
  * 10
      Part: Part 1
      Section: Part 1 — Medicine basics for the software
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 11
      Part: Part 1
      Section: 1.2 Just-enough body knowledge
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 12
      Part: Part 1
      Section: 1.3 How the body responds when something is wrong (the most useful
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 12
      Part: Part 1
      Section: 1.4 The people in the simulator (NPCs)
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 14
      Part: Part 2
      Section: Part 2 — The two operations, explained simply
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 14
      Part: Part 2
      Section: 2.1 S1 — Laparoscopic cholecystectomy (planned keyhole gallbladder
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 15
      Part: Part 2
      Section: 2.2 S2 — Emergency appendectomy (urgent removal of an inflamed
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 16
      Part: Part 2
      Section: 2.3 What ProcSense does NOT simulate
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 17
      Part: Part 3
      Section: Part 3 — The patient's journey (the big picture)
      Requirement: Foundations, scenario definitions, journey + 5 outcomes, NPC roster
      Files: Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv
      Guides: B-R12, B-R17, B-S3, B-R4
      Status: Specified
      Validation / unresolved: —
  * 19
      Part: Part 4
      Section: Part 4 — Vital signs, taught from zero
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 19
      Part: Part 4
      Section: 4.0 Evidence cards for the six core vital signs
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 21
      Part: Part 4
      Section: 4.1 Blood pressure (BP)
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 24
      Part: Part 4
      Section: 4.2 Heart rate (HR, pulse)
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 25
      Part: Part 4
      Section: 4.3 Temperature
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 26
      Part: Part 4
      Section: 4.4 Respiratory rate (breathing rate, RR)
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 26
      Part: Part 4
      Section: 4.5 Oxygen saturation (SpO2)
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 28
      Part: Part 4
      Section: 4.6 Alertness (level of consciousness)
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 29
      Part: Part 4
      Section: 4.7 Pain score
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 29
      Part: Part 4
      Section: 4.8 Other bedside clues
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 29
      Part: Part 4
      Section: 4.9 NEWS2 — the early-warning score, explained from zero
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 30
      Part: Part 4
      Section: 4.10 Sepsis, explained from zero
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 32
      Part: Part 4
      Section: 4.11 Putting the vital signs together — pattern recognition
      Requirement: Vital signs, NEWS2, sepsis, patterns
      Files: vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv
      Guides: B-R13, B-R18, B-S10
      Status: Specified
      Validation / unresolved: Anaesthetist alarm limits (C)
  * 33
      Part: Part 5
      Section: Part 5 — Blood and urine tests, taught from zero
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 33
      Part: Part 5
      Section: 5.0 Reference range vs decision threshold — every test at a glance
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 36
      Part: Part 5
      Section: 5A — Full blood count (FBC): the blood cells
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 36
      Part: Part 5
      Section: 5.1 Haemoglobin (Hb)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 37
      Part: Part 5
      Section: 5.2 White blood cells (WBC) and neutrophils
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 37
      Part: Part 5
      Section: 5.3 Platelets (Plt)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 38
      Part: Part 5
      Section: 5.4 CRP (C-reactive protein)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 39
      Part: Part 5
      Section: 5B — Kidneys and body salts (U&E)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 39
      Part: Part 5
      Section: 5.5 Sodium (Na)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 39
      Part: Part 5
      Section: 5.6 Potassium (K)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 40
      Part: Part 5
      Section: 5.7 Urea
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 41
      Part: Part 5
      Section: 5.8 Creatinine and eGFR — kidney function
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 43
      Part: Part 5
      Section: 5C — Blood sugar and diabetes
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 43
      Part: Part 5
      Section: 5.9 Blood glucose (blood sugar)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 43
      Part: Part 5
      Section: 5.10 HbA1c (glycated haemoglobin)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 44
      Part: Part 5
      Section: 5.11 Ketones
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 45
      Part: Part 5
      Section: 5D — Liver tests (LFTs): the heart of S1
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 45
      Part: Part 5
      Section: 5.12 Bilirubin
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 45
      Part: Part 5
      Section: 5.13 ALT and AST (liver cell enzymes)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 46
      Part: Part 5
      Section: 5.14 ALP and GGT (bile duct enzymes)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 46
      Part: Part 5
      Section: 5.15 Albumin
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 46
      Part: Part 5
      Section: 5.16 Reading liver tests as PATTERNS (most important)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 47
      Part: Part 5
      Section: 5.17 Lipase / amylase (pancreas enzymes)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 48
      Part: Part 5
      Section: 5E — Clotting tests and blood bank
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 48
      Part: Part 5
      Section: 5.18 PT and INR
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 48
      Part: Part 5
      Section: 5.19 APTT and fibrinogen
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 49
      Part: Part 5
      Section: 5.20 Group and save / crossmatch (blood bank)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 50
      Part: Part 5
      Section: 5F — Pregnancy test and urine tests
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 50
      Part: Part 5
      Section: 5.21 Pregnancy test (β-hCG)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 51
      Part: Part 5
      Section: 5.22 Urine dipstick (urinalysis)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 52
      Part: Part 5
      Section: 5G — Emergency and special tests
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 52
      Part: Part 5
      Section: 5.23 Lactate (and blood gas)
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 52
      Part: Part 5
      Section: 5.24 Blood cultures
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 52
      Part: Part 5
      Section: 5.25 Troponin
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 52
      Part: Part 5
      Section: 5.26 Magnesium, calcium, iron, other local tests
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 53
      Part: Part 5
      Section: 5.27 Dangerous ('critical') results the lab will phone about CLINICAL REFERENCE —
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 53
      Part: Part 5
      Section: 5.28 When results are confusing — the trainee's playbook
      Requirement: Laboratory tests, artefacts, critical results
      Files: lab_bands.csv; critical_results.csv
      Guides: B-M13, B-M20
      Status: Specified
      Validation / unresolved: Hospital lab ranges (C)
  * 54
      Part: Part 6
      Section: Part 6 — Heart tracing (ECG) and scans, taught
      Requirement: ECG and imaging interpretation
      Files: Master Plan Steps 6–7; lab_bands.csv (QTc)
      Guides: B-M13, B-S10
      Status: Specified
      Validation / unresolved: —
  * 54
      Part: Part 6
      Section: 6.1 ECG (electrocardiogram)
      Requirement: ECG and imaging interpretation
      Files: Master Plan Steps 6–7; lab_bands.csv (QTc)
      Guides: B-M13, B-S10
      Status: Specified
      Validation / unresolved: —
  * 55
      Part: Part 6
      Section: 6.2 Imaging (scans) — the basics
      Requirement: ECG and imaging interpretation
      Files: Master Plan Steps 6–7; lab_bands.csv (QTc)
      Guides: B-M13, B-S10
      Status: Specified
      Validation / unresolved: —
  * 56
      Part: Part 6
      Section: 6.3 Gallbladder ultrasound results (S1) — what each means and what
      Requirement: ECG and imaging interpretation
      Files: Master Plan Steps 6–7; lab_bands.csv (QTc)
      Guides: B-M13, B-S10
      Status: Specified
      Validation / unresolved: —
  * 56
      Part: Part 6
      Section: 6.4 Appendix scan results (S2) — what each means and what happens
      Requirement: ECG and imaging interpretation
      Files: Master Plan Steps 6–7; lab_bands.csv (QTc)
      Guides: B-M13, B-S10
      Status: Specified
      Validation / unresolved: —
  * 58
      Part: Part 7
      Section: Part 7 — Scoring systems, taught from zero
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 58
      Part: Part 7
      Section: 7.1 Appendicitis scores (S2): Alvarado, AIR and AAS
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 59
      Part: Part 7
      Section: 7.2 Tokyo Guidelines 2018 (TG18) — is the gallbladder inflamed, and
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 60
      Part: Part 7
      Section: 7.3 Tokyo Guidelines — infected bile duct (cholangitis)
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 60
      Part: Part 7
      Section: 7.4 ASA physical status — 'how fit is this patient for anaesthesia?'
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 60
      Part: Part 7
      Section: 7.5 Airway and sleep-apnoea checks (for the anaesthetist)
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 61
      Part: Part 7
      Section: 7.6 Blood-clot (VTE) risk
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 61
      Part: Part 7
      Section: 7.7 Overall surgical risk tools
      Requirement: Scores and risk tools
      Files: scores.json
      Guides: B-M20, B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon); risk tool (C)
  * 62
      Part: Part 8
      Section: Part 8 — Medicines and anaesthesia, taught from
      Requirement: Medicines, anaesthesia, dose policy, role convention
      Files: medicines.csv
      Guides: B-M14, B-R17, B-R2
      Status: Specified
      Validation / unresolved: Anaesthetist; antibiotic policy (C)
  * 62
      Part: Part 8
      Section: 8.1 Why doses vary — and why this book mostly won't give them
      Requirement: Medicines, anaesthesia, dose policy, role convention
      Files: medicines.csv
      Guides: B-M14, B-R17, B-R2
      Status: Specified
      Validation / unresolved: Anaesthetist; antibiotic policy (C)
  * 62
      Part: Part 8
      Section: 8.2 Anaesthesia from zero
      Requirement: Medicines, anaesthesia, dose policy, role convention
      Files: medicines.csv
      Guides: B-M14, B-R17, B-R2
      Status: Specified
      Validation / unresolved: Anaesthetist; antibiotic policy (C)
  * 63
      Part: Part 8
      Section: 8.3 Medicine groups the trainee deals with
      Requirement: Medicines, anaesthesia, dose policy, role convention
      Files: medicines.csv
      Guides: B-M14, B-R17, B-R2
      Status: Specified
      Validation / unresolved: Anaesthetist; antibiotic policy (C)
  * 68
      Part: Part 9
      Section: Part 9 — Fasting, taught from zero
      Requirement: Fasting rules
      Files: fasting_rules.csv
      Guides: B-M14, B-R17
      Status: Specified
      Validation / unresolved: Anaesthesia department (C)
  * 68
      Part: Part 9
      Section: What if the patient ate recently? (S1)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 70
      Part: Part 10
      Section: Part 10 — Consent, taught from zero
      Requirement: Consent
      Files: Master Plan Step 9
      Guides: B-R3, B-M3, B-S10
      Status: Specified
      Validation / unresolved: Consent law (C)
  * 73
      Part: Part 11
      Section: Part 11 — The steps, one by one
      Requirement: Step teaching structure
      Files: steps_21_questions.csv
      Guides: all step guides
      Status: Specified
      Validation / unresolved: —
  * 74
      Part: Part 11
      Section: STEP 1 — Patient arrival and identification
      Requirement: Step 1 21-question specification
      Files: Master Plan Step 1; Guide 01; steps_21_questions.csv
      Guides: B-R11, B-S7, B-S9, B-M11, B-M18
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 76
      Part: Part 11
      Section: STEP 2 — Asking questions (the history)
      Requirement: Step 2 21-question specification
      Files: Master Plan Step 2; Guide 02; steps_21_questions.csv
      Guides: B-R6, B-R3, B-S10, B-M3, B-M19
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 78
      Part: Part 11
      Section: STEP 3 — Checking vital signs
      Requirement: Step 3 21-question specification
      Files: Master Plan Step 3; Guide 03; steps_21_questions.csv
      Guides: B-R13, B-R18, B-S9, B-S10
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 80
      Part: Part 11
      Section: STEP 4 — Examining the patient
      Requirement: Step 4 21-question specification
      Files: Master Plan Step 4; Guide 04; steps_21_questions.csv
      Guides: B-S9, B-S2, B-M3
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 82
      Part: Part 11
      Section: STEP 5 — Risk and anaesthetic assessment
      Requirement: Step 5 21-question specification
      Files: Master Plan Step 5; Guide 05; steps_21_questions.csv
      Guides: B-R12, B-M20, B-M12
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 84
      Part: Part 11
      Section: STEP 6 — Choosing the tests
      Requirement: Step 6 21-question specification
      Files: Master Plan Step 6; Guide 06; steps_21_questions.csv
      Guides: B-R16, B-M13, B-S7
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 86
      Part: Part 11
      Section: STEP 7 — Getting and understanding the results
      Requirement: Step 7 21-question specification
      Files: Master Plan Step 7; Guide 07; steps_21_questions.csv
      Guides: B-M13, B-S10, B-R17, B-M20
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 88
      Part: Part 11
      Section: STEP 8 — Deciding, treating and optimising
      Requirement: Step 8 21-question specification
      Files: Master Plan Step 8; Guide 08; steps_21_questions.csv
      Guides: B-M14, B-R17, B-R13, B-R18, B-R14
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 91
      Part: Part 11
      Section: STEP 9 — Consent
      Requirement: Step 9 21-question specification
      Files: Master Plan Step 9; Guide 09; steps_21_questions.csv
      Guides: B-R6, B-M3, B-R3, B-S10
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 93
      Part: Part 11
      Section: STEP 10 — Preparing the patient (ward/ED
      Requirement: Step 10 21-question specification
      Files: Master Plan Step 10; Guide 10; steps_21_questions.csv
      Guides: B-R15, B-S10
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 95
      Part: Part 11
      Section: STEP 11 — Theatre preparation
      Requirement: Step 11 21-question specification
      Files: Master Plan Step 11; Guide 11; steps_21_questions.csv
      Guides: B-S9, B-S1, B-R15, B-R14
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 97
      Part: Part 11
      Section: STEP 12 — Transfer and handover (SBAR)
      Requirement: Step 12 21-question specification
      Files: Master Plan Step 12; Guide 12; steps_21_questions.csv
      Guides: B-R16, B-M15, B-R8
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 99
      Part: Part 11
      Section: STEP 13 — WHO Sign In (before the patient goes to
      Requirement: Step 13 21-question specification
      Files: Master Plan Step 13; Guide 13; steps_21_questions.csv
      Guides: B-R15, B-S10, B-R4
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 101
      Part: Part 11
      Section: STEP 14 — WHO Time Out (before the first cut) —
      Requirement: Step 14 21-question specification
      Files: Master Plan Step 14; Guide 14; steps_21_questions.csv
      Guides: B-R15, B-S10
      Status: Specified
      Validation / unresolved: Per-step clinician validation line
  * 102
      Part: Part 11
      Section: STEP 15 (optional) — WHO Sign Out and handover
      Requirement: Step 15 21-question specification
      Files: Master Plan Step 15; Guide 15; steps_21_questions.csv
      Guides: B-R15, B-M15, B-R9, B-S12
      Status: Specified (P1)
      Validation / unresolved: Per-step clinician validation line
  * 103
      Part: Part 12
      Section: Part 12 — S1 as a story: a planned gallbladder
      Requirement: Scenario stories / patient variants
      Files: patient_library.csv; Master Plan §Patient library
      Guides: B-M19, B-M1
      Status: Specified
      Validation / unresolved: Surgeon: hidden facts and answers per variant
  * 109
      Part: Part 13
      Section: Part 13 — S2 as a story: an emergency appendix
      Requirement: Scenario stories / patient variants
      Files: patient_library.csv; Master Plan §Patient library
      Guides: B-M19, B-M1
      Status: Specified
      Validation / unresolved: Surgeon: hidden facts and answers per variant
  * 114
      Part: Part 14
      Section: Part 14 — WHAT IF something goes wrong?
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 114
      Part: Part 14
      Section: 14.0 The universal emergency method: ABCDE
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 114
      Part: Part 14
      Section: 14.1 Vital-sign problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 114
      Part: Part 14
      Section: What if blood pressure is too low? (top number 90 or less)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 115
      Part: Part 14
      Section: What if blood pressure is extremely high? (180/110 or more)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 115
      Part: Part 14
      Section: What if the heart rate is very high? (above 130)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 115
      Part: Part 14
      Section: What if the heart rate is very slow? (40 or below)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 116
      Part: Part 14
      Section: What if the patient has a fever? (38.0 °C or more)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 116
      Part: Part 14
      Section: What if the patient is cold? (36.0 °C or less)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 116
      Part: Part 14
      Section: What if oxygen saturation is low? (below 94%)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 117
      Part: Part 14
      Section: What if breathing is very fast? (25 or more)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 117
      Part: Part 14
      Section: What if breathing is very slow? (8 or less)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 117
      Part: Part 14
      Section: What if the patient becomes confused?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 118
      Part: Part 14
      Section: What if pain gets much worse or the tummy becomes rigid?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 118
      Part: Part 14
      Section: 14.2 Blood-test problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 118
      Part: Part 14
      Section: What if potassium is high? (above 5.3)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 118
      Part: Part 14
      Section: What if potassium is low? (below 3.5)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 119
      Part: Part 14
      Section: What if sodium is abnormal?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 119
      Part: Part 14
      Section: What if haemoglobin is low?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 119
      Part: Part 14
      Section: What if the white cell count is high?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 120
      Part: Part 14
      Section: What if the white cell count is low?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 120
      Part: Part 14
      Section: What if creatinine is high?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 120
      Part: Part 14
      Section: What if liver tests are abnormal?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 121
      Part: Part 14
      Section: What if bilirubin is high?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 121
      Part: Part 14
      Section: What if blood sugar is low? (below 4.0)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 121
      Part: Part 14
      Section: What if blood sugar is high? (above 12)
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 122
      Part: Part 14
      Section: What if the clotting test (INR) is high?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 122
      Part: Part 14
      Section: What if lactate is raised?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 122
      Part: Part 14
      Section: 14.3 Heart tracing problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 122
      Part: Part 14
      Section: What if the ECG is abnormal?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 123
      Part: Part 14
      Section: 14.4 Pregnancy, allergy and medicine problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 123
      Part: Part 14
      Section: What if the patient is pregnant?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 123
      Part: Part 14
      Section: What if pregnancy status is unknown or the patient declines a test?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 123
      Part: Part 14
      Section: What if the patient is allergic to an antibiotic?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 124
      Part: Part 14
      Section: What if an allergic reaction happens after a medicine?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 124
      Part: Part 14
      Section: What if the patient takes blood thinners?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 124
      Part: Part 14
      Section: What if the patient has diabetes?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 125
      Part: Part 14
      Section: What if the patient takes steroids, GLP-1 injections or SGLT2 tablets?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 125
      Part: Part 14
      Section: 14.5 Fasting problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 125
      Part: Part 14
      Section: What if the patient hasn't fasted?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 125
      Part: Part 14
      Section: What if fasting status is unknown?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 126
      Part: Part 14
      Section: 14.6 Scan and result problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 126
      Part: Part 14
      Section: What if imaging is inconclusive?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 126
      Part: Part 14
      Section: What if a test result doesn't make sense?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 126
      Part: Part 14
      Section: What if a laboratory result is delayed?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 127
      Part: Part 14
      Section: What if a critical result is phoned through?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 127
      Part: Part 14
      Section: 14.7 Deterioration and sepsis
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 127
      Part: Part 14
      Section: What if the patient deteriorates?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 127
      Part: Part 14
      Section: What if the patient becomes septic?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 128
      Part: Part 14
      Section: 14.8 Consent and communication problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 128
      Part: Part 14
      Section: What if the patient refuses surgery?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 128
      Part: Part 14
      Section: What if the patient cannot understand the consent discussion?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 128
      Part: Part 14
      Section: What if the patient is under 18?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 129
      Part: Part 14
      Section: 14.9 System problems
      Requirement: What-if method / ABCDE
      Files: whatif_library.csv
      Guides: B-R14, B-R17
      Status: Specified
      Validation / unresolved: —
  * 129
      Part: Part 14
      Section: What if equipment fails?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 129
      Part: Part 14
      Section: What if documents are missing?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 129
      Part: Part 14
      Section: What if the patient deteriorates during transfer?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 130
      Part: Part 14
      Section: What if there's an identity mismatch at theatre?
      Requirement: WHAT-IF branch (11 fields)
      Files: whatif_library.csv; Master Plan §What-if library
      Guides: B-R14, B-M19, B-R17
      Status: Specified
      Validation / unresolved: Row 'Clinician validation'
  * 131
      Part: Part 14B
      Section: Part 14B — When several problems happen at once
      Requirement: Stacking method (ladder, rules, clashes, recipe)
      Files: priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt
      Guides: B-R17, B-M19
      Status: Specified
      Validation / unresolved: New combinations approved before build (C)
  * 131
      Part: Part 14B
      Section: 14B.1 The priority order (use it every time)
      Requirement: Stacking method (ladder, rules, clashes, recipe)
      Files: priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt
      Guides: B-R17, B-M19
      Status: Specified
      Validation / unresolved: New combinations approved before build (C)
  * 131
      Part: Part 14B
      Section: 14B.2 Golden rules for stacking
      Requirement: Stacking method (ladder, rules, clashes, recipe)
      Files: priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt
      Guides: B-R17, B-M19
      Status: Specified
      Validation / unresolved: New combinations approved before build (C)
  * 132
      Part: Part 14B
      Section: 14B.3 Common treatment clashes and the safe resolution
      Requirement: Stacking method (ladder, rules, clashes, recipe)
      Files: priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt
      Guides: B-R17, B-M19
      Status: Specified
      Validation / unresolved: New combinations approved before build (C)
  * 133
      Part: Part 14B
      Section: 14B.4 Worked stacked cases
      Requirement: Stacking method (ladder, rules, clashes, recipe)
      Files: priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt
      Guides: B-R17, B-M19
      Status: Specified
      Validation / unresolved: New combinations approved before build (C)
  * 133
      Part: Part 14B
      Section: SC-1 (S2): Sepsis + penicillin anaphylaxis + AKI
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 133
      Part: Part 14B
      Section: SC-2 (S2): Positive pregnancy test + lower-right pain + low BP
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 133
      Part: Part 14B
      Section: SC-3 (S2): Pregnant (20 weeks) + appendicitis + fever
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 134
      Part: Part 14B
      Section: SC-4 (S1): High BP + poorly controlled diabetes
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 134
      Part: Part 14B
      Section: SC-5 (S1): On apixaban (taken this morning) + new fast AF
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 134
      Part: Part 14B
      Section: SC-6 (S1): Fasting breach + diabetes on insulin
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 134
      Part: Part 14B
      Section: SC-7 (S2): Unknown fasting + vomiting + opioid given + sleepy
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 134
      Part: Part 14B
      Section: SC-8 (S2): High potassium + AKI + sepsis
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 135
      Part: Part 14B
      Section: SC-9 (S1): Jaundice + fever + shaking (cholangitis) + on clopidogrel
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 135
      Part: Part 14B
      Section: SC-10 (S2): Language barrier + 17 years old + needs urgent surgery
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 135
      Part: Part 14B
      Section: SC-11 (S2): Refuses surgery + becomes septic later
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 135
      Part: Part 14B
      Section: SC-12 (S1): Haemolysed high potassium + genuinely low sodium + water tablets
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 136
      Part: Part 14B
      Section: SC-13 (S1): Anaemia + anticoagulant + needs G&S
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 136
      Part: Part 14B
      Section: SC-14 (S2): Equivocal ultrasound + deteriorating vital signs
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 136
      Part: Part 14B
      Section: SC-15 (S2): Anaphylaxis to antibiotic + still needs antibiotics
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 136
      Part: Part 14B
      Section: SC-16 (S1): GLP-1 injection + nausea + high BMI + sleep apnoea
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 136
      Part: Part 14B
      Section: SC-17 (S2): Sickle cell trait/thalassaemia + dehydration
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 137
      Part: Part 14B
      Section: SC-18 (S1): Missing consent + equipment fault + late list
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 137
      Part: Part 14B
      Section: SC-19 (S2): Low sugar + confusion + sepsis in a diabetic on SGLT2
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 137
      Part: Part 14B
      Section: SC-20 (S2): Older patient + appendicitis picture + weight loss + anaemia + on aspirin
      Requirement: Compact stacked case
      Files: stacked_cases_compact.csv; patient_library.csv
      Guides: B-M19, B-R17
      Status: Specified (P2)
      Validation / unresolved: Send to surgeon/anaesthetist before building (14B.5)
  * 137
      Part: Part 14B
      Section: 14B.5 How to build ANY new combination (recipe for the engineering
      Requirement: Stacking method (ladder, rules, clashes, recipe)
      Files: priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt
      Guides: B-R17, B-M19
      Status: Specified
      Validation / unresolved: New combinations approved before build (C)
  * 139
      Part: Part 15
      Section: Part 15 — The WHO Surgical Safety Checklist,
      Requirement: WHO checklist + extensions
      Files: who_checklist.csv
      Guides: B-R15, B-S10
      Status: Specified
      Validation / unresolved: Theatre governance local additions (C)
  * 141
      Part: Part 16
      Section: Part 16 — What the player actually sees
      Requirement: Player view
      Files: player_view.csv
      Guides: B-S7, B-S9, B-S10
      Status: Specified
      Validation / unresolved: —
  * 141
      Part: Part 16
      Section: Stage 1 — Arrival and identity
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 141
      Part: Part 16
      Section: Stage 2 — History
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 141
      Part: Part 16
      Section: Stage 3 — Vital signs
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 142
      Part: Part 16
      Section: Stage 4 — Examination
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 142
      Part: Part 16
      Section: Stage 5 — Risk and anaesthetic assessment
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 142
      Part: Part 16
      Section: Stage 6 — Tests and moving the patient
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 142
      Part: Part 16
      Section: Stage 7 — Results
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 143
      Part: Part 16
      Section: Stage 8 — Decide, treat, optimise
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 143
      Part: Part 16
      Section: Stage 9 — Consent
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 143
      Part: Part 16
      Section: Stage 10 — Ward preparation
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 143
      Part: Part 16
      Section: Stage 11 — Theatre preparation
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 144
      Part: Part 16
      Section: Stage 12 — Transfer and handover
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 144
      Part: Part 16
      Section: Stages 13–14 — Sign In and Time Out
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 144
      Part: Part 16
      Section: End states
      Requirement: Player view per stage / end states
      Files: player_view.csv; end_states.csv
      Guides: B-S7, B-S9, B-S10, B-M15
      Status: Specified
      Validation / unresolved: —
  * 145
      Part: Part 17
      Section: Part 17 — All the numbers in one place (with plain
      Requirement: All numbers
      Files: vitals_bands.csv; lab_bands.csv; key_rules.csv; all_numbers_raw.txt
      Guides: B-M11, B-M12, B-M18
      Status: Specified
      Validation / unresolved: —
  * 148
      Part: Part 18
      Section: Part 18 — Dictionary of every medical word and
      Requirement: Dictionary
      Files: glossary.csv; Implementation Guide glossary
      Guides: B-M18
      Status: Specified
      Validation / unresolved: —
  * 151
      Part: PART B
      Section: PART B — Clinical Accuracy Refinements (v5)
      Requirement: Clinical accuracy refinements + consistency audit
      Files: clinical_refinements.txt; Change report
      Guides: B-M12, B-M18
      Status: Specified
      Validation / unresolved: —
  * 151
      Part: PART B
      Section: B.1 Corrections and clarifications
      Requirement: Clinical accuracy refinements + consistency audit
      Files: clinical_refinements.txt; Change report
      Guides: B-M12, B-M18
      Status: Specified
      Validation / unresolved: —
  * 151
      Part: PART B
      Section: B.2 Consistency audit — concepts checked across the whole document
      Requirement: Clinical accuracy refinements + consistency audit
      Files: clinical_refinements.txt; Change report
      Guides: B-M12, B-M18
      Status: Specified
      Validation / unresolved: —
  * 153
      Part: PART C
      Section: PART C — Expanded VR Clinical Reasoning (Medical
      Requirement: VR reasoning cards / event table
      Files: vr_cards.csv; vr_event_table.txt
      Guides: B-M3, B-S10
      Status: Specified
      Validation / unresolved: —
  * 153
      Part: PART C
      Section: C.1 Identity check
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 153
      Part: PART C
      Section: C.2 History
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 153
      Part: PART C
      Section: C.3 Observations / NEWS2
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 154
      Part: PART C
      Section: C.4 Low SpO2
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 154
      Part: PART C
      Section: C.5 High BP (S1)
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 154
      Part: PART C
      Section: C.6 Fever (S1)
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 154
      Part: PART C
      Section: C.7 Possible sepsis
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 155
      Part: PART C
      Section: C.8 Potassium abnormal
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 155
      Part: PART C
      Section: C.9 Hypoglycaemia
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 155
      Part: PART C
      Section: C.10 Liver tests abnormal (S1)
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 155
      Part: PART C
      Section: C.11 Pregnancy possible
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 156
      Part: PART C
      Section: C.12 Appendicitis score
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 156
      Part: PART C
      Section: C.13 Imaging result
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 156
      Part: PART C
      Section: C.14 Medicine needed
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 157
      Part: PART C
      Section: C.15 Anticoagulant
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 157
      Part: PART C
      Section: C.16 Fasting
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 157
      Part: PART C
      Section: C.17 Consent
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 157
      Part: PART C
      Section: C.18 Ward preparation
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 158
      Part: PART C
      Section: C.19 Transfer & handover
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 158
      Part: PART C
      Section: C.20 WHO Sign In
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 158
      Part: PART C
      Section: C.21 WHO Time Out
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 159
      Part: PART C
      Section: C.22 Intra-operative event — ProcSense educational/simulation extension — clinician
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17, B-S12
      Status: Specified (P1 extension)
      Validation / unresolved: C.22 clinician validation required
  * 159
      Part: PART C
      Section: C.23 WHO Sign Out
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17, B-S12
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 159
      Part: PART C
      Section: C.24 Recovery handover & debrief
      Requirement: VR reasoning card
      Files: vr_cards.csv; Master Plan step sections
      Guides: B-M3, B-S10, B-R17, B-S12
      Status: Specified
      Validation / unresolved: C.22 clinician validation required
  * 163
      Part: PART D
      Section: PART D — Expanded Stacked Cases (full playable
      Requirement: Stacked cases
      Files: stacked_cases_full.json
      Guides: B-M19, B-M15
      Status: Specified
      Validation / unresolved: —
  * 163
      Part: PART D
      Section: STACKED CASE S1-A — Diabetes medication error: hypoglycaemia +
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 168
      Part: PART D
      Section: STACKED CASE S1-B — Anticoagulated AF + iron-deficiency anaemia +
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 173
      Part: PART D
      Section: STACKED CASE S1-C — True hyperkalaemia with ECG changes + acute
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 177
      Part: PART D
      Section: STACKED CASE S1-D — Patient awaiting elective cholecystectomy who
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 181
      Part: PART D
      Section: STACKED CASE S2-A — Appendicitis + dehydration/tachycardia + acute
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 186
      Part: PART D
      Section: STACKED CASE S2-B — Perforated appendicitis + high-risk sepsis +
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 190
      Part: PART D
      Section: STACKED CASE S2-C — Opioid sedation + vomiting + unknown/recent
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 193
      Part: PART D
      Section: STACKED CASE S2-D — Appendicitis in pregnancy (18 weeks) +
      Requirement: Full playable stacked case (20 sections)
      Files: stacked_cases_full.json; Master Plan §Stacked cases
      Guides: B-M19, B-M15, B-R17
      Status: Specified (P0/P1 per patient_library.csv)
      Validation / unresolved: Anaesthetist/surgeon per case
  * 198
      Part: PART D
      Section: PART D — Stacked-case master table (overview)
      Requirement: Stacked cases
      Files: stacked_cases_full.json
      Guides: B-M19, B-M15
      Status: Specified
      Validation / unresolved: —
  * 200
      Part: PART E
      Section: PART E — Equipment and Asset Mapping
      Requirement: Equipment inventory
      Files: equipment.csv
      Guides: B-S1, B-S9, B-R15
      Status: Specified
      Validation / unresolved: —
  * 204
      Part: PART F
      Section: PART F — S1 Master Scenario (Elective
      Requirement: S1 master scenario (20 stages)
      Files: flow_S1_20_stages.csv
      Guides: B-R12, B-S12
      Status: Specified
      Validation / unresolved: —
  * 210
      Part: PART G
      Section: PART G — S2 Master Scenario (Emergency
      Requirement: S2 master scenario (20 stages)
      Files: flow_S2_20_stages.csv
      Guides: B-R12, B-S12
      Status: Specified
      Validation / unresolved: —
  * 216
      Part: PART H
      Section: PART H — Clinical Evidence Audit (source-to-claim)
      Requirement: Evidence audit
      Files: evidence_audit.txt; Data Source Inventory
      Guides: B-M17
      Status: Specified
      Validation / unresolved: Clinician validation column
  * 218
      Part: PART H
      Section: H.2 v5.1 verification log — the four previously unverified items
      Requirement: Evidence audit
      Files: evidence_audit.txt; Data Source Inventory
      Guides: B-M17
      Status: Specified
      Validation / unresolved: Clinician validation column
  * 219
      Part: PART I
      Section: PART I — Clinician Validation, Institutional
      Requirement: Clinician validation & institutional adaptation
      Files: clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation
      Guides: B-M17
      Status: Specified — awaiting decisions
      Validation / unresolved: 15 decisions + checklist A–G
  * 219
      Part: PART I
      Section: I.1 Institutional adaptation (context-dependent items)
      Requirement: Clinician validation & institutional adaptation
      Files: clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation
      Guides: B-M17
      Status: Specified — awaiting decisions
      Validation / unresolved: 15 decisions + checklist A–G
  * 219
      Part: PART I
      Section: I.2 Final Clinician / Institutional Decisions Required
      Requirement: Clinician validation & institutional adaptation
      Files: clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation
      Guides: B-M17
      Status: Specified — awaiting decisions
      Validation / unresolved: 15 decisions + checklist A–G
  * 219
      Part: PART I
      Section: I.3 Clinician Final Validation Checklist
      Requirement: Clinician validation & institutional adaptation
      Files: clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation
      Guides: B-M17
      Status: Specified — awaiting decisions
      Validation / unresolved: 15 decisions + checklist A–G
  * 222
      Part: PART I
      Section: FINAL STATUS
      Requirement: Clinician validation & institutional adaptation
      Files: clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation
      Guides: B-M17
      Status: Specified — awaiting decisions
      Validation / unresolved: 15 decisions + checklist A–G
  * 223
      Part: Part 28
      Section: Part 28 — Sources (references)
      Requirement: Sources
      Files: sources.csv; Data Source Inventory
      Guides: B-M17
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 1. Patient arrival
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 1
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 2. Initial assessment
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 1
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Identifier set CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 3. Observations
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 3, 4
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Alarm limits CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 4. History
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 2, 4
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 5. Investigations
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 6
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Ordering rules CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 6. Results
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 7
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Lab ranges CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 7. Abnormal branches
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 7
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 8. Optimisation
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 8
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Antibiotic policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 9. Consent
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 9
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Law/form CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 10. Anaesthetic assessment
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 5
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 11. WHO Sign In
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 13
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 12. Theatre preparation (runs in parallel before the patient arrives)
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 11
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 13. WHO Time Out
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 14
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 14. Procedure start
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 14
      Guides: B-R12, B-S12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 15. Relevant intra-operative training events (ProcSense extension)
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 15
      Guides: B-R12, B-S12
      Status: Specified (P1 extension)
      Validation / unresolved: Surgeon to approve events
  * 
      Part: PART F
      Section: S1 master stage 16. WHO Sign Out
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 15
      Guides: B-R12, B-S12
      Status: Specified (P1 extension)
      Validation / unresolved: —
  * 
      Part: PART F
      Section: S1 master stage 17. Recovery / handover
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 15
      Guides: B-R12, B-S12
      Status: Specified (P1 extension)
      Validation / unresolved: Discharge criteria CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 18. Deterioration branches
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map
      Guides: B-R12, B-R18, B-R14
      Status: Specified (P1 extension)
      Validation / unresolved: Clocks (design) — clinician realism
  * 
      Part: PART F
      Section: S1 master stage 19. Emergency branches
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map
      Guides: B-R12, B-R18, B-R14
      Status: Specified
      Validation / unresolved: Protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
  * 
      Part: PART F
      Section: S1 master stage 20. Debrief
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S1_20_stages.csv; Master Plan §S1 master map
      Guides: B-R12, B-M15, B-S5
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 1. Patient arrival
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 1
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Triage system CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART G
      Section: S2 master stage 2. Initial assessment
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 1, 4
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 3. Observations
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 3
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Tool CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
  * 
      Part: PART G
      Section: S2 master stage 4. History
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 2, 4
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 5. Investigations
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 6
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Availability CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART G
      Section: S2 master stage 6. Results
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 7
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Score choice (surgeon)
  * 
      Part: PART G
      Section: S2 master stage 7. Abnormal branches
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 7
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 8. Optimisation (in parallel)
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 8
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Antibiotic policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
          CONFIRMATION
  * 
      Part: PART G
      Section: S2 master stage 9. Consent
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 9
      Guides: B-R12
      Status: Specified
      Validation / unresolved: Law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
  * 
      Part: PART G
      Section: S2 master stage 10. Anaesthetic assessment
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 5
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 11. WHO Sign In
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 13
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 12. Theatre preparation
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 11
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 13. WHO Time Out
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 14
      Guides: B-R12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 14. Procedure start
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 14
      Guides: B-R12, B-S12
      Status: Specified
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 15. Relevant intra-operative training events (ProcSense extension)
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 15
      Guides: B-R12, B-S12
      Status: Specified (P1 extension)
      Validation / unresolved: Surgeon to approve
  * 
      Part: PART G
      Section: S2 master stage 16. WHO Sign Out
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 15
      Guides: B-R12, B-S12
      Status: Specified (P1 extension)
      Validation / unresolved: —
  * 
      Part: PART G
      Section: S2 master stage 17. Recovery / handover
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 15
      Guides: B-R12, B-S12
      Status: Specified (P1 extension)
      Validation / unresolved: Policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
  * 
      Part: PART G
      Section: S2 master stage 18. Deterioration branches
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map
      Guides: B-R12, B-R18, B-R14
      Status: Specified (P1 extension)
      Validation / unresolved: Clocks (design)
  * 
      Part: PART G
      Section: S2 master stage 19. Emergency branches
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map
      Guides: B-R12, B-R18, B-R14
      Status: Specified
      Validation / unresolved: Protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION
  * 
      Part: PART G
      Section: S2 master stage 20. Debrief
      Requirement: Scenario flow stage (10 fields)
      Files: flow_S2_20_stages.csv; Master Plan §S2 master map
      Guides: B-R12, B-M15, B-S5
      Status: Specified
      Validation / unresolved: —
```


## 46. REFERENCES

```text
Walkthrough Part 28 (verbatim list). Data/licence sources are in ProcSense_Data_Source_Inventory.csv.

  - World Health Organization. WHO Surgical Safety Checklist (2009) and Implementation Manual. Geneva: WHO;
    2009.
  - WHO Collaborating Centre for Patient Safety Solutions. Communication during patient hand-overs. Patient
    Safety Solutions vol 1, solution 3; 2007.
  - Gelb AW et al. World Health Organization–World Federation of Societies of Anaesthesiologists (WHO-WFSA)
    International Standards for a Safe Practice of Anesthesia. Can J Anesth 2018.
  - Association of Anaesthetists. Checking anaesthetic equipment (2019); Recommendations for standards of
    monitoring during anaesthesia and recovery (2021).
  - General Medical Council. Decision making and consent (2020). Montgomery v Lanarkshire Health Board [2015]
    UKSC 11.
  - Resuscitation Council UK. Emergency treatment of anaphylaxis (2021); ABCDE approach; ALS algorithms
    (2021).
  - NICE NG45. Routine preoperative tests for elective surgery (2016).
  - NICE NG180. Perioperative care in adults (2020). NICE QS216 Perioperative care (2022).
  - NICE NG89. Venous thromboembolism in over 16s (2018, updated 2019).
  - NICE NG125. Surgical site infections: prevention and treatment (2019, updated 2020).
  - NICE CG65. Hypothermia: prevention and management in adults having surgery (2008, updated 2016).
  - NICE CG174. Intravenous fluid therapy in adults in hospital (2013, updated 2017).
  - NICE NG24. Blood transfusion (2015).
  - Hartle A et al. The measurement of adult blood pressure and management of hypertension before elective
    surgery: AAGBI and British Hypertension Society. Anaesthesia 2016;71:326–37.
  - Mancia G et al. 2023 ESH Guidelines for the management of arterial hypertension. J Hypertens 2023. McEvoy
    JW et al. 2024 ESC Guidelines for elevated blood pressure and hypertension. Eur Heart J 2024.
  - Halvorsen S et al. 2022 ESC Guidelines on cardiovascular assessment and management of patients undergoing
    non-cardiac surgery. Eur Heart J 2022.
  - American Society of Anesthesiologists. ASA Physical Status Classification System (updated 2020).
  - American Society of Anesthesiologists. Practice guidelines for preoperative fasting (2017) and Joshi GP et
    al. modular update, Anesthesiology 2023.
  - Kindel TL et al. Multisociety clinical practice guidance for the safe use of GLP-1 receptor agonists in
    the perioperative period (ASA/AGA/ASMBS/ISPCOP/SAGES). Surg Obes Relat Dis 2024;20:1183–6; Clin
    Gastroenterol Hepatol 2024.
  - Centre for Perioperative Care (CPOC). Guideline for perioperative care for people with diabetes mellitus
    undergoing elective and emergency surgery (2021, updated 2023). JBDS-IP hypoglycaemia and DKA guidelines.
  - Woodcock T et al. Guidelines for the management of glucocorticoids during the peri-operative period
    (Association of Anaesthetists / Society for Endocrinology). Anaesthesia 2020.
  - Douketis JD et al. Perioperative management of patients with atrial fibrillation receiving a DOAC (PAUSE).
    JAMA Intern Med 2019. Doherty JU et al. 2017 ACC Expert Consensus Decision Pathway for periprocedural
    management of anticoagulation.
  - Bratzler DW et al. Clinical practice guidelines for antimicrobial prophylaxis in surgery
    (ASHP/IDSA/SIS/SHEA). Am J Health-Syst Pharm 2013.
  - British Thoracic Society. Guideline for oxygen use in healthcare and emergency settings (2017).
  - Chung F et al. STOP-Bang questionnaire. Anesthesiology 2008; Chest 2016.
  - Royal College of Physicians. National Early Warning Score (NEWS) 2 (2017).
  - Singer M et al. The Third International Consensus Definitions for Sepsis and Septic Shock (Sepsis-3). JAMA
    2016.
  - Evans L et al. Surviving Sepsis Campaign: international guidelines 2021. Crit Care Med 2021.
  - NICE NG253. Suspected sepsis in over 16s: identification and early management in acute hospital settings
    using NEWS2 (November 2025); supersedes the NEWS2-related parts of NG51.
  - KDIGO Clinical Practice Guideline for Acute Kidney Injury (2012); KDIGO CKD guideline (2024). NICE NG148
    Acute kidney injury (2019).
  - UK Kidney Association. Clinical practice guideline: treatment of acute hyperkalaemia in adults (2023).
  - Royal College of Pathologists. The communication of critical and unexpected pathology results (2017).
  - US FDA. Pulse oximeter accuracy and limitations: safety communication (2021). NHS Race and Health
    Observatory. Pulse oximetry and racial bias (2021).
  - NICE CG188. Gallstone disease: diagnosis and management (2014). NICE QS104 (2015).
  - Yokoe M et al. Tokyo Guidelines 2018: diagnostic criteria and severity grading of acute cholecystitis;
    Kiriyama S et al. TG18 acute cholangitis. J Hepatobiliary Pancreat Sci 2018.
  - Buxbaum JL et al. ASGE guideline on the role of endoscopy in the evaluation and management of
    choledocholithiasis. Gastrointest Endosc 2019;89:1075–1105.
  - Manes G et al. ESGE guideline: endoscopic management of common bile duct stones (2019).
  - Kwo PY et al. ACG Clinical Guideline: evaluation of abnormal liver chemistries. Am J Gastroenterol 2017.
  - Banks PA et al. Revised Atlanta classification of acute pancreatitis (2012).
  - StatPearls: Laparoscopic cholecystectomy; Cholecystitis (NCBI Bookshelf, current editions).
  - Di Saverio S et al. Diagnosis and treatment of acute appendicitis: 2020 update of the WSES Jerusalem
    guidelines. World J Emerg Surg 2020;15:27. (2025 special-populations update — verify access.)
  - Alvarado A. A practical score for the early diagnosis of acute appendicitis. Ann Emerg Med 1986.
  - Andersson M, Andersson RE. The Appendicitis Inflammatory Response score. World J Surg 2008.
  - Sammalkorpi HE et al. A new adult appendicitis score improves diagnostic accuracy. BMC Gastroenterol 2014.
  - Ghali MS et al. Adult appendicitis score versus Alvarado score: comparative study. Surg Open Sci
    2023;14:96–102.
  - CODA Collaborative. A randomized trial comparing antibiotics with appendectomy for appendicitis. NEJM
    2020.
  - Salminen P et al. Five-year follow-up of antibiotic therapy for uncomplicated acute appendicitis (APPAC).
    JAMA 2018.
  - StatPearls: Acute appendicitis; Appendectomy (NCBI Bookshelf).
  - BMJ Best Practice. Acute appendicitis — criteria (accessed 2026).
  - NICE NG126. Ectopic pregnancy and miscarriage (2019, updated 2023).
  - Pearl JP et al. SAGES guidelines for the use of laparoscopy during pregnancy. Surg Endosc 2017.
  - WHO. Guideline on haemoglobin cutoffs to define anaemia in individuals and populations (2024).
  - Muñoz M et al. International consensus statement on the peri-operative management of anaemia and iron
    deficiency. Anaesthesia 2017.
  - Estcourt LJ et al. British Society for Haematology guidelines for the use of platelet transfusions (2017).
  - WHO Expert Consultation. Appropriate body-mass index for Asian populations. Lancet 2004.
  - British National Formulary (BNF) — paracetamol, ondansetron, enoxaparin monographs (current edition).
  - openFDA Drug Label API — contraindications/warnings only (no doses imported).
  - Target institution's approved clinical protocols (applied at implementation).
  - Pathology Harmony Group. Agreed adult clinical biochemistry reference intervals (January 2011). UK.
  - UK Kidney Association. Clinical practice guideline: treatment of acute hyperkalaemia in adults (2023).
  - Spasovski G et al. Clinical practice guideline on diagnosis and treatment of hyponatraemia. Eur J
    Endocrinol 2014.
  - Kwo PY, Cohen SM, Lim JK. ACG clinical guideline: evaluation of abnormal liver chemistries. Am J
    Gastroenterol 2017.
  - NICE NG253. Suspected sepsis in over 16s: identification and early management in acute hospital settings
    using NEWS2 (November 2025).
  - Di Saverio S et al. Diagnosis and treatment of acute appendicitis: 2025 edition of the WSES Jerusalem
    guidelines. JAMA Surg 2026 (doi:10.1001/jamasurg.2025.6218).
  - Yokoe M et al. Tokyo Guidelines 2018 (and TG07/TG13 imaging criteria: wall >4 mm; long axis >8 cm, short
    axis >4 cm).
  - Pucher PH et al. (SAGES Safe Cholecystectomy Task Force). Outcome trends and safety measures after 30
    years of laparoscopic cholecystectomy: systematic review and pooled data analysis. Surg Endosc 2018.
  - CODA Collaborative. A randomized trial comparing antibiotics with appendectomy for appendicitis. N Engl J
    Med 2020;383:1907–19.
  - Salminen P et al. Five-year follow-up of antibiotic therapy for uncomplicated acute appendicitis in the
    APPAC randomized clinical trial. JAMA 2018;320:1259–65.
  - El-Boghdadly K et al. Elective peri-operative management of adults taking GLP-1 receptor agonists, GIP
    agonists and SGLT2 inhibitors: multidisciplinary consensus statement (Association of Anaesthetists, ABCD,
    BOMSS, CPOC, JBDS-IP, RCoA, SOBA, UKCPA). Anaesthesia 2025;80:412–24.
  - Kindel TL et al. Multisociety clinical practice guidance for the safe use of GLP-1 receptor agonists in
    the perioperative period. Surg Obes Relat Dis 2024.
  - SPAQI. Perioperative management of patients taking GLP-1 receptor agonists: multidisciplinary consensus
    statement. Br J Anaesth 2025;135:48–78.
  - Association of Anaesthetists, Obstetric Anaesthetists' Association, Regional Anaesthesia UK. Regional
    anaesthesia and patients with abnormalities of coagulation. Anaesthesia 2013.
  - Rautaharju PM et al. AHA/ACCF/HRS recommendations for the standardization and interpretation of the ECG,
    part IV (2009); Drew BJ et al. AHA scientific statement on prevention of torsade de pointes (2010).
  - Koch E et al. Shock index in the emergency department: utility and limitations. Open Access Emerg Med
    2019;11:179–99. Rivers E et al. Comparison of the shock index and conventional vital signs to identify
    acute critical illness in the emergency department. Ann Emerg Med 1994. Allgöwer M, Burri C. Dtsch Med
    Wochenschr 1967 (origin).
  - NICE CG183. Drug allergy: diagnosis and management (2014).
  - Royal College of Anaesthetists. 6th National Audit Project (NAP6): perioperative anaphylaxis (2018).
```

