# 11: Pulse oximeter probe (equipment-list #11)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** ward/ED/theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 3, 13

**Representative device:** Nonin Onyx II 9550 fingertip oximeter (generic)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Body height x width x depth | 56 x 33 x 32 mm | [Nonin 9550 Onyx II listing](https://www.wms.co.uk/Equipment/Diagnostic/Pulse-Oximeters/Nonin-9550-Onyx-II-Finger-Pulse-Oximeter/p/W6415) | listing | exact |
| Finger thickness accepted | 8 to 26 mm | [same](https://www.wms.co.uk/Equipment/Diagnostic/Pulse-Oximeter/Nonin-9550-Onyx-II-Finger-Pulse-Oximeter/p/W6415) | listing | exact |
| Weight | 53.5 g with batteries | [same](https://www.medi-shop.gr/en/oximeters/finger-oximeter-onyx-ii-9550) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Clip body with a small display; for the monitor probe version a cable and connector.

## Materials and colours

Dark grey plastic, black rubber grip.

## States

- off
- on and reading
- poor signal
- not on a finger

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Clip on finger; cable
- Trainee does: Clips on finger; checks waveform
- System response: SpO2 + pulse + pleth; no waveform → recheck
- Failure or scenario: —
- Asset needs (named points): Finger `SOCKET_`, cable

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- None.
