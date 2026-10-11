# p0-clinical-logic-s09 — Imaging report categories

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Imaging results as report categories with report text and key findings: S1 ultrasound — 7 report types (walkthrough 6.3); S2 appendix — positive simple / complicated / negative-other / non-diagnostic / normal (6.4). 'Appendix not visualised' is equivocal, never negative.

## Why
Imaging is report-based (category B rooms); interpretation is taught through categories.

## How to implement
1. `content/imaging_reports.json`: per case + study → category, report text, measurements (wall mm, duct mm, appendix diameter) with refs.
2. `Clinical/Imaging.cs`: categories → finding codes → decision engine.
3. Interpretation rule: treating 'not visualised' as negative → major item (T-IV7, E-guide classic error).
4. Images (if licensed + approved) referenced by ID for the EMR viewer; otherwise report only.

## Verification
`dotnet test --filter FullyQualifiedName~Imaging` → T-IV7 green; VAR-S1-A clinic ultrasound 'several stones; wall 2 mm; duct 4 mm' (walkthrough Scene 6) shown as simple gallstones.

## Files changed
`src/ProcSense.Core/Clinical/Imaging.cs`, `content/imaging_reports.json`, tests.

## Dependencies
p0-clinical-logic-s06; p0-medical-research-data-s16.

## Common issues
Images: real licensed first; labelled synthetic only as the reviewed fallback (ADR-0003, 10 Oct 2026; earlier wording "Never AI-generated images"). The finding code, not the picture, drives the engine.

## Sources
B-M13 Step 8, T-IV7; walkthrough Part 6 (6.3, 6.4); inventory D44, D222.
