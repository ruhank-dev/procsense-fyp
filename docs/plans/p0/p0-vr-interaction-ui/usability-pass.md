# Usability pass script (EXP-UI-1, EXP-VR-2, p0-vr-interaction-ui-s11)

Status: **ready, not run.** Five people new to ProcSense, at least three of them medical students (the plan's rule: gamers are not the target users). The analysis is ready and tested on simulated rows (`tools/eval/analyse_usability.py pass`); the results page is written by it into `results/usability-pass.md`.

## Before each session

1. Build the Demo APK (`task unity-build -- Demo`) and install it (`docs/RELEASE.md`); reset the Settings tab ("Reset for the next person").
2. Start the session log; note the participant code (`U01`), never a name.
3. Read aloud: "Please think aloud: say what you are looking for and what you expect to happen. I won't help during a task; that is how we find what is hard."

## Tasks (round 1, controllers)

| Task id | Task given to the participant | Success when | Time limit |
|---|---|---|---|
| `step1_identity` | "Make sure this is the right patient." | identity confirmed in the log, unaided | 5 min |
| `attach_sensors` | "Find out how she is doing." | SpO2 probe and cuff attached, a BP reading taken | 4 min |
| `order_test` | "Order the blood test you think she needs and send it." | an order and a sent sample in the log | 5 min |
| `read_results` | "The result is back. What does it say, and what will you do?" | result opened and an interpretation chosen | 4 min |
| `sign_in` | "Lead the Sign In." | Sign In phase complete on the WHO board | 5 min |

## Hand tracking (EXP-VR-2)

The same participant then repeats the two small-object tasks with hand tracking on (Meta hand tracking through XRI Hands): `read_wristband` (grab and turn the wristband until it reads) and `label_tube` (put the label on the tube). Order alternates between participants (odd codes controllers first). Record success and time for both inputs.

## What the observer writes (one row per participant, task and input)

`participant,round,task,input,success,seconds,confusions,note` (template: `results/usability-pass-template.csv`). A confusion is any time the participant says they are lost, looks for more than 20 s, or presses a control that does nothing. The note is a short quote or what they tried.

## After round 1

1. `uv run --project tools tools/eval/analyse_usability.py pass --csv pass.csv --out docs/plans/p0/p0-vr-interaction-ui/results/usability-pass.md`
2. List the three issues with the most confusions across participants; open a fix for each (owner: whoever owns the panel in `docs/plans/frontend/front_end_load_distribution.md`).
3. Round 2: two new people repeat the tasks that had the issues; add their rows with `round` = 2 and run the script again.

## Pass rule

EXP-UI-1 passes when at least 4 of 5 complete `step1_identity` unaided with controllers in round 1. EXP-VR-2 has no pass mark: it records whether hands are easier or harder for the small objects, which decides whether hand tracking is offered.
