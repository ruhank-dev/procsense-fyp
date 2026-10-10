# 43: Airway equipment (laryngoscope, tubes) + difficult-airway trolley (equipment-list #43)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D · **Room:** theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 11, 13

**Representative device:** Macintosh size 3 laryngoscope, cuffed tracheal tube 7.5 mm, difficult-airway trolley

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Macintosh 3 blade length | 110 to 137 mm depending on maker (126 mm fibre-optic) | [blade listings](https://shop.cardinalhealth.ca/chc/macintosh-fiber-optic-laryngoscope-blade) | listing | approx |
| Cuffed tube 7.5 mm | internal diameter 7.5 mm, outer 10.5 mm, length 310 mm | [KRUUSE tube listing](https://shop.kruuse.com/CVT-INT/en/EUR/product/272029) | listing | exact |
| Difficult-airway trolley | 650 x 480 x 1070 mm with six drawers (typical); other makers 550 x 550 x 993 mm or 615 x 500 x 915 mm | [supplier listings (Vetequip, Bristol Maid Caretray)](https://bristolmaid.com/default/caretray-trolley-airway-difficult-intubation.html) | listing | approx |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Handle with a light, curved blade, tubes in several sizes, a bougie, a face mask and a bag, a trolley with drawers.

## Materials and colours

Steel blade and handle, clear tubes.

## States

- drawer closed
- drawer open (contents visible)
- item missing

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Confirm availability; drawers
- Trainee does: Opens drawers, confirms laryngoscope/tubes present + working (light on)
- System response: Drawer slides; items highlight on inspect; laryngoscope light turns on when blade opened
- Failure or scenario: Difficult airway not declared → Sign In issue
- Asset needs (named points): Drawers `PIVOT_`, `GRAB_` per item, blade hinge

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- None.
