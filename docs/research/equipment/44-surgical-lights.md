# 44: Surgical lights (equipment-list #44)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 14

**Representative device:** Draeger Polaris 600 ceiling light (generic)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Lighthead diameter | 785 mm | [Draeger Polaris 600 specification as listed by a distributor](https://remma.fr/en/model/surgical-lamp-polaris-600) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Ceiling suspension, articulated arm, a dome with an LED array, a handle, a wall control panel.

## Materials and colours

White dome, grey arm.

## States

- off
- on (intensity step)
- aimed

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Arms (`PIVOT_*`), light emitters
- Trainee does: Presses on/off and intensity on the wall/arm panel; grabs handle to aim
- System response: Light turns on, beam + spot on table, intensity steps; arm rotates within limits
- Failure or scenario: (Environment item in WT; operable by user request)
- Asset needs (named points): `PIVOT_` arms, `GRAB_Handle`, emissive + spotlight, control buttons

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- Illuminance figures from the data sheet
