# DT-250: blind dialogue comparison, local Qwen against Gemma 4 31B Cloud

**Label: AI-simulated human-style assessment.** The ratings were made by an AI assistant, in two readings, with the model identity hidden. They are not ratings by people. The requirement for real human raters stays open as a separate external item (DT-250 parts a and b).

Date: 25 Oct 2026. Data: `tests/dt250/` (cases, both blind sheets, both rating files, the key). Scoring: `tools/ai/dt250_score.py`. Result: `reports/ai/dt250-dialogue-evaluation.json`.

## Method

- 60 cases on 10 invented patients: 46 patient-dialogue cases and 14 staff-dialogue cases. All are labelled synthetic and AI-written, and none was used to develop the prompts.
- Both models answered through the production `Dialogue` code: local `qwen3.5:4b-q4_K_M` with the version 1 prompts it keeps, and `gemma4:31b-cloud` with the version 2 prompts. Two cloud answers that fell back to local were rerun.
- Each case was shown as response A and response B. Pass 1 and pass 2 used a different case order and opposite A/B positions, and the key was not opened until both passes were saved.
- Patient dimensions (1 to 5): realism, flow, persona, history consistency, grounding, uncertainty, disclosure control, clarity. Staff dimensions: appropriateness, role fit, restraint, clarity. Each response also got an "unsupported statement" flag.
- A case counts as agreed when both passes prefer the same model. A split resolves to a tie and is listed.

## Results

| | Local Qwen | Gemma 4 31B Cloud |
|---|---|---|
| Patient overall (1 to 5) | 4.33 | 4.81 |
| Patient realism / flow / persona | 3.48 / 3.57 / 3.75 | 4.43 / 4.45 / 4.82 |
| Patient grounding / disclosure control | 4.76 / 4.91 | 4.98 / 4.98 |
| Staff overall | 2.45 | 4.62 |
| Staff restraint | 1.79 | 5.00 |
| Preferred, patient cases (46) | 2 | 32 (12 ties) |
| Preferred, staff cases (14) | 0 | 14 |
| Unsupported statements (both passes) | 6 | 0 |

The two passes preferred the same side in 58 of 60 cases (96.7 %). The two splits (c07 and c36) were a tie against the cloud answer, and both resolve to a tie.

## What it shows and what was done

- The cloud model is better on every dimension, mainly on realism, flow and persona. The ties are mostly short direct questions where both answers are the same fact.
- The local model produced three answers that denied or invented facts (c02 false denial of tablets, c30 an invented "hot twice", c38 an invented denial of blood thinners) and one non-answer (c04). The cloud model produced none. This confirms the routing in ADR-0026: patient and staff dialogue go to the cloud model once consent and approval allow it, and the local model is only the fallback.
- The local staff selection speaks on nearly every turn (restraint 1.79). Production only takes staff lines from the cloud model, so this does not reach trainees. It is a reason to keep that rule.
- Two cases went to local (c14 off-topic, c43 pressure). Both are about the local answer being shorter; neither was an error by the cloud model. No prompt change was made because the cloud scores leave no failing dimension to fix.

## Limitations

1. Both passes are one AI assistant reading twice. They are not independent in the way two people are.
2. The patients and conversations are invented and are not real trainee dialogue.
3. The rater saw the withheld facts, as a clinician reviewer would, but answer style can hint at the model.
4. The local model's staff choice is forced through a prompt production does not use.
5. Real blind ratings by people are still needed and are not claimed here.
