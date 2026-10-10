# 48: Instrument trays (lap chole / lap + open appendix) (equipment-list #48)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 14

**Representative device:** DIN 1/1 sterilisation container with lap-chole and appendectomy sets

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Container footprint | 600 x 300 mm (DIN 1/1) | [DIN 58952 container module (recalled; Aesculap catalogue pages confirm the 1/1 container)](https://api.dispatcher.qas.cw.bbraun.com/content/dam/aesculap-us/us/website/aesculap-implant_systems/company/DOC1647-SterilContainer-JS-Series-GTIN-Data-Sheet.pdf) | listing | approx |
| Container heights | 90, 120, 135, 187 and 200 mm | [Aesculap SterilContainer JS series](https://api.dispatcher.qas.cw.bbraun.com/content/dam/aesculap-us/us/website/aesculap-implant_systems/company/DOC1647-SterilContainer-JS-Series-GTIN-Data-Sheet.pdf) | listing | exact |
| Laparoscopic cholecystectomy and appendicectomy set contents | Maryland dissector, atraumatic grasper, Metzenbaum scissors, suction and irrigation, monopolar hook, clip applier, trocars with reducers, Veress needle; sets vary by trust and surgeon preference | [NHS trust instrument-tray documents, Molnlycke laparoscopic tray components and operating-theatre set-up references](https://belfasttrust.hscni.net/download/835/june/25612/37416-laparoscopic-cholecystectomy-instrument-trays.pdf) | listing | exact |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Lidded container, an instrument basket, sterility indicators.

## Materials and colours

Anodised aluminium, silver.

## States

- sealed
- indicator changed
- opened

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Sterility indicators
- Trainee does: Opens tray, checks sterility indicator colour
- System response: Indicator shows pass/fail (scenario); fail → must report
- Failure or scenario: Indicator fail → stop
- Asset needs (named points): Lid `PIVOT_`, indicator material swap

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- None.
