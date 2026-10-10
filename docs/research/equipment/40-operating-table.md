# 40: Operating table + positioning padding (equipment-list #40)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11

**Representative device:** Maquet Alphamaxx 1133 operating table (generic)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Table length x width | 1860 x 600 mm overall; width 540 mm without side rails, 592 mm across them | [Maquet Alphamaxx 1133 listings](https://bioclinicalservices.com.au/maquet/clinical/alphamaxx-1133-22-operating-instructions-ver-05-05-may-2010) | listing | exact |
| Height range | 590 to 1060 mm | [same](https://www.somatechnology.com/surgical-tables/maquet-alphamaxx-1133) | listing | exact |
| Tilt | lateral 20 degrees; Trendelenburg and reverse up to 30 degrees | [same](https://www.somatechnology.com/surgical-tables/maquet-alphamaxx-1133) | listing | exact |
| Load | up to 450 kg | [same](https://www.somatechnology.com/surgical-tables/maquet-alphamaxx-1133) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Mobile column with a table top in sections, side rails, a hand control, padding.

## Materials and colours

Stainless steel column, blue radiolucent pads.

## States

- flat
- tilted
- raised
- padded

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Positions (`PIVOT_*`)
- Trainee does: Uses control to set height/tilt/flat; places padding at pressure points
- System response: Table sections animate; padding snaps to sockets; positioning checklist item ticks
- Failure or scenario: Team check at Step 11
- Asset needs (named points): `PIVOT_` sections, `SOCKET_Pad_*`

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- None.
