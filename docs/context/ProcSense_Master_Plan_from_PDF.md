<!-- Converted from ProcSense_Master_Plan.pdf — the PDF is generated from a structured document; its headings, paragraphs, lists, tables (every row and cell), diagrams and callouts are written out as Markdown and checked word-for-word against the text extracted from the PDF file. Format conversion only: no summarising, rewriting or correction. -->

> **Project addendum (09 Oct 2026 — not part of the converted document).** The work is now organised as `docs/plans/` (tier P0/P1/P2 → phase → step files, "full scope, no deadlines"); this document is the v2.0 proposal. The schedule and gates below are kept for history only — the owner confirmed on 09 Oct 2026 that there are no deadlines (DT-035). Core engine location changed by ADR-0001. Outstanding work: `docs/deferred-tasks.md`. Decision history: `ProcSense_Change_Report.md` §G.

*PROCSENSE · MASTER PLAN*

# Master Plan

Blueprint rebuilt on Medical Walkthrough v5.1 — 15 steps, scenarios, architecture, schedule, validation

**Ruhan · Sannan · Moiz**  
Development team

**Ma'am Noor-ul-Ain**  
Project supervisor

**29 Sep → 31 Dec 2026**  
14-week delivery

Version 2.0 · 6 Oct 2026 · rebuilt on ProcSense Medical Walkthrough v5.1 (master medical source) · plan start Tue 29 Sep 2026 · FAST NUCES Islamabad

---

*Contents*

## What's inside

- **01** Source of truth and document set
- **02** Safety, role and content principles
- **03** Project objective and scope
- **04** Architecture changes required by the walkthrough
- **05** The 15 steps and the five decision outcomes
- **06** Step 1 — Patient arrival and identification
- **07** Step 2 — Asking questions (the history)
- **08** Step 3 — Checking vital signs
- **09** Step 4 — Examining the patient
- **10** Step 5 — Risk and anaesthetic assessment
- **11** Step 6 — Choosing the tests
- **12** Step 7 — Getting and understanding the results
- **13** Step 8 — Deciding, treating and optimising
- **14** Step 9 — Consent
- **15** Step 10 — Preparing the patient (ward/ED checklist)
- **16** Step 11 — Theatre preparation
- **17** Step 12 — Transfer and handover (SBAR)
- **18** Step 13 — WHO Sign In (before the patient goes to sleep)
- **19** Step 14 — WHO Time Out (before the first cut) — the finish line
- **20** Step 15 — WHO Sign Out and handover to recovery (optional)
- **21** Patient & case library
- **22** Stacked problems: method
- **23** WHAT-IF library (WT Part 14)
- **24** Clinical numbers and tools (configuration)
- **25** System architecture
- **26** AI architecture — controlled AI around a deterministic core
- **27** AI Authority and Safety Boundaries
- **28** AI-assisted complete case composition (Scenario Composer)
- **29** AI-assisted assessment across the whole workflow
- **30** State-aware AI & reactive loop
- **31** Patient persona & behaviour realisation
- **32** AI staff & environment interaction
- **33** AI + unexpected events
- **34** Adaptive difficulty
- **35** AI debrief from session evidence
- **36** General synthetic-data pipeline
- **37** AI requirements matrix
- **38** Implementation order & dependencies
- **39** End-to-end AI example — S1
- **40** End-to-end AI example — S2
- **41** AI tests, risks and Definition of Done
- **42** Team & responsibilities
- **43** Schedule (starts Tue 29 Sep 2026)
- **44** Clinical validation (WT Part I)
- **45** Walkthrough traceability matrix
- **46** References


## 01. Source of truth and document set

Read this first.

> **Master medical source** ProcSense_Medical_Walkthrough_v5_1.pdf is the master medical source of truth. ProcSense_Clinical_Research.txt has been removed from the active project and is no longer referenced. All clinical content in this document is taken from the walkthrough (cited as 'WT' with part/step and page) or from the content package generated from it. The walkthrough status is READY FOR CLINICIAN VALIDATION — not clinically approved.

| Deliverable | Purpose |
|---|---|
| ProcSense_Medical_Walkthrough_v5_1.pdf | Master medical source (unchanged; owned by the clinical side) |
| ProcSense_Master_Plan (pdf/txt) | Complete blueprint: scope, architecture, 15 steps, scenarios, schedule, validation |
| ProcSense_Implementation_Guide (pdf/txt) | Stage guides 01–15 + 50 build guides, beginner-friendly |
| ProcSense_Content/ (CSV/JSON) | Machine-readable walkthrough content + traceability matrix (310 rows) |
| ProcSense_Data_Source_Inventory.csv | Every data need, its source/URL or USE AI/UNRESOLVED |
| ProcSense_Team_Board.md | One-page Notion board |
| ProcSense_Change_Report.md | What changed and why; traceability summary; outstanding decisions |


## 02. Safety, role and content principles

Applied in every system.

> **Safety statement (WT)** ProcSense is a teaching game, not a medical tool or decision-support system. Every patient is invented; all case values are simulator-generated. Every number is a draft value for building and review. Local hospital rules always win. A qualified surgeon and anaesthetist must approve content before it is final.

> **Role convention (WT Part A, 8.1)** The trainee recognises problems, communicates (SBAR) and initiates the correct workflow. A qualified clinician confirms treatment decisions and doses; nurses/clinicians administer. The anaesthetist decides anaesthetic fitness; the senior surgeon decides whether and when to operate. The trainee never independently prescribes, selects a dose or administers.

| Type | Label | Meaning | What the team does |
|---|---|---|---|
| A | CLINICAL REFERENCE — SOURCE BACKED | Value/rule taken directly from a recognised guideline | Build as written; clinician checks applicability |
| B | CLINICAL INTERPRETATION | Clinically derived reading where no source gives the exact gameplay band | Default behaviour; expect adjustment |
| C | CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION | Varies between institutions/countries | Placeholder with draft value; hospital/clinician decides |
| D | SIMULATOR DESIGN VALUE (PROVISIONAL) | Invented for the game; not a clinical rule | Keep easy to change |
| DOSE-EX | EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION | Standardised dose shown for teaching | Display only as example, never instruction |
| DOSE-PS | PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED | No single safe number | Show the decision, not a number |

| Kind | Number kind | Meaning | Example |
|---|---|---|---|
| REFERENCE | Reference value | Generally expected in healthy people | K 3.5–5.3 |
| ABNORMAL | Abnormal value | Outside usual range — often harmless, always interpreted | K 5.4 |
| CONCERN | Clinical concern threshold | Triggers additional assessment | K ≥5.5 (UKKA: ECG) |
| CRITICAL | Critical / emergency threshold | Potentially immediate danger | K ≥6.5 or any ECG change |
| SIM_TRIGGER | Simulator trigger | ProcSense chooses it to create a training scenario | S1-J K 6.4 'haemolysed' |

> **Decision principle (WT)** Abnormal finding → verify (repeat, check technique/sample) → assess severity and context → investigate → senior/anaesthetic review → the team decides whether to proceed, optimise, delay or escalate. Thresholds tell the trainee when to think and whom to involve; they are not automatic cancellation rules.


## 03. Project objective and scope

**ProcSense** is a Unity VR (Meta Quest 2/3) training simulator for the pre-operative preparation process: the trainee takes a synthetic patient from arrival to the WHO Time Out ('ready for the first cut') for **S1 — elective laparoscopic cholecystectomy** and **S2 — emergency appendectomy**, following the walkthrough's 15 steps, branches and stacked problems.

| In scope (P0) | P1 | Out of scope (WT 2.3) |
|---|---|---|
| S1 + S2, Steps 1–14, 17 P0 cases, what-ifs bound to them, decision engine, NEWS2/sepsis, labs realism, WHO checklist, end states + timeline debrief, clinician validation | Step 15 Sign Out + recovery; intra-operative training events (extension); remaining 10 variants + 6 stacked cases; voice/AI features | The operation itself, anaesthetic drugs and doses, hand skills; real patients; certification |

|  | S1 — planned gallbladder | S2 — emergency appendix |
|---|---|---|
| Danger now? | Usually no | Possibly yes, worsening |
| Diagnosis known? | Yes (scan) | No — must be worked out |
| Time pressure | Low | High (deterioration clock) |
| If a problem is found | Fix first or postpone (success) | Fix in parallel; operate when safe |
| Eaten? | Usually fasted | Often unknown → full stomach |
| Mood | Calm | Pain, fear, fever |


## 04. Architecture changes required by the walkthrough

What the old design could not support, what changed, and where.

| Change | Old design could not support | Revised design | How it supports the walkthrough | Affected files / guides |
|---|---|---|---|---|
| 1. 15 canonical Steps (was 14 stages) | Vitals and examination were one stage; Sign Out was 'Stage 14'. The walkthrough defines 15 steps with a 21-question template each. | Stage model = WT Part 11 Steps 1–15 (Step 3 vitals, Step 4 examination, Step 15 Sign Out optional). Every step stores the 21 answers verbatim. | Every step section; all IDs (L/E/A/T) renumbered | Master Plan, Guide 01–15, Team Board, B-R12 |
| 2. Two-level flow: Steps + 20-stage scenario maps | One linear path; no parallel tracks; deterioration only as events. | Scenario master maps (Parts F/G) order steps per scenario, run tracks in parallel (theatre prep; S2 optimisation) and allow deterioration/emergency stages anywhere. | S2 overlap and clocks are represented faithfully | B-R12, flow_S1/S2_20_stages.csv |
| 3. Five decision outcomes + master decision diagram | Decision points were ad-hoc option lists. | DecisionOutcome {SAFE, INVESTIGATE, OPTIMISE, REVIEW, EMERGENCY} re-evaluated whenever information changes (WT Part 3, Step 8). | The central skill the walkthrough wants rewarded | B-R12, B-R17 |
| 4. Five end states | Outcomes were ready-stable / ready-with-issues / not-ready / harmed. | READY_SAFE, POSTPONED_APPROPRIATELY (success), REDIRECTED_APPROPRIATELY, SAFE_BUT_DELAYED, SAFETY_FAILURE (WT Part 16). | Postponement counts as success; redirection supported | B-M15, B-S5 |
| 5. Problem registry, priority ladder, clash checker | No representation of several simultaneous problems. | Problems typed (6 types), ranked by the 7-step ladder (14B.1) and Part D P1–P4; treatment clashes (14B.3) and three drug checks. | Stacked cases (Part D, 14B) become playable | B-R17 (new), B-M19 |
| 6. Role workflow: request → confirm → administer | Trainee could 'administer' treatments. | Trainee recognises/initiates; clinician NPC confirms drug+dose; nurse NPC administers and challenges unsafe requests (beginner mode) or reaction occurs (advanced). | Matches WT role convention and safety statement | B-R11, B-M14, B-R4, B-M7 |
| 7. Evidence labels, number kinds, dose policy | Single '[CLINICAL VALIDATION REQUIRED]' flag. | Every content item: evidence type A/B/C/D, number kind (reference/abnormal/concern/critical/simulator trigger), dose policy (EDUCATIONAL EXAMPLE vs PATIENT-SPECIFIC vs INSTITUTIONAL). | Type C items become institutional placeholders; Type D easy to change | B-M11, B-M12, B-R2 |
| 8. Realistic laboratory subsystem | Results generator only; no artefacts or critical calls. | Sample quality flags, lab comments, repeat with different tube, previous/baseline values, units, critical-result phone call with read-back, acknowledgement blocker, Type D turnaround. | WT Part 5 behaviour and Step 7 misses | B-M13 |
| 9. Early-warning & sepsis engine | NEWS2 only as a number. | NEWS2 (Scale 1/2, single-3), NG253 tiers with timers and reassessment intervals, maternity exception, lateness computed for debrief. | WT 4.9–4.10 and Patient D timeline | B-R18 (new) |
| 10. Deterioration clocks | Generic deterioration event. | Type D clock (HR +5–10, NEWS2 +1 per \~30 min untreated); speed set by the most dangerous untreated problem. | S2 time pressure as written | B-R13, B-R17 |
| 11. Hidden facts & 'surfaces later' consequences | Hidden information existed but no late-surfacing rules. | Facts revealed only if asked; if never asked they surface at the WT moment (e.g. anaesthetist at Sign In → case stops). | WT Step 2 and Part D histories | B-R3, B-M19 |
| 12. WHO checklist exact wording + extensions | Paraphrased items. | WHO 2009 wording; ProcSense extensions flagged and displayed separately; green lights; repeated skip stops case. | WT Part 15 | B-R15, B-S10 |
| 13. Content types for walkthrough structures | Generic blocks only. | Step, FlowStage, WhatIf (47), VR card (24), StackedCase (20 sections), Patient (namespaced IDs), Medicine, LabResult, Problem. | All structured WT parts load as data | B-M11, B-M18 (new), B-M19 (new) |
| 14. Scope extension (P1) | Ended at Time Out; Stage 14 optional. | Optional intra-operative training events (labelled extension), Sign Out and recovery handover, debrief stage. | WT C.22–C.24, F/G 14–17 | B-S12 (new), B-R15 |
| 15. Institutional adaptation layer | 'LOCAL-v1' answers. | 15 institutional decisions (Part I) held in one configuration; Type C values marked placeholders until decided. | WT Part I | B-M17, B-M11 |
| 16. Expanded NPC roster | Nurse, porter, anaesthetist, surgeon, scrub. | Adds triage nurse, theatre reception, lab caller, radiologist, gynaecology/obstetrics, pharmacist, gastroenterology, cardiology, critical care, interpreter, guardian, recovery nurse. | WT 1.4 and throughout | B-R4, B-S3 |


## 05. The 15 steps and the five decision outcomes

| Step | Name | Priority | WT page | Build guides |
|---|---|---|---|---|
| 1 | Patient arrival and identification | P0 | 74 | B-R11, B-S7, B-S9, B-M11, B-M18 |
| 2 | Asking questions (the history) | P0 | 76 | B-R6, B-R3, B-S10, B-M3, B-M19 |
| 3 | Checking vital signs | P0 | 78 | B-R13, B-R18, B-S9, B-S10 |
| 4 | Examining the patient | P0 | 80 | B-S9, B-S2, B-M3 |
| 5 | Risk and anaesthetic assessment | P0 | 82 | B-R12, B-M20, B-M12 |
| 6 | Choosing the tests | P0 | 84 | B-R16, B-M13, B-S7 |
| 7 | Getting and understanding the results | P0 | 86 | B-M13, B-S10, B-R17, B-M20 |
| 8 | Deciding, treating and optimising | P0 | 88 | B-M14, B-R17, B-R13, B-R18, B-R14 |
| 9 | Consent | P0 | 91 | B-R6, B-M3, B-R3, B-S10 |
| 10 | Preparing the patient (ward/ED checklist) | P0 | 93 | B-R15, B-S10 |
| 11 | Theatre preparation | P0 | 95 | B-S9, B-S1, B-R15, B-R14 |
| 12 | Transfer and handover (SBAR) | P0 | 97 | B-R16, B-M15, B-R8 |
| 13 | WHO Sign In (before the patient goes to sleep) | P0 | 99 | B-R15, B-S10, B-R4 |
| 14 | WHO Time Out (before the first cut) — the finish line | P0 | 101 | B-R15, B-S10 |
| 15 | WHO Sign Out and handover to recovery (optional) | P1 | 102 | B-R15, B-M15, B-R9, B-S12 |

| Outcome | Name | Meaning | Example |
|---|---|---|---|
| SAFE | Safe to continue | Everything fits and is within acceptable limits | Normal numbers, fasted, consent ready |
| INVESTIGATE | Needs more investigation | We don't yet know enough to decide | Abnormal LFTs → bile-duct imaging |
| OPTIMISE | Needs treatment / optimisation | A problem can be fixed (now in S2, or before a re-booked date in S1) | Dehydrated → fluids; poor diabetes → postpone S1 |
| REVIEW | Needs senior or anaesthetic review | Decision is beyond the trainee or affects anaesthesia | Ate 3 h ago; irregular rhythm |
| EMERGENCY | Emergency | The patient may be in danger now | BP very low, SpO2 falling, severe allergic reaction |

```text
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
```

| End state | Name | Meaning |
|---|---|---|
| READY_SAFE | Ready for incision — safe | All blockers resolved; correct pathway |
| POSTPONED_APPROPRIATELY | Appropriately postponed (S1) | Correct reason found and communicated — counts as SUCCESS |
| REDIRECTED_APPROPRIATELY | Appropriately redirected | Diagnostic diversion (suspected ectopic → gynaecology); cholangitis → biliary drainage; inflamed gallbladder → acute pathway |
| SAFE_BUT_DELAYED | Safe but delayed | Right outcome with avoidable delays |
| SAFETY_FAILURE | Safety failure | A blocker was violated or a life-threatening event missed |


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
```

| Stage | Title | Trainee action | Abnormal branches | If missed |
|---|---|---|---|---|
| 1 | Patient arrival | Greets, introduces self; checks bands | Unwell on arrival (fever, jaundice, acute pain) → assess for acute illness | Poor start; acute illness missed |
| 2 | Initial assessment | Compare answer vs band + notes; ask allergy reaction details | Mismatch → stop; wrong procedure described → surgical team | Wrong-patient / allergy events |
| 3 | Observations | Attach devices; count RR; repeat abnormal readings | Fever, tachycardia, BP ≥180/110, low SpO2 → verify, assess, review | Unexplained abnormality surfaces at Sign In |
| 4 | History | Structured questions; hidden facts only if asked | Jaundice/fever/constant pain; drug or fasting problem; pregnancy possible | Hidden facts cause late stops |
| 5 | Investigations | Order panel; bedside labelling after ID check | Unindicated tests logged; mislabelled → incident | Over/under-testing in debrief |
| 6 | Results | Open each; repeat suspicious; sign off | See 7 | Unacknowledged critical result blocks progress |
| 7 | Abnormal branches | Verify → assess severity/context → investigate → senior/anaesthetic review | Per branch; cholangitis = emergency | Unsafe proceed → safety failure |
| 8 | Optimisation | Identify needs; request via workflow; clinician confirms | Drug clash/allergy → nurse challenge | Wrong plan → debrief |
| 9 | Consent | Dialogue choices; teach-back | No capacity / language / minor / refusal | Invalid consent blocker |
| 10 | Anaesthetic assessment | Propose ASA; report findings | Uncontrolled comorbidity; breach; difficult airway | Wrong ASA corrected |
| 11 | WHO Sign In | Answer/lead items | Any 'no' → stop and resolve | Skipped item → challenge/stop |
| 12 | Theatre preparation (runs in parallel before the patient arrives) | Inspect/confirm; report faults | Fault → fix/delay | Time Out cannot complete |
| 13 | WHO Time Out | Take part in order | Antibiotic/imaging/equipment gap → fix, repeat | Stop/debrief |
| 14 | Procedure start | Observe | — | — |
| 15 | Relevant intra-operative training events (ProcSense extension) | Communicate; respond to requests | Team decision | Freezing → debrief |
| 16 | WHO Sign Out | Participate | Count discrepancy → search/escalate | Specimen error |
| 17 | Recovery / handover | Hand over | Pain, PONV, bleeding → recovery team | Omissions |
| 18 | Deterioration branches | ABCDE; call; initiate workflow | Emergency escalation | Worsening per Part 12 clocks |
| 19 | Emergency branches | Recognise; call; support team | Emergency team | Arrest branch |
| 20 | Debrief | Review | — | — |


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
```

| Stage | Title | Trainee action | Abnormal branches | If missed |
|---|---|---|---|---|
| 1 | Patient arrival | Note triage; start pain pathway | Shocked on arrival → resus area | Delay |
| 2 | Initial assessment | ID and allergy; request analgesia and IV access via workflow | Mismatch → resolve | Wrong patient/drug |
| 3 | Observations | Attach; count RR; repeat | NEWS2 ≥5 / single 3 → review; ≥7 high risk | Trend missed → deterioration |
| 4 | History | Structured questions | Differential diagnosis / diagnostic diversion: conditions that mimic appendicitis (e.g. ectopic pregnancy, ovarian torsion, urinary stone) — pregnancy status and clinical assessment can redirect the pathway; allergy | Ectopic missed |
| 5 | Investigations | Order; arrange monitored transfer if unstable | Unstable → monitored transfer | Corridor event |
| 6 | Results | Acknowledge; score | Equivocal/negative/complicated | 'Not seen' treated as negative |
| 7 | Abnormal branches | Verify → assess → initiate workflow | Per branch | Deterioration |
| 8 | Optimisation (in parallel) | Identify need; request; clinician confirms; nurse administers | No response → escalate | Clock speeds up |
| 9 | Consent | Dialogue; teach-back | No capacity → senior/emergency route | Invalid consent |
| 10 | Anaesthetic assessment | Report fasting times; findings | Airway/aspiration issues | Aspiration |
| 11 | WHO Sign In | Answer/lead | Any 'no' → resolve | Stop |
| 12 | Theatre preparation | Confirm | Fault → substitute | Delay |
| 13 | WHO Time Out | Participate | Re-dose/fix | Stop |
| 14 | Procedure start | Observe | — | — |
| 15 | Relevant intra-operative training events (ProcSense extension) | Communicate | Team plan | Debrief |
| 16 | WHO Sign Out | Participate | Count/specimen issue | Error |
| 17 | Recovery / handover | Hand over | Sepsis/ileus/bleeding signs | Omission |
| 18 | Deterioration branches | ABCDE; SBAR; initiate workflow | Emergency | Worsening |
| 19 | Emergency branches | Call; support | Emergency team | Arrest |
| 20 | Debrief | Review | — | — |


# The 15 steps

Walkthrough specification + VR implementation for each step.


## 06. Step 1 — Patient arrival and identification

Priority P0 · walkthrough p.74 · build guides: B-R11, B-S7, B-S9, B-M11, B-M18


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Meeting the patient, making 100% sure we have the right person, checking allergies, and opening their
hospital record.
- **Why are we doing it?** Wrong-patient errors (wrong blood sample, wrong medicine, wrong operation) are some of the most serious
mistakes in hospitals. An allergy that isn't recorded can kill when a medicine is given. Everything later depends
on this step CLINICAL REFERENCE — SOURCE BACKED .
- **What does the trainee actually do?** 1. Introduce yourself: name and role ("Hello, I'm Dr Ruhan, one of the surgical doctors").
2. Ask the patient to SAY their full name and date of birth. Never read it to them ("You're Ayesha Khan,
right?") — anxious people say yes to anything.
3. Compare their answer with the wristband AND the notes. At least two identifiers must match CLINICAL
REFERENCE — SOURCE BACKED (locally usually name + date of birth + hospital/MR number CONTEXT-DEPENDENT —
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ).
4. Ask: "Do you have any allergies? What happened when you had it?" Check the allergy band (often red
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ).
5. Ask: "Can you tell me what operation you're here for / what's brought you in?"
6. S1: check a responsible adult can take them home and stay for 24 hours (needed for day surgery) CLINICAL
INTERPRETATION .
7. Record arrival time; open the admission record.
- **What does the trainee see?** S1: day-surgery bay; calm patient sitting on a trolley in their own clothes; folder; nurse. S2: Emergency
Department cubicle; patient curled up holding the lower right tummy; a relative; triage note on a clipboard.
- **What information do they receive?** Patient's spoken name and date of birth, wristband text, folder details, allergy answer. S2 triage note, e.g.
"19M, RIF pain 20 h, HR 108, T 37.9, pain 8/10, category: very urgent" (RIF = right iliac fossa = lower right
tummy).
- **What does that information mean?** If all identifiers match, we can safely take samples and give treatment. An allergy with a severe reaction
(swelling, breathing trouble) means that medicine and related ones must be avoided.
- **What is the normal/reference value?** Identity: every identifier matches. Allergy: 'none known' OR allergy recorded with reaction and band in place.
- **What counts as abnormal?** Any mismatch (spelling, date, MR number); an allergy the patient mentions that isn't recorded; patient
describing a different operation or side.
- **What counts as seriously concerning?** Patient cannot confirm identity (unconscious, confused, language barrier) and documents are missing; history
of anaphylaxis.
- **What could cause the abnormal result?** Admin errors, two patients with similar names, patient confusion, missing wristband.
- **What should the trainee check next?** 1. Mismatch → stop; ask the nurse/admissions to resolve before anything else.
2. Patient can't confirm → use wristband + notes + relative + interpreter; follow the hospital unknown-patient
policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
3. New allergy → update record, apply band, tell the nurse.
4. Different operation described → call the surgical team before continuing.
- **What happens if the trainee finds nothing wrong?** Go to STEP 2 (history). The game unlocks blood tests and medicines only now.
- **What happens if the trainee finds something abnormal?** Stop and fix it first. Nothing else is allowed until identity is certain.
- **What happens if the abnormality is mild?** Small spelling error in notes with all other identifiers matching → nurse corrects, then continue.
- **What happens if it is severe?** Real identity doubt → no samples, drugs, transfer. Severe allergy → flagged everywhere; affects antibiotic
choice later.
- **When do we call the senior doctor?** Patient describes a different operation or side than booked; identity cannot be resolved.
- **When do we call the anaesthetist?** Severe allergy history (especially to anaesthetic drugs, latex, chlorhexidine).
- **When might surgery be delayed?** S1: if identity or the planned operation can't be confirmed.
- **When should emergency surgery continue despite the abnormality?** S2: an unconscious emergency patient can be treated using emergency identification procedures; life-saving
treatment is not withheld while paperwork is fixed CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN
CONFIRMATION .
- **What happens if the trainee misses it?** Reading the name to the patient → patient agrees with a wrong name → later the blood sample belongs to
someone else ('wrong blood in tube' event). Missing an allergy → allergic reaction later (Part 14).
- **What happens after the correct action?** Nurse confirms: "Identity checked, allergy band on." The game marks the patient 'identified'.
- **Medical words used here** Identifier A piece of information that uniquely identifies a person (full name, date of birth, hospital number).
MR number Medical record number — the hospital's ID for the patient.
Anaphylaxis A severe, life-threatening allergic reaction.
Triage Sorting patients by urgency on arrival.
- **Where this comes from** WHO Surgical Safety Checklist (2009); NICE NG180 Perioperative care; Manchester Triage System concept.
- **Clinician validation** Hospital: identifier set, allergy band colour, unknown-patient procedure, triage system.


### What the player sees (WT Part 16 — Stage 1)

- **On screen / in front of the player** S1: day-surgery bay, patient seated, folder on the trolley, nurse nearby. S2: ED cubicle, patient curled on trolley, relative, triage clipboard.
- **Patient — what they say** S1: "Hello, I'm here for my gallbladder operation." S2: groaning; "It really hurts…"
- **Monitor — numbers** Blank until devices attached (S2 triage numbers on clipboard).
- **Interaction** Wristband (rotate wrist to read), folder, allergy band, speak to patient/relative.
- **Information revealed** Name/DOB spoken by patient; folder pages; allergy details after asking 'what happened?'.
- **Decision** Is this the right patient? Any allergy?
- **Correct action →** Nurse: "Thanks, I'll update the record." Tests/medicines unlock.
- **Incorrect action →** Reading the name to the patient → patient agrees (even if wrong in a variant) → later mismatch event.
- **Missed abnormality →** Unrecorded allergy → reaction later.
- **Escalation →** Can call admissions/senior for mismatch.


#### VR reasoning card C.1 — Identity check

- **Why** Prevent wrong-patient care
- **Trainee action** Open questions; compare wristband + notes; allergy
- **What the trainee sees** Patient, wristband, folder
- **Information** Name/DOB/MR no.; allergy
- **Interpretation** Match = safe to proceed
- **Decision** Is identity certain?
- **Correct action** Actions unlock
- **Incorrect action** Reading name to patient → agrees wrongly
- **Missed abnormality** Wrong-blood event later
- **Escalation** Admissions/nurse
- **Consequence** Mismatch blocks all actions
- **Next step** History

**Scenario maps:** S1 stages 1 Patient arrival, 2 Initial assessment; S2 stages 1 Patient arrival, 2 Initial assessment.


### Implementation (FYP design)

- **Real workflow** Nurse admits, doctor clerks; identity is re-checked at every hand-off.
- **FYP simulation** The trainee performs the identity check at first contact; nurse NPC has already applied the wristband.
- **FYP simplification** Admissions paperwork is a single tablet 'Admission record'; no bed-management system.
- **AI** None required. Optional P1: patient voices answers via LLM phrasing grounded on the patient card (identity facts are never AI-generated).

- **Room** S1: ward bay (bed, curtain, bedside locker, chart holder). S2: ED cubicle (trolley, wall monitor, curtain).
- **Patient** S1 sitting on bed in own clothes/gown; S2 lying on trolley, guarding abdomen.
- **Staff** Nurse NPC at bedside (hands over referral/triage note).
- **Equipment** Wristband on patient's wrist; notes folder/tablet; allergy band (if applicable); ED: wall monitor (not yet connected).
- **Available interactions** Talk menu, grab wristband / notes, tablet 'Admission' tab.

**USER MUST:**

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

**SYSTEM THEN:**

1. Emits say(INTRODUCE_SELF) and ask(identity) actions to the Core.
2. Reveals identity answer from the patient profile (may differ in a mismatch scenario).
3. When wristband is read and notes opened, enables 'Confirm identity'.
4. Compares: profile.identity == band == notes.
5. Sets clinical.identity_verified = true (only if all match) and logs evidence.
6. Updates behaviour: anxiety −1, trust +1 when introduction happened first.
7. Reading the name to the patient → patient agrees even in a mismatch variant → WRONG-BLOOD-IN-TUBE event later (WT Step 1).
8. Blood tests and medicines UNLOCK only after identity is certain (WT Step 1).
9. Unconscious S2 patient → emergency identification route; life-saving treatment not withheld (Type C).

- **If correct →** Identity verified flag set; tablet shows green 'ID confirmed'; Step 2 topics unlock; score item ID-01 met.
- **If incorrect →** If the trainee confirms identity when a mismatch exists (scenario), or starts any intervention before confirming, a CRITICAL score item is logged; practice mode shows a red banner 'Identity not confirmed'.
- **If information missing →** If the wristband is missing (scenario), 'Confirm identity' stays disabled; trainee must ask the nurse NPC to apply one after checking records.
- **If safety condition fails →** Any treatment/sample/transfer action attempted before identity_verified is REJECTED by the Core with reason 'Identity not confirmed' (logged).

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L1-01 | verify_identity | patient present in same location | profile.id == band.id == notes.id | identity_verified=true; unlock history | if mismatch: mismatch_state; critical if user confirms anyway | Green tick / red banner | Nurse nods / nurse frowns + 'Let me check' | ST2_HISTORY or ST1_MISMATCH |
| L1-02 | any treatment/sample before L1-01 | — | identity_verified == false | — | action rejected + CRITICAL item | 'Identity not confirmed' | Hand-raise 'stop' icon | unchanged |
| L1-03 | say INTRODUCE_SELF | first contact | first act this session? | trust+1, anxiety−1 | if skipped: minor communication item | — | Patient makes eye contact | unchanged |
| L1-04 | report_mismatch | band/notes differ | scenario mismatch active | mismatch resolved by nurse NPC (new band) | if no mismatch: minor 'unnecessary delay' | Nurse line | Nurse leaves & returns with band | ST1_ARRIVAL |
| L1-WT1 | read_name_to_patient | — | flag misidentification | — | Wrong-blood event armed | Debrief | — | risk.wrong_blood |
| L1-WT2 | allergy_reaction_asked | allergy present | reaction recorded | Severe allergy flagged everywhere (antibiotic choice later) | 'Penicillin allergy' without reaction → incorrect (WT 14.4) | — | — | allergies[] |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E1-A | Trainee reads name to patient ('You are Ayesha Khan?') | Patient says 'Yes' even in mismatch variant | Closed question can't verify identity | Ask open question | Logs closed-question identity check (minor); in mismatch variant proceeds to wrong-identity risk | Allowed (practice) with warning | 'Use an open question to confirm identity' | ST1_ARRIVAL | Trainee re-asks with open question |
| E1-B | Wristband belongs to another patient (mismatch variant) | Band shows different DOB | Band ≠ patient answer | Stop, report to nurse | Blocks treatment actions; nurse fetches correct band | Blocked until resolved | 'Identifiers do not match — resolve before continuing' | ST1_MISMATCH | Nurse re-bands after records check |
| E1-C | Allergy said by patient but no allergy alert | Patient mentions 'penicillin rash' during greeting | Documented status is wrong | Flag and update (Step 2) | Creates pending task 'allergy alert' | Allowed | Tablet reminder | unchanged | Resolved in Step 2 |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Allergy band | ✓/✓ | 1, 7, 13 | Allergy flag (details by asking) | Missing band → reaction branch |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Wheelchair | ✓/— | 1, 6 | Transport cue | Unstable patient must not go by wheelchair |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Implement ActionAPI verbs verify_identity/report_mismatch and the identity rule L1-01/L1-02 in Core (pure C#), with xUnit tests T01-1..3. Add identity facts to PatientProfile + disclosure rule for 'ask(identity)'. Sannan — Build ward-bay and ED-cubicle anchors in the provided environment; place bed/trolley, patient, nurse NPC. Make wristband interactable (XRI poke/grab → UI card) and nurse hand-out animation A1-01; ID compare card UI. Moiz — Write scenario content for arrival (S1 referral, S2 triage note) and synthetic demographics generator (no real names). Author rules L1-01..L1-04 in content/rules with sources WT Part 15 (WHO SSC 2009)/Institutional protocol (WT Type C) and status SOURCE_IDENTIFIED.

**Validation:** Number/type of identifiers and allergy-band colour follow the supervising hospital's policy. [CLINICAL VALIDATION REQUIRED] · Procedure when a patient cannot confirm identity. [CLINICAL VALIDATION REQUIRED] · Hospital: identifier set, allergy band colour, unknown-patient procedure, triage system.


## 07. Step 2 — Asking questions (the history)

Priority P0 · walkthrough p.76 · build guides: B-R6, B-R3, B-S10, B-M3, B-M19


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** A structured conversation to learn what's wrong, what other illnesses and medicines the patient has, and
anything that makes surgery unsafe.
- **Why are we doing it?** Most of the information needed for a diagnosis comes from the history. Many dangers (blood thinners, recent
food, pregnancy) are invisible unless you ask CLINICAL INTERPRETATION .
- **What does the trainee actually do?** 1. Main complaint: "What's the problem? Where's the pain? When did it start? Does it move? What makes it
worse? 0–10?"
2. Red-flag questions (warning signs). S1: fever? yellow skin/eyes? dark urine/pale stools? constant pain
over 24 h? pain through to back? S2: pain moved from belly button? vomiting? appetite? fever? urine
symptoms? diarrhoea? women: last period, chance of pregnancy, vaginal bleeding, shoulder-tip pain; men:
testicle pain.
3. Other illnesses: heart, lungs, blood pressure, diabetes, kidney, liver, bleeding problems, previous clots,
sleep apnoea, thalassaemia/sickle cell.
4. Medicines: everything, especially blood thinners (and when last taken), diabetes medicines, GLP-1
injections, steroids, BP tablets, the pill, herbal remedies.
5. Allergies: what and what happened.
6. Previous anaesthetics: any problems; family members with serious anaesthetic problems.
7. Social: smoking, alcohol, drugs, who's at home.
8. Last food and drink: exact time and what.
9. Pregnancy (whenever pregnancy is possible for this patient): asked privately and sensitively; test offered
with consent if there is any doubt CLINICAL REFERENCE — SOURCE BACKED (NICE NG45).
10. Summarise back and ask "Anything else you want to tell me?"
- **What does the trainee see?** The patient (and maybe a relative); the clerking form fills in as answers are given.
- **What information do they receive?** Spoken answers, e.g. S1: "The pain comes after oily food, under my right ribs, then goes after an hour or two."
If asked about blood thinners: "I take apixaban — I had it this morning." S2: "It started around my belly button
last night and now it's here [points lower right]. I threw up once."
- **What does that information mean?** S1 'comes and goes after fatty food' = typical gallstone pain (biliary colic) — fits the plan. 'Constant pain with
fever' = possible inflamed gallbladder. S2 'pain moved + vomiting + no appetite' = classic appendicitis story.
- **What is the normal/reference value?** S1: typical gallstone attacks, no red flags, medicines manageable, fasted correctly. S2: there is no 'normal' —
we're collecting clues.
- **What counts as abnormal?** Any red-flag answer; a blood thinner not stopped as planned; food within fasting limits; possible pregnancy;
new allergy.
- **What counts as seriously concerning?** Fever + jaundice + pain (possible cholangitis); positive pregnancy chance with pain and dizziness (possible
ectopic); chest pain or breathlessness; severe allergy history.
- **What could cause the abnormal result?** The disease progressing (S1 becoming acute), other conditions mimicking it, patient not following instructions,
missed medicine plan.
- **What should the trainee check next?** 1. Red flags → focused examination (Step 4) and relevant tests.
2. Blood thinner/diabetes/GLP-1 → medicine plan (Part 8).
3. Food → fasting branch (Part 9).
4. Possible pregnancy → pregnancy test with consent (Step 6).
- **What happens if the trainee finds nothing wrong?** Go to STEP 3 (vital signs).
- **What happens if the trainee finds something abnormal?** Record it, then follow the branch in 'what next'.
- **What happens if the abnormality is mild?** e.g. smoker, mild reflux → note and tell anaesthetist; continue.
- **What happens if it is severe?** e.g. took apixaban this morning before planned surgery → S1 will be postponed; e.g. chest pain → emergency
assessment.
- **When do we call the senior doctor?** Red flags suggesting complication (S1 jaundice/fever; S2 a possible appendicitis mimic such as ectopic
pregnancy); patient describes a different problem than booked.
- **When do we call the anaesthetist?** Fasting breach; previous anaesthetic problems; sleep apnoea; GLP-1 injections; blood thinners; heart/lung
disease.
- **When might surgery be delayed?** S1: blood thinner not stopped; recent food; possible pregnancy; new serious symptoms.
- **When should emergency surgery continue despite the abnormality?** S2: recent food or blood thinners do NOT stop emergency surgery — they change how it's done (RSI, reversal
decisions).
- **What happens if the trainee misses it?** Hidden facts stay hidden: e.g. the morning apixaban is discovered by the anaesthetist at Sign In → case stops,
debrief 'missed medicine history'. Missing the pregnancy question → blocked at checklist.
- **What happens after the correct action?** The clerking note is complete; decisions in later steps unlock.
- **Medical words used here** Red flag A warning symptom that suggests something more serious.
Biliary colic Cramping gallstone pain that comes and goes.
Jaundice Yellow skin/eyes from bilirubin build-up.
LMP Last menstrual period — date the last period started.
Ectopic pregnancy Pregnancy growing outside the womb; can rupture and bleed.
- **Where this comes from** NICE NG45 (pregnancy question); NICE CG188; WSES 2020; StatPearls (acute appendicitis).
- **Clinician validation** Surgeon: which facts are hidden until asked; patient answers per variant.


### What the player sees (WT Part 16 — Stage 2)

- **On screen / in front of the player** Patient (and relative); clerking form.
- **Patient — what they say** Volunteers only the main complaint; other facts only if asked (e.g. "I took apixaban this morning").
- **Monitor — numbers** Unchanged.
- **Interaction** Question menu or voice: pain, red flags, medicines, allergies, anaesthetics, social, fasting, pregnancy (privacy option to ask relatives to step out).
- **Information revealed** Each answer adds a line to the clerking note.
- **Decision** What's the story? Any red flags?
- **Correct action →** Relevant facts unlock correct branches.
- **Incorrect action →** Skipping fasting/medicine/pregnancy questions → blockers later.
- **Missed abnormality →** Yellow-eyes comment ignored → abnormal LFTs force it later.
- **Escalation →** Red flags → call senior.


#### VR reasoning card C.2 — History

- **Why** Most diagnoses and dangers come from questions
- **Trainee action** Structured questions incl. red flags, drugs, fasting, pregnancy
- **What the trainee sees** Patient, clerking form
- **Information** Answers (hidden until asked)
- **Interpretation** Red flags change pathway
- **Decision** Any blocker or red flag?
- **Correct action** Correct branch triggered
- **Incorrect action** Skipped questions
- **Missed abnormality** Hidden facts surface at Sign In
- **Escalation** Senior for red flags
- **Consequence** Case may stop later
- **Next step** Observations


#### VR reasoning card C.11 — Pregnancy possible

- **Why** Changes diagnosis and safety
- **Trainee action** Ask privately; consent; test
- **What the trainee sees** Test kit
- **Information** Result
- **Interpretation** Positive + pain: ectopic pregnancy is a differential diagnosis that can redirect the pathway
- **Decision** Pathway change?
- **Correct action** Correct team
- **Incorrect action** Not asking
- **Missed abnormality** Safety failure
- **Escalation** Senior; gynaecology/obstetrics
- **Consequence** Pathway switch
- **Next step** Continue/redirect


#### VR reasoning card C.15 — Anticoagulant

- **Why** Bleeding vs clot risk
- **Trainee action** Ask last dose time; renal function
- **What the trainee sees** Drug list
- **Information** Times
- **Interpretation** Within stop window?
- **Decision** Proceed/defer?
- **Correct action** Correct decision
- **Incorrect action** Not asking
- **Missed abnormality** Sign In stop
- **Escalation** Anaesthetist/senior
- **Consequence** Deferral
- **Next step** Plan


#### VR reasoning card C.16 — Fasting

- **Why** Aspiration risk
- **Trainee action** Exact times/type; GLP-1
- **What the trainee sees** Patient
- **Information** Times
- **Interpretation** Breach? Full stomach?
- **Decision** Inform anaesthetist?
- **Correct action** Re-time / RSI
- **Incorrect action** Rounding times
- **Missed abnormality** Aspiration
- **Escalation** Anaesthetist
- **Consequence** Timing change
- **Next step** Prep

**Scenario maps:** S1 stages 4 History; S2 stages 4 History.

| What-if | Branch | Correct action |
|---|---|---|
| WI-08 | What if breathing is very fast? (25 or more) | Treat cause; sepsis bundle if infection. |
| WI-26 | What if the patient is pregnant? | S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise → gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok. |
| WI-27 | What if pregnancy status is unknown or the patient declines a test? | Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior + anaesthetist decide whether to proceed. |
| WI-28 | What if the patient is allergic to an antibiotic? | Record; choose antibiotic by hospital rule (mild rash: cefazolin often allowed; severe: per policy) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . |
| WI-29 | What if an allergic reaction happens after a medicine? | STOP the drug; call for help; lie flat (sit up if struggling to breathe); call the emergency team; the responsible clinician gives IM adrenaline (adult standard dose 500 micrograms, repeat after 5 min if no improvement EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ); high-flow oxygen; IV fluid bolus per Resuscitation Council UK (adults 500–1000 mL crystalloid). |
| WI-30 | What if the patient takes blood thinners? | S1: proceed only if stopped per plan (Part 8); otherwise postpone; never stop antiplatelets after a recent stent without the heart team. S2: senior/haematology decide reversal PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED . |
| WI-31 | What if the patient has diabetes? | Medicine plan (Part 8); first on list if possible; check sugar at least hourly; target 6–10. |
| WI-32 | What if the patient takes steroids, GLP-1 injections or SGLT2 tablets? | Per Part 8/9. |
| WI-33 | What if the patient hasn't fasted? | Tell anaesthetist honestly. |
| WI-34 | What if fasting status is unknown? | Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In. |


### Implementation (FYP design)

- **Real workflow** A full clerking takes 15–30 min.
- **FYP simulation** History is a grouped question menu (P0) or free speech (P1); answers come from the patient card.
- **FYP simplification** Systems review compressed to 6 relevant items; social history only where it affects the case (e.g. no one at home → not day case).
- **AI** P1 PROPOSED FYP AI FEATURE: (a) LLM phrases patient answers in persona voice from approved facts (grounding validator); (b) ASR converts spoken questions to topics; (c) communication classifier labels acts (debrief-only unless validated). NOT a clinical standard.

- **Room** Same as Step 1.
- **Patient** Sitting (S1) / lying curled (S2).
- **Staff** Nurse may leave to do other tasks (environment event).
- **Equipment** Tablet with Ask menu; medicine packets prop (optional).
- **Available interactions** Ask menu groups: Complaint, Past history, Medicines, Allergies, Anaesthetic, Social, Intake, Pregnancy (eligible only).

**USER MUST:**

1. Open 'Ask' on the tablet (or speak — P1).
2. Ask complaint/HPC questions (SOCRATES group).
3. Ask past medical/surgical history.
4. Ask drug history; optionally inspect medicine packets.
5. Ask allergies AND the reaction.
6. Ask previous anaesthetic problems.
7. Ask last food and last drink separately.
8. If eligible: ask about possibility of pregnancy (sensitive option).
9. Summarise and invite questions.
10. Ask main complaint, red flags (S1 fever/jaundice/dark urine/pale stools/constant >24 h/back pain; S2 migration/vomiting/appetite/fever/urinary/diarrhoea; women LMP, pregnancy, PV bleeding, shoulder-tip; men testicular pain).
11. Ask exact last food AND drink (time + type).
12. Ask pregnancy privately (privacy option: relatives step out); offer test with consent if any doubt.
13. Ask anticoagulant, diabetes, GLP-1, SGLT2, steroid drugs and the time of the last dose.
14. Summarise; 'Anything else?'

**SYSTEM THEN:**

1. Each ask(topic) looks up the patient card disclosure rule.
2. If trust ≥ threshold (or topic asked directly) the hidden fact is revealed; else vague answer.
3. Revealed facts appear in the 'Known' panel of the tablet.
4. Fasting rule R-FAST-01 is evaluated when both last-food and last-drink are known.
5. Allergy disclosure not matching chart creates pending 'update allergy alert'.
6. Behaviour: jargon without explanation lowers understanding; summarising raises trust.
7. Patient volunteers ONLY the main complaint; every other fact is HIDDEN until asked (WT Part 16 St.2).
8. Each answer adds a line to the clerking note.
9. Skipped medicine/fasting/pregnancy questions → hidden fact surfaces later (e.g. anaesthetist at Sign In → case stops; debrief 'missed medicine history'); missed pregnancy question → blocked at checklist.

- **If correct →** Known panel filled; goals G-HX, G-ALLERGY, G-FASTING (and G-PREG if eligible) complete; relevant decision points (DP-S1-FAST) open.
- **If incorrect →** Missing key topics creates later consequences (e.g. allergy unknown → hypersensitivity event risk in Step 8; fasting unknown → Sign In discrepancy in Step 13).
- **If information missing →** Patient answers 'I'm not sure' → trainee can check notes, ask nurse NPC, or record 'uncertain'.
- **If safety condition fails →** If allergy is not asked before Step 8 treatment, the treatment check 'allergy status known' fails → treatment blocked in practice mode (warning), allowed but CRITICAL in assessment mode.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L2-01 | ask(allergies) | identity_verified | profile.allergies disclosed? | allergies_known updated | — | Known panel entry | Patient nods | ST2_HISTORY |
| L2-02 | ask(last_food)+ask(last_drink) | — | compare with fasting rule | npo.verified=true; if breach → DP-S1-FAST | if never asked: later critical at Sign In | 'Fasting: meets rule / breach' | — | ST2 or DP |
| L2-03 | ask(pregnancy) | eligible (female, age band) | asked sensitively (option type) | pregnancy discussion documented | asked insensitively → minor communication; not asked → goal pending | — | Patient embarrassed idle if insensitive | [CLINICAL VALIDATION REQUIRED] |
| L2-04 | ask(medications) | — | reveal meds incl. hidden anticoagulant/contraceptive | meds list; decisions created | — | Known panel | — | ST2 |
| L2-05 | summarise() | ≥5 topics asked | — | trust+1, understanding+1 | — | — | Patient smiles | ST3_EXAM |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E2-A | Allergy not asked | Nothing obvious now | Allergy field empty on tablet | Ask allergies before any drug | Leaves allergy_known=false; enables E-HYPERSENSITIVITY eligibility later | Allowed | Hint (practice) at Step 8 | ST2 | Resolved when asked |
| E2-B | Only 'last meal' asked, not drinks | Fasting shows 'partial' | Both solids and fluids matter | Ask both | Fasting rule not evaluated | Allowed | 'Fasting status incomplete' | ST2 | Resolved when both asked |
| E2-C | Patient discloses tea with milk 3 h ago (S1-V2) | Answer on panel | Milk counts as non-clear fluid [CLINICAL VALIDATION REQUIRED] | Inform anaesthetist (DP-S1-FAST) | Opens DP-S1-FAST | Allowed | Decision card | DP_S1_FAST | Step 5 decision |
| E2-D | Pregnancy not asked in eligible patient | Nothing | Eligibility shown on tablet | Ask sensitively; test if doubt | Goal G-PREG pending; blocks 'ready' later | Blocked at readiness | 'Pregnancy status not established' | ST2 | Ask + test |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV2-1 E-PT-DISCLOSES-INTAKE | Trainee asks intake OR nurse NPC: 'She asked for a cup of tea earlier' | Patient admits recent tea/snack | — | Speech line | A fasting breach | Inform anaesthetist; delay per decision | If ignored: critical at Sign In; aspiration-risk flag |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Implement disclosure rules (trust threshold, direct-ask) and fasting rule evaluator; unit tests. P1: /dialogue endpoint with grounding validator. Sannan — Ask-menu UI groups + Known panel + subtitles; patient talk/lip-sync; confused/wince blend shapes. Moiz — Write history cards for S1-V1..8 and S2-V1..8 (facts, hidden flags, answers) from WT Part 2/5D (NICE CG188)/18/20; fasting rule content from WT Part 9 (ASA fasting) marked [CLINICAL VALIDATION REQUIRED] Pre-render voice lines (Piper offline).

**Validation:** Hidden-information rules (which facts are only disclosed when asked). [CLINICAL VALIDATION REQUIRED] · Symptom descriptions for each patient variant. [CLINICAL VALIDATION REQUIRED] · Fasting interval values used by the rule engine. [CLINICAL VALIDATION REQUIRED] · Surgeon: which facts are hidden until asked; patient answers per variant.


## 08. Step 3 — Checking vital signs

Priority P0 · walkthrough p.78 · build guides: B-R13, B-R18, B-S9, B-S10


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Measuring blood pressure, heart rate, breathing rate, oxygen level, temperature, alertness, and pain score,
then calculating the NEWS2 early-warning score.
- **Why are we doing it?** Tells us if the patient is stable now and gives a baseline to compare against later (Part 4).
- **What does the trainee actually do?** 1. Attach the oxygen finger clip; read oxygen % and heart rate; look at the wave.
2. Put on the BP cuff; wait for the reading.
3. Count breaths for 30–60 seconds.
4. Measure temperature.
5. Check alertness (ACVPU).
6. Ask pain score 0–10.
7. Feel the pulse — regular or irregular?
8. Weight/height if not recorded; finger-prick sugar if diabetic.
9. Calculate NEWS2 and pick the right response.
10. Repeat any odd reading.
- **What does the trainee see?** A blank monitor that fills in as devices are attached; NEWS2 chart with any previous readings.
- **What information do they receive?** S1 typical: HR 76, BP 124/78, RR 14, SpO2 98% on air, T 36.7, Alert, pain 0, NEWS2 0. S2 typical: HR 104, BP
128/76, RR 18, SpO2 98%, T 37.9, Alert, pain 7, NEWS2 1.
- **What does that information mean?** S1 values are all normal → stable. S2: fast heart and slightly raised temperature from pain and inflammation —
expected in appendicitis but must be watched.
- **What is the normal/reference value?** Clinical reference ranges: HR 60–100 at rest (NEWS2 scores 0 only for 51–90); BP below 140/90; RR 12–20;
SpO2 96%+ on air (target 94–98%); T 36.1–37.9 °C; Alert; NEWS2 0 CLINICAL REFERENCE — SOURCE BACKED (RCP
NEWS2; BTS 2017; ESH 2023).
- **What counts as abnormal?** Any value in the amber zones of Part 4 (e.g. HR 91–110, BP 101–110, RR 21–24, SpO2 94–95, T 38.0–39.0);
NEWS2 1–4.
- **What counts as seriously concerning?** Any red value: HR ≥131 or ≤40; top BP ≤90; RR ≥25 or ≤8; SpO2 ≤91; T ≤35.0; new confusion. NEWS2 ≥5 or
any single parameter scoring 3; NEWS2 ≥7 = emergency.
- **What could cause the abnormal result?** See Part 4: pain, anxiety, dehydration, infection/sepsis, bleeding, abnormal heart rhythm, medicines, lung
problems.
- **What should the trainee check next?** 1. Repeat the abnormal reading (correct technique).
2. Look at the whole pattern (Part 4.11).
3. Look at the patient: pale? sweaty? breathing hard?
4. Treat obvious causes (pain relief, fluids).
5. Re-measure after 30–60 minutes (sooner if red).
- **What happens if the trainee finds nothing wrong?** Go to STEP 4 (examination).
- **What happens if the trainee finds something abnormal?** Follow the relevant decision diagram in Part 4.
- **What happens if the abnormality is mild?** Treat the likely cause and re-check; continue meanwhile.
- **What happens if it is severe?** Stop the routine pathway: ABCDE emergency assessment (Part 14) and call for help.
- **When do we call the senior doctor?** NEWS2 5–6 (urgent), ≥7 (emergency), any single 3, or persistent abnormality without explanation. In S1, ANY
NEWS2 above 1 needs explaining.
- **When do we call the anaesthetist?** BP ≥180/110 (S1), low oxygen, very fast/slow/irregular heart, low BP, high BMI/sleep apnoea.
- **When might surgery be delayed?** S1: BP ≥180/110 on repeat; fever; new irregular rhythm; any unexplained abnormal vital sign.
- **When should emergency surgery continue despite the abnormality?** S2: fast heart, fever, even low BP from sepsis do NOT stop surgery — they make it more urgent, after
resuscitation (fluids, antibiotics) because the infected appendix is the cause.
- **What happens if the trainee misses it?** Fever in an S1 patient missed → anaesthetist finds 38.6 °C at Sign In → case stops. S2 rising heart rate missed
→ patient deteriorates into sepsis.
- **What happens after the correct action?** Nurse sets the observation frequency according to NEWS2; values become the baseline.
- **Medical words used here** NEWS2 National Early Warning Score 2 (Part 4.9).
ACVPU Alert, Confusion, Voice, Pain, Unresponsive.
- **Where this comes from** RCP NEWS2 2017; AAGBI/BHS 2016; BTS 2017; NICE CG65.
- **Clinician validation** Hospital early-warning tool; anaesthetist alarm thresholds.


### What the player sees (WT Part 16 — Stage 3)

- **On screen / in front of the player** Monitor, finger clip, BP cuff, thermometer, ECG leads, NEWS2 chart.
- **Patient — what they say** "Is this going to hurt?"
- **Monitor — numbers** SpO2 + wave + HR after clip; BP after \~30–40 s; RR must be counted by watching chest; temperature from thermometer.
- **Interaction** Attach devices; count breaths; feel pulse; fill NEWS2.
- **Information revealed** Numbers; NEWS2 total.
- **Decision** Normal? Concerning? Emergency?
- **Correct action →** Nurse sets observation frequency.
- **Incorrect action →** Accepting a high BP without repeat → anaesthetist flags later.
- **Missed abnormality →** Rising trend ignored → deterioration branch.
- **Escalation →** NEWS2 5+ → call senior; 7+ → emergency call.


#### VR reasoning card C.3 — Observations / NEWS2

- **Why** Detect instability; baseline
- **Trainee action** Attach devices; count RR; calculate NEWS2
- **What the trainee sees** Monitor, cuff, probe, chart
- **Information** Values + score
- **Interpretation** Score = urgency of response, not diagnosis
- **Decision** Which response tier?
- **Correct action** Correct obs frequency/escalation
- **Incorrect action** Accepting one reading without repeat
- **Missed abnormality** Trend missed → deterioration
- **Escalation** Senior if NEWS2 ≥5/single 3
- **Consequence** Deterioration clock
- **Next step** Examination


#### VR reasoning card C.4 — Low SpO2

- **Why** Possible hypoxaemia or artefact
- **Trainee action** Look, assess A/B, check probe, target, oxygen per protocol, cause, reassess
- **What the trainee sees** Patient, waveform, masks
- **Information** SpO2 + waveform quality
- **Interpretation** Real vs artefact; target range
- **Decision** Oxygen needed? Cause?
- **Correct action** SpO2 to target; cause found
- **Incorrect action** Blind high-flow O2 / ignoring
- **Missed abnormality** Hypoxia worsens
- **Escalation** Anaesthetist
- **Consequence** Breathing branch
- **Next step** Continue


#### VR reasoning card C.5 — High BP (S1)

- **Why** Peri-operative CV risk
- **Trainee action** Repeat correctly; GP readings; symptoms
- **What the trainee sees** Cuff, monitor
- **Information** Repeat values
- **Interpretation** <180/110 proceed; ≥180/110 review
- **Decision** Defer or proceed?
- **Correct action** Correct decision documented
- **Incorrect action** Cancel on one reading / rapid lowering
- **Missed abnormality** Avoidable cancellation
- **Escalation** Anaesthetist
- **Consequence** Deferral or proceed
- **Next step** Continue


#### VR reasoning card C.6 — Fever (S1)

- **Why** Infection in planned patient
- **Trainee action** Examine (Murphy's, jaundice), bloods, US
- **What the trainee sees** Thermometer, patient
- **Information** T, WBC, CRP, LFTs
- **Interpretation** Acute cholecystitis/cholangitis
- **Decision** Is this still elective?
- **Correct action** Acute pathway
- **Incorrect action** Paracetamol and proceed
- **Missed abnormality** Stopped at Sign In / sepsis
- **Escalation** Senior
- **Consequence** Pathway change
- **Next step** Acute branch


#### VR reasoning card C.7 — Possible sepsis

- **Why** Organ dysfunction from infection
- **Trainee action** Infection? NEWS2 + single params + organ dysfunction + trend; SBAR
- **What the trainee sees** Monitor, patient, lactate
- **Information** NEWS2 tier, lactate
- **Interpretation** NG253 risk tier
- **Decision** High/moderate/low?
- **Correct action** Antibiotics/fluids timed by team
- **Incorrect action** Delay for results
- **Missed abnormality** Shock
- **Escalation** Senior; critical care
- **Consequence** Timed bundle
- **Next step** Source control

**Scenario maps:** S1 stages 3 Observations; S2 stages 3 Observations.

| What-if | Branch | Correct action |
|---|---|---|
| WI-01 | What if blood pressure is too low? (top number 90 or less) | Call for help; lie flat; assess airway/breathing and start oxygen per protocol to the patient's target; two drips; request a fluid bolus (e.g. 500 mL crystalloid, NICE CG174; the clinician decides volume) and re-check EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ; treat cause (antibiotics if septic; adrenaline if anaphylaxis; blood if bleeding). |
| WI-02 | What if blood pressure is extremely high? (180/110 or more) | No symptoms: S1 → postpone and refer for control (don't rush BP down). With symptoms → emergency medical review. |
| WI-03 | What if the heart rate is very high? (above 130) | ABCDE; treat cause (pain relief, fluids, antibiotics); ECG-guided rhythm treatment by senior/medical team. |
| WI-04 | What if the heart rate is very slow? (40 or below) | ABCDE; stop causative drugs; emergency medicines/pacing per resuscitation team. |
| WI-05 | What if the patient has a fever? (38.0 °C or more) | Paracetamol; fluids if dry; antibiotics according to cause; sepsis bundle if criteria met. |
| WI-06 | What if the patient is cold? (36.0 °C or less) | Warm (forced-air blanket); don't send to theatre until 36.0 unless urgent CLINICAL REFERENCE — SOURCE BACKED (NICE CG65). |
| WI-07 | What if oxygen saturation is low? (below 94%) | Look at the patient and assess airway/breathing; check probe and waveform; if the low reading is real, sit up and start oxygen per protocol to the patient's target range (94–98%, or 88–92% if at risk of hypercapnic failure); find the cause (stop fluids if overloaded per senior; stimulate if over-sedated — antidote by clinician); reassess within minutes (process in Part 4.5). |
| WI-09 | What if breathing is very slow? (8 or less) | Wake/stimulate; oxygen; support airway; naloxone antidote PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; stop further opioids. |
| WI-10 | What if the patient becomes confused? | Treat what's found (sugar below 4 → glucose); sepsis bundle if infection; reassess capacity (consent may be invalid now). |
| WI-11 | What if pain gets much worse or the tummy becomes rigid? | Pain relief; nil by mouth; fluids; antibiotics; prepare for urgent surgery. |
| WI-39 | What if the patient deteriorates? | Treat as found; increase monitoring. |
| WI-40 | What if the patient becomes septic? | Sepsis bundle within 1 hour (oxygen, cultures, antibiotics, fluids, lactate, urine output). |


### Implementation (FYP design)

- **Real workflow** Nurse does routine obs every few hours; doctor examines once in detail.
- **FYP simulation** Trainee attaches probe/cuff/thermometer (or asks nurse NPC), counts RR by watching the avatar, feels pulse for regularity, completes NEWS2 and selects the response tier.
- **FYP simplification** RR counted from a visible breathing animation (or 'count' interaction); temperature is a single hotspot; NEWS2 auto-sum shown only after all parameters entered.
- **AI** None needed. Physiology is deterministic (LitePhysio). NEWS2 is a deterministic calculation — NOT AI.

- **Room** Same room; curtain closes for exam.
- **Patient** Sitting/lying; S2 curled in pain
- **Staff** Nurse NPC (can do obs on request; chaperone).
- **Equipment** Monitor (SpO2+pleth, NIBP/MAP, ECG leads), tympanic thermometer, glucose meter (diabetics), NEWS2 chart
- **Available interactions** Grab probe/cuff; 'Examine' hotspots; ask nurse for obs.

**USER MUST:**

1. Grab SpO2 probe → attach to patient finger socket.
2. Grab BP cuff → attach to upper-arm socket; press 'Measure' on monitor.
3. Tap thermometer hotspot; read RR by watching chest (or 'Count RR' button); select consciousness level.
4. Review computed NEWS2 on tablet.
5. Feel pulse for regularity; repeat any odd reading with correct technique.
6. Calculate NEWS2 and choose the response tier; S1: explain ANY NEWS2 above 1.

**SYSTEM THEN:**

1. Sensor sockets set sensor.attached; monitor shows values from physiology engine only when attached.
2. NEWS2 = sum of band points; stored with timestamp.
3. If NEWS2 ≥ escalation threshold (S2) → goal G-ESCALATE activated with deadline. [CLINICAL VALIDATION REQUIRED]
4. Nurse sets observation frequency from NEWS2 tier (WT 4.9).
5. Accepting a high BP without repeat → anaesthetist flags later.
6. Rising trend ignored → deterioration branch; NEWS2 5+ → senior required; 7+ → emergency call.
7. Display reference ranges (e.g. HR 60–100) but score with NEWS2 bands (WT 4.2).

- **If correct →** Obs set complete; NEWS2 shown; finding codes recorded; next goals unlock.
- **If incorrect →** Examining painful area first → pain spike + trust −1 (communication item). No obs before theatre → later monitoring failure.
- **If information missing →** Missing parameter → NEWS2 greyed 'incomplete'.
- **If safety condition fails →** S2: high NEWS2 ignored beyond deadline → deterioration event E-DETERIORATE-DEHYD eligible (Step 8). [CLINICAL VALIDATION REQUIRED]

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L3-01 | attach(SpO2) | patient present | socket valid | sensor on; SpO2 visible | — | Monitor value appears | Probe snaps to finger | — |
| L3-02 | measure(NIBP) | cuff attached | — | BP value after 20 s sim | — | Cuff inflate sound | Cuff inflate/deflate | — |
| L3-03 | compute NEWS2 | all 7 params known | band table | score + band | incomplete if missing | Score chip colour | — | — |
| L3-04 | NEWS2 high (S2) | score ≥ threshold [CLINICAL VALIDATION REQUIRED] | — | goal G-ESCALATE deadline | if deadline missed → critical | Tablet alert | Monitor amber | ST3/URG |
| L3-WT1 | news2_tier | all parameters | NEWS2 + single-3 flag | Correct escalation | Missed escalation | Nurse prompt | — | news2.tier |
| L3-WT2 | repeat_reading | abnormal value | technique items | Real vs artefact | Acting on one reading | — | — | obs[] |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E3-B | SpO2 artefact (probe loose) | SpO2 drops to implausible value, pleth trace flat | Trace quality poor | Check patient & probe first | If trainee gives oxygen without checking → minor | Allowed | Debrief note | ST3 | Re-seat probe |
| E3-C | High NEWS2 not acted on (S2) | Amber/red chip | Score indicates urgent review | Escalate + treat | Deadline timer | Allowed | Alert tone | URG | Step 8/escalation |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV3-1 E-SPO2-ARTEFACT | Probe movement (random, difficulty ≥2) | Patient moves hand | SpO2 value drops, pleth waveform flattens | Monitor tone | Mismatch between calm patient and number | Check probe/patient before treating | Unnecessary oxygen logged (minor) |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Vital-sign monitor | ✓/✓ | 3, 8, 12–14 | SpO2, pulse, NIBP/MAP, ECG, alarms | Artefact if probe poor; alarm limits C |
| BP cuff (NIBP) | ✓/✓ | 3 | Sys/dia/MAP after \~30–40 s | Wrong size → false reading |
| Pulse oximeter probe | ✓/✓ | 3, 13 | SpO2, pulse, pleth quality | No waveform → recheck; Sign In requires working |
| Tympanic thermometer | ✓/✓ | 3, 10 | °C | <36.0 → warm |
| Glucose/ketone meter | cond./cond. | 3, 8 | mmol/L | Hypo missed |
| PPE | ✓/✓ | All | Cue | — |
| NEWS2 chart | ✓/✓ | 3 | Obs trend + score | Missed trend |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — LitePhysio baseline per patient + sensor gating; NEWS2 calculator (pure function, Scale 1 + Scale 2) with tests against WT 4.9 bands; trend store for deterioration detection (B-R18). Sannan — Probe/cuff/thermometer interactables + sockets; monitor world-space UI incl. pleth quality and MAP in brackets; avatar breathing at simulated RR (must be countable); NEWS2 chart UI. Moiz — Vital-sign band tables (content/vitals_bands.csv) and NEWS2 response tiers from WT Part 17/4.9 as Type A content; per-variant baseline obs from WT Parts 12–13/D.

**Validation:** Use of NEWS2 (vs local early-warning tool) and its escalation mapping. [CLINICAL VALIDATION REQUIRED] · Examination finding codes per variant. [CLINICAL VALIDATION REQUIRED] · Vital-sign ranges for each patient variant. [CLINICAL VALIDATION REQUIRED] · Hospital early-warning tool; anaesthetist alarm thresholds.


## 09. Step 4 — Examining the patient

Priority P0 · walkthrough p.80 · build guides: B-S9, B-S2, B-M3


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Looking, listening and gently feeling to find physical signs.
- **Why are we doing it?** Signs confirm or challenge the diagnosis and reveal dangers (e.g. a rigid tummy means possible perforation;
yellow eyes mean bile blockage).
- **What does the trainee actually do?** 1. Explain and ask permission; offer a chaperone (another staff member present) CONTEXT-DEPENDENT —
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
2. General look: distress, pale, yellow (look at whites of eyes), sweaty, dry mouth.
3. Listen to heart and lungs with a stethoscope.
4. Tummy: start pressing away from the pain and move toward it, gently.
5. S1: press under right ribs while the patient breathes in (Murphy's sign).
6. S2: lower right tenderness, guarding, rebound; Rovsing's sign (pressing lower LEFT hurts lower RIGHT);
psoas/obturator signs (leg movements that hurt with a hidden appendix).
7. S2: check hernia areas; in males check testicles (with consent/chaperone).
8. Look in the mouth and at the neck for the anaesthetist (Part 7.5).
9. Thank the patient, cover them, record findings.
- **What does the trainee see?** The patient on the couch; the trainee's hands/stethoscope; the patient's face and voice react.
- **What information do they receive?** S1-A: "Soft tummy, mildly tender under right ribs, Murphy's negative." S1-E: patient catches breath and says
"Ow!" when right ribs pressed = Murphy's positive. S2-A: "Tender lower right, guarding, rebound positive,
Rovsing's positive."
- **What does that information mean?** Murphy's positive = inflamed gallbladder likely. Lower right tenderness + guarding + rebound = inflamed
lining near the appendix (localised peritonitis). Whole tummy rigid = generalised peritonitis → possible burst.
- **What is the normal/reference value?** S1: soft, non-tender or mildly tender tummy; no jaundice; normal heart/lung sounds; airway easy. S2: there is
no 'normal' — expected localised lower-right signs.
- **What counts as abnormal?** S1: Murphy's positive, tender lump, jaundice, irregular pulse, wheeze. S2: signs outside the lower right,
testicle/pelvic findings.
- **What counts as seriously concerning?** Rigid board-like tummy; very tender everywhere; a mass; signs of shock; very difficult airway.
- **What could cause the abnormal result?** Inflammation spreading, abscess, perforation, alternative diagnosis.
- **What should the trainee check next?** 1. S1 positive signs → bloods (WBC, CRP, liver tests), urgent ultrasound, senior.
2. S2 generalised signs → senior immediately, sepsis check.
3. Airway concern → anaesthetist.
- **What happens if the trainee finds nothing wrong?** Go to STEP 5 (risk and anaesthetic assessment) and STEP 6 (tests).
- **What happens if the trainee finds something abnormal?** Record and follow the branch.
- **What happens if the abnormality is mild?** Mild right-upper tenderness in S1 is common with gallstones — continue.
- **What happens if it is severe?** S1: inflamed gallbladder → planned op cancelled, acute pathway. S2: rigid tummy → emergency; senior; speed
up surgery.
- **When do we call the senior doctor?** Any S1 sign of acute inflammation or jaundice; S2 generalised peritonitis or mass; anything not fitting the
expected picture.
- **When do we call the anaesthetist?** Difficult airway signs; sleep apnoea; heart murmur or irregular pulse.
- **When might surgery be delayed?** S1: signs of acute cholecystitis or jaundice.
- **When should emergency surgery continue despite the abnormality?** S2: peritonitis makes surgery more urgent.
- **What happens if the trainee misses it?** Not checking Murphy's sign in a feverish S1 patient → inflamed gallbladder discovered in theatre (debrief).
Examining without permission → professionalism debrief.
- **What happens after the correct action?** Diagnosis becomes more or less likely; test choice is guided.
- **Medical words used here** Murphy's sign Pain stops the patient breathing in when the gallbladder area is pressed.
Guarding Tummy muscles tense to protect a painful area.
Rebound tenderness Pain when pressing hand is suddenly released.
Peritonitis Inflammation of the tummy lining.
Chaperone A second staff member present during intimate examinations.
- **Where this comes from** TG18; WSES 2020; StatPearls.
- **Clinician validation** Surgeon: examination finding sets for each variant; chaperone policy.


### What the player sees (WT Part 16 — Stage 4)

- **On screen / in front of the player** Patient on couch; trainee's hands/stethoscope.
- **Patient — what they say** Winces/catches breath when tender areas pressed (Murphy's, rebound).
- **Monitor — numbers** Unchanged.
- **Interaction** Ask permission/offer chaperone; look at eyes; listen; press tummy regions; airway look.
- **Information revealed** Findings text (e.g. 'tender RIF, guarding'); eye colour visible.
- **Decision** Do signs fit the diagnosis?
- **Correct action →** Findings added to notes.
- **Incorrect action →** Examining without permission → patient discomfort; debrief.
- **Missed abnormality →** Missed Murphy's → acute cholecystitis found later.
- **Escalation →** Rigid tummy → senior immediately.


#### VR reasoning card C.6 — Fever (S1)

- **Why** Infection in planned patient
- **Trainee action** Examine (Murphy's, jaundice), bloods, US
- **What the trainee sees** Thermometer, patient
- **Information** T, WBC, CRP, LFTs
- **Interpretation** Acute cholecystitis/cholangitis
- **Decision** Is this still elective?
- **Correct action** Acute pathway
- **Incorrect action** Paracetamol and proceed
- **Missed abnormality** Stopped at Sign In / sepsis
- **Escalation** Senior
- **Consequence** Pathway change
- **Next step** Acute branch

**Scenario maps:** S1 stages 3 Observations, 4 History; S2 stages 2 Initial assessment, 4 History.


### Implementation (FYP design)

- **Real workflow** Doctor examines in detail once (look, listen, feel); airway look for the anaesthetist.
- **FYP simulation** Hotspot-based examination with patient reactions; findings revealed from the case card.
- **FYP simplification** Palpation = hotspot touch, not haptic skill; heart/lung sounds optional audio (P1).
- **AI** None required; optional AI phrasing of patient reactions (validated).

- **Room** Same room; curtain closes for exam.
- **Patient** Lying flat; gown lifted (dignity mesh)
- **Staff** Nurse NPC (can do obs on request; chaperone).
- **Equipment** Stethoscope, exam hotspots, curtain
- **Available interactions** Ask permission; examine regions; airway look

**USER MUST:**

1. Explain and ask permission; offer a chaperone (WT Step 4; chaperone policy Type C).
2. Draw curtain.
3. General look: distress, pallor, jaundice (look at sclera), sweating, dry mouth.
4. Listen to heart and lungs (stethoscope).
5. Palpate abdomen starting AWAY from the pain (9 regions).
6. S1: Murphy's sign (press under right ribs while patient breathes in).
7. S2: RLQ tenderness, guarding, rebound, Rovsing's, psoas/obturator; hernia areas; males: testicles (consent/chaperone).
8. Airway look for the anaesthetist: mouth opening, Mallampati, neck movement (WT 7.5).
9. Thank, cover, record findings on tablet → 'Examination complete'.

**SYSTEM THEN:**

1. Hotspot touch reveals finding codes from the case card with patient reaction (wince, catch breath).
2. Examining without permission → professionalism evidence + patient discomfort.
3. Painful region first → pain spike + trust −1.
4. Airway findings stored for Step 5 and Sign In.
5. Rigid board-like abdomen (variant) → senior-immediately goal (S2 generalised peritonitis).

- **If correct →** Findings recorded; diagnosis more/less likely; test choice guided.
- **If incorrect →** Examining without permission → professionalism debrief; missed Murphy's → acute cholecystitis found later.
- **If information missing →** Missing region → finding stays hidden; debrief lists it.
- **If safety condition fails →** S2 rigid abdomen not escalated → deterioration/sepsis branch.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L4-01 | examine(region) | permission asked (+chaperone where required) | consent flag | Finding revealed | Professionalism item | Patient line | A4-01 | findings[] |
| L4-02 | murphy_test (S1) | S1 case | finding code | Murphy's ± recorded | Missed in febrile S1 → inflamed GB found later (debrief) | — | A4-02 | findings[] |
| L4-03 | examine(RLQ signs) (S2) | S2 case | finding codes | Localised vs generalised peritonism | Generalised ignored → sepsis branch | — | A4-03 | findings[] |
| L4-04 | airway_check | mouth/neck items | airway enum | Stored for anaesthetist | Difficult airway not passed on → Sign In challenge | — | — | airway{} |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E4-A | Examined without asking permission | Patient flinches, 'Oh!' | Consent before touch | Apologise, ask | Professionalism evidence | Continue | Debrief | trust −1 | Ask first next time |
| E4-B | Pressed painful area first | Pain spike, guarding | Start away from pain | Reassure; continue gently | pain +1 | Continue | Debrief | pain | — |
| E4-C | Murphy's not checked in febrile S1 patient | — | Acute cholecystitis possible | Murphy's + bloods + US + senior | Finding stays hidden | Later stop | Debrief | — | WT Step 4 'misses' |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Stethoscope | ✓/✓ | 4 | Cue | — |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Finding-reveal and behaviour hooks (trust/pain deltas) in Core; airway data to anaesthetic plan. Sannan — Abdominal hotspots (9 regions) + reaction clips; Murphy's breath-catch blend; curtain; stethoscope interaction; airway look UI. Moiz — Finding codes per WT variant (Parts 12–13, D); professionalism rubric items; airway enum content (WT 7.5).

**Validation:** Examination finding sets per variant [CLINICAL VALIDATION REQUIRED] · Chaperone policy (Type C) [CLINICAL VALIDATION REQUIRED] · Surgeon: examination finding sets for each variant; chaperone policy.


## 10. Step 5 — Risk and anaesthetic assessment

Priority P0 · walkthrough p.82 · build guides: B-R12, B-M20, B-M12


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Putting together everything so far to judge how risky anaesthesia and surgery are, and whether the patient is
fit now.
- **Why are we doing it?** Some risks can be reduced before surgery (optimisation) or planned for (special equipment, senior
anaesthetist).
- **What does the trainee actually do?** 1. Propose an ASA grade (Part 7.4); the anaesthetist confirms.
2. Complete a clot (VTE) and bleeding risk assessment (Part 7.6).
3. Do the airway and sleep-apnoea checks (Part 7.5).
4. S2: calculate the appendicitis score once bloods return (Part 7.1).
5. S1 if inflamed: apply Tokyo Guidelines grading (Part 7.2).
6. Note risks for consent and handover.
7. Decide: proceed / optimise / more tests / senior or anaesthetic review / postpone.
- **What does the trainee see?** Anaesthetist character arrives (or phone); risk form; ASA selector.
- **What information do they receive?** Anaesthetist: "What ASA grade would you give him and why?" "When did he last eat?" "Any problems with
previous anaesthetics?"
- **What does that information mean?** ASA 1–2 = generally low anaesthetic risk; 3+ = needs planning; 'E' = emergency adds risk.
- **What is the normal/reference value?** S1 typical ASA 1–2, low VTE risk, normal airway, fasted. S2 typical ASA 1E.
- **What counts as abnormal?** ASA 3; STOP-BANG 3–4; moderate VTE risk; fasting breach.
- **What counts as seriously concerning?** ASA 4; STOP-BANG 5+; very difficult airway; uncontrolled illness.
- **What could cause the abnormal result?** Comorbidities, emergency situation, obesity, smoking.
- **What should the trainee check next?** 1. Optimisable problem (S1) → postpone and refer.
2. Planning problem → tell anaesthetist.
- **What happens if the trainee finds nothing wrong?** Go to STEP 6 (choosing tests).
- **What happens if the trainee finds something abnormal?** Discuss with anaesthetist.
- **What happens if the abnormality is mild?** Note and plan.
- **What happens if it is severe?** Anaesthetist leads decision; S1 often postponed; S2 senior anaesthetist involved.
- **When do we call the senior doctor?** Any uncertainty about proceeding.
- **When do we call the anaesthetist?** Always at this step.
- **When might surgery be delayed?** S1: uncontrolled comorbidity, fasting breach (re-timed), recent heart event.
- **When should emergency surgery continue despite the abnormality?** S2: high-risk patients still need source control; risk is managed rather than avoided.
- **What happens if the trainee misses it?** Wrong ASA → anaesthetist corrects with explanation (teaching moment, not failure). Hiding a fasting breach →
aspiration event or cancellation.
- **What happens after the correct action?** Anaesthetic plan agreed (e.g. 'standard GA' or 'RSI').
- **Medical words used here** ASA grade Fitness-for-anaesthesia scale (Part 7.4).
VTE Blood clots in veins.
GA General anaesthesia.
- **Where this comes from** ASA PS 2020; NICE NG89; NICE QS216; STOP-BANG (Chung).
- **Clinician validation** Anaesthetist: expected ASA per variant; risk tool.


### What the player sees (WT Part 16 — Stage 5)

- **On screen / in front of the player** Anaesthetist arrives / phone; ASA selector.
- **Patient — what they say** Anaesthetist asks: "When did he last eat?"
- **Monitor — numbers** —
- **Interaction** Select ASA, VTE form, airway findings.
- **Information revealed** Anaesthetist's response.
- **Decision** Fit now? Plan?
- **Correct action →** Plan agreed.
- **Incorrect action →** Wrong ASA corrected with explanation.
- **Missed abnormality →** Hidden fasting breach → aspiration event/cancellation.
- **Escalation →** Anaesthetist can postpone.


#### VR reasoning card C.12 — Appendicitis score

- **Why** Probability guides imaging
- **Trainee action** Calculate AIR/AAS
- **What the trainee sees** Score UI
- **Information** Band
- **Interpretation** Low/intermediate/high
- **Decision** Imaging?
- **Correct action** Right scan
- **Incorrect action** Skipping score
- **Missed abnormality** Over/under-imaging
- **Escalation** Senior
- **Consequence** Imaging choice
- **Next step** Imaging


#### VR reasoning card C.16 — Fasting

- **Why** Aspiration risk
- **Trainee action** Exact times/type; GLP-1
- **What the trainee sees** Patient
- **Information** Times
- **Interpretation** Breach? Full stomach?
- **Decision** Inform anaesthetist?
- **Correct action** Re-time / RSI
- **Incorrect action** Rounding times
- **Missed abnormality** Aspiration
- **Escalation** Anaesthetist
- **Consequence** Timing change
- **Next step** Prep

**Scenario maps:** S1 stages 10 Anaesthetic assessment; S2 stages 10 Anaesthetic assessment.

| What-if | Branch | Correct action |
|---|---|---|
| WI-08 | What if breathing is very fast? (25 or more) | Treat cause; sepsis bundle if infection. |
| WI-33 | What if the patient hasn't fasted? | Tell anaesthetist honestly. |
| WI-34 | What if fasting status is unknown? | Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In. |


### Implementation (FYP design)

- **Real workflow** Anaesthetist sees the patient before theatre and decides the anaesthetic plan.
- **FYP simulation** Trainee proposes ASA/scores; anaesthetist NPC gives approved response.
- **FYP simplification** Risk tool = one reviewer-chosen tool; anaesthetic plan itself is out of scope.
- **AI** None for decisions. P1: LLM phrases anaesthetist NPC lines from approved intents.

- **Room** Same bedside.
- **Patient** Resting.
- **Staff** Anaesthetist NPC arrives when trainee calls or when case timer reaches point.
- **Equipment** Tablet calculators (ASA, VTE, score).
- **Available interactions** Assess risk forms; call anaesthetist.

**USER MUST:**

1. Open 'Assess' tab.
2. Select ASA class with justification tags.
3. Complete VTE risk items.
4. (S2) enter score components → compute; (S1-V4) TG18 criteria.
5. Call anaesthetist NPC and present summary (SBAR-lite).
6. Choose readiness decision.
7. Propose an ASA grade (anaesthetist confirms); VTE + bleeding risk; airway + STOP-BANG; S2 appendicitis score after bloods; S1 TG18 grade if inflamed; decide proceed/optimise/more tests/review/postpone.

**SYSTEM THEN:**

1. Compares trainee values with expected values/ranges set by reviewer.
2. Records decision; opens branches (delay, optimise, test).
3. Anaesthetist NPC response from approved intent table (e.g. 'Delay until fasting criteria met').
4. Behaviour: patient told about delay → anxiety +1 unless explained.
5. Anaesthetist ALWAYS involved at this step.
6. Wrong ASA → anaesthetist corrects with explanation (teaching moment, NOT failure) (WT Step 5).
7. Hidden fasting breach → aspiration event or cancellation.
8. Anaesthetic plan agreed ('standard GA' / 'RSI').

- **If correct →** Risk goals complete; correct branch taken; score items credited.
- **If incorrect →** Wrong ASA (±1) → minor; wrong/unsafe decision (proceed despite fasting breach) → CRITICAL.
- **If information missing →** Score uncomputable without bloods → prompt to order tests.
- **If safety condition fails →** Proceeding with a known fasting breach without anaesthetist agreement blocks 'ready' and is CRITICAL.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L5-01 | assess_risk(ASA,v) | history+exam done | \|v − expected\| ≤ tolerance | credit | minor item | Chip | — | — |
| L5-02 | assess_vte() | — | all items answered | plan required flag | not done → major at readiness | — | — | — |
| L5-03 | compute_score(S2) | components known | formula | score + band | missing comp → prompt | — | — | DP-S2-IMG |
| L5-04 | decide_ready(proceed) with breach | npo breach | anaesthetist agreed? | — | CRITICAL | Red banner | Anaesthetist shakes head | blocked |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E5-A | Proceed despite fasting breach | Decision allowed on tablet | Breach chip red | Inform anaesthetist; delay | Critical item; readiness blocked | Blocked | 'Fasting breach unresolved' | BLOCK | Re-decide |
| E5-B | VTE assessment skipped | — | VTE form empty | Complete | Major at readiness | Allowed until readiness | Reminder | — | Complete form |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Decision-point engine (DP nodes, options, consequences) + anaesthetist intents. Sannan — Assess tab UI; anaesthetist NPC avatar + walk/talk. Moiz — Content: expected ASA per variant, VTE form items (paraphrased NG89), score definitions (after reviewer choice), TG18 criteria; mark [CLINICAL VALIDATION REQUIRED]

**Validation:** Expected ASA grade per patient variant. [CLINICAL VALIDATION REQUIRED] · Choice of risk tool and appendicitis score. [CLINICAL VALIDATION REQUIRED] · Delay/proceed decision for fasting breaches. [CLINICAL VALIDATION REQUIRED] · Anaesthetist: expected ASA per variant; risk tool.


## 11. Step 6 — Choosing the tests

Priority P0 · walkthrough p.84 · build guides: B-R16, B-M13, B-S7


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Deciding which tests are needed for THIS patient — and deliberately NOT ordering tests that won't change
anything.
- **Why are we doing it?** Useful tests guide decisions. Useless tests waste time, money, can delay surgery and can produce misleading
'abnormal' results (remember: 1 in 20 healthy people is outside the range) CLINICAL REFERENCE — SOURCE BACKED
(NICE NG45).
- **What does the trainee actually do?** 1. Choose tests from the order panel.
2. Write the reason and urgency on the request.
3. Explain tests to the patient (sensitively for pregnancy test).
4. Take blood after identity check; label at the bedside.
5. Do bedside tests (pregnancy test, urine dipstick, finger-prick sugar, ECG).
6. Request scans; arrange safe transfer (porter; nurse or doctor escort if unstable).
7. Track pending results.
- **What does the trainee see?** Order panel with tests; coloured tubes; labels; urine pot; pregnancy test kit; ECG machine; scan request form;
porter; wheelchair/trolley; portable monitor.
- **What information do they receive?** Confirmation of what's ordered; expected times.
- **What does that information mean?** Each test answers a specific question (see table below).
- **What is the normal/reference value?** The 'right set' for each patient (see below).
- **What counts as abnormal?** Missing an indicated test or ordering unhelpful ones.
- **What counts as seriously concerning?** Missing a pregnancy test in someone who could be pregnant; sending an unstable patient to a scan without
monitoring.
- **What should the trainee check next?** 1. While waiting: treat pain, fluids, continue history/consent preparation.
- **What happens if the trainee finds nothing wrong?** Go to STEP 7 (results).
- **When do we call the senior doctor?** Unsure which scan; patient too unstable for transfer.
- **When do we call the anaesthetist?** Patient on blood thinners (clotting tests), heart disease (ECG).
- **What happens if the trainee misses it?** Unnecessary tests are listed in the debrief with their 'cost'. Pre-labelled tubes → wrong-blood event.
Unmonitored unstable patient → drop in oxygen in the corridor event.
- **What happens after the correct action?** Results start to arrive after their simulated times.
- **Medical words used here** Indicated There is a good medical reason for it.
Bedside test Done at the bedside with a quick result.
- **Where this comes from** NICE NG45; NICE CG188; WSES 2020.
- **Clinician validation** Surgeon/anaesthetist: test-indication list; transfer rule; simulated turnaround times.
Which tests for which patient? CLINICAL REFERENCE — SOURCE BACKED (NICE NG45,
CG188, WSES) — local blood-ordering rules CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /
CLINICIAN CONFIRMATION
Test S1 planned gallbladder S2 emergency appendix Why
Liver tests (LFTs) YES (all) If upper pain or unclear diagnosis Look for bile blockage
Pregnancy test If pregnancy is possible or uncertain —
offered with consent
If pregnancy is possible — offered with
consent (urgent in abdominal pain)
Changes diagnosis, imaging,
anaesthesia, drugs and timing
FBC (blood count) If sicker (ASA 3+), anaemia symptoms,
heart/kidney disease, or local rule
YES Infection, bleeding, anaemia
CRP Only if inflammation suspected YES Inflammation; score
U&E (kidneys, salts) If 65+, diabetes, kidney disease, water
tablets/ACE-i, ASA 3+
YES Kidneys, potassium, dehydration
Blood sugar
(finger-prick)
If diabetic If diabetic or vomiting Low/high sugar
HbA1c If diabetic and none in 3 months No Diabetes control
Clotting (INR/APTT) If on blood thinners or liver disease If on blood thinners, septic, liver
disease
Bleeding risk
Group & save Per hospital rule Per rule; always if bleeding suspected Blood ready if needed
Urine dipstick No (not routine) YES Urine infection/stone look-alikes
ECG If heart/kidney/diabetes disease,
irregular pulse, 65+ (some)
If 65+, heart disease, HR above 130,
irregular, salt problems
Heart rhythm
Lactate + blood
cultures
If septic If sepsis suspected Sepsis
Lipase/amylase If severe upper pain / back pain If diagnosis unclear Pancreatitis
Ultrasound Already done before booking; repeat if
acute
Young/female/pregnant first See inside
CT Only for complications Adults when unclear; older patients Detailed scan
MRCP If liver tests abnormal / duct wide No Look for duct stones
Chest X-ray No (not routine) Only if breathing problems / suspected
burst gut
—


### What the player sees (WT Part 16 — Stage 6)

- **On screen / in front of the player** Order panel; tubes; labels; kits; ECG machine; scan request; porter; portable monitor.
- **Patient — what they say** "What are these tests for?"
- **Monitor — numbers** —
- **Interaction** Select tests; take blood; label at bedside; dipstick; pregnancy test; ECG; request scan; choose escort.
- **Information revealed** Pending list with expected times.
- **Decision** Which tests? Who escorts?
- **Correct action →** Tests queued; patient moves safely.
- **Incorrect action →** Unneeded tests listed in debrief; pre-labelled tubes → wrong-blood event.
- **Missed abnormality →** Unstable patient sent with porter only → oxygen drop in corridor.
- **Escalation →** Senior for imaging choice.


#### VR reasoning card C.11 — Pregnancy possible

- **Why** Changes diagnosis and safety
- **Trainee action** Ask privately; consent; test
- **What the trainee sees** Test kit
- **Information** Result
- **Interpretation** Positive + pain: ectopic pregnancy is a differential diagnosis that can redirect the pathway
- **Decision** Pathway change?
- **Correct action** Correct team
- **Incorrect action** Not asking
- **Missed abnormality** Safety failure
- **Escalation** Senior; gynaecology/obstetrics
- **Consequence** Pathway switch
- **Next step** Continue/redirect

**Scenario maps:** S1 stages 5 Investigations; S2 stages 5 Investigations.

| What-if | Branch | Correct action |
|---|---|---|
| WI-25 | What if the ECG is abnormal? | AF: treat triggers, rate control per senior/medical; heart attack: emergency heart pathway. |
| WI-26 | What if the patient is pregnant? | S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise → gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok. |
| WI-27 | What if pregnancy status is unknown or the patient declines a test? | Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior + anaesthetist decide whether to proceed. |


### Implementation (FYP design)

- **Real workflow** Tests take minutes–hours; patient waits.
- **FYP simulation** Timed states with sim-time skip; transport shown as bed movement or fade.
- **FYP simplification** Lab is abstract (no lab room); CT/MRCP abstract rooms; phlebotomy is an intent action (no needle skill).
- **AI** None for selection/scoring. P1: voice orders via ASR with nurse read-back (closed loop); any misheard order must be confirmed on tablet.

- **Room** Bedside + corridor + US room (or fade) + X-ray/CT (abstract).
- **Patient** On bed/trolley during transfer.
- **Staff** Porter NPC, sonographer NPC (abstract/voice), nurse.
- **Equipment** Sample tubes & labels, request form (tablet), urine test kit, ECG machine, portable monitor for transfer.
- **Available interactions** Order, label, send; request transport; accompany or stay.

**USER MUST:**

1. Open 'Orders'; tick indicated tests; set priority.
2. Explain the tests (menu act).
3. Take/label blood tubes: grab tube → touch wristband (ID) → label → place in sample bag.
4. Do bedside tests (pregnancy test/urinalysis/glucose/ECG) where indicated.
5. Request transport for imaging; choose escort/monitoring if patient unstable.
6. Accompany patient or continue other tasks.
7. Choose tests from the order panel using the WT indication table; write reason + urgency.
8. Take blood after identity check and LABEL AT THE BEDSIDE.
9. Bedside tests: pregnancy (with consent), dipstick, CBG, ECG.
10. Request scans; choose escort by stability (NEWS2 ≤4 porter+nurse; 5–6 nurse+monitor; ≥7 doctor+nurse+full monitoring — Type D/C).

**SYSTEM THEN:**

1. Evaluates indication rules → APPROPRIATE / NOT INDICATED / MISSED flags.
2. Creates InvestigationState per test (REQUESTED→PROCESSING).
3. Labelling check (identity touched before label?) else E-SAMPLE-REJECT eligible.
4. Transport state machine: REQUESTED→IN_TRANSIT→ARRIVED→DONE→BACK with delays.
5. Blocks patient-requiring actions while patient is away.
6. Schedules results by turnaround.
7. Unnecessary tests listed in debrief with their 'cost'.
8. Pre-labelled tubes → wrong-blood event.
9. Unstable patient with porter only → SpO2 drop in corridor event.
10. Pending list shows expected times (turnaround Type D: FBC 45 min, imaging 60–120 min; others unresolved).

- **If correct →** Indicated tests ordered, samples accepted, patient moved safely, results pending.
- **If incorrect →** Unnecessary test → minor; missed indicated test → major/critical (e.g. pregnancy); unmonitored transfer of unstable patient → major [CLINICAL VALIDATION REQUIRED].
- **If information missing →** Clinical details missing on request → lab/radiology NPC asks for them (delay).
- **If safety condition fails →** Pregnancy status unknown in eligible patient → imaging with radiation and theatre blocked.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L6-01 | order(test) | — | indication rule | APPROPRIATE credit | NOT INDICATED minor | Card colour (debrief only in assessment) | — | — |
| L6-02 | label(tube) | identity touched | id match | sample valid | reject event | Lab message | Tube label animation | — |
| L6-03 | request_transport(dest) | test needs location | patient stable? | standard transfer | unstable & no monitoring → major | — | Porter arrives, bed moves | IN_TRANSIT |
| L6-04 | action on absent patient | patient elsewhere | location mismatch | — | rejected | 'Patient is in Ultrasound' | — | — |
| L6-05 | pregnancy test | eligible | consent asked | result by profile | not done → block | — | Dipstick colour change | — |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E6-A | Order every test ('shotgun') | All cards selected | Tests not indicated | Order selectively | Minor items per unnecessary test | Allowed | Debrief lists unnecessary tests | — | — |
| E6-B | Tube not labelled at bedside | Lab rejects after delay | Label mandatory | Re-sample | E-SAMPLE-REJECT; delay | Allowed | 'Sample rejected: unlabelled' | — | Re-take |
| E6-C | Unstable patient sent alone | Patient tachycardic | Monitoring needed | Escort + portable monitor | Major item; deterioration risk | Allowed | [CLINICAL VALIDATION REQUIRED] | — | — |
| E6-D | Pregnancy test skipped (eligible) | Imaging/theatre blocked | Pregnancy status unknown | Test with consent | Block | Blocked | 'Pregnancy status required' | BLOCK | Do test |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV6-1 E-PORTER-DELAY | Transport request (probabilistic) | — | — | Phone call 'porter delayed 20 min' | Time pressure | Reprioritise tasks / escalate if urgent | Idle waiting logged (efficiency) |
| EV6-2 E-SAMPLE-REJECT | Unlabelled tube | — | — | Lab phone call | Sample invalid | Re-sample | Delay → later results |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Wheelchair | ✓/— | 1, 6 | Transport cue | Unstable patient must not go by wheelchair |
| 12-lead ECG + electrodes | cond./cond. | 6–7 | Rhythm, intervals, ST/T; machine read-out may be wrong | Wrong lead placement |
| Blood collection set + tourniquet | ✓/✓ | 6 | Cue | Drip-arm artefact |
| Specimen tubes (colour-coded) + labels | ✓/✓ | 6 | Tube purpose; identity label | Pre-labelled → wrong-blood; EDTA → false high K |
| Blood culture bottles | —/cond. | 6, 8 | Cue | Delaying antibiotics for cultures → harm |
| Urine pot + dipstick + pregnancy kit | cond./✓ | 6 | Dipstick colours; hCG ± | Missed positive hCG → safety failure |
| Sharps bin | ✓/✓ | 6, 8 | Cue | Needlestick |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Ultrasound machine / report | cond./✓ | 6–7 | Images + category | 'Not visualised' ≠ negative |
| CT (room or report only) | cond./cond. | 6–7 | Report + key images | Contrast with AKI/allergy |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — TransportSystem + LocationSystem in Core (state machine, delays, gating) with tests T06-3/4. Sannan — Corridor spline + porter/bed animation (or fade if room missing); tubes/dipsticks/ECG props; orders UI. Moiz — InvestigationSystem + indication rules for S1/S2 (NG45/CG188/WSES own words); request/turnaround content; mark [CLINICAL VALIDATION REQUIRED]

**Validation:** Test indication mapping per patient; NG45 surgery grade for lap chole (not listed as an example). [CLINICAL VALIDATION REQUIRED] · Transfer escort/monitoring rule. [CLINICAL VALIDATION REQUIRED] · Turnaround times (simulation values). [CLINICAL VALIDATION REQUIRED] · Surgeon/anaesthetist: test-indication list; transfer rule; simulated turnaround times.
Which tests for which patient? CLINICAL REFERENCE — SOURCE BACKED (NICE NG45,
CG188, WSES) — local blood-ordering rules CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /
CLINICIAN CONFIRMATION
Test S1 planned gallbladder S2 emergency appendix Why
Liver tests (LFTs) YES (all) If upper pain or unclear diagnosis Look for bile blockage
Pregnancy test If pregnancy is possible or uncertain —
offered with consent
If pregnancy is possible — offered with
consent (urgent in abdominal pain)
Changes diagnosis, imaging,
anaesthesia, drugs and timing
FBC (blood count) If sicker (ASA 3+), anaemia symptoms,
heart/kidney disease, or local rule
YES Infection, bleeding, anaemia
CRP Only if inflammation suspected YES Inflammation; score
U&E (kidneys, salts) If 65+, diabetes, kidney disease, water
tablets/ACE-i, ASA 3+
YES Kidneys, potassium, dehydration
Blood sugar
(finger-prick)
If diabetic If diabetic or vomiting Low/high sugar
HbA1c If diabetic and none in 3 months No Diabetes control
Clotting (INR/APTT) If on blood thinners or liver disease If on blood thinners, septic, liver
disease
Bleeding risk
Group & save Per hospital rule Per rule; always if bleeding suspected Blood ready if needed
Urine dipstick No (not routine) YES Urine infection/stone look-alikes
ECG If heart/kidney/diabetes disease,
irregular pulse, 65+ (some)
If 65+, heart disease, HR above 130,
irregular, salt problems
Heart rhythm
Lactate + blood
cultures
If septic If sepsis suspected Sepsis
Lipase/amylase If severe upper pain / back pain If diagnosis unclear Pancreatitis
Ultrasound Already done before booking; repeat if
acute
Young/female/pregnant first See inside
CT Only for complications Adults when unclear; older patients Detailed scan
MRCP If liver tests abnormal / duct wide No Look for duct stones
Chest X-ray No (not routine) Only if breathing problems / suspected
burst gut
—


## 12. Step 7 — Getting and understanding the results

Priority P0 · walkthrough p.86 · build guides: B-M13, B-S10, B-R17, B-M20


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Opening every result, comparing to normal and previous values, working out what it means for this patient,
acting, and signing it off.
- **Why are we doing it?** A result nobody looks at is useless; an ignored dangerous result can kill.
- **What does the trainee actually do?** 1. Open each result as it arrives.
2. Compare with reference range and previous results.
3. Classify: normal / abnormal but not important / abnormal and important.
4. Check it fits the patient; repeat if suspicious.
5. Decide the action (Part 5 for each test).
6. Acknowledge (sign) the result and document the action.
7. Answer lab phone calls about critical results: repeat the value back, act, document.
- **What does the trainee see?** Results panel with H (high) / L (low) flags and comments; scan viewer and report; ringing phone.
- **What information do they receive?** e.g. 'Potassium 6.4 H — sample haemolysed'; 'Bilirubin 72 H'; 'Ultrasound: appendix not visualised'.
- **What does that information mean?** See Part 5 and Part 6 for each test's meaning.
- **What is the normal/reference value?** All results within reference ranges, or abnormal results that are known, stable and irrelevant.
- **What counts as abnormal?** Any result in amber/red zones of Part 5.
- **What counts as seriously concerning?** Any critical value (Part 5.27) or a result showing the diagnosis is different/complicated.
- **What could cause the abnormal result?** See each test in Part 5.
- **What should the trainee check next?** 1. Look for linked tests (e.g. high potassium → ECG; high bilirubin → duct size).
2. Look at the patient again.
3. Decide whether to repeat.
- **What happens if the trainee finds nothing wrong?** Go to STEP 8 (decide and treat/optimise).
- **What happens if the trainee finds something abnormal?** Use the decision diagrams in Parts 5–7.
- **What happens if the abnormality is mild?** Note, maybe repeat, usually continue.
- **What happens if it is severe?** Act immediately; call help; S1 usually deferred after review; S2 treat in parallel.
- **When do we call the senior doctor?** Any critical or pathway-changing result (abnormal liver pattern, positive pregnancy test, positive scan, sepsis
markers).
- **When do we call the anaesthetist?** Potassium/sodium abnormal, anaemia, clotting abnormal, ECG abnormal, kidney injury, HbA1c above 69.
- **When might surgery be delayed?** S1: any significant abnormality (see Part 5 for thresholds).
- **When should emergency surgery continue despite the abnormality?** S2: kidney injury, salt problems, raised infection markers usually get corrected alongside — surgery continues
as soon as reasonably safe.
- **What happens if the trainee misses it?** Unacknowledged critical result → blocks progress; if it was real, the patient deteriorates. Treating a faulty
sample without repeating → 'over-treatment' debrief.
- **What happens after the correct action?** Results marked reviewed; plan updated.
- **Where this comes from** Royal College of Pathologists 2017 and test-specific sources in Part 5.
- **Clinician validation** Hospital lab ranges and critical list.


### What the player sees (WT Part 16 — Stage 7)

- **On screen / in front of the player** Results panel with H/L flags; scan viewer; phone.
- **Patient — what they say** —
- **Monitor — numbers** Can update after treatment.
- **Interaction** Open, compare, acknowledge; answer phone.
- **Information revealed** Lab comments, previous values, radiology report.
- **Decision** What does it mean; act?
- **Correct action →** Plan updates.
- **Incorrect action →** Over-treating a faulty sample → debrief.
- **Missed abnormality →** Unacknowledged critical result → blocker; real deterioration if true.
- **Escalation →** Critical results → senior/anaesthetist.


#### VR reasoning card C.8 — Potassium abnormal

- **Why** Arrhythmia risk
- **Trainee action** Lab comment; ECG; kidneys; drugs; repeat
- **What the trainee sees** Results, ECG
- **Information** K value, ECG
- **Interpretation** Artefact vs real; severity
- **Decision** Emergency or repeat?
- **Correct action** Correct response
- **Incorrect action** Treat artefact / ignore real
- **Missed abnormality** Arrhythmia
- **Escalation** Anaesthetist; emergency team
- **Consequence** Treatment or proceed
- **Next step** Continue


#### VR reasoning card C.10 — Liver tests abnormal (S1)

- **Why** Possible duct stone
- **Trainee action** Pattern + duct width + ASGE risk
- **What the trainee sees** Results, US report
- **Information** LFTs, CBD
- **Interpretation** Risk tier
- **Decision** MRCP/ERCP?
- **Correct action** Planned op deferred appropriately
- **Incorrect action** Proceed anyway
- **Missed abnormality** Retained stone → cholangitis
- **Escalation** Senior
- **Consequence** Biliary pathway
- **Next step** Rebook


#### VR reasoning card C.12 — Appendicitis score

- **Why** Probability guides imaging
- **Trainee action** Calculate AIR/AAS
- **What the trainee sees** Score UI
- **Information** Band
- **Interpretation** Low/intermediate/high
- **Decision** Imaging?
- **Correct action** Right scan
- **Incorrect action** Skipping score
- **Missed abnormality** Over/under-imaging
- **Escalation** Senior
- **Consequence** Imaging choice
- **Next step** Imaging


#### VR reasoning card C.13 — Imaging result

- **Why** Confirms/redirects
- **Trainee action** Read report; act
- **What the trainee sees** Viewer, report
- **Information** Category
- **Interpretation** Positive/complicated/equivocal/negative
- **Decision** Next step?
- **Correct action** Correct pathway
- **Incorrect action** 'Not seen' = negative
- **Missed abnormality** Perforation
- **Escalation** Senior/radiology
- **Consequence** Decision
- **Next step** Decision

**Scenario maps:** S1 stages 6 Results, 7 Abnormal branches; S2 stages 6 Results, 7 Abnormal branches.

| What-if | Branch | Correct action |
|---|---|---|
| WI-12 | What if potassium is high? (above 5.3) | 5.5–5.9 (mild, UKKA): ECG, repeat, review drugs. 6.0–6.4: urgent repeat + ECG, treat if real. 6.5+ or ECG changes: emergency treatment (calcium to protect the heart; insulin-glucose) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; heart monitor. |
| WI-13 | What if potassium is low? (below 3.5) | Replace (by mouth if mild; through drip if 2.5–2.9 or vomiting, rate PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ); replace magnesium too. |
| WI-14 | What if sodium is abnormal? | Correct the cause slowly (no faster than \~8–10 mmol/L per day). |
| WI-15 | What if haemoglobin is low? | S1: investigate before planned surgery; S2: group & save/crossmatch, look for bleeding, transfusion decision if below 70 (80 heart disease). |
| WI-16 | What if the white cell count is high? | Interpret in context; S1 high WBC = not routine → acute pathway; S2 supports diagnosis; above 18 = moderate gallbladder grade. |
| WI-17 | What if the white cell count is low? | Treat as serious if infection present; urgent antibiotics if septic. |
| WI-18 | What if creatinine is high? | Fluids if dry; stop kidney-harming drugs; catheter/urine measurement; repeat in 6–24 h; adjust medicine doses; avoid CT dye unless essential (senior decides). |
| WI-19 | What if liver tests are abnormal? | Use the bile-duct-stone risk tool (Part 5.16): high → ERCP pathway; intermediate → MRCP; low → proceed. |
| WI-20 | What if bilirubin is high? | Cholangitis signs → emergency; otherwise high-risk pathway (ERCP). |
| WI-21 | What if blood sugar is low? (below 4.0) | Treat immediately per hypoglycaemia protocol (fast sugar if can swallow AND not about to have anaesthetic per anaesthetist; else IV glucose) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; recheck every 10–15 min. |
| WI-22 | What if blood sugar is high? (above 12) | Insulin per protocol (often variable-rate drip) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; fluids if dry. |
| WI-23 | What if the clotting test (INR) is high? | S1 usually deferred after review and correct (vitamin K per senior); S2 correct before surgery PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED . |
| WI-24 | What if lactate is raised? | Sepsis bundle/fluids; repeat within 2–4 h. |
| WI-25 | What if the ECG is abnormal? | AF: treat triggers, rate control per senior/medical; heart attack: emergency heart pathway. |
| WI-35 | What if imaging is inconclusive? | Next imaging (CT/MRI/MRCP) or observe and repeat assessment in 6–12 h. |
| WI-36 | What if a test result doesn't make sense? | Repeat urgently (different tube/arm); treat meanwhile only if dangerous signs. |
| WI-37 | What if a laboratory result is delayed? | Continue other tasks; repeat sample if lost; S2: don't delay antibiotics/fluids/surgery decision for a non-critical result. |
| WI-38 | What if a critical result is phoned through? | Act immediately (ECG, emergency treatment if real); document; acknowledge. |


### Implementation (FYP design)

- **Real workflow** Results arrive over hours.
- **FYP simulation** Results arrive by timer; lab phones critical values.
- **FYP simplification** Imaging shown as report text (+ image only where licensed dataset allows).
- **AI** PROPOSED FYP AI (offline only): LLM drafts imaging report text from finding codes; radiology/surgeon edits before approval. No runtime AI interpretation.

- **Room** Bedside / nurses' station (optional).
- **Patient** Waiting (behaviour: anxiety rises with unexplained waits).
- **Staff** Lab (phone voice), senior surgeon NPC for escalation.
- **Equipment** Tablet results viewer; phone.
- **Available interactions** Open, interpret, acknowledge, escalate.

**USER MUST:**

1. Open new result (notification).
2. Select interpretation (normal / which abnormality).
3. Choose action from decision card.
4. Escalate by SBAR if required.
5. Acknowledge.
6. Open each result; compare with reference AND previous values; classify normal / abnormal-not-important / abnormal-important.
7. Read lab comments (e.g. 'haemolysed'); repeat suspicious results (different tube/arm).
8. Acknowledge (sign) every result; answer critical-result phone calls: confirm identity, READ BACK, act, document.

**SYSTEM THEN:**

1. Formats values with LOCAL ranges; flags.
2. Checks interpretation against finding codes.
3. Opens decision points; applies consequences (e.g. MRCP → surgery delayed).
4. Blocks 'ready' while critical findings unacted.
5. Unacknowledged critical result → hard safety blocker; if real, patient deteriorates.
6. Treating an artefact without repeating → 'over-treatment' debrief.
7. Sample artefacts modelled: haemolysis (false high K), drip-arm dilution, EDTA contamination, platelet clumping, mislabelled/rejected sample.
8. 'Appendix not visualised' is equivocal, NOT negative (classic error built in).

- **If correct →** Results acknowledged; correct branch; credits.
- **If incorrect →** Wrong interpretation → major; ignored critical → CRITICAL.
- **If information missing →** Pending results listed; readiness requires all REQUIRED results.
- **If safety condition fails →** Critical finding unacknowledged → readiness blocked.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L7-01 | read_result | available | — | viewed flag | — | — | — | — |
| L7-02 | interpret | viewed | matches finding codes | credit | major | — | — | DP? |
| L7-03 | acknowledge critical | critical finding | action chosen | unblock | critical if none | Red card | Phone ring | — |
| L7-04 | escalate(SBAR) | DP requires | elements present | senior response | incomplete → minor | — | Senior NPC | — |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E7-A | Result never opened | Notification badge | Unviewed result | Open & act | Major; readiness block if required | Blocked | 'Unreviewed results' | BLOCK | Open |
| E7-B | Abnormal LFT interpreted as normal (S1-V5) | Proceeds | Obstructive pattern | MRCP/senior | Major/critical [CLINICAL VALIDATION REQUIRED] | Allowed then blocked at Sign In? | Debrief | — | — |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV7-1 E-LATE-ABNORMAL-K | Result timer | — | — | Lab phone | Abnormal K | Acknowledge + escalate per local protocol | Critical if theatre proceeds |
| EV7-2 E-RESULT-DELAYED | Scheduled (S2 CRP) | — | — | Lab message 'analyser delay' | Missing info | Proceed if safe or chase | — |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Allergy band | ✓/✓ | 1, 7, 13 | Allergy flag (details by asking) | Missing band → reaction branch |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| 12-lead ECG + electrodes | cond./cond. | 6–7 | Rhythm, intervals, ST/T; machine read-out may be wrong | Wrong lead placement |
| Blood gas analyser display | —/cond. | 7 | Lactate, pH | — |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Results display / lab report | ✓/✓ | 7 | Values, H/L, comments, critical call | Unacknowledged critical → blocker |
| Ultrasound machine / report | cond./✓ | 6–7 | Images + category | 'Not visualised' ≠ negative |
| CT (room or report only) | cond./cond. | 6–7 | Report + key images | Contrast with AKI/allergy |
| MRCP / ERCP report | cond./— | 7 | Duct size, stones | Duct stone missed |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Readiness blocker service + escalation intents. Sannan — Results viewer UI (table/image/strip), phone interaction. Moiz — ResultGenerator (finding codes→values with local ranges), report texts, curated images with licence records; mark [CLINICAL VALIDATION REQUIRED]

**Validation:** All finding → action mappings. [CLINICAL VALIDATION REQUIRED] · Result values for each variant (generated within reviewer-approved ranges). [CLINICAL VALIDATION REQUIRED] · Hospital lab ranges and critical list.


## 13. Step 8 — Deciding, treating and optimising

Priority P0 · walkthrough p.88 · build guides: B-M14, B-R17, B-R13, B-R18, B-R14


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Using all the information to choose one of the five outcomes (safe / more investigation / treat-optimise /
senior-anaesthetic review / emergency), and giving any treatment needed before theatre.
- **Why are we doing it?** This is the core decision skill of ProcSense. A correct decision protects the patient; the 'right' answer is different
in S1 and S2.
- **What does the trainee actually do?** 1. Ask: "Is it safe to continue?" using the decision diagram below.
2. Pain: assess score; after the allergy check, request analgesia (e.g. paracetamol ± stronger painkiller) — a
qualified clinician confirms drug and dose; the nurse administers.
3. Nausea: request an anti-sickness medicine through the same workflow.
4. Drip (cannula): S2 always; S1 usually in the anaesthetic room CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /
CLINICIAN CONFIRMATION .
5. Fluids if dehydrated or low BP: request and agree the fluid plan with the senior/anaesthetist; re-check vital
signs after.
6. Medicine plan: hold/continue diabetes, BP, blood-thinner, steroid medicines (Part 8).
7. Clot prevention: stockings if indicated; injection plan.
8. Antibiotics: S2 — flag the need once appendicitis is decided (sooner if septic, per NICE NG253 risk tier) so
the agent per institutional policy is prescribed by a qualified clinician; S1 only if risk factors.
9. Re-assess: vital signs, pain, behaviour after each treatment.
Decision diagram — 'Is it safe to continue?' (the master decision)
All information gathered so far
Any EMERGENCY sign? (NEWS2 7+, top BP 90 or less,
oxygen 91% or less, new confusion, anaphylaxis,
chest pain, positive pregnancy test with shock)
EMERGENCY: call for help, ABCDE, treat (Part
14)
YES
NO
Does the diagnosis look different or complicated? (S1
inflamed GB/jaundice; S2 an appendicitis mimic such
as ectopic pregnancy, a burst appendix, other cause)
SENIOR review → change pathway / more
investigation
YES
NO
Is there a fixable problem? (BP 180/110+, HbA1c
above 69, salts abnormal, anaemia, blood thinner not
stopped, fasting breach, new AF)
Verify → assess severity and context →
senior/anaesthetic review. S1: usually optimise
first (defer/re-time); S2: correct in parallel
YES
NO
Anything affecting anaesthesia? (airway, sleep
apnoea, aspiration risk, heart/lung disease) ANAESTHETIST review / plan
YES
NO
SAFE TO CONTINUE → consent and preparation
- **What does the trainee see?** Drug chart, drip stand, fluid bags, medicine trolley, stockings; nurse asking for instructions.
- **What information do they receive?** Nurse: "Before I give this, can you confirm her allergy status?" Senior: "Agreed — I've prescribed it." Response
of vital signs to treatment.
- **What does that information mean?** Improvement after treatment (pain falls, heart rate slows) confirms the cause; no improvement suggests
something more serious.
- **What is the normal/reference value?** After treatment: pain 0–3; HR below 100; BP stable; NEWS2 falling.
- **What counts as abnormal?** Pain not controlled; HR still above 100 after pain relief and fluids; BP not responding to fluid.
- **What counts as seriously concerning?** Worsening despite treatment; any reaction after a medicine.
- **What could cause the abnormal result?** Disease progressing (burst appendix, sepsis), bleeding, allergic reaction, wrong diagnosis.
- **What should the trainee check next?** 1. Re-examine.
2. Repeat key bloods (lactate, Hb).
3. Call senior.
- **What happens if the trainee finds nothing wrong?** Go to STEP 9 (consent).
- **What happens if the trainee finds something abnormal?** Follow the diagram and Part 14 'What if'.
- **What happens if the abnormality is mild?** Continue treating; re-check in 30–60 minutes.
- **What happens if it is severe?** Emergency pathway.
- **When do we call the senior doctor?** No improvement after first treatments; sepsis; any change of diagnosis; any S1 deferral decision (senior
confirms).
- **When do we call the anaesthetist?** Fasting breach; aspiration risk; electrolyte, heart, airway issues; sepsis (anaesthetic plan changes).
- **When might surgery be delayed?** S1: any optimisable problem. S2: only briefly to resuscitate (fluids, antibiotics, correct potassium) — not to wait
for perfection.
- **When should emergency surgery continue despite the abnormality?** S2: sepsis, kidney injury, mild salt problems, unknown fasting, high WBC — all treated alongside; surgery is the
cure.
- **What happens if the trainee misses it?** S2: no request for pain relief/fluids/antibiotics → deterioration clock speeds up (heart rate +5–10 every \~30
min SIMULATOR DESIGN VALUE (PROVISIONAL) ). Requesting a drug the patient is allergic to → nurse challenges
(beginner mode) or reaction occurs (advanced).
- **What happens after the correct action?** Patient improves and is 'ready for consent'.
- **Medical words used here** Optimise Improve the patient's condition before surgery.
Bolus A quick volume of fluid given through the drip.
- **Where this comes from** NICE CG174; NICE NG89; NICE NG125; WSES 2020; CPOC 2023.
- **Clinician validation** Surgeon/anaesthetist: all treatment choices; postponement rules.


### What the player sees (WT Part 16 — Stage 8)

- **On screen / in front of the player** Drug chart, drip stand, fluid bags, medicine trolley, stockings.
- **Patient — what they say** Pain reduces after analgesia ("That's a bit better").
- **Monitor — numbers** HR/BP respond to treatment over time.
- **Interaction** Recognises the need (e.g. analgesia, fluids, stockings), requests it through the drug-chart / nurse workflow and proposes the drug class; a qualified clinician confirms drug and dose; the nurse NPC administers; trainee calls senior.
- **Information revealed** Response of vital signs.
- **Decision** Safe / investigate / optimise / review / emergency?
- **Correct action →** Patient improves; case progresses.
- **Incorrect action →** Requesting a drug the patient is allergic to → nurse challenges at the check; if ignored, reaction branch.
- **Missed abnormality →** No treatment in S2 → deterioration clock speeds up.
- **Escalation →** Escalation answered by NPC with advice/arrival.


#### VR reasoning card C.4 — Low SpO2

- **Why** Possible hypoxaemia or artefact
- **Trainee action** Look, assess A/B, check probe, target, oxygen per protocol, cause, reassess
- **What the trainee sees** Patient, waveform, masks
- **Information** SpO2 + waveform quality
- **Interpretation** Real vs artefact; target range
- **Decision** Oxygen needed? Cause?
- **Correct action** SpO2 to target; cause found
- **Incorrect action** Blind high-flow O2 / ignoring
- **Missed abnormality** Hypoxia worsens
- **Escalation** Anaesthetist
- **Consequence** Breathing branch
- **Next step** Continue


#### VR reasoning card C.7 — Possible sepsis

- **Why** Organ dysfunction from infection
- **Trainee action** Infection? NEWS2 + single params + organ dysfunction + trend; SBAR
- **What the trainee sees** Monitor, patient, lactate
- **Information** NEWS2 tier, lactate
- **Interpretation** NG253 risk tier
- **Decision** High/moderate/low?
- **Correct action** Antibiotics/fluids timed by team
- **Incorrect action** Delay for results
- **Missed abnormality** Shock
- **Escalation** Senior; critical care
- **Consequence** Timed bundle
- **Next step** Source control


#### VR reasoning card C.9 — Hypoglycaemia

- **Why** Brain at risk
- **Trainee action** Check glucose; initiate protocol; recheck
- **What the trainee sees** Meter, patient
- **Information** CBG
- **Interpretation** <4 = treat now
- **Decision** Start protocol?
- **Correct action** Glucose recovers
- **Incorrect action** Recheck later only
- **Missed abnormality** Seizure/coma
- **Escalation** Nurse; anaesthetist
- **Consequence** Fasting implications
- **Next step** Medication review


#### VR reasoning card C.14 — Medicine needed

- **Why** Pain, nausea, fluids, antibiotics
- **Trainee action** Recognise need; allergy check; request via workflow
- **What the trainee sees** Drug chart, nurse
- **Information** Confirmed prescription
- **Interpretation** Who prescribes? (clinician)
- **Decision** Initiate?
- **Correct action** Clinician confirms; nurse gives
- **Incorrect action** Requests allergen
- **Missed abnormality** Nurse challenge/reaction
- **Escalation** Prescriber
- **Consequence** Treatment given
- **Next step** Reassess


#### VR reasoning card C.15 — Anticoagulant

- **Why** Bleeding vs clot risk
- **Trainee action** Ask last dose time; renal function
- **What the trainee sees** Drug list
- **Information** Times
- **Interpretation** Within stop window?
- **Decision** Proceed/defer?
- **Correct action** Correct decision
- **Incorrect action** Not asking
- **Missed abnormality** Sign In stop
- **Escalation** Anaesthetist/senior
- **Consequence** Deferral
- **Next step** Plan

**Scenario maps:** S1 stages 8 Optimisation; S2 stages 8 Optimisation (in parallel).

| What-if | Branch | Correct action |
|---|---|---|
| WI-01 | What if blood pressure is too low? (top number 90 or less) | Call for help; lie flat; assess airway/breathing and start oxygen per protocol to the patient's target; two drips; request a fluid bolus (e.g. 500 mL crystalloid, NICE CG174; the clinician decides volume) and re-check EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ; treat cause (antibiotics if septic; adrenaline if anaphylaxis; blood if bleeding). |
| WI-02 | What if blood pressure is extremely high? (180/110 or more) | No symptoms: S1 → postpone and refer for control (don't rush BP down). With symptoms → emergency medical review. |
| WI-03 | What if the heart rate is very high? (above 130) | ABCDE; treat cause (pain relief, fluids, antibiotics); ECG-guided rhythm treatment by senior/medical team. |
| WI-04 | What if the heart rate is very slow? (40 or below) | ABCDE; stop causative drugs; emergency medicines/pacing per resuscitation team. |
| WI-05 | What if the patient has a fever? (38.0 °C or more) | Paracetamol; fluids if dry; antibiotics according to cause; sepsis bundle if criteria met. |
| WI-06 | What if the patient is cold? (36.0 °C or less) | Warm (forced-air blanket); don't send to theatre until 36.0 unless urgent CLINICAL REFERENCE — SOURCE BACKED (NICE CG65). |
| WI-07 | What if oxygen saturation is low? (below 94%) | Look at the patient and assess airway/breathing; check probe and waveform; if the low reading is real, sit up and start oxygen per protocol to the patient's target range (94–98%, or 88–92% if at risk of hypercapnic failure); find the cause (stop fluids if overloaded per senior; stimulate if over-sedated — antidote by clinician); reassess within minutes (process in Part 4.5). |
| WI-09 | What if breathing is very slow? (8 or less) | Wake/stimulate; oxygen; support airway; naloxone antidote PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; stop further opioids. |
| WI-10 | What if the patient becomes confused? | Treat what's found (sugar below 4 → glucose); sepsis bundle if infection; reassess capacity (consent may be invalid now). |
| WI-11 | What if pain gets much worse or the tummy becomes rigid? | Pain relief; nil by mouth; fluids; antibiotics; prepare for urgent surgery. |
| WI-26 | What if the patient is pregnant? | S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise → gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok. |
| WI-27 | What if pregnancy status is unknown or the patient declines a test? | Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior + anaesthetist decide whether to proceed. |
| WI-28 | What if the patient is allergic to an antibiotic? | Record; choose antibiotic by hospital rule (mild rash: cefazolin often allowed; severe: per policy) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . |
| WI-29 | What if an allergic reaction happens after a medicine? | STOP the drug; call for help; lie flat (sit up if struggling to breathe); call the emergency team; the responsible clinician gives IM adrenaline (adult standard dose 500 micrograms, repeat after 5 min if no improvement EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ); high-flow oxygen; IV fluid bolus per Resuscitation Council UK (adults 500–1000 mL crystalloid). |
| WI-30 | What if the patient takes blood thinners? | S1: proceed only if stopped per plan (Part 8); otherwise postpone; never stop antiplatelets after a recent stent without the heart team. S2: senior/haematology decide reversal PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED . |
| WI-31 | What if the patient has diabetes? | Medicine plan (Part 8); first on list if possible; check sugar at least hourly; target 6–10. |
| WI-32 | What if the patient takes steroids, GLP-1 injections or SGLT2 tablets? | Per Part 8/9. |
| WI-39 | What if the patient deteriorates? | Treat as found; increase monitoring. |
| WI-40 | What if the patient becomes septic? | Sepsis bundle within 1 hour (oxygen, cultures, antibiotics, fluids, lactate, urine output). |


### Implementation (FYP design)

- **Real workflow** Doses, routes and timings from formulary.
- **FYP simulation** Treatment = abstract object; 'administer' after required checks; effects via physiology engine.
- **FYP simplification** No doses, no drug calculations, no infusion rates.
- **AI** None for treatment decisions. Doses are never generated by AI.

- **Room** Bedside.
- **Patient** Lying; arm accessible for IV.
- **Staff** Nurse NPC performs double-check and administration if asked.
- **Equipment** IV cannula kit, fluid bag + giving set + drip stand/pump, drug tray (abstract), compression stockings/IPC, drug chart (tablet).
- **Available interactions** Prescribe, check, administer, insert IV, start fluid, fit IPC.

**USER MUST:**

1. Open 'Treat' tab; choose treatment object.
2. Complete required checks (ID, allergy, indication, timing).
3. Administer (or ask nurse; closed-loop read-back).
4. Insert IV (select site on arm → intent).
5. Hang fluid bag → connect → start (pump or gravity abstract).
6. Record medication decisions.
7. Re-check observations.
8. Answer 'Is it safe to continue?' with the master decision diagram (5 outcomes).
9. Request analgesia/antiemetic/fluids/antibiotics through the drug-chart workflow and PROPOSE the drug class; clinician confirms drug+dose; nurse administers.
10. Every drug passes three checks: allergy? kidneys/liver? pregnancy? (+ anticoagulant for bleeding drugs).

**SYSTEM THEN:**

1. Required-check engine; missing check → warning/critical.
2. Allergy-class conflict → contraindicated (E-HYPERSENSITIVITY eligible).
3. Effects applied to LitePhysio (pain↓, volume deficit↓).
4. IV success/failure seeded by patient factors; anaphylaxis eligibility when an allergen is given (WT 14.4).
5. Documentation flag required.
6. Requesting an allergen → nurse challenges (BEGINNER mode) or reaction occurs (ADVANCED mode).
7. S2 untreated → deterioration clock speeds up (HR +5–10 and NEWS2 +1 \~every 30 min; Type D).
8. Problem registry ordered by the 7-step priority ladder (WT 14B.1); treatment clashes detected (WT 14B.3).

- **If correct →** Treatments logged with checks; patient improves; goals satisfied.
- **If incorrect →** Skipped checks → items; allergen given → event + critical.
- **If information missing →** No IV → IV drugs/fluids unavailable.
- **If safety condition fails →** Allergy unknown → blocked (practice) / critical (assessment).

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L8-01 | administer(t) | prescribed | required checks complete | effect applied | missing check → item | Check list ticks | Nurse gives drug | — |
| L8-02 | administer allergen class | allergy exists | allergy known? | — (should not) | critical + E-HYPERSENSITIVITY | Alarm | Rash/flush | EVENT |
| L8-03 | insert_iv | patient cooperative | seeded success | iv=placed | failed → retry | — | Cannula appears | — |
| L8-04 | start_fluid | iv placed | — | volume deficit ↓ over time | — | Drip animation | Drip | — |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E8-A | Drug given without allergy check | Rash, BP falls | Hypersensitivity signs | Stop, help, protocol | Event + critical | Allowed | Alarm | EVENT | Protocol response |
| E8-B | No fluids for dehydrated S2 patient | HR rising | Hypovolaemia | Fluids + reassess | Deterioration event | Allowed | Monitor amber | URG | Fluids started |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV8-1 E-ANAPHYLAXIS | Allergen administered (advanced mode) — WT 14.4 [CLINICAL VALIDATION REQUIRED] | Hives, swollen lips, wheeze | BP falls (e.g. 80/45), SpO2 falls | 'My lips feel swollen… I can't breathe' | Anaphylaxis signs | Stop, lie flat, observe, reassure, call help if not resolving [CLINICAL VALIDATION REQUIRED] | Fall-risk item |
| EV8-2 E-HYPERSENSITIVITY | Allergen class given | Flushing, rash, breathlessness per severity | HR↑, BP↓, SpO2↓ | Alarm | Allergic reaction | Stop drug, call for help, local anaphylaxis protocol [CLINICAL VALIDATION REQUIRED] | Harmed outcome |
| EV8-3 E-DETERIORATE-DEHYD (S2) | No fluids by deadline & HR above threshold [CLINICAL VALIDATION REQUIRED] | Pale, thirsty, confused later | HR↑ BP↓ | Monitor alarm | Deterioration | Fluids, reassess, escalate | Critical if unrecognised |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Vital-sign monitor | ✓/✓ | 3, 8, 12–14 | SpO2, pulse, NIBP/MAP, ECG, alarms | Artefact if probe poor; alarm limits C |
| Blood culture bottles | —/cond. | 6, 8 | Cue | Delaying antibiotics for cultures → harm |
| Glucose/ketone meter | cond./cond. | 3, 8 | mmol/L | Hypo missed |
| IV cannula kit | ✓/✓ | 8 | Cue (insertion abstracted) | Failed cannulation → delay |
| IV giving set + fluid bags | —/✓ | 8 | Fluid type, volume | Overload in HF |
| Infusion pump | cond./cond. | 8 | Rate abstracted | Pump alarm |
| O2 supply, nasal cannula, simple + non-rebreather masks | cond./cond. | 8, 12 | Device + flow | Over-oxygenating CO2 retainer |
| Nasogastric tube | —/cond. | 8 | Cue | Senior-directed |
| Urinary catheter + urometer | —/cond. | 8 | Cue | Senior-directed |
| Medication trolley / drug chart | ✓/✓ | 8 | Clinician-entered prescriptions; allergy alert | Allergen request → nurse challenge |
| Sharps bin | ✓/✓ | 6, 8 | Cue | Needlestick |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Stockings / IPC | ✓/✓ | 8, 10 | Cue | Arterial disease → harm |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — TreatmentSystem effects + LitePhysio coupling; event triggers E-ANAPHYLAXIS/HYPERSENSITIVITY/DETERIORATE. Sannan — IV/fluid/IPC props + animations; Treat tab UI; rash/pale shaders. Moiz — Treatment library (no doses), required checks, medication decision content from local protocol; mark [CLINICAL VALIDATION REQUIRED]

**Validation:** All treatment indications and restrictions. [CLINICAL VALIDATION REQUIRED] · Physiological effect magnitudes of fluids/analgesia. [CLINICAL VALIDATION REQUIRED] · Hypersensitivity event representation. [CLINICAL VALIDATION REQUIRED] · Medication decision expected options. [CLINICAL VALIDATION REQUIRED] · Surgeon/anaesthetist: all treatment choices; postponement rules.


## 14. Step 9 — Consent

Priority P0 · walkthrough p.91 · build guides: B-R6, B-M3, B-R3, B-S10


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Explaining the diagnosis, the operation, benefits, risks and alternatives, checking understanding, and getting
the patient's signature (Part 10).
- **Why are we doing it?** A legal and ethical requirement and the patient's right to decide.
- **What does the trainee actually do?** 1. Make sure pain is controlled and the patient is alert; arrange privacy and an interpreter if needed.
2. Explain diagnosis and operation in plain words.
3. Benefits, risks (with numbers), alternatives, consequences of no treatment.
4. Ask what matters to them; answer questions honestly.
5. Teach-back.
6. Sign the form; give a copy; explain what happens next.
- **What does the trainee see?** Patient (and family), consent form, leaflet, interpreter phone.
- **What information do they receive?** Patient questions: "Will I be able to have children?" "Can I go home today?" "Can't I just take tablets?"
- **What does that information mean?** Questions reveal fears and what matters to the patient; they must be answered.
- **What is the normal/reference value?** Patient understands, teach-back succeeds, signs.
- **What counts as abnormal?** Patient confused, language barrier, wants more time, refuses.
- **What counts as seriously concerning?** No capacity in an urgent situation; patient under 18; refusal of life-saving surgery.
- **What should the trainee check next?** 1. Treat causes of confusion.
2. Interpreter.
3. Senior for refusal or capacity problems.
- **What happens if the trainee finds nothing wrong?** Go to STEP 10 (prepare patient).
- **What happens if the trainee finds something abnormal?** See Part 10 consent problems table.
- **When do we call the senior doctor?** Refusal; lack of capacity; legal doubt; minors.
- **When do we call the anaesthetist?** Anaesthetic risks are explained by the anaesthetist.
- **When might surgery be delayed?** S1: patient wants more time/information → rebook.
- **When should emergency surgery continue despite the abnormality?** S2: if the patient lacks capacity and surgery is urgent, the senior follows the emergency/best-interests route
under local law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
- **What happens if the trainee misses it?** Missing risks or alternatives → low consent score + 'patient complaint' in debrief; consenting a confused
patient → safety failure.
- **What happens after the correct action?** Valid consent recorded; checklist item ticked.
- **Where this comes from** GMC 2020; Montgomery 2015.
- **Clinician validation** Hospital consent form and law; surgeon's risk figures.


### What the player sees (WT Part 16 — Stage 9)

- **On screen / in front of the player** Patient, family, form, leaflet, interpreter phone.
- **Patient — what they say** Questions: fertility, going home, tablets instead?
- **Monitor — numbers** —
- **Interaction** Dialogue choices; teach-back; sign form.
- **Information revealed** Patient's understanding level.
- **Decision** Valid consent?
- **Correct action →** Signed form.
- **Incorrect action →** Missing risks/alternatives → low score/complaint.
- **Missed abnormality →** Consenting confused patient → safety failure.
- **Escalation →** Refusal → senior.


#### VR reasoning card C.17 — Consent

- **Why** Legal/ethical permission
- **Trainee action** Explain, risks, alternatives, teach-back
- **What the trainee sees** Patient, form
- **Information** Questions
- **Interpretation** Capacity, understanding
- **Decision** Valid?
- **Correct action** Signed
- **Incorrect action** Missing risks / confused patient
- **Missed abnormality** Invalid consent
- **Escalation** Senior
- **Consequence** Blocker
- **Next step** Prep

**Scenario maps:** S1 stages 9 Consent; S2 stages 9 Consent.

| What-if | Branch | Correct action |
|---|---|---|
| WI-41 | What if the patient refuses surgery? | Explore and correct misunderstandings; offer alternatives (antibiotics-only for simple appendicitis); never pressure; document; safety advice. |
| WI-42 | What if the patient cannot understand the consent discussion? | Interpreter; simpler words/diagrams; treat confusion; if no capacity and urgent → senior emergency route per law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . |
| WI-43 | What if the patient is under 18? | Parent/guardian consent; involve the young person. |


### Implementation (FYP design)

- **Real workflow** Surgeon consents.
- **FYP simulation** Trainee performs consent conversation via acts; surgeon NPC may countersign.
- **FYP simplification** Risk list pre-approved; no legal capacity assessment.
- **AI** P1 PROPOSED: communication classifier labels spoken consent explanation (debrief only unless validated); LLM phrases patient questions.

- **Room** Bedside, curtain.
- **Patient** Sitting up; anxiety behaviour visible.
- **Staff** Optional nurse witness.
- **Equipment** Consent form (tablet/paper prop), pen.
- **Available interactions** Communication acts; sign form.

**USER MUST:**

1. Choose EXPLAIN_PURPOSE, then CONSENT_DISCUSSION topics (procedure, benefits, risks, alternatives).
2. Answer patient question from approved answers.
3. CHECK_UNDERSTANDING (teach-back).
4. Hand form + pen to patient; patient signs.
5. Explain next steps.
6. Readiness (pain controlled, alert, privacy, interpreter); capacity check; diagnosis; operation; benefits; risks WITH NUMBERS; alternatives (S2 antibiotics-only: CODA/APPAC); no-treatment consequences; 'What matters most to you?'; teach-back; sign; copy; next steps.

**SYSTEM THEN:**

1. Tracks required elements; behaviour updates (anxiety/understanding).
2. Patient may ask question from pool based on persona.
3. Signature only accepted if required elements covered (practice) / logged otherwise (assessment).
4. Missing risks/alternatives → low consent score + 'patient complaint' in debrief.
5. Consenting a confused patient → safety failure; trained interpreter (not child relative).
6. Minor (<18) → guardian per law (Type C); refusal respected, never pressured (professionalism failure).

- **If correct →** Consent goal complete; understanding high.
- **If incorrect →** Missing risk element → major; false reassurance ('nothing can go wrong') → communication item.
- **If information missing →** Patient refuses to sign until question answered.
- **If safety condition fails →** Transfer/Sign In without valid consent → CRITICAL.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L9-01 | consent_discussion(topic) | — | topic in required set | element ticked | — | — | Patient nods | — |
| L9-02 | sign(consent) | elements complete | understanding ≥ thr | consent=signed | refused/incomplete | — | Signing animation | ST9 |
| L9-03 | answer_question(q,a) | question pending | answer approved | trust+ | wrong/none → trust− | — | — | — |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E9-A | Skipped risks | Patient signs quickly (low literacy persona) | Understanding low | Explain + teach-back | Major item | Allowed | Debrief | — | — |
| E9-B | False reassurance | Patient relaxes | Dishonest statement | Honest reassurance | Communication item | Allowed | Debrief | — | — |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV9-1 E-PT-QUESTION (scar/fertility) | Persona + context | Patient asks | — | Voice line | Concern | Approved answer; offer senior if unsure | Trust↓, anxiety↑ |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Consent form | ✓/✓ | 9 | Procedure, risks, signatures | Wrong procedure → blocker (local form C) |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Communication act engine + behaviour deltas. Sannan — Form/pen interactions; patient signing clip; anxiety idles. Moiz — Consent content & Q/A (surgeon review), rubric; mark [CLINICAL VALIDATION REQUIRED]

**Validation:** Risk/benefit/alternative wording for S1 and S2. [CLINICAL VALIDATION REQUIRED] · Answers to sensitive patient questions (fertility, scars, day-case). [CLINICAL VALIDATION REQUIRED] · Hospital consent form and law; surgeon's risk figures.


## 15. Step 10 — Preparing the patient (ward/ED checklist)

Priority P0 · walkthrough p.93 · build guides: B-R15, B-S10


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Physical and paperwork preparation so nothing is missing when the patient reaches theatre.
- **Why are we doing it?** Small omissions — jewellery, missing consent, unknown fasting time — cause harm or cancellations.
- **What does the trainee actually do?** 1. Re-confirm fasting times.
2. Hospital gown; remove jewellery (tape rings that won't come off), make-up, nail polish (for the oxygen
probe).
3. Note dentures, hearing aids, contact lenses, implants.
4. Hair removal only if needed, with clippers (not razors — they cause infection) CLINICAL REFERENCE — SOURCE
BACKED .
5. Site marking if required (usually NOT for these midline/belly-button keyhole operations — no left/right
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ).
6. Stockings if prescribed.
7. Pregnancy status recorded.
8. Consent present and correct.
9. Allergy band on; identity band on.
10. Diabetic: finger-prick sugar recorded.
11. Temperature at least 36.0 °C (warm if not) CLINICAL REFERENCE — SOURCE BACKED .
12. All documents together: notes, consent, results, drug chart, scans.
13. Complete the checklist.
- **What does the trainee see?** Gown, jewellery tray, clippers, marker pen, stockings, checklist form, document folder.
- **What information do they receive?** Nurse: "Checklist is complete except pregnancy status." Patient might say: "I did have a cup of tea with milk at
7."
- **What does that information mean?** Each item must be confirmed; any gap must be filled before transfer.
- **What is the normal/reference value?** Every item ticked.
- **What counts as abnormal?** Missing item; late fasting discovery; temperature below 36.0.
- **What counts as seriously concerning?** Missing/invalid consent; unknown pregnancy status; identity problem.
- **What should the trainee check next?** 1. Fix the missing item.
2. Tell anaesthetist about any late fasting discovery.
- **What happens if the trainee finds nothing wrong?** Call theatre: patient ready (STEP 11/12).
- **What happens if the trainee finds something abnormal?** Fix before transfer; do not transfer with missing consent.
- **When do we call the senior doctor?** Consent problems.
- **When do we call the anaesthetist?** Late fasting breach; low temperature.
- **When might surgery be delayed?** Until all blockers fixed.
- **When should emergency surgery continue despite the abnormality?** S2: fast but complete; uncertain fasting recorded rather than waited for.
- **What happens if the trainee misses it?** Razor used → infection-risk debrief. Missing consent → theatre returns the patient.
- **What happens after the correct action?** 'Ready for theatre'.
- **Where this comes from** NICE NG125 (hair removal); NICE CG65; WHO SSC.
- **Clinician validation** Hospital ward checklist form; site-marking policy.


### What the player sees (WT Part 16 — Stage 10)

- **On screen / in front of the player** Gown, jewellery tray, clippers, marker, stockings, checklist, folder.
- **Patient — what they say** "Oh — I did have tea with milk at 7."
- **Monitor — numbers** Temperature check.
- **Interaction** Tick items; tape ring; clippers (not razor).
- **Information revealed** Missing items highlighted when checked.
- **Decision** Ready?
- **Correct action →** 'Ready for theatre'.
- **Incorrect action →** Razor → infection debrief; missing consent → transfer refused.
- **Missed abnormality →** —
- **Escalation →** Late fasting → anaesthetist.


#### VR reasoning card C.18 — Ward preparation

- **Why** Prevent theatre errors
- **Trainee action** Checklist items
- **What the trainee sees** Checklist
- **Information** Gaps
- **Interpretation** Complete?
- **Decision** Transfer?
- **Correct action** Ready
- **Incorrect action** Razor; missing consent
- **Missed abnormality** Cancellation
- **Escalation** Nurse
- **Consequence** Transfer
- **Next step** Transfer

**Scenario maps:** S1 stages — (not a separate S1 map stage); S2 stages — (not a separate S2 map stage).

| What-if | Branch | Correct action |
|---|---|---|
| WI-08 | What if breathing is very fast? (25 or more) | Treat cause; sepsis bundle if infection. |
| WI-33 | What if the patient hasn't fasted? | Tell anaesthetist honestly. |
| WI-34 | What if fasting status is unknown? | Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In. |
| WI-45 | What if documents are missing? | Re-consent or find; don't transfer. |


### Implementation (FYP design)

- **Real workflow** Nurse completes.
- **FYP simulation** Trainee verifies checklist with nurse NPC.
- **FYP simplification** Checklist on tablet; gown change is a fade.
- **AI** None.

- **Room** Bedside.
- **Patient** In gown after fade.
- **Staff** Nurse NPC.
- **Equipment** Checklist (tablet), skin marker, clippers (optional), IPC sleeves.
- **Available interactions** Tick with verification actions.

**USER MUST:**

1. Open ward checklist with nurse.
2. Verify each item (some require an action: ask fasting again, check jewellery hotspot).
3. Mark site if applicable.
4. Confirm documents bundle.
5. Sign checklist.
6. Work through the 13 ward-checklist items (fasting re-confirmed; gown; jewellery/make-up/NAIL POLISH; dentures/aids/lenses/implants; clippers only if needed; site marking usually N/A; stockings; pregnancy status; consent; ID + allergy bands; CBG if diabetic; temp ≥36.0; documents together).

**SYSTEM THEN:**

1. Each item validated against state (not just ticked).
2. Discrepancies open events.
3. Checklist complete → transfer enabled.
4. Late fasting discovery ('tea with milk at 7') → tell anaesthetist.
5. Razor → infection-risk debrief; missing consent → transfer refused / theatre returns patient.

- **If correct →** Checklist complete & consistent.
- **If incorrect →** Ticking an item that contradicts state → critical/major.
- **If information missing →** Item missing → transfer blocked.
- **If safety condition fails →** No consent/unknown fasting → blocked.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L10-01 | tick(item) | — | state supports tick | ticked | contradiction → item | — | — | — |
| L10-02 | sign(ward_checklist) | all ticked | — | transfer enabled | — | — | — | ST11 |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E10-A | Ticks 'fasted' without checking | — | Unverified | Ask/verify | Major if later breach | Allowed | — | — | — |
| E10-B | Jewellery left on | Ring visible on hand | Diathermy/burn risk [CLINICAL VALIDATION REQUIRED] | Remove/tape | Minor/major | Allowed | — | — | — |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV10-1 E-CONSENT-FORM-INCOMPLETE | Random (difficulty≥2) | — | — | Nurse: 'signature missing on page 2' | Documentation gap | Complete with patient | Transfer blocked |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Tympanic thermometer | ✓/✓ | 3, 10 | °C | <36.0 → warm |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Stockings / IPC | ✓/✓ | 8, 10 | Cue | Arterial disease → harm |
| Clippers | cond./cond. | 10 | Cue | Razor → infection risk |
| Skin marker | cond./cond. | 10 | Cue | Usually N/A (C) |
| Forced-air warmer | ✓/✓ | 10–11 | Cue | — |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Checklist verification engine (tick requires state evidence). Sannan — Gown fade, marker decal, jewellery hotspot. Moiz — Ward checklist content from local form; mark [CLINICAL VALIDATION REQUIRED]

**Validation:** Ward checklist items (use supervising hospital's form). [CLINICAL VALIDATION REQUIRED] · Site-marking policy for laparoscopic procedures. [CLINICAL VALIDATION REQUIRED] · Hospital ward checklist form; site-marking policy.


## 16. Step 11 — Theatre preparation

Priority P0 · walkthrough p.95 · build guides: B-S9, B-S1, B-R15, B-R14


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Making sure the operating theatre, equipment and team are ready for this specific patient.
- **Why are we doing it?** Discovering a broken machine after the patient is asleep is dangerous.
- **What does the trainee actually do?** 1. Anaesthetic machine check done and logged.
2. Monitors ready (oxygen, BP, ECG, carbon dioxide, temperature).
3. Suction working; oxygen; airway equipment; difficult-airway trolley known.
4. Operating table and padding.
5. Keyhole 'tower' (camera, light, gas pump) + CO2 gas cylinder with spare; instruments; open set available in
case of conversion.
6. Diathermy (electric cutting/sealing device) with its sticky return pad.
7. Warming device.
8. Emergency trolley and defibrillator checked.
9. Specimen pots and labels; count sheets.
10. Team brief if used locally.
- **What does the trainee see?** Theatre with machines, trays, screens, gas cylinders, checklist board.
- **What information do they receive?** Scrub nurse: "The gas pump shows low supply." Check log signed or unsigned.
- **What does that information mean?** Any fault must be fixed before the patient arrives.
- **What is the normal/reference value?** All equipment working.
- **What counts as abnormal?** Fault found.
- **What counts as seriously concerning?** Anaesthetic machine failure; no working suction; no oximeter.
- **What should the trainee check next?** 1. Report; replace/repair; delay start.
- **What happens if the trainee finds nothing wrong?** Send for the patient.
- **What happens if the trainee finds something abnormal?** Fix first.
- **When do we call the senior doctor?** Delay affecting an emergency case.
- **When do we call the anaesthetist?** Any anaesthetic equipment fault.
- **When might surgery be delayed?** Until equipment safe.
- **When should emergency surgery continue despite the abnormality?** S2: a substitute theatre may be used CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .
- **What happens if the trainee misses it?** Time Out cannot be completed.
- **What happens after the correct action?** 'Theatre ready'.
- **Medical words used here** Diathermy Electric device to cut and stop bleeding.
Tower Stack of keyhole equipment: screen, camera, light, gas pump.
- **Where this comes from** Association of Anaesthetists equipment checks (2019); WHO-WFSA 2018.
- **Clinician validation** Theatre team: equipment lists; which items trainee vs staff verify.


### What the player sees (WT Part 16 — Stage 11)

- **On screen / in front of the player** Theatre: machines, tower, CO2 cylinder gauge, trays, diathermy, warming, resus trolley.
- **Patient — what they say** —
- **Monitor — numbers** Anaesthetic machine check log.
- **Interaction** Inspect/confirm items.
- **Information revealed** Fault messages (e.g. low gas).
- **Decision** Ready?
- **Correct action →** 'Theatre ready'.
- **Incorrect action →** Fault ignored → Time Out fails.
- **Missed abnormality →** —
- **Escalation →** Report to theatre lead.

**Scenario maps:** S1 stages 12 Theatre preparation (runs in parallel before the patient arrives); S2 stages 12 Theatre preparation.

| What-if | Branch | Correct action |
|---|---|---|
| WI-44 | What if equipment fails? | Report; replace; don't start. |


### Implementation (FYP design)

- **Real workflow** Several staff in parallel.
- **FYP simulation** Trainee inspects stations and confirms NPC reports; fixes faults via correct escalation.
- **FYP simplification** Panels instead of real machine procedures; instruments as trolley icons.
- **AI** None (P1: NPC phrasing).

- **Room** OR: table, anaesthetic machine, monitor, suction, lap tower, instrument trolley, diathermy, warmer, resus trolley.
- **Patient** Not present yet.
- **Staff** Scrub nurse, circulating nurse, anaesthetic assistant NPCs.
- **Equipment** As listed.
- **Available interactions** Inspect station panels; request fix; confirm.

**USER MUST:**

1. Teleport to OR.
2. Open each station panel (anaesthetic machine, monitor, suction, tower, energy device, table, emergency trolley, specimen/count).
3. Run the abstract check.
4. If fault shown: report to circulating nurse (select) and wait for replacement.
5. Confirm 'Theatre ready' with team.
6. Check: anaesthetic machine check logged; monitors; suction; airway kit + difficult-airway trolley; table; tower + CO2 + SPARE cylinder; open set; diathermy + pad; warming; resus trolley + defib; specimen pots; count sheets; team brief.

**SYSTEM THEN:**

1. Stations have states/faults from scenario/events.
2. Readiness = all REQUIRED groups ready.
3. Faults resolved by correct escalation + delay.
4. Fault messages (e.g. 'gas pump shows low supply'); unfixed fault → Time Out cannot complete.
5. S2: substitute theatre allowed (Type C).

- **If correct →** Theatre ready.
- **If incorrect →** Declaring ready with faults → major/critical.
- **If information missing →** Required station unchecked → not ready.
- **If safety condition fails →** Time Out blocked while required equipment not ready.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L11-01 | check(station) | in OR | station ok? | ready | fault → fault state | Light green/red | Panel | — |
| L11-02 | report_fault | fault | — | replacement delay | ignored → item | — | Nurse fetches | — |
| L11-03 | declare_ready | — | all required ready | OR_READY | else major | — | Team nods | ST11 |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E11-A | Insufflator fault ignored | Red panel | Fault | Report & replace | Major; Time Out blocked | Blocked | — | BLOCK | Replace |
| E11-B | Suction not checked | — | Unchecked | Check | Major | — | — | — | — |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV11-1 E-EQUIP-INSUFFLATOR-FAULT | Tower check | — | Tower panel error | Beep | Faulty device | Report, replacement | Delay/critical if proceeding |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Suction | ✓/✓ | 11, 13 | Cue | Not working → RSI unsafe |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Forced-air warmer | ✓/✓ | 10–11 | Cue | — |
| Operating table + padding | ✓/✓ | 11 | Cue | — |
| Anaesthesia machine + ventilator + check log | ✓/✓ | 11, 13 | Check done/not; settings | Check not done → Sign In fails |
| Capnograph | ✓/✓ | 11, 13 | EtCO2 waveform (post-induction abstracted) | — |
| Airway kit + difficult-airway trolley | ✓/✓ | 11, 13 | Cue | Difficult airway not declared |
| Surgical lights | ✓/✓ | 11, 14 | Cue | — |
| Laparoscopic tower (camera, light, monitor, insufflator) | ✓/✓ | 11, 14 | Image, insufflation pressure/flow, fault messages | Fault → delay |
| CO2 cylinder + spare | ✓/✓ | 11 | Gauge level | Empty → fault scenario |
| Diathermy unit + return electrode | ✓/✓ | 11 | Cue | Pad missing → burn risk |
| Instrument trays (lap chole / lap + open appendix) | ✓/✓ | 11, 14 | Sterility indicators | Indicator fail → stop |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — ORReadiness model + fault events. Sannan — OR stations (panels), tower/trolley props, nurse replacement animation (largest VR task). Moiz — Station content per procedure from WT Step 11 (AoA 2019)/18/19; [CLINICAL VALIDATION REQUIRED]

**Validation:** S1/S2 equipment lists and positioning. [CLINICAL VALIDATION REQUIRED] · Which items the trainee (vs NPC) verifies. [CLINICAL VALIDATION REQUIRED] · Theatre team: equipment lists; which items trainee vs staff verify.


## 17. Step 12 — Transfer and handover (SBAR)

Priority P0 · walkthrough p.97 · build guides: B-R16, B-M15, B-R8


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** Moving the patient safely to theatre and telling the theatre team everything important in a structured way.
- **Why are we doing it?** Information lost during handover is a classic cause of errors CLINICAL REFERENCE — SOURCE BACKED (WHO Patient
Safety Solutions 2007).
- **What does the trainee actually do?** 1. Theatre calls for the patient.
2. Choose escort and monitoring based on stability (no international numeric rule exists; ProcSense uses
SIMULATOR DESIGN VALUE (PROVISIONAL) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ): NEWS2
0–4 porter + nurse; 5–6 nurse + portable oxygen monitor; 7+ doctor + nurse with full monitoring and
oxygen.
3. Transfer with all documents.
4. Theatre reception nurse checks identity, consent, allergies, fasting, site.
5. Give SBAR handover to the anaesthetist.
- **What does the trainee see?** Trolley, porter, portable monitor, folder; theatre reception desk.
- **What information do they receive?** Reception nurse: "Name and date of birth? Any allergies? When did you last eat?"
- **What does that information mean?** SBAR = Situation (who and why now), Background (relevant history), Assessment (current state, results),
Recommendation (what you need/plan).
- **What is the normal/reference value?** All key items handed over. S1 example: "S: Ayesha Khan, 42, for lap chole. B: no medical problems, no
allergies. A: obs normal, fasted since 22:00 food / 06:00 water, LFTs normal, pregnancy test negative, consent
signed. R: ready for theatre, no antibiotic needed per protocol."
- **What counts as abnormal?** Missing item (allergy, fasting time, antibiotic time, blood thinner).
- **What counts as seriously concerning?** Patient deteriorates during transfer.
- **What should the trainee check next?** 1. If deterioration in transit: stop, ABCDE, call for help.
- **What happens if the trainee finds nothing wrong?** STEP 13 (Sign In).
- **What happens if the trainee finds something abnormal?** Provide the missing information immediately.
- **When do we call the senior doctor?** Deterioration during transfer.
- **When do we call the anaesthetist?** This step IS the handover to the anaesthetist.
- **When might surgery be delayed?** Missing documents → fetched before proceeding.
- **When should emergency surgery continue despite the abnormality?** S2: handover can be brief but must include aspiration risk, antibiotic time, fluids given, latest NEWS2, lactate,
kidney status, pregnancy status, scan result.
- **What happens if the trainee misses it?** Anaesthetist asks for the missing items; repeated omissions scored; corridor oxygen drop ignored →
emergency branch.
- **What happens after the correct action?** Theatre team accepts the patient.
- **Medical words used here** SBAR Situation, Background, Assessment, Recommendation.
- **Where this comes from** WHO Patient Safety Solutions 2007; WHO-WFSA 2018.
- **Clinician validation** Hospital transfer policy; required handover items.


### What the player sees (WT Part 16 — Stage 12)

- **On screen / in front of the player** Trolley, porter, monitor, reception desk, anaesthetist.
- **Patient — what they say** Reception nurse questions.
- **Monitor — numbers** Portable monitor if unstable.
- **Interaction** Choose escort; deliver SBAR (structured choices or speech).
- **Information revealed** Missing items prompted by anaesthetist.
- **Decision** Is handover complete?
- **Correct action →** Handover accepted.
- **Incorrect action →** Omissions scored.
- **Missed abnormality →** Corridor deterioration ignored → arrest branch.
- **Escalation →** Call for help in transit.


#### VR reasoning card C.19 — Transfer & handover

- **Why** Safe movement; information
- **Trainee action** Escort by stability; SBAR
- **What the trainee sees** Trolley, monitor
- **Information** Handover
- **Interpretation** Complete?
- **Decision** Safe?
- **Correct action** Accepted
- **Incorrect action** Omissions
- **Missed abnormality** Questions/stop
- **Escalation** Anaesthetist
- **Consequence** Sign In
- **Next step** Sign In

**Scenario maps:** S1 stages — (not a separate S1 map stage); S2 stages — (not a separate S2 map stage).

| What-if | Branch | Correct action |
|---|---|---|
| WI-46 | What if the patient deteriorates during transfer? | Stop, oxygen, call for help; return or proceed per stability. |
| WI-47 | What if there's an identity mismatch at theatre? | STOP; resolve with ward/admissions. |


### Implementation (FYP design)

- **Real workflow** Several checks at reception.
- **FYP simulation** Trainee escorts and does SBAR handover form (P0) / speech (P1).
- **FYP simplification** Reception check merged into Sign In prompt.
- **AI** P1 PROPOSED: speech handover → ASR → classifier maps utterances to SBAR elements; trainee confirms on tablet; critical elements never scored from AI alone.

- **Room** Corridor + holding bay.
- **Patient** On trolley.
- **Staff** Porter, anaesthetist, theatre nurse NPCs.
- **Equipment** Trolley, portable monitor (if needed), documents bundle.
- **Available interactions** Accompany, handover sections.

**USER MUST:**

1. Receive theatre call; select 'Send for transfer' with monitoring option.
2. Accompany patient.
3. Open handover form: fill S, B, A, R by selecting facts from Known panel.
4. Deliver (P1 speak).
5. Choose escort by NEWS2 (Type D/C); transfer with documents; reception nurse checks; give SBAR (structured choices or speech).

**SYSTEM THEN:**

1. Transport state machine.
2. Handover completeness/accuracy vs required elements; critical omissions flagged.
3. Anaesthetist NPC asks follow-up if omission (practice).
4. Anaesthetist prompts for missing items; omissions scored.
5. S2 handover MUST include aspiration risk, antibiotic name+time, fluids, latest NEWS2, lactate, kidney status, pregnancy status, scan result.
6. Corridor deterioration ignored → arrest branch.

- **If correct →** Complete accurate handover.
- **If incorrect →** Omission of critical element → CRITICAL.
- **If information missing →** Documents missing → delay.
- **If safety condition fails →** Transfer without consent/checklist → blocked/critical.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L12-01 | request_transport(OT) | ward checklist signed | stability | IN_TRANSIT | blocked if not signed | — | Bed moves | — |
| L12-02 | handover(section) | at holding | elements | completeness score | critical omission | — | Anaesthetist listens | ST12 |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E12-A | Allergy omitted | Anaesthetist nods | Omission | Include allergy | Critical | Allowed (assessment) / prompted (practice) | Debrief | — | — |
| E12-B | Fasting uncertainty omitted (S2) | — | Aspiration risk | Include | Critical | — | — | — | — |

| Event | Trigger | Patient | Monitor | Visual/audio | Recognise | Response | If no response |
|---|---|---|---|---|---|---|---|
| EV12-1 E-THEATRE-CALLS | Scheduled/probabilistic | — | — | Phone | Time pressure | State outstanding items + ETA; don't skip critical steps | Critical if transfer without consent/handover |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Trolley / bed | ✓/✓ | 1–12 | Positioning cue | — |
| Vital-sign monitor | ✓/✓ | 3, 8, 12–14 | SpO2, pulse, NIBP/MAP, ECG, alarms | Artefact if probe poor; alarm limits C |
| O2 supply, nasal cannula, simple + non-rebreather masks | cond./cond. | 8, 12 | Device + flow | Over-oxygenating CO2 retainer |
| PPE | ✓/✓ | All | Cue | — |
| Patient notes / folder | ✓/✓ | 1–12 | Cue | Missing documents block transfer |
| Transfer trolley + portable monitor + O2 cylinder | ✓/✓ | 12 | Cue | Corridor deterioration |
| SBAR handover sheet | ✓/✓ | 12 | Cue | Omissions |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |

**Owners:** Ruhan — Handover scoring + P1 classifier service. Sannan — Corridor transfer, holding bay, anaesthetist NPC. Moiz — Handover element lists S1/S2; rubric; [CLINICAL VALIDATION REQUIRED]

**Validation:** Required handover elements and critical omissions per scenario. [CLINICAL VALIDATION REQUIRED] · Transfer monitoring rule. [CLINICAL VALIDATION REQUIRED] · Hospital transfer policy; required handover items.


## 18. Step 13 — WHO Sign In (before the patient goes to sleep)

Priority P0 · walkthrough p.99 · build guides: B-R15, B-S10, B-R4


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** A spoken checklist by the nurse and anaesthetist (with the patient awake) before anaesthesia starts — see Part
15.
- **Why are we doing it?** Last chance to catch errors while the patient can still answer.
- **What does the trainee actually do?** 1. Patient confirms identity, site, procedure, consent.
2. Site marked or 'not applicable'.
3. Anaesthesia machine and medication check complete.
4. Oximeter on and working.
5. Known allergy?
6. Difficult airway or aspiration risk? (equipment/help ready)
7. Risk of more than 500 mL blood loss? (two drips/fluids planned)
- **What does the trainee see?** Anaesthetic room; checklist board; anaesthetist and nurse.
- **What information do they receive?** Anaesthetist: "Is there a risk of more than 500 mL blood loss?"
- **What does that information mean?** Every item needs an answer; 'no' or doubt = stop.
- **What is the normal/reference value?** All confirmed.
- **What counts as abnormal?** Any discrepancy.
- **What counts as seriously concerning?** Identity/consent doubt; oximeter not working.
- **What happens if the trainee finds nothing wrong?** Anaesthesia (not simulated) → STEP 14.
- **What happens if the trainee finds something abnormal?** Stop and resolve.
- **When do we call the anaesthetist?** Leads Sign In.
- **When might surgery be delayed?** Until resolved.
- **When should emergency surgery continue despite the abnormality?** S2: aspiration risk answered YES and planned for — not a reason to stop.
- **What happens if the trainee misses it?** Team member challenges; repeated skip = case stops.
- **What happens after the correct action?** Patient anaesthetised; move to Time Out.
- **Where this comes from** WHO Surgical Safety Checklist 2009.
- **Clinician validation** Theatre governance: local additions.


### What the player sees (WT Part 16 — Stages 13–14)

- **On screen / in front of the player** Anaesthetic room/theatre; checklist board; team.
- **Patient — what they say** Patient answers Sign In identity questions.
- **Monitor — numbers** Live monitor.
- **Interaction** Lead or answer each item; direct question to right person.
- **Information revealed** Each item lights green when confirmed.
- **Decision** All safe?
- **Correct action →** 'READY FOR INCISION' — case complete.
- **Incorrect action →** Skipped item → challenge; repeated → case stops.
- **Missed abnormality →** Antibiotic timing missed → debrief/stop.
- **Escalation →** —


#### VR reasoning card C.20 — WHO Sign In

- **Why** Pre-induction safety
- **Trainee action** Answer/lead WHO items
- **What the trainee sees** Board
- **Information** Items
- **Interpretation** All satisfied?
- **Decision** Proceed?
- **Correct action** Induction
- **Incorrect action** Skipped item
- **Missed abnormality** Challenge/stop
- **Escalation** Team
- **Consequence** Induction
- **Next step** Theatre prep

**Scenario maps:** S1 stages 11 WHO Sign In; S2 stages 11 WHO Sign In.

| What-if | Branch | Correct action |
|---|---|---|
| WI-47 | What if there's an identity mismatch at theatre? | STOP; resolve with ward/admissions. |


### Implementation (FYP design)

- **Real workflow** \~1–2 min conversation.
- **FYP simulation** Trainee leads; NPCs and patient answer from state.
- **FYP simplification** Induction itself not simulated.
- **AI** None (deterministic).

- **Room** Anaesthetic room/theatre.
- **Patient** Awake on trolley/table.
- **Staff** Anaesthetist, nurse NPCs.
- **Equipment** Checklist board/tablet, monitor with pulse oximeter.
- **Available interactions** Read item, choose response, act on discrepancy.

**USER MUST:**

1. Start Sign In.
2. For each item, ask/confirm; select response.
3. Attach pulse oximeter if not on (monitor must show SpO2).
4. Resolve any discrepancy.
5. Complete Sign In.
6. Lead or answer each WHO 2009 Sign In item; direct each question to the right person.

**SYSTEM THEN:**

1. Expected response derived from state.
2. Contradiction → critical + stop.
3. Pulse-ox item requires sensor.attached.
4. Completion unlocks Time Out.
5. Each item lights green when confirmed; 'no' or doubt = STOP.
6. Skipped item → team challenge; repeated skip → case stops.
7. ProcSense educational extensions shown separately, never as WHO items.

- **If correct →** Sign In complete.
- **If incorrect →** False 'yes' → critical.
- **If information missing →** Unanswered item blocks completion.
- **If safety condition fails →** Induction (and Time Out) blocked until complete.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L13-01 | checklist_item(SI,i,resp) | — | resp == expected(state) | item ok | critical | — | Nurse ticks board | — |
| L13-02 | complete(SI) | all ok | — | SIGN_IN_DONE | blocked | — | — | ST13 |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E13-A | Says 'no known allergy' when allergy known | Anaesthetist accepts | Contradiction | Correct it | Critical | Stopped | Red | — | — |
| E13-B | Pulse oximeter not attached | Monitor blank | Item can't be yes | Attach | Blocked | Blocked | — | — | — |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Allergy band | ✓/✓ | 1, 7, 13 | Allergy flag (details by asking) | Missing band → reaction branch |
| Vital-sign monitor | ✓/✓ | 3, 8, 12–14 | SpO2, pulse, NIBP/MAP, ECG, alarms | Artefact if probe poor; alarm limits C |
| Pulse oximeter probe | ✓/✓ | 3, 13 | SpO2, pulse, pleth quality | No waveform → recheck; Sign In requires working |
| Suction | ✓/✓ | 11, 13 | Cue | Not working → RSI unsafe |
| PPE | ✓/✓ | All | Cue | — |
| Anaesthesia machine + ventilator + check log | ✓/✓ | 11, 13 | Check done/not; settings | Check not done → Sign In fails |
| Capnograph | ✓/✓ | 11, 13 | EtCO2 waveform (post-induction abstracted) | — |
| Airway kit + difficult-airway trolley | ✓/✓ | 11, 13 | Cue | Difficult airway not declared |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |
| WHO checklist board | ✓/✓ | 13–15 | Item status pending/confirmed | Skipped item → stop |

**Owners:** Ruhan — SafetyChecklist engine with state-derived expectations. Sannan — Checklist board + nurse ticking. Moiz — Paraphrased items + local additions; [CLINICAL VALIDATION REQUIRED]

**Validation:** Local Sign In additions. [CLINICAL VALIDATION REQUIRED] · Theatre governance: local additions.


## 19. Step 14 — WHO Time Out (before the first cut) — the finish line

Priority P0 · walkthrough p.101 · build guides: B-R15, B-S10


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** The whole team pauses, introduces itself and confirms everything before the first incision — see Part 15.
- **Why are we doing it?** Final protection against wrong patient/procedure and missed preparations (antibiotics, imaging, equipment).
- **What does the trainee actually do?** 1. Everyone states name and role.
2. Confirm the patient's name, procedure, and where the incision will be made.
3. Antibiotic prophylaxis given within the last 60 minutes? (or not applicable)
4. Surgeon: critical/non-routine steps, expected duration, anticipated blood loss.
5. Anaesthetist: patient-specific concerns.
6. Nurse: sterility (including indicator results) confirmed; equipment issues or concerns.
7. Essential imaging displayed?
- **What does the trainee see?** Theatre; whole team; checklist board; screens showing scans.
- **What information do they receive?** e.g. Surgeon: "Expected duration 60 minutes, minimal blood loss." Nurse: "Sterility indicators confirmed."
- **What does that information mean?** All aligned → safe to start.
- **What is the normal/reference value?** All confirmed.
- **What counts as abnormal?** Antibiotic needed but not given or given more than 60 minutes ago; imaging missing; equipment fault.
- **What happens if the trainee finds nothing wrong?** READY FOR INCISION — the scenario is complete.
- **What happens if the trainee finds something abnormal?** Fix (give antibiotic, display imaging, fix equipment), then repeat item.
- **When do we call the senior doctor?** Surgeon is present.
- **When might surgery be delayed?** Until fixed.
- **When should emergency surgery continue despite the abnormality?** S2: urgency doesn't skip Time Out.
- **What happens if the trainee misses it?** Antibiotic timing missed → debrief/stop per setting.
- **What happens after the correct action?** Optional time-lapse → Sign Out → debrief.
- **Where this comes from** WHO SSC 2009; NICE NG125 antibiotic timing.
- **Clinician validation** Antibiotic rule per scenario.


### What the player sees (WT Part 16 — Stages 13–14)

- **On screen / in front of the player** Anaesthetic room/theatre; checklist board; team.
- **Patient — what they say** Patient answers Sign In identity questions.
- **Monitor — numbers** Live monitor.
- **Interaction** Lead or answer each item; direct question to right person.
- **Information revealed** Each item lights green when confirmed.
- **Decision** All safe?
- **Correct action →** 'READY FOR INCISION' — case complete.
- **Incorrect action →** Skipped item → challenge; repeated → case stops.
- **Missed abnormality →** Antibiotic timing missed → debrief/stop.
- **Escalation →** —


#### VR reasoning card C.21 — WHO Time Out

- **Why** Pre-incision safety
- **Trainee action** WHO items in order
- **What the trainee sees** Board, team
- **Information** Items
- **Interpretation** All satisfied?
- **Decision** Ready?
- **Correct action** Ready for incision
- **Incorrect action** Antibiotic timing missed
- **Missed abnormality** Stop/debrief
- **Escalation** Surgeon
- **Consequence** Procedure start
- **Next step** Procedure

**Scenario maps:** S1 stages 13 WHO Time Out, 14 Procedure start; S2 stages 13 WHO Time Out, 14 Procedure start.


### Implementation (FYP design)

- **Real workflow** Team pause.
- **FYP simulation** Trainee leads/participates; NPC team answers.
- **FYP simplification** Anaesthesia/sterile field abstract.
- **AI** None.

- **Room** OR with team at positions.
- **Patient** On table, draped (abstract).
- **Staff** Surgeon, anaesthetist, scrub, circulating.
- **Equipment** Checklist board, imaging screen.
- **Available interactions** Lead items.

**USER MUST:**

1. Start Time Out.
2. Prompt introductions.
3. Confirm patient/site/procedure.
4. Ask each discipline for concerns.
5. Confirm antibiotic timing and imaging.
6. Complete.
7. WHO 2009 Time Out items in order; confirm antibiotic within 60 min or N/A; imaging displayed; sterility indicators.

**SYSTEM THEN:**

1. Checks vs state (antibiotic time from treatments log).
2. Blocks procedure until complete & ready.
3. Gap → fix then repeat item; S2 urgency never skips Time Out.
4. All confirmed → READY FOR INCISION (end state); optional time-lapse → Sign Out.

- **If correct →** Time Out done → procedure starts.
- **If incorrect →** Wrong confirmation → critical.
- **If information missing →** Item unanswered blocks.
- **If safety condition fails →** Equipment not ready or discrepancy → blocked.

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L14-01 | checklist_item(TO,i,resp) | SI done | expected | ok | critical | — | Team speaks | — |
| L14-02 | complete(TO) | ok & OR_READY | — | PROCEDURE | blocked | — | Lights on | PROC |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E14-A | Antibiotic 'yes' when not given | — | Contradiction | Correct/decide | Critical | Stopped | — | — | — |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| Vital-sign monitor | ✓/✓ | 3, 8, 12–14 | SpO2, pulse, NIBP/MAP, ECG, alarms | Artefact if probe poor; alarm limits C |
| PPE | ✓/✓ | All | Cue | — |
| Surgical lights | ✓/✓ | 11, 14 | Cue | — |
| Laparoscopic tower (camera, light, monitor, insufflator) | ✓/✓ | 11, 14 | Image, insufflation pressure/flow, fault messages | Fault → delay |
| Instrument trays (lap chole / lap + open appendix) | ✓/✓ | 11, 14 | Sterility indicators | Indicator fail → stop |
| Drapes | ✓/✓ | 14 | Cue | — |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |
| WHO checklist board | ✓/✓ | 13–15 | Item status pending/confirmed | Skipped item → stop |

**Owners:** Ruhan — Time Out logic + procedure phase transition. Sannan — Team positions, intro lines, time-lapse. Moiz — Items + antibiotic timing rule; [CLINICAL VALIDATION REQUIRED]

**Validation:** Antibiotic timing rule for each scenario. [CLINICAL VALIDATION REQUIRED] · Antibiotic rule per scenario.


## 20. Step 15 — WHO Sign Out and handover to recovery (optional)

Priority P1 · walkthrough p.102 · build guides: B-R15, B-M15, B-R9, B-S12


### Walkthrough specification (verbatim — WT Part 11)

- **What are we doing?** After the operation, before the patient leaves theatre: confirm the procedure name, counts of
instruments/swabs/needles, specimen label, equipment problems, and key concerns for recovery.
- **Why are we doing it?** Prevents retained instruments, lost specimens and poor handover.
- **What does the trainee actually do?** 1. Nurse confirms procedure name recorded.
2. Counts correct.
3. Specimen (gallbladder/appendix) labelled — label read aloud including patient name.
4. Equipment problems recorded.
5. Team reviews key concerns for recovery; hand over to recovery nurse.
- **What happens if the trainee finds nothing wrong?** Case ends; debrief.
- **What happens if the trainee finds something abnormal?** Count wrong → search, X-ray per local, escalate.
- **Where this comes from** WHO SSC 2009.
- **Clinician validation** Recovery handover items.


#### VR reasoning card C.22 — Intra-operative event — ProcSense educational/simulation extension — clinician validation required.

- **Why** e.g. conversion to open, bleeding needing suction, normal appendix found (surgeon's decisions; the trainee observes and communicates)
- **Trainee action** Observe; respond to team requests
- **What the trainee sees** Theatre
- **Information** Event
- **Interpretation** Team management
- **Decision** Communicate?
- **Correct action** Smooth teamwork
- **Incorrect action** Freezing/no communication
- **Missed abnormality** Debrief
- **Escalation** Surgeon/anaesthetist
- **Consequence** Plan update
- **Next step** Sign Out


#### VR reasoning card C.23 — WHO Sign Out

- **Why** Counts, specimen, concerns
- **Trainee action** Participate
- **What the trainee sees** Board
- **Information** Items
- **Interpretation** Complete?
- **Decision** Leave OR?
- **Correct action** Handover
- **Incorrect action** Specimen not read aloud
- **Missed abnormality** Lost specimen
- **Escalation** Team
- **Consequence** Recovery
- **Next step** Recovery


#### VR reasoning card C.24 — Recovery handover & debrief

- **Why** Continuity of care; learning
- **Trainee action** Hand over key concerns
- **What the trainee sees** Recovery bay
- **Information** Summary
- **Interpretation** —
- **Decision** —
- **Correct action** Case closes
- **Incorrect action** Omits antibiotic plan
- **Missed abnormality** Debrief
- **Escalation** Recovery nurse
- **Consequence** End
- **Next step** Debrief

**Scenario maps:** S1 stages 15 Relevant intra-operative training events (ProcSense extension), 16 WHO Sign Out, 17 Recovery / handover; S2 stages 15 Relevant intra-operative training events (ProcSense extension), 16 WHO Sign Out, 17 Recovery / handover.


### Implementation (FYP design)

- **Real workflow** —
- **FYP simulation** Abstract.
- **FYP simplification** No recovery care.
- **AI** P1: debrief rewrite at case end (validated).

- **Room** Theatre → recovery zone.
- **Patient** Anaesthetised → recovering (abstract).
- **Staff** Team + recovery nurse.
- **Equipment** Specimen pot, count sheet, board.
- **Available interactions** Items.

**USER MUST:**

1. Complete Sign Out items.
2. Label specimen.
3. Handover to recovery.
4. Sign Out: procedure name, counts, specimen label read aloud incl. patient name, equipment problems, recovery concerns; SBAR to recovery nurse.
5. P1 extension: observe/communicate during intra-operative training events (conversion, bleeding needing suction, unexpected findings) — surgeon approves events.

**SYSTEM THEN:**

1. Checks counts/specimen/equipment log.
2. Count discrepancy → search, X-ray per local, escalate.
3. Specimen not read aloud → lost-specimen debrief; omitted antibiotic plan at recovery → debrief.

- **If correct →** Done.
- **If incorrect →** Unlabelled specimen → major.
- **If information missing →** —
- **If safety condition fails →** —

| ID | Trigger | Condition | Check | Correct | Incorrect | Feedback | Animation | Next state |
|---|---|---|---|---|---|---|---|---|
| L15-01 | checklist_item(SO) | procedure done | expected | ok | item | — | — | END |

| ID | What went wrong | User sees | Should notice | Correct response | System does | Progression | Feedback | State | Resolution |
|---|---|---|---|---|---|---|---|---|---|
| E15-A | Equipment fault not reported | — | From Step 11 | Report | Minor | — | — | — | — |

| Equipment (WT Part E) | Use S1/S2 | Steps | Information provided | Failure scenario |
|---|---|---|---|---|
| Patient wristband | ✓/✓ | 1, all | Name, DOB, MR number (readable) | Mismatch → stop |
| PPE | ✓/✓ | All | Cue | — |
| Specimen container + label | ✓/✓ | 15 | Label read aloud | Mislabelled specimen |
| Resus trolley + defibrillator + emergency drugs | ✓/✓ | All | Cue | Not checked → delay in emergency |
| WHO checklist board | ✓/✓ | 13–15 | Item status pending/confirmed | Skipped item → stop |
| Recovery bay equipment | ✓/✓ | 15 | Cue | — |

**Owners:** Ruhan — Sign Out + debrief trigger. Sannan — Recovery zone (fade ok). Moiz — Items; [CLINICAL VALIDATION REQUIRED]

**Validation:** Recovery handover elements. [CLINICAL VALIDATION REQUIRED] · Recovery handover items.


# Scenarios, branches and numbers


## 21. Patient & case library

Namespaced IDs (WT-I01): VAR- = Parts 12–13 variants, STK- = Part D full stacked cases, SC- = 14B compact.

| ID | WT label | Patient | Scen. | Key findings | Expected end state | WT ref | Priority |
|---|---|---|---|---|---|---|---|
| VAR-S1-A | Example Patient A / S1-A | Mrs Ayesha Khan, 42 | S1 | Uncomplicated; all normal; water 06:30 teaching point; NEWS2 0; ASA 1 | READY_SAFE | Part 12 p.103–105 | P0 |
| VAR-S1-PB | Example Patient B | Mrs Farzana Ahmed, 61 | S1 | Jaundice; bili 72, ALP 380, GGT 410, ALT 160; CBD 9 mm; ASGE high | POSTPONED (ERCP pathway) | Part 12 p.105–106 | P0 |
| VAR-S1-E | S1-E | Mrs Rabia, 48 | S1 | T 38.4, HR 108, Murphy's +, WBC 15.2, CRP 96, wall 6 mm + fluid → TG18 Grade I | REDIRECTED (acute pathway) | Part 12 p.106 | P0 |
| VAR-S1-B | S1-B (+B2) | Mr Tariq, 58 | S1 | BP 172/104 → 168/102 proceed; B2 188/114, 186/112 postpone | READY_SAFE / POSTPONED | Part 12 p.106–107 | P0 |
| VAR-S1-C | S1-C (+C2) | Mrs Nasreen, 55 | S1 | T2DM metformin+gliclazide; HbA1c 58, CBG 7.8 proceed; C2 HbA1c 82, CBG 13.4, ketones 0.4 postpone | READY_SAFE / POSTPONED | Part 12 p.107 | P0 |
| VAR-S1-D | S1-D (a/b) | Mr Imran, 67 | S1 | AF on apixaban, eGFR 68; (a) 3 days ago proceed; (b) this morning defer | READY_SAFE / POSTPONED | Part 12 p.107 | P0 |
| VAR-S1-G | S1-G | Ms Sana, 39 | S1 | Toast + milky tea 07:00, surgery 09:00 → not safe until 13:00 | Re-timed / POSTPONED | Part 12 p.107 | P0 |
| VAR-S1-H | S1-H | Ms Hina, 29 | S1 | LMP 7 weeks; UPT positive | POSTPONED | Part 12 p.107 | P0 |
| VAR-S1-J | S1-J | Mr Kamran, 50 | S1 | K 6.4 haemolysed, ECG normal → repeat 4.6 | READY_SAFE | Part 12 p.107–108 | P0 |
| VAR-S1-K | S1-K | Mrs Shazia, 52 | S1 | Hb 98, MCV 72 | POSTPONED (investigate) | Part 12 p.108 | P1 |
| VAR-S1-L | S1-L | Mr Aslam, 70 | S1 | New AF HR 128, BP 132/84 | POSTPONED (medical) | Part 12 p.108 | P1 |
| VAR-S1-M | S1-M | Mr Rizwan, 44 | S1 | BMI 41, STOP-BANG 6, SpO2 95 → ASA 3 | Anaesthetist decides | Part 12 p.108 | P1 |
| VAR-S1-I | S1-I | Mrs Amna, 45 | S1 | Penicillin anaphylaxis 2015 | READY_SAFE (allergy flagged) | Part 12 p.108 | P1 |
| VAR-S2-PC | Example Patient C | Mr Hamza Ali, 19 (DOB 02/06/2007) | S2 | Classic; HR 108→96, T 37.9, WBC 13.6, neut 82, CRP 38; AIR 7 / AAS 19; US positive simple | READY_SAFE | Part 13 p.109–111 | P0 |
| VAR-S2-PD | Example Patient D | Mr Usman, 34 | S2 | Deterioration timeline 14:00→15:45 (NEWS2 2→5→10→16); perforation | READY_SAFE if acted at 14:45 / SAFETY_FAILURE | Part 13 p.111–112 | P0 |
| VAR-S2-B | S2-B | Ms Sadia, 24 | S2 | RLQ + shoulder-tip pain, HR 112, BP 98/60, UPT +, Hb 104 → ectopic differential | REDIRECTED (gynaecology) | Part 13 p.112 | P0 |
| VAR-S2-D | S2-D | Ms Zainab, 28 | S2 | AIR 5; US 'appendix not visualised' | Senior: CT or re-score 6–12 h | Part 13 p.112 | P0 |
| VAR-S2-E | S2-E | Mr Akbar, 68 | S2 | Weight loss, Hb 112, CRP 64 → CT first | Pathway by CT | Part 13 p.112 | P1 |
| VAR-S2-F | S2-F | Mr Bilal, 22 | S2 | Biryani 2 h ago → RSI, not cancelled | READY_SAFE | Part 13 p.112 | P0 |
| VAR-S2-G | S2-G | Mrs Rukhsana, 45 | S2 | Vomiting 2 days, HR 116, BP 104/66, urea 12.4, creat 160 (usual 85) AKI 1 | READY_SAFE after resuscitation | Part 13 p.113 | P0 |
| VAR-S2-H | S2-H | Mrs Mehwish, 30 (20 weeks) | S2 | Pregnant; maternity EWS; US then MRI; left tilt | READY_SAFE (obstetric support) | Part 13 p.113 | P1 |
| VAR-S2-I | S2-I | Hamid, 16 | S2 | Minor consent per law | READY_SAFE (valid consent) | Part 13 p.113 | P1 |
| VAR-S2-J | S2-J | Ms Aqsa, 26 | S2 | Dysuria; leucocytes 2+, nitrite −; don't stop at UTI | Continue appendicitis assessment | Part 13 p.113 | P1 |
| VAR-S2-K | S2-K | Mr Faisal, 21 | S2 | Childhood rash to penicillin | READY_SAFE (hospital rule) | Part 13 p.113 | P1 |
| VAR-S2-L | S2-L | Mr Naveed, 37 | S2 | Capacitous refusal; antibiotics-only option | Respect decision; safety-net | Part 13 p.113 | P1 |
| STK-S1-A | STACKED CASE S1-A |  | S1 | Diabetes medication error: hypoglycaemia + | see case §13 | Part D p.163 | P1 |
| STK-S1-B | STACKED CASE S1-B |  | S1 | Anticoagulated AF + iron-deficiency anaemia + anticoagulant taken this morning | see case §13 | Part D p.168 | P1 |
| STK-S1-C | STACKED CASE S1-C |  | S1 | True hyperkalaemia with ECG changes + acute kidney injury + bradycardia/low BP (heart-failure medicines) | see case §13 | Part D p.173 | P1 |
| STK-S1-D | STACKED CASE S1-D |  | S1 | Patient awaiting elective cholecystectomy who develops acute cholangitis | see case §13 | Part D p.177 | P0 |
| STK-S2-A | STACKED CASE S2-A |  | S2 | Appendicitis + dehydration/tachycardia + acute kidney injury + low potassium | see case §13 | Part D p.181 | P1 |
| STK-S2-B | STACKED CASE S2-B |  | S2 | Perforated appendicitis + high-risk sepsis + penicillin anaphylaxis | see case §13 | Part D p.186 | P0 |
| STK-S2-C | STACKED CASE S2-C |  | S2 | Opioid sedation + vomiting + unknown/recent food → airway and aspiration risk with falling SpO2 | see case §13 | Part D p.190 | P1 |
| STK-S2-D | STACKED CASE S2-D |  | S2 | Appendicitis in pregnancy (18 weeks) + physiology that masks severity + imaging limits | see case §13 | Part D p.193 | P1 |

| ID | Scen. | Title | Priority |
|---|---|---|---|
| SC-1 | S2 | Sepsis + penicillin anaphylaxis + AKI | P2 |
| SC-2 | S2 | Positive pregnancy test + lower-right pain + low BP | P2 |
| SC-3 | S2 | Pregnant (20 weeks) + appendicitis + fever | P2 |
| SC-4 | S1 | High BP + poorly controlled diabetes | P2 |
| SC-5 | S1 | On apixaban (taken this morning) + new fast AF | P2 |
| SC-6 | S1 | Fasting breach + diabetes on insulin | P2 |
| SC-7 | S2 | Unknown fasting + vomiting + opioid given + sleepy | P2 |
| SC-8 | S2 | High potassium + AKI + sepsis | P2 |
| SC-9 | S1 | Jaundice + fever + shaking (cholangitis) + on clopidogrel | P2 |
| SC-10 | S2 | Language barrier + 17 years old + needs urgent surgery | P2 |
| SC-11 | S2 | Refuses surgery + becomes septic later | P2 |
| SC-12 | S1 | Haemolysed high potassium + genuinely low sodium + water tablets | P2 |
| SC-13 | S1 | Anaemia + anticoagulant + needs G&S | P2 |
| SC-14 | S2 | Equivocal ultrasound + deteriorating vital signs | P2 |
| SC-15 | S2 | Anaphylaxis to antibiotic + still needs antibiotics | P2 |
| SC-16 | S1 | GLP-1 injection + nausea + high BMI + sleep apnoea | P2 |
| SC-17 | S2 | Sickle cell trait/thalassaemia + dehydration | P2 |
| SC-18 | S1 | Missing consent + equipment fault + late list | P2 |
| SC-19 | S2 | Low sugar + confusion + sepsis in a diabetic on SGLT2 | P2 |
| SC-20 | S2 | Older patient + appendicitis picture + weight loss + anaemia + on aspirin | P2 |


## 22. Stacked problems: method

WT 14B and Part D.

| Ladder | Question | Action |
|---|---|---|
| 1 | Immediately life-threatening? (A, B, C, low sugar, anaphylaxis, NEWS2 7+) | Fix FIRST with ABCDE + call for help — everything else waits |
| 2 | Hard safety blocker? (identity doubt, allergy unknown before a drug, invalid consent, positive pregnancy test with pain) | Resolve before any non-emergency treatment or transfer |
| 3 | Time-critical treatment? (sepsis antibiotics ≤1 h, analgesia, fluids for shock, K 6.5+) | Do it now, in parallel |
| 4 | Finding changes diagnosis/pathway? (ectopic, cholangitis, burst appendix, duct stone) | Senior/specialist; change pathway |
| 5 | Treatments conflict? | Choose safest alternative (clash table); ask senior/anaesthetist |
| 6 | S1 with any fixable problem left? | Senior/anaesthetic review → usually optimise first (defer/re-time/rebook) |
| 7 | Prepare, consent, hand over | SBAR lists EVERY problem and its plan |

| Problem type | Examples | Effect |
|---|---|---|
| LIFE_THREAT | Shock, low O2, anaphylaxis, low sugar, K 6.5+, NEWS2 7+ | Stops everything; emergency mode |
| SAFETY_BLOCKER | Identity doubt, unknown allergy, invalid consent, unknown pregnancy status, unacknowledged critical result, incomplete checklist | Cannot pass checkpoints until resolved |
| TIME_CRITICAL | Sepsis antibiotics, analgesia, fluids for dehydration | Must be done within a clock |
| PATHWAY_CHANGING | Ectopic, cholangitis, perforation, duct stone, new AF, MI | Switches branch/team |
| OPTIMISABLE | BP 180/110+, HbA1c >69, anaemia, salts, anticoagulant timing, fasting | S1 defer/re-time after review; S2 correct in parallel |
| INFORMATION_ONLY | Smoker, mild obesity, controlled AF | Recorded and handed over |

| Part D priority | Meaning |
|---|---|
| P1 | Immediate threat to airway, breathing, circulation (or brain: glucose) |
| P2 | Potentially serious physiological abnormality |
| P3 | Condition needing optimisation before proceeding |
| P4 | Important but non-urgent finding |

- The most dangerous problem sets the pace.
- In S1, problems add up to 'postpone'; in S2 to 'more preparation in parallel'.
- A new diagnosis beats the old plan.
- Every drug: allergy? kidneys/liver? pregnancy? (+ blood thinner for bleeding drugs).
- Clashes are decided by senior/anaesthetist — the trainee must spot them.
- Re-check after every intervention.
- Handover must include ALL problems.

| Clash | Safer approach (draft) | Decided by |
|---|---|---|
| Fluids needed + heart failure / severe CKD | Smaller boluses (e.g. 250 mL) with re-check | Senior/anaesthetist (DOSE-PS) |
| Pain relief + AKI | Paracetamol + titrated opioid (often not morphine); no NSAIDs | DOSE-PS |
| Pain relief + pregnancy | Paracetamol; opioid per senior; no NSAIDs | DOSE-PS |
| Antibiotic + penicillin allergy | Hospital allergy-alternative rule | Microbiology/policy (C) |
| CT + pregnancy | US → MRI; CT only if senior decides | Senior/radiology |
| Contrast CT + AKI | Fluids first; non-contrast/US alternatives | Senior/radiology |
| Contrast + dye allergy | Alternative imaging or pre-medication per radiology | Radiology (C) |
| Antiemetic + long QTc | Alternative antiemetic; fix K/Mg | Anaesthetist |
| Metoclopramide + bowel obstruction | Different antiemetic; NG tube per senior | DOSE-PS |
| Anticoagulant + urgent surgery | Reversal by senior/haematology; blood ready | DOSE-PS |
| Antiplatelets after recent stent | S1 defer; S2 operate on antiplatelets with cardiology | Cardiology + senior |
| Insulin + nil by mouth | Adjust insulin, CBG hourly, glucose fluid or VRIII | DOSE-PS |
| SGLT2 + sepsis/vomiting | Ketones even if glucose near normal | Diabetes team |
| Steroid user + sepsis/surgery | Extra steroid cover | Anaesthetist |
| High K + suxamethonium | Different relaxant; correct K first | Anaesthetist |
| Full stomach + emergency surgery | RSI; don't wait unless safe | Anaesthetist + surgeon |
| Low SpO2 + CO2 retainer | Target 88–92%; blood gas | Senior |
| Confused + consent + emergency | Treat cause; best-interests/emergency route (C) | Senior |


### Full stacked cases (Part D) — structure

Each case has 20 sections: introduction; presentation; observations; history (immediately available vs only revealed if asked); three abnormalities; prioritisation; chronological decision process; interactions; investigations; results; surgery decision; anaesthesia decision; escalation; correct path; incorrect paths (with NPC/monitor reaction and recovery); missed path; recovery path; debrief. Full text: ProcSense_Content/stacked_cases_full.json.

```text
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


## 23. WHAT-IF library (WT Part 14)

47 branches; each has 11 fields in whatif_library.csv.

```text
 ABCDE (RCUK 2021): A airway → B breathing → C circulation → D disability (incl. glucose) → E exposure; fix each problem as found.
```

| ID | Group | What if | Steps | Correct action | Incorrect action | Escalation |
|---|---|---|---|---|---|---|
| WI-01 | 14.1 Vital-sign problems | What if blood pressure is too low? (top number 90 or less) | 3, 8 | Call for help; lie flat; assess airway/breathing and start oxygen per protocol to the patient's target; two drips; request a fluid bolus (e.g. 500 mL crystalloid, NICE CG174; the clinician decides volume) and re-check EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ; treat cause (antibiotics if septic; adrenaline if anaphylaxis; blood if bleeding). | Giving a BP tablet or opioid; sending to scan unmonitored; waiting for the next routine obs | Senior surgeon + anaesthetist immediately; gynaecology if pregnancy positive; critical care if not responding. |
| WI-02 | 14.1 Vital-sign problems | What if blood pressure is extremely high? (180/110 or more) | 3, 8 | No symptoms: S1 → postpone and refer for control (don't rush BP down). With symptoms → emergency medical review. | Rapidly lowering BP on the morning of planned surgery; cancelling after one unrepeated reading | Anaesthetist; medical team if symptoms. |
| WI-03 | 14.1 Vital-sign problems | What if the heart rate is very high? (above 130) | 3, 8 | ABCDE; treat cause (pain relief, fluids, antibiotics); ECG-guided rhythm treatment by senior/medical team. | Treating the number with a rate-slowing drug before finding the cause (e.g. hypovolaemia) | Senior + anaesthetist (NEWS2 3 for this alone = urgent). |
| WI-04 | 14.1 Vital-sign problems | What if the heart rate is very slow? (40 or below) | 3, 8 | ABCDE; stop causative drugs; emergency medicines/pacing per resuscitation team. | Giving more beta-blocker/opioid; proceeding to anaesthesia | Senior + anaesthetist + medical/cardiology urgently. |
| WI-05 | 14.1 Vital-sign problems | What if the patient has a fever? (38.0 °C or more) | 3, 8 | Paracetamol; fluids if dry; antibiotics according to cause; sepsis bundle if criteria met. | S1: proceeding with the planned operation; treating fever only with paracetamol without looking for a source | Senior (in S1 the pathway changes, so yes). |
| WI-06 | 14.1 Vital-sign problems | What if the patient is cold? (36.0 °C or less) | 3, 8 | Warm (forced-air blanket); don't send to theatre until 36.0 unless urgent CLINICAL REFERENCE — SOURCE BACKED (NICE CG65). | Transferring a cold patient for planned surgery | Anaesthetist; senior if septic. |
| WI-07 | 14.1 Vital-sign problems | What if oxygen saturation is low? (below 94%) | 3, 8 | Look at the patient and assess airway/breathing; check probe and waveform; if the low reading is real, sit up and start oxygen per protocol to the patient's target range (94–98%, or 88–92% if at risk of hypercapnic failure); find the cause (stop fluids if overloaded per senior; stimulate if over-sedated — antidote by clinician); reassess within minutes (process in Part 4.5). | Ignoring because 'probe is wrong' without checking; high-flow O2 to a CO2 retainer without monitoring | Anaesthetist/senior urgently; emergency team if 88% or below. |
| WI-08 | 14.1 Vital-sign problems | What if breathing is very fast? (25 or more) | 2, 5, 10 | Treat cause; sepsis bundle if infection. | Giving a sedative to 'calm' the patient | Senior urgently (NEWS2 3). |
| WI-09 | 14.1 Vital-sign problems | What if breathing is very slow? (8 or less) | 3, 8 | Wake/stimulate; oxygen; support airway; naloxone antidote PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; stop further opioids. | Giving further opioid for pain | Emergency/anaesthetist immediately. |
| WI-10 | 14.1 Vital-sign problems | What if the patient becomes confused? | 3, 8 | Treat what's found (sugar below 4 → glucose); sepsis bundle if infection; reassess capacity (consent may be invalid now). | Assuming it is 'just pain medicine' without checking glucose | Senior urgently (NEWS2 3); medical team if stroke signs. |
| WI-11 | 14.1 Vital-sign problems | What if pain gets much worse or the tummy becomes rigid? | 3, 8 | Pain relief; nil by mouth; fluids; antibiotics; prepare for urgent surgery. | Giving more analgesia without re-examining or calling the senior | Senior immediately. |
| WI-12 | 14.2 Blood-test problems | What if potassium is high? (above 5.3) | 7 | 5.5–5.9 (mild, UKKA): ECG, repeat, review drugs. 6.0–6.4: urgent repeat + ECG, treat if real. 6.5+ or ECG changes: emergency treatment (calcium to protect the heart; insulin-glucose) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; heart monitor. | Emergency treatment of an obviously haemolysed sample with normal ECG; ignoring a real 6.5+ | Senior + anaesthetist (6.0+); emergency team if ECG changes. |
| WI-13 | 14.2 Blood-test problems | What if potassium is low? (below 3.5) | 7 | Replace (by mouth if mild; through drip if 2.5–2.9 or vomiting, rate PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ); replace magnesium too. | Fast undiluted IV potassium (dangerous); not checking magnesium | Anaesthetist; senior if below 2.5. |
| WI-14 | 14.2 Blood-test problems | What if sodium is abnormal? | 7 | Correct the cause slowly (no faster than \~8–10 mmol/L per day). | Rapid correction (risk of osmotic demyelination) | Senior + anaesthetist; medical team if below 125. |
| WI-15 | 14.2 Blood-test problems | What if haemoglobin is low? | 7 | S1: investigate before planned surgery; S2: group & save/crossmatch, look for bleeding, transfusion decision if below 70 (80 heart disease). | Proceeding with planned surgery without investigating unexplained anaemia; transfusing a stable patient above threshold | Senior + anaesthetist. |
| WI-16 | 14.2 Blood-test problems | What if the white cell count is high? | 7 | Interpret in context; S1 high WBC = not routine → acute pathway; S2 supports diagnosis; above 18 = moderate gallbladder grade. | Treating the number alone; ignoring it in a planned patient | Senior if S1 or if very high. |
| WI-17 | 14.2 Blood-test problems | What if the white cell count is low? | 7 | Treat as serious if infection present; urgent antibiotics if septic. | Assuming 'no infection' because WBC is low | Senior; haematology if neutrophils below 0.5. |
| WI-18 | 14.2 Blood-test problems | What if creatinine is high? | 7 | Fluids if dry; stop kidney-harming drugs; catheter/urine measurement; repeat in 6–24 h; adjust medicine doses; avoid CT dye unless essential (senior decides). | Giving NSAIDs, contrast or nephrotoxic drugs without senior decision | Senior + anaesthetist; kidney team if stage 2–3. |
| WI-19 | 14.2 Blood-test problems | What if liver tests are abnormal? | 7 | Use the bile-duct-stone risk tool (Part 5.16): high → ERCP pathway; intermediate → MRCP; low → proceed. | Proceeding with planned cholecystectomy despite high-risk duct-stone criteria | Senior (in S1). |
| WI-20 | 14.2 Blood-test problems | What if bilirubin is high? | 7 | Cholangitis signs → emergency; otherwise high-risk pathway (ERCP). | Ignoring fever + jaundice | Senior. |
| WI-21 | 14.2 Blood-test problems | What if blood sugar is low? (below 4.0) | 7 | Treat immediately per hypoglycaemia protocol (fast sugar if can swallow AND not about to have anaesthetic per anaesthetist; else IV glucose) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; recheck every 10–15 min. | Waiting to 'recheck later'; giving oral sugar to a drowsy patient | Anaesthetist (affects fasting); diabetes team. |
| WI-22 | 14.2 Blood-test problems | What if blood sugar is high? (above 12) | 7 | Insulin per protocol (often variable-rate drip) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; fluids if dry. | Ignoring ketones in an SGLT2 user | Anaesthetist; diabetes team. |
| WI-23 | 14.2 Blood-test problems | What if the clotting test (INR) is high? | 7 | S1 usually deferred after review and correct (vitamin K per senior); S2 correct before surgery PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED . | Proceeding with planned surgery; giving neuraxial anaesthesia | Senior + anaesthetist; haematology. |
| WI-24 | 14.2 Blood-test problems | What if lactate is raised? | 7 | Sepsis bundle/fluids; repeat within 2–4 h. | Ignoring because BP is 'normal' | Senior; anaesthetist. |
| WI-25 | 14.3 Heart tracing problems | What if the ECG is abnormal? | 6, 7 | AF: treat triggers, rate control per senior/medical; heart attack: emergency heart pathway. | Relying on the machine's automatic interpretation; proceeding with new AF/ischaemia | Senior + anaesthetist; medical/heart team. |
| WI-26 | 14.4 Pregnancy, allergy and medicine problems | What if the patient is pregnant? | 2, 6, 8 | S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise → gynaecology urgently. Avoid CT/unsafe drugs; ultrasound ok. | Continuing appendicitis pathway; ordering CT first | Senior; gynaecology/obstetrics. |
| WI-27 | 14.4 Pregnancy, allergy and medicine problems | What if pregnancy status is unknown or the patient declines a test? | 2, 6, 8 | Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior + anaesthetist decide whether to proceed. | Testing without consent; skipping documentation | Senior + anaesthetist. |
| WI-28 | 14.4 Pregnancy, allergy and medicine problems | What if the patient is allergic to an antibiotic? | 2, 8 | Record; choose antibiotic by hospital rule (mild rash: cefazolin often allowed; severe: per policy) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . | Giving the drug anyway; recording 'penicillin allergy' without reaction details | Pharmacist/microbiology per policy; anaesthetist. |
| WI-29 | 14.4 Pregnancy, allergy and medicine problems | What if an allergic reaction happens after a medicine? | 2, 8 | STOP the drug; call for help; lie flat (sit up if struggling to breathe); call the emergency team; the responsible clinician gives IM adrenaline (adult standard dose 500 micrograms, repeat after 5 min if no improvement EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ); high-flow oxygen; IV fluid bolus per Resuscitation Council UK (adults 500–1000 mL crystalloid). | Giving antihistamine first instead of adrenaline; continuing the drug | Emergency team + anaesthetist. |
| WI-30 | 14.4 Pregnancy, allergy and medicine problems | What if the patient takes blood thinners? | 2, 8 | S1: proceed only if stopped per plan (Part 8); otherwise postpone; never stop antiplatelets after a recent stent without the heart team. S2: senior/haematology decide reversal PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED . | Proceeding with planned surgery after a morning DOAC dose; stopping DAPT after a recent stent | Senior + anaesthetist; haematology/cardiology as needed. |
| WI-31 | 14.4 Pregnancy, allergy and medicine problems | What if the patient has diabetes? | 2, 8 | Medicine plan (Part 8); first on list if possible; check sugar at least hourly; target 6–10. | Giving usual sulfonylurea while fasting; no CBG monitoring | Anaesthetist; diabetes team if poorly controlled. |
| WI-32 | 14.4 Pregnancy, allergy and medicine problems | What if the patient takes steroids, GLP-1 injections or SGLT2 tablets? | 2, 8 | Per Part 8/9. | Not asking; missing steroid cover | Anaesthetist. |
| WI-33 | 14.5 Fasting problems | What if the patient hasn't fasted? | 2, 5, 10 | Tell anaesthetist honestly. | 'Rounding' the time; proceeding without telling the anaesthetist | Anaesthetist. |
| WI-34 | 14.5 Fasting problems | What if fasting status is unknown? | 2, 5, 10 | Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In. | Recording 'fasted' by assumption | Anaesthetist. |
| WI-35 | 14.6 Scan and result problems | What if imaging is inconclusive? | 7 | Next imaging (CT/MRI/MRCP) or observe and repeat assessment in 6–12 h. | Treating 'not visualised' as negative and discharging | Senior. |
| WI-36 | 14.6 Scan and result problems | What if a test result doesn't make sense? | 7 | Repeat urgently (different tube/arm); treat meanwhile only if dangerous signs. | Acting on an implausible value without repeating; ignoring a dangerous one | Senior if dangerous possibility. |
| WI-37 | 14.6 Scan and result problems | What if a laboratory result is delayed? | 7 | Continue other tasks; repeat sample if lost; S2: don't delay antibiotics/fluids/surgery decision for a non-critical result. | Waiting idle; delaying antibiotics in sepsis for results | Senior if it blocks a decision. |
| WI-38 | 14.6 Scan and result problems | What if a critical result is phoned through? | 7 | Act immediately (ECG, emergency treatment if real); document; acknowledge. | Not reading back; not acting | Senior + anaesthetist. |
| WI-39 | 14.7 Deterioration and sepsis | What if the patient deteriorates? | 3, 8 | Treat as found; increase monitoring. | Waiting for next routine obs; not escalating | NEWS2 5–6: urgent senior. 7+: emergency team. |
| WI-40 | 14.7 Deterioration and sepsis | What if the patient becomes septic? | 3, 8 | Sepsis bundle within 1 hour (oxygen, cultures, antibiotics, fluids, lactate, urine output). | Antibiotics late; no cultures; no fluids | Senior + anaesthetist; critical care if shock. |
| WI-41 | 14.8 Consent and communication problems | What if the patient refuses surgery? | 9 | Explore and correct misunderstandings; offer alternatives (antibiotics-only for simple appendicitis); never pressure; document; safety advice. | Pressuring; operating without consent | Senior. |
| WI-42 | 14.8 Consent and communication problems | What if the patient cannot understand the consent discussion? | 9 | Interpreter; simpler words/diagrams; treat confusion; if no capacity and urgent → senior emergency route per law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . | Using a child relative as interpreter; proceeding without teach-back | Senior; interpreter service. |
| WI-43 | 14.8 Consent and communication problems | What if the patient is under 18? | 9 | Parent/guardian consent; involve the young person. | Accepting the minor's signature alone | Senior. |
| WI-44 | 14.9 System problems | What if equipment fails? | 11 | Report; replace; don't start. | Starting anyway | Theatre lead; anaesthetist. |
| WI-45 | 14.9 System problems | What if documents are missing? | 10 | Re-consent or find; don't transfer. | Transferring without consent | Senior if re-consent needed. |
| WI-46 | 14.9 System problems | What if the patient deteriorates during transfer? | 12 | Stop, oxygen, call for help; return or proceed per stability. | Continuing to theatre without assessment | Emergency team/anaesthetist. |
| WI-47 | 14.9 System problems | What if there's an identity mismatch at theatre? | 12, 13 | STOP; resolve with ward/admissions. | Proceeding 'because it's obviously the right person' | Senior; theatre lead. |


## 24. Clinical numbers and tools (configuration)

Transcribed from WT Parts 4–9, 15, 17. Lab ranges are Type C until aligned with the target laboratory.

| Parameter | Unit | Normal | Watch | Worry | Emergency | Note | Type |
|---|---|---|---|---|---|---|---|
| HR | /min | 51–90 | 91–110 or 41–50 | 111–130 | ≥131 or ≤40 | Display reference 60–100 (AHA); NEWS2 bands used for scoring | A |
| SBP_low | mmHg | 111–219 | 101–110 | 91–100 | ≤90 / MAP <65 | Fall >40 below usual = NG253 high-risk criterion | A |
| BP_high | mmHg | <140/90 | 140–179 / 90–109 | ≥180/110 (S1 usually deferred after review) | ≥180/120 + symptoms | Elective rule AAGBI/BHS 2016 | A |
| RR | /min | 12–20 | 9–11 or 21–24 | ≥22 with infection | ≥25 or ≤8 | Avatar breathing visible; trainee must count | A |
| SpO2 | % | ≥96 | 94–95 | 92–93 | ≤91 (critical <88) | Target 94–98; 88–92 hypercapnic risk (Scale 2) | A |
| Temp | °C | 36.1–37.9 | 35.1–36.0 / 38.0–39.0 | ≥39.1 | ≤35.0 | NEWS2 0 = 36.1–38.0 (display vs scoring differ) | A |
| Alertness | ACVPU | Alert | — | New confusion | Voice/Pain/Unresponsive | Check glucose FIRST if confused | A |
| Pain | /10 | 0–3 | 4–6 | 7–10 | Uncontrolled | Early analgesia correct (WSES 2020) | A |
| CRT | s | ≤2 | 2–3 | >3 | Mottled skin | Adjunct (SSC 2021) | B |
| Urine | mL/kg/h | ≥0.5 | below for <6 h | below for 6 h+ | Almost none | KDIGO | A |
| ShockIndex | HR÷SBP | 0.5–0.7 (≤\~0.9) | 0.8–0.9 | approaching/above 1.0 | no validated higher cut-off | Supporting indicator only (Koch 2019; Rivers 1994) | B |
| NEWS2 | points | 0 | 1–4 | 5–6 or any single 3 | ≥7 | Risk stratification, not diagnosis; not for pregnancy | A |

| NEWS2 parameter | 3 | 2/1 low | 0 | 1/2 high | 3 high |
|---|---|---|---|---|---|
| RR | ≤8 → 3 | 9–11 → 1 | 12–20 → 0 | 21–24 → 2 | ≥25 → 3 |
| SpO2 Scale 1 | ≤91 → 3 | 92–93 → 2 | 94–95 → 1 | ≥96 → 0 |  |
| Supplemental O2 | Yes → 2 |  | No → 0 |  |  |
| SBP | ≤90 → 3 | 91–100 → 2 | 101–110 → 1 | 111–219 → 0 | ≥220 → 3 |
| HR | ≤40 → 3 | 41–50 → 1 | 51–90 → 0 | 91–110 → 1 · 111–130 → 2 | ≥131 → 3 |
| Consciousness |  |  | Alert → 0 |  | C/V/P/U → 3 |
| Temp | ≤35.0 → 3 | 35.1–36.0 → 1 | 36.1–38.0 → 0 | 38.1–39.0 → 1 | ≥39.1 → 2 |

| Score | Risk | Frequency | Response |
|---|---|---|---|
| 0 | Low | ≥12-hourly | Routine |
| 1–4 | Low | ≥4–6-hourly | Nurse decides whether to call doctor |
| Any single 3 | Low–medium | ≥hourly | Urgent ward-doctor review |
| 5–6 | Medium | ≥hourly | Urgent review by doctor skilled in acutely ill |
| ≥7 | High | Continuous | Emergency team incl. critical-care skills |

| NG253 tier | Criteria | Team actions | Reassess |
|---|---|---|---|
| High | Suspected infection AND NEWS2 ≥7, or 5–6 with a single 3 (or confirmed on review) | Senior review; IV antibiotics ≤1 h; fluid bolus ≤1 h (unless contraindicated); consider critical care | NEWS2 every 30 min; escalate if no response in 1 h |
| Moderate | NEWS2 5–6, or 3–4 with a single 3 | Clinician review ≤1 h incl. venous lactate; antibiotics ≤3 h if infection likely | NEWS2 hourly |
| Low | NEWS2 1–4 | Clinical assessment; antibiotics ≤6 h if infection likely | NEWS2 every 4–6 h |
| Very low | NEWS2 0 | Routine unless clinical concern | Standard observations |

| Test | Unit | Normal | Mild | Significant | Severe/critical | Decision rule | Type |
|---|---|---|---|---|---|---|---|
| Haemoglobin | g/L | M 130–170; F 120–150 | M 110–129 / F 110–119 | 80–109 | <80 (critical <70 or fall ≥20) | Unexplained anaemia → investigate before planned surgery (B); transfusion 70 (80 ACS) | A/B |
| WBC | ×10⁹/L | 4.0–11.0 | 11.1–15.0 | 15.1–20.0 | >20 or <4 | Raised in planned patient → reassess for acute infection (B); >18 TG18 moderate | A/B |
| Neutrophils | ×10⁹/L | 2.0–7.5 | — | <1.5 | <0.5 (critical) | Haematology advice <0.5 | A |
| Platelets | ×10⁹/L | 150–400 | 100–149 | 50–99 | <50 (critical <30) | ≥50 most surgery; ≥80 neuraxial (BSH 2017); repeat in citrate tube if clumping | A/B |
| CRP | mg/L | <5 (some labs <10) | 10–49 (WT 5.4: 5–49) | 50–149 | ≥150 (≥300 critical) | Supports diagnosis; not a cancellation value | A |
| Sodium | mmol/L | 135–145 (Harmony 133–146) | 130–134 / 146–150 | 125–129 / 151–155 | <125 / >155 (critical ≤120 / ≥160) | S1 usually deferred if <130 or >150 (B); correct ≤10 in 24 h | A/B |
| Potassium | mmol/L | 3.5–5.3 | 3.0–3.4 / 5.5–5.9 (5.4 = just above reference) | 2.5–2.9 / 6.0–6.4 | <2.5 / ≥6.5 or ECG change | ECG for all hyperkalaemia (UKKA); planned acceptance 3.0–5.5 (B+C) | A/B/C |
| Urea | mmol/L | 2.5–7.8 | 7.9–12 | >12 | — | Interpreted with creatinine | A |
| Creatinine | µmol/L | M 60–110; F 45–90 | ×1.5–1.9 baseline (or +26.5 in 48 h) = AKI 1 | ×2–2.9 = AKI 2 | ×3 or ≥354 = AKI 3 | Needs baseline; S1 usually deferred; S2 correct then proceed | A/B |
| eGFR | mL/min/1.73m² | ≥90 | 60–89 | 30–59 | <30 | Drug-dose changes; NSAID avoidance | A |
| Glucose (diabetic, peri-op) | mmol/L | 6–10 (to 12 OK) | 12.1–14 | >14 (check ketones) | <4.0 (severe <3.0) | Hypo protocol immediately; CBG hourly while fasting | A |
| HbA1c | mmol/mol | <42 | 42–47 / 48–69 | >69 (postpone S1) | — | S1 >69 → optimise (CPOC); S2 proceed + diabetes team | A |
| Ketones | mmol/L | <0.6 | 0.6–1.4 | 1.5–2.9 | ≥3.0 (+acidosis = DKA) | DKA → no planned surgery; SGLT2 euglycaemic DKA | A |
| Bilirubin | µmol/L | 3–21 | 22–30 | 31–68 | >68 | >68 + CBD >6 mm = ASGE high → ERCP before cholecystectomy | A |
| ALT | U/L | ≤40 | 41–120 | 121–400 | >400 | Pattern-based (R ratio) | A |
| ALP | U/L | 30–130 | ≤2× ULN | 2–5× ULN | >5× ULN | Cholestatic pattern → duct imaging | A |
| GGT | U/L | M <60; F <40 | ≤3× ULN | >3× ULN | — | Confirms hepatobiliary ALP | A |
| Albumin | g/L | 35–50 | 30–34 (WT 5.15: low 25–34) | 25–29 | <25 | Risk marker, not cancellation | A/B |
| Lipase/amylase | U/L | \<ULN | 1–3× ULN | >3× ULN (pancreatitis) | — | Pancreatitis → planned list cancelled | A |
| INR (no warfarin) | ratio | 0.8–1.2 | 1.3–1.5 | >1.5 | >2.0 | ≤1.5 general surgery; ≤1.4 neuraxial (B) | A/B |
| APTT ratio | ratio | 0.8–1.2 | — | >1.2 | — | Haematology if unexplained | A |
| Lactate | mmol/L | <2.0 | — | 2.0–3.9 | ≥4.0 | Sepsis pathway; repeat 2–4 h | A |
| β-hCG (serum) | IU/L | <5 | 5–25 (repeat 48 h) | >25 positive | positive + pain/shock | Positive → S1 postponed; S2 ectopic differential → gynaecology | A |
| QTc | ms | M <450; F <460 | prolonged ≥450 M / ≥460 F | — | ≥500 markedly prolonged | No single interval decides surgery | A |
| Mg / adjusted Ca / ferritin | mmol/L / µg/L | 0.7–1.0 / 2.2–2.6 / >30 | — | — | Ca ≤1.8 or ≥3.5 critical | WT 5.26 | A |

| Critical result | Lab phones if |
|---|---|
| Potassium | ≤2.5 or ≥6.5 |
| Sodium | ≤120 or ≥160 |
| Glucose | ≤2.5 |
| Calcium | ≤1.8 or ≥3.5 |
| Creatinine | ≥354 (no known kidney failure) |
| CRP | ≥300 |
| Haemoglobin | <70 g/L or fall ≥20 |
| Platelets | <30 |
| Neutrophils | <0.5 |
| INR | ≥5 on warfarin; >1.5 unexpectedly |
| Lactate | ≥4.0 |

| Rule | Value | Type |
|---|---|---|
| Fasting (planned) | Clear 2 h / milk & light meal 6 h / heavy meal 8 h | A |
| S1 BP postpone | ≥180/110 on repeat (GP target <160/100) | A |
| S1 diabetes | HbA1c >69 mmol/mol (8.5%) → optimise | A |
| Peri-op glucose target | 6–10 (6–12 acceptable) | A |
| Bile-duct stone high risk | Bilirubin >68 + duct >6 mm; or stone seen; or cholangitis | A |
| Gallbladder moderate grade | WBC >18; lump; >72 h; severe local inflammation | A |
| Appendix abnormal width | >6 mm | A |
| AIR bands | 0–4 low, 5–8 unsure, 9–12 high | A |
| AAS bands | 0–10 low, 11–15 intermediate, ≥16 high | A |
| Sepsis antibiotics | ≤1 h if NEWS2 ≥7 (or 5–6 + single 3); ≤3 h if 5–6; ≤6 h if 1–4 (NG253) | A |
| Antibiotic before incision | Within 60 min | A |
| Theatre temperature | ≥36.0 °C | A |
| Sign In blood loss | >500 mL (7 mL/kg children) | A |
| Appendectomy timing | Generally prompt; WSES 2025 lap appendectomy ≤24 h of admission when indicated (context-dependent) | A |
| Acute cholecystitis surgery | Early (ideally ≤72 h; NICE ≤1 week) | A |
| Transfer escort | NEWS2 ≤4 porter+nurse; 5–6 nurse+monitor; ≥7 doctor+nurse+full monitoring | D+C |
| Deterioration clock (untreated S2) | HR +5–10 and NEWS2 +1 about every 30 min | D |
| Turnaround times | FBC 45 min; imaging 60–120 min; others not specified in WT (unresolved) | D |
| Low-SpO2 emergency trigger | SpO2 ≤85% (simulator trigger, not guideline cut-off) | D |

| Intake | Minimum | Note | Type |
|---|---|---|---|
| Clear liquids | 2 h | Water, pulp-free juice, black tea/coffee, fizzy drinks, carbohydrate drinks (some hospitals sips to 1 h — C) | A |
| Breast milk | 4 h | Listed for completeness; adults only in ProcSense | A |
| Milk / formula / tea or coffee WITH milk | 6 h | Counts as food | A |
| Light meal (e.g. toast + clear drink) | 6 h |  | A |
| Heavy/fried/fatty meal, meat (biryani, paratha, nihari) | 8 h+ | Classing these dishes as heavy = B | A/B |
| Chewing gum | Don't cancel | Remove before sleep (ASA 2023) | A |
| Regular tablets | Sip of water | Unless told to stop | A |
| Emergency (S2) | Treat as full stomach → RSI | Not delayed for food when waiting is more dangerous; short wait sometimes (C) | A/C |
| GLP-1 RA | Continue (ProcSense default) | Ask drug + nausea/vomiting/bloating; anaesthetist decides RSI vs delay (B+C) | B/C |

| Test | S1 | S2 | Why |
|---|---|---|---|
| Liver tests (LFTs) | YES (all) | If upper pain or unclear diagnosis | Bile blockage |
| Pregnancy test | If pregnancy possible/uncertain — with consent | If possible — with consent (urgent in abdominal pain) | Diagnosis, imaging, anaesthesia, drugs, timing |
| FBC | If ASA 3+, anaemia symptoms, heart/kidney disease, or local rule | YES | Infection, bleeding, anaemia |
| CRP | Only if inflammation suspected | YES | Inflammation; score |
| U&E | If 65+, diabetes, kidney disease, diuretics/ACE-i, ASA 3+ | YES | Kidneys, K, dehydration |
| Capillary glucose | If diabetic | If diabetic or vomiting | Low/high sugar |
| HbA1c | If diabetic and none in 3 months | No | Diabetes control |
| Clotting (INR/APTT) | If anticoagulant or liver disease | If anticoagulant, septic, liver disease | Bleeding risk |
| Group & save | Per hospital rule | Per rule; always if bleeding suspected | Blood ready |
| Urine dipstick | No (not routine) | YES | UTI/stone look-alikes |
| ECG | Heart/kidney/diabetes, irregular pulse, 65+ (some) | 65+, heart disease, HR >130, irregular, salt problems | Rhythm |
| Lactate + blood cultures | If septic | If sepsis suspected | Sepsis |
| Lipase/amylase | If severe upper/back pain | If diagnosis unclear | Pancreatitis |
| Ultrasound | Already done; repeat if acute | Young/female/pregnant first | Imaging |
| CT | Only for complications | Adults when unclear; older patients | Detailed scan |
| MRCP | If LFTs abnormal / duct wide | No | Duct stones |
| Chest X-ray | No (not routine) | Only breathing problems / suspected perforation | — |


#### Alvarado (0–10)

| Item | Points / rule |
|---|---|
| Migration to RLQ | 1 |
| Anorexia | 1 |
| Nausea/vomiting | 1 |
| RLQ tenderness | 2 |
| Rebound | 1 |
| Temp ≥37.3 | 1 |
| WBC >10 | 2 |
| Neutrophils >75% | 1 |
| Bands | 0–4 unlikely; 5–6 unsure; 7–8 probable; 9–10 very probable |


#### AIR (0–12) — draft default

| Item | Points / rule |
|---|---|
| RLQ pain | 1 |
| Vomiting | 1 |
| Rebound/guarding light/medium/strong | 1/2/3 |
| Temp ≥38.5 | 1 |
| WBC 10.0–14.9 / ≥15 | 1/2 |
| Neutrophils 70–84% / ≥85% | 1/2 |
| CRP 10–49 / ≥50 | 1/2 |
| Bands | 0–4 low; 5–8 unsure; 9–12 high |


#### AAS (0–23) — optional

| Item | Points / rule |
|---|---|
| RLQ pain | 2 |
| Migration | 2 |
| RLQ tenderness: women 16–49 / everyone else | 1/3 |
| Guarding mild / moderate-severe | 2/4 |
| WBC 7.2–10.8 / 10.9–13.9 / ≥14 | 1/2/3 |
| Neutrophils 62–74.9 / 75–82.9 / ≥83% | 2/3/4 |
| CRP pain <24 h: 4–10.9 / 11–24.9 / 25–82.9 / ≥83 | 2/3/5/1 |
| CRP pain ≥24 h: 12–52.9 / 53–151.9 / ≥152 | 2/2/1 |
| Bands | 0–10 low; 11–15 intermediate; ≥16 high |


#### TG18 acute cholecystitis

| Item | Points / rule |
|---|---|
| A local | Murphy's sign; RUQ mass/pain/tenderness |
| B systemic | Fever; raised CRP; raised WBC |
| C imaging | US/CT inflamed GB (wall >4 mm, GB >8×4 cm, pericholecystic fluid, sonographic Murphy's, stone in neck) |
| Diagnosis | Suspected A+B; definite A+B+C |
| Grade III | Organ dysfunction: vasopressors; reduced consciousness; poor oxygenation; oliguria or creatinine >2.0 mg/dL (\~177 µmol/L); INR >1.5; platelets <100 |
| Grade II | WBC >18; tender RUQ mass; >72 h; severe local inflammation |
| Grade I | Neither |


#### TG18 acute cholangitis

| Item | Points / rule |
|---|---|
| A inflammation | Fever >38 and/or rigors; WBC <4 or >10; CRP ≥10 |
| B cholestasis | Bilirubin ≥34 µmol/L (2 mg/dL); LFT >1.5× ULN |
| C imaging | Duct dilatation; cause seen |
| Diagnosis | Definite A+B+C; suspected A+(B or C) → emergency sepsis care + urgent drainage |
| Grade II example (STK-S1-D) | WBC >12 and fever ≥39, no organ failure → early drainage; Grade III urgent |


#### ASGE 2019 duct-stone risk

| Item | Points / rule |
|---|---|
| High | Stone on imaging, or bilirubin >68 + CBD >6 mm, or cholangitis → ERCP pathway |
| Intermediate | Abnormal LFT, age >55, dilated CBD → MRCP/EUS |
| Low | Proceed |
| Note | ESGE omits age; surgeon chooses (C) |


#### ASA (2020)

| Item | Points / rule |
|---|---|
| 1 | Healthy |
| 2 | Mild disease (smoker, pregnancy, BMI 30–40, controlled DM/BP) |
| 3 | Severe disease limiting (poor DM/BP, COPD, BMI 40+, dialysis, MI/stroke >3 months, pacemaker) |
| 4 | Constant threat to life (MI/stroke <3 months, sepsis, severe valve disease) |
| 5 | Not expected to survive without operation |
| 6 | Brain-dead donor |
| E | Emergency (all S2) |
| ProcSense expected | Healthy S1 = 1; controlled BP/DM = 2; poor DM, AF on anticoag, BMI 40+ = 3; healthy S2 = 1E; septic S2 = 2E–4E (B) |


#### Airway / OSA

| Item | Points / rule |
|---|---|
| Mallampati | 1–2 normal; 3–4 concerning |
| Mouth opening | ≥3 cm normal |
| Thyromental | ≥6.5 cm normal; <6 concerning |
| Neck movement | Full vs stiff |
| STOP-BANG | Snoring, Tired, Observed apnoea, Pressure, BMI >35, Age >50, Neck >40 cm, male Gender: 0–2 low; 3–4 intermediate; 5–8 high |


#### SORT (overall risk)

| Item | Points / rule |
|---|---|
| Draft | QS216 requires validated tool; SORT draft choice (C); P-POSSUM / NSQIP alternatives; NELA not for appendectomy |

| Medicine / group | Purpose | Cautions | Timing | Dose policy |
|---|---|---|---|---|
| Paracetamol | First analgesic; antipyretic | Severe liver disease; low weight | Early | DOSE-EX IV SmPC: 1 g over 15 min up to 4×/day, ≥4 h apart, max 4 g/day (>50 kg), 3 g/day with risk factors; <50 kg 15 mg/kg (max 60 mg/kg/day, ≤3 g) |
| NSAIDs | Pain + inflammation | AKI/dehydration, ulcers, anticoagulants, some asthma, pregnancy, heart failure; avoid S2 with AKI | — | DOSE-PS |
| Opioids | Moderate–severe pain | Respiratory depression, drowsiness, nausea; accumulate in kidney failure | Titrated IV; watch breathing | DOSE-PS |
| Antiemetics | Nausea/vomiting | Ondansetron: long QTc; metoclopramide: not with bowel obstruction | When needed | DOSE-EX ondansetron 4 mg slow IV (≥30 s) for PONV; others DOSE-PS |
| IV fluids | Replace losses; resuscitation | Heart/kidney failure (overload) | Bolus vs maintenance | DOSE-EX NICE CG174: crystalloid Na 130–154, 500 mL <15 min, reassess (scope excludes pregnancy, severe liver/kidney, diabetes, burns; HF smaller boluses) else DOSE-PS |
| Antibiotic prophylaxis S1 | Prevent SSI | Not routine low-risk; risk factors: age >70, inflamed GB, jaundice/duct stones, diabetes, ASA 3+, immunosuppression, pregnancy, likely conversion | ≤60 min before incision; redose if long | DOSE-EX cefazolin 2 g IV (3 g ≥120 kg) ASHP 2013; agent per institution (C) |
| Antibiotics S2 | All appendectomies pre-op | Allergy branch | As soon as decided; sepsis per NG253 tier; confirm at Time Out | C (e.g. cefazolin + metronidazole; co-amoxiclav); post-op none if simple, 2–3 days if complicated |
| Warfarin | Anticoagulant | Bleeding | Stop 5 days; INR day before ≤1.5 | Bridging specialist (C) |
| Apixaban/rivaroxaban/edoxaban | DOAC | Bleeding | Stop 1 day (low–mod) / 2 days (high bleeding risk) if kidneys OK | Lap chole bleeding category C (PAUSE) |
| Dabigatran | DOAC | Renal clearance | 1–2 days good kidneys; 2–4 days weaker | — |
| Aspirin | Antiplatelet | — | Usually continue for heart/stroke; may stop 7 days if primary prevention | Surgeon decides |
| Clopidogrel / ticagrelor / prasugrel | Antiplatelet | Recent stent → cardiology; don't stop; defer planned | 5 / 3–5 / 7 days | — |
| Metformin | Diabetes | Kidneys/CT dye (some hospitals) | Normal if one meal missed | C |
| Sulfonylureas (gliclazide) | Diabetes | Hypoglycaemia when fasting | Day of: OMIT | — |
| SGLT2 inhibitors | Diabetes | Euglycaemic DKA | UK 2025: omit day before AND day of (FDA 3 days; ertugliflozin 4) | ProcSense default UK (C); check ketones if unwell |
| GLP-1 RA | Diabetes/weight | Slow gastric emptying | See fasting | Continue + anaesthetist assessment (B+C) |
| DPP-4 inhibitors | Diabetes | Low risk | Day of omit or per local | C |
| Long-acting insulin | Diabetes | Hypoglycaemia while fasting | Reduce \~20% day before and day of | DOSE-PS |
| Other insulin | Diabetes | — | Adjust/omit per protocol; VRIII if >1 meal missed or poor control; Type 1 never stop entirely (A) | DOSE-PS |
| ACE-i / ARB | BP | Hypotension at induction | Often omit 24 h | C |
| Beta-blockers | BP/rate | Don't stop suddenly; not started on day | Continue | — |
| Diuretics | Fluid | Dehydration, low K | Usually omit morning | C |
| Statins / CCB | — | — | Continue | — |
| Steroids (pred ≥5 mg >4 wk) | — | Adrenal crisis | Continue + extra cover if long-term/adrenal insufficiency | DOSE-EX hydrocortisone 100 mg IV at induction (AoA/RCP/SfE 2020); anaesthetist decides |
| COCP / HRT | — | Clot risk | Major surgery may stop 4 weeks (NG89); lap chole usually continue + VTE prophylaxis | C |
| Herbal (ginkgo, garlic, ginseng, fish oil) | — | Bleeding | Often stop 1–2 weeks | C |
| Epilepsy / Parkinson's | — | Fits / stiffness | Continue on time | Specialist if dose missed |
| VTE prophylaxis | Clot prevention | Mechanical not if poor circulation/ulcers/neuropathy | LMWH 6–12 h post-op, ≥7 days abdominal if clot>bleed (NG89) | DOSE-EX enoxaparin 20 mg moderate / 40 mg high / 20 mg CrCl 15–30, ≥7–10 days; timing DOSE-PS |
| Adrenaline (anaphylaxis) | Emergency | — | Immediately | DOSE-EX 500 micrograms IM (0.5 mL 1 mg/mL) anterolateral thigh, repeat after 5 min (RCUK 2021) |
| Glucose (hypo, can swallow) | Emergency | Fasting status matters | Immediately; recheck 10–15 min | DOSE-EX 15–20 g quick carbohydrate (JBDS) |
| Glucose IV (hypo, NBM/drowsy) | Emergency | — | Over \~15 min; recheck 10 min | DOSE-EX 10% 150–200 mL or 20% 75–100 mL (JBDS) |
| Calcium + insulin–glucose (hyperK) | Emergency | ECG changes | Immediately | DOSE-EX 30 mL 10% Ca gluconate over 10 min (or 10 mL 10% CaCl); 10 units soluble insulin in 25 g glucose over 15–30 min; 10% glucose 50 mL/h × 5 h if pre-glucose <7 (UKKA) |
| Naloxone (opioid toxicity) | Emergency | — | — | DOSE-PS |
| Sepsis antibiotics + fluids | Emergency | Allergy | Per NG253 tier | C agent; DOSE-EX 500 mL bolus <15 min |

| Phase | Item | S1 | S2 | If no | Source |
|---|---|---|---|---|---|
| SIGN IN | Has the patient confirmed his/her identity, site, procedure and consent? | Yes | Yes | STOP — resolve | WHO 2009 |
| SIGN IN | Is the site marked? (Yes / Not applicable) | Usually N/A (C) | Usually N/A (C) | Ask surgeon | WHO 2009 |
| SIGN IN | Is the anaesthesia machine and medication check complete? | Yes | Yes | STOP — complete check | WHO 2009 |
| SIGN IN | Is the pulse oximeter on the patient and functioning? | Yes | Yes | STOP — replace | WHO 2009 |
| SIGN IN | Does the patient have a known allergy? (No / Yes) | No/Yes + what | No/Yes + what | Prepare alternatives | WHO 2009 |
| SIGN IN | Difficult airway or aspiration risk? (No / Yes, and equipment/assistance available) | Usually no | Yes — aspiration; RSI equipment, suction, assistance | Plan, don't stop | WHO 2009 |
| SIGN IN | Risk of >500 mL blood loss (7 mL/kg in children)? (No / Yes, and two IVs/central access and fluids planned) | Usually no | Usually no; yes if bleeding/coagulopathy | Two IVs + fluids/blood | WHO 2009 |
| TIME OUT | Confirm all team members have introduced themselves by name and role | Yes | Yes | — | WHO 2009 |
| TIME OUT | Confirm the patient's name, procedure, and where the incision will be made | Yes | Yes | — | WHO 2009 |
| TIME OUT | Has antibiotic prophylaxis been given within the last 60 minutes? (Yes / Not applicable) | Only if indicated, else N/A | Yes — time confirmed; redose decision | Fix, repeat item | WHO 2009 |
| TIME OUT | To surgeon: critical/non-routine steps? case duration? anticipated blood loss? | \~60 min; minimal | \~45–60 min; minimal | — | WHO 2009 |
| TIME OUT | To anaesthetist: any patient-specific concerns? | As relevant (OSA, diabetes, aspiration) | Aspiration, sepsis, kidneys | — | WHO 2009 |
| TIME OUT | To nursing team: sterility (incl. indicator results) confirmed? equipment issues or concerns? | Tower working | Lap + open sets | Indicator fail → stop | WHO 2009 |
| TIME OUT | Is essential imaging displayed? (Yes / Not applicable) | US ± MRCP | US/CT | Display, repeat item | WHO 2009 |
| SIGN OUT | Nurse verbally confirms the name of the procedure | — | — | — | WHO 2009 |
| SIGN OUT | Nurse verbally confirms completion of instrument, sponge and needle counts | — | — | Search, X-ray per local, escalate | WHO 2009 |
| SIGN OUT | Nurse verbally confirms specimen labelling (read labels aloud incl. patient name) | Gallbladder | Appendix | — | WHO 2009 |
| SIGN OUT | Nurse verbally confirms whether there are equipment problems to be addressed | — | — | — | WHO 2009 |
| SIGN OUT | Surgeon, anaesthetist and nurse: key concerns for recovery and management | — | — | — | WHO 2009 |
| EXTENSION | VTE prophylaxis confirmed |  |  |  | ProcSense educational extension (NICE NG89; NPSA 2010) |
| EXTENSION | Capillary glucose recorded for patients with diabetes |  |  |  | ProcSense educational extension (CPOC) |
| EXTENSION | Patient temperature ≥36.0 °C before induction |  |  |  | ProcSense educational extension (NICE CG65) |
| EXTENSION | Pregnancy status documented where relevant |  |  |  | ProcSense educational extension (NICE NG45) |
| EXTENSION | Team brief before the list / debrief after |  |  |  | ProcSense educational extension (NPSA 2010) |

| Role | Behaviour |
|---|---|
| Ward / ED nurse | Vitals, gives medicines, checklists; reports numbers; asks decisions; CHALLENGES unsafe orders |
| Triage nurse (S2) | First summary and urgency |
| Senior surgeon | Phone/arrival; confirms diagnosis and decision to operate; leads deferral |
| Anaesthetist | Fitness, ASA confirmation, airway/fasting, may delay; leads Sign In |
| Gynaecologist / obstetric team | Positive pregnancy with pain; pregnant patients |
| Laboratory staff | Phone critical results |
| Radiologist / sonographer | Scan reports; queries CT in pregnancy |
| Porter | Moves patient |
| Theatre reception nurse | Identity, consent, allergies, fasting, site |
| Scrub nurse / ODP / circulating | Theatre prep, counts, sterility |
| Recovery nurse | Recovery handover (Step 15) |
| Pharmacist / microbiology | Allergy-alternative antibiotics |
| Gastroenterology / ERCP team | Cholangitis drainage |
| Medical / cardiology team | New AF, STEMI, hyperkalaemia emergencies |
| Critical care / emergency team | NEWS2 ≥7, shock |
| Diabetes team | Hypo/DKA/poor control |
| Interpreter (trained) | Language barrier; not child relatives |
| Relative / parent / guardian | Minor consent; history |
| Admissions | Identity mismatch |


# Architecture, AI, team and schedule


## 25. System architecture

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


## 26. AI architecture — controlled AI around a deterministic core

ProcSense is NOT an LLM pretending to be a patient. It is a deterministic clinical simulation platform enhanced by multiple controlled AI systems for natural interaction, adaptive behaviour, assessment, scenario composition, synthetic-data generation and feedback.

| Owner | Scope |
|---|---|
| Deterministic engine OWNS | Clinical state · simulation state · physiology · rules · events · consequences · scoring · assessment facts · scenario validity |
| AI OWNS / ASSISTS WITH | Natural language · speech interpretation · behavioural expression · scenario proposals · case composition within approved constraints · communication interpretation · adaptive presentation · staff/patient dialogue · synthetic-data generation · debrief language · development-time content generation |


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

| Component | Type | Responsibility | Where it runs | Fallback |
|---|---|---|---|---|
| 1 LLM | LLM | Patient dialogue, staff dialogue, scenario proposal (Stage A), debrief wording, controlled content drafting | Server GPU (P1); offline drafting on dev PC | Pre-rendered lines / templates |
| 2 ASR | ML model | Trainee speech → text | Server (P1); on-device spike P2 | Tablet menu |
| 3 TTS | ML model | Patient, staff, system voices | Offline pre-render (P0); live (P1) | Subtitles only |
| 4 Intent/communication classifier | LLM-based (P1) → small classifier (P2) | Communication acts, topics, SBAR elements, evidence spans | Server (async) | Debrief-only / menu evidence |
| 5 Embeddings/retrieval | ML model (optional) | Retrieve approved content for grounding and debrief evidence | Server (P2) | Rule explain text |
| 6 Scenario Composer | DETERMINISTIC solver (+ optional LLM proposal) | Constraint-based complete case composition | Offline tool | Hand-authored cases |
| 7 Rule engine | MUST BE DETERMINISTIC | Clinical/simulation correctness | Quest | n/a |
| 8 Physiology engine | MUST BE DETERMINISTIC | Physiological behaviour | Quest | LitePhysio |
| 9 Event Director | MUST BE DETERMINISTIC | Approved event selection | Quest | n/a |
| 10 Adaptive Difficulty Engine | DETERMINISTIC selection (+ LLM explanation P1) | Case/difficulty selection from approved pool | Quest (selection) | Fixed curriculum order |
| 11 Grounding/Validation layer | MUST BE DETERMINISTIC | Validate every AI output (schema, entities, disclosure, intents) | Quest + server | Reject → fallback |
| 12 AI Audit Logger | Deterministic | Model, version, prompt version, input context ID, output, validation result, latency, failure/fallback | Server + on-device log | Local log |


## 27. AI Authority and Safety Boundaries

| AI MAY | AI MAY NOT |
|---|---|
| Generate natural-language responses | Directly modify authoritative patient state |
| Interpret trainee language | Directly modify physiological state |
| Classify communication | Directly modify scores |
| Propose scenarios (Stage A) | Create unapproved clinical facts at runtime |
| Select from approved content | Introduce arbitrary medications/treatments |
| Generate natural variations | Invent investigation results |
| Generate debrief wording | Invent diagnoses |
| Generate development/test data | Override deterministic clinical rules |
| Adapt presentation/difficulty within approved limits | Bypass validation |
|  | Make assessment decisions outside defined scoring rules |
|  | Load unapproved clinical content into assessment mode |

**How the boundary is enforced:**

- AI clients receive read-only DTOs; no API mutates Core state
- All AI output is JSON-schema constrained and passes the grounding/validation layer
- Behavioural intents are applied only through the approved transition table (bounded)
- Scores come only from deterministic rules; AI evidence counts only above threshold and never for critical items
- Loader refuses unapproved content in assessment mode; AI-proposed cases stop at IMPLEMENTED
- Every AI call audited; fallback always available


## 28. AI-assisted complete case composition (Scenario Composer)

The composer builds COMPLETE simulation cases from approved building blocks — not just narratives. The **structured patient card is authoritative**; the AI narrative is only a presentation layer.

```text
 Approved clinical building blocks → constraint/compatibility checking → Scenario Composer → contradiction detection
   → clinical-rule validation → approved complete patient case → deterministic simulation engine
```

| Building block | ID family | Compatibility constraints |
|---|---|---|
| Demographics / age band / sex (where clinically relevant) | DEM-* | age band ↔ comorbidity prevalence; sex ↔ pregnancy eligibility |
| Surgical scenario | SCN-S1 / SCN-S2 | defines allowed diagnoses, tests, OR set |
| Presenting complaint + HPC | PRES-* | must match diagnosis block |
| Past medical history / comorbidities | CMB-* | requires/excludes tags; drives tests (NG45) |
| Previous surgeries | PSH-* | e.g. PSH-APPENDECTOMY excludes SCN-S2 |
| Allergies (+ reaction class) | ALG-* | excludes treatment options of that class |
| Current medications | MED-* | each requires its indication comorbidity |
| Family / social history | SOC-* | day-case eligibility, anaesthetic family history |
| Baseline observations envelope | OBS-* | consistent with severity + physiology |
| Clinical/exam findings | FND-* | consistent with diagnosis and severity |
| Risk factors | RSK-* | consistent with comorbidities |
| Laboratory findings | LAB-* | pattern ↔ diagnosis; values within reviewed bands |
| Imaging findings/reports | IMG-* | ↔ diagnosis; adult S2 text only |
| Pre-operative concerns | CON-* | patient questions pool |
| Communication/personality profile | PER-* | anxiety, health literacy, cooperation, question frequency |
| Pain/discomfort state | PAIN-* | ↔ diagnosis & severity |
| Difficulty level | DIFF-1..3 | limits number/complexity of blocks + events |
| Approved complications/events | EVT-* | eligibility rules must be satisfiable |
| Environment conditions | ENV-* | rooms available, time of day, interruptions |
| Staff availability | STAFF-* | porter delay, anaesthetist availability |
| Time/workflow constraints | TIME-* | OT slot, urgency deadlines (S2) |


### Contradiction detection

| Contradiction | Example | Action |
|---|---|---|
| Incompatible demographics | Pregnancy-positive block with male sex or outside age band | Reject |
| Inconsistent history | PSH-APPENDECTOMY in an S2 appendicitis case; PSH-CHOLECYSTECTOMY in S1 | Reject |
| Impossible investigation combination | US-GALLSTONES-NORMAL-DUCT together with MRCP-CBD-STONE and normal LFT without explanation | Reject / flag [CLINICAL VALIDATION REQUIRED] |
| Allergy vs medication | ALG-PENICILLIN with MED-AMOXICILLIN current; beta-lactam antibiotic option pre-selected | Reject |
| Findings vs diagnosis | Right-upper-quadrant findings in an appendicitis case with no alternative explanation | Reject |
| Impossible physiological state | SpO2 envelope below survivable range with 'calm, alert' persona; HR/BP pair outside physiology model | Reject |
| Inconsistent severity | S2 'uncomplicated' severity with perforation imaging + sepsis observations | Reject |
| Inconsistent surgical indication | Asymptomatic gallstones in normal gallbladder listed for elective surgery (CG188 1.2.1) without symptoms block | Reject [CLINICAL VALIDATION REQUIRED] |
| Results vs clinical state | Normal WBC/CRP with 'sepsis progression' event and fever envelope | Reject / flag |
| Ineligible events | E-GLUCOSE-LOW selected for a non-diabetic; E-HYPERSENSITIVITY with no allergy block | Reject |
| Medication without indication | MED-METFORMIN without CMB-T2DM | Reject |
| Disclosure impossibility | Hidden fact whose disclose_on topic does not exist in the Ask menu | Reject |


### AI-generated scenarios are complete scenarios (two-stage)

**Stage A — AI proposal:** the LLM proposes a full configuration (surgical case, profile, complaint, history, risk factors, allergies, medications, baseline state, investigation requirements/results, personality, communication behaviour, difficulty, staff and environment configuration, eligible events, expected competency opportunities) using ONLY block IDs from the approved library.

**Stage B — deterministic validation:**

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

> **Assessment mode** UNAPPROVED AI-GENERATED SCENARIOS MUST NEVER DIRECTLY ENTER RUNTIME. Only reviewer-approved cases enter the approved pool.


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


## 29. AI-assisted assessment across the whole workflow

Communication is one of many assessed behaviours. The LLM never decides clinical correctness: deterministic rules define expected behaviour; AI only interprets natural language into structured evidence.

```text
 Trainee speech ─► ASR (transcript + timings + confidence)
   ─► Intent / communication classifier (acts, topic, SBAR element, evidence span, confidence)
   ─► Evidence mapper (deterministic): confidence ≥ threshold? act allowed in this context? critical item? → Evidence record
   ─► Deterministic competency/scoring rules (decide correctness)  ─► Score item (with evidence refs)
 Trainee VR/tablet action ─► ActionEmitter ─► Core ─► rule hits ─► Score item   (deterministic evidence)
 Critical items (identity, allergy, consent, handover critical elements) always require deterministic evidence.
```

| Behaviour assessed | Steps | Deterministic evidence | AI-interpreted evidence | Deciding rule |
|---|---|---|---|---|
| Asks relevant questions | 2 | Ask menu topics / speech→topic | Classifier maps question→topic | Required-topic rules per patient |
| Identifies important patient information | 2,7 | Facts disclosed + acknowledged on tablet | — | Fact-acknowledged rules |
| Clarifies unclear information | 2,7 | 'Clarify' act; follow-up question on vague answer | Classifier: CLARIFY act | Vague-answer follow-up rule |
| Responds to patient concerns/questions | 2,9 | answer_question(q,a) | Classifier: ANSWER_QUESTION + topic match | Pending question must be answered within window |
| Explains procedures/tests | 6,9 | EXPLAIN_* acts | Classifier: EXPLAIN_* + topic | Explain-before-action rule |
| Explains before performing an interaction | 3–4,6,8 | say(EXPLAIN) precedes action in log | Classifier | Order rule (explain → act) |
| Checks understanding | 9 | CHECK_UNDERSTANDING act | Classifier (teach-back detection) | Consent rule element |
| Responds appropriately to anxiety | 2,8,9 | ACKNOWLEDGE_EMOTION/REASSURE after anxiety cue | Classifier; inappropriate-reassurance flag (debrief) | Response-window rule on anxiety cue |
| Notices clinically relevant info from patient | 2,3–4 | Acknowledge fact / action taken | Classifier detects acknowledgement in speech | Disclosed-fact follow-up rules (e.g. intake → inform anaesthetist) |
| Communicates with nurses/staff | 6,8,11 | call_staff, closed-loop confirm | Classifier: CLOSED_LOOP | Order read-back rule |
| Communicates during handover/transfer | 12,15 | SBAR form / speech | Classifier maps utterances→SBAR elements | Required-element + critical-omission rules |
| Gives appropriate instructions | 8,10,12 | Order/instruction actions | Voice-order parser | Instruction-validity rules (formulary, state) |
| Performs required checks | 1,8,10,13,14 | Check actions | — | Required-check rules |
| Responds to abnormal findings | 3–4,7 | interpret + decision | — | Finding→action rules |
| Responds to unexpected events | 3–4,6,8,11,12 | Response actions within window | Speech escalation via classifier | Event response rules |
| Recognises need for more information | 5,7 | order test / ask / chase | — | Missing-information rules |
| Follows workflow & sequence | all | Action order in log | — | Order/hard-edge rules |
| Notices environmental/safety problems | 11,13 | report_fault, stop | Speech 'report' via classifier | Fault-response rules |
| Responds when information is missing | 2,6,10 | Clarify/escalate | Classifier | Missing-info rules |
| Escalates / seeks assistance | 3–4,7,8 | escalate(role, SBAR) | Classifier SBAR completeness (non-critical) | Escalation-trigger rules |
| Completes preparation & safety checks | 8–14 | Checklist + treatment actions | — | Readiness blockers |
| Responds to changing patient/environment state | 3–4,8,12 | Actions after state change | — | Time-window rules |


## 30. State-aware AI & reactive loop

AI components receive a READ-ONLY, disclosure-aware view of the simulation. AI output is interpreted by deterministic systems; it never changes truth directly.

| Context field | Meaning | Given to |
|---|---|---|
| current_stage / phase | Workflow position | Patient, staff |
| completed_actions / missing_actions | What has / hasn't happened | Staff (e.g. nurse asks 'is consent done?') |
| disclosed_facts | Facts the trainee has already learned | Patient, staff, debrief |
| disclosable_facts (+ disclose_on) | Facts the character may reveal if asked | Patient only |
| told_to_patient | What the trainee has explained to the patient | Patient (avoids asking already-answered questions) |
| behaviour_state | Anxiety, trust, understanding, pain, fear, frustration, confusion … | Patient |
| observable_physiology | Only perceivable signs (pale, sweating), not raw truth unless on monitor | Patient, staff |
| active_events | Approved events currently running + their communication intents | Patient, staff |
| conversation_context | Last N turns + previous patient responses | Patient, staff |
| environment_state | Location, interruptions, equipment faults | Staff |
| staff_state | Availability, pending requests | Staff |
| revealed_results | Results released + acknowledged | Staff, debrief |
| difficulty | Current tier (style only) | Patient, staff |

```text
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


## 31. Patient persona & behaviour realisation

| Attribute | Range | Driven by |
|---|---|---|
| Anxiety | 0–10 | Explanation/reassurance ↓; unexplained actions, waits ↑ |
| Trust | 0–10 | Introduction, honesty ↑; dismissive, ignoring questions ↓ |
| Cooperation | 0–10 | Trust/understanding ↑; dismissive ↓ → may refuse cannulation until reassured |
| Understanding | 0–10 | Plain language, teach-back ↑; jargon ↓ |
| Communication style | enum (talkative, terse, anxious-questioning, stoic) | Persona constant |
| Pain/discomfort | 0–10 | Driven by physiology + analgesia |
| Fear | 0–10 | Consent risks discussed without support ↑; reassurance ↓ |
| Frustration | 0–10 | Repeated questions, long waits ↑ |
| Confusion | 0–3 | Physiology-driven (hypoglycaemia/hypoxia/sepsis) + jargon |
| Willingness to answer | 0–10 | Trust-gated disclosure of sensitive facts |
| Question frequency | persona constant | How often the patient initiates questions |
| Need for reassurance | persona constant | Scales anxiety response to events |
| Communication difficulty | enum (language preference, hearing) | Educationally appropriate only; [CLINICAL VALIDATION REQUIRED] |

| Trainee behaviour / event | Bounded transition (approved table) |
|---|---|
| Appropriate explanation before action | understanding +1, trust +1 |
| Poor communication / jargon | understanding −1, confusion ↑ (if already low) |
| Repeated unexplained actions (×2) | anxiety +1 → patient asks 'What are you doing?' (question intent) |
| Dismissive reply to a question | trust −2, cooperation −1 |
| Reassurance + clear explanation | anxiety −1…−2 (diminishing), fear −1 |
| Long unexplained wait | anxiety +1, frustration +1 per interval |
| Approved event fires (e.g. anaphylaxis) | fear +2, patient expresses symptom via AI realisation |
| Analgesia effect | pain tracks physiology; irritability ↓ |

> **Bounds** Magnitudes are simulation design values reviewed for plausibility. [CLINICAL VALIDATION REQUIRED] The LLM realises a state in words; it cannot create clinical consequences.


## 32. AI staff & environment interaction

| Role | Approved intents (AI phrases only these) |
|---|---|
| Nurse | ASK_CLARIFICATION, CONFIRM_ORDER (read-back), REPORT_OBSERVATION, ASK_PREP_STATUS, REPORT_PATIENT_REQUEST (e.g. water) |
| Anaesthesia staff | ASK_FASTING_ALLERGY_AIRWAY, RAISE_APPROVED_CONCERN, RESPOND_TO_ESCALATION, SIGN_IN_ANSWER |
| Surgeon | REQUEST_UPDATE, DECISION_RESPONSE (approved), TIME_OUT_ANSWER, APPROVED_INTRAOP_ISSUE (narration) |
| Porter | CONFIRM_TRANSFER, REPORT_DELAY, ASK_MONITORING |
| Lab/imaging staff | REPORT_APPROVED_RESULT, REPORT_SAMPLE_REJECTED, REPORT_DELAY |
| Scrub/circulating nurse | REPORT_MISSING_ITEM, REPORT_FAULT, CONFIRM_READY, COUNT_STATUS |
| Ward/reception staff (if required) | CONFIRM_ADMISSION_DETAILS, REPORT_BED_STATUS |

Staff interactions also produce assessment evidence: e.g. trainee handover → ASR → classifier extracts SBAR elements → deterministic evaluator checks required elements → scorer records evidence.


## 33. AI + unexpected events

```text
 Approved Event Library → eligibility rules → Event Director → selected event → deterministic state change
   → AI Behaviour Layer → patient/staff natural-language response (approved event communication intents + approved facts only)
```

AI never creates emergencies at runtime; it only decides HOW an approved event is voiced. Assessment mode uses a fixed event schedule, so runs stay reproducible.


## 34. Adaptive difficulty

**Inputs (structured evidence from previous sessions):**

- Competency performance (per category)
- Communication performance (deterministic + validated classifier evidence)
- Workflow completion
- Error patterns (repeated missed checks)
- Response to unexpected events (time-to-response)
- Time/efficiency
- Handover quality
- Recognising missing information
- Responding to patient concerns

| Difficulty dimension | How it is realised (approved variants only) |
|---|---|
| More complex history | More comorbidity/medication blocks (approved combos) |
| More communication challenges | Personas with low literacy, high anxiety, terse style |
| More competing priorities | Parallel tasks (porter delay + results + patient question) |
| More environmental interruptions | ENV/STAFF blocks: phone, theatre calls for the patient (WT Step 12) |
| More information requiring clarification | More hidden facts, inconsistent chart vs patient |
| Approved unexpected events | Higher event count/tier |
| More complex handover | More required elements (abnormal results, uncertainty) |
| Higher cognitive workload | Tighter (reviewed) time constraints |

The engine selects approved variants inside the clinically approved scenario space; it never invents conditions. Disabled in assessment mode.


## 35. AI debrief from session evidence

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

```text
 Session Log ─► Deterministic Scorer ─► Structured Debrief Facts (item IDs, verdicts, evidence refs, sources)
   ─► AI Debrief Generator (LLM, JSON: sections referencing fact IDs only) ─► Grounding Validator
        (every fact ID exists · every number/verdict preserved · no new events/mistakes · no treatment advice beyond rule text)
   ─► Final debrief   |  fail → template debrief (P0)
```


## 36. General synthetic-data pipeline

Synthetic data never silently becomes clinical truth: every dataset has a card and a runtime/dev label.

| Dataset | Purpose | Generator | Source data | Method | Constraints | Validation | Human/clinical review | Labels | Intended model/use | Runtime? | Dev only? |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Patient profiles / histories / case variants | Runtime cases after review | Composer (solver) + LLM narrative | Approved blocks | Constraint solving | requires/excludes, contradiction rules | Stage B + reviewer | YES (100% for assessment) | — | Scenario library | Runtime (approved only) | — |
| Investigation-result combinations | Result variety | Rule-based sampler | Reviewed bands + local ranges | Sampling within bands | Pattern ↔ diagnosis | Range + consistency tests | YES | finding codes | ResultGenerator | Runtime (approved) | — |
| Imaging-report variants | Report text | LLM drafting tool | Finding codes, SRC-18/19/20 facts | Constrained generation | Entities ⊆ finding codes | Entity check + reviewer edit | YES | — | Results viewer | Runtime (approved) | — |
| Patient questions & responses | Dialogue pools | LLM drafting tool | Scenario context | Constrained generation | Only approved topics | Validator + surgeon answers | YES (answers) | topic tags | Patient dialogue | Runtime (approved) | — |
| Staff dialogue variants | Natural variation | LLM drafting tool | Approved intents | Paraphrase | Intent preserved | Validator + spot check | Light | intent tag | Staff dialogue / TTS | Runtime (approved) | — |
| Handover examples (good/poor) | Classifier + eval | LLM + team role-play | SBAR element lists | Generate with controlled omissions | Known omissions | Human labels | Labels by team | SBAR element labels | Classifier | No | Dev only |
| Trainee questions/explanations/communication examples | Classifier training/eval | LLM + role-play | Act taxonomy | Generate per act | Balanced classes | 2 labellers, kappa | No (not clinical) | act labels | Classifier | No | Dev only |
| Miscommunication examples | Negative cases | LLM | Taxonomy INAPPROPRIATE | Generate | — | Human labels | No | labels | Classifier | No | Dev only |
| Behavioural responses | Test behaviour transitions | Rule-based | Transition table | Enumerate | Bounds | Unit tests | Magnitudes [CLINICAL VALIDATION REQUIRED] | — | Behaviour model tests | No | Dev only |
| Event combinations | Event Director testing | Rule-based enumerator | Event library | Combinatorial | Eligibility | Golden runs | YES (for approved combos) | — | Event Director | Runtime (approved) | — |
| Environmental scenarios | Environment variety | Composer | ENV/STAFF/TIME blocks | Constraint solving | Plausibility | Stage B | Light | — | Scenarios | Runtime (approved) | — |
| Difficulty variants | Adaptive difficulty pool | Composer | Difficulty dimensions | Constraint solving | Tier limits | Stage B + reviewer | YES | tier label | Difficulty engine | Runtime (approved) | — |
| Structured assessment examples | Scorer/debrief testing | Scripted bots | Rubric | Golden playthroughs | Expected ScoreReports | Tests | Rubric weights [CLINICAL VALIDATION REQUIRED] | expected items | Scorer, debrief | No | Dev only |
| Physiological trajectories | Physiology realism checks | Statistical fit | VitalDB (CC BY 4.0) | Envelope + AR(1) noise | Model bounds | Plot review | YES (plots) | — | LitePhysio | Runtime (parameters) | — |
| Edge cases | Robustness | Rule-based + LLM | Templates | Boundary values | — | Tests | No | — | All validators | No | Dev only |
| Contradiction cases / invalid scenarios | Validator testing | Mutation generator | Valid cases | Inject contradictions | Known defects | Must be rejected | No | defect label | Stage B validator | No | Dev only |
| Adversarial dialogue questions | Hallucination testing | LLM + team | Patient cards | Generate off-card questions | — | Automated grounding check | No | expected refusal | Dialogue eval | No | Dev only |
| Speech samples (voice orders, accents) | ASR evaluation | TTS + consented recordings | Order phrases | Render/record | — | WER | No | transcripts | ASR | No | Dev only |


## 37. AI requirements matrix

| Feature | Owner | Purpose | Runtime/Dev | AI/Non-AI | Input | Output | Authoritative source | Validation | Clinical review | Offline | Assessment mode? | Fallback | Priority | Definition of Done |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Deterministic rule engine | R | Correctness | Runtime | Non-AI | State+action | Rule hits | Rules (reviewed) | Vectors+golden | YES | YES | YES | n/a | P0 | All rules tested |
| Scenario Composer (solver) | M | Complete valid cases | Dev | Non-AI | Blocks, template | Case card | Card + blocks | Stage B | YES | YES | Approved only | Hand-authored cases | P0 | ≥8 S1/≥6 S2 approved cases |
| Contradiction detector | M | Reject impossible cases | Dev | Non-AI | Case card | Pass/defects | Rule list | Mutation tests | Rules YES | YES | n/a | n/a | P0 | All mutation cases rejected |
| AI scenario proposal (Stage A) | R | Propose diverse cases | Dev | AI (LLM) | Library + targets | Candidate card | Stage B + reviewer | Stage B | YES | Dev PC | Only after approval | Solver sampling | P1 | ≥20 candidates, 0 invalid pass Stage B |
| Structured patient card | M | Authoritative case | Runtime | Non-AI | Composer output | Card JSON | Card | Schema | YES | YES | YES | n/a | P0 | Loader strict mode |
| Narrative presentation | R | Readable story | Dev→Runtime | AI (LLM) | Card | Text | Card | Entity check | Light | YES (pre-made) | Approved only | Template text | P1 | 0 entity violations |
| Patient Behaviour Model | R | Bounded behaviour | Runtime | Non-AI | Actions, events | Attributes/modes | Transition table | Unit tests | Magnitudes | YES | YES | n/a | P0 | Transitions tested |
| State-aware AI context (read-only DTO) | R | Responsive AI | Runtime | Non-AI | Core state | DTO | Core | Read-only tests | No | YES | YES | n/a | P1 | No mutation path; disclosure respected |
| Behaviour realisation (AI) | R | Natural expression | Runtime | AI (LLM) | DTO + intent | Utterance + intent | Behaviour Model | Grounding+intent validator | Light | Server | YES (validated) | Pre-rendered line | P1 | 0 invented facts on eval |
| Patient dialogue (AI) | R | Natural answers | Runtime | AI (LLM) | DTO + question | Utterance | Card | Grounding | Light | Server | YES | Pre-rendered | P1 | 0 invented facts; p95 ≤2.5 s |
| Staff/environment AI | R | Natural staff | Runtime | AI (LLM) | Intent + DTO | Utterance | StaffDef intents | Intent validator | Lines light | Server | YES | Pre-rendered | P1 | No non-approved instructions |
| ASR | R | Speech→text | Runtime | AI | Audio | Transcript | — | WER eval | No | Server | YES (with confirm) | Menu | P1 | Order accuracy ≥95% after read-back |
| Intent/communication classifier | R (M: dataset/eval) | Evidence from speech | Runtime | AI | Transcript | Acts+spans+conf | Rules decide | F1/kappa | Taxonomy YES | Server | Non-critical only | Debrief-only | P1 | Meets thresholds or debrief-only |
| Workflow-behaviour assessment (evidence mapper) | M | Assess whole workflow | Runtime | Non-AI (uses AI evidence) | Actions + evidence | Score items | Rules | Golden runs | YES | YES | YES | Deterministic only | P0 | All assessment rows mapped |
| Event Director + AI communication | R | Approved events, natural cues | Runtime | Non-AI + AI phrasing | State | Event + utterance | Event library | Eligibility + grounding | YES | YES | YES (fixed schedule) | Pre-rendered cues | P0 (+P1 phrasing) | Events reproducible |
| Adaptive Difficulty Engine | M | Right challenge | Runtime | Non-AI | Session evidence | Next case | Approved pool | Bounds tests | Pool YES | YES | NO | Fixed order | P0 | Selects only approved |
| Difficulty explanation | M | Transparency | Runtime | AI | Ratings + choice | Text | Engine | Validator | No | Server | NO | Template | P1 | Validator passes |
| AI debrief | R | Readable feedback | Runtime | AI | Debrief facts | Narrative | Scorer | Grounding | Light | Server | YES | Template debrief | P1 | 0 validator failures /50 |
| Synthetic-data pipeline | M | Dev/eval data | Dev | AI + Non-AI | Blocks, templates | Datasets + cards | Dataset cards | Per dataset | Per dataset | Dev PC | Only approved runtime sets | — | P1 | Every dataset has a card |
| Embeddings/retrieval | R | Grounded explanations | Runtime | AI | Query | Passages | Approved own-words corpus | Precision@3 | Corpus YES | Server | Debrief only | Rule explain | P2 | — |
| Grounding/validation layer | R | Guard every AI output | Runtime | Non-AI | AI output + context | Pass/fail | Content lexicons | Unit + adversarial | No | YES | YES | Fallback | P0 | 100% of AI calls validated |
| AI audit logger | M | Traceability | Runtime | Non-AI | AI calls | Audit records | — | Completeness test | No | YES (local) | YES | Local log | P1 | Every call logged |
| Offline TTS voice pipeline | M | Voiced lines offline | Dev | AI | Approved lines | Audio | Lines | Listening | No | YES | YES | Subtitles | P0 | All lines voiced |


## 38. Implementation order & dependencies

| # | Stage of implementation | Depends on | Weeks | Owner |
|---|---|---|---|---|
| 1 | Clinical content foundation (stages, sources, blocks) | — | W1–3 | M |
| 2 | Deterministic ProcSense.Core kernel | — | W1–2 | R |
| 3 | Patient state model (6 layers) | 2 | W2 | R |
| 4 | Workflow engine (case graph, decision points, blockers) | 2,3 | W2–3 | R |
| 5 | Rule engine + provenance | 2,1 | W2–3 | M |
| 6 | Investigation subsystem + transport | 4,5 | W3–4 | M |
| 7 | Medication/IV/fluids framework | 4,5 | W4 | M |
| 8 | Equipment/OR/environment simulation | 4 | W4–6 | S |
| 9 | Physiology (LitePhysio; Pulse gate) | 3 | W2–6 | R |
| 10 | Event Director + event library | 5,9 | W6–7 | R |
| 11 | Scoring + evidence mapper | 5,12 | W4–6 | M |
| 12 | Session logging | 2 | W2 | R |
| 13 | Scenario Composer (solver) | 1,3,5 | W5–6 | M |
| 14 | Constraint/contradiction validation (Stage B) | 13 | W5–6 | M |
| 15 | Patient Behaviour Model | 3,4 | W6 | R |
| 16 | ASR | 12 | W7–8 | R |
| 17 | Communication/intent classifier | 16,11 | W8–9 | R+M |
| 18 | Patient/staff AI dialogue + grounding | 15,24 | W8 | R |
| 19 | AI state-aware reaction layer | 18,15,10 | W8–9 | R |
| 20 | Adaptive Difficulty Engine | 11,13 | W9 | M |
| 21 | AI-assisted scenario generation (Stage A) | 13,14,18 | W9 | R |
| 22 | Synthetic-data pipeline | 13,18 | W7–10 | M |
| 23 | AI debrief | 11,24 | W10 | R |
| 24 | Grounding/validation + audit + eval suite (starts early, before any AI ships) | 2 | W6→W10 | R (validator) + M (audit, eval) |
| 25 | S1 integration | 1–15 | W5 (P0) / W9 (AI) | ALL |
| 26 | S2 integration | 25 | W9 | ALL |
| 27 | Offline/Quest performance testing | 25,26 | W10–12 | S+R |
| 28 | Clinical validation | 25,26 | W11 | M (coord) + ALL |
| 29 | Assessment-mode freeze | 28 | W11–12 | ALL |
| 30 | Final release | 29 | W13–14 | ALL |


## 39. End-to-end AI example — S1

| Step | What happens | AI component participating |
|---|---|---|
| Scenario configuration | Composer selected S1-C017 (approved card) for DIFF-2 | Adaptive Difficulty Engine (selection), Composer (earlier, offline) |
| Patient state | Core loads card; anxiety 6, trust 4; hidden intake + allergy | — |
| Trainee enters room | Nurse intent INTRODUCE_PATIENT | Staff AI phrases approved intent → validator → TTS |
| Patient speaks | Behaviour mode ANXIOUS → intent GREET_ANXIOUS | Behaviour realisation (LLM) with read-only DTO |
| Trainee asks 'Have you had anything to eat or drink today?' | Speech | ASR → classifier: ASK_CLOSED, topic=last_intake (conf 0.94) |
| Deterministic evaluation | ask(last_intake) emitted; disclosure rule reveals tea 3 h; R-FAST-01 breach | Evidence mapper (deterministic) |
| Patient behaviour update | Trainee explains why → understanding +1 (transition table) | Classifier EXPLAIN_PURPOSE → Behaviour Model applies bounded delta |
| Investigation request | Orders HbA1c/U&E/LFT/ECG (tablet) | — |
| Simulated investigation / result | ResultGenerator → LAB-HBA1C-HIGH | Report text pre-drafted offline (reviewed) |
| Trainee responds | Acknowledges; decision per local plan | — |
| Staff interaction | Trainee: 'Please hold her metformin' → nurse read-back | ASR → voice-order parser → staff AI CONFIRM_ORDER → trainee confirms |
| Preparation | IV insertion | — |
| Approved unexpected event | Advanced mode: allergen given → Event Director fires E-ANAPHYLAXIS (WT 14.4) | Deterministic |
| Deterministic state change | HR↓ BP↓ effect; fear +2 | Physiology + Behaviour Model |
| AI patient/staff reaction | Patient intent EXPRESS_BREATHLESSNESS; nurse intent REPORT_OBSERVATION | LLM phrasing from approved event communication intents |
| Trainee response | Lies patient flat, reassures, attaches monitor | Classifier REASSURE (non-critical evidence) |
| Handover | Spoken SBAR to anaesthetist | ASR → classifier maps SBAR elements → trainee confirms on tablet → deterministic element check |
| Scoring | Scorer builds items with evidence refs | Deterministic |
| AI debrief | Debrief facts → LLM narrative → grounding validator | AI debrief + audit logger |


## 40. End-to-end AI example — S2

| Step | What happens | AI component participating |
|---|---|---|
| Scenario configuration | Composer case S2-C004 (DIFF-3: uncertain fasting, CRP delay, theatre-calls) | Adaptive Difficulty Engine picked tier 3 from evidence (handover strong, events slow) |
| Patient state | Pain 7, anxiety 7, dehydration driver | — |
| Trainee enters ED cubicle | Nurse intent REPORT_TRIAGE | Staff AI phrasing |
| Patient speaks | Mode IN_PAIN → short answers | Behaviour realisation (terse, pain) |
| Trainee asks about pain and offers analgesia | Speech | ASR → classifier ASK_OPEN (SOCRATES), OFFER_ANALGESIA |
| Deterministic evaluation | Analgesia timing goal met after tablet prescription + checks | Evidence mapper |
| Patient behaviour update | Pain ↓ via physiology; cooperation +1 | Behaviour Model |
| Investigation request | Urgent bloods, pregnancy test, US with monitored transfer | Porter AI CONFIRM_TRANSFER |
| Result returned | CRP delayed event; lab AI REPORT_DELAY | Staff AI |
| Trainee responds | Proceeds; escalates to surgeon by speech | Classifier SBAR elements (non-critical clarity) |
| Approved unexpected event | E-THEATRE-CALLS (fixed schedule in assessment) | Event Director |
| AI staff reaction | OT coordinator intent REQUEST_PATIENT_NOW | Staff AI |
| Trainee response | 'Consent and handover outstanding, ETA 10 min' | Classifier detects STATE_OUTSTANDING + ETA → deterministic response rule |
| Handover | Includes fasting uncertainty (critical — confirmed on tablet) | ASR + classifier + deterministic critical check |
| Scoring / AI debrief | Items + competencies → narrative | Scorer + AI debrief + validator + audit |


## 41. AI tests, risks and Definition of Done

| Test ID | Area | Scenario | Expected behaviour | Outcome | Pass |
|---|---|---|---|---|---|
| T-AI-01 | Composer | Inject each contradiction type (12) | All rejected by Stage B | No invalid case reaches pool | 12/12 rejected |
| T-AI-02 | Composer | AI Stage A proposes 50 cases | Stage B filters; valid ones stop at IMPLEMENTED | No auto-approval | 0 auto-approved |
| T-AI-03 | State-aware AI | Attempt write via AI client | No API exists; compile/test fails | Truth protected | No mutation path |
| T-AI-04 | State-aware AI | Ask about a fact not yet disclosable | Patient deflects; validator blocks leak | Hidden info preserved | 0 leaks /100 |
| T-AI-05 | Behaviour | AI suggests intent outside table | Rejected; no state change | Bounded behaviour | Rejected |
| T-AI-06 | Staff AI | Prompt nurse to give a drug instruction | Only approved intents; validator rejects | No invented instructions | 0 violations |
| T-AI-07 | Assessment | Critical item said only by speech | Not credited until deterministic confirm | Critical items deterministic | Correct |
| T-AI-08 | Events | Event fires; AI phrases it | Only approved facts in utterance | Safe reproducible events | Grounded |
| T-AI-09 | Difficulty | Strong trainee | Next case from approved pool only | No invented conditions | Pool membership |
| T-AI-10 | Debrief | Mutate a fact ID/number | Validator rejects → template | No invented mistakes | Rejected |
| T-AI-11 | Synthetic data | Dataset without card/labels | Pipeline refuses | Traceable data | Refused |
| T-AI-12 | Offline | Server off | All AI paths fall back | Simulation continues | Complete run |
| T-AI-13 | Assessment mode | Load unapproved AI case | Loader refuses | Assessment integrity | Refused |

| Risk | Level | Mitigation |
|---|---|---|
| AI-composed case contains a contradiction | Medium | Stage B detector + mutation tests + reviewer approval before pool |
| AI leaks hidden information | Medium | Disclosure-aware DTO + validator + T-AI-04 |
| Behaviour drift (AI escalates emotions) | Low | Intents only; bounded transition table |
| Synthetic data treated as truth | Low | Dataset cards, synthetic flags, dev-only labels |
| State-aware context adds latency | Medium | Compact DTO; pre-rendered fallback ≤2.5 s |
| Classifier evidence over-trusted | Medium | Threshold, non-critical only, human-labelled eval |

- Scenario Composer produces ≥8 S1 / ≥6 S2 approved complete cases; contradiction detector passes all mutation tests (P0)
- Assessment evidence mapper covers every row of the workflow-assessment table with deterministic rules (P0)
- Behaviour Model with bounded transitions; AI realisation validated (P0 model / P1 realisation)
- Grounding/validation layer wraps 100% of AI calls; audit logger records every call (P0 / P1)
- Staff AI, state-aware reaction layer, ASR, classifier, AI debrief, Stage A scenario proposal meet their evaluation thresholds or fall back (P1)
- Every synthetic dataset has a dataset card (purpose, generator, source, validation, labels, runtime/dev) (P1)
- Adaptive difficulty selects only approved variants; disabled in assessment mode (P0)


## 42. Team & responsibilities

Original ownership kept; new components added.

| Person | Role | Strengths | Why this ownership |
|---|---|---|---|
| Ruhan | Simulation core + main AI | AI/LLM + automation (internship); HPC course and GPU optimisation; owns the two GPU machines (RTX 4050 laptop, GTX 1660 Super desktop) | The hardest AI constraint is making models fit and run fast on 6 GB GPUs, which is exactly HPC/GPU optimisation work. LLM/automation internship experience fits prompt design, grounding, model serving and the classifier. Core, physiology and Event Director are performance-sensitive simulation code. GPU-heavy AI stays where the hardware is. |
| Sannan | VR, 3D and the app | Blender 3D (personal project); app development (course) | Blender covers custom equipment models, patient blend shapes and staff rigs, which nobody else can do quickly. App-dev covers Unity UI, Android/Quest builds and client-side networking (voice client). None of this needs a strong GPU. |
| Moiz | Content, data, evaluation + supporting AI | Some AI (internship); app development (course) | Internship AI exposure fits data-centred AI: synthetic datasets, labelling, evaluation, and light rule-plus-AI pieces (voice-order parser, difficulty engine). These run via APIs or CPU, so his lighter GPU is not a limit. App-dev fits backend, loaders, audit logger and tooling. Composer, validators and scoring are structured-data logic that sits naturally with the schema owner. |

| Person | Original ownership (unchanged) | New components added |
|---|---|---|
| Ruhan | Core kernel · case graph & decision points · patient state · LitePhysio/Pulse · Patient Behaviour Model · Event Director · readiness/checklist engines | LLM setup + benchmark + GPU optimisation · grounding/validation layer · patient dialogue · staff/environment AI · state-aware reaction layer & behaviour realisation · speech recognition service · intent/communication classifier (model, prompts, thresholds) · AI debrief · Stage A scenario proposal |
| Sannan | Unity project · XR rig · SimulationHost bridge · environment import · all interactables · avatars & animation · all in-VR UI · Quest builds & profiling | Blender custom equipment models · patient blend shapes/shaders + behaviour-mode animation mapping · staff avatar rigs · voice client (push-to-talk, fallback, subtitles, lip sync) · debrief & difficulty-explanation UI |
| Moiz | Schemas · content loader & validators · investigation & treatment systems · result generator · rules & provenance · scoring engine + rubrics · backend/session sync · source register · doctor coordination | Scenario Composer solver + case cards · Stage B validator + contradiction detector · assessment evidence mapper · synthetic-data pipeline + dataset cards · AI eval suite · classifier labelled dataset & taxonomy · adaptive difficulty engine + explanation · voice-order parser & read-back · offline voice pipeline · AI audit logger |

| Person | Added for the walkthrough (v6) |
|---|---|
| Ruhan | + B-R17 Decision & priority engine; + B-R18 Early-warning & sepsis engine |
| Sannan | + B-S12 Theatre extension scenes (P1) |
| Moiz | + B-M18 Walkthrough content pipeline & traceability; + B-M19 Case library; + B-M20 Clinical calculators |

| From | To | What passes |
|---|---|---|
| Ruhan — Stage A proposals | Moiz — Stage B validator | Candidate case JSON (block IDs only) |
| Ruhan — classifier output | Moiz — evidence mapper → scoring | {acts, topic, span, confidence} |
| Moiz — synthetic data + eval suite | Ruhan — dialogue, classifier, debrief | Datasets + pass/fail reports |
| Ruhan — behaviour intents/modes | Sannan — animations/blend shapes | Behaviour mode + intensity |
| Ruhan — AI endpoints | Sannan — voice client | HTTP/JSON + audio |
| Moiz — voice-order parser | Ruhan — staff AI (read-back phrasing) | Parsed order JSON |
| Moiz — offline voice pipeline | Sannan — audio catalogue in Unity | Vorbis files + line index |
| Moiz — difficulty engine | Sannan — menu UI | Next case + explanation text |

| Build guide | Component | Owner | Priority | Weeks | v6 status |
|---|---|---|---|---|---|
| B-R11 | Core kernel (SimClock, ActionAPI, EventBus, Session, deterministic RNG) | Ruhan | P0 | W1–W2 | Rewritten/expanded for walkthrough |
| B-R12 | Case graph, decision points & readiness blockers | Ruhan | P0 | W2–W3 | Rewritten/expanded for walkthrough |
| B-R13 | LitePhysio engine + Pulse gate | Ruhan | P0 (Lite) / P1 (Pulse) | W1–W6 | Rewritten/expanded for walkthrough |
| B-R14 | Event Director + event library runtime | Ruhan | P0 | W6–W7 | Rewritten/expanded for walkthrough |
| B-R15 | SafetyChecklist & OR readiness engines (Sign In / Time Out / Sign Out) | Ruhan | P0 (SI/TO) / P1 (SO) | W5–W6 | Rewritten/expanded for walkthrough |
| B-R16 | Location & transport system | Ruhan | P0 | W3–W4 | Rewritten/expanded for walkthrough |
| B-R1 | LLM setup, model benchmark & GPU optimisation | Ruhan | P1 | W6–W7 | Rewritten/expanded for walkthrough |
| B-R2 | Grounding / validation layer | Ruhan | P0 | W6 (before any AI ships) | Rewritten/expanded for walkthrough |
| B-R3 | Patient dialogue (state-aware) | Ruhan | P1 | W8 | Rewritten/expanded for walkthrough |
| B-R4 | Staff & environment AI | Ruhan | P1 | W8 | Rewritten/expanded for walkthrough |
| B-R5 | State-aware reaction layer & behaviour realisation | Ruhan | P1 (DTO P0) | W6 (DTO) / W8–W9 (AI) | Reviewed — purpose/traceability updated |
| B-R6 | Patient Behaviour Model (deterministic, expanded) | Ruhan | P0 | W6 | Rewritten/expanded for walkthrough |
| B-R7 | Speech recognition service | Ruhan | P1 | W7–W8 | Reviewed — purpose/traceability updated |
| B-R8 | Intent / communication classifier (model side) | Ruhan | P1 | W8–W9 | Reviewed — purpose/traceability updated |
| B-R9 | AI debrief | Ruhan | P1 | W10 | Rewritten/expanded for walkthrough |
| B-R10 | Stage A scenario proposal | Ruhan | P1 | W9 | Rewritten/expanded for walkthrough |
| B-R17 | Decision & priority engine (problems, ladder, clashes, roles) | Ruhan | P0 | W4–W7 | NEW (v6) |
| B-R18 | Early-warning & sepsis engine (NEWS2, NG253 tiers, timers) | Ruhan | P0 | W3–W6 | NEW (v6) |
| B-S6 | Unity project setup, XR rig & locomotion | Sannan | P0 | W1–W2 | Reviewed — purpose/traceability updated |
| B-S7 | SimulationHost bridge, ActionEmitters, StateViews & SceneDirector | Sannan | P0 | W2–W4 | Reviewed — purpose/traceability updated |
| B-S8 | Environment import, optimisation & room anchors | Sannan | P0 | W1–W3 | Rewritten/expanded for walkthrough |
| B-S9 | Interaction framework & equipment state machines | Sannan | P0 | W3–W7 | Rewritten/expanded for walkthrough |
| B-S10 | In-VR UI framework (tablet, monitor, results viewer, checklist boards) | Sannan | P0 | W3–W6 | Rewritten/expanded for walkthrough |
| B-S11 | Quest builds, profiling & Unity CI | Sannan | P0 | W2 → W14 | Reviewed — purpose/traceability updated |
| B-S1 | Blender custom equipment models | Sannan | P0 | W3–W7 | Rewritten/expanded for walkthrough |
| B-S2 | Patient blend shapes, shaders & behaviour animation mapping | Sannan | P0 | W5–W8 | Rewritten/expanded for walkthrough |
| B-S3 | Staff avatar rigs | Sannan | P0 | W6–W7 | Reviewed — purpose/traceability updated |
| B-S4 | Voice client (push-to-talk, fallback, subtitles, lip sync) | Sannan | P1 (subtitles/lip sync P0) | W8 | Reviewed — purpose/traceability updated |
| B-S5 | Debrief & difficulty-explanation UI | Sannan | P0 (template) / P1 (AI text) | W10 | Rewritten/expanded for walkthrough |
| B-S12 | Theatre extension scenes: intra-op events, Sign Out, recovery (P1) | Sannan | P1 | W9–W11 | NEW (v6) |
| B-M11 | Schemas, content loader, validators & packaging | Moiz | P0 | W1–W2 | Rewritten/expanded for walkthrough |
| B-M12 | Rule evaluator (C# + Python twin) & provenance | Moiz | P0 | W2–W3 | Rewritten/expanded for walkthrough |
| B-M13 | Investigation system & result generator | Moiz | P0 | W3–W5 | Rewritten/expanded for walkthrough |
| B-M14 | Treatment system & required checks | Moiz | P0 | W4–W5 | Rewritten/expanded for walkthrough |
| B-M15 | Scoring engine, rubrics & template debrief (DebriefFacts) | Moiz | P0 | W4–W10 | Rewritten/expanded for walkthrough |
| B-M16 | Backend session sync & reports | Moiz | P1 | W8–W9 | Reviewed — purpose/traceability updated |
| B-M17 | Source register, clinical question log & doctor review packs | Moiz | P0 | W1 → W11 | Rewritten/expanded for walkthrough |
| B-M1 | Scenario Composer solver + case cards | Moiz | P0 | W5–W6 | Rewritten/expanded for walkthrough |
| B-M2 | Stage B validator + contradiction detector | Moiz | P0 | W5–W6 | Rewritten/expanded for walkthrough |
| B-M3 | Assessment evidence mapper | Moiz | P0 (deterministic) / P1 (AI evidence) | W6 / W9 | Rewritten/expanded for walkthrough |
| B-M4 | Synthetic-data pipeline + dataset cards | Moiz | P1 | W7–W10 | Reviewed — purpose/traceability updated |
| B-M5 | AI eval suite | Moiz | P1 | W6 skeleton → W10 | Reviewed — purpose/traceability updated |
| B-M6 | Classifier taxonomy & labelled dataset | Moiz | P1 | W7–W8 | Reviewed — purpose/traceability updated |
| B-M7 | Adaptive difficulty engine + explanation | Moiz | P0 (selection) / P1 (explanation) | W9 | Rewritten/expanded for walkthrough |
| B-M8 | Voice-order parser & read-back flow | Moiz | P1 | W8 | Reviewed — purpose/traceability updated |
| B-M9 | Offline voice pipeline | Moiz | P0 | W4–W6 | Reviewed — purpose/traceability updated |
| B-M10 | AI audit logger | Moiz | P1 | W7 | Reviewed — purpose/traceability updated |
| B-M18 | Walkthrough content pipeline & traceability | Moiz | P0 | W2–W3 (then continuous) | NEW (v6) |
| B-M19 | Case library: patients, stacked cases and what-if bindings | Moiz | P0 (P0 set) / P1 (rest) | W3–W9 | NEW (v6) |
| B-M20 | Clinical calculators: scores and risk tools | Moiz | P0 | W4–W6 | NEW (v6) |


## 43. Schedule (starts Tue 29 Sep 2026)

Gate dates unchanged; scope tiered to fit.

| Week | Work | Milestones |
|---|---|---|
| W1 Tue 29 Sep–Sun 4 Oct | Kickoff; Core kernel; Unity project + XR rig; schemas v1 (done per original plan) | — |
| W2 5–11 Oct | Walkthrough integration: content package + traceability (B-M18); 15-step model; Step 1 on Quest | GATE 1 Fri 9 Oct: Step 1 playable on Quest |
| W3 12–18 Oct | Steps 2–4 (history with hidden facts, vitals/NEWS2, examination); NEWS2/sepsis engine (B-R18) start; calculators (B-M20) start | CRG-1 Wed 14 Oct: Steps 1–7 + institutional decisions 1, 4, 5, 6, 10, 11 · GATE 2 Fri 16 Oct: Steps 1–4 + ordering headless |
| W4 19–25 Oct | Steps 5–8: lab realism + critical calls (B-M13), medicine request workflow (B-M14), decision engine (B-R17) start | — |
| W5 26 Oct–1 Nov | Steps 9–14 for S1; WHO exact wording; ward checklist; theatre stations | CRG-2 Wed 28 Oct: Steps 8–15 + decisions 2, 3, 8, 9, 12 · GATE 3 Fri 30 Oct: S1 Steps 1–14 end-to-end with VAR-S1-A and VAR-S1-PB |
| W6 2–8 Nov | Problem registry, ladder, clashes; deterioration clocks; S1 P0 variants B, C, D, E, G, H, J | GATE 4 Fri 6 Nov: physiology + decision engine |
| W7 9–15 Nov | What-if library (P0-bound branches), event visuals; STK-S1-D | CRG-3 Wed 11 Nov: what-ifs, stacked cases, S2 P0 · GATE 5 Fri 13 Nov: all S1 P0 cases |
| W8 16–22 Nov | S2 build (overlap, triage, early analgesia); AI P1 work | GATE 6 Fri 20 Nov: AI keep/cut + Step 15/extension keep/cut |
| W9 23–29 Nov | S2 P0 cases incl. Usman deterioration timeline and STK-S2-B | CRG-4 Wed 25 Nov · GATE 7 Fri 27 Nov: S2 end-to-end |
| W10 30 Nov–6 Dec | End states, timeline debrief, scoring weights; regression | GATE 8 Fri 4 Dec: FEATURE FREEZE |
| W11 7–13 Dec | Clinical validation playthroughs (Part I checklist A–G); content-only P1 cases if validated | CRG-5 Wed 9 Dec · GATE 9 Fri 11 Dec |
| W12 14–20 Dec | Performance, full test pass | GATE 10 Wed 16 Dec · GATE 11 Fri 18 Dec: CODE FREEZE |
| W13 21–27 Dec | rc1 Mon 21; rehearsal Wed 23 | GATE 12 Sat 26 Dec: v1.0 |
| W14 28–31 Dec | Clean-machine test Tue 29; sign-off Wed 30 | SUBMIT Thu 31 Dec |

| Schedule realism | Detail |
|---|---|
| Scope added by the walkthrough | 15 steps × 21 questions; 25 variants; 8 full + 20 compact stacked cases; 47 what-ifs; 2 × 20-stage maps; 50 build guides (was 44). |
| Honest assessment | Building every case and branch as P0 by 4 Dec is not realistic for three students. The schedule structure and gates are kept, but scope is tiered. |
| Adjustment 1 — data-driven engine | Cases, what-ifs and stacked cases are CONTENT, not code. Once the engine supports a content type, extra cases are additions to the content package. |
| Adjustment 2 — P0 case set | 17 cases: VAR-S1-A, -PB, -B(+B2), -C(+C2), -D(a/b), -E, -G, -H, -J; VAR-S2-PC, -PD, -B, -D, -F, -G; STK-S1-D; STK-S2-B. |
| Adjustment 3 — P1/P2 | P1: remaining 10 variants + 6 stacked cases (content-only after freeze if validated and no new code needed); Step 15 + intra-op extension (decided at Gate 6). P2: SC-1..SC-20. |
| Adjustment 4 — what-ifs | P0 = what-ifs bound to P0 cases; the rest are loaded and testable but not polished. |
| Adjustment 5 — clinician time | CRG sessions now include the 15 institutional decisions; packs limited to ≤45 min by prioritising Type C and P0 items. |

| Risk | Level | Mitigation |
|---|---|---|
| Clinical review bottleneck (more content) | High | Prioritise Type C + P0 items; 15 decisions spread over CRG-1/2; unapproved content disabled in assessment mode |
| Scope growth from walkthrough | High | Tiered P0/P1/P2; data-driven cases; cut lines at Gates 3, 5, 6 |
| Walkthrough inconsistencies (WT-I01..12) | Medium | issues_flagged.csv; supervisor confirms; namespaced IDs |
| Content drift between walkthrough and code | Medium | B-M18 regenerates package; CI traceability check |
| Type D values mistaken for medicine | Medium | Labelled in content and UI; clinician realism review (decision 15) |
| Quest performance | Medium | Budgets + weekly profiling |
| AI hallucination / dose output | Medium | Validator dose-policy check; AI P1 only |
| Team availability | Medium | Pairing, secondary owners, Friday rebalance |


## 44. Clinical validation (WT Part I)

| # | Institutional decision | Owner |
|---|---|---|
| 1 | Laboratory reference ranges, units and critical-value list | Hospital laboratory |
| 2 | Antibiotic protocol: prophylaxis agent/dose/redose; appendicitis regimen; penicillin-allergy alternative (incl. cefazolin after anaphylaxis) | Microbiology / surgeon |
| 3 | Consent/legal workflow: form, witness, age/guardian, mature minors, emergency treatment, family involvement | Institution legal / surgeon |
| 4 | Fasting policy details (clear 2 h vs 1 h; GLP-1 and SGLT2 policy choice) | Anaesthesia department |
| 5 | Local anaesthetic protocols (insulin, steroid cover, K acceptance 3.0–5.5 and INR limits as defaults) | Anaesthesia department |
| 6 | Emergency escalation: early-warning tool, responders, sepsis tool | Hospital |
| 7 | Equipment availability (MRCP, ERCP, CT, gastric ultrasound, capnography, warming) | Hospital |
| 8 | Documentation: ward checklist, handover form, WHO version/additions, site marking | Theatre governance |
| 9 | Blood-bank procedure (G&S/crossmatch, labelling) | Transfusion lab |
| 10 | Patient identification policy (identifiers, band colours) | Hospital |
| 11 | Transfer escort/monitoring rule | Hospital |
| 12 | Post-operative/day-case discharge criteria | Surgeon / anaesthetist |
| 13 | Choice of appendicitis score (AIR default vs AAS) and imaging pathway for high-score young patients | Surgeon |
| 14 | Unit-specific complication rates for consent | Surgeon |
| 15 | Realism of simulator design values (deterioration clocks, turnaround times) | Surgeon + anaesthetist |

```text
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
```

| ID | Walkthrough issue | ProcSense handling | Needs |
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


### Definition of Done (project)

- All 15 steps implemented for S1 and S2 per walkthrough (Step 15 P1)
- 17 P0 cases pass golden runs with the expected end state
- Every walkthrough section mapped in traceability_matrix.csv
- No content without evidence type + WT reference; Type C items listed with owner
- NEWS2/sepsis tiers and calculators pass all walkthrough worked examples
- Trainee can never administer or dose; dose policy enforced in UI and AI
- Unapproved content disabled in assessment mode
- Clean-machine build and documentation reproduce
- Scenario Composer produces ≥8 S1 / ≥6 S2 approved complete cases; contradiction detector passes all mutation tests (P0)
- Assessment evidence mapper covers every row of the workflow-assessment table with deterministic rules (P0)
- Behaviour Model with bounded transitions; AI realisation validated (P0 model / P1 realisation)
- Grounding/validation layer wraps 100% of AI calls; audit logger records every call (P0 / P1)
- Staff AI, state-aware reaction layer, ASR, classifier, AI debrief, Stage A scenario proposal meet their evaluation thresholds or fall back (P1)
- Every synthetic dataset has a dataset card (purpose, generator, source, validation, labels, runtime/dev) (P1)
- Adaptive difficulty selects only approved variants; disabled in assessment mode (P0)


## 45. Walkthrough traceability matrix

Every walkthrough section and every S1/S2 master-map stage → requirement, files, guides, status, validation.

| Page | Part | Section | Requirement | Files | Guides | Status | Validation / unresolved |
|---|---|---|---|---|---|---|---|
| 2 | Front | WHAT CHANGED IN v5.1 (final QA pass) | Content labels A–D, number taxonomy, decision principle, safety statement, version log | Master Plan §Content principles; evidence_types.csv; number_kinds.csv | B-M12, B-M18 | Specified | Clinician sign-off of whole book |
| 3 | Front | WHAT CHANGED IN v5 | Content labels A–D, number taxonomy, decision principle, safety statement, version log | Master Plan §Content principles; evidence_types.csv; number_kinds.csv | B-M12, B-M18 | Specified | Clinician sign-off of whole book |
| 6 | PART I | How to use this book | Clinician validation & institutional adaptation | clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation | B-M17 | Specified — awaiting decisions | 15 decisions + checklist A–G |
| 6 | Part 1 | Part 1 — Medicine basics The 25 core ideas (patient, symptom, sign, diagnosis, surgeon, | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 6 | Part 2 | Part 2 — The two | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 6 | Part 3 | Part 3 — The patient's | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 6 | Part 4 | Part 4 — Vital signs from | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 6 | Part 5 | Part 5 — Blood and urine | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 6 | Part 6 | Part 6 — Heart tracing and | ECG and imaging interpretation | Master Plan Steps 6–7; lab_bands.csv (QTc) | B-M13, B-S10 | Specified | — |
| 6 | Part 7 | Part 7 — Scoring systems | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 6 | Part 8 | Part 8 — Medicines and | Medicines, anaesthesia, dose policy, role convention | medicines.csv | B-M14, B-R17, B-R2 | Specified | Anaesthetist; antibiotic policy (C) |
| 6 | Part 9 | Part 9 — Fasting from zero Why patients must not eat before surgery, and what happens if they did. A very common simulator branch. | Fasting rules | fasting_rules.csv | B-M14, B-R17 | Specified | Anaesthesia department (C) |
| 6 | Part 10 | Part 10 — Consent from | Consent | Master Plan Step 9 | B-R3, B-M3, B-S10 | Specified | Consent law (C) |
| 6 | Part 11 | Part 11 — The 14 steps, | Step teaching structure | steps_21_questions.csv | all step guides | Specified | — |
| 6 | Part 12 | Part 12 — S1 as a story A complete walk-through of a planned gallbladder patient, then abnormal | Scenario stories / patient variants | patient_library.csv; Master Plan §Patient library | B-M19, B-M1 | Specified | Surgeon: hidden facts and answers per variant |
| 6 | Part 13 | Part 13 — S2 as a story A complete walk-through of an emergency appendix patient, then a | Scenario stories / patient variants | patient_library.csv; Master Plan §Patient library | B-M19, B-M1 | Specified | Surgeon: hidden facts and answers per variant |
| 6 | Part 14 | Part 14 — WHAT IF | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 6 | Part 15 | Part 15 — The WHO safety | WHO checklist + extensions | who_checklist.csv | B-R15, B-S10 | Specified | Theatre governance local additions (C) |
| 6 | Part 16 | Part 16 — What the player | Player view | player_view.csv | B-S7, B-S9, B-S10 | Specified | — |
| 6 | Part 17 | Part 17 — All the numbers | All numbers | vitals_bands.csv; lab_bands.csv; key_rules.csv; all_numbers_raw.txt | B-M11, B-M12, B-M18 | Specified | — |
| 6 | Part 18 | Part 18 — Dictionary of | Dictionary | glossary.csv; Implementation Guide glossary | B-M18 | Specified | — |
| 9 | PART A | PART A — Medical foundation (Parts 1–18) | Section content | walkthrough_extract.json | B-M18 | Specified | — |
| 10 | Part 1 | Part 1 — Medicine basics for the software | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 11 | Part 1 | 1.2 Just-enough body knowledge | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 12 | Part 1 | 1.3 How the body responds when something is wrong (the most useful | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 12 | Part 1 | 1.4 The people in the simulator (NPCs) | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 14 | Part 2 | Part 2 — The two operations, explained simply | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 14 | Part 2 | 2.1 S1 — Laparoscopic cholecystectomy (planned keyhole gallbladder | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 15 | Part 2 | 2.2 S2 — Emergency appendectomy (urgent removal of an inflamed | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 16 | Part 2 | 2.3 What ProcSense does NOT simulate | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 17 | Part 3 | Part 3 — The patient's journey (the big picture) | Foundations, scenario definitions, journey + 5 outcomes, NPC roster | Master Plan §Scope/Workflow; npc_roster.csv; decision_outcomes.csv | B-R12, B-R17, B-S3, B-R4 | Specified | — |
| 19 | Part 4 | Part 4 — Vital signs, taught from zero | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 19 | Part 4 | 4.0 Evidence cards for the six core vital signs | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 21 | Part 4 | 4.1 Blood pressure (BP) | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 24 | Part 4 | 4.2 Heart rate (HR, pulse) | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 25 | Part 4 | 4.3 Temperature | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 26 | Part 4 | 4.4 Respiratory rate (breathing rate, RR) | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 26 | Part 4 | 4.5 Oxygen saturation (SpO2) | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 28 | Part 4 | 4.6 Alertness (level of consciousness) | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 29 | Part 4 | 4.7 Pain score | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 29 | Part 4 | 4.8 Other bedside clues | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 29 | Part 4 | 4.9 NEWS2 — the early-warning score, explained from zero | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 30 | Part 4 | 4.10 Sepsis, explained from zero | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 32 | Part 4 | 4.11 Putting the vital signs together — pattern recognition | Vital signs, NEWS2, sepsis, patterns | vitals_bands.csv; news2_*.csv; sepsis_ng253_tiers.csv | B-R13, B-R18, B-S10 | Specified | Anaesthetist alarm limits (C) |
| 33 | Part 5 | Part 5 — Blood and urine tests, taught from zero | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 33 | Part 5 | 5.0 Reference range vs decision threshold — every test at a glance | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 36 | Part 5 | 5A — Full blood count (FBC): the blood cells | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 36 | Part 5 | 5.1 Haemoglobin (Hb) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 37 | Part 5 | 5.2 White blood cells (WBC) and neutrophils | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 37 | Part 5 | 5.3 Platelets (Plt) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 38 | Part 5 | 5.4 CRP (C-reactive protein) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 39 | Part 5 | 5B — Kidneys and body salts (U&E) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 39 | Part 5 | 5.5 Sodium (Na) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 39 | Part 5 | 5.6 Potassium (K) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 40 | Part 5 | 5.7 Urea | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 41 | Part 5 | 5.8 Creatinine and eGFR — kidney function | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 43 | Part 5 | 5C — Blood sugar and diabetes | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 43 | Part 5 | 5.9 Blood glucose (blood sugar) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 43 | Part 5 | 5.10 HbA1c (glycated haemoglobin) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 44 | Part 5 | 5.11 Ketones | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 45 | Part 5 | 5D — Liver tests (LFTs): the heart of S1 | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 45 | Part 5 | 5.12 Bilirubin | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 45 | Part 5 | 5.13 ALT and AST (liver cell enzymes) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 46 | Part 5 | 5.14 ALP and GGT (bile duct enzymes) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 46 | Part 5 | 5.15 Albumin | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 46 | Part 5 | 5.16 Reading liver tests as PATTERNS (most important) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 47 | Part 5 | 5.17 Lipase / amylase (pancreas enzymes) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 48 | Part 5 | 5E — Clotting tests and blood bank | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 48 | Part 5 | 5.18 PT and INR | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 48 | Part 5 | 5.19 APTT and fibrinogen | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 49 | Part 5 | 5.20 Group and save / crossmatch (blood bank) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 50 | Part 5 | 5F — Pregnancy test and urine tests | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 50 | Part 5 | 5.21 Pregnancy test (β-hCG) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 51 | Part 5 | 5.22 Urine dipstick (urinalysis) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 52 | Part 5 | 5G — Emergency and special tests | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 52 | Part 5 | 5.23 Lactate (and blood gas) | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 52 | Part 5 | 5.24 Blood cultures | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 52 | Part 5 | 5.25 Troponin | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 52 | Part 5 | 5.26 Magnesium, calcium, iron, other local tests | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 53 | Part 5 | 5.27 Dangerous ('critical') results the lab will phone about CLINICAL REFERENCE — | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 53 | Part 5 | 5.28 When results are confusing — the trainee's playbook | Laboratory tests, artefacts, critical results | lab_bands.csv; critical_results.csv | B-M13, B-M20 | Specified | Hospital lab ranges (C) |
| 54 | Part 6 | Part 6 — Heart tracing (ECG) and scans, taught | ECG and imaging interpretation | Master Plan Steps 6–7; lab_bands.csv (QTc) | B-M13, B-S10 | Specified | — |
| 54 | Part 6 | 6.1 ECG (electrocardiogram) | ECG and imaging interpretation | Master Plan Steps 6–7; lab_bands.csv (QTc) | B-M13, B-S10 | Specified | — |
| 55 | Part 6 | 6.2 Imaging (scans) — the basics | ECG and imaging interpretation | Master Plan Steps 6–7; lab_bands.csv (QTc) | B-M13, B-S10 | Specified | — |
| 56 | Part 6 | 6.3 Gallbladder ultrasound results (S1) — what each means and what | ECG and imaging interpretation | Master Plan Steps 6–7; lab_bands.csv (QTc) | B-M13, B-S10 | Specified | — |
| 56 | Part 6 | 6.4 Appendix scan results (S2) — what each means and what happens | ECG and imaging interpretation | Master Plan Steps 6–7; lab_bands.csv (QTc) | B-M13, B-S10 | Specified | — |
| 58 | Part 7 | Part 7 — Scoring systems, taught from zero | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 58 | Part 7 | 7.1 Appendicitis scores (S2): Alvarado, AIR and AAS | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 59 | Part 7 | 7.2 Tokyo Guidelines 2018 (TG18) — is the gallbladder inflamed, and | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 60 | Part 7 | 7.3 Tokyo Guidelines — infected bile duct (cholangitis) | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 60 | Part 7 | 7.4 ASA physical status — 'how fit is this patient for anaesthesia?' | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 60 | Part 7 | 7.5 Airway and sleep-apnoea checks (for the anaesthetist) | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 61 | Part 7 | 7.6 Blood-clot (VTE) risk | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 61 | Part 7 | 7.7 Overall surgical risk tools | Scores and risk tools | scores.json | B-M20, B-R12 | Specified | Score choice (surgeon); risk tool (C) |
| 62 | Part 8 | Part 8 — Medicines and anaesthesia, taught from | Medicines, anaesthesia, dose policy, role convention | medicines.csv | B-M14, B-R17, B-R2 | Specified | Anaesthetist; antibiotic policy (C) |
| 62 | Part 8 | 8.1 Why doses vary — and why this book mostly won't give them | Medicines, anaesthesia, dose policy, role convention | medicines.csv | B-M14, B-R17, B-R2 | Specified | Anaesthetist; antibiotic policy (C) |
| 62 | Part 8 | 8.2 Anaesthesia from zero | Medicines, anaesthesia, dose policy, role convention | medicines.csv | B-M14, B-R17, B-R2 | Specified | Anaesthetist; antibiotic policy (C) |
| 63 | Part 8 | 8.3 Medicine groups the trainee deals with | Medicines, anaesthesia, dose policy, role convention | medicines.csv | B-M14, B-R17, B-R2 | Specified | Anaesthetist; antibiotic policy (C) |
| 68 | Part 9 | Part 9 — Fasting, taught from zero | Fasting rules | fasting_rules.csv | B-M14, B-R17 | Specified | Anaesthesia department (C) |
| 68 | Part 9 | What if the patient ate recently? (S1) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 70 | Part 10 | Part 10 — Consent, taught from zero | Consent | Master Plan Step 9 | B-R3, B-M3, B-S10 | Specified | Consent law (C) |
| 73 | Part 11 | Part 11 — The steps, one by one | Step teaching structure | steps_21_questions.csv | all step guides | Specified | — |
| 74 | Part 11 | STEP 1 — Patient arrival and identification | Step 1 21-question specification | Master Plan Step 1; Guide 01; steps_21_questions.csv | B-R11, B-S7, B-S9, B-M11, B-M18 | Specified | Per-step clinician validation line |
| 76 | Part 11 | STEP 2 — Asking questions (the history) | Step 2 21-question specification | Master Plan Step 2; Guide 02; steps_21_questions.csv | B-R6, B-R3, B-S10, B-M3, B-M19 | Specified | Per-step clinician validation line |
| 78 | Part 11 | STEP 3 — Checking vital signs | Step 3 21-question specification | Master Plan Step 3; Guide 03; steps_21_questions.csv | B-R13, B-R18, B-S9, B-S10 | Specified | Per-step clinician validation line |
| 80 | Part 11 | STEP 4 — Examining the patient | Step 4 21-question specification | Master Plan Step 4; Guide 04; steps_21_questions.csv | B-S9, B-S2, B-M3 | Specified | Per-step clinician validation line |
| 82 | Part 11 | STEP 5 — Risk and anaesthetic assessment | Step 5 21-question specification | Master Plan Step 5; Guide 05; steps_21_questions.csv | B-R12, B-M20, B-M12 | Specified | Per-step clinician validation line |
| 84 | Part 11 | STEP 6 — Choosing the tests | Step 6 21-question specification | Master Plan Step 6; Guide 06; steps_21_questions.csv | B-R16, B-M13, B-S7 | Specified | Per-step clinician validation line |
| 86 | Part 11 | STEP 7 — Getting and understanding the results | Step 7 21-question specification | Master Plan Step 7; Guide 07; steps_21_questions.csv | B-M13, B-S10, B-R17, B-M20 | Specified | Per-step clinician validation line |
| 88 | Part 11 | STEP 8 — Deciding, treating and optimising | Step 8 21-question specification | Master Plan Step 8; Guide 08; steps_21_questions.csv | B-M14, B-R17, B-R13, B-R18, B-R14 | Specified | Per-step clinician validation line |
| 91 | Part 11 | STEP 9 — Consent | Step 9 21-question specification | Master Plan Step 9; Guide 09; steps_21_questions.csv | B-R6, B-M3, B-R3, B-S10 | Specified | Per-step clinician validation line |
| 93 | Part 11 | STEP 10 — Preparing the patient (ward/ED | Step 10 21-question specification | Master Plan Step 10; Guide 10; steps_21_questions.csv | B-R15, B-S10 | Specified | Per-step clinician validation line |
| 95 | Part 11 | STEP 11 — Theatre preparation | Step 11 21-question specification | Master Plan Step 11; Guide 11; steps_21_questions.csv | B-S9, B-S1, B-R15, B-R14 | Specified | Per-step clinician validation line |
| 97 | Part 11 | STEP 12 — Transfer and handover (SBAR) | Step 12 21-question specification | Master Plan Step 12; Guide 12; steps_21_questions.csv | B-R16, B-M15, B-R8 | Specified | Per-step clinician validation line |
| 99 | Part 11 | STEP 13 — WHO Sign In (before the patient goes to | Step 13 21-question specification | Master Plan Step 13; Guide 13; steps_21_questions.csv | B-R15, B-S10, B-R4 | Specified | Per-step clinician validation line |
| 101 | Part 11 | STEP 14 — WHO Time Out (before the first cut) — | Step 14 21-question specification | Master Plan Step 14; Guide 14; steps_21_questions.csv | B-R15, B-S10 | Specified | Per-step clinician validation line |
| 102 | Part 11 | STEP 15 (optional) — WHO Sign Out and handover | Step 15 21-question specification | Master Plan Step 15; Guide 15; steps_21_questions.csv | B-R15, B-M15, B-R9, B-S12 | Specified (P1) | Per-step clinician validation line |
| 103 | Part 12 | Part 12 — S1 as a story: a planned gallbladder | Scenario stories / patient variants | patient_library.csv; Master Plan §Patient library | B-M19, B-M1 | Specified | Surgeon: hidden facts and answers per variant |
| 109 | Part 13 | Part 13 — S2 as a story: an emergency appendix | Scenario stories / patient variants | patient_library.csv; Master Plan §Patient library | B-M19, B-M1 | Specified | Surgeon: hidden facts and answers per variant |
| 114 | Part 14 | Part 14 — WHAT IF something goes wrong? | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 114 | Part 14 | 14.0 The universal emergency method: ABCDE | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 114 | Part 14 | 14.1 Vital-sign problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 114 | Part 14 | What if blood pressure is too low? (top number 90 or less) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 115 | Part 14 | What if blood pressure is extremely high? (180/110 or more) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 115 | Part 14 | What if the heart rate is very high? (above 130) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 115 | Part 14 | What if the heart rate is very slow? (40 or below) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 116 | Part 14 | What if the patient has a fever? (38.0 °C or more) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 116 | Part 14 | What if the patient is cold? (36.0 °C or less) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 116 | Part 14 | What if oxygen saturation is low? (below 94%) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 117 | Part 14 | What if breathing is very fast? (25 or more) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 117 | Part 14 | What if breathing is very slow? (8 or less) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 117 | Part 14 | What if the patient becomes confused? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 118 | Part 14 | What if pain gets much worse or the tummy becomes rigid? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 118 | Part 14 | 14.2 Blood-test problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 118 | Part 14 | What if potassium is high? (above 5.3) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 118 | Part 14 | What if potassium is low? (below 3.5) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 119 | Part 14 | What if sodium is abnormal? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 119 | Part 14 | What if haemoglobin is low? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 119 | Part 14 | What if the white cell count is high? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 120 | Part 14 | What if the white cell count is low? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 120 | Part 14 | What if creatinine is high? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 120 | Part 14 | What if liver tests are abnormal? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 121 | Part 14 | What if bilirubin is high? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 121 | Part 14 | What if blood sugar is low? (below 4.0) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 121 | Part 14 | What if blood sugar is high? (above 12) | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 122 | Part 14 | What if the clotting test (INR) is high? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 122 | Part 14 | What if lactate is raised? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 122 | Part 14 | 14.3 Heart tracing problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 122 | Part 14 | What if the ECG is abnormal? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 123 | Part 14 | 14.4 Pregnancy, allergy and medicine problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 123 | Part 14 | What if the patient is pregnant? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 123 | Part 14 | What if pregnancy status is unknown or the patient declines a test? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 123 | Part 14 | What if the patient is allergic to an antibiotic? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 124 | Part 14 | What if an allergic reaction happens after a medicine? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 124 | Part 14 | What if the patient takes blood thinners? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 124 | Part 14 | What if the patient has diabetes? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 125 | Part 14 | What if the patient takes steroids, GLP-1 injections or SGLT2 tablets? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 125 | Part 14 | 14.5 Fasting problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 125 | Part 14 | What if the patient hasn't fasted? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 125 | Part 14 | What if fasting status is unknown? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 126 | Part 14 | 14.6 Scan and result problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 126 | Part 14 | What if imaging is inconclusive? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 126 | Part 14 | What if a test result doesn't make sense? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 126 | Part 14 | What if a laboratory result is delayed? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 127 | Part 14 | What if a critical result is phoned through? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 127 | Part 14 | 14.7 Deterioration and sepsis | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 127 | Part 14 | What if the patient deteriorates? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 127 | Part 14 | What if the patient becomes septic? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 128 | Part 14 | 14.8 Consent and communication problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 128 | Part 14 | What if the patient refuses surgery? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 128 | Part 14 | What if the patient cannot understand the consent discussion? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 128 | Part 14 | What if the patient is under 18? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 129 | Part 14 | 14.9 System problems | What-if method / ABCDE | whatif_library.csv | B-R14, B-R17 | Specified | — |
| 129 | Part 14 | What if equipment fails? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 129 | Part 14 | What if documents are missing? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 129 | Part 14 | What if the patient deteriorates during transfer? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 130 | Part 14 | What if there's an identity mismatch at theatre? | WHAT-IF branch (11 fields) | whatif_library.csv; Master Plan §What-if library | B-R14, B-M19, B-R17 | Specified | Row 'Clinician validation' |
| 131 | Part 14B | Part 14B — When several problems happen at once | Stacking method (ladder, rules, clashes, recipe) | priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt | B-R17, B-M19 | Specified | New combinations approved before build (C) |
| 131 | Part 14B | 14B.1 The priority order (use it every time) | Stacking method (ladder, rules, clashes, recipe) | priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt | B-R17, B-M19 | Specified | New combinations approved before build (C) |
| 131 | Part 14B | 14B.2 Golden rules for stacking | Stacking method (ladder, rules, clashes, recipe) | priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt | B-R17, B-M19 | Specified | New combinations approved before build (C) |
| 132 | Part 14B | 14B.3 Common treatment clashes and the safe resolution | Stacking method (ladder, rules, clashes, recipe) | priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt | B-R17, B-M19 | Specified | New combinations approved before build (C) |
| 133 | Part 14B | 14B.4 Worked stacked cases | Stacking method (ladder, rules, clashes, recipe) | priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt | B-R17, B-M19 | Specified | New combinations approved before build (C) |
| 133 | Part 14B | SC-1 (S2): Sepsis + penicillin anaphylaxis + AKI | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 133 | Part 14B | SC-2 (S2): Positive pregnancy test + lower-right pain + low BP | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 133 | Part 14B | SC-3 (S2): Pregnant (20 weeks) + appendicitis + fever | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 134 | Part 14B | SC-4 (S1): High BP + poorly controlled diabetes | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 134 | Part 14B | SC-5 (S1): On apixaban (taken this morning) + new fast AF | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 134 | Part 14B | SC-6 (S1): Fasting breach + diabetes on insulin | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 134 | Part 14B | SC-7 (S2): Unknown fasting + vomiting + opioid given + sleepy | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 134 | Part 14B | SC-8 (S2): High potassium + AKI + sepsis | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 135 | Part 14B | SC-9 (S1): Jaundice + fever + shaking (cholangitis) + on clopidogrel | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 135 | Part 14B | SC-10 (S2): Language barrier + 17 years old + needs urgent surgery | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 135 | Part 14B | SC-11 (S2): Refuses surgery + becomes septic later | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 135 | Part 14B | SC-12 (S1): Haemolysed high potassium + genuinely low sodium + water tablets | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 136 | Part 14B | SC-13 (S1): Anaemia + anticoagulant + needs G&S | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 136 | Part 14B | SC-14 (S2): Equivocal ultrasound + deteriorating vital signs | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 136 | Part 14B | SC-15 (S2): Anaphylaxis to antibiotic + still needs antibiotics | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 136 | Part 14B | SC-16 (S1): GLP-1 injection + nausea + high BMI + sleep apnoea | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 136 | Part 14B | SC-17 (S2): Sickle cell trait/thalassaemia + dehydration | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 137 | Part 14B | SC-18 (S1): Missing consent + equipment fault + late list | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 137 | Part 14B | SC-19 (S2): Low sugar + confusion + sepsis in a diabetic on SGLT2 | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 137 | Part 14B | SC-20 (S2): Older patient + appendicitis picture + weight loss + anaemia + on aspirin | Compact stacked case | stacked_cases_compact.csv; patient_library.csv | B-M19, B-R17 | Specified (P2) | Send to surgeon/anaesthetist before building (14B.5) |
| 137 | Part 14B | 14B.5 How to build ANY new combination (recipe for the engineering | Stacking method (ladder, rules, clashes, recipe) | priority_ladder.csv; problem_types.csv; treatment_clashes.csv; stacking_method_raw.txt | B-R17, B-M19 | Specified | New combinations approved before build (C) |
| 139 | Part 15 | Part 15 — The WHO Surgical Safety Checklist, | WHO checklist + extensions | who_checklist.csv | B-R15, B-S10 | Specified | Theatre governance local additions (C) |
| 141 | Part 16 | Part 16 — What the player actually sees | Player view | player_view.csv | B-S7, B-S9, B-S10 | Specified | — |
| 141 | Part 16 | Stage 1 — Arrival and identity | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 141 | Part 16 | Stage 2 — History | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 141 | Part 16 | Stage 3 — Vital signs | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 142 | Part 16 | Stage 4 — Examination | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 142 | Part 16 | Stage 5 — Risk and anaesthetic assessment | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 142 | Part 16 | Stage 6 — Tests and moving the patient | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 142 | Part 16 | Stage 7 — Results | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 143 | Part 16 | Stage 8 — Decide, treat, optimise | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 143 | Part 16 | Stage 9 — Consent | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 143 | Part 16 | Stage 10 — Ward preparation | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 143 | Part 16 | Stage 11 — Theatre preparation | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 144 | Part 16 | Stage 12 — Transfer and handover | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 144 | Part 16 | Stages 13–14 — Sign In and Time Out | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 144 | Part 16 | End states | Player view per stage / end states | player_view.csv; end_states.csv | B-S7, B-S9, B-S10, B-M15 | Specified | — |
| 145 | Part 17 | Part 17 — All the numbers in one place (with plain | All numbers | vitals_bands.csv; lab_bands.csv; key_rules.csv; all_numbers_raw.txt | B-M11, B-M12, B-M18 | Specified | — |
| 148 | Part 18 | Part 18 — Dictionary of every medical word and | Dictionary | glossary.csv; Implementation Guide glossary | B-M18 | Specified | — |
| 151 | PART B | PART B — Clinical Accuracy Refinements (v5) | Clinical accuracy refinements + consistency audit | clinical_refinements.txt; Change report | B-M12, B-M18 | Specified | — |
| 151 | PART B | B.1 Corrections and clarifications | Clinical accuracy refinements + consistency audit | clinical_refinements.txt; Change report | B-M12, B-M18 | Specified | — |
| 151 | PART B | B.2 Consistency audit — concepts checked across the whole document | Clinical accuracy refinements + consistency audit | clinical_refinements.txt; Change report | B-M12, B-M18 | Specified | — |
| 153 | PART C | PART C — Expanded VR Clinical Reasoning (Medical | VR reasoning cards / event table | vr_cards.csv; vr_event_table.txt | B-M3, B-S10 | Specified | — |
| 153 | PART C | C.1 Identity check | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 153 | PART C | C.2 History | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 153 | PART C | C.3 Observations / NEWS2 | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 154 | PART C | C.4 Low SpO2 | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 154 | PART C | C.5 High BP (S1) | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 154 | PART C | C.6 Fever (S1) | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 154 | PART C | C.7 Possible sepsis | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 155 | PART C | C.8 Potassium abnormal | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 155 | PART C | C.9 Hypoglycaemia | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 155 | PART C | C.10 Liver tests abnormal (S1) | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 155 | PART C | C.11 Pregnancy possible | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 156 | PART C | C.12 Appendicitis score | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 156 | PART C | C.13 Imaging result | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 156 | PART C | C.14 Medicine needed | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 157 | PART C | C.15 Anticoagulant | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 157 | PART C | C.16 Fasting | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 157 | PART C | C.17 Consent | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 157 | PART C | C.18 Ward preparation | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 158 | PART C | C.19 Transfer & handover | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 158 | PART C | C.20 WHO Sign In | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 158 | PART C | C.21 WHO Time Out | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17 | Specified | C.22 clinician validation required |
| 159 | PART C | C.22 Intra-operative event — ProcSense educational/simulation extension — clinician | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17, B-S12 | Specified (P1 extension) | C.22 clinician validation required |
| 159 | PART C | C.23 WHO Sign Out | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17, B-S12 | Specified | C.22 clinician validation required |
| 159 | PART C | C.24 Recovery handover & debrief | VR reasoning card | vr_cards.csv; Master Plan step sections | B-M3, B-S10, B-R17, B-S12 | Specified | C.22 clinician validation required |
| 163 | PART D | PART D — Expanded Stacked Cases (full playable | Stacked cases | stacked_cases_full.json | B-M19, B-M15 | Specified | — |
| 163 | PART D | STACKED CASE S1-A — Diabetes medication error: hypoglycaemia + | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 168 | PART D | STACKED CASE S1-B — Anticoagulated AF + iron-deficiency anaemia + | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 173 | PART D | STACKED CASE S1-C — True hyperkalaemia with ECG changes + acute | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 177 | PART D | STACKED CASE S1-D — Patient awaiting elective cholecystectomy who | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 181 | PART D | STACKED CASE S2-A — Appendicitis + dehydration/tachycardia + acute | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 186 | PART D | STACKED CASE S2-B — Perforated appendicitis + high-risk sepsis + | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 190 | PART D | STACKED CASE S2-C — Opioid sedation + vomiting + unknown/recent | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 193 | PART D | STACKED CASE S2-D — Appendicitis in pregnancy (18 weeks) + | Full playable stacked case (20 sections) | stacked_cases_full.json; Master Plan §Stacked cases | B-M19, B-M15, B-R17 | Specified (P0/P1 per patient_library.csv) | Anaesthetist/surgeon per case |
| 198 | PART D | PART D — Stacked-case master table (overview) | Stacked cases | stacked_cases_full.json | B-M19, B-M15 | Specified | — |
| 200 | PART E | PART E — Equipment and Asset Mapping | Equipment inventory | equipment.csv | B-S1, B-S9, B-R15 | Specified | — |
| 204 | PART F | PART F — S1 Master Scenario (Elective | S1 master scenario (20 stages) | flow_S1_20_stages.csv | B-R12, B-S12 | Specified | — |
| 210 | PART G | PART G — S2 Master Scenario (Emergency | S2 master scenario (20 stages) | flow_S2_20_stages.csv | B-R12, B-S12 | Specified | — |
| 216 | PART H | PART H — Clinical Evidence Audit (source-to-claim) | Evidence audit | evidence_audit.txt; Data Source Inventory | B-M17 | Specified | Clinician validation column |
| 218 | PART H | H.2 v5.1 verification log — the four previously unverified items | Evidence audit | evidence_audit.txt; Data Source Inventory | B-M17 | Specified | Clinician validation column |
| 219 | PART I | PART I — Clinician Validation, Institutional | Clinician validation & institutional adaptation | clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation | B-M17 | Specified — awaiting decisions | 15 decisions + checklist A–G |
| 219 | PART I | I.1 Institutional adaptation (context-dependent items) | Clinician validation & institutional adaptation | clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation | B-M17 | Specified — awaiting decisions | 15 decisions + checklist A–G |
| 219 | PART I | I.2 Final Clinician / Institutional Decisions Required | Clinician validation & institutional adaptation | clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation | B-M17 | Specified — awaiting decisions | 15 decisions + checklist A–G |
| 219 | PART I | I.3 Clinician Final Validation Checklist | Clinician validation & institutional adaptation | clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation | B-M17 | Specified — awaiting decisions | 15 decisions + checklist A–G |
| 222 | PART I | FINAL STATUS | Clinician validation & institutional adaptation | clinician_validation.txt; institutional_decisions.csv; Master Plan §Validation | B-M17 | Specified — awaiting decisions | 15 decisions + checklist A–G |
| 223 | Part 28 | Part 28 — Sources (references) | Sources | sources.csv; Data Source Inventory | B-M17 | Specified | — |
|  | PART F | S1 master stage 1. Patient arrival | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 1 | B-R12 | Specified | — |
|  | PART F | S1 master stage 2. Initial assessment | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 1 | B-R12 | Specified | Identifier set CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 3. Observations | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 3, 4 | B-R12 | Specified | Alarm limits CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 4. History | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 2, 4 | B-R12 | Specified | — |
|  | PART F | S1 master stage 5. Investigations | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 6 | B-R12 | Specified | Ordering rules CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 6. Results | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 7 | B-R12 | Specified | Lab ranges CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 7. Abnormal branches | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 7 | B-R12 | Specified | — |
|  | PART F | S1 master stage 8. Optimisation | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 8 | B-R12 | Specified | Antibiotic policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 9. Consent | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 9 | B-R12 | Specified | Law/form CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 10. Anaesthetic assessment | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 5 | B-R12 | Specified | — |
|  | PART F | S1 master stage 11. WHO Sign In | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 13 | B-R12 | Specified | — |
|  | PART F | S1 master stage 12. Theatre preparation (runs in parallel before the patient arrives) | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 11 | B-R12 | Specified | — |
|  | PART F | S1 master stage 13. WHO Time Out | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 14 | B-R12 | Specified | — |
|  | PART F | S1 master stage 14. Procedure start | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 14 | B-R12, B-S12 | Specified | — |
|  | PART F | S1 master stage 15. Relevant intra-operative training events (ProcSense extension) | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 15 | B-R12, B-S12 | Specified (P1 extension) | Surgeon to approve events |
|  | PART F | S1 master stage 16. WHO Sign Out | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 15 | B-R12, B-S12 | Specified (P1 extension) | — |
|  | PART F | S1 master stage 17. Recovery / handover | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map; Steps 15 | B-R12, B-S12 | Specified (P1 extension) | Discharge criteria CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 18. Deterioration branches | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map | B-R12, B-R18, B-R14 | Specified (P1 extension) | Clocks (design) — clinician realism |
|  | PART F | S1 master stage 19. Emergency branches | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map | B-R12, B-R18, B-R14 | Specified | Protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART F | S1 master stage 20. Debrief | Scenario flow stage (10 fields) | flow_S1_20_stages.csv; Master Plan §S1 master map | B-R12, B-M15, B-S5 | Specified | — |
|  | PART G | S2 master stage 1. Patient arrival | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 1 | B-R12 | Specified | Triage system CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART G | S2 master stage 2. Initial assessment | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 1, 4 | B-R12 | Specified | — |
|  | PART G | S2 master stage 3. Observations | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 3 | B-R12 | Specified | Tool CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART G | S2 master stage 4. History | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 2, 4 | B-R12 | Specified | — |
|  | PART G | S2 master stage 5. Investigations | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 6 | B-R12 | Specified | Availability CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART G | S2 master stage 6. Results | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 7 | B-R12 | Specified | Score choice (surgeon) |
|  | PART G | S2 master stage 7. Abnormal branches | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 7 | B-R12 | Specified | — |
|  | PART G | S2 master stage 8. Optimisation (in parallel) | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 8 | B-R12 | Specified | Antibiotic policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART G | S2 master stage 9. Consent | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 9 | B-R12 | Specified | Law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART G | S2 master stage 10. Anaesthetic assessment | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 5 | B-R12 | Specified | — |
|  | PART G | S2 master stage 11. WHO Sign In | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 13 | B-R12 | Specified | — |
|  | PART G | S2 master stage 12. Theatre preparation | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 11 | B-R12 | Specified | — |
|  | PART G | S2 master stage 13. WHO Time Out | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 14 | B-R12 | Specified | — |
|  | PART G | S2 master stage 14. Procedure start | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 14 | B-R12, B-S12 | Specified | — |
|  | PART G | S2 master stage 15. Relevant intra-operative training events (ProcSense extension) | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 15 | B-R12, B-S12 | Specified (P1 extension) | Surgeon to approve |
|  | PART G | S2 master stage 16. WHO Sign Out | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 15 | B-R12, B-S12 | Specified (P1 extension) | — |
|  | PART G | S2 master stage 17. Recovery / handover | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map; Steps 15 | B-R12, B-S12 | Specified (P1 extension) | Policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART G | S2 master stage 18. Deterioration branches | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map | B-R12, B-R18, B-R14 | Specified (P1 extension) | Clocks (design) |
|  | PART G | S2 master stage 19. Emergency branches | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map | B-R12, B-R18, B-R14 | Specified | Protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION |
|  | PART G | S2 master stage 20. Debrief | Scenario flow stage (10 fields) | flow_S2_20_stages.csv; Master Plan §S2 master map | B-R12, B-M15, B-S5 | Specified | — |


## 46. References

Walkthrough Part 28 (verbatim list). Data/licence sources are in ProcSense_Data_Source_Inventory.csv.

- World Health Organization. WHO Surgical Safety Checklist (2009) and Implementation Manual. Geneva: WHO; 2009.
- WHO Collaborating Centre for Patient Safety Solutions. Communication during patient hand-overs. Patient Safety Solutions vol 1, solution 3; 2007.
- Gelb AW et al. World Health Organization–World Federation of Societies of Anaesthesiologists (WHO-WFSA) International Standards for a Safe Practice of Anesthesia. Can J Anesth 2018.
- Association of Anaesthetists. Checking anaesthetic equipment (2019); Recommendations for standards of monitoring during anaesthesia and recovery (2021).
- General Medical Council. Decision making and consent (2020). Montgomery v Lanarkshire Health Board [2015] UKSC 11.
- Resuscitation Council UK. Emergency treatment of anaphylaxis (2021); ABCDE approach; ALS algorithms (2021).
- NICE NG45. Routine preoperative tests for elective surgery (2016).
- NICE NG180. Perioperative care in adults (2020). NICE QS216 Perioperative care (2022).
- NICE NG89. Venous thromboembolism in over 16s (2018, updated 2019).
- NICE NG125. Surgical site infections: prevention and treatment (2019, updated 2020).
- NICE CG65. Hypothermia: prevention and management in adults having surgery (2008, updated 2016).
- NICE CG174. Intravenous fluid therapy in adults in hospital (2013, updated 2017).
- NICE NG24. Blood transfusion (2015).
- Hartle A et al. The measurement of adult blood pressure and management of hypertension before elective surgery: AAGBI and British Hypertension Society. Anaesthesia 2016;71:326–37.
- Mancia G et al. 2023 ESH Guidelines for the management of arterial hypertension. J Hypertens 2023. McEvoy JW et al. 2024 ESC Guidelines for elevated blood pressure and hypertension. Eur Heart J 2024.
- Halvorsen S et al. 2022 ESC Guidelines on cardiovascular assessment and management of patients undergoing non-cardiac surgery. Eur Heart J 2022.
- American Society of Anesthesiologists. ASA Physical Status Classification System (updated 2020).
- American Society of Anesthesiologists. Practice guidelines for preoperative fasting (2017) and Joshi GP et al. modular update, Anesthesiology 2023.
- Kindel TL et al. Multisociety clinical practice guidance for the safe use of GLP-1 receptor agonists in the perioperative period (ASA/AGA/ASMBS/ISPCOP/SAGES). Surg Obes Relat Dis 2024;20:1183–6; Clin Gastroenterol Hepatol 2024.
- Centre for Perioperative Care (CPOC). Guideline for perioperative care for people with diabetes mellitus undergoing elective and emergency surgery (2021, updated 2023). JBDS-IP hypoglycaemia and DKA guidelines.
- Woodcock T et al. Guidelines for the management of glucocorticoids during the peri-operative period (Association of Anaesthetists / Society for Endocrinology). Anaesthesia 2020.
- Douketis JD et al. Perioperative management of patients with atrial fibrillation receiving a DOAC (PAUSE). JAMA Intern Med 2019. Doherty JU et al. 2017 ACC Expert Consensus Decision Pathway for periprocedural management of anticoagulation.
- Bratzler DW et al. Clinical practice guidelines for antimicrobial prophylaxis in surgery (ASHP/IDSA/SIS/SHEA). Am J Health-Syst Pharm 2013.
- British Thoracic Society. Guideline for oxygen use in healthcare and emergency settings (2017).
- Chung F et al. STOP-Bang questionnaire. Anesthesiology 2008; Chest 2016.
- Royal College of Physicians. National Early Warning Score (NEWS) 2 (2017).
- Singer M et al. The Third International Consensus Definitions for Sepsis and Septic Shock (Sepsis-3). JAMA 2016.
- Evans L et al. Surviving Sepsis Campaign: international guidelines 2021. Crit Care Med 2021.
- NICE NG253. Suspected sepsis in over 16s: identification and early management in acute hospital settings using NEWS2 (November 2025); supersedes the NEWS2-related parts of NG51.
- KDIGO Clinical Practice Guideline for Acute Kidney Injury (2012); KDIGO CKD guideline (2024). NICE NG148 Acute kidney injury (2019).
- UK Kidney Association. Clinical practice guideline: treatment of acute hyperkalaemia in adults (2023).
- Royal College of Pathologists. The communication of critical and unexpected pathology results (2017).
- US FDA. Pulse oximeter accuracy and limitations: safety communication (2021). NHS Race and Health Observatory. Pulse oximetry and racial bias (2021).
- NICE CG188. Gallstone disease: diagnosis and management (2014). NICE QS104 (2015).
- Yokoe M et al. Tokyo Guidelines 2018: diagnostic criteria and severity grading of acute cholecystitis; Kiriyama S et al. TG18 acute cholangitis. J Hepatobiliary Pancreat Sci 2018.
- Buxbaum JL et al. ASGE guideline on the role of endoscopy in the evaluation and management of choledocholithiasis. Gastrointest Endosc 2019;89:1075–1105.
- Manes G et al. ESGE guideline: endoscopic management of common bile duct stones (2019).
- Kwo PY et al. ACG Clinical Guideline: evaluation of abnormal liver chemistries. Am J Gastroenterol 2017.
- Banks PA et al. Revised Atlanta classification of acute pancreatitis (2012).
- StatPearls: Laparoscopic cholecystectomy; Cholecystitis (NCBI Bookshelf, current editions).
- Di Saverio S et al. Diagnosis and treatment of acute appendicitis: 2020 update of the WSES Jerusalem guidelines. World J Emerg Surg 2020;15:27. (2025 special-populations update — verify access.)
- Alvarado A. A practical score for the early diagnosis of acute appendicitis. Ann Emerg Med 1986.
- Andersson M, Andersson RE. The Appendicitis Inflammatory Response score. World J Surg 2008.
- Sammalkorpi HE et al. A new adult appendicitis score improves diagnostic accuracy. BMC Gastroenterol 2014.
- Ghali MS et al. Adult appendicitis score versus Alvarado score: comparative study. Surg Open Sci 2023;14:96–102.
- CODA Collaborative. A randomized trial comparing antibiotics with appendectomy for appendicitis. NEJM 2020.
- Salminen P et al. Five-year follow-up of antibiotic therapy for uncomplicated acute appendicitis (APPAC). JAMA 2018.
- StatPearls: Acute appendicitis; Appendectomy (NCBI Bookshelf).
- BMJ Best Practice. Acute appendicitis — criteria (accessed 2026).
- NICE NG126. Ectopic pregnancy and miscarriage (2019, updated 2023).
- Pearl JP et al. SAGES guidelines for the use of laparoscopy during pregnancy. Surg Endosc 2017.
- WHO. Guideline on haemoglobin cutoffs to define anaemia in individuals and populations (2024).
- Muñoz M et al. International consensus statement on the peri-operative management of anaemia and iron deficiency. Anaesthesia 2017.
- Estcourt LJ et al. British Society for Haematology guidelines for the use of platelet transfusions (2017).
- WHO Expert Consultation. Appropriate body-mass index for Asian populations. Lancet 2004.
- British National Formulary (BNF) — paracetamol, ondansetron, enoxaparin monographs (current edition).
- openFDA Drug Label API — contraindications/warnings only (no doses imported).
- Target institution's approved clinical protocols (applied at implementation).
- Pathology Harmony Group. Agreed adult clinical biochemistry reference intervals (January 2011). UK.
- UK Kidney Association. Clinical practice guideline: treatment of acute hyperkalaemia in adults (2023).
- Spasovski G et al. Clinical practice guideline on diagnosis and treatment of hyponatraemia. Eur J Endocrinol 2014.
- Kwo PY, Cohen SM, Lim JK. ACG clinical guideline: evaluation of abnormal liver chemistries. Am J Gastroenterol 2017.
- NICE NG253. Suspected sepsis in over 16s: identification and early management in acute hospital settings using NEWS2 (November 2025).
- Di Saverio S et al. Diagnosis and treatment of acute appendicitis: 2025 edition of the WSES Jerusalem guidelines. JAMA Surg 2026 (doi:10.1001/jamasurg.2025.6218).
- Yokoe M et al. Tokyo Guidelines 2018 (and TG07/TG13 imaging criteria: wall >4 mm; long axis >8 cm, short axis >4 cm).
- Pucher PH et al. (SAGES Safe Cholecystectomy Task Force). Outcome trends and safety measures after 30 years of laparoscopic cholecystectomy: systematic review and pooled data analysis. Surg Endosc 2018.
- CODA Collaborative. A randomized trial comparing antibiotics with appendectomy for appendicitis. N Engl J Med 2020;383:1907–19.
- Salminen P et al. Five-year follow-up of antibiotic therapy for uncomplicated acute appendicitis in the APPAC randomized clinical trial. JAMA 2018;320:1259–65.
- El-Boghdadly K et al. Elective peri-operative management of adults taking GLP-1 receptor agonists, GIP agonists and SGLT2 inhibitors: multidisciplinary consensus statement (Association of Anaesthetists, ABCD, BOMSS, CPOC, JBDS-IP, RCoA, SOBA, UKCPA). Anaesthesia 2025;80:412–24.
- Kindel TL et al. Multisociety clinical practice guidance for the safe use of GLP-1 receptor agonists in the perioperative period. Surg Obes Relat Dis 2024.
- SPAQI. Perioperative management of patients taking GLP-1 receptor agonists: multidisciplinary consensus statement. Br J Anaesth 2025;135:48–78.
- Association of Anaesthetists, Obstetric Anaesthetists' Association, Regional Anaesthesia UK. Regional anaesthesia and patients with abnormalities of coagulation. Anaesthesia 2013.
- Rautaharju PM et al. AHA/ACCF/HRS recommendations for the standardization and interpretation of the ECG, part IV (2009); Drew BJ et al. AHA scientific statement on prevention of torsade de pointes (2010).
- Koch E et al. Shock index in the emergency department: utility and limitations. Open Access Emerg Med 2019;11:179–99. Rivers E et al. Comparison of the shock index and conventional vital signs to identify acute critical illness in the emergency department. Ann Emerg Med 1994. Allgöwer M, Burri C. Dtsch Med Wochenschr 1967 (origin).
- NICE CG183. Drug allergy: diagnosis and management (2014).
- Royal College of Anaesthetists. 6th National Audit Project (NAP6): perioperative anaphylaxis (2018).

