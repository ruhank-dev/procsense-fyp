# monitor_on_stand: Vital-sign monitor on stand (equipment-list #9)

**Status:** DRAFT · **Date sheet written:** 10 October 2026 UTC · **Representative device:** Philips IntelliVue MX450 (bedside, 12 inch). Generic shape, no logo in the model.

## Dimensions
| Part | Value | Source opened (date) | Exact / approx. |
|---|---|---|---|
| Monitor body height | 288 mm (11.34 in) | Philips IntelliVue MX450 specification sheet, https://www.somatechnology.com/pdfFiles/Philips-IntelliVue-MX450-Patient-Monitor.pdf, page 2, opened 10 Oct 2026 | Exact (manufacturer figure via a distributor copy of the sheet) |
| Monitor body width | 327 mm (12.87 in) | same | Exact |
| Monitor body depth | 182 mm (7.17 in) | same | Exact |
| Weight | 5.9 kg (12.9 lb) | same | Exact |
| Display | 12 inch WXGA colour touch screen, 310 mm diagonal; useful screen 261.1 mm x 163.2 mm; pixel 0.204 mm | same | Exact |
| Waveform sweep speeds offered | 6.25, 12.5, 25 and 50 mm/s | same | Exact |
| Stand / trolley height and base | not set | no stand datasheet opened | **Open** |

## Components
Body with touch screen; handle; sockets for ECG leads, SpO2 probe, NIBP hose and temperature (the equipment list names SpO2, NIBP, ECG, temperature); alarm light and speaker. Sensors are separate assets (items 10, 11).

## Materials and colours
Light grey plastic body, dark screen bezel (typical; the sheet gives none): **approx., choose neutral, no logo**.

## States
Off · on with live values · alarm (visual + audio) · sensor not attached (value shown as "--") · signal lost.

## Interaction points
`SCREEN_main` (live values and traces from Core), `ATTACH_ecg`, `ATTACH_spo2`, `ATTACH_nibp`, `ATTACH_temp`, alarm silence button.

## Triangle-budget class
Medium (screen is the detail; body is simple).

## Open points
Stand dimensions; real alarm sound design; exact colours.
