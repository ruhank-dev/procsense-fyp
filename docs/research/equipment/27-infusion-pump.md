# 27: Infusion pump (equipment-list #27)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** cond. / cond. · **Steps:** 8

**Representative device:** B. Braun Infusomat Space volumetric pump (generic)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Body width x height x depth | 214 x 68 x 124 mm | [B. Braun Infusomat Space data sheet, p.2](https://www.somatechnology.com/pdfFiles/B-Braun-Infusomat-Space-Infusion-System.pdf) | datasheet | exact |
| Weight | about 1.4 kg | [same](https://www.somatechnology.com/pdfFiles/B-Braun-Infusomat-Space-Infusion-System.pdf) | datasheet | exact |
| Pole clamp | holds up to three pumps, horizontal or vertical | [same, p.1](https://www.somatechnology.com/pdfFiles/B-Braun-Infusomat-Space-Infusion-System.pdf) | datasheet | exact |
| Display layout | a user-interface item: the project draws the pump screen | project design (no physical dimension) | design | exact |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Pump body with a display, keys, a door for the giving set and a pole clamp.

## Materials and colours

Light grey and white, dark display.

## States

- off
- idle
- infusing (rate shown)
- alarm (occlusion, bag empty)
- battery low

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Rate display; alarm
- Trainee does: Clamps to pole; rate set by nurse after confirmation (abstracted)
- System response: Display shows rate; alarm in scenario
- Failure or scenario: Pump alarm
- Asset needs (named points): `SCREEN_`, pole clamp

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- Display layout: design item, no physical dimension
