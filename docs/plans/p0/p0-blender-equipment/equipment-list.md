# Equipment & props list

**Source:** Walkthrough **Part E — Equipment and Asset Mapping** (53 items transcribed, read 07 October 2026).
"Steps" = the walkthrough's Part 11 step numbers where the item appears. Tier: items used only in Step 15 → P1; everything else → P0.
"Kind": **3D** = Blender script · **UI** = in-VR screen/panel · **3D+UI** = model with a live screen/label · **B** = door transition (no room) · **C** = EMR computer only.
What the trainee does with each item and how it responds: `../p0-vr-interaction-ui/equipment-interaction-spec.md`.
Rooms (see `../p0-environment-rooms/`): "ward/ED" = the pre-op **patient bay** (A); "theatre" = **OT** (A); imaging/CT/lab = **doors** (B).

## Patient identification & bedside

| # | Item | S1 / S2 | Steps | Tier | Kind | Room | Interaction / detail needed |
|---|---|---|---|---|---|---|---|
| 1 | Patient wristband (identity) | ✓ / ✓ | 1, all | P0 | 3D+UI | ward/ED | Rotate to read; name, DOB, MR number readable (`LABEL_*`) |
| 2 | Allergy band | ✓ / ✓ | 1, 7, 13 | P0 | 3D | ward/ED | Colour flag (colour = institutional choice) |
| 3 | Hospital trolley / bed | ✓ / ✓ | 1–12 | P0 | 3D | ward/ED | Head-up / flat positions (`PIVOT_*`), wheels, rails |
| 4 | Wheelchair | ✓ / — | 1, 6 | P0 | 3D | ward | Optional transport choice |
| 5 | Patient notes / folder | ✓ / ✓ | 1–12 | P0 | 3D+UI | ward/ED | Opens to UI notes |
| 6 | Observation (NEWS2) chart | ✓ / ✓ | 3 | P0 | UI (+3D clipboard) | ward/ED | Trend + score |
| 7 | Consent form | ✓ / ✓ | 9 | P0 | 3D+UI | ward/ED | Procedure, risks, signatures (local form) |
| 8 | PPE: gloves, apron, mask; theatre cap, gown | ✓ / ✓ | all | P0 | 3D | all | Don items; dispensers |

## Monitoring & examination

| # | Item | S1 / S2 | Steps | Tier | Kind | Room | Interaction / detail needed |
|---|---|---|---|---|---|---|---|
| 9 | Vital-sign monitor (SpO2, NIBP, ECG, temp) | ✓ / ✓ | 3, 8, 12–14 | P0 | 3D+UI | ward/ED/theatre | Live values + traces on `SCREEN_*`; lead/cable `ATTACH_*`; alarms |
| 10 | BP cuff (NIBP) | ✓ / ✓ | 3 | P0 | 3D | ward/ED | Wrap arm (`SOCKET_` on arm); hose to monitor; sizes |
| 11 | Pulse oximeter probe | ✓ / ✓ | 3, 13 | P0 | 3D | ward/ED/theatre | Clip on finger; cable |
| 12 | Thermometer (tympanic) | ✓ / ✓ | 3, 10 | P0 | 3D+UI | ward/ED | Reading display |
| 13 | 12-lead ECG machine + electrodes | cond. / cond. | 6–7 | P0 | 3D+UI | ward/ED | Place 10 electrodes; printout |
| 14 | Stethoscope | ✓ / ✓ | 4 | P0 | 3D | ward/ED | Auscultation; grab + place |
| 15 | Capillary glucose/ketone meter | cond. / cond. | 3, 8 | P0 | 3D+UI | ward/ED | Reading display |

## Samples & tests

| # | Item | S1 / S2 | Steps | Tier | Kind | Room | Interaction / detail needed |
|---|---|---|---|---|---|---|---|
| 16 | Blood collection set + tourniquet | ✓ / ✓ | 6 | P0 | 3D | ward/ED | Take blood after ID check |
| 17 | Specimen tubes (colour-coded) + labels | ✓ / ✓ | 6 | P0 | 3D+UI | ward/ED | Label at bedside (`LABEL_*`); colour caps |
| 18 | Blood culture bottles | — / cond. | 6, 8 | P0 | 3D | ED | Fill before antibiotics |
| 19 | Urine pot + dipstick; pregnancy test kit | cond. / ✓ | 6 | P0 | 3D+UI | ward/ED | Dipstick colours; hCG result |
| 20 | Blood gas analyser (result display) | — / cond. | 7 | P0 | **C** (EMR) | — | Result in EMR only |
| 21 | Results display / lab report | ✓ / ✓ | 7 | P0 | **C** (EMR) + printed report | bay/OT desk | Flags, lab comments; critical call still by phone |
| 22 | Ultrasound machine / report | cond. / ✓ | 6–7 | P0 | **B** door → report + images in EMR | Imaging door | No ultrasound room/machine modelled |
| 23 | CT scanner (representation) / report | cond. / cond. | 6–7 | P0 | **B** door → report (+ key images in EMR) | CT door | **Decided:** report-only / transition (no CT room) |
| 24 | MRCP / ERCP (report only) | cond. / — | 7 | P0 | **B/C** report | MRCP door / EMR | Report only |

## Treatment & access

| # | Item | S1 / S2 | Steps | Tier | Kind | Room | Interaction / detail needed |
|---|---|---|---|---|---|---|---|
| 25 | IV cannula kit | ✓ / ✓ | 8 | P0 | 3D | ward/ED | Insertion abstracted |
| 26 | IV giving set + fluid bags (+ IV pole) | — / ✓ | 8 | P0 | 3D+UI | ED | Hang bag (`SOCKET_` on pole); label shows fluid/volume |
| 27 | Infusion pump | cond. / cond. | 8 | P0 | 3D+UI | ward/ED | Rate display; alarm |
| 28 | Oxygen supply, nasal cannula, face masks (simple, non-rebreather) | cond. / cond. | 8, 12 | P0 | 3D | ward/ED/transfer | Select device; wall outlet/flowmeter |
| 29 | Suction (ward and theatre) | ✓ / ✓ | 11, 13 | P0 | 3D | ward/theatre | Check working (state) |
| 30 | Nasogastric tube | — / cond. | 8 | P0 | 3D | ED | Senior-directed |
| 31 | Urinary catheter + urometer | — / cond. | 8 | P0 | 3D | ED | Senior-directed |
| 32 | Medication trolley / drug chart | ✓ / ✓ | 8 | P0 | 3D+UI | ward/ED | Drawers (`PIVOT_*`); chart UI |
| 33 | Sharps bin | ✓ / ✓ | 6, 8 | P0 | 3D | ward/ED | Dispose needles (`SOCKET_` opening) |
| 34 | Anti-embolism stockings / IPC device | ✓ / ✓ | 8, 10 | P0 | 3D | ward/ED | Fit if indicated |

## Preparation & transfer

| # | Item | S1 / S2 | Steps | Tier | Kind | Room | Interaction / detail needed |
|---|---|---|---|---|---|---|---|
| 35 | Clippers | cond. / cond. | 10 | P0 | 3D | ward/ED | Clippers not razor |
| 36 | Skin marker | cond. / cond. | 10 | P0 | 3D | ward/ED | Surgeon marks (policy) |
| 37 | Forced-air warming device | ✓ / ✓ | 10–11 | P0 | 3D | ward/theatre | Apply if < 36 °C or long case |
| 38 | Transfer trolley + portable monitor + oxygen cylinder | ✓ / ✓ | 12 | P0 | 3D+UI | corridor | Monitoring by stability |
| 39 | Handover sheet (SBAR) | ✓ / ✓ | 12 | P0 | UI | transfer | SBAR delivery |

## Theatre

| # | Item | S1 / S2 | Steps | Tier | Kind | Room | Interaction / detail needed |
|---|---|---|---|---|---|---|---|
| 40 | Operating table + positioning padding | ✓ / ✓ | 11 | P0 | 3D | theatre | Positions (`PIVOT_*`) |
| 41 | Anaesthesia machine (with ventilator) + check log | ✓ / ✓ | 11, 13 | P0 | 3D+UI | theatre | Check done/not; settings display |
| 42 | Capnograph | ✓ / ✓ | 11, 13 | P0 | 3D+UI | theatre | EtCO2 waveform (often part of monitor) |
| 43 | Airway equipment (laryngoscope, tubes) + difficult-airway trolley | ✓ / ✓ | 11, 13 | P0 | 3D | theatre | Confirm availability; drawers |
| 44 | Surgical lights | ✓ / ✓ | 11, 14 | P0 | 3D | theatre | Arms (`PIVOT_*`), light emitters |
| 45 | Laparoscopic tower (camera, light source, monitor, insufflator) | ✓ / ✓ | 11, 14 | P0 | 3D+UI | theatre | Fault messages; pressure/flow display |
| 46 | CO2 cylinder (+ spare) | ✓ / ✓ | 11 | P0 | 3D+UI | theatre | Gauge level |
| 47 | Electrosurgical (diathermy) unit + return electrode | ✓ / ✓ | 11 | P0 | 3D | theatre | Pad placement |
| 48 | Instrument trays (lap chole / lap + open appendix) | ✓ / ✓ | 11, 14 | P0 | 3D | theatre | Sterility indicators |
| 49 | Drapes | ✓ / ✓ | 14 | P0 | 3D | theatre | Sterile field (cloth) |
| 50 | Resuscitation trolley + defibrillator + emergency drugs (representation) | ✓ / ✓ | all | P0 | 3D | all | Checked; used in emergencies |
| 51 | WHO checklist board / form | ✓ / ✓ | 13–15 | P0 | 3D+UI | theatre | Item status pending/confirmed |

## Step 15 (P1 — p1-theatre-extension)

| # | Item | S1 / S2 | Steps | Tier | Kind | Room | Interaction / detail needed |
|---|---|---|---|---|---|---|---|
| 52 | Specimen container + label | ✓ / ✓ | 15 | P1 | 3D+UI | theatre | Label read aloud at Sign Out |
| 53 | Recovery bay equipment (monitor, oxygen, suction) | ✓ / ✓ | 15 | P1 | 3D | recovery | Reuses #9, #28, #29 |


## Interaction-relevant room objects — NOT in Part E (only what the trainee uses)

Rule (user direction): no decorative dressing. Each object below exists because the trainee uses it.

| Object | Where | Why it's needed (interaction) |
|---|---|---|
| Ceiling-mounted surgical light **control panel** | OT | Turn lights on / adjust intensity (#44) |
| OT storage cupboards + drawers (working doors) | OT | Retrieve airway kit, PPE, drapes, consumables |
| Instrument (Mayo) trolley + back table / work surfaces | OT | Place and lay out equipment |
| Scrub sink + hand-rub dispensers | OT, bay | Hand hygiene before patient/sterile contact |
| Surface disinfectant wipes dispenser | OT, bay | Clean equipment/surfaces where a step requires it |
| Wall oxygen + suction outlets with flowmeter / regulator | OT, bay | Connect oxygen (#28) and suction (#29) |
| IV pole (standalone) | OT, bay | Hang fluids, mount pump (#26, #27) |
| Clinical waste bin (pedal) | OT, bay | Dispose of used consumables (sharps → #33) |
| Computer workstation + desk | OT, bay | EMR computer + place physical reports/folder |
| Physical patient folder (flippable pages) + printed report sheets + imaging printouts | bay, OT | Current-episode documents (p0-patient-records-emr) |
| Diagnostic doors with sign plates: CT Scan · Imaging · X-ray · Laboratory · MRCP | bay corridor | Category-B transitions (p0-diagnostic-transitions) |
| Specimen drop / pneumatic-tube station | bay | Send labelled samples to the Laboratory |
| Phone (wall/desk) | bay, OT | Receive critical-result calls + read-back |

**Not included** (no trainee interaction): decorative furniture, visitor chairs, plants, wall art, curtains (unless a step uses them),
autoclave/sterilisation machines (instrument sterilisation is not a trainee task in the walkthrough — trainee only checks tray sterility indicators).
