# Audio-to-transcript safety evaluation (design, 20 October 2026, p1-ai-language-s17)

**Status: DESIGN ONLY. No audio exists, nothing was recorded, no recogniser was run for this purpose, and nothing here is validated.** It is separate from the text-only evaluation (`docs/research/unsafe_speech_safety.md`, `docs/testing/safety_challenge_review.md`) and must never be pooled with it. A claim about spoken safety can come only from this evaluation, after it has been run.

## 1. Why a separate evaluation
Every text rule of the interpretation path assumes the transcript says what was said. The recogniser can break that assumption in the one way no text rule can repair: it can drop or alter a negation ("I have not asked about your allergies" becomes "I asked about your allergies"), merge a self-correction into an assertion, drop a hedge, or hear a different speaker. The `asr_negation_lost` rows of the text sets (18 of 28 credited on development) only illustrate the consequence; they say nothing about how often it happens.

## 2. Questions it answers (and only these)
1. **Negation preservation:** how often does the transcript lose, add or move a negation, relative to what the speaker said?
2. **Self-correction and retraction:** does a spoken correction ("... no wait, I didn't") survive into the transcript in a form the assertion layer refuses?
3. **Uncertainty and hedges:** are "maybe", "I think", "I can't remember" kept?
4. **Attribution and quotation:** are "the nurse said ..." and reading from a script kept without being turned into a first-person assertion?
5. **Disfluency and truncation:** fillers, restarts, cut-off words and run-together words ("wantthe"): do they change the status?
6. **Downstream effect:** for each transcript, what does the interpretation path decide and what does the engine accept? This is the only number about safety; the others explain it.
7. **Confidence usefulness:** does any recogniser confidence (word-level probability or average log-probability) separate dropped-negation transcripts from correct ones well enough to be a refusal signal? Unknown: a hypothesis to test, not a feature to build now.

## 3. Materials
* **Script set:** utterances written in advance by people who have not seen the text sets, with the spoken label recorded BEFORE recording: performed, not performed (with the reason: negated, planned, hypothetical, reported, quoted, retracted, hedged, other speaker, fragment) or critical. Each script has a reference transcript, which is what was meant to be said. At least 40 scripts per family of the taxonomy that is spoken in practice; at least half contain a negation, a correction or a hedge.
* **Speakers:** at least 8, with different accents and speaking rates, at least 2 not native speakers of English, none of them the builder; consent recorded by the owner; no real patient or staff data in the scripts; audio stored outside the repository (only hashes, durations and transcripts enter `reports/`).
* **Conditions:** the headset microphone chain used in play (Sannan's Quest, DC-08), a desktop microphone, and a quiet and a simulated-theatre-noise condition; each script spoken once naturally and once with a deliberate self-correction or restart.
* **Recogniser:** the configuration that would ship (faster-whisper small.en as in `reports/ai/asr-*.json`), frozen by version and decoding settings BEFORE recording; one transcript per recording; nothing re-run after seeing results.

## 4. Procedure
1. Freeze the script set, the recogniser configuration and the interpretation configuration (file hashes, as in the `configuration` block of `tools/bench/safety_eval.py`) in a manifest.
2. Record. Two people transcribe each recording by ear (not from the script) to get the spoken text; a third adjudicates differences. The recogniser's transcript is produced separately and not shown to them.
3. Align the recogniser transcript to the spoken text and count, per family: negation words kept, dropped, added; correction markers kept or lost; hedges kept or lost; word error rate overall and on the words that carry stance.
4. Run the recogniser transcripts through the interpretation path exactly as `safety_eval.py` does (decision, reasons, engine verdict), and the spoken text through it as well; the difference is the damage the recogniser does.
5. Score once per frozen configuration; report counts with Wilson intervals, per family, speaker group and condition; list every transcript credited although the spoken text was not credit-worthy.

## 5. Metrics
Negation-loss rate (negations in the spoken text absent from the transcript, over negations spoken); negation-insertion rate; correction-survival rate; hedge-survival rate; stance-word error rate; **unsafe credit from transcripts** (must-not-credit spoken items credited by the engine) beside **unsafe credit from the spoken text** (the same items typed); legitimate credit from transcripts against typed; for any recogniser confidence, the ROC or a threshold analysis with its cost in legitimate refusals.

## 6. Acceptance criteria
All **PENDING the owner** (DT-229); proposals only: (a) report, do not hide, the negation-loss rate with its interval; (b) any item credited from a transcript although the spoken text was not credit-worthy is individually reviewed; (c) a refusal signal from recogniser confidence is adopted only if it removes the dropped-negation credits on a held-out half without refusing more than a stated fraction of legitimate items; (d) the headset and desktop conditions are reported separately.

## 7. What it can and cannot show, and what must not be claimed
Can: how often the recogniser changes stance and whether the layers downstream catch it. Cannot: behaviour of learners in a real scene, other recognisers, accents that were not sampled. **Do not claim** that spoken negation is safe, that the text figures hold for audio, or that a confidence signal works, until this has been run and reviewed. Until then `asr_negation_lost` stays "not mitigable by text" and the gate stays OFF.

## 8. Ownership and status
It needs people and a headset: an EX-01 extension (part C) for the owner to arrange; Sannan owns the Quest recording (DC-08). **NOT RUN. BLOCKED on people, recordings and the owner's approval of the acceptance proposals.**
