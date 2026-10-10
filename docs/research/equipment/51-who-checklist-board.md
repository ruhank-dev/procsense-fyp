# 51: WHO checklist board / form (equipment-list #51)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 13–15

**Representative device:** WHO Surgical Safety Checklist board, A3

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Board | 297 x 420 mm (A3) | [ISO 216 paper sizes (A3 297 x 420 mm, A4 210 x 297 mm)](https://www.iso.org/standard/36631.html) | standard | approx |
| Form layout | the WHO checklist wording is fixed (verified, V-31); the board layout is the project's design | project design | design | exact |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Wall board or wipe-clean panel with the Sign In, Time Out and Sign Out boxes; item status pending or confirmed.

## Materials and colours

White panel, coloured headings.

## States

- pending
- confirmed
- mismatch flagged

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Item status pending/confirmed
- Trainee does: Leads/answers Sign In and Time Out items
- System response: Items change pending → confirmed (green); skipped item → challenge, repeated skip stops case
- Failure or scenario: Skipped item → stop
- Asset needs (named points): `SCREEN_` / board UI

## Triangle-budget class

Very low

## Open points (nothing here is guessed)

- Form layout: design item, no physical dimension
