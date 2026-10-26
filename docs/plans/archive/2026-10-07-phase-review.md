> **ARCHIVED (27 Oct 2026).** A one-off review of 07 Oct 2026. Every proposal in it was decided the same day (approved: P1, P2, P3, P6; rejected: P4 and P5); the decisions live in the phase list of `docs/plans/README.md`, `docs/plans/p0/README.md` and the change report (`docs/context/ProcSense_Change_Report.md`, rows 8 and 9). Kept for the reasoning only.

# Phase review — 07 October 2026

Goal: practical phases for the FYP workflow — no time on things the trainee never uses.
**Applied** = already changed in the plans (follows directly from your direction). **Proposed** = waiting for your OK.

## Applied

| # | Change | Type | Why |
|---|---|---|---|
| A1 | **Added `p0-diagnostic-transitions`** — CT / Imaging / X-ray / Lab / MRCP as signed doors: send → wait (~10 s) → report back → EMR updates | Add | Your environment direction (B locations) |
| A2 | **Added `p0-patient-records-emr`** — physical folder (current episode, page flipping) vs EMR computer (search, tabs, full history, auto-updating results/images) | Add | Your folder-vs-computer + computer-UI direction |
| A3 | **`p0-environment-rooms` rewritten**: OT = main fully detailed room; one pre-op patient bay; every location classified A / B / C | Change | Your classification + "model only what's used" |
| A4 | **CT (#23) → report-only / transition**; ultrasound (#22) → door + report/images in EMR; results (#21), blood gas (#20) → EMR | Change | Your CT decision, extended to all diagnostic rooms |
| A5 | **Room dressing list replaced** by interaction-relevant objects only (lights panel, cupboards, work surfaces, sinks, outlets, workstation, folder, doors, phone…) | Remove decorative | Your room-dressing rule |
| A6 | **Added `equipment-interaction-spec.md`** — for every major item: what the trainee does + system/visual response | Add | Your "OT equipment must be operational" rule |
| A7 | **Characters: avatars only for people physically present** (patient, nurses, anaesthetist, surgeon, scrub/ODP, porter); phone/report roles are voice/text only | Reduce | Same principle applied to people |
| A8 | Characters + workflow: walk / wheelchair / trolley + escort transport, chosen and scored | Change | Needed by the door transitions (see clinical notes) |
| A9 | Free-model research done; list with links in `p0/p0-blender-equipment/free-model-candidates.md` | Add | Your request |

### Clinical notes I built in (so the door idea doesn't break walkthrough teaching)
1. **Blood tests: samples go through the Laboratory door, the patient stays.** Bedside sampling after ID check + labelling at the bedside is a
   walkthrough safety lesson (pre-labelled tubes → wrong-blood event).
2. **Unstable patients don't walk** — trolley + escort + monitoring; wrong choice is scored and can trigger the corridor event.
3. **The 10 s wait = real turnaround on the sim clock** — an untreated S2 patient keeps deteriorating while in CT.
4. **Critical results still come by phone + read-back + acknowledgement**, even though the EMR updates automatically.
5. **Previous history/reports in the EMR mostly don't exist in the walkthrough** — they must be written per case, consistent, marked synthetic, medically approved.

## Proposals — user decisions (07 October 2026)

| # | Proposal | Decision | Applied as |
|---|---|---|---|
| P1 | One pre-op patient bay for S1 (ward) + S2 (ED) | ✅ Approved | `p0-environment-rooms` |
| P2 | Merge `p0-datasets` into medical research | ✅ Approved | Folder renamed `p0/p0-medical-research-data/`; `p0-datasets` removed |
| P3 | Walking skeleton first (greybox loop before final art) | ✅ Approved | Build order in `p0/README.md`; `p0-scenario-s1` order |
| P4 | Move `p1-pulse-physiology` → P2 | ❌ Rejected | Stays in P1 |
| P5 | Move `p1-scenario-composer-difficulty` → P2 | ❌ Rejected | Stays in P1 |
| P6 | Recovery reuses the patient bay layout | ✅ Approved | `p1-theatre-extension`, `p0-environment-rooms` |
| P7 | Corridor as transition only | ❌ Rejected → **walkable corridor**; trainee can roam but **can't go through** the B-room doors | Corridor added to category A; doors don't open for the trainee |

## Kept as is (checked, still needed)
Core engine, simulation workflow, clinical logic, physiology/events/behaviour, scoring/debrief, case library, VR platform, Blender equipment,
VR interaction/UI, voice/audio, both scenarios, evaluation/performance; P1 theatre extension, extended cases, backend, AI language, voice input, P1 evaluation; P2 phases.

## Update — 10 October 2026 (owner instruction)

| # | Change | Type | Why |
|---|---|---|---|
| A-D1 | **Data first:** all data work (extraction, clinical tables, decision rules, flow maps, cases, datasets, synthetic data, review pack) is done before the remaining engine and VR work — [data-first-order.md](../p0/data-first-order.md), [ADR-0002](../../decisions/ADR-0002-data-first-order.md) | Reorder | Most remaining engine steps read content that does not exist yet |
| A-D2 | **Ultrasound images with matching reports:** real dataset first, labelled synthetic fallback, clinician review — [feature plan](../p0/p0-medical-research-data/ultrasound-images-and-reports.md), [ADR-0003](../../decisions/ADR-0003-synthetic-medical-images.md) | Change | Replaces "no AI-generated medical images"; the old wording is kept as history where it appeared |
