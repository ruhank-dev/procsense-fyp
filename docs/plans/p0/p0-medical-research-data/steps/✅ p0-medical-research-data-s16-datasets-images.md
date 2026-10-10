# p0-medical-research-data-s16 — Images for the EMR imaging viewer

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Real, licensed images for the EMR viewer where cases need them — gallbladder ultrasound (UIdataGB), chest X-ray (NIH ChestX-ray14), abdominal CT (public set to find) — each chosen to match a case's report and approved by the medical professional; report-only where nothing suitable exists (adult appendix ultrasound).

## Why
Real images make results believable, but they are medical data: they must be licensed for this use, match the case's finding exactly, and be approved by the medical reviewer. **Updated 10 Oct 2026 ([ADR-0003](../../../../decisions/ADR-0003-synthetic-medical-images.md)):** where no suitable real image exists, a clearly labelled synthetic image with a matching report may be used, but only after clinician review (the earlier wording was "AI-generated medical images are never allowed"). Ultrasound images and reports: [feature plan](../ultrasound-images-and-reports.md).

## How to implement
1. Open each dataset page; record access/terms/contents in the dataset register.
2. Select images per case finding; store in `content/images/<case>/` (Git LFS) with `content/images/index.json` {image, case, finding, source, approval}.
3. Report-only fallback documented per case.
4. Downscale to the size the EMR viewer needs (keeps the APK small).

## Verification
Every image used has a register row, a licence record (real) or a generator record and visible mark (synthetic), and an approval state; unapproved images are not shown.

## Files changed
`content/images/*`, `content/images/index.json`, dataset register.

## Dependencies
p0-medical-research-data-s10, s11.

## Common issues
Strip any embedded metadata from images before committing.

## Sources
Inventory D40–D44.
