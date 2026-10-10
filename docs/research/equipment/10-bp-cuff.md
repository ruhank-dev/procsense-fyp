# 10: BP cuff (NIBP) (equipment-list #10)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** ward/ED · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 3

**Representative device:** Adult non-invasive blood pressure cuff

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Small adult cuff (bladder) | 12 x 22 cm for arm 22 to 26 cm | [AHA cuff size table as cited in the literature](https://www.nature.com/articles/hr2010210) | listing | exact |
| Standard adult cuff (bladder) | 16 x 30 cm for arm 27 to 34 cm | [same](https://www.nature.com/articles/hr2010210) | listing | exact |
| Hose length | an adult cuff comes with a short attached hose of about 30 cm; an extension air hose of 1.5 m is the usual length | [Philips adult NIBP air hose 1.5 m and compatible M1574A cuff listings](https://acc.usa.philips.com/healthcare/product/989803209761/adult-nibp-air-hose-length-15m-492-feet) | listing | approx |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Fabric cuff with hook-and-loop closure, inflatable bladder, hose and connector.

## Materials and colours

Navy fabric, black hose.

## States

- wrapped
- inflating
- deflating
- loose (bad reading)

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Wrap arm (`SOCKET_` on arm); hose to monitor; sizes
- Trainee does: Wraps arm, starts cycle
- System response: ~30–40 s cycle (WT), then SBP/DBP/MAP on monitor
- Failure or scenario: Wrong size → false reading
- Asset needs (named points): Arm `SOCKET_`, hose `ATTACH_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- None.
