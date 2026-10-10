# iv_pole_and_pump: IV pole and infusion pump (equipment-list #26, #27)

**Status:** DRAFT · **Date sheet written:** 10 October 2026 UTC · **Representative device:** B. Braun Infusomat Space volumetric pump on a generic five-castor IV pole. No logo in the model.

## Dimensions
| Part | Value | Source opened (date) | Exact / approx. |
|---|---|---|---|
| Pump body (W x H x D) | 214 x 68 x 124 mm | B. Braun Infusomat Space data sheet, https://www.somatechnology.com/pdfFiles/B-Braun-Infusomat-Space-Infusion-System.pdf, page 2, opened 10 Oct 2026 | Exact |
| Pump weight | approx. 1.4 kg | same | Exact (as stated: "approx.") |
| Pole clamp | holds up to three pumps, mounted horizontally or vertically | same, page 1 | Exact (clamp diameter range not in this sheet) |
| Pole outer diameter | about 38 mm outer tube, 25 mm inner tube (telescopic) | **Not opened.** Seen only in search-result summaries of supplier listings | **Approx., unverified** |
| Pole height range | about 1.1 to 2.0 m | same | **Approx., unverified** |
| Base | five castors, about 560 to 600 mm across | same | **Approx., unverified** |

## Components
Pole (telescopic tube, hook head with 2 to 4 hooks, five-leg base with castors, height lock), pump (body, display, keys, door for the giving set, pole clamp), fluid bag hanging from a hook, giving set line.

## Materials and colours
Pole: stainless or chrome-plated steel with grey/white plastic base (typical). Pump: light grey/white with dark display. **Approx.**

## States
Pump: off · on and idle · infusing (rate shown) · alarm (occlusion, bag empty) · battery low.
Pole: height locked/unlocked; hooks empty/occupied.

## Interaction points
`SOCKET_hook_1..2` (bag), `SOCKET_pump_clamp`, `SCREEN_pump` (rate from Core), `PIVOT_height`.

## Triangle-budget class
Low (pole) and medium (pump).

## Open points
Pole figures need one supplier datasheet actually opened (the suppliers' pages blocked or redirected the automatic reader on 10 Oct 2026). Pump display layout.
