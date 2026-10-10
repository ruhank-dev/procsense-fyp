# 26: IV giving set + fluid bags (+ IV pole) (equipment-list #26)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ED · **Scenarios (S1 / S2):** — / ✓ · **Steps:** 8

**Representative device:** 1 L fluid bag, giving set, and a Hidemar H166 style telescopic pole

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Pole height range | 1120 to 2000 mm | [Hidemar IV pole on casters H166 (MedicalExpo catalogue)](https://www.medicalexpo.com/prod/hidemar/product-68739-483238.html) | listing | exact |
| Pole base / castors | 600 mm diameter; five swivel castors of 50 mm, two with brakes | [same](https://www.medicalexpo.com/prod/hidemar/product-68739-483238.html) | listing | exact |
| Pole material | stainless steel 18/10, four hooks | [same](https://www.medicalexpo.com/prod/hidemar/product-68739-483238.html) | listing | exact |
| 1 L bag size | not found (a 500 mL bag is about 177 x 136 x 54 mm in its carton listing) | [Baxter Viaflex catalogue listings](https://www.yesmed.com.au/brands/baxter/baxter-viaflex-iv-solutions-0-9-sodium-chloride-500ml.html) | open | approx |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Pole (telescopic tube, hook head, five-leg base, height lock), bag with a label, giving set with a drip chamber and a roller clamp.

## Materials and colours

Stainless pole, grey base, clear bag with a printed label.

## States

- empty pole
- bag hung
- running
- bag empty

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Hang bag (`SOCKET_` on pole); label shows fluid/volume
- Trainee does: Hangs bag (fluid requested + confirmed via workflow)
- System response: Bag on hook; label shows fluid/volume; drip running
- Failure or scenario: Fluid overload in heart failure
- Asset needs (named points): Hook `SOCKET_`, `LABEL_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- Exact bag dimensions
- Pole figures come from a catalogue listing, not a datasheet
- 1 L bag size: no source found
