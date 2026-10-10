# 42: Capnograph (equipment-list #42)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 13

**Representative device:** End-tidal CO2 module of the monitor (item 9) or the anaesthesia machine (item 41)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Separate device | none: a module of items 9 and 41 | equipment list note 'often part of monitor' | open | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

A waveform and value on the monitor; a sampling line.

## Materials and colours

See items 9 and 41.

## States

- no waveform
- waveform present
- absent after intubation (alarm)

## Interaction (from the equipment list and the interaction spec)

- Equipment list: EtCO2 waveform (often part of monitor)
- Trainee does: Looks at EtCO2 after induction (abstracted)
- System response: Waveform + value from Core
- Failure or scenario: —
- Asset needs (named points): `SCREEN_`

## Triangle-budget class

None (screen element)

## Open points (nothing here is guessed)

- Sampling line length
- Separate device: no source found
