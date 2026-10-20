# DT-233: audit of the ten remaining final-split failures (20 October 2026, p1-ai-language-s17)

**The frozen candidate `s16` is not changed, the ten sentences are not used for tuning, and nothing here fixes anything.** The failures are those listed by `docs/research/unsafe_speech_safety.md` (candidate on the final split, 10 of 407 must-not-credit rows credited by the engine). The final split is spent for every configuration after `s16` (`tests/safety_challenge/frozen.json`); any fix is a NEW configuration scored on a FRESH set.

## 1. Mechanisms (suspected, then checked on probes that are NOT the final sentences)

To separate cause from coincidence, each mechanism was checked with a probe sentence built from a development seed (`ask about your appetite`), never from a final seed. A probe tells which rule did or did not fire; it is diagnosis, not a fix and not a tuning set.

| # | final rows (n) | wording frame | what the layer does | probe result (dev seed) | mechanism |
|---|---|---|---|---|---|
| 1 | question_advice R5 (5) | "The guidance says to X." | no rule | "The guidance says to ask about your appetite." is not flagged; "The nurse says to ask ..." is `reported` | **closed list of reporting sources**: the roles list holds people; a document or rule as the source ("guidance", "protocol", "policy", "the checklist") is not a role, and a bare instruction to do X is not recognised as advice |
| 2 | negation N5 (2) | "I have not had the chance to X." | the denial pattern allows at most three words between the negation and the verb | "I have not had the chance to ask ..." is not flagged (four words: had, the, chance, to); "I have not had time to ask ..." is `denied` | **fixed word window** in the denial pattern |
| 3 | hedged U6 (2) | "I probably X earlier, I can't remember." | hedge patterns need the hedge BEFORE the subject, or "might have" + verb | "Probably I asked ..." is `hedged`; "I probably asked ..." and "I asked ... earlier, I can't remember." are not flagged | **word order of the hedge** (subject first) and **no trailing-hedge rule** ("I can't remember" after the claim) |
| 4 | combination X6 (1) | "I wondered whether I should X (a physical verb)." | the intention pattern needs a verb from `_VERBS` | "... should ask about ..." and "... should check your pulse now" are `intended`; "... should look at your throat now" is not flagged; "I am wondering whether to ask ..." is not flagged | **closed verb lexicon**: physical-action verbs (look, take, feel, ...) are in the lexicon only next to a negation, on purpose (so "the porter will take her" is not refused), and "wondering whether to" has no rule |

Common root: all four are the same design limit as in `safety_layers_audit.md` F1 and F12. The assertion layer is a set of regular expressions over closed lists (roles, verbs, hedges, delay markers, gap sizes); a wording outside a list passes. The failures are not ten independent bugs; they are four list or window limits, each reachable from many other wordings.

## 2. Why they were not fixed in this phase
Fixing them against the split that revealed them would turn the final split into development data and make its 2.5 percent figure uninformative. The honest alternatives are (a) leave the configuration frozen and report the limit, or (b) build a new configuration with a development-only process and a fresh final set. This phase does (a) and prepares (b).

## 3. Development-only evaluation plan for any later fix (preregistered)

1. **New development data, not these sentences.** Write `dev2`: new seed families and new wordings for the four mechanisms and their neighbours (other document-like sources; other gap lengths between a negation and its verb; hedges before and after the claim, with and without a subject; wondering, deciding, considering, thinking about, with listed and unlisted verbs). Prefer an author who has not seen the final split or the table above; if the builder writes it, label it builder-written. Do not copy a final sentence or its frame.
2. **Fresh final.** Write `final2` (different seeds and templates and, if possible, a different author), freeze its hash in a new `frozen.json` BEFORE any fix exists, and keep it out of every tool that tunes.
3. **Candidate fixes, each justified by `dev2` and the legitimate sets only:** (a) source nouns as a second closed list, or any non-first-person "X says, states or requires to ..." frame; (b) a variable gap, or clause-level negation scope, for denials; (c) hedges after the claim and subject-first hedges; (d) "wondering, considering, thinking about + whether, if, to" frames, and a verb lexicon that is not closed for these frames (for example any verb after a modal of possibility). A structural alternative (an act-agnostic detector of the speaker's stance, learned on independent data) is considered only if (a) to (d) fail the rule below; no new model is run now.
4. **Acceptance rule, fixed now:** a change is kept only if (i) it lowers must-not-credit credit on `dev2`; (ii) `tools/bench/assertion_diff.py --before <commit of s16>` newly flags no legitimate sentence of any set (bench dev, test, test2; AI-judged v2; the regression paraphrases; the `dev` and `challenge` credit_allowed rows); (iii) `tools/bench/interpretation_bench.py` recall is unchanged on dev, test and test2 (95, 76 and 79 of 140); (iv) every mutation test and the engine suite pass; (v) the cap and thresholds are unchanged unless a separate decision with its own evidence says otherwise.
5. **Score once.** Freeze the new configuration under a new name, record the file hashes, score `final2` once for the new configuration and for `s16` on the same rows, and report counts with Wilson intervals. A second look at `final2` makes it development data.
6. **What stays unproven either way:** same-author style, no human labels, no real recogniser output. The independent run (EX-01 part B) is the only evidence that can move the gate.

## 4. Status
DT-233 stays **Pending** (needs fresh data). The ten failures are reported in `unsafe_speech_safety.md`; the gate stays OFF.
