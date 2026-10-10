# 15: Capillary glucose/ketone meter (equipment-list #15)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** cond. / cond. · **Steps:** 3, 8

**Representative device:** Accu-Chek Guide meter (generic)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Length x width x height | 76 x 48 x 16 mm | [Accu-Chek Guide listing](https://www.digitec.ch/en/s1/product/accu-chek-guide-blood-tests-30674604) | listing | exact |
| Weight | 43 g with batteries | [same](https://www.astleymobility.com.au/accu-chek-guide-me-blood-glucose-meter) | listing | exact |
| Sample / time | 0.6 microlitre / under 4 s | [same](https://www.healthline.com/diabetesmine/roche-accu-chek-guide-meter) | listing | exact |
| Glucose and ketone meter (FreeStyle Optium Neo) | 87 x 60 x 9 mm, 36 g; reads glucose and blood beta-ketone with separate strips; 0.6 microlitre sample | [Abbott FreeStyle Optium Neo specifications on distributor and teardown pages](https://ts.ifixit.com/Device/Abbott_FreeStyle_Optium) | listing | exact |
| Alternative: Nova Max Plus | 91.4 x 58.4 x 22.9 mm, 75 g; becomes a ketone meter with the ketone strip (0.8 microlitre) | [Nova Max Plus consumer guide listing](https://consumerguide.diabetes.org/products/nova-max-plus) | listing | exact |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Meter with a strip port and a display, test strips, a lancing device.

## Materials and colours

Grey or white plastic, orange strip port.

## States

- off
- strip inserted
- result shown
- error

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Reading display
- Trainee does: Finger-prick test
- System response: Glucose / ketone value on display
- Failure or scenario: Hypoglycaemia missed
- Asset needs (named points): `SCREEN_`

## Triangle-budget class

Low

## Open points (nothing here is guessed)

- None.
