# 01: Patient wristband (identity) (equipment-list #1)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 1, all

**Representative device:** Zebra Z-Band Direct adult identification band (generic, no logo)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Band length x width | 279 x 25 mm (1 x 11 in; some listings 279.4 x 25.4) | [Zebra Z-Band Direct adult band, labelzone.co.uk listing](https://www.labelzone.co.uk/products/zebra-z-band-direct-wristband-25-x-279mm-white-box-of-6-rolls) | listing | exact |
| Band colour and content (NPSA) | white band with black text; core identifiers last name, first name, date of birth and NHS number | [NPSA Safer Practice Notice 24, Standardising wristbands improves patient safety (2007), as published by Brenmoor](https://brenmoor.com/wp-content/uploads/2025/02/standardising-wristbands-improves-patient-safety.pdf) | listing | exact |
| Band length and width in mm | the NPSA notice sets colour and content, not millimetres; no national size is published, the Zebra adult band is the representative size | [NPSA SPN 24 (read) and the Zebra Z-Band data](https://brenmoor.com/wp-content/uploads/2025/02/standardising-wristbands-improves-patient-safety.pdf) | unpublished | approx |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Strap with adhesive closure; a printed or label panel with surname, first name, date of birth and hospital number.

## Materials and colours

White with black text for an identity band; the allergy band is a separate red band (the colour is an institutional choice, DT list). The NHS/NPSA standard asks for white bands with black text (not opened).

## States

- present and correct
- missing
- wrong patient (name or number differs)
- illegible

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Rotate to read; name, DOB, MR number readable (`LABEL_*`)
- Trainee does: Rotates band to read name/DOB/MRN; compares with spoken answer + notes
- System response: Text readable (live data); identity-verified state when matched
- Failure or scenario: Mismatch → stop; missing band → apply after verification
- Asset needs (named points): `LABEL_`, wrist `SOCKET_`

## Triangle-budget class

Very low

## Open points (nothing here is guessed)

- Band length and width in mm: not published by the manufacturer (a design size is used, approximate)
