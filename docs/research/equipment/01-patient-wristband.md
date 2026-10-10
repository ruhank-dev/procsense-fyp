# 01: Patient wristband (identity) (equipment-list #1)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 1, all

**Representative device:** Zebra Z-Band Direct adult identification band (generic, no logo)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Band length x width | 279 x 25 mm (1 x 11 in; some listings 279.4 x 25.4) | [Zebra Z-Band Direct adult band, labelzone.co.uk listing](https://www.labelzone.co.uk/products/zebra-z-band-direct-wristband-25-x-279mm-white-box-of-6-rolls) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

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

- NPSA millimetre sizes for adult, child and neonatal bands were not found; the Zebra adult band is the representative size
