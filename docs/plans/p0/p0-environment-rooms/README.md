# p0-environment-rooms

**Tier:** P0 · **Builds on:** p0-vr-platform · **Feeds:** p0-blender-equipment (what goes where), p0-diagnostic-transitions, vr-interaction-ui, scenarios

## Principle
**Fully model what the trainee actually learns and interacts with. Simulate/abstract everything else.**
The supervisor's base hospital room model provides the shells; we dress and make interactive only category-A spaces.

## Goal
Turn the supervisor's room shells into the few spaces the trainee actually works in — with the **Operation Theatre as the main,
fully detailed environment** — and represent every other location as a door transition (B) or inside the computer record (C).

## Location classification

### A — Fully modelled + interactive
| Location | Why it must be modelled | Walkthrough steps | Notes |
|---|---|---|---|
| **Operation Theatre** (incl. anaesthetic / Sign In area, scrub area, storage) | Main environment: theatre preparation, equipment checks, WHO Sign In + Time Out, positioning, monitoring | 11, 13, 14 (15 in P1) | Most detailed space; every listed equipment item operational (see `../p0-vr-interaction-ui/equipment-interaction-spec.md`) |
| **Pre-operative patient bay** (S1 ward bay / S2 ED cubicle) | Steps 1–10 are hands-on at the bedside: identity + allergy bands, vitals, examination, **bedside blood sampling + tube labelling**, ECG, IV access, oxygen, consent, preparation | 1–10 | **One bay**, dressed as ward bay (S1) or ED cubicle (S2) from data (approved P1) |
| **Corridor** (walkable) | Trainee can roam between the patient bay, the OT and the diagnostic doors; accompanies the patient on transfer (Step 12); corridor deterioration event plays out here | 12 + movement | Approved: roamable, but the trainee **cannot go through** the B doors (they don't open for the trainee — patient/porter only) |
| **Work desk + computer** (in patient bay, and in OT) | Physical folder + reports placed here; EMR computer used here | all | Small area inside the A rooms, not a separate room |

### B — Transition / report-only
The trainee sends the patient (or sample) there; a door with a clear sign plate **along the corridor**; a short wait; the patient comes back with a report;
the computer record updates automatically. The trainee can walk up to these doors but **cannot enter** — trying shows a short message (e.g. "Patients and staff only — wait for the result"). Mechanics in `../p0-diagnostic-transitions/`.

| Location | What goes through the door | Comes back with | Walkthrough items |
|---|---|---|---|
| **CT Scan** | Patient (escorted as their stability requires) | CT report (+ key images in the computer) | #23 CT — walkthrough allows "room or report only" |
| **Imaging / Ultrasound** | Patient | Ultrasound report (+ images where licensed images exist) | #22 ultrasound |
| **X-ray** | Patient | X-ray report (+ image) | Chest X-ray if indicated |
| **Laboratory** | **Labelled samples only** (patient stays in bed) — see clinical note | Printed lab report + computer results; critical results still phoned | #16–#19 bloods, cultures |
| **MRCP / ERCP** | Patient (S1 duct-stone pathway) | Report | #24 (walkthrough: report only) |
| **Gynaecology / other specialty** (redirect end state) | Patient leaves pathway | Case ends as "appropriately redirected" | End state REDIRECTED_APPROPRIATELY |
| **Recovery** (P1) | — | — | Step 15 — P1: **reuses the patient bay layout** (approved P6), not a new room |

### C — Computer / UI representation (no 3D space)
Previous medical history, previous reports/tests/scans, current lab results, imaging viewer, blood gas results, NEWS2 chart trend,
drug chart (confirmed prescriptions), results/critical-call log, SBAR handover form, MRCP/ERCP report text — all in the
EMR computer (`../p0-patient-records-emr/`) or tablet.

## Clinical notes on the door-transition idea (must keep the walkthrough's teaching)
1. **Blood tests stay at the bedside.** Taking blood after the identity check and labelling tubes at the bedside is a core safety lesson
   (walkthrough Step 6: pre-labelled tubes → wrong-blood event; drip-arm / EDTA artefacts). So for lab tests the **samples** go through the
   Laboratory door (porter or pneumatic-tube station), not the patient.
2. **Unstable patients must not walk.** Walkthrough: "Unstable patient must not go by wheelchair"; S2 "Unstable → monitored transfer";
   transfer escort depends on NEWS2 (inventory D202). So the trainee chooses **walk / wheelchair / trolley + escort + monitoring** and that
   choice is scored. A wrong choice can trigger the corridor deterioration event.
3. **Time passes during the wait.** The ~10 s on screen represents the test's turnaround on the simulation clock (walkthrough: FBC 45 min,
   imaging 60–120 min; others Type D). The patient's physiology keeps running — an untreated S2 patient can deteriorate during a CT.
4. **Critical results still need the phone call + acknowledgement** (walkthrough Part 5) — the computer update doesn't replace it.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Main environment | OT fully modelled + interactive | All rooms equal | User direction; it's where the core training happens | — |
| Bedside space | **One pre-op patient bay**, dressed as ward bay (S1) or ED cubicle (S2) from data — approved | Two separate rooms | Half the modelling; same interactions | Supervisor's model already has both rooms finished → use both |
| Diagnostics | Door + sign + wait + report (category B) | Model CT/lab/imaging rooms | User direction; no trainee interaction inside | — |
| Lab tests | Samples through the lab door; patient stays | Patient walks to lab | Keeps the bedside labelling lesson | — |
| Corridor | **Walkable corridor** (from the supplied model) connecting bay, OT and the B doors; trainee walks with the patient on transfer; B doors don't open for the trainee — approved | Transition/fade only (rejected) | User decision: free movement keeps the hospital feeling real | — |
| Room dressing | Only interaction-relevant objects (see equipment list) | Full decorative dressing | User direction | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-ENV-1 | Is the supplied model Quest-ready (tris, materials, scale)? | Import + profile on device | Within budget, else optimisation plan | planned |
| EXP-ENV-2 | Does the door transition feel believable? | Testers send a patient to CT | Understood without explanation | planned |
| EXP-ENV-3 | Which rooms does the supplied model include? | Inspect on arrival | Map shells → A/B locations | planned |

## Exit
OT + patient bay + corridor load on Quest within budget with all anchors; every B location has a door + sign + transition that the trainee can't pass; no unneeded room is dressed.

## Who does what
Developer: import validation, anchor/manifest tooling, placement scripts, door/sign assets, lighting settings. Team: receive model from supervisor, layout judgement, headset checks.

## Sources
Walkthrough Part E (equipment + where it appears), Step 6, Step 12, Part 5 (critical results); inventory D202–D204; Master Plan §25.
