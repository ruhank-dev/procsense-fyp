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
