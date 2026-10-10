# 13: 12-lead ECG machine + electrodes (equipment-list #13)

**Status:** DRAFT, approved for training on the owner's statement · **Written:** 10 October 2026 · **Tier:** P0 · **Kind:** 3D+UI · **Room:** ward/ED · **Scenarios (S1 / S2):** cond. / cond. · **Steps:** 6–7

**Representative device:** Philips PageWriter TC30 ECG machine with 3M Red Dot electrodes

## Dimensions (every number has a source)

| Part | Value | Source (date opened or seen: 10 October 2026) | Level | Exact / approx |
|---|---|---|---|---|
| Machine height x width x depth | 205 x 310 x 405 mm | [Philips PageWriter TC30 listing](https://somatechnology.com/ekg-machines/philips-pagewriter-tc30) | listing | exact |
| Machine weight | 8.6 kg | [same](https://www.somatechnology.com/pdfFiles/Philips-PageWriter-TC30-EKG-Machine.pdf) | listing | exact |
| Display | 10 in touch screen | [same](https://somatechnology.com/ekg-machines/philips-pagewriter-tc30) | listing | exact |
| Electrode (Red Dot 2670-3) | 1.5 x 1.75 in (about 38 x 44 mm), silver/silver-chloride | [3M Red Dot listing](https://mms.mckesson.com/product/486607/Solventum-Corporation-2670-3) | listing | exact |
| Electrodes for a 12-lead ECG | 10 (4 limb, 6 chest) | [Standard 12-lead layout](https://myshare-cms.siemens.com/12-lead-ecg-placement) | listing | exact |

Levels: **datasheet** = manufacturer data sheet opened; **listing** = manufacturer specification as printed on a distributor or catalogue page; **standard** = published standard; **open** = not found.

## Components

Cart or tabletop machine with a screen and printer, a patient cable with ten leads, ten adhesive electrodes.

## Materials and colours

Light grey machine, black cable, white electrodes with a blue or green snap.

## States

- off
- on
- leads off
- recording
- printout ready

## Interaction (from the equipment list and the interaction spec)

- Equipment list: Place 10 electrodes; printout
- Trainee does: Places 10 electrodes; reads printout
- System response: Electrodes snap to chest/limb sockets; printout appears; machine read-out may be wrong (WT)
- Failure or scenario: Wrong placement → misleading trace
- Asset needs (named points): 10 body `SOCKET_`s, printout prop

## Triangle-budget class

Medium

## Open points (nothing here is guessed)

- Printout paper width
