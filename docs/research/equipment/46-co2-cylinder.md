# 46: CO2 cylinder (+ spare) (equipment-list #46)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11

**Representative device:** BOC carbon dioxide size E cylinder and a spare

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Size E | 865 mm long x 102 mm diameter, 1800 L at 50 bar, 8.5 kg full | [BOC carbon dioxide cylinder data](https://www.bioclinicalservices.com.au/british-oxygen-company/medical-cylinder-data/medical-gas-cylinder-data-chart-aug-2011) | listing | exact |
| Size C | 430 mm x 89 mm, 450 L, 3.0 kg full | [same](https://www.bioclinicalservices.com.au/british-oxygen-company/medical-cylinder-data/medical-gas-cylinder-data-chart) | listing | exact |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Cylinder with a pin-index valve and a gauge on the regulator.

## Materials and colours

Grey body (CO2).

## States

- full
- low
- empty
- spare present

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Gauge level
- Trainee does: Reads gauge; swaps to spare if empty
- System response: Gauge shows level from scenario; swap animation; insufflator fault clears
- Failure or scenario: Empty → fault scenario
- Asset needs (named points): Gauge `PIVOT_`, `GRAB_`, `SOCKET_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- None.
