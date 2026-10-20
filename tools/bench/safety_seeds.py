"""Seed families for the unsafe-speech safety evaluation (tools/bench/metamorph.py; ADR-0023). SYNTHETIC: written by the builder (a language model), NOT by a person who works on the simulator, and NOT independent.

A FAMILY is one meaning of an act in three surface wordings (`surfaces`, each a sentence that PERFORMS the act when a trainee says it now) and three verb forms (`inf`: "ask about your appetite", `pp` and `past`: "asked about your appetite") so the same meaning can be put under every unsafe
status by a TEMPLATE without anyone re-labelling it: the expected outcome of a variant follows from the construction, and is PROVISIONAL until a person has reviewed it (docs/testing/safety_challenge_review.md). Acts are those of content/assessment/taxonomy.json; nothing clinical is invented.
Every family belongs to exactly one split (`dev`, `challenge` or `final`), assigned here by hand and before any run; a family never crosses a split, and the templates of tools/bench/safety_templates.py are split the same way, so a challenge or final sentence shares neither its seed nor its template with a development one.
The seeds are new: none repeats a family of tests/action_bench_v1 (its topics are sleep, cough, heavy, left or right, the drip, the porter, the ECG order and so on); the builder also removes any generated sentence that is a near-duplicate of a sentence of an existing set.
"""
# act -> [(split, [3 surfaces], (inf, pp, past))]   two dev families, one challenge, one final per ordinary act
ORDINARY = {
    "ASK_TOPIC": [
        ("dev", ["How is your appetite at the moment?", "Are you eating normally these days?", "Tell me about how much you have been eating."], ("ask about your appetite", "asked about your appetite", "asked about your appetite")),
        ("dev", ["Have you had any trouble with your bowels?", "Is your bowel habit normal for you?", "Any changes in your toilet habits lately?"], ("ask about your bowels", "asked about your bowels", "asked about your bowels")),
        ("challenge", ["Do you ever feel dizzy when you stand up?", "Does standing up make you light-headed?", "Have you felt faint on getting out of a chair?"], ("ask about dizziness", "asked about dizziness", "asked about dizziness")),
        ("final", ["Who looks after you at home?", "Is there anyone at home to help you?", "Do you live with someone who can support you?"], ("ask about help at home", "asked about help at home", "asked about help at home")),
    ],
    "CLARIFY": [
        ("dev", ["Sorry, what did you say after that?", "Could you go back over the last bit?", "I lost you at the end, can you say it again?"], ("ask you to go back over that", "asked you to go back over that", "asked you to go back over that")),
        ("dev", ["What does a funny feeling mean to you?", "When you say strange, how do you mean?", "Can you put the odd feeling into other words?"], ("ask what you mean by strange", "asked what you meant by strange", "asked what you meant by strange")),
        ("challenge", ["Which arm do you mean, the one with the band?", "Is it the arm with the drip you are talking about?", "Do you mean this arm or the other one?"], ("check which arm you mean", "checked which arm you meant", "checked which arm you meant")),
        ("final", ["Did you say it hurts when you breathe in?", "So the pain comes when you take a breath, is that right?", "Let me check, it hurts on breathing in?"], ("check when it hurts", "checked when it hurts", "checked when it hurt")),
    ],
    "ANSWER_QUESTION": [
        ("dev", ["You will be able to go home the same evening.", "Yes, you can leave this evening once you are awake.", "You should be home by tonight."], ("tell you that you can go home tonight", "told you that you can go home tonight", "told you that you could go home tonight")),
        ("dev", ["It will not hurt while you are asleep.", "You will feel nothing during the operation.", "No, you will not feel any pain on the table."], ("tell you that you will feel no pain", "told you that you will feel no pain", "told you that you would feel no pain")),
        ("challenge", ["Your family can wait in the day room.", "Yes, your daughter is welcome to wait nearby.", "Someone from your family can stay close by."], ("tell you that your family can wait", "told you that your family can wait", "told you that your family could wait")),
        ("final", ["You can have a sip of water until six.", "Yes, water is fine for a little while longer.", "Small sips of water are allowed for now."], ("tell you that you can have water", "told you that you can have water", "told you that you could have water")),
    ],
    "EXPLAIN_PROCEDURE": [
        ("dev", ["Afterwards you will have a small dressing over each cut.", "At the end of the operation each cut gets a small dressing.", "When it is finished we cover the cuts with dressings."], ("explain the dressings", "explained the dressings", "explained the dressings")),
        ("dev", ["The anaesthetist will give you medicine through the drip to make you sleep.", "You go to sleep through the drip in your arm.", "We use the cannula to send you off to sleep."], ("explain how you go to sleep", "explained how you go to sleep", "explained how you would go to sleep")),
        ("challenge", ["After the operation you will wake up in the recovery area.", "You wake up in recovery with a nurse beside you.", "Recovery is where you come round after the operation."], ("explain the recovery area", "explained the recovery area", "explained the recovery area")),
        ("final", ["The surgeon will make a few small cuts and use a camera.", "A camera goes in through a tiny cut so the surgeon can see.", "We operate through keyhole cuts with a camera."], ("explain the keyhole approach", "explained the keyhole approach", "explained the keyhole approach")),
    ],
    "EXPLAIN_TEST": [
        ("dev", ["The blood test shows whether you are anaemic.", "We check your blood count to look for anaemia.", "This sample tells us how much haemoglobin you have."], ("explain the blood count", "explained the blood count", "explained the blood count")),
        ("dev", ["The x ray shows us the shape of your chest.", "A chest film lets us see your lungs and heart.", "This picture is of your lungs."], ("explain the chest film", "explained the chest film", "explained the chest film")),
        ("challenge", ["The urine test checks for infection.", "We test the sample for signs of a water infection.", "Your urine can tell us whether there is an infection."], ("explain the urine test", "explained the urine test", "explained the urine test")),
        ("final", ["The heart tracing records the rhythm of your heart.", "This trace shows how regular your heartbeat is.", "The ECG draws the electrical pattern of the heart."], ("explain the heart tracing", "explained the heart tracing", "explained the heart tracing")),
    ],
    "EXPLAIN_ACTION": [
        ("dev", ["I am going to feel your tummy now.", "Now I will press gently on your abdomen.", "I'll examine your tummy in a moment, tell me if it hurts."], ("examine your abdomen now", "examined your abdomen", "examined your abdomen")),
        ("dev", ["I am just going to take your pulse.", "Now I will count your pulse at the wrist.", "I'm taking your pulse now."], ("take your pulse now", "took your pulse", "took your pulse")),
        ("challenge", ["I will clip a probe on your finger to check your oxygen.", "Now I'm putting a small clip on your finger.", "This clip goes on your finger so I can read your oxygen."], ("put the clip on your finger now", "put the clip on your finger", "put the clip on your finger")),
        ("final", ["I'm going to look in your mouth now.", "Now I will check your throat with a light.", "Open wide please, I am looking at your throat."], ("look at your throat now", "looked at your throat", "looked at your throat")),
    ],
    "CHECK_UNDERSTANDING": [
        ("dev", ["Tell me what you will do about eating tonight.", "What have you understood about food before the operation?", "Can you say back what I asked you to do tonight?"], ("ask you to repeat the eating instructions", "asked you to repeat the eating instructions", "asked you to repeat the eating instructions")),
        ("dev", ["Does that explanation make sense to you?", "Is everything I said clear?", "Have I been clear about what happens next?"], ("check that the explanation was clear", "checked that the explanation was clear", "checked that the explanation was clear")),
        ("challenge", ["Can you tell me why we are doing the operation?", "In your own words, what is the operation for?", "What do you understand the operation will fix?"], ("ask you to say what the operation is for", "asked you to say what the operation is for", "asked you to say what the operation is for")),
        ("final", ["How will you take your tablets tomorrow morning?", "What will you do with your tablets tomorrow?", "Tell me how you will manage your tablets in the morning."], ("check how you will take your tablets", "checked how you will take your tablets", "checked how you would take your tablets")),
    ],
    "ACKNOWLEDGE_EMOTION": [
        ("dev", ["It is perfectly understandable to feel frightened.", "I can tell this is a frightening day for you.", "Anyone would feel scared in your position."], ("acknowledge that you are frightened", "acknowledged that you are frightened", "acknowledged that you were frightened")),
        ("dev", ["You seem very tense, and that is natural.", "I can see how tense you are.", "It looks like this is weighing on you."], ("acknowledge how tense you are", "acknowledged how tense you are", "acknowledged how tense you were")),
        ("challenge", ["It must be hard waiting for so long.", "I know the waiting has been difficult for you.", "Waiting like this is wearing, I can see that."], ("acknowledge how hard the wait is", "acknowledged how hard the wait is", "acknowledged how hard the wait was")),
        ("final", ["I can see you are upset about the delay.", "You sound upset, and I understand why.", "It is understandable to be annoyed by the delay."], ("acknowledge that you are upset", "acknowledged that you are upset", "acknowledged that you were upset")),
    ],
    "REASSURE": [
        ("dev", ["The team will look after you carefully.", "You are being looked after by an experienced team.", "We will take good care of you."], ("reassure you about the team", "reassured you about the team", "reassured you about the team")),
        ("dev", ["Most people go through this without any problems.", "This is a very common operation and it usually goes well.", "People generally do very well after this."], ("reassure you that it usually goes well", "reassured you that it usually goes well", "reassured you that it usually went well")),
        ("challenge", ["We will make sure you are comfortable afterwards.", "You will have pain relief as soon as you need it.", "We will keep your pain under control."], ("reassure you about pain control", "reassured you about pain control", "reassured you about pain control")),
        ("final", ["There is a nurse with you the whole time.", "You will never be left on your own.", "Someone will stay with you throughout."], ("reassure you that you will not be alone", "reassured you that you would not be alone", "reassured you that you would not be alone")),
    ],
    "ACKNOWLEDGE_FACT": [
        ("dev", ["Okay, so you stopped smoking last year.", "Right, you gave up cigarettes a year ago.", "Noted, no smoking since last year."], ("note that you stopped smoking", "noted that you stopped smoking", "noted that you had stopped smoking")),
        ("dev", ["Thank you, so you live on the second floor.", "I see, you are on the second floor with no lift.", "Understood, a second floor flat."], ("note where you live", "noted where you live", "noted where you lived")),
        ("challenge", ["So the last time you ate was at noon.", "Right, you had lunch at midday and nothing since.", "I've got it, nothing to eat since noon."], ("note when you last ate", "noted when you last ate", "noted when you last ate")),
        ("final", ["I see you had your appendix out as a child.", "Okay, your appendix was removed years ago.", "Thank you, so no appendix since childhood."], ("note your earlier operation", "noted your earlier operation", "noted your earlier operation")),
    ],
    "CLOSED_LOOP": [
        ("dev", ["Confirming: paracetamol one gram now.", "Reading back, one gram of paracetamol now.", "So that's paracetamol, one gram, given now."], ("read back the paracetamol order", "read back the paracetamol order", "read back the paracetamol order")),
        ("dev", ["Understood, I'll let you know when the bloods are sent.", "Got it, I will tell you as soon as the bloods have gone.", "Okay, I'll report back when the samples are away."], ("confirm I will tell you when the bloods are sent", "confirmed I would tell you when the bloods were sent", "confirmed I would tell you when the bloods were sent")),
        ("challenge", ["Repeating that: fluids at a hundred an hour.", "So fluids at a hundred millilitres an hour, confirmed.", "Read back: a hundred an hour of fluid."], ("read back the fluid rate", "read back the fluid rate", "read back the fluid rate")),
        ("final", ["Understood, oxygen up to four litres, I'll tell you once it's on.", "Okay, four litres of oxygen, I'll say when it's running.", "Got it, four litres, will report when done."], ("confirm the oxygen order", "confirmed the oxygen order", "confirmed the oxygen order")),
    ],
    "ESCALATE_SPEECH": [
        ("dev", ["Could the anaesthetic registrar come and see this patient?", "I would like the anaesthetic registrar to review him.", "Can you ask the anaesthetic registrar to come?"], ("ask for the anaesthetic registrar", "asked for the anaesthetic registrar", "asked for the anaesthetic registrar")),
        ("dev", ["I'm not happy with this, I need my senior now.", "This needs a senior decision, please call someone.", "I need help from the consultant on this one."], ("ask for the consultant", "asked for the consultant", "asked for the consultant")),
        ("challenge", ["Please bleep the surgical registrar for me.", "Can someone get the surgical registrar to come down?", "I need the surgical registrar on the ward."], ("ask for the surgical registrar", "asked for the surgical registrar", "asked for the surgical registrar")),
        ("final", ["Can the sister take over, I'm out of my depth.", "I need the ward sister to help me with this.", "Please get the sister, I need her advice."], ("ask for the ward sister", "asked for the ward sister", "asked for the ward sister")),
    ],
    "REPORT_FAULT": [
        ("dev", ["The oxygen outlet at bed four has no flow.", "There is no oxygen coming from the wall at bed four.", "Bed four's oxygen is not working."], ("report the oxygen fault at bed four", "reported the oxygen fault at bed four", "reported the oxygen fault at bed four")),
        ("dev", ["The infusion pump keeps alarming and will not run.", "This pump will not start and just keeps beeping.", "The drip pump is faulty, it won't run."], ("report the faulty pump", "reported the faulty pump", "reported the faulty pump")),
        ("challenge", ["The call bell in bay two is not ringing.", "Nothing happens when you press the bell in bay two.", "The bay two bell is dead."], ("report the broken bell", "reported the broken bell", "reported the broken bell")),
        ("final", ["The theatre light has gone out over the table.", "The overhead light in theatre is not working.", "We have lost the main light in theatre."], ("report the theatre light", "reported the theatre light", "reported the theatre light")),
    ],
    "VOICE_ORDER": [
        ("dev", ["Please send off a urine sample.", "Could we get a urine sample sent to the lab?", "Can someone send a urine specimen?"], ("order a urine sample", "ordered a urine sample", "ordered a urine sample")),
        ("dev", ["Could you book a bed in recovery for her?", "Please arrange a recovery bed for this patient.", "We need a recovery bed booked."], ("ask for a recovery bed", "asked for a recovery bed", "asked for a recovery bed")),
        ("challenge", ["Please get a portable chest x ray done.", "Can we have a portable chest film?", "Could radiology come and do a chest x ray here?"], ("order a portable x ray", "ordered a portable x ray", "ordered a portable x ray")),
        ("final", ["Please call the porter to take him to theatre.", "Can you ask the porter to come for him?", "Get a porter to collect the patient, please."], ("call the porter", "called the porter", "called the porter")),
    ],
}
# acts for which "I will X" IS the act when said by the speaker, so a plan wrapper would not be a different status (as in tools/bench/build_act_dataset.py)
NO_INTENT = {"EXPLAIN_ACTION", "CLOSED_LOOP"}
# critical content: speech may NEVER earn credit for these, whatever the wrapper (the engine credits them from the verified action)
CRITICAL = {
    "CONFIRM_IDENTITY": [
        ("dev", ["Let me check your wristband against your notes.", "I am reading your wristband to confirm who you are.", "I need to confirm your name and date of birth first."], ("check your wristband", "checked your wristband", "checked your wristband")),
        ("challenge", ["Is this your name on the band?", "Can you read me the name on your wristband?", "Please say your name and birthday for me."], ("confirm your name", "confirmed your name", "confirmed your name")),
        ("final", ["The band and the chart match, so you are who we think you are.", "Your wristband matches your notes.", "The details on the band agree with the chart."], ("match the band to the chart", "matched the band to the chart", "matched the band to the chart")),
    ],
    "STATE_ALLERGY": [
        ("dev", ["Have you ever had a bad reaction to a medicine?", "Is there any drug that disagrees with you?", "Do any tablets make you come out in a rash?"], ("ask about medicine reactions", "asked about medicine reactions", "asked about medicine reactions")),
        ("challenge", ["Are there any foods that make your throat swell?", "Do you react to shellfish or nuts?", "Does any food ever make you come up in hives?"], ("ask about food reactions", "asked about food reactions", "asked about food reactions")),
        ("final", ["Has latex ever given you a rash?", "Do rubber gloves ever make your skin sore?", "Any trouble with latex or plasters?"], ("ask about latex", "asked about latex", "asked about latex")),
    ],
    "CONSENT_SIGN": [
        ("dev", ["Are you happy for us to go ahead with the operation?", "Do you agree to have the surgery today?", "Is it all right with you to proceed?"], ("ask for your agreement", "asked for your agreement", "asked for your agreement")),
        ("challenge", ["Please sign here if you are happy to proceed.", "I need your signature on the form.", "Would you put your name on the consent form?"], ("ask you to sign", "asked you to sign", "asked you to sign")),
        ("final", ["You have agreed to the operation, so I'll record that.", "I am noting that you consent to the surgery.", "That is your consent recorded."], ("record your consent", "recorded your consent", "recorded your consent")),
    ],
    "HANDOVER_CRITICAL_ELEMENT": [
        ("dev", ["She has not had anything to eat since midnight.", "He last ate at midnight and has had nothing since.", "Nil by mouth since twelve."], ("hand over the fasting state", "handed over the fasting state", "handed over the fasting state")),
        ("challenge", ["His oxygen saturation has fallen to eighty-eight.", "Her sats are dropping, now eighty-eight percent.", "The oxygen level is down to eighty-eight."], ("hand over the falling oxygen", "handed over the falling oxygen", "handed over the falling oxygen")),
        ("final", ["Please note she reacted badly to penicillin before.", "He is allergic to penicillin, it caused a rash.", "Penicillin gave her a rash in the past."], ("hand over the penicillin reaction", "handed over the penicillin reaction", "handed over the penicillin reaction")),
    ],
    "SBAR_ELEMENT": [
        ("dev", ["Situation: Mr Adeyemi, hernia repair this afternoon.", "Situation: a day-case hernia repair for Mr Adeyemi.", "Situation, Mr Adeyemi is here for a hernia repair."], ("give the situation", "gave the situation", "gave the situation")),
        ("challenge", ["Background: he has well-controlled blood pressure.", "Background, his blood pressure is controlled with tablets.", "Background is hypertension on treatment."], ("give the background", "gave the background", "gave the background")),
        ("final", ["Recommendation: please review him before theatre.", "Recommendation, I would like him seen before he goes in.", "My recommendation is a review before theatre."], ("give the recommendation", "gave the recommendation", "gave the recommendation")),
    ],
}
