# p0-patient-records-emr

**Tier:** P0 · **Builds on:** p0-core-engine, p0-clinical-logic (results), p0-case-library (patient data), p0-vr-platform · **Feeds:** scenarios, scoring (what the trainee looked at)

## Goal
Two clearly different record sources, both interactive:

| | **Physical patient folder** (on the desk / bed) | **EMR computer** (workstation in bay + OT) |
|---|---|---|
| Holds | **Current episode** paperwork: referral/clerking, observation chart, consent form, ward checklist, printed reports as they arrive | **Complete available record**: current + previous reports, labs, scans/images, medical history, medications, allergies, previous admissions |
| Interactions | Pick up, open folder, flip pages, pick up / place reports, read documents, view printed images | Search by patient name/MRN, open profile, switch tabs, open reports, view images, see new results appear |
| Updates | New printed report physically added when the patient/porter brings it | **Automatically** when a test completes (p0-diagnostic-transitions) |

## EMR screens (tabs across the top, several open at once)
Search (name / MRN / DOB) · Patient profile (identity, allergies, alerts) · Current episode (reason for admission, plan, NEWS2 trend) ·
Lab results (current + previous, H/L flags, lab comments e.g. "haemolysed", trends) · Imaging (reports + image viewer) ·
Reports (all documents, current and previous) · Medical history (past illness, surgery, family, social) · Medications (current list + confirmed prescriptions) ·
Previous admissions/tests. New results show a badge on their tab when they arrive.

## Scope
- **In:** physical folder asset with page-flip + document interactions (Blender asset + Unity logic), printed report/image props,
  EMR UI (world-space screen on a Blender-made workstation; keyboard/pointer input in VR), search, tabs, image viewer, auto-update
  from Core `ResultReady` events, "what the trainee opened" logging for scoring/debrief, **historical record content** per case.
- **Out:** a real hospital information system; editing clinical data (trainee reads; requests go through the order/medicine workflow).

## Content needed (medical-research)
The walkthrough cases give current presentation, history and results. **Previous reports/scans/admissions are mostly not authored in the walkthrough.**
They must be written per case, **consistent with the case**, marked **synthetic**, and medically approved — logged in the source register like any clinical content.
Images in the viewer: licensed real images (see p0-medical-research-data), otherwise a clearly labelled synthetic image with a matching report **after clinician review** ([ADR-0003](../../../decisions/ADR-0003-synthetic-medical-images.md), 10 Oct 2026), otherwise report-only. (Earlier wording: "never AI-generated medical images".) An unapproved image is never shown; the viewer falls back to the report.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Folder vs computer split | Folder = current episode; computer = full history | Everything in one | User direction; mirrors real hospitals | — |
| EMR rendering | Unity world-space UI on the workstation screen | 2D overlay; tablet only | Feels like using a hospital computer | Readability fails (EXP-REC-2) → zoomed "focus" view |
| Text input in VR | Virtual keyboard + quick patient list (search still works) | Voice search; physical keyboard only | Reliable with controllers | Voice input in P1 |
| Page flipping | Animated page turns (blend shape / bone rig from the Blender script) | Static pages swapped | User direction: proper interactions | Performance |
| Data source | Same Core state + content package (no separate database on device) | Separate EMR DB | One source of truth, deterministic replay | Backend (P1) adds history sync |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-REC-1 | Can trainees find a previous result quickly? | Task: "find last HbA1c" | ≤ 30 s for most testers | planned |
| EXP-REC-2 | EMR text readable in the headset? | Font sizes at desk distance | Size fixed in UI guidelines | planned |
| EXP-REC-3 | Page flipping feels natural with controllers? | Testers | No help needed | planned |

## Exit
For every P0 case: folder holds current paperwork, EMR shows full record, new results appear in both after each test, and what the trainee opened is logged.

## Who does what
Developer: EMR UI code, data binding, search/tabs, folder interaction logic, Blender scripts (folder, pages, workstation, printouts), historical-record drafts for review.
Team: UX feel in headset; medical approval of historical records.

## Sources
Walkthrough Part E (#5 notes/folder, #6 NEWS2 chart, #7 consent, #21 results display, #32 drug chart), Part 16 (player view), Parts 12–13 (case data).
