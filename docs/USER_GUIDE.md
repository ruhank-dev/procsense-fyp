# ProcSense user guide (trainees and teachers)

ProcSense is a VR trainer for preparing a patient for surgery: you take an invented patient from arrival on the ward to the WHO Time Out in theatre. **It is an educational simulation, not a medical device: all patients are invented, and nothing here is advice for a real patient.**

This guide describes the app as it is today (a prototype; the headset feel, the final rooms and some steps are still being built). Credits and sources: [ATTRIBUTIONS.md](ATTRIBUTIONS.md).

## 1. Starting

1. Put on the Meta Quest headset and open ProcSense.
2. **Sign in** with your ProcSense email and password on the login screen (your organisation invites you; the password is sent only to the account system and is never stored on the headset). A teacher's demo build may also offer a fixed demo account that practises offline and uploads nothing.
3. The first time, answer the privacy question on the ProcSense web page (storing your results is required to train; research use is optional).
4. You start in the **patient bay**, next to the bed and the patient.

## 2. Moving and doing things

| To | Do this |
|---|---|
| Move | Point the controller at a **blue pad** (or a clear part of the corridor floor) and release to teleport; the screen fades briefly |
| Turn | Flick the thumbstick: 45 degree snap turns (smooth turning is off to avoid motion sickness) |
| Pick up | Grab with the grip button (for example the notes folder on the table) |
| Open the folder | Hold it and press the trigger; the page shows the patient's name, date of birth and number |
| Read the wristband | Point at the band on the patient's wrist and select it |
| Everything else | The **tablet**: tabs Identity, History, Observe, More (actions for what is around you) and Session |

Working on a laptop without a headset (for teachers and developers): WASD walks, F7 cycles the room's teleport pads, and [ and ] turn.

## 3. How a case flows

You follow the real order of checks, the 15 steps of the walkthrough (Step 15, the Sign Out, is optional and comes later):

1. Arrival and identity (ask the name and date of birth, read the wristband, check the notes; nothing is ordered or given before the identity is confirmed)
2. History (open questions find what the patient does not volunteer)
3. Vital signs (attach the sensors; the monitor and the NEWS2 score)
4. Examination
5. Risk and anaesthetic assessment
6. Tests (order them; samples are taken and labelled at the bedside; scans take the patient away for a while)
7. Results (read them; the laboratory phones you with a critical result, which you must answer and read back)
8. Decide, treat and optimise (request medicines; a clinician confirms and a nurse gives them; you never give a medicine yourself)
9. Consent
10. Preparing the patient (the ward checklist)
11. Theatre preparation
12. Transfer and handover (SBAR)
13. WHO Sign In
14. WHO Time Out: the finish line

Time passes on the simulation clock. While you wait for a result you can skip time; the skip stops by itself when something needs you (the laboratory phones, the patient deteriorates, the nurse points something out).

## 4. How a case ends

| End | Meaning |
|---|---|
| Ready for incision, safe | Every blocker resolved, on the correct pathway |
| Appropriately postponed | You found the reason the operation must not go ahead today and said so: **this is a success** |
| Appropriately redirected | The patient needed a different path (a diagnosis to rule out first) |
| Safe but delayed | The right outcome, with avoidable delays |
| Safety failure | A blocker was broken or a life-threatening event was missed |

Putting an operation off for the right reason is as good a result as going ahead.

## 5. The debrief and your results

The debrief (today the session report on the ProcSense web page; the debrief screen in the headset comes later) lists what you did well, what was missed and why, each with the rule and the source it comes from, and a timeline of the case. On the tablet's **Session** tab press **Finish session and upload**: the session goes to the ProcSense service, which replays it with the same engine to check the result, and your verified results appear under **Refresh my results** and on the web page. If there is no network, the session waits on the headset and goes when the network is back. Scores are provisional while the scoring weights are being reviewed; the result (success or failure) does not depend on them.

## 6. Practice and assessment

| | Practice | Assessment |
|---|---|---|
| Help | Goals, hints, blocker messages, the NEWS2 helper; the nurse questions an unsafe request (beginner level) | None |
| Which cases | Cases approved for training | Only cases approved for assessment, in a fixed order |
| After a decision | Shows what the right decision was | Never |
| The mode | Chosen before the case starts; it cannot be changed during the case and is written into the session |

No case is approved for assessment yet: assessment mode opens once the clinician has signed cases off.

## 7. For teachers

- Invite trainees and see their verified sessions, progress and reports on the ProcSense web page (your organisation's administrator gives you the supervisor role).
- Each report shows the end state, the items lost and gained with their sources, and the timeline; export to CSV is on the same page.
- Running a demonstration: [server/DEMO.md](../server/DEMO.md). Keeping the service running: [server/RUNBOOK.md](../server/RUNBOOK.md).

## 8. If something goes wrong

| Problem | What to do |
|---|---|
| "Sign-in is unavailable" or the first sign-in is slow | The free server sleeps when unused and takes about a minute to wake: wait and try again |
| An action is refused | Read the message; most often the identity is not confirmed yet, or the patient is away for a scan |
| The case cannot start in assessment mode | It is not approved for assessment (see section 6) |
| Feeling unwell in the headset | Take it off and rest; keep to teleport and snap turns |
