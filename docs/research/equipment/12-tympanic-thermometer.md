# 12: Thermometer (tympanic) (equipment-list #12)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 3, 10

**Representative device:** Braun ThermoScan 7 IRT6520 (generic)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Height x width x depth | 145 x 35 x 52 mm | [Braun ThermoScan 7 listing](https://www.multitronic.fi/en/products/4508973/braun-thermoscan-7-age-precision-irt6520bwe-ear-thermometer---black) | listing | exact |
| Weight | 120 g | [same](https://www.multitronic.fi/en/products/4508973/braun-thermoscan-7-age-precision-irt6520bwe-ear-thermometer---black) | listing | exact |
| Range | 34 to 42.2 degC, 3 s measurement | [same](https://www.galaxus.de/en/s6/product/thermoscan-pi-ear-thermometer-7-irt-6520-ear-medical-thermometers-18520032) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Handle with a probe tip and a probe cover, display, start button.

## Materials and colours

White body, grey tip.

## States

- off
- ready
- reading shown
- tip cover on/off

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Reading display
- Trainee does: Inserts in ear
- System response: Reading in °C on device display
- Failure or scenario: < 36.0 → warm before theatre
- Asset needs (named points): `SCREEN_`, `GRAB_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- None.
