"""Seed families for the ProcSense act benchmark (tools/bench/build_act_dataset.py; ADR-0020). SYNTHETIC: written by the builder, not by a person who works on the simulator, and labelled by construction.

A FAMILY is one meaning of an act in three surface wordings (`surfaces`) and three verb forms (`inf`: "explain the procedure", `pp`: "explained the procedure", `past`: the simple past, used after "I") so that the same meaning can be put under every assertion status
(done, denied, intended, hypothetical, reported, said by someone else, broken off) without a person re-labelling it: the label of a variant follows from the construction, not from a reading of the text. Acts are the ones of content/assessment/taxonomy.json; no act is invented.
Families are assigned to the `dev` or `test` split here, by hand and before any approach was run on the benchmark, two and two per act; a family never crosses the split, so a test sentence shares neither wording nor verb phrase with a development one.
"""
ORDINARY = {
    "ASK_TOPIC": [
        ("dev", ["How have you been sleeping lately?", "Could you tell me about your sleep?", "Do you sleep well at night?"], ("ask about your sleep", "asked about your sleep", "asked about your sleep")),
        ("dev", ["Do you get any cough?", "Have you had a cough recently?", "Tell me about any coughing."], ("ask about your cough", "asked about your cough", "asked about your cough")),
        ("test", ["What was it that brought you in today?", "Why have you come to see us?", "Tell me what has been bothering you."], ("ask what brought you in", "asked what brought you in", "asked what brought you in")),
        ("test", ["Are you able to climb a flight of stairs?", "Can you manage the stairs at home?", "How are you with stairs?"], ("ask about your stairs", "asked about your stairs", "asked about your stairs")),
    ],
    "CLARIFY": [
        ("dev", ["When you say heavy, what do you mean?", "What do you mean by heavy?", "Heavy, in what way?"], ("clarify what heavy means", "clarified what heavy means", "clarified what heavy meant")),
        ("dev", ["Sorry, could you say that again?", "I didn't catch that, once more please.", "Can you repeat the last part?"], ("ask you to repeat that", "asked you to repeat that", "asked you to repeat that")),
        ("test", ["Do you mean the left side or the right?", "Which side exactly, left or right?", "Is that the left side you are pointing to?"], ("clarify which side", "clarified which side", "clarified which side")),
        ("test", ["Can you describe that in a bit more detail?", "Tell me more about that, please.", "Could you explain what you felt a little more?"], ("ask for more detail", "asked for more detail", "asked for more detail")),
    ],
    "ANSWER_QUESTION": [
        ("dev", ["Yes, you will be asleep for all of it.", "You will be fully asleep throughout.", "Yes, you will not be awake during it."], ("answer that you will be asleep", "answered that you will be asleep", "answered that you would be asleep")),
        ("dev", ["It usually takes about an hour.", "The whole thing lasts roughly an hour.", "About an hour, give or take."], ("answer how long it takes", "answered how long it takes", "answered how long it took")),
        ("test", ["No, you will not need a drip afterwards.", "There will be no drip when you wake up.", "You won't have a drip afterwards."], ("answer about the drip", "answered about the drip", "answered about the drip")),
        ("test", ["Yes, someone will take you home.", "Yes, we will arrange your transport home.", "A porter will take you back to the ward."], ("answer about getting home", "answered about getting home", "answered about getting home")),
    ],
    "EXPLAIN_PROCEDURE": [
        ("dev", ["First we put in a small tube for the medicine.", "We start by placing a small line in your arm.", "A small tube goes into your arm first."], ("explain the first step", "explained the first step", "explained the first step")),
        ("dev", ["The surgeon will make a small cut and look inside with a camera.", "We use a camera through a small cut.", "A camera goes in through a small incision."], ("explain the camera", "explained the camera", "explained the camera")),
        ("test", ["Afterwards you will wake up in the recovery area.", "When it is over, you wake in recovery.", "You come round in the recovery room."], ("explain the recovery", "explained the recovery", "explained the recovery")),
        ("test", ["A mask will give you oxygen while you go to sleep.", "You breathe oxygen through a mask as you drift off.", "First you breathe oxygen from a mask."], ("explain the mask", "explained the mask", "explained the mask")),
    ],
    "EXPLAIN_TEST": [
        ("dev", ["The blood test shows how well your kidneys work.", "This blood test checks your kidneys.", "We take blood to look at your kidney function."], ("explain the kidney test", "explained the kidney test", "explained the kidney test")),
        ("dev", ["The scan takes pictures of your belly.", "This scan makes pictures of your abdomen.", "The scan lets us see inside your tummy."], ("explain the scan", "explained the scan", "explained the scan")),
        ("test", ["The heart tracing shows how your heart is beating.", "This tracing records your heart rhythm.", "The tracing of your heart shows the rhythm."], ("explain the heart tracing", "explained the heart tracing", "explained the heart tracing")),
        ("test", ["The urine sample checks for infection.", "We test the urine to look for infection.", "This urine test looks for signs of infection."], ("explain the urine test", "explained the urine test", "explained the urine test")),
    ],
    "EXPLAIN_ACTION": [
        ("dev", ["I am going to put a cuff on your arm now.", "I'm putting the blood pressure cuff on your arm.", "Now I'll place the cuff around your arm."], ("put the cuff on your arm", "put the cuff on your arm", "put the cuff on your arm")),
        ("dev", ["I'm going to listen to your chest with this.", "I will now listen to your chest.", "Listening to your chest now."], ("listen to your chest", "listened to your chest", "listened to your chest")),
        ("test", ["I am just clipping this onto your finger.", "I'll clip a small sensor onto your finger.", "This little clip goes on your finger now."], ("clip the sensor on your finger", "clipped the sensor on your finger", "clipped the sensor on your finger")),
        ("test", ["I am going to raise the head of your bed.", "I'll lift the top of the bed a little.", "Raising the head of the bed now."], ("raise the head of your bed", "raised the head of your bed", "raised the head of your bed")),
    ],
    "CHECK_UNDERSTANDING": [
        ("dev", ["Does that all make sense to you?", "Is that clear so far?", "Do you follow what I have said?"], ("check that it makes sense", "checked that it makes sense", "checked that it made sense")),
        ("dev", ["Can you tell me in your own words what will happen?", "In your own words, what is going to happen?", "Could you say back to me what happens next?"], ("ask you to say it in your own words", "asked you to say it in your own words", "asked you to say it in your own words")),
        ("test", ["What questions do you have for me?", "Is there anything you would like to ask me?", "Do you have any questions at all?"], ("ask for questions", "asked for questions", "asked for questions")),
        ("test", ["Would it help if I went through that again?", "Shall I go over that once more?", "Should I explain that again?"], ("offer to go over it again", "offered to go over it again", "offered to go over it again")),
    ],
    "ACKNOWLEDGE_EMOTION": [
        ("dev", ["I can see you are nervous.", "You look nervous, and that is understandable.", "It is clear that you are feeling nervous."], ("acknowledge your nerves", "acknowledged your nerves", "acknowledged your nerves")),
        ("dev", ["It is natural to feel anxious before an operation.", "Feeling anxious beforehand is completely normal.", "Many people feel anxious at this point."], ("say anxiety is normal", "said anxiety is normal", "said anxiety was normal")),
        ("test", ["I am sorry, this must be very hard for you.", "That sounds really hard, I am sorry.", "I can imagine how hard this is."], ("say I am sorry it is hard", "said I was sorry it is hard", "said I was sorry it was hard")),
        ("test", ["You seem upset, and that makes sense.", "I can tell you are upset.", "It is okay to be upset about this."], ("acknowledge you are upset", "acknowledged you were upset", "acknowledged you were upset")),
    ],
    "REASSURE": [
        ("dev", ["You are in good hands here.", "The team looking after you is very experienced.", "We will take good care of you."], ("reassure you", "reassured you", "reassured you")),
        ("dev", ["This is a very common operation.", "We do this operation all the time.", "It is a routine operation for us."], ("say it is routine", "said it is routine", "said it was routine")),
        ("test", ["There is nothing to be afraid of.", "Please do not worry about it.", "There is no need to worry."], ("tell you not to worry", "told you not to worry", "told you not to worry")),
        ("test", ["The pain will settle soon.", "The discomfort should ease quickly.", "That pain should wear off before long."], ("say the pain will settle", "said the pain will settle", "said the pain would settle")),
    ],
    "ACKNOWLEDGE_FACT": [
        ("dev", ["Thank you, I have noted that you live alone.", "Okay, noted that you live on your own.", "I see, you live alone."], ("note that you live alone", "noted that you live alone", "noted that you lived alone")),
        ("dev", ["Right, so the pain started on Tuesday.", "Understood, it began on Tuesday.", "Okay, Tuesday is when it started."], ("note when the pain started", "noted when the pain started", "noted when the pain started")),
        ("test", ["Thanks, I have written down that you take tablets for your sugar.", "Noted, you take tablets for diabetes.", "Okay, you take diabetes tablets, thank you."], ("note your sugar tablets", "noted your sugar tablets", "noted your sugar tablets")),
        ("test", ["Got it, you had the same pain last year.", "I see, you had this before last year.", "Okay, this happened last year too."], ("note the earlier pain", "noted the earlier pain", "noted the earlier pain")),
    ],
    "CLOSED_LOOP": [
        ("dev", ["Repeating that back: one litre over four hours.", "Reading back: a litre over four hours.", "To confirm, one litre over four hours."], ("read back the fluids order", "read back the fluids order", "read back the fluids order")),
        ("dev", ["Understood, I will call you when the porter arrives.", "Got it, I'll tell you once the porter is here.", "Okay, I will let you know as soon as the porter comes."], ("promise to call when the porter arrives", "promised to call when the porter arrived", "promised to call when the porter arrived")),
        ("test", ["Confirming, bloods first and then the ECG.", "Just to confirm: bloods, then the ECG.", "So that is bloods and then the ECG, confirmed."], ("confirm the order of tests", "confirmed the order of tests", "confirmed the order of tests")),
        ("test", ["Okay, oxygen up to four litres and I will report back in ten minutes.", "Understood, four litres of oxygen and I will update you in ten minutes.", "Got it, four litres, and I will tell you how she is in ten minutes."], ("repeat the oxygen order", "repeated the oxygen order", "repeated the oxygen order")),
    ],
    "ESCALATE_SPEECH": [
        ("dev", ["I would like the senior doctor to see him.", "Can the senior doctor come and review him?", "I need the senior doctor to look at him."], ("ask for the senior doctor", "asked for the senior doctor", "asked for the senior doctor")),
        ("dev", ["This is beyond what I can manage safely, so I am calling the registrar.", "I cannot manage this alone, I am calling the registrar.", "I am escalating this to the registrar."], ("call the registrar", "called the registrar", "called the registrar")),
        ("test", ["Please bleep the on-call anaesthetist.", "Could someone bleep the anaesthetist on call?", "I want the on-call anaesthetist bleeped."], ("bleep the anaesthetist", "bleeped the anaesthetist", "bleeped the anaesthetist")),
        ("test", ["I am worried and want the consultant informed.", "The consultant needs to know about this.", "I am going to inform the consultant."], ("inform the consultant", "informed the consultant", "informed the consultant")),
    ],
    "REPORT_FAULT": [
        ("dev", ["The infusion pump keeps alarming and will not run.", "This pump will not start and it keeps beeping.", "The pump is alarming and not working."], ("report the pump fault", "reported the pump fault", "reported the pump fault")),
        ("dev", ["The monitor shows no trace at all.", "The monitor trace has gone completely.", "There is no signal on the monitor."], ("report the monitor fault", "reported the monitor fault", "reported the monitor fault")),
        ("test", ["The oxygen cylinder is empty.", "This oxygen cylinder has run out.", "The cylinder here reads empty."], ("report the empty cylinder", "reported the empty cylinder", "reported the empty cylinder")),
        ("test", ["The call bell is not working in this bay.", "The bell in this bay does not ring.", "The call bell here is broken."], ("report the call bell", "reported the call bell", "reported the call bell")),
    ],
    "VOICE_ORDER": [
        ("dev", ["Please arrange a chest x ray.", "I would like a chest x ray requested.", "Request a chest x ray please."], ("order a chest x ray", "ordered a chest x ray", "ordered a chest x ray")),
        ("dev", ["Can we get an ECG done now?", "Please do an ECG now.", "I want an ECG done straight away."], ("order an ECG", "ordered an ECG", "ordered an ECG")),
        ("test", ["Request a full blood count and a CRP.", "Please send a full blood count and a CRP.", "I would like a full blood count and CRP."], ("order the bloods", "ordered the bloods", "ordered the bloods")),
        ("test", ["Please ask the porter to take her to the scanner.", "Get the porter to take her to scan.", "I would like the porter to take her to the scanner."], ("order the porter", "ordered the porter", "ordered the porter")),
    ],
}
# Critical acts: these must NEVER be credited from speech under any ordinary act. Each family is a sentence that performs the critical act without its obvious words.
CRITICAL = {
    "STATE_ALLERGY": [
        ("dev", ["Has any medicine ever made you break out in lumps?", "Did a tablet once bring you out in lumps?", "Have you ever come out in lumps after a medicine?"]),
        ("dev", ["Is there something that makes your lips swell?", "Do your lips ever swell up after something you eat?", "Does anything make your face puff up?"]),
        ("test", ["Was there a drug that left you struggling to breathe?", "Did any medicine ever make it hard to breathe?", "Has a medicine ever taken your breath away?"]),
        ("test", ["Which medicines are you not able to have?", "Is there any medicine you cannot have?", "Tell me the medicines that do not agree with you."]),
    ],
    "CONFIRM_IDENTITY": [
        ("dev", ["Say your full name and your birthday for me.", "Can you tell me your name and when you were born?", "Please give me your name and date of birth."]),
        ("dev", ["I am reading your bracelet; is that your name on it?", "Is the name on this band yours?", "Does the name on your wrist band match yours?"]),
        ("test", ["Let me check that the number on your arm is yours.", "I will compare the number on your wrist to your notes.", "Is this the hospital number that belongs to you?"]),
        ("test", ["Before we start, who are you according to your paperwork?", "I need to confirm who you are first.", "Let me make sure you are the right patient."]),
    ],
    "CONSENT_SIGN": [
        ("dev", ["Are you happy for us to go ahead today?", "Is it still your wish to go ahead with this?", "Do you want us to go ahead with it?"]),
        ("dev", ["Please put your signature at the bottom of the page.", "Would you sign at the bottom here?", "Sign here if you agree to the operation."]),
        ("test", ["You can change your mind at any time, do you still agree?", "Do you still agree to this operation?", "Are you still willing to have this done?"]),
        ("test", ["I need to be sure you understand the risks and accept them.", "Do you accept the risks that I have described?", "You have heard the risks; do you accept them?"]),
    ],
    "SBAR_ELEMENT": [
        ("dev", ["I am calling about the man in bed six whose breathing is faster.", "I'm phoning about a patient whose breathing has got faster.", "The reason for my call is his faster breathing."]),
        ("dev", ["My recommendation is that someone reviews him now.", "I recommend that he is reviewed straight away.", "What I would like is for him to be reviewed urgently."]),
        ("test", ["Background: she came in with a fever yesterday.", "She was admitted yesterday with a fever.", "To give you the background, she has had a fever since yesterday."]),
        ("test", ["I think the infection is spreading.", "My assessment is that the infection is getting worse.", "I believe she has an infection that is spreading."]),
    ],
    "HANDOVER_CRITICAL_ELEMENT": [
        ("dev", ["She has had nothing to eat since midnight.", "He has not eaten since last night.", "She has been fasting since midnight."]),
        ("dev", ["His blood pressure has been falling all afternoon.", "Her pressure has been dropping since lunch.", "The blood pressure keeps going down."]),
        ("test", ["Make sure the next team knows about her allergy to penicillin.", "Tell the next team about the penicillin allergy.", "Please pass on that she is allergic to penicillin."]),
        ("test", ["The consent form has not been signed yet.", "He has not signed the consent form.", "There is no signed consent form for her yet."]),
    ],
}
# Sentences that are not any act: small talk, thinking aloud, fragments (expected: no credit, act NONE).
NONE_SENTENCES = [
    ("dev", ["Hmm, let me think.", "Okay, okay.", "Right, one moment.", "Er, where was I?"]),
    ("dev", ["What time is it?", "It is quite warm in here.", "I think it is going to rain.", "The canteen closes at two."]),
    ("test", ["Mm, never mind.", "Ah, sorry, just a second.", "So, yes, well.", "Let me find the pen."]),
    ("test", ["The car park is full today.", "I forgot my lunch.", "Did you see the match?", "Where is the lift?"]),
]
