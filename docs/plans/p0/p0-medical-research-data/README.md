# p0-medical-research-data

**Tier:** P0 (continuous) · speech/dialogue datasets live in P1 phases · **Builds on:** p0-foundation · **Feeds:** clinical-logic, simulation-workflow, case-library, physiology, patient-records-emr (history, images), blender-equipment (real dimensions), evaluation

(Merged from p0-medical-research + p0-datasets — phase review P2, approved 07 October 2026.)

## Goal
1. **Proof of truth:** every clinical value, rule, threshold, case fact and dose example has a recorded walkthrough reference,
   original source opened and checked, evidence type (A/B/C/D) and status. `content/` is generated only from these rows.
2. **Data:** find and verify the external datasets P0 needs; generate synthetic data where none exists — traceable, never shipping real patient rows.

## Why
The medical content is approved by a medical professional; the register makes that approval **traceable**. Realistic vitals, waveforms,
lab distributions and images make the patient believable.

## Scope
- **Research:** source register, issues log (WT-I01…WT-I14 + new), cross-checking cited guidelines (NICE, RCP NEWS2, WHO checklist, WSES,
  TG18, UKKA, ASA…), institutional values (the 15 Part I decisions) with the approved choice, content package build (vitals bands, NEWS2,
  sepsis tiers, lab bands, critical results, fasting rules, WHO checklist, test indications, medicines-as-plans, scores).
- **Records content:** per-case **historical records** for the EMR (previous reports, tests, admissions) — written consistent with the case,
  marked synthetic, medically approved.
- **Equipment references:** real-world dimensions/components/appearance for every Blender asset.
- **Data:** dataset discovery + content check, aggregate extraction (percentiles, noise models, waveform templates), synthetic generators
  (lab noise/artefacts, vital trajectories) with dataset cards, real result images for the EMR viewer.
- **Out:** inventing or changing approved medical decisions; AI-generated medical images (never); raw third-party patient rows in builds.

## Deliverables
- `docs/research/source_register.csv` complete for P0 content; `docs/research/issues.md` with every conflict + who resolved it.
- `content/` package v1, each row linked to a register ID.
- Historical EMR records per P0 case (synthetic, approved).
- Equipment reference sheets → `docs/research/equipment/`.
- `docs/research/dataset_register.csv` filled; extracted parameter files; synthetic datasets + cards.

## Dataset candidates (from original inventory — all to be checked)
| Need | Candidate | Check |
|---|---|---|
| Surgical vitals + waveforms | VitalDB | Access + citation terms |
| ED triage vitals, adult lab distributions | MIMIC-IV demo / MIMIC-IV-ED demo (PhysioNet) | Demo vs credentialed access |
| ECG traces | PTB-XL (PhysioNet) | 12-lead → monitor trace |
| Gallbladder ultrasound images | UIdataGB (Mendeley Data) | Medical approval per image |
| Chest X-ray images | NIH ChestX-ray14 | Label noise; medical approval per image |
| CT images for the EMR viewer (abdomen) | to research (public CT datasets) | Report-only if none suitable; never AI images |
| Adult appendix ultrasound | none known free | Text report from finding codes |
| Drug label safety fields | openFDA | No doses imported |

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Master medical source | Walkthrough v5.1 (approved), cross-checked against original guidelines | Guidelines only | Walkthrough is designed for this game | New walkthrough version |
| Walkthrough ≠ newer guideline (e.g. WT-I13, WT-I14) | Keep walkthrough value, log it, ask medical professional | Silently update | Never pick silently (INSTRUCTIONS §4d) | — |
| Content format | CSV for tables, JSON for nested (cases, what-ifs) | All JSON; ScriptableObjects | Readable/editable by medical reviewers | Reviewers prefer another format |
| Content generation | Script: register → `content/` (no hand edits) | Hand-edited | One source of truth | — |
| How data enters the app | Parameters/aggregates generated at build time | Ship raw rows | Privacy, small app | — |
| Case lab values | Authored (walkthrough); generator adds only noise/artefacts inside verified bands | Fully generated | Cases are clinically designed | — |
| Missing imaging | Text reports | AI images | Never generate medical images | Real image set found |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-MED-1 | How much cited material can be verified from free sources? | Verify first 30 P0 values end to end | % VERIFIED + time per value |
| EXP-MED-2 | Can `content/` be fully regenerated from the register? | Build script + diff check in CI | Zero manual edits |
| EXP-DATA-1 | Do VitalDB-derived noise/waveforms look realistic? | Blind comparison by medical reviewer | Derived preferred |
| EXP-DATA-2 | Generated lab values always within the case's band? | Property test, 10k seeds | 0 violations |

## Exit
100% of P0 content rows have register entries; 0 invented sources; every conflict has a recorded resolution; every dataset used is
registered; every synthetic set (incl. EMR history) has a card and approval.

## Risks
Paywalled guideline → cite walkthrough, mark UNVERIFIED, ask for access. Guidelines updated mid-project → record version + date checked.
Credentialed-only datasets → demo subsets or literature ranges (Type B/D).

## Who does what
Claude: research, verification, register, content scripts, datasets, generators, history drafts. Medical professional: resolves logged conflicts, approves history.

## Sources
Walkthrough (Parts 4–10, 17 numbers, H evidence audit, I, 28 sources); Data Source Inventory (D31–D44 data); Change Report §F; Implementation Guide B-M4, B-M13.
