# 🩺 ProcSense · Team Board (v2 — walkthrough edition)

> **Master medical source:** ProcSense_Medical_Walkthrough_v5_1.pdf (READY FOR CLINICIAN VALIDATION — not approved). Full detail: Master Plan · Implementation Guide · ProcSense_Content/ · Data Source Inventory · Change Report.

| 🗓️ Start | 🚩 Feature freeze | 🧊 Code freeze | 🎬 v1.0 | 🎓 Submit |
|---|---|---|---|---|
| Tue 29 Sep | Fri 4 Dec | Fri 18 Dec | Sat 26 Dec | Thu 31 Dec |

> **Status addendum — 09 Oct 2026** (the line above is the v2 original and is kept): the project owner treats all existing medical research and clinical workflows as **clinician-approved** (walkthrough first on 07 Oct, extended 09 Oct). Hospital-specific values and open source conflicts still need input. History: `ProcSense_Change_Report.md` §G.

## 📌 Where the build stands (09 Oct 2026)

| Area | Status |
|---|---|
| Plans | Tier → phase → step files in `docs/plans/` (221 step files). Postponed / outstanding work: `docs/deferred-tasks.md` |
| Walking skeleton 1 | Steps 1–5 approved (content, clock/RNG, session/log, loader/patient state, observable/CLI/replay); step 6 (identity + allergy) built, awaiting approval |
| Step 1 follow-ups now planned | Nurse wristband replacement + missing band (s18), robust spoken identity (s19), 3D nurse (characters s08), VR + tablet fallback (UI s12), nurse voice (audio s06) |
| Repository | `github.com/ruhank-dev/procsense-fyp` (transferred from Myt1x54) |
| Working rules added | Git commands with every hand-off; prompt history; deferred-task register; `docs/context/` kept in sync (INSTRUCTIONS §3, §8f–§8h) |

## 🧭 Rules everyone follows

- The trainee **recognises, communicates and initiates**; a clinician confirms; a nurse administers. No trainee dosing.
- Never type clinical numbers into code — read them from `ProcSense_Content/`.
- Every content item has an evidence type: **A** source · **B** interpretation · **C** institution decides · **D** game design value.
- Appropriate postponement is a **success** end state.

## 🏥 The 15 steps

| Step | Name | P | Build guides |
|---|---|---|---|
| 1 | Patient arrival and identification | P0 | B-R11, B-S7, B-S9, B-M11, B-M18 |
| 2 | Asking questions (the history) | P0 | B-R6, B-R3, B-S10, B-M3, B-M19 |
| 3 | Checking vital signs | P0 | B-R13, B-R18, B-S9, B-S10 |
| 4 | Examining the patient | P0 | B-S9, B-S2, B-M3 |
| 5 | Risk and anaesthetic assessment | P0 | B-R12, B-M20, B-M12 |
| 6 | Choosing the tests | P0 | B-R16, B-M13, B-S7 |
| 7 | Getting and understanding the results | P0 | B-M13, B-S10, B-R17, B-M20 |
| 8 | Deciding, treating and optimising | P0 | B-M14, B-R17, B-R13, B-R18, B-R14 |
| 9 | Consent | P0 | B-R6, B-M3, B-R3, B-S10 |
| 10 | Preparing the patient (ward/ED checklist) | P0 | B-R15, B-S10 |
| 11 | Theatre preparation | P0 | B-S9, B-S1, B-R15, B-R14 |
| 12 | Transfer and handover (SBAR) | P0 | B-R16, B-M15, B-R8 |
| 13 | WHO Sign In (before the patient goes to sleep) | P0 | B-R15, B-S10, B-R4 |
| 14 | WHO Time Out (before the first cut) — the finish line | P0 | B-R15, B-S10 |
| 15 | WHO Sign Out and handover to recovery (optional) | P1 | B-R15, B-M15, B-R9, B-S12 |

## 🏁 End states

- **Ready for incision — safe** — All blockers resolved; correct pathway
- **Appropriately postponed (S1)** — Correct reason found and communicated — counts as SUCCESS
- **Appropriately redirected** — Diagnostic diversion (suspected ectopic → gynaecology); cholangitis → biliary drainage; inflamed gallbladder → acute pathway
- **Safe but delayed** — Right outcome with avoidable delays
- **Safety failure** — A blocker was violated or a life-threatening event missed

## 🧑‍⚕️ P0 cases (17)

VAR-S1-A · VAR-S1-PB · VAR-S1-E · VAR-S1-B · VAR-S1-C · VAR-S1-D · VAR-S1-G · VAR-S1-H · VAR-S1-J · VAR-S2-PC · VAR-S2-PD · VAR-S2-B · VAR-S2-D · VAR-S2-F · VAR-S2-G · STK-S1-D · STK-S2-B

P1: remaining 10 variants + 6 stacked cases · P2: SC-1..SC-20

## 👥 Who owns what

| | Focus | New in v2 |
|---|---|---|
| 🟢 Ruhan | Core, physiology, events, checklist engine, main AI | B-R17 decision & priority engine · B-R18 NEWS2/sepsis engine |
| 🔵 Sannan | Unity, XR, 3D, UI, builds | B-S12 theatre extension scenes (P1) |
| 🟠 Moiz | Content, schemas, labs, medicines, scoring, cases, review | B-M18 content pipeline · B-M19 case library · B-M20 calculators |

## 🗺️ Timeline

> **Note (09 Oct 2026):** `docs/plans/README.md` states "Full scope, no deadlines". Whether the dates below still bind is not recorded — owner to confirm (DT-035). GATE 1 (Step 1 playable on Quest, Fri 9 Oct) is **not met**: Step 1 logic runs headless; Unity/Quest are not installed yet (DT-024).

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

---

## 📆 W2 5–11 Oct

> 🚩 GATE 1 Fri 9 Oct: Step 1 playable on Quest

**🟢 Ruhan**
- [ ] Core: 15-step IDs, identity unlock rule, request-only medicine verbs (B-R11) — *identity unlock rule built (step 6, awaiting approval); the rest not started*

**🔵 Sannan**
- [ ] Step 1 bedside on Quest: wristband rotate-to-read, allergy band, nurse (B-S7/S9)

**🟠 Moiz**
- [ ] Import content package + traceability CI (B-M18)
- [ ] Schemas for Step/FlowStage/WhatIf/StackedCase/Patient/LabResult (B-M11)

## 📆 W3 12–18 Oct

> 🚩 CRG-1 Wed 14 Oct: Steps 1–7 + institutional decisions 1, 4, 5, 6, 10, 11 · GATE 2 Fri 16 Oct: Steps 1–4 + ordering headless

**🟢 Ruhan**
- [ ] NEWS2 Scale 1/2 + NG253 tiers (B-R18)
- [ ] Hidden-fact disclosure for Step 2 (B-R3/B-R11)

**🔵 Sannan**
- [ ] Steps 2–4 UI: clerking note, monitor + pleth, breathing animation, exam hotspots (B-S10/B-S2)

**🟠 Moiz**
- [ ] Calculators: AIR/AAS/Alvarado, TG18, ASGE, ASA, STOP-BANG (B-M20)
- [ ] CRG-1 pack: Steps 1–7 + decisions 1,4,5,6,10,11 (B-M17)

## 📆 W4 19–25 Oct

> 🚩 —

**🟢 Ruhan**
- [ ] Decision engine: 5 outcomes + master decision (B-R12/B-R17)

**🔵 Sannan**
- [ ] Order panel, tubes + bedside labelling, results panel with H/L + comments + phone (B-S9/S10)

**🟠 Moiz**
- [ ] Lab realism: artefacts, critical calls, acknowledgement blocker (B-M13)
- [ ] Medicine request→confirm→administer + 3 checks (B-M14)

## 📆 W5 26 Oct–1 Nov

> 🚩 CRG-2 Wed 28 Oct: Steps 8–15 + decisions 2, 3, 8, 9, 12 · GATE 3 Fri 30 Oct: S1 Steps 1–14 end-to-end with VAR-S1-A and VAR-S1-PB

**🟢 Ruhan**
- [ ] WHO exact wording + extensions + green lights (B-R15)

**🔵 Sannan**
- [ ] Steps 9–14 scenes: consent, ward checklist, theatre stations, transfer, Sign In/Time Out board

**🟠 Moiz**
- [ ] Cases VAR-S1-A and VAR-S1-PB playable (B-M19)
- [ ] CRG-2 pack: Steps 8–15 + decisions 2,3,8,9,12

## 📆 W6 2–8 Nov

> 🚩 GATE 4 Fri 6 Nov: physiology + decision engine

**🟢 Ruhan**
- [ ] Problem registry, priority ladder, clash checker (B-R17)
- [ ] Deterioration clock + patterns (B-R13)

**🔵 Sannan**
- [ ] Patient visuals: jaundice, pallor, rash, opioid sedation (B-S2)

**🟠 Moiz**
- [ ] S1 P0 variants B, C, D, E, G, H, J (B-M19)

## 📆 W7 9–15 Nov

> 🚩 CRG-3 Wed 11 Nov: what-ifs, stacked cases, S2 P0 · GATE 5 Fri 13 Nov: all S1 P0 cases

**🟢 Ruhan**
- [ ] What-if branches as data + teaching events (B-R14)

**🔵 Sannan**
- [ ] Event visuals, corridor event, equipment faults

**🟠 Moiz**
- [ ] STK-S1-D cholangitis case; CRG-3 pack (what-ifs, stacked, S2 P0)

## 📆 W8 16–22 Nov

> 🚩 GATE 6 Fri 20 Nov: AI keep/cut + Step 15/extension keep/cut

**🟢 Ruhan**
- [ ] AI P1 (dialogue, ASR) — keep/cut at Gate 6

**🔵 Sannan**
- [ ] S2 scenes: ED cubicle, triage, early analgesia; voice client (P1)

**🟠 Moiz**
- [ ] S2 cases VAR-S2-PC, -B, -D, -F, -G

## 📆 W9 23–29 Nov

> 🚩 CRG-4 Wed 25 Nov · GATE 7 Fri 27 Nov: S2 end-to-end

**🟢 Ruhan**
- [ ] Sepsis timers + lateness for Usman timeline (B-R18)

**🔵 Sannan**
- [ ] Step 15 / extension scenes if kept (B-S12)

**🟠 Moiz**
- [ ] VAR-S2-PD timeline + STK-S2-B; CRG-4 pack

## 📆 W10 30 Nov–6 Dec

> 🚩 GATE 8 Fri 4 Dec: FEATURE FREEZE

**🟢 Ruhan**
- [ ] Regression + performance

**🔵 Sannan**
- [ ] Debrief timeline UI (B-S5)

**🟠 Moiz**
- [ ] End states, safety failures, scoring weights (B-M15)

## 📆 W11–W14

- [ ] ⚪ Validation playthroughs with Part I checklist A–G (CRG-5 Wed 9 Dec)
- [ ] ⚪ Performance + full tests (Gates 10–11)
- [ ] ⚪ rc1 → rehearsal → v1.0 Sat 26 Dec
- [ ] ⚪ Clean-machine test, sign-off, SUBMIT Thu 31 Dec

## ❓ Decisions needed from Ma'am / clinicians

- [ ] 1. Laboratory reference ranges, units and critical-value list — *Hospital laboratory*
- [ ] 2. Antibiotic protocol: prophylaxis agent/dose/redose; appendicitis regimen; penicillin-allergy alternative (incl. cefazolin after anaphylaxis) — *Microbiology / surgeon*
- [ ] 3. Consent/legal workflow: form, witness, age/guardian, mature minors, emergency treatment, family involvement — *Institution legal / surgeon*
- [ ] 4. Fasting policy details (clear 2 h vs 1 h; GLP-1 and SGLT2 policy choice) — *Anaesthesia department*
- [ ] 5. Local anaesthetic protocols (insulin, steroid cover, K acceptance 3.0–5.5 and INR limits as defaults) — *Anaesthesia department*
- [ ] 6. Emergency escalation: early-warning tool, responders, sepsis tool — *Hospital*
- [ ] 7. Equipment availability (MRCP, ERCP, CT, gastric ultrasound, capnography, warming) — *Hospital*
- [ ] 8. Documentation: ward checklist, handover form, WHO version/additions, site marking — *Theatre governance*
- [ ] 9. Blood-bank procedure (G&S/crossmatch, labelling) — *Transfusion lab*
- [ ] 10. Patient identification policy (identifiers, band colours) — *Hospital*
- [ ] 11. Transfer escort/monitoring rule — *Hospital*
- [ ] 12. Post-operative/day-case discharge criteria — *Surgeon / anaesthetist*
- [ ] 13. Choice of appendicitis score (AIR default vs AAS) and imaging pathway for high-score young patients — *Surgeon*
- [ ] 14. Unit-specific complication rates for consent — *Surgeon*
- [ ] 15. Realism of simulator design values (deterioration clocks, turnaround times) — *Surgeon + anaesthetist*
- [ ] 16. *(added 09 Oct 2026)* Missing wristband: apply the band before or after verification? Walkthrough says after verification; Guide T01-4 says after a delay (ISS-0002) — *Surgeon / hospital*
- [ ] 17. *(added 09 Oct 2026)* BP cuff cycle: 30–40 s (walkthrough) or 20 s (Guide)? (ISS-0001) — *Medical reviewer*

> Note (09 Oct 2026): the owner's approval statement does not say whether it closes items 1–15 above (DT-034). They stay open until the owner says so.

> Plus walkthrough issues WT-I01…WT-I14 (see Change Report).