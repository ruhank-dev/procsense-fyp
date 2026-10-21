"""Content seeds of the FRESH HELD-OUT set (HO1) for the final acceptance of the speech evaluator (ADR-0025): what each item is about, written and frozen with the specifications BEFORE any sentence exists and before the evaluator is run on the set.
Unseen by development: the reserved personas P5 and P7, four reserved topic groups (H1 cataract and eye drops, H2 dental extraction, H3 hand surgery under an arm block, H4 varicose veins and leg compression) and new semantic combinations. No seed, core or pair
is a seed of tests/synthetic_dev_v1. A seed carries no label: the label comes from the approved rules (tools/synth_dev/gold.py) and from the class and situation the plan assigns.
A seed may start with 'P: <earlier turn> || <content>' (the earlier turn is context, a patient or colleague turn)."""

ORDINARY = {
    "ASK_TOPIC": {
        "cores": ["ask whether the patient has been using any eye drops"],
        "seeds": ["ask whether the patient grinds their teeth at night", "ask whether the patient has had pins and needles in the fingers", "ask whether the patient has noticed swelling in the ankles by evening",
                  "ask whether the patient can lie flat for a long time without discomfort", "ask whether the patient wears contact lenses",
                  "ask whether the patient has had any bleeding from the gums lately"],
    },
    "CLARIFY": {
        "cores": ["P: My eyes feel a bit odd in the evenings. || ask what the patient means by odd"],
        "seeds": ["P: I was told to stop my tablets for a few days. || ask which tablets the patient was told to stop", "ask the patient to describe where exactly the tingling is in the hand",
                  "ask the dental nurse to repeat which tooth is being taken out", "ask the patient to say again how many weeks the leg has been swollen",
                  "ask the nurse what she meant by the second bay on the left", "ask the technician which side the arm block goes on"],
    },
    "ANSWER_QUESTION": {
        "cores": ["P: Will I see anything during the eye operation? || tell the patient they will see bright lights but no detail, and it does not hurt"],
        "seeds": ["P: How long will my mouth feel numb? || tell the patient several hours, so to be careful with hot drinks", "P: Can I drive myself home? || tell the patient no, someone must take them",
                  "P: Will I have a bandage on my hand? || tell the patient yes, a light dressing for a few days", "P: Do I need to wear the tight stockings in bed? || tell the patient only for the first night",
                  "P: How soon can I eat after the tooth comes out? || tell the patient soft food once the numbness has worn off",
                  # out of window (1), then state absent: no patient question waiting (2)
                  "P: Will my husband get a call afterwards? || tell the patient the nurse will phone him once they are in recovery",
                  "tell the patient the eye clinic will post the follow-up date", "tell the patient the dental ward closes at five"],
    },
    "EXPLAIN_PROCEDURE": {
        "cores": ["explain that the surgeon will replace the cloudy lens in the eye with a clear artificial one"],
        "seeds": ["explain that the tooth is loosened and lifted out after the gum is numbed", "explain that the arm is numbed by an injection near the shoulder so the hand operation is pain free",
                  "explain that the surgeon will seal the swollen vein with a thin heated wire", "explain that the patient stays awake but relaxed with a sedative drip",
                  "explain that a small band round the upper arm keeps the hand area free of blood during the operation",
                  "explain that the patient will wear a firm bandage on the leg afterwards"],
    },
    "EXPLAIN_TEST": {
        "cores": ["explain that the eye measurement checks which strength of lens will be needed"],
        "seeds": ["explain that the dental x-ray shows the roots of the tooth under the gum", "explain that the nerve test in the wrist shows how well the signal travels",
                  "explain that the leg ultrasound shows which veins are leaking", "explain that the finger blood test checks the sugar level before the sedative",
                  "explain that the heart tracing is routine before sedation", "explain that the swab inside the nose checks for a germ before the operation"],
    },
    "EXPLAIN_ACTION": {
        "cores": ["say you are about to put a drop into each eye to widen the pupils"],
        "seeds": ["say you are about to wrap a warm blanket around the patient's legs", "say you are about to swab the skin on the forearm before the cannula",
                  "say you are about to place a small pillow under the patient's wrist", "say you are about to measure the patient's calf for the stocking",
                  "say you are about to rinse the patient's mouth with a cold mouthwash", "say you are about to attach the sticky pad of the heart monitor to the patient's shoulder"],
    },
    "CHECK_UNDERSTANDING": {
        "cores": ["ask the patient to tell you back what they will do with their eye drops after the operation"],
        "seeds": ["ask the patient to tell you what they must avoid doing with the operated hand for a week", "ask the patient to say in their own words what the sedative will feel like",
                  "ask the patient whether they know when the numbness in the mouth will wear off", "ask the patient to describe how they will look after the leg dressing",
                  "ask the patient whether the arm block explanation was clear", "ask the patient to say what they will do if the bleeding starts again at home"],
    },
    "ACKNOWLEDGE_EMOTION": {
        "cores": ["tell the patient you can tell they are scared of being awake during the eye operation"],
        "seeds": ["recognise that the patient is embarrassed about their swollen legs", "recognise that the patient is dreading the injection in the mouth",
                  "recognise that the patient is worried about not being able to use the hand for a while", "recognise that the patient is upset about the cancelled earlier slot",
                  "recognise that the patient is tearful about the long trip to the hospital", "recognise that the patient is frustrated by all the repeated questions"],
    },
    "REASSURE": {
        "cores": ["tell the patient the dentist takes out teeth like this all the time and the area will be fully numb"],
        "seeds": ["tell the patient the sedative is gentle and the nurse will stay beside them", "tell the patient the eye operation is short and almost everyone is home by lunchtime",
                  "tell the patient the arm will feel heavy and warm and that is a good sign", "tell the patient the leg bruising is common and fades in a couple of weeks",
                  "tell the patient there is no rush and the team will go at their pace", "tell the patient that the numbness is working before anything starts"],
    },
    "ACKNOWLEDGE_FACT": {
        "cores": ["P: I only use drops for dry eyes. || note back that the patient uses eye drops only for dry eyes"],
        "seeds": ["P: I work as a gardener. || note back that the patient works as a gardener", "P: I have had two children. || note back that the patient has two children",
                  "P: I cannot take anything with codeine in it. || note back that codeine does not suit the patient", "P: My mother has the same vein problem. || note back that the patient's mother has the same vein problem",
                  "P: I have a dentist's phobia. || note back that the patient has a fear of dentists", "P: I am left handed. || note back that the patient is left handed"],
    },
    "CLOSED_LOOP": {
        "cores": ["P: (nurse) Give the sedative at ten forty. || repeat the sedative time back to the nurse"],
        "seeds": ["P: (nurse) The sugar is seven point four. || read the sugar result back", "P: (registrar) Keep the leg raised on two pillows. || repeat the leg instruction back",
                  "P: (nurse) Take his pulse before the block. || repeat the pulse instruction back", "P: (registrar) Send her to the dental suite at eleven. || repeat the dental suite time back",
                  "P: (nurse) The oxygen is on at one litre. || repeat the oxygen setting back",
                  # out of window (1), then state absent: no read-back waiting (2)
                  "P: (nurse) Hold the afternoon tablets. || repeat the tablet instruction back",
                  "P: (registrar) Keep the arm elevated in recovery. || repeat the elevation instruction back", "P: (nurse) Check the dressing in an hour. || repeat the dressing check back"],
    },
    "ESCALATE_SPEECH": {
        "cores": ["ask the eye surgeon to come and look at the patient's pupil"],
        "seeds": ["ask the senior dentist to review the swelling before starting", "ask the duty anaesthetist to come because the patient is very anxious",
                  "ask the lead nurse to take a decision about the delayed list", "ask the consultant to come and speak with the patient's daughter",
                  "ask the on-call registrar for advice on the dizzy spells", "ask the ward manager to help find a free bed"],
    },
    "REPORT_FAULT": {
        "cores": ["tell the nurse the warming blanket in bay four will not heat up"],
        "seeds": ["report that the eye drop fridge door will not close", "report that the dental chair will not lower", "report that the arm board has a loose screw",
                  "report that the tourniquet gauge reads zero all the time", "report that the leg elevation stand has a cracked foot",
                  "report that the bedside pulse monitor beeps without a patient attached"],
    },
    "VOICE_ORDER": {
        "cores": ["ask the nurse to arrange a wrist x-ray for the patient"],
        "seeds": ["ask the nurse to fetch the compression stockings from the store", "ask the porter to bring the wheelchair to the dental suite",
                  "ask the nurse to give the patient a glass of water with the tablet", "ask the healthcare assistant to put another pillow behind the patient",
                  "ask the nurse to ring the eye clinic and confirm the appointment", "ask the nurse to change the patient's dressing in an hour"],
    },
}

CRITICAL = {
    "CONFIRM_IDENTITY": {
        "recipient": "patient",
        "core": "ask the patient to state their name and birth date before the eye drops",
        "seeds": ["ask the patient to spell their surname so you can match it to the wristband", "say you are comparing the theatre list with the patient's wristband"],
    },
    "STATE_ALLERGY": {
        "recipient": "patient",
        "core": "ask the patient whether any medicine has ever disagreed with them",
        "seeds": ["ask whether the patient reacts to the adhesive on dressings", "ask whether the patient has ever been unwell after a dental anaesthetic injection"],
    },
    "CONSENT_SIGN": {
        "recipient": "patient",
        "core": "ask the patient whether they agree to have the procedure today",
        "seeds": ["ask the patient to write their name on the line at the bottom of the consent form", "ask the patient whether they would like another minute before signing"],
    },
    "HANDOVER_CRITICAL_ELEMENT": {
        "recipient": "colleague",
        "core": "tell the nurse the patient is allergic to latex gloves",
        "seeds": ["tell the nurse the patient took the blood thinner this morning", "tell the nurse the patient's oxygen level has dropped to eighty-nine after the sedative"],
    },
    "SBAR_ELEMENT": {
        "recipient": "colleague",
        "core": "give the situation line of a handover for a cataract operation this morning",
        "seeds": ["give the recommendation line: please repeat the blood pressure before the sedative", "give the background line: asthma well controlled, one inhaler"],
    },
}
CRITICAL_DISCUSSION = [
    "wonder aloud why identity bands are checked at every stage", "wonder aloud how many dental patients have an undeclared allergy",
    "wonder aloud whether consent is meaningful when the patient is nervous", "wonder aloud how to hand over a deteriorating patient clearly", "wonder aloud why handover structure matters on a busy list",
]

NONE_SEEDS = {
    "small_talk": ["remark that the waiting room chairs look new", "say the weekend forecast looks mild", "ask the patient whether the lift was easy to find", "say you enjoyed the quiet morning"],
    "thinking_aloud": ["think aloud about which forms are still in the tray", "think aloud about the order of the three patients after lunch", "think aloud about whether the pen has ink", "think aloud about the route to the dental suite"],
    "filler_or_noise_words": ["a quiet hmm", "an uh and a sniff", "a breath in and an um", "a short cough"],
    "off_topic_statement": ["say the bakery near the station sells good rolls", "say you are learning to play the guitar", "say the bus timetable changed this week", "say your flat has a new neighbour"],
    "near_miss": ["say hay fever has been terrible this spring (not about the patient)", "say you would never sign a petition without reading it (not about the patient)", "say the identity badge clip on your lanyard broke",
                  "say your cousin has a nut allergy at weddings (not about the patient)", "tell the reader to confirm the booking for the restaurant", "say the handover at the football club was messy (not clinical)",
                  "say the consent cookie banner on the website is irritating", "say you reported a missing parcel to the post office yesterday (past, non-clinical)"],
}

MULTI_PERFORMED = [
    (("EXPLAIN_PROCEDURE", "CHECK_UNDERSTANDING"), "explain the arm block in a sentence and ask the patient to say it back"),
    (("ACKNOWLEDGE_EMOTION", "EXPLAIN_PROCEDURE"), "recognise the nerves and explain the short eye operation"),
    (("ASK_TOPIC", "ACKNOWLEDGE_FACT"), "ask about the patient's job and note back that it involves heavy lifting"),
    (("REASSURE", "EXPLAIN_TEST"), "say the team is nearby and explain the heart tracing in a few words"),
    (("EXPLAIN_ACTION", "ESCALATE_SPEECH"), "say you are about to lower the dental chair and ask the senior dentist to come"),
    (("ESCALATE_SPEECH", "ACKNOWLEDGE_EMOTION"), "ask the senior to come and recognise that the patient is upset"),
    (("CLARIFY", "ASK_TOPIC"), "ask the patient to repeat the name of the eye drop and ask when they last used it"),
    (("EXPLAIN_PROCEDURE", "REASSURE"), "explain the vein closure and promise the area will be numb"),
    (("ACKNOWLEDGE_EMOTION", "CHECK_UNDERSTANDING"), "recognise the worry and ask the patient to say what they will do tonight"),
    (("ANSWER_QUESTION", "ACKNOWLEDGE_FACT"), "P: Am I allowed to keep my glasses? || answer yes until the theatre door and note back that the patient wears them all day"),
    (("CLOSED_LOOP", "ESCALATE_SPEECH"), "P: (nurse) The pressure is lower than before. || repeat the pressure statement back and ask the registrar to come"),
    (("ASK_TOPIC", "REASSURE"), "ask whether the patient has eaten and promise a snack straight afterwards"),
    (("EXPLAIN_TEST", "ACKNOWLEDGE_EMOTION"), "explain the ultrasound and recognise that it sounds a bit daunting"),
    (("CLOSED_LOOP", "EXPLAIN_ACTION"), "P: (nurse) The arm is ready for the block. || repeat that back and say you are about to lift the arm onto the board"),
]
MULTI_MIXED = [
    (("REASSURE", "negated", "CHECK_UNDERSTANDING"), "reassure the patient but say you have not asked what they understood"),
    (("EXPLAIN_TEST", "intended_later", "ASK_TOPIC"), "explain the wrist test and say you will ask about medicines after the doctor leaves"),
    (("ACKNOWLEDGE_FACT", "hypothetical", "ESCALATE_SPEECH"), "note back the dizzy spells and say that if they happen again the senior should be called"),
    (("ASK_TOPIC", "other_actor_does_it", "EXPLAIN_PROCEDURE"), "ask about the last meal and say the surgeon will explain the operation"),
]
