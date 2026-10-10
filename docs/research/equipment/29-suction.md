# 29: Suction (ward and theatre) (equipment-list #29)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** ward/theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 13

**Representative device:** Laerdal Compact Suction Unit 4 (portable) and a wall suction regulator with jar

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Portable unit | 185 x 262 x 81 mm (7.3 x 10.3 x 3.2 in), 1.53 kg, 300 mL canister version; 30 L/min | [Laerdal LCSU 4 specification as listed by St John Ambulance and distributors](https://shop.sja.org.uk/products/laerdal-compact-suction-unit-lcsu-4) | listing | exact |
| Wall regulator with safety jar | 230 x 70 x 90 mm (H x W x D) | [RVTM3 listing](https://storage.mtender.gov.md/get/82735822-42c3-4eaa-9cbd-c8fb46ecc789-1646984995908) | listing | approx |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Collection jar, tubing, a rigid (Yankauer) tip, a regulator with a gauge.

## Materials and colours

Clear jar, white regulator, clear tubing.

## States

- off
- suction on
- working
- not working (fault)
- jar full

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Check working (state)
- Trainee does: Turns on, occludes tubing to test
- System response: Gauge rises / sound plays; state = working or fault (scenario)
- Failure or scenario: Not working → RSI unsafe
- Asset needs (named points): Switch, `ATTACH_Tube`, gauge needle `PIVOT_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- None.
