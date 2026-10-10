# p0-medical-research-data-s12 — Stacked P0 cases STK-S1-D and STK-S2-B

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
The two P0 stacked cases with all 20 Part D sections: intro, presentation, observations, history (available vs revealed only if asked), abnormalities, prioritisation, decision process (checkpoints), interactions, investigations, results, surgery, anaesthesia, escalation, correct path, incorrect paths, missed path, recovery path, debrief.

## Why
Stacked cases are where trainees practise prioritisation across several simultaneous problems; their correct / incorrect / missed / recovery paths become the golden scripts that prove the engine ranks and responds correctly.

## How to implement
1. Source: walkthrough Part D (+ master table overview).
2. Correct / incorrect / missed / recovery paths become golden-run scripts for p0-evaluation-performance-s02.
3. Problems carry ladder rank + Part D priority (P1–P4).
4. Verbatim check + register rows.
5. Map §4 'immediately available' vs 'only revealed if asked' to `disclose_on` rules; §9 decisions to checkpoints; §17 incorrect paths to what-if bindings.

## Verification
Validator green; 20 sections present for both; ranks match Part D.

## Files changed
`content/cases/STK-S1-D.json`, `STK-S2-B.json`, register.

## Dependencies
p0-medical-research-data-s10, s11.

## Common issues
Part D reuses variant letters — namespaced IDs avoid collisions (WT-I01).

## Sources
Walkthrough Part D; inventory D478, D480.
