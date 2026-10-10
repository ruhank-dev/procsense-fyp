# 50: Resuscitation trolley + defibrillator + emergency drugs (representation) (equipment-list #50)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** all · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** all

**Representative device:** Philips HeartStart XL+ defibrillator on a resuscitation trolley (representation)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Defibrillator height x width x depth | 230 x 296 x 279 mm, 6.6 kg | [HeartStart XL+ listing](https://somatechnology.com/defibrillators/philips-heartstart-xl-plus) | listing | exact |
| Trolley (typical) | about 1100 x 800 x 700 mm, or a slim 1550 x 675 x 400 mm model | [supplier listings](https://org.gem.gov.in/cart/cart_global/directBuy/135863) | listing | approx |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Trolley with labelled drawers, a defibrillator on top with pads, an emergency drug box with tamper tags (representation only).

## Materials and colours

Red trolley, grey defibrillator.

## States

- checked (tag intact)
- not checked
- in use

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Checked; used in emergencies
- Trainee does: Checks trolley (seal/drawers); uses in emergency branch
- System response: Drawer contents visible; defib screen on; emergency NPCs respond
- Failure or scenario: Not checked → delay in emergency
- Asset needs (named points): Drawers `PIVOT_`, `SCREEN_`

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- Drawer layout
