# Equipment interaction spec — what the trainee does, what the system does

For every major item: the **trainee action** (from walkthrough Part E "what the trainee sees and does" + step guides) and the
**system / visual response** (our design — Type D unless the walkthrough states it). Item numbers = `../p0-blender-equipment/equipment-list.md`.
Blender assets must provide the named points (`GRAB_`, `SOCKET_`, `PIVOT_`, `SCREEN_`, `LABEL_`, `ATTACH_`) these interactions need.

Role rule (walkthrough Part 8.1): medicines are **requested**; a clinician NPC confirms, a nurse NPC administers. No item lets the trainee dose or administer.

## Operation Theatre (category A — main environment)

| # | Item | Trainee does | System / visual response | Failure / scenario (walkthrough) | Asset needs |
|---|---|---|---|---|---|
| 44 | Surgical lights + control panel | Presses on/off and intensity on the wall/arm panel; grabs handle to aim | Light turns on, beam + spot on table, intensity steps; arm rotates within limits | (Environment item in WT; operable by user request) | `PIVOT_` arms, `GRAB_Handle`, emissive + spotlight, control buttons |
| 40 | Operating table + positioning padding | Uses control to set height/tilt/flat; places padding at pressure points | Table sections animate; padding snaps to sockets; positioning checklist item ticks | Team check at Step 11 | `PIVOT_` sections, `SOCKET_Pad_*` |
| 41 | Anaesthesia machine + check log | Opens check log; confirms machine check done (with anaesthetist NPC) | Screen shows self-test / ready; check log status → done; Sign In item "anaesthesia machine check complete" can be confirmed | Check not done → Sign In fails | `SCREEN_Main`, `GRAB_CheckLog`, drawers `PIVOT_` |
| 42 | Capnograph (on monitor) | Looks at EtCO2 after induction (abstracted) | Waveform + value from Core | — | `SCREEN_` |
| 9 | Patient monitor (OT) | Attaches leads, SpO2 probe, BP cuff; reads values; hears alarms; silences/acknowledges alarm | Values appear only for attached sensors; live traces; alarms at thresholds; artefact if probe poorly placed | Alarm ignored → deterioration continues | `SCREEN_`, `ATTACH_` lead ports |
| 43 | Airway equipment + difficult-airway trolley | Opens drawers, confirms laryngoscope/tubes present + working (light on) | Drawer slides; items highlight on inspect; laryngoscope light turns on when blade opened | Difficult airway not declared → Sign In issue | Drawers `PIVOT_`, `GRAB_` per item, blade hinge |
| 29 | Suction (theatre) | Turns on, occludes tubing to test | Gauge rises / sound plays; state = working or fault (scenario) | Not working → RSI unsafe | Switch, `ATTACH_Tube`, gauge needle `PIVOT_` |
| 45 | Laparoscopic tower | Powers on units; checks camera image, light source, insufflator pressure/flow; reads fault messages | Monitor shows image / "no signal"; insufflator display shows values or a fault message (scenario) | Fault → delay | `SCREEN_` per unit, power buttons |
| 46 | CO2 cylinder (+ spare) | Reads gauge; swaps to spare if empty | Gauge shows level from scenario; swap animation; insufflator fault clears | Empty → fault scenario | Gauge `PIVOT_`, `GRAB_`, `SOCKET_` |
| 47 | Electrosurgical unit + return electrode | Confirms return pad placed on patient | Pad snaps to body socket; unit status "pad OK" | Pad missing → burn risk (stop) | `GRAB_Pad`, `SOCKET_` on patient, `SCREEN_` |
| 48 | Instrument trays | Opens tray, checks sterility indicator colour | Indicator shows pass/fail (scenario); fail → must report | Indicator fail → stop | Lid `PIVOT_`, indicator material swap |
| 49 | Drapes | (Scrub NPC drapes; trainee observes at Time Out) | Drapes appear on patient at the right moment | — | Cloth mesh, two states |
| 51 | WHO checklist board | Leads/answers Sign In and Time Out items | Items change pending → confirmed (green); skipped item → challenge, repeated skip stops case | Skipped item → stop | `SCREEN_` / board UI |
| 50 | Resuscitation trolley + defibrillator | Checks trolley (seal/drawers); uses in emergency branch | Drawer contents visible; defib screen on; emergency NPCs respond | Not checked → delay in emergency | Drawers `PIVOT_`, `SCREEN_` |
| 37 | Forced-air warming device | Applies blanket + switches on if T < 36 °C or long case | Unit display shows on; patient temperature trend responds (Core) | Hypothermia not addressed | Blanket mesh, power button |
| — | Scrub sink / hand-hygiene station | Uses sink / hand-rub before touching patient or sterile items | Water/rub effect; hand-hygiene event logged (scored where required) | (WHO hand hygiene, PPE row) | Tap/sensor `GRAB_` or proximity trigger |
| 8 | PPE (cap, mask, gown, gloves) | Dons from dispensers / shelf | Avatar hands/body show PPE; PPE state logged | — | `GRAB_` per item, dispensers |
| — | OT storage cupboards | Opens doors/drawers to retrieve equipment | Doors/drawers animate; items can be taken out | — | `PIVOT_` doors/drawers, item `SOCKET_`s |
| — | Instrument / Mayo trolley, back table | Places equipment on work surface | Items snap to surface sockets | — | Surface `SOCKET_`s |
| — | Computer workstation (EMR) | Searches patient, opens tabs, reads results/images | EMR UI (see p0-patient-records-emr) | — | `SCREEN_` |

## Pre-operative patient bay (category A)

| # | Item | Trainee does | System / visual response | Failure / scenario (walkthrough) | Asset needs |
|---|---|---|---|---|---|
| 1 | Wristband | Rotates band to read name/DOB/MRN; compares with spoken answer + notes | Text readable (live data); identity-verified state when matched | Mismatch → stop; missing band → apply after verification | `LABEL_`, wrist `SOCKET_` |
| 2 | Allergy band | Checks before any drug | Colour flag visible; details only in notes/by asking | Missing band with reported allergy → reaction branch | Colour material |
| 3 | Bed / trolley | Sets head-up / flat (flat for low BP) | Bed section animates; patient pose follows | — | `PIVOT_` head section, control |
| 9 | Monitor (spot-check on stand) | Attaches sensors; reads values | As OT monitor | Artefact if probe poor | `SCREEN_`, `ATTACH_` |
| 10 | BP cuff | Wraps arm, starts cycle | ~30–40 s cycle (WT), then SBP/DBP/MAP on monitor | Wrong size → false reading | Arm `SOCKET_`, hose `ATTACH_` |
| 11 | Pulse oximeter probe | Clips on finger; checks waveform | SpO2 + pulse + pleth; no waveform → recheck | — | Finger `SOCKET_`, cable |
| 12 | Tympanic thermometer | Inserts in ear | Reading in °C on device display | < 36.0 → warm before theatre | `SCREEN_`, `GRAB_` |
| 13 | 12-lead ECG + electrodes | Places 10 electrodes; reads printout | Electrodes snap to chest/limb sockets; printout appears; machine read-out may be wrong (WT) | Wrong placement → misleading trace | 10 body `SOCKET_`s, printout prop |
| 14 | Stethoscope | Places on chest/abdomen | Auscultation audio for that site (case-specific) | — | `GRAB_`, body hotspots |
| 15 | Glucose/ketone meter | Finger-prick test | Glucose / ketone value on display | Hypoglycaemia missed | `SCREEN_` |
| 16–17 | Blood collection + tubes + labels | Takes blood **after ID check**; labels tubes **at bedside**; sends to lab | Tubes fill; label shows patient data; samples go to Laboratory door (porter) | Pre-labelled tubes → wrong-blood event; drip arm → artefact; EDTA → false high K | Tube `LABEL_`, caps colours |
| 18 | Blood culture bottles | Fills before antibiotics (S2) | Bottles marked filled | Delaying antibiotics for cultures → harm | `GRAB_`, fill state |
| 19 | Urine pot + dipstick + pregnancy test | Dips; reads at 1–5 min | Colour pads / hCG result appear after timer | Missed positive hCG → safety failure | Material states |
| 25 | IV cannula kit | Inserts (abstracted) | Cannula visible on arm; access state | Failed cannulation → delay | Arm `SOCKET_` |
| 26 | IV giving set + fluid bags + pole | Hangs bag (fluid requested + confirmed via workflow) | Bag on hook; label shows fluid/volume; drip running | Fluid overload in heart failure | Hook `SOCKET_`, `LABEL_` |
| 27 | Infusion pump | Clamps to pole; rate set by nurse after confirmation (abstracted) | Display shows rate; alarm in scenario | Pump alarm | `SCREEN_`, pole clamp |
| 28 | Oxygen (outlet, flowmeter, cannula, masks) | Selects device to reach target | Device on face; flow set per protocol; SpO2 responds (Core) | Over-oxygenating CO2 retainer | Face `SOCKET_`, flowmeter knob |
| 32 | Medication trolley / drug chart | Requests medicine; views confirmed prescription | Request → clinician confirms → nurse administers; allergen request → nurse challenge | Request for an allergen → challenge | Drawers, chart UI |
| 33 | Sharps bin | Disposes needles | Needle snaps in; hazard cleared | Needlestick if ignored | Opening `SOCKET_` |
| 34 | Stockings / IPC | Fits if indicated | Stockings on legs; IPC unit on | Fitted with arterial disease → harm | Leg `SOCKET_`s |
| 35 | Clippers | Uses clippers (not razor) | Hair-removal state on site | Razor → infection risk | `GRAB_` |
| 4 / 38 | Wheelchair / transfer trolley + portable monitor + O2 cylinder | Chooses transport + escort for tests and theatre | Patient moves accordingly (p0-diagnostic-transitions); choice scored | Unstable patient by wheelchair → error; corridor deterioration | Seats/sockets for patient |
| 5 | Patient folder | Opens, flips pages, reads, adds reports | See p0-patient-records-emr | Missing documents block transfer | Page rig, `GRAB_` |
| 7 | Consent form | Completes with patient (dialogue) | Form fields fill; signature state | Wrong procedure → blocker | Document surface |
