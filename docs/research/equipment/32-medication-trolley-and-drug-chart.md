# 32: Medication trolley / drug chart (equipment-list #32)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 8

**Representative device:** Bristol Maid pharmacy trolley with drawers

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Width x depth x height | 511 x 542 x 1072 mm | [Bristol Maid pharmacy trolley listing](https://dentalworld.sigmasolve.com/p/furniture/bristol-maid-controlled-drug-cabinets/pharmacy-trolley-trolley-drug-dispensing-patient-double-door-bolt-lock-18-a-12-c-trays) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Locked trolley with drawers, a worktop, four castors, and a clipboard for the drug chart.

## Materials and colours

Grey or white powder-coated steel.

## States

- locked
- unlocked
- drawer open

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Drawers (`PIVOT_*`); chart UI
- Trainee does: Requests medicine; views confirmed prescription
- System response: Request → clinician confirms → nurse administers; allergen request → nurse challenge
- Failure or scenario: Request for an allergen → challenge
- Asset needs (named points): Drawers, chart UI

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- Drug chart layout is a UI item
