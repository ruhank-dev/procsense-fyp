# p1-live-scenario-generation-s06 — ECG, ultrasound and imaging selection (LG-08)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Tag retrieval from the media index; seeded ECG variants (NeuroKit2 or template); report text filled from slots.

## Why
Images must show exactly the case's findings.

## How to implement
1. `server/generation/media.py`.
2. ECG variant renderer reusing `tools/data/ecg_images.py`.
3. Licence filter.

## Status (17 October 2026)

- **Built:** `server/generation/media.py` (`select_media`, `check_media`): for each imaging slot of the spine (`content/case_imaging.csv`) the pictures that carry the finding code are the candidates (tags from `picture_tags.csv`, s04); a forbidden licence (non-commercial, no derivatives, research only), a technical grade C or a status below training is dropped; real before synthetic, grade A before B, the seed breaks ties; for an ECG the strip nearest the pulse is chosen and a free pulse moves to within 3 of the strip's measured rate; the report is the picture's own approved report with its measurements; a slot with no picture says so (written-only result or a change of story).
- **Verification result** (`tools/ci/test_generation_media.py`, 9 tests): every imaging slot of all 53 authored cases gets a picture of its finding (the three written-only tests say so), zero tag mismatches, a licence for every picked asset, the same seed the same picture, the forbidden licence and low status are dropped, an ECG strip and a free pulse agree within 5.
- **Not built:** the seeded parametric ECG variant (NeuroKit2 or a template rendered as a new picture): the library holds four real strips per class and the pulse follows the strip instead, which keeps pulse and strip in agreement without rendering a new image; hyperkalaemia and hypokalaemia use the library's synthetic strips until the cardiology reference for a parametric pattern is read (OQ-05).

## Verification
Zero tag mismatches; ECG measured rate within 5 bpm.

## Files changed
`server/generation/media.py`

## Dependencies
p1-dataset-analysis-s04, p1-dataset-analysis-s03

## Common issues
Library gaps: written-only result or block swap.

## Sources
Spec LG-08; B15, B16, B17.
