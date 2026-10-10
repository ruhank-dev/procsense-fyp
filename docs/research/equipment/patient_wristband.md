# patient_wristband: Patient identity wristband (equipment-list #1)

**Status:** DRAFT · **Date sheet written:** 10 October 2026 UTC · **Representative device:** generic adult white identification band (no logo).

## Dimensions
| Part | Value | Source opened (date) | Exact / approx. |
|---|---|---|---|
| Band length and width | not set | **No source opened.** Search results confirmed that the NHS (NPSA) standard has three sizes (adult, paediatric, neonatal) but gave no millimetre figures; the Australian standard page was reachable only as a description (colour, size, comfort, usability, recording of identifiers), not as numbers | **Open: do not model to a size until one datasheet is opened** |
| Text a band must carry | surname, first name, date of birth, NHS or hospital number | NHS/NPSA requirement as summarised in search results; primary document not opened | Approx. (wording), unverified |
| Colour | white with black text for identity bands | same | Approx., unverified |

## Components
Strap with adhesive or snap closure; printed or labelled panel (name, date of birth, MR number) readable by rotating the wrist.

## States
Present and correct · missing · wrong patient (name or number differs) · illegible. These feed the identity-check tasks (walkthrough step 1).

## Interaction points
`LABEL_name`, `LABEL_dob`, `LABEL_mrn` (readable text from the case file).

## Triangle-budget class
Very low.

## Open points
Size; closure type; allergy band colour (an institutional choice, DT list).
