# 09: Vital-sign monitor (SpO2, NIBP, ECG, temp) (equipment-list #9)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED/theatre · **Scenarios (S1 / S2):** ✓ / ✓ · **Steps:** 3, 8, 12–14

**Representative device:** Philips IntelliVue MX450 (generic shape, no logo)

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Body height x width x depth | 288 x 327 x 182 mm (11.34 x 12.87 x 7.17 in) | [Philips IntelliVue MX450 specification sheet, p.2](https://www.somatechnology.com/pdfFiles/Philips-IntelliVue-MX450-Patient-Monitor.pdf) | datasheet | exact |
| Weight | 5.9 kg | [same](https://www.somatechnology.com/pdfFiles/Philips-IntelliVue-MX450-Patient-Monitor.pdf) | datasheet | exact |
| Display | 12 in WXGA colour touch screen, useful screen 261.1 x 163.2 mm | [same](https://www.somatechnology.com/pdfFiles/Philips-IntelliVue-MX450-Patient-Monitor.pdf) | datasheet | exact |
| Waveform sweep speeds | 6.25, 12.5, 25 and 50 mm/s | [same](https://www.somatechnology.com/pdfFiles/Philips-IntelliVue-MX450-Patient-Monitor.pdf) | datasheet | exact |
| Roll stand | overall height 116.8 cm (46 in) with a 63.5 cm (25 in) base; a shorter model is 100.3 cm | [GCX roll stand kit for Philips IntelliVue MX400 to MX550](https://www.gcx.com/parts/1056070) | listing | exact |
| Alarm tone design | each pulse has a fundamental frequency of 150 to 1000 Hz; a high-priority burst has 10 fast pulses, a medium-priority burst 3 slower pulses, a low-priority burst 1 or 2 | [IEC 60601-1-8 as summarised by MDDI and Same Sky](https://www.mddionline.com/components/audible-alarms-in-medical-equipment) | standard | exact |

Levels: **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **datasheet** = manufacturer data sheet opened; **unpublished** = the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate; **design** = a user-interface, layout or content item with no physical dimension; defined by the project; **open** = not found.

## Components

Body with touch screen and handle; sockets for ECG, SpO2, NIBP, temperature; alarm light and speaker. Sensors are items 10 and 11.

## Materials and colours

Light grey plastic, dark bezel; no logo.

## States

- off
- on with live values
- alarm (visual and audio)
- sensor not attached ('--')
- signal lost

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Live values + traces on `SCREEN_*`; lead/cable `ATTACH_*`; alarms
- Trainee does: Attaches leads, SpO2 probe, BP cuff; reads values; hears alarms; silences/acknowledges alarm
- System response: Values appear only for attached sensors; live traces; alarms at thresholds; artefact if probe poorly placed
- Failure or scenario: Alarm ignored → deterioration continues
- Asset needs (named points): `SCREEN_`, `ATTACH_` lead ports
- Trainee does: Attaches sensors; reads values
- System response: As OT monitor
- Failure or scenario: Artefact if probe poor
- Asset needs (named points): `SCREEN_`, `ATTACH_`

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- None.
