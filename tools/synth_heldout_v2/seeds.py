"""Content seeds of the FRESH HELD-OUT set v2 (HO2), the final software acceptance of the speech evaluator (ADR-0025): what each item is about, written and frozen with the specifications BEFORE any sentence exists and before the evaluator is run on the set.
New topic groups, none used by the development set or held-out v1: H5 endoscopy with sedation and bowel preparation, H6 shoulder arthroscopy with a nerve block and a sling, H7 skin lesion removal under local anaesthetic, H8 ear surgery. New seeds, new multi-intent pairs
and a new family of MIXED items in which a critical act and an independent ordinary act are performed in separate clauses (the approved semantics allow both). Authoring discipline fixed beforehand from the approved rubric: a REASSURE seed is comfort or care with no new fact; an ANSWER_QUESTION
seed gives facts; an allergy, identity or consent seed is that critical act only; a single-job seed contains no second job. A seed carries no label: the label comes from the approved rules (tools/synth_dev/gold.py) and the class and situation the plan assigns.
A seed may start with 'P: <earlier turn> || <content>'."""

ORDINARY = {
    "ASK_TOPIC": {
        "cores": ["ask whether the patient has had a camera test of the bowel before"],
        "seeds": ["ask whether the patient takes any iron tablets", "ask whether the patient has trouble swallowing", "ask what the patient normally eats in the evening",
                  "ask whether the patient was ever told they have a heart murmur", "ask whether the patient gets heartburn at night"],
    },
    "CLARIFY": {
        "cores": ["P: The preparation was horrible. || ask what the patient found horrible about it"],
        "seeds": ["P: I am not good with the sling thing. || ask what the patient means by the sling thing", "P: I had something to drink around six. || ask the patient to say again what they drank around six",
                  "P: It hurts when I lift it. || ask the patient to say which movement hurts", "P: (nurse) Go to the room at the end. || ask the nurse which room at the end she means",
                  "ask the porter to repeat the name of the ward"],
    },
    "ANSWER_QUESTION": {
        "cores": ["P: Will I remember the camera test? || tell the patient most people remember nothing because of the sedative"],
        "seeds": ["P: Can I take my morning tablets? || tell the patient the blood pressure tablet is fine with a sip of water", "P: How long until I can drive? || tell the patient not until the day after the sedation",
                  "P: Will there be a big scar on my shoulder? || tell the patient only three tiny marks", "P: Can my friend collect me? || tell the patient yes, the friend can wait in the day unit",
                  "P: Will I have to stay overnight? || tell the patient no, they go home this evening if they are steady",
                  "tell the patient the day unit shuts at seven", "tell the patient the pharmacy is on the ground floor"],
    },
    "EXPLAIN_PROCEDURE": {
        "cores": ["explain that a thin flexible camera is passed through the mouth to look at the stomach"],
        "seeds": ["explain that the skin lesion is cut out and the edges are stitched together", "explain that the nerve block makes the whole shoulder numb and the arm heavy",
                  "explain that the surgeon makes two small holes in the shoulder for the camera and tools", "explain that a small ear tube lets fluid drain from behind the eardrum",
                  "explain that the patient stays lightly asleep for the whole test"],
    },
    "EXPLAIN_TEST": {
        "cores": ["explain that the blood test shows whether the blood count is low"],
        "seeds": ["explain that the stool test looks for hidden blood", "explain that the shoulder x-ray shows the joint space", "explain that the skin swab checks for infection",
                  "explain that the hearing test measures how softly the patient can hear", "explain that the clotting screen checks how quickly the blood forms a clot"],
    },
    "EXPLAIN_ACTION": {
        "cores": ["say you are about to place the oxygen prongs under the patient's nose"],
        "seeds": ["say you are about to lift the patient's arm into the sling", "say you are about to mark the skin where the cut will go", "say you are about to dim the lights a little",
                  "say you are about to raise the head of the trolley", "say you are about to take the patient's blood pressure on the other arm"],
    },
    "CHECK_UNDERSTANDING": {
        "cores": ["ask the patient to tell you in their own words how the bowel preparation works"],
        "seeds": ["ask the patient to say back what they must do if the sling feels too tight", "ask the patient whether the plan for the day makes sense to them",
                  "ask the patient to tell you what they will do about driving", "ask the patient to repeat when they may eat again",
                  "ask the patient to explain how they will look after the stitches"],
    },
    "ACKNOWLEDGE_EMOTION": {
        "cores": ["tell the patient you can see how embarrassed they feel about the test"],
        "seeds": ["recognise that the patient is nervous about the needle in the neck", "recognise that the patient feels guilty about taking up the day", "recognise that the patient is annoyed at having fasted so long",
                  "recognise that the patient is anxious about a scar on the face", "recognise that the patient is homesick and tired"],
    },
    "REASSURE": {
        "cores": ["tell the patient they will be well looked after and that no one will hurry them"],
        "seeds": ["tell the patient you will be right here beside them", "tell the patient they are in capable hands", "tell the patient they can ask to pause at any moment",
                  "tell the patient the team will take good care of them whatever happens", "tell the patient there is no hurry at all"],
    },
    "ACKNOWLEDGE_FACT": {
        "cores": ["P: I take a water tablet in the morning. || note back that the patient takes a water tablet in the morning"],
        "seeds": ["P: I fainted at my last blood test. || note back that the patient fainted at the last blood test", "P: I do not eat meat. || note back that the patient does not eat meat",
                  "P: My daughter will drive me home. || note back that the daughter will drive the patient home", "P: I had a bad cough in the spring. || note back that the patient had a bad cough in the spring",
                  "P: I have worn a hearing aid for years. || note back that the patient has worn a hearing aid for years"],
    },
    "CLOSED_LOOP": {
        "cores": ["P: (nurse) Start the sedation at nine fifteen. || repeat the start time back to the nurse"],
        "seeds": ["P: (registrar) Give the antibiotic with the first dose. || repeat the antibiotic instruction back", "P: (nurse) The potassium is four point one. || read the potassium back",
                  "P: (registrar) Keep the sling on until the review. || repeat the sling instruction back", "P: (nurse) Recheck the temperature in twenty minutes. || repeat the temperature recheck back",
                  "P: (nurse) Offer him tea at noon. || repeat the tea instruction back",
                  "P: (nurse) The bay is ready now. || repeat that back", "P: (registrar) Hold the afternoon list. || repeat that back"],
    },
    "ESCALATE_SPEECH": {
        "cores": ["ask the endoscopy lead to come to the room"],
        "seeds": ["ask the anaesthetic registrar to decide about the sedation level", "ask the senior sister to take over the paperwork", "ask the consultant to talk to the worried relative",
                  "ask the clinical lead to come and look at the patient's swollen arm",
                  "ask the theatre manager for a decision about the late start"],
    },
    "REPORT_FAULT": {
        "cores": ["tell the nurse the suction canister is cracked"],
        "seeds": ["report that the endoscopy screen flickers", "report that the nerve block tray is missing a syringe", "report that the sling dispenser is empty", "report that the pulse monitor cable is frayed",
                  "report that the trolley brake sticks"],
    },
    "VOICE_ORDER": {
        "cores": ["ask the nurse to arrange a shoulder x-ray for the patient"],
        "seeds": ["ask the nurse to give the patient a laxative", "ask the porter to take the patient to the endoscopy room", "ask the healthcare assistant to bring a clean gown",
                  "ask the nurse to book a follow-up appointment", "ask the nurse to order a hearing test"],
    },
}

CRITICAL = {
    "CONFIRM_IDENTITY": {"recipient": "patient", "core": "ask the patient to give their name and birth date before the sedation", "seeds": ["ask the patient to read their name from the wristband"]},
    "STATE_ALLERGY": {"recipient": "patient", "core": "ask the patient whether they have any allergies to medicines", "seeds": ["ask whether the patient reacts badly to antiseptic skin wash"]},
    "CONSENT_SIGN": {"recipient": "patient", "core": "ask the patient whether they are willing for the test to go ahead", "seeds": ["ask the patient to put their signature at the foot of the form"]},
    "HANDOVER_CRITICAL_ELEMENT": {"recipient": "colleague", "core": "tell the nurse the patient has not been fasting since midnight", "seeds": ["tell the nurse the blood pressure has fallen to eighty over fifty"]},
    "SBAR_ELEMENT": {"recipient": "colleague", "core": "give the situation line of a handover for a shoulder operation this afternoon", "seeds": ["give the assessment line: breathing comfortable and pulse steady"]},
}
CRITICAL_DISCUSSION = [
    "wonder aloud whether bands ever get put on the wrong wrist", "wonder aloud how allergies are recorded in other hospitals", "wonder aloud how long a consent form should be",
    "wonder aloud whether the handover template is too long", "wonder aloud whether wristband scanners are better than reading",
]

NONE_SEEDS = {
    "small_talk": ["remark that the corridor smells of fresh paint", "say the sunshine has finally come back", "ask the patient whether they found a seat in the waiting area"],
    "thinking_aloud": ["think aloud about where the spare gowns are kept", "think aloud about how many forms are left to file", "think aloud about whether the kettle is on"],
    "filler_or_noise_words": ["a soft mm", "a throat clear and an er", "a long breath out"],
    "off_topic_statement": ["say your sister is moving house on Saturday", "say the cinema is showing an old film this weekend", "say your bicycle tyre went flat"],
    "near_miss": ["say the allergy aisle in the supermarket is huge (not about the patient)", "joke that you need a signature from your landlord", "say the identity card scanner at the library is slow",
                  "tell the reader to confirm that the pizza order went through", "say your friend's handover of the flat keys went well (not clinical)", "say the consent form for the gym app is far too long (not clinical)",
                  "say your cat has a food allergy (not about the patient)", "say you reported a pothole to the council last month (past, non-clinical)"],
}

MULTI_PERFORMED = [
    (("EXPLAIN_TEST", "ANSWER_QUESTION"), "P: Why do I need the blood test? || explain that it checks the blood count and answer that the result comes back within the hour"),
    (("VOICE_ORDER", "EXPLAIN_ACTION"), "ask the nurse to hand over the blood pressure cuff and say you are about to put it on the patient's arm"),
    (("CLOSED_LOOP", "REPORT_FAULT"), "P: (nurse) Use the monitor in bay two. || repeat that back and report that the screen of that monitor is cracked"),
    (("ACKNOWLEDGE_EMOTION", "CLARIFY"), "recognise that the patient is upset and ask what exactly upset them"),
    (("REASSURE", "CLARIFY"), "promise the patient that no one will rush them and ask them to say again what they were worried about"),
    (("ANSWER_QUESTION", "CLARIFY"), "P: Is it the same doctor? || answer that a different doctor will see them and ask the patient to repeat which doctor they expected"),
    (("ACKNOWLEDGE_FACT", "EXPLAIN_ACTION"), "P: I get light-headed on standing. || note back the light-headedness and say you are about to check the lying and standing blood pressure"),
    (("ACKNOWLEDGE_FACT", "ESCALATE_SPEECH"), "P: My arm has gone numb. || note back the numb arm and ask the senior to come and look"),
    (("VOICE_ORDER", "REASSURE"), "ask the nurse to bring a warm blanket and tell the patient they will be warm in a moment"),
    (("VOICE_ORDER", "ACKNOWLEDGE_EMOTION"), "ask the nurse to dim the lights and recognise that the bright room is unsettling for the patient"),
    (("CLOSED_LOOP", "ACKNOWLEDGE_EMOTION"), "P: (nurse) The patient will go in first. || repeat that back and recognise that the patient looks relieved"),
    (("CLARIFY", "ESCALATE_SPEECH"), "ask the nurse to repeat which bed and ask the senior to come to that bed"),
    (("CLARIFY", "VOICE_ORDER"), "ask the nurse which sample she means and ask her to send it to the lab"),
    (("EXPLAIN_TEST", "EXPLAIN_ACTION"), "explain that the pulse oximeter reads the oxygen in the blood and say you are about to clip it on the finger"),
]
MULTI_MIXED = [
    (("EXPLAIN_TEST", "negated", "REASSURE"), "explain the swab test but say you have not offered any reassurance about it"),
    (("CLARIFY", "hypothetical", "ASK_TOPIC"), "ask the patient to repeat the name of the tablet and say that if they stay vague you might then ask about all their medicines"),
    (("REASSURE", "other_actor_does_it", "EXPLAIN_PROCEDURE"), "promise the patient that the team is here and say the surgeon will explain the operation"),
    (("ESCALATE_SPEECH", "reported_about_someone_else", "VOICE_ORDER"), "ask the sister to come and mention that the registrar already asked the porter to move the patient"),
]
# a critical act and an independent ordinary act, in separate clauses or sentences (the approved semantics credit the ordinary one, never the critical behaviour)
MIXED_CRITICAL = [
    (("CONFIRM_IDENTITY", "EXPLAIN_ACTION"), "Ask the patient for their name and birth date. Then say you are about to clip the pulse sensor on the finger."),
    (("STATE_ALLERGY", "ASK_TOPIC"), "Ask whether the patient reacts to any medicine. Then ask whether the patient snores."),
    (("CONSENT_SIGN", "CHECK_UNDERSTANDING"), "Ask the patient to sign the consent form. Then ask them to say back what they have agreed to."),
    (("HANDOVER_CRITICAL_ELEMENT", "VOICE_ORDER"), "Tell the nurse the patient took a blood thinner today. Then ask the nurse to fetch another trolley."),
    (("SBAR_ELEMENT", "REPORT_FAULT"), "Give the background line of a handover for the patient. Then report that the call bell in the bay is dead."),
    (("CONFIRM_IDENTITY", "ACKNOWLEDGE_EMOTION"), "Ask the patient for name and birth date. Then recognise that the patient seems nervous."),
    (("STATE_ALLERGY", "REASSURE"), "Ask whether the patient has an allergy to dressings. Then promise the patient they will be well looked after."),
    (("CONSENT_SIGN", "REASSURE"), "Ask whether the patient is happy for the test to go ahead. Then tell the patient you will stay with them."),
]
