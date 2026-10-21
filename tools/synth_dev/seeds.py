"""Content seeds for the direct synthetic development set: WHAT each item is about, written before any sentence and frozen with the specification.
A seed carries no label: the label comes from the approved rules (gold.py) and from the class/situation the plan assigns. A seed may start with 'P: <earlier turn> || <content>'; the earlier turn is context (a patient or colleague turn), not the trainee's utterance.
CORES are the shared scenarios of the contrast sets: one per (act, k); the performed member and the five non-performed members are written from the same core."""

# ordinary acts: cores (2 per act, shared by a contrast set) and standalone seeds (A standalone, then the out-of-window and state-absent items)
ORDINARY = {
    "ASK_TOPIC": {
        "cores": ["ask whether the patient has eaten or drunk anything this morning", "ask which regular medicines the patient takes"],
        "seeds": ["ask whether the patient smokes", "ask how much alcohol the patient drinks in a week", "ask whether the patient gets breathless climbing stairs", "ask whether anyone in the family had problems with an anaesthetic",
                  "ask whether the patient has had a cough or cold recently", "ask who is at home to help after the operation", "ask whether the patient snores at night", "ask about loose teeth or dentures",
                  "ask about past operations", "ask whether the patient had trouble waking up from an anaesthetic before", "ask when the pain started", "ask how bad the pain is now"],
    },
    "CLARIFY": {
        "cores": ["P: I get a bit dizzy now and then. || ask what exactly the patient means by dizzy", "P: (mumbled, hard to hear) || ask the patient to say the last answer again"],
        "seeds": ["P: I have a funny feeling after meals. || ask what the patient means by a funny feeling", "ask the patient to repeat the name of their tablets because the room is noisy",
                  "ask the nurse to repeat the dose she just gave", "P: It was nil by mouth from midnight, the registrar said. || ask what nil by mouth covers (does water count)", "ask the patient to explain what they mean by a tight chest",
                  "ask the nurse which bay the patient is moving to", "ask the patient to explain what on and off means for their breathing", "ask the porter to repeat which theatre to take the patient to",
                  "P: I was a bit rough last time. || ask what rough meant", "ask the nurse to say again which drip is running", "ask the patient to repeat how long the numbness lasted"],
    },
    "ANSWER_QUESTION": {
        "cores": ["P: How long will the operation take? || tell the patient it takes about an hour", "P: Will I be able to eat afterwards? || tell the patient yes, once the nurse says it is fine, a few hours later"],
        "seeds": ["P: Can my wife stay with me? || tell the patient the wife can wait on the ward and see them after", "P: Will it hurt? || tell the patient there is a small pinch for the drip, then they will be asleep, with painkillers afterwards",
                  "P: When can I go home? || tell the patient most people go home the same evening if all is well", "P: Where will I wake up? || tell the patient in the recovery area with a nurse beside them",
                  "P: Will you be there the whole time? || tell the patient the anaesthetist stays with them throughout", "P: How many people will be in the room? || tell the patient several: surgeon, nurses and the anaesthetist",
                  "P: Will I feel sick afterwards? || tell the patient some people do and medicine is given to help", "P: Am I first on the list? || tell the patient they are second, probably late morning",
                  # out of window (2) and state absent: no patient question is waiting (8)
                  "P: Will I wake up in pain? || tell the patient painkillers are given before they wake", "P: Is my son allowed in? || tell the patient visitors are welcome after recovery",
                  "tell the patient visiting hours start at two", "tell the patient they will stay in recovery for about an hour", "tell the patient they can have a drink once they are fully awake",
                  "tell the patient the operation list runs until the afternoon", "tell the patient a nurse will bring them back to the ward", "tell the patient a family member can phone the ward later",
                  "tell the patient breakfast will be saved for them", "tell the patient the car park closes at six"],
    },
    "EXPLAIN_PROCEDURE": {
        "cores": ["explain that the operation removes the gallbladder through small cuts in the tummy", "explain that the anaesthetic will make the patient sleep through the operation"],
        "seeds": ["explain that the surgeon will repair the hernia through a cut in the groin", "explain that the patient will have a spinal injection and be numb from the waist down", "explain that a camera and thin tools are used through tiny cuts",
                  "explain that the patient will be monitored the whole time in theatre", "explain that after the operation the patient wakes in recovery", "explain that a breathing tube goes in while asleep and comes out before waking",
                  "explain that the knee operation replaces the worn joint", "explain that a local anaesthetic will numb the area for the small procedure", "explain that the operation is planned and not an emergency",
                  "explain that the cut will be closed with stitches that dissolve", "explain that the patient will have a drip for fluids during the operation"],
    },
    "EXPLAIN_TEST": {
        "cores": ["explain that blood is taken to check how the kidneys are working", "explain that the ECG records the electrical activity of the heart"],
        "seeds": ["explain that the finger clip measures the oxygen in the blood", "explain that the chest x-ray shows the heart and lungs", "explain that the blood test checks for anaemia before the operation",
                  "explain that the blood pressure cuff measures how hard the heart pushes blood round", "explain that the urine test checks for infection", "explain that the pregnancy test is routine before an operation",
                  "explain that the sticky chest dots let the machine trace the heartbeat", "explain that the swab checks for a skin germ", "explain that the blood sugar test uses a tiny prick on the finger",
                  "explain that the clotting test checks how well the blood clots", "explain that the scan checks the neck vessels"],
    },
    "EXPLAIN_ACTION": {
        "cores": ["say you are about to put a cuff on the patient's arm to take the blood pressure", "say you are about to listen to the patient's chest with the stethoscope"],
        "seeds": ["say you are about to clip the sensor on the patient's finger", "say you are about to put three sticky dots on the chest", "say you are about to feel the patient's tummy and ask them to tell you if it hurts",
                  "say you are about to put a small tube in the back of the hand for the drip", "say you are about to take the patient's temperature", "say you are about to help the patient sit up",
                  "say you are about to look at the patient's mouth and teeth", "say you are about to tilt the bed back a little", "say you are about to put the oxygen mask on",
                  "say you are about to draw a little blood from the arm", "say you are about to check the patient's pulse at the wrist"],
    },
    "CHECK_UNDERSTANDING": {
        "cores": ["ask the patient to say in their own words what will happen this morning", "ask the patient whether the explanation of the anaesthetic made sense"],
        "seeds": ["ask the patient to tell you back what they will do about eating and drinking", "ask the patient whether they have understood what the drip is for", "ask the patient to tell you what happens in recovery",
                  "ask the patient to explain in their own words why they need the monitor", "ask the patient whether anything about the plan is unclear", "ask the patient to repeat how to ask for help on the ward",
                  "ask the patient to say what they understood about the sleeping injection", "ask the patient whether the visiting arrangements are clear", "ask the patient to tell you what they will feel when waking up",
                  "ask the patient whether they follow what the nurse just explained", "ask the patient to summarise what you have just told them"],
    },
    "ACKNOWLEDGE_EMOTION": {
        "cores": ["tell the patient you can see they are worried about the operation", "tell the patient it is understandable to feel nervous today"],
        "seeds": ["recognise that the patient looks upset", "recognise that the patient seems tired and fed up with waiting", "recognise that the patient is frightened of waking up during the operation",
                  "recognise that the patient is worried about their family at home", "recognise that the patient sounds anxious about the needle", "recognise that the patient seems low today",
                  "recognise that being in hospital is stressful", "recognise that the patient looks embarrassed about the gown", "recognise that the patient is anxious because of the long wait",
                  "recognise that the patient is angry about the delay", "recognise that the patient seems overwhelmed by all the questions"],
    },
    "REASSURE": {
        "cores": ["tell the patient the team will look after them and stay with them the whole time", "tell the patient the team does this operation every day"],
        "seeds": ["tell the patient the anaesthetist will watch them closely throughout", "tell the patient they will have painkillers when they wake up", "tell the patient the nurses are right outside if they need anything",
                  "tell the patient a lot of people feel nervous and it usually goes well", "tell the patient the needle is quick and the scratch fades fast", "tell the patient they will not be left alone in the waiting area",
                  "tell the patient the team is well prepared for them", "tell the patient the delay is only because of the busy list, nothing is wrong", "tell the patient they will be warm and comfortable",
                  "tell the patient their family will be told as soon as they wake", "tell the patient they are in safe hands"],
    },
    "ACKNOWLEDGE_FACT": {
        "cores": ["note back that the patient takes a daily tablet for blood pressure", "note back that the patient had a chest infection last month"],
        "seeds": ["P: I live on my own. || note back that the patient lives alone", "P: I last ate at eight last night. || note back the time the patient said they last ate", "P: I use an inhaler when I get wheezy. || note back that the patient uses an inhaler",
                  "P: My knee has been bad for a year. || note back that the knee has been bad for a year", "P: I had my appendix out as a child. || note back the childhood appendix operation", "P: I drink about two glasses of wine at weekends. || note back the weekend wine",
                  "P: I walk the dog every day. || note back that the patient walks the dog daily", "P: I stopped smoking two years ago. || note back that the patient stopped smoking", "P: My sister had trouble with anaesthetics. || note back the sister's anaesthetic problem", "P: I take iron tablets from the chemist. || note back the iron tablets"],
    },
    "CLOSED_LOOP": {
        "cores": ["P: (nurse) Please set the oxygen to two litres. || repeat the oxygen instruction back to the nurse", "P: (registrar) Take his blood pressure again in five minutes. || repeat the blood pressure instruction back to the registrar"],
        "seeds": ["P: (nurse) Run the fluids at one hundred an hour. || repeat the fluid rate back", "P: (registrar) Call the porter at ten. || repeat the porter call time back", "P: (nurse) Recheck the sats after he sits up. || repeat the sats recheck back",
                  "P: (registrar) Move him to bay four. || repeat the bay move back", "P: (nurse) The potassium came back at five point two. || read the potassium result back", "P: (nurse) Check his temperature in fifteen minutes. || repeat the temperature check back",
                  "P: (registrar) Bleep the anaesthetist after the scan. || repeat the bleep instruction back", "P: (nurse) Put the cot sides up. || repeat the cot sides instruction back",
                  "P: (nurse) The haemoglobin is ten point eight. || read the haemoglobin back", "P: (registrar) Stop the infusion at noon. || repeat the stop time back",
                  # state absent (no read-back waiting): eight
                  "P: (nurse) Keep him nil by mouth until the registrar says. || repeat the nil by mouth instruction back", "P: (nurse) Bed two needs the monitor on. || repeat the monitor instruction back",
                  "P: (registrar) Send the sample to the lab. || repeat the sample instruction back", "P: (nurse) Roll him every two hours. || repeat the rolling instruction back",
                  "P: (registrar) Chart the fluids hourly. || repeat the charting instruction back", "P: (nurse) The glucose is six point one. || read the glucose back",
                  "P: (registrar) Hold his morning tablets. || repeat the hold instruction back", "P: (nurse) Take him round at half past two. || repeat the transfer time back"],
    },
    "ESCALATE_SPEECH": {
        "cores": ["ask the anaesthetist to come and see the patient now", "ask the senior nurse to come and help with the patient"],
        "seeds": ["ask the registrar for a decision about the delayed list", "ask the consultant to review the patient before theatre", "ask the charge nurse to take over this patient while you fetch equipment",
                  "ask a senior to come because you are unsure about the plan", "ask the surgeon to speak to the patient about the consent questions", "ask the anaesthetic registrar to advise on the patient's cough",
                  "ask a senior colleague to help because the patient is distressed", "ask the on-call doctor to come to bay three", "ask the sister to make a decision about moving the patient",
                  "ask the theatre coordinator to help with the changed order", "ask the consultant to decide whether to go ahead"],
    },
    "REPORT_FAULT": {
        "cores": ["tell the nurse the suction at bed two is not working", "tell the nurse the monitor screen keeps going blank"],
        "seeds": ["report that the oxygen supply at bed three is not working", "report that the bed brakes do not hold", "report that the overhead light in bay one is out", "report that the drip pump alarm keeps sounding with no cause",
                  "report that the call bell in bay two does not ring", "report that there is water leaking by the sink", "report that the trolley wheel is jammed", "report that the blood pressure cuff will not inflate",
                  "report that the printer in the office is broken", "report that the door to theatre will not stay open"],
    },
    "VOICE_ORDER": {
        "cores": ["ask the nurse for a chest x-ray for the patient", "ask the porter to bring the trolley for theatre"],
        "seeds": ["ask the nurse for a full blood count and kidney tests", "ask the nurse to give the patient a painkiller", "ask the nurse to call the lab to chase the results", "ask the porter to take the patient to recovery",
                  "ask the healthcare assistant to bring another blanket", "ask the nurse to set up the monitor in bay three", "ask the nurse to send a urine sample", "ask the nurse to book the scan for this afternoon",
                  "ask the nurse not to give the next dose yet", "ask the nurse to phone pharmacy for the antibiotic", "ask the nurse to change the dressing"],
    },
}

# critical acts: core (1 per act, shared by a contrast set) and ten standalone genuine seeds (D) each
CRITICAL = {
    "CONFIRM_IDENTITY": {
        "recipient": "patient",
        "core": "ask the patient to tell you their full name and date of birth",
        "seeds": ["ask the patient to read out the name on the wristband", "say that the wristband matches the notes", "ask the patient to confirm the date of birth on the form", "ask whether the name on the wristband is spelt correctly",
                  "ask the patient to give their full name before you begin", "say that you are checking the wristband against the consent form", "ask the patient to confirm their address and date of birth", "ask the patient to tell you their name and what operation they are having",
                  "say that name and date of birth both match the theatre list", "ask the patient to check that the wristband details are right"],
    },
    "STATE_ALLERGY": {
        "recipient": "patient",
        "core": "ask the patient whether they are allergic to anything",
        "seeds": ["ask what happened when the patient took penicillin", "ask whether the patient has a latex allergy", "ask about reactions to eggs or soya", "ask what sort of reaction the patient had to the antibiotic",
                  "ask about reactions to plasters or tape", "ask whether the patient has ever reacted to a medicine", "say back that penicillin gave the patient a rash", "check that the patient has no known drug allergies",
                  "ask whether iodine skin wash has ever caused a rash", "ask whether the allergy is written on the wristband"],
    },
    "CONSENT_SIGN": {
        "recipient": "patient",
        "core": "ask the patient whether they are happy for the operation to go ahead",
        "seeds": ["ask the patient to sign the consent form where marked", "ask the patient to confirm they have read the consent form", "ask whether the patient has questions before signing", "say that the form is ready to sign if they agree",
                  "ask the patient to confirm they agree to the anaesthetic", "ask whether the patient feels pushed into the operation", "offer the pen and ask the patient to sign", "remind the patient they can change their mind before the operation",
                  "ask the patient to check the date next to their signature", "ask the patient to confirm they agree with what the form says"],
    },
    "HANDOVER_CRITICAL_ELEMENT": {
        "recipient": "colleague",
        "core": "tell the nurse the patient is allergic to penicillin",
        "seeds": ["tell the nurse the patient has not fasted and had toast at seven", "tell the nurse the name on the wristband does not match the notes", "tell the nurse the oxygen saturation has fallen from ninety-six to eighty-eight",
                  "tell the nurse the blood pressure is falling and is now ninety over fifty", "tell the nurse the pulse is racing at one hundred and thirty", "tell the nurse the patient has low blood sugar and is confused",
                  "tell the nurse the patient has a latex allergy", "tell the nurse the patient had a drink at six this morning", "tell the nurse the date of birth on the consent form is wrong", "tell the nurse the patient's breathing is getting worse"],
    },
    "SBAR_ELEMENT": {
        "recipient": "colleague",
        "core": "give the situation line of a handover for a planned hip replacement this morning",
        "seeds": ["give the situation line: Mr Khan, planned gallbladder surgery, theatre two at nine", "give the background line: diabetic on tablets, no previous operations", "give the assessment line: observations stable, patient calm",
                  "give the recommendation line: I would like the anaesthetist to review before theatre", "give the situation line: Mrs Okoye, knee replacement, second on the list", "give the background line: hypertension, takes one tablet daily",
                  "give the assessment line: pain well controlled, comfortable", "give the recommendation line: please keep him nil by mouth until the registrar reviews", "give the situation line: Mr Brown, hernia repair, day case",
                  "give the background line: seen in the pre-op clinic last week"],
    },
}

# critical topic without claiming completion: one plain discussion per topic (5 standalone)
CRITICAL_DISCUSSION = [
    "wonder aloud why identity checks are repeated so many times", "wonder aloud how often an allergy is missed in busy lists", "wonder aloud whether patients really read a consent form",
    "wonder aloud how to tell a colleague a patient's condition is getting worse", "wonder aloud how a structured handover should be ordered",
]

# no-act items (50): subtype counts small_talk 10, thinking_aloud 10, filler 8, off_topic 10, near_miss 12
NONE_SEEDS = {
    "small_talk": ["remark on the weather today", "ask the patient whether the hospital car park was busy", "say it has been a long week", "comment on the radio playing in the corridor", "mention the football result from last night",
                   "say you like the new ward colours", "remark that it is nearly lunchtime", "say the coffee machine is working again", "comment on the busy corridor", "say it is good to see the sun"],
    "thinking_aloud": ["mutter that you need to find the next form", "think aloud about which bay comes next", "think aloud about where you left your pen", "think aloud about what time the list starts",
                       "think aloud that you should tidy the trolley", "think aloud about which bed is free", "think aloud about the order of the list", "think aloud about the walk to the ward", "think aloud about the tea break", "think aloud about the shoes you forgot"],
    "filler_or_noise_words": ["just a hesitation sound", "a hesitation and a cough", "a thinking sound and then nothing", "a sigh", "an um and a pause", "a short okay with nothing after it", "a clearing of the throat", "a hmm"],
    "off_topic_statement": ["say your bus was late this morning", "say you must remember to buy milk", "say your phone battery is low", "mention a holiday you are planning", "say you are meeting a friend tonight",
                            "say the lift is slow today", "talk about a television programme", "say you need a haircut", "say the train strike starts tomorrow", "say your coursework is due on Friday"],
    "near_miss": ["say allergy season is awful this year (not about the patient)", "say identity parades are a thing in crime dramas", "joke that you would sign anything to get a coffee", "say your brother has a latex balloon allergy at parties (not about the patient)",
                  "say the consent screen on your phone is annoying", "say you were handed a leaflet on the bus", "say you need a handover from the night shift cleaning rota (not clinical)", "tell the reader to check the score of the match",
                  "tell the reader to confirm the ticket booking for the weekend", "say the wristband at the music festival was itchy", "say you will explain the football rules to your cousin", "say you reported the broken coffee machine to the canteen last week (a past, non-clinical remark)"],
}

# multi-intent pairs: (acts, content). 24 with two performed ordinary acts and 6 with one performed and one not performed (second act's situation given)
MULTI_PERFORMED = [
    (("ACKNOWLEDGE_EMOTION", "REASSURE"), "see the patient is nervous and promise the team will stay with them"),
    (("EXPLAIN_PROCEDURE", "EXPLAIN_ACTION"), "explain briefly what the operation involves and say you are about to put the monitor on"),
    (("EXPLAIN_PROCEDURE", "CHECK_UNDERSTANDING"), "explain the keyhole operation and ask the patient to say it back"),
    (("ASK_TOPIC", "CLARIFY"), "ask about medicines and ask what the patient meant by a water tablet"),
    (("ACKNOWLEDGE_FACT", "ASK_TOPIC"), "note back that the patient takes a daily tablet and ask whether there are any others"),
    (("EXPLAIN_TEST", "REASSURE"), "explain the heart trace and say it does not hurt at all"),
    (("REPORT_FAULT", "ESCALATE_SPEECH"), "report the broken suction and ask the charge nurse to come"),
    (("VOICE_ORDER", "ESCALATE_SPEECH"), "ask the nurse for a blood test and ask the registrar to come and see the patient"),
    (("CLOSED_LOOP", "VOICE_ORDER"), "repeat back the oxygen instruction and ask the porter to fetch the trolley"),
    (("ANSWER_QUESTION", "REASSURE"), "P: How long will I be asleep? || answer about an hour and say they will be looked after"),
    (("EXPLAIN_ACTION", "REASSURE"), "say you are putting the drip in and promise it is quick"),
    (("CLARIFY", "ACKNOWLEDGE_FACT"), "ask the patient to repeat the dose and note back the tablet they named"),
    (("EXPLAIN_PROCEDURE", "EXPLAIN_TEST"), "explain the operation and the blood test before it"),
    (("ACKNOWLEDGE_EMOTION", "ASK_TOPIC"), "see the patient is tired and ask when they last slept"),
    (("ANSWER_QUESTION", "CHECK_UNDERSTANDING"), "P: Can I eat now? || answer no, not until after the operation, and ask them to say what they will do"),
    (("ESCALATE_SPEECH", "REPORT_FAULT"), "ask the sister for help and report that the pump will not start"),
    (("EXPLAIN_ACTION", "ACKNOWLEDGE_EMOTION"), "say you are going to help the patient sit up and recognise it is uncomfortable for them"),
    (("REASSURE", "CHECK_UNDERSTANDING"), "reassure the patient about the needle and ask if they have any worries left"),
    (("ASK_TOPIC", "ACKNOWLEDGE_EMOTION"), "ask about the pain and recognise that it sounds frightening"),
    (("EXPLAIN_TEST", "CHECK_UNDERSTANDING"), "explain the clotting test and ask if it makes sense"),
    (("VOICE_ORDER", "CLOSED_LOOP"), "ask the nurse to book the scan and repeat back the time she gives"),
    (("EXPLAIN_PROCEDURE", "ACKNOWLEDGE_EMOTION"), "explain the spinal injection and recognise that it sounds scary"),
    (("ACKNOWLEDGE_FACT", "REASSURE"), "note back the inhaler use and say the team will keep it handy"),
    (("CLARIFY", "EXPLAIN_ACTION"), "ask the patient to repeat which arm and say you are about to take the blood pressure on it"),
]
# performed act + a second act that is NOT performed (the situation of the second act)
MULTI_MIXED = [
    (("EXPLAIN_ACTION", "negated", "REASSURE"), "say you are about to put the cuff on but you have not yet reassured about the squeeze"),
    (("ACKNOWLEDGE_EMOTION", "intended_later", "EXPLAIN_PROCEDURE"), "recognise the worry and say you will explain the operation after the nurse arrives"),
    (("ASK_TOPIC", "hypothetical", "ESCALATE_SPEECH"), "ask about the stairs and say that if the answer worries you the senior might need to be called"),
    (("REPORT_FAULT", "other_actor_does_it", "ESCALATE_SPEECH"), "report the broken light and say the sister will call the engineer"),
    (("EXPLAIN_TEST", "negated", "CHECK_UNDERSTANDING"), "explain the heart trace but say you have not asked whether it made sense"),
    (("REASSURE", "intended_later", "ASK_TOPIC"), "reassure the patient and say you will ask about medicines in a minute"),
]
