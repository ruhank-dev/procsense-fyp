"""Contrastive worked examples for the evaluator prompt (p1-ai-language-s22, ADR-0025). They are written from the approved semantics (taxonomy definitions, annotation-rubric tie-breaks, the assertion kinds of ADR-0022) on topics chosen to differ from the
development set; none is a row of tests/synthetic_dev_v1 or of any benchmark. Format: utterance => objective assertion/actor/form relevance.correctness.completeness; ... | physical claim.
Grades are shown only where they teach something; 3.3.3 is the default for a clear, correct, complete performance."""
from __future__ import annotations

# who did it, and when: one family of sentences about the same communication
MODALITY = [
    ('"How did you get here today, did someone drive you?"', "ASK_TOPIC performed/trainee/question 3.3.3"),
    ('"I asked how you got here when you arrived this morning."', "ASK_TOPIC earlier_claim/trainee"),
    ('"I haven\'t asked how you got here yet."', "ASK_TOPIC negated/trainee"),
    ('"I\'ll ask how you got here once the forms are done."', "ASK_TOPIC intended_later/trainee"),
    ('"If I asked how you got here, you\'d say the bus, wouldn\'t you?"', "ASK_TOPIC hypothetical/trainee"),
    ('"The receptionist asked how you got here."', "ASK_TOPIC reported/other"),
    ('"The receptionist will ask how you got here."', "ASK_TOPIC other_actor/other"),
    ('"Kim said the team wants to know how you got here."', "ASK_TOPIC reported/other"),
    ('"I haven\'t asked how you got here, but the nurse will."', "ASK_TOPIC negated/trainee (one entry per objective: the speaker\'s own assertion)"),
    ('"I haven\'t noted yet that you take tablets for your thyroid."', "ACKNOWLEDGE_FACT negated/trainee (the fact restated inside a denial is not noted)"),
    ('"She said she noted your thyroid tablets."', "ACKNOWLEDGE_FACT reported/other"),
    ('"I\'m going to roll you onto your side now so I can look at your back."', "EXPLAIN_ACTION performed/trainee 3.3.3 | claims physical: yes (rolling the patient)"),
    ('"I\'ll roll you onto your side in a few minutes, after the porter arrives."', "EXPLAIN_ACTION intended_later/trainee"),
]
# a question or offer is only the act when asking is how the act is done
FORM = [
    ('"Shall I go through the catheter removal with you?"', "EXPLAIN_PROCEDURE intended_later/trainee/question (an offer is not the explanation)"),
    ('"Can you tell me in your own words what happens after the dressing goes on?"', "CHECK_UNDERSTANDING performed/trainee/question 3.3.3"),
    ('"Sorry, which leg did you say hurts?"', "CLARIFY performed/trainee/question"),
    ('"Which leg hurts?"', "ASK_TOPIC performed/trainee/question"),
]
# neighbouring objectives
BOUNDARIES = [
    ('"Could you ask the physio to see him before lunch?"', "VOICE_ORDER performed/trainee/colleague (a task for a colleague)"),
    ('"Please bring a wheelchair to bay six."', "VOICE_ORDER performed/trainee/colleague"),
    ('"I\'m not sure it is safe to move him, can the registrar come and decide?"', "ESCALATE_SPEECH performed/trainee/senior (asks a senior to come and decide)"),
    ('"You sound really on edge about the scan."', "ACKNOWLEDGE_EMOTION performed/trainee (names the feeling)"),
    ('"Someone will stay beside you all the way through, and you can ask us to stop at any point."', "REASSURE performed/trainee (comforts, names no feeling)"),
    ('"I can see you\'re on edge, and someone will stay beside you all the way through."', "ACKNOWLEDGE_EMOTION performed; REASSURE performed (two clauses, two jobs)"),
    ('"You will be back on the ward within the hour, and visiting opens then."', "ANSWER_QUESTION performed (information that comforts stays the information objective; no REASSURE)"),
    ('"It is very common to feel tense before a scan, that is a normal reaction."', "ACKNOWLEDGE_EMOTION performed (normalising a feeling names it; no REASSURE)"),
    ('"In the operation the surgeon repairs the tendon and closes the skin."', "EXPLAIN_PROCEDURE performed/trainee 3.3.3"),
    ('"The spirometer shows how much air you can blow out."', "EXPLAIN_TEST performed/trainee 3.3.3"),
    ('"That\'s the spirometer."', "EXPLAIN_TEST performed/trainee 2.3.1 (on topic, correct, almost nothing explained)"),
    ('"The spirometer checks your heart rhythm."', "EXPLAIN_TEST performed/trainee 3.0.2 (fluent but wrong: correctness 0)"),
]
# grading content that is wrong, off-question, dismissive, pressuring or vague (fluent and on topic is not enough for a 3)
GRADING = [
    ('"The pulse oximeter on your finger measures how warm your hands are."', "EXPLAIN_TEST performed/trainee 3.0.2 (fluent but false: correctness 0)"),
    ('"There is no chance of any complication, so you can relax completely."', "REASSURE performed/trainee 3.0.3 (promises the impossible)"),
    ('"You have no reason to feel like that, so let us carry on."', "ACKNOWLEDGE_EMOTION performed/trainee 2.0.1 (dismisses the feeling instead of recognising it)"),
    ('(a patient question about wearing glasses is waiting) "Lunch is served at noon."', "ANSWER_QUESTION performed/trainee 0.3.1 (answers a different question)"),
    ('"Surely you cannot have forgotten what I just said?"', "CHECK_UNDERSTANDING performed/trainee/question 1.1.1 (pressure, not a genuine check)"),
    ('"Fine, fine."', "REASSURE performed/trainee 2.2.1 (comfort so vague it carries almost nothing)"),
]
# critical topics, physical claims and several jobs in one utterance
CRITICAL_AND_MULTI = [
    ('"Could you tell me your full name and date of birth?"', "CONFIRM_IDENTITY performed/trainee/question 3.3.3 | critical topic: yes | claims physical: no (asking is not a physical action)"),
    ('"I\'m checking your wristband against the form now."', "CONFIRM_IDENTITY performed/trainee | critical topic: yes | claims physical: yes (checking the wristband); you cannot know it happened"),
    ('"I already checked her wristband earlier."', "CONFIRM_IDENTITY earlier_claim/trainee | claims physical: yes"),
    ('"The nurse will check your wristband."', "CONFIRM_IDENTITY other_actor/other"),
    ('"Are you allergic to anything? And have you had any trouble sleeping lately?"', "STATE_ALLERGY performed/question; ASK_TOPIC performed/question | critical topic: yes"),
    ('"You look worried. I\'m going to check your pulse now, and then we\'ll talk about the plan."', "ACKNOWLEDGE_EMOTION performed; EXPLAIN_ACTION performed | claims physical: yes (checking the pulse)"),
    ('"So the, um, the thing we usually"', "no objective (acts empty)"),
    ('"Lovely colours on this ward."', "no objective (acts empty)"),
]
# one job or two (the second-objective test), and the critical objectives next to ordinary speech
ONE_OR_TWO = [
    ('"Before we go through to the scan room, which side of your chest is the sore one?"', "ASK_TOPIC performed/trainee/question (the timing phrase is not a second job)"),
    ('"Someone will be beside you the whole time, and you can ask for a break whenever you like."', "REASSURE performed/trainee (care with no new fact: one job)"),
    ('"The scan takes ten minutes. And by the way, which hand do you write with?"', "ANSWER_QUESTION performed; ASK_TOPIC performed (two sentences, two jobs)"),
    ('(the patient has just said "I go dizzy when I stand") "Dizzy how, exactly?"', "CLARIFY performed/trainee/question (follows up the patient\'s last words)"),
    ('"Have you ever fainted at the sight of a needle?"', "ASK_TOPIC performed/trainee/question (opens a topic of its own)"),
    ('"The consultant wrote in the file that you take no regular tablets."', "ACKNOWLEDGE_FACT reported/other (attributed to a record, not noted back from the patient)"),
    ('"I\'m not promising it won\'t hurt, and I won\'t pretend it\'s nothing."', "REASSURE negated/trainee (a denial of promising is not comfort)"),
]
CRITICAL_NEXT = [
    ('"Have you ever reacted badly to a plaster, iodine or any medicine?"', "STATE_ALLERGY performed/trainee/question | critical topic: yes (an allergy question is STATE_ALLERGY only, not ASK_TOPIC)"),
    ('"Full name and date of birth, please, before the injection."', "CONFIRM_IDENTITY performed/trainee (the timing phrase is not EXPLAIN_ACTION)"),
    ('"Please sign here if you are happy to go ahead."', "CONSENT_SIGN performed/trainee"),
    ('"Recommendation: please recheck his pulse in ten minutes."', "SBAR_ELEMENT performed/trainee/colleague (the content of a handover line is not also VOICE_ORDER)"),
    ('"Full name and date of birth, please. Now I am fastening the cuff round your arm."', "CONFIRM_IDENTITY performed; EXPLAIN_ACTION performed (two sentences, two jobs) | claims physical: yes (fastening the cuff)"),
    ('"I do wonder how many people really read the allergy band."', "no objective (wondering about a critical topic performs nothing)"),
]
GROUPS = (("Who, when and which assertion", MODALITY), ("Question, offer or request", FORM), ("Neighbouring objectives", BOUNDARIES), ("Grading wrong, off-question, dismissive or vague content", GRADING), ("Critical topics, physical claims, several jobs", CRITICAL_AND_MULTI), ("One job or two", ONE_OR_TWO), ("Critical objectives next to ordinary speech", CRITICAL_NEXT))


def render() -> str:
    out = ["Worked examples (original, not from any test set):"]
    for title, items in GROUPS:
        out.append(title + ":")
        out += [f"- {u} => {e}" for u, e in items]
    return "\n".join(out)
