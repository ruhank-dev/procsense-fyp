# p1-scenario-composer-difficulty-s01 — Building blocks and templates

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Block library (21 block types: DEM, PRES, CMB, MED, ALG, PSH, SOC, OBS, FND, RSK, LAB, IMG, CON, PER, PAIN, DIFF, EVT, ENV, STAFF, TIME…) derived from the walkthrough cases, with tags, requires/excludes, enabled rules/events, sources and status; templates per scenario with slot min/max and difficulty limits.

## Why
The composer can only produce trustworthy cases if its raw material is trustworthy: every block derived from a walkthrough case, tagged consistently, with its source.

## How to implement
1. `content/blocks/<type>/*.json`, `tags.yaml`, `content/templates/SCN-S1.json`, `SCN-S2.json`.
2. Composer uses only blocks ≥ APPROVED_FOR_TRAINING.
3. Tag vocabulary `tags.yaml` (e.g. tag:diabetes, tag:anticoagulant) used consistently by requires/excludes.

## Status (17 October 2026)

- **Built** (decision of 17 October 2026, written in `tools/composer/blocks.py`): a composed case is an approved *spine* (an authored case file that fixes the clinical story) varied by *blocks* that do not change that story. 21 block types are named (`content/blocks/types.json`); five carry variation now: DEM (title, sex, age, occupation), NAME (first and family names), SOC (smoking and alcohol answers), PSH (previous and family anaesthetic answers) and ALG (allergies, only for stories that never touch allergies or antibiotics). The other 16 types are owned by the spine and open up as the generators arrive (OBS and LAB by the packs, IMG by the tags, EVT by the director).
- 256 blocks in `content/blocks/<TYPE>/*.json`, each cut from named cases (`derived_from`), with tags, `requires`, `excludes`, sources and the lowest status of its source cases; the tag vocabulary `content/blocks/tags.yaml` (every tag used is defined); `content/templates/SCN-S1.json` and `SCN-S2.json` (slots with min and max, `source` block or spine, difficulty limits by spine tier and problem count). `--check` is in CI.
- **Why not vary comorbidities and medicines:** each one changes the ASA grade, the tests and the decision. They travel with their whole source case, so a stacked case is an authored one (the P1 stacked cases) or made with the 14B.5 recipe (p2-content-extensions-s02).
- **Verification result:** the validator checks every tag exists and every block lists the case it came from.

## Verification
Validator checks every tag exists; every block lists the case(s) it was derived from.

## Files changed
`content/blocks/<type>/*.json`, `content/blocks/tags.yaml`, `content/templates/SCN-S1.json`, `SCN-S2.json`, schema `content/schema/block.schema.json`.

## Dependencies
p1-case-library-extended-s02.

## Common issues
Blocks invented 'to fill gaps' break proof of truth — every block must cite the walkthrough case it came from.

## Sources
Implementation Guide B-M1 Steps 1–2; inventory D46 (21 block types).
