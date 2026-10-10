# 45: Laparoscopic tower (camera, light source, monitor, insufflator) (equipment-list #45)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 14

**Representative device:** Olympus workstation cart with camera control unit, light source, monitor and an insufflator

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Cart (H x W x D) | 1524 x 610 x 711 mm (60 x 24 x 28 in) | [Olympus WM-SC workstation listing](https://www.dotmed.com/listing/endoscopy-general/olympus/wm-sc-workstation/endoscopy-tower-stand/5482837) | listing | exact |
| Insufflator (ConMed 35L) | 338 x 175 x 375 mm (H x W x D), 10.4 kg | [ConMed Linvatec 35L listing](https://www.medwrench.com/equipment/6891/conmed-linvatec-35l) | listing | exact |
| Insufflator (Olympus UHI-4) | 370 x 120 x 300 mm, 9.3 kg | [Olympus tower configuration listing](https://www.somatechnology.com/pdfFiles/olympus-hysteroflator-100-insufflator.pdf) | listing | approx |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Cart with shelves for the units, a boom or a monitor arm, a screen, cables to the laparoscope and the insufflation tube.

## Materials and colours

Grey units, black monitor.

## States

- off
- on with image
- no signal
- insufflator fault message

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Fault messages; pressure/flow display
- Trainee does: Powers on units; checks camera image, light source, insufflator pressure/flow; reads fault messages
- System response: Monitor shows image / "no signal"; insufflator display shows values or a fault message (scenario)
- Failure or scenario: Fault → delay
- Asset needs (named points): `SCREEN_` per unit, power buttons

## Triangle-budget class

High

## Open points (nothing here is guessed)

- None.
