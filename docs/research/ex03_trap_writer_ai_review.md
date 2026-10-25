# EX-03: AI-simulated reviewer passes on the trap writer

**Label: AI-simulated review.** Two readings by the same AI assistant, not two people. They do not satisfy the gate `trap_writer.human_plausibility`, which still reads `reports/ai/trap-writer-human-rating.json` (two raters, written by `tools/eval/blind_extras.py analyse`). The real two-person rating stays an external item (EX-03 in `docs/testing/SANNAN_EXTERNAL_TESTING.md`).

Data: `tests/ex03/ai_simulated_review.json`. Dimensions: plausibility, clinical consistency, educational relevance, clarity, ambiguity, difficulty, realism, unfair or misleading.

## Method

Each reworded wrong option was read next to the authored one in two passes, the second in reverse order. A dimension failed if the reworded option no longer meant the authored wrong thing, or made it look correct, or added something. Items that failed in both passes failed; a split on a minor point was resolved to pass.

## Result before the change (prompt v2, 12 samples)

6 of 12 failed. The wording added urgency ("immediately"), a second clause ("and address the hyperglycemia", "focus on the patient's symptoms", "until you have more data") or dropped "only". The sepsis example turned the wrong option into the right one. The lexical checks missed these.

## Change

`prompts/trap/reword_v3.txt` (a new version, registered with its hash; v2 is kept): the prompt now forbids a second action or clause, words of urgency or timing the original lacks, and swapping one action for another; it says to keep every content word of an already natural instruction, including "only", "just", "alone" and "without". No new keyword gate or checker was added.

## Retest (cloud model, 24 cases, 45 wrong options)

- The first run of v3 fixed five of the six, but "Check the INR and then proceed." still dropped "only". The prompt line about limiting words was added and the run repeated.
- Final run: all 12 sampled rewordings pass in both passes ("Go ahead after checking only the INR."), 43 of 45 accepted or kept, no case with lower trap fairness (`reports/ai/trap-writer-eval-v3-cloud.json`).

## Limits

The sample is 12 items, all from the first cases of the library. The same assistant read both passes. Whether the new wording is more plausible than the authored one to a busy clinician is exactly what the human rating measures and is not claimed. The trap writer stays OFF.
