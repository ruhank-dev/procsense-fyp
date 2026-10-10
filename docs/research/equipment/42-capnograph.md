# 42: Capnograph (equipment-list #42)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 13

**Representative device:** End-tidal CO2 module of the monitor (item 9) or the anaesthesia machine (item 41)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Sampling line | 2, 3 or 4 m lengths; no separate device: the capnograph is a module of items 9 and 41 | [Masimo NomoLine sampling line brochure and distributor listings](https://masimo.fr/siteassets/us/documents/pdf/plm-11480e-brochure-nomoline-sampling-lines-us.pdf) | listing | exact |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

A waveform and value on the monitor; a sampling line.

## Materials and colours

See items 9 and 41.

## States

- no waveform
- waveform present
- absent after intubation (alarm)

## Interaction (from the equipment list and the interaction spec)

- Equipment list: EtCO2 waveform (often part of monitor)
- Trainee does: Looks at EtCO2 after induction (abstracted)
- System response: Waveform + value from Core
- Failure or scenario: —
- Asset needs (named points): `SCREEN_`

## Triangle-budget class

None (screen element)

## Open points (nothing here is guessed)

- None.
