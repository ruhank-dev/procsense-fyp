# Author brief: independent evaluation set for the semantic reader (EX-01)

Write NEW fictional dialogue of a simulated pre-operative consultation or handover. You will not be shown any existing sentence, any prompt, any model answer or any label, and you must not look for them. No real patient or staff, no clinical rule, dose or threshold of your own: use only the approved action definitions and the scenario steps below.

**Scope of this evaluation:** it measures how well a reader understands what a trainee meant from clean written text, independently of speech-recognition quality. Real speech-recognition wording (family F17) is NOT part of it; it will be evaluated later from real recordings. Write clean, natural text: no deliberate spelling damage of any kind.

**Do not look for, copy, or rewrite any existing benchmark sentence.** You are not shown any; write from the situations below in your own words. A sentence that matches or nearly matches a repository sentence is refused by the owner's check and has to be replaced.

You write sentences; you do **not** label them. Two other people label every item without seeing your family tag or your intended act. Write each sentence the way a person says it, in your own words. **Do not invent typos, swapped letters or glued words**: spoken-language wording is welcome, deliberate spelling damage is not, and `asr_source` stays empty.

## The approved actions (content/assessment/taxonomy.json)

* `ASK_TOPIC`: Asks the patient about a history topic (complaint, medicines, last meal, previous anaesthetics...). Steps where it counts: 2.
* `CLARIFY`: Asks the patient or a colleague to explain or repeat something unclear or missing. Steps where it counts: 2, 6, 7, 10.
* `ANSWER_QUESTION`: Answers a question the patient asked. Steps where it counts: 2, 9.
* `EXPLAIN_PROCEDURE`: Explains the operation or anaesthetic in plain words. Steps where it counts: 6, 9.
* `EXPLAIN_TEST`: Explains what a test is and why it is done. Steps where it counts: 6, 9.
* `EXPLAIN_ACTION`: Says what is about to be done to the patient before doing it. Steps where it counts: 3, 4, 6, 8.
* `CHECK_UNDERSTANDING`: Checks the patient has understood, for example by asking them to say it back. Steps where it counts: 9.
* `ACKNOWLEDGE_EMOTION`: Recognises how the patient feels. Steps where it counts: 2, 8, 9.
* `REASSURE`: Offers reassurance that is true to the situation. Steps where it counts: 2, 8, 9.
* `ACKNOWLEDGE_FACT`: Notes back a relevant fact the patient has given. Steps where it counts: 2, 3, 4.
* `CLOSED_LOOP`: Repeats an instruction or result back to a colleague to confirm it. Steps where it counts: 6, 8, 11.
* `ESCALATE_SPEECH`: Asks a senior or another team member for help or a decision. Steps where it counts: 3, 4, 6, 7, 8, 11, 12.
* `REPORT_FAULT`: Reports a fault in equipment or the environment. Steps where it counts: 11, 13.
* `SBAR_ELEMENT` (critical): States an element of a structured handover (situation, background, assessment, recommendation). Steps where it counts: 12, 15.
* `VOICE_ORDER`: Gives an order to a colleague (a test, a medicine, transport, a call). Steps where it counts: 8, 10, 12.
* `CONFIRM_IDENTITY` (critical): Asks or states the identity checks: name, date of birth, wristband.
* `STATE_ALLERGY` (critical): Asks about or states an allergy and its reaction.
* `CONSENT_SIGN` (critical): Asks for or confirms consent to the procedure.
* `HANDOVER_CRITICAL_ELEMENT` (critical): States a critical element in a handover: allergy, fasting state, identity, or a deteriorating sign.
* `NONE`: Speech that shows none of the acts above (small talk, thinking aloud, noise).

## The situations you must cover (items per family)

The set is 300 items in all, written by at least 2 authors, with the number of items per family shown below (your own share is in your personal instructions). Give every item exactly ONE family (the one that describes why the sentence is interesting).

| family | name | items | what to write |
|---|---|---|---|
| F01 | ordinary_paraphrase | 24 | The trainee performs one ordinary act of the taxonomy now, in plain but unfamiliar wording (not the textbook phrasing). Cover many different acts. |
| F02 | indirect_natural_speech | 22 | An ordinary act performed in natural, indirect or elliptical speech (no keyword of the act, relying on the situation or on how people actually talk). |
| F03 | question_vs_assertion | 16 | Pairs of wordings that differ in being a question or a statement about the same content, where the approved definitions say whether the act is performed (for example an act that is performed by asking, against one that is performed by stating). |
| F04 | request_vs_completion_claim | 18 | A request or instruction to do something against a claim that it has been done, about the same thing. |
| F05 | hypothetical | 16 | The act is imagined, conditional or supposed, not performed. |
| F06 | reported_speech | 16 | Someone else's act or words are reported (a colleague, the patient, a chart); the trainee does not perform the act themself. |
| F07 | negation | 16 | The act is denied or refused, or something is not done. |
| F08 | actor_recipient | 16 | Who speaks, who does the act and to whom it is addressed differ (the patient or a colleague speaks; the trainee speaks to someone other than the usual recipient; an act is assigned to another person). |
| F09 | escalation_recommendation_handover_wording | 26 | Requests for a senior or other person to attend, review or decide, recommendations, and handover statements, in many wordings, with and without a named recipient. Authors state the recipient and the step in the scenario. |
| F10 | emotion_acknowledged_vs_reassured | 14 | Speech about the patient's worry or fear: some acknowledges the feeling, some offers comfort or a prediction, some does both. |
| F11 | explain_action_vs_explain_procedure | 14 | Explaining what the trainee is about to do to the patient now, against explaining the course of the procedure or what will happen overall. |
| F12 | answer_with_patient_question_context | 16 | Replies whose act can only be recognised from the patient's question in the earlier turns (the scene has a patient question still waiting). Give prior_turns and set patient_question_pending to yes. |
| F13 | closed_loop_readback_context | 16 | Read-backs and confirmations of a colleague's instruction or result, recognisable only from the earlier turns (a critical result or order is waiting to be read back). Give prior_turns and set readback_pending to yes. |
| F14 | critical_topic_discussion | 32 | Speech about identity checks, allergy, consent, fasting state, a deteriorating sign or a structured handover: questions, statements, requests, in many wordings including ones that sound like an ordinary act. These are semantically labelled as they are; they are never credited from speech. |
| F15 | adversarial_or_misleading_wording | 24 | Wording built to look like a performed act without being one, or to mislead a reader (instructions addressed to the reader, claims about what was done, quoting, sarcasm, unfinished sentences, fillers). |
| F16 | out_of_window_valid_speech | 14 | An ordinary act that is semantically performed but spoken at a scenario step where the approved behaviour windows (content/assessment/mapping.json) do not list it. The author states the step; the engine, not the label, decides eligibility. |
| F17 | asr_evidence | 0 | OUT OF SCOPE for the first EX-01 evaluation (quota 0, not allowed). Real speech-recognition wording will be evaluated in a separate later phase from actual recordings and transcripts (audio and headset work, DC-08, EX-18, docs/testing/asr_safety_evaluation.md). Invented typos, swapped letters or glued words are never allowed anywhere in the set. |

The quotas weight the safety-sensitive and historically ambiguous families more heavily; they are not built around any particular failure.

## The columns of your file (`author_template.csv`)

* `scenario_step`: a workflow step number 1 to 15 of content/assessment/mapping.json
* `prior_turns`: up to four earlier turns written as speaker: text separated by ' | ' (speaker is trainee, patient, colleague or tutor); empty if none
* `speaker_role`: who speaks the target utterance: trainee, patient, colleague or tutor
* `patient_question_pending`: yes only if the scene has a patient question still waiting for an answer
* `readback_pending`: yes only if a critical result or instruction is waiting to be read back
* `author_intended_acts`: the author's intention, used ONLY to check the coverage and the out-of-window family; it is never shown to an annotator and is never the label
* `no_clinical_inventions`: Use only the approved action definitions (content/assessment/taxonomy.json) and the scenario rules (content/assessment/mapping.json). Do not state a clinical requirement, a dose or a threshold.
* `author_code`: a short code for the author (A1, A2, ...) held in the roles record; never a name; never shown to an annotator
* `asr_source`: must be empty in phase two (no real-ASR family)
* `near_duplicates`: a sentence that is identical to, or a near-copy of, a sentence of any repository benchmark is refused by the owner's check; the check never shows the repository sentence to the author
* `item_id`: your own running number; `family`: F01 to F17; `scenario_context`: one or two lines saying where the scene is and who is present; `utterance`: the one sentence to be judged.

## What happens next
The owner removes your name, shuffles the items and gives each annotator a blind file with only the scene, the earlier turns and the sentence. Where the two annotators differ, an adjudicator decides, and a sentence that the approved definitions cannot settle is recorded as a specification issue, not as a failure of any system. Nobody changes a sentence or a label after the set is frozen.
