# 05: Patient notes / folder (equipment-list #5)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 1–12

**Representative device:** Paper patient folder, A4

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Page size | 210 x 297 mm | [ISO 216 paper sizes (A3 297 x 420 mm, A4 210 x 297 mm)](https://www.iso.org/standard/36631.html) | standard | exact |
| Thickness | a thin A4 folder of a few millimetres; no standard exists, the model chooses it | project design choice (no real-world figure is defined) | design | approx |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Folder cover, flippable pages, a spine tag.

## Materials and colours

Card cover, white pages.

## States

- closed
- open at a page

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Opens to UI notes
- Trainee does: Opens, flips pages, reads, adds reports
- System response: See p0-patient-records-emr
- Failure or scenario: Missing documents block transfer
- Asset needs (named points): Page rig, `GRAB_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- Thickness: design item, no physical dimension
