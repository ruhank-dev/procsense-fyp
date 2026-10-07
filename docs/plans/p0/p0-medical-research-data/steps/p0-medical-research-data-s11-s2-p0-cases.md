# p0-medical-research-data-s11 — S2 P0 cases as data

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Case files for VAR-S2-PC (Hamza Ali), -PD (Usman — deterioration timeline 14:00 → 15:45), -B (ectopic, redirect), -D (US not visualised), -F (full stomach → RSI), -G (dehydration/AKI), including triage notes and timed observation points.

## Why
S2 cases carry time-dependent data (triage observations, the Usman deterioration timeline) that physiology, sepsis timers and scoring replay; any transcription error changes when the trainee is 'late'.

## How to implement
1. Source: walkthrough Part 13; inventory D463–D469 as key-value checklist.
2. Usman timeline as timed observation points (untreated and treated branches) — used by p0-physiology-events-behaviour-s04 fixtures.
3. WT-I02 (Hamza 14 h vs 20 h): use Part 13 values, note the conflict.
4. Verbatim check + register rows as s10.
5. Triage notes and timed observation points carry their clock times exactly as written.

## Verification
Validator green; Usman points match the walkthrough table; verbatim check 100%.

## Files changed
`content/cases/VAR-S2-*.json`, register, issues.

## Dependencies
p0-medical-research-data-s10.

## Common issues
Timeline points are data — never code branches.

## Sources
Walkthrough Part 13; inventory D463–D469; WT-I02.
