# 41: Anaesthesia machine (with ventilator) + check log (equipment-list #41)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 13

**Representative device:** Draeger Perseus A500 (generic)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Height x width x depth | 1480 x 1150 x 790 mm | [Perseus A500 listing](https://www.somatechnology.com/pdfFiles/Drager-Perseus-A500-anesthesia-machine.pdf) | listing | exact |
| Weight | 163 kg | [same](https://www.somatechnology.com/anesthesia-machines/drager-perseus-a500) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Trolley with a ventilator, a breathing system, vaporisers, a screen, gas supply hoses, a work surface and drawers.

## Materials and colours

Grey and blue.

## States

- off
- checked
- not checked
- ventilating
- alarm

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Check done/not; settings display
- Trainee does: Opens check log; confirms machine check done (with anaesthetist NPC)
- System response: Screen shows self-test / ready; check log status → done; Sign In item "anaesthesia machine check complete" can be confirmed
- Failure or scenario: Check not done → Sign In fails
- Asset needs (named points): `SCREEN_Main`, `GRAB_CheckLog`, drawers `PIVOT_`

## Triangle-budget class

High

## Open points (nothing here is guessed)

- Check-log layout
