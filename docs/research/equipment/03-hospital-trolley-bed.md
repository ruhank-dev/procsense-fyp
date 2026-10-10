# 03: Hospital trolley / bed (equipment-list #3)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** ward/ED · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 1–12

**Representative device:** Hill-Rom VersaCare hospital bed (generic shape)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Sleep surface width x length | 902 x 2180 mm (35.5 x 86 in) | [Hill-Rom VersaCare spec sheet 178171](https://assets.hillrom.com/is/content/hillrom/178171-EN-r3_VersaCare_SpecSheet-LRpdf-2) | listing | exact |
| Overall width | 940 mm rails stowed; 1020 mm rails up | [same](https://assets.hillrom.com/is/content/hillrom/178171-EN-r3_VersaCare_SpecSheet-LRpdf-2) | listing | exact |
| Overall length | 2100 to 2400 mm (retracted to extended) | [same](https://assets.hillrom.com/is/content/hillrom/178171-EN-r3_VersaCare_SpecSheet-LRpdf-2) | listing | exact |
| Deck height | 330 to 430 mm low; 760 to 860 mm high | [same](https://assets.hillrom.com/is/content/hillrom/178171-EN-r3_VersaCare_SpecSheet-LRpdf-2) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Mattress deck with head and knee sections, four side rails, head and foot boards, four castors with a brake, a patient-handling control.

## Materials and colours

Grey painted steel frame, white or grey rails, blue wipe-clean mattress.

## States

- flat
- head up
- rails up/down
- brake on/off

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Head-up / flat positions (`PIVOT_*`), wheels, rails
- Trainee does: Sets head-up / flat (flat for low BP)
- System response: Bed section animates; patient pose follows
- Failure or scenario: —
- Asset needs (named points): `PIVOT_` head section, control

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- None.
