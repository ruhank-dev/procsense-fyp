# 47: Electrosurgical (diathermy) unit + return electrode (equipment-list #47)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11

**Representative device:** ERBE VIO 300 D electrosurgical unit with a return pad

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Width x height x depth | 410 x 165 x 380 mm | [ERBE VIO 300 D listing](https://www.somatechnology.com/pdfFiles/ERBE-Vio-300D-ESU.pdf) | listing | exact |
| Weight | 9.5 kg | [same](https://www.somatechnology.com/pdfFiles/ERBE-Vio-300D-ESU.pdf) | listing | exact |
| Adult return pad | 250 x 150 mm | [BOWA neutral electrode listing](https://www.klsmartin.com/shop/en/products/neutral-electrodes/1) | listing | approx |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Unit with a display, sockets, a foot switch, a pencil, a return pad with a cable.

## Materials and colours

Grey unit, white pad.

## States

- off
- on
- pad placed
- pad fault

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Pad placement
- Trainee does: Confirms return pad placed on patient
- System response: Pad snaps to body socket; unit status "pad OK"
- Failure or scenario: Pad missing → burn risk (stop)
- Asset needs (named points): `GRAB_Pad`, `SOCKET_` on patient, `SCREEN_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- None.
