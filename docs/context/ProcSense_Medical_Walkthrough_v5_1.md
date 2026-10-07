<!-- Converted from ProcSense_Medical_Walkthrough_v5_1.pdf — line-by-line conversion: every source line kept in order; headings marked; hard line breaks kept; Markdown-special characters backslash-escaped; running page headers shown as horizontal rule + italic lines. Format conversion only: no summarising, rewriting or correction. -->

# ProcSense


# The Beginner's Medical


# Walkthrough

A step-by-step guide to everything that happens to the patient — and  
everything the trainee must notice, decide and do — in the two ProcSense  
VR scenarios, written for software engineers with zero medical background  
Scenarios S1 — Planned keyhole gallbladder removal (elective laparoscopic cholecystectomy). S2 — Emergency removal of an  
inflamed appendix (emergency appendectomy).  
Version v5.1 — final clinical QA, evidence verification and consistency pass on v5 (October 2026). READY FOR CLINICIAN  
VALIDATION — not clinically approved.  
Project FYP ProcSense • FAST NUCES Islamabad  
Readers 1) The software team (no medical knowledge assumed). 2) The supervising surgeon and anaesthetist, who check every  
step, number and branch.  
Status DRAFT — AWAITING CLINICAL VALIDATION. Clinical numbers are now source-backed and cited; only genuine game-design  
values are marked SIMULATOR DESIGN VALUE, and only genuinely context-dependent matters are marked  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION.  
Reviewer Surgeon: \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_ Anaesthetist: \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_ Date: \_\_\_\_\_\_\_\_\_\_  
Safety first (full statement at the end of this book)  
ProcSense is a teaching game, not a medical tool. It must never be used to make decisions about real patients. Every  
patient in it is invented. Every number in this book is a draft value for building and reviewing the simulator, not a  
medical instruction. Local hospital rules always win. A qualified surgeon and anaesthetist must approve the final  
content before it is implemented.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 2*


## WHAT CHANGED IN v5.1 (final QA pass)

v5.1 is a targeted quality-assurance pass. The curriculum, structure, stacked cases and VR mapping are  
unchanged; only accuracy, sourcing, wording and consistency were corrected. Status: ready for clinician  
validation — not clinically approved.  
\# Change in v5.1 Where  
1 Four items freshly verified against sources (see the verification log in Part H): adult resting heart rate (AHA);  
ECG intervals (QTc per AHA/ACCF/HRS 2009; PR and QRS marked as educational reference values; QRS wording  
corrected from 'below 120 ms' to roughly 80–100 ms normal, ≥120 ms wide); every educational dose (RCUK, UKKA,  
JBDS, SmPCs, ASHP, Association of Anaesthetists); shock index (Koch 2019, Rivers 1994).  
Parts 4, 6, 8, H  
2 Medication doses corrected or caveated: IV paracetamol now follows the SmPC (≥4 h between doses; 3 g/day  
if risk factors); enoxaparin now risk-dependent (20 mg moderate risk, 40 mg high risk, 20 mg if CrCl 15–30);  
ondansetron tied to its PONV indication; hydrocortisone limited to patients on long-term steroids or with adrenal  
insufficiency; NICE CG174 fluid scope exclusions stated; UKKA calcium/insulin–glucose and JBDS glucose doses  
added as labelled educational examples. The trainee is never independent prescriber or administrator.  
Part 8; what-if  
3 Shock index re-sourced and re-worded as a supporting indicator; the unsupported '>1.3' band was removed. Part 4; Part 17  
4 Cholangitis case renamed 'Patient awaiting elective cholecystectomy who develops acute cholangitis' and  
re-reasoned so the elective pathway visibly stops continuing unchanged; drainage timing is left to the responsible  
clinicians (TG18).  
Part D (S1-D)  
5 Ectopic pregnancy is framed only as a differential diagnosis / diagnostic diversion, never as an appendicitis or  
appendectomy complication or an intra-operative branch.  
Parts 5, 11, 14, G  
6 Appendectomy timing rewritten as specific, sourced and contextual: generally prompt; WSES 2025 recommends  
laparoscopic appendectomy within 24 h of admission when surgery is indicated; severity, sepsis, anaesthetic  
assessment and resources set the actual time.  
Parts 2, 8, 11, G, H  
7 Absolute-language audit: clinically meaningful 'always/never/must' statements made contextual (kept for  
genuine safety requirements such as identity checks and specimen labelling); abnormal ECG/BP/K/Na/Hb/INR rows  
now lead to review and a clinician decision rather than automatic cancellation; SpO2, sepsis/NEWS2 and pregnancy  
logic re-audited.  
Throughout  
8 Evidence-type letters aligned to the QA specification: A directly supported, B clinically derived, C  
context-dependent, D ProcSense simulation design. VR events that are teaching devices are labelled 'ProcSense  
educational/simulation extension — clinician validation required'.  
How to use; Parts C,  
F, G, H  
9 Final status statement updated. Part I  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 3*


## WHAT CHANGED IN v5

v5 is a targeted clinical-accuracy, consistency and training-depth pass on v4. Nothing useful was removed.  
\# Change Where  
1 International scope. The Pakistan-specific layer was removed from the core curriculum. Context-dependent items  
are now labelled CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION and adapted to  
the target institution's approved protocol at implementation.  
Labels; Part I  
2 Trainee role corrected. All 'prescribe/give/treat' language in trainee columns now means: identify the problem,  
communicate, initiate the workflow; a qualified clinician confirms the decision and dose. A role convention box was  
added.  
Part 8 and  
throughout  
3 No automatic rules. A five-kind number taxonomy (reference / abnormal / concern / critical / simulator trigger)  
and a single decision principle (verify → assess → investigate → review → decide) replace 'IF X → ALWAYS Y'  
wording.  
How to use; decision  
diagrams  
4 SpO2 logic rewritten as an 11-step assessment process with patient-specific targets (BTS 2017). Part 4.5; what-if; VR  
map  
5 Sepsis/NEWS2 rewritten to teach NEWS2 as risk stratification combined with infection, organ dysfunction,  
appearance and trajectory, using NICE NG253 (Nov 2025) risk tiers.  
Part 4.10  
6 Pregnancy testing now follows the NICE NG45 clinical question (is pregnancy possible? ask, consent, test if any  
doubt), with no automatic age rule; explains why pregnancy changes management.  
Part 5.21; Step 2;  
Step 6  
7 WHO checklist re-mapped to the exact 2009 wording and order; ProcSense additions labelled 'ProcSense  
educational extension'.  
Part 15; Steps 13–14  
8 Part B — Clinical Accuracy Refinements and a document-wide consistency audit. Part B  
9 Part C — Expanded VR clinical reasoning: 24 step cards (medical step → why → action → sees → info →  
interpretation → decision → correct → incorrect → missed → escalation → consequence → next).  
Part C  
10 Part D — Eight full stacked cases (4 S1, 4 S2), each a complete playable reasoning scenario with prioritisation,  
interaction analysis, investigations, synthetic results, surgery/anaesthesia decisions, escalation,  
correct/incorrect/missed/recovery paths and debrief; plus a master table.  
Part D  
11 Equipment inventory gains an 'information it provides' column; S1/S2 master maps rebuilt around the 20  
required stages incl. intra-operative training events, Sign Out, deterioration/emergency branches and debrief.  
Parts E–G  
12 Clinical Evidence Audit rebuilt as a source-to-claim table (claim, value, source, exact context, type A–D,  
ProcSense interpretation, validation).  
Part H  
Document map (v5)  
Part Contents Numbered sections  
A — Medical foundation Basics, operations, journey, vitals, labs, imaging, scores, medicines, fasting,  
consent, the 15 steps, S1/S2 stories, what-if + compact stacked cases, WHO  
checklist, player view, numbers, dictionary  
Parts 1–18  
B — Clinical accuracy  
refinements  
All v5 corrections and the consistency audit Part B  
C — Expanded VR clinical  
reasoning  
Medical step → VR cards Part C  
D — Expanded stacked cases 8 full scenarios + master table Part D  
E — Equipment and asset  
mapping  
Clinical equipment inventory Part E  
F — S1 master scenario 20-stage medical-to-VR map Part F  
G — S2 master scenario 20-stage medical-to-VR map Part G  
H — Clinical Evidence Audit Source-to-claim verification Part H  
I — Clinician validation Institutional adaptation, decisions, checklist, final status Part I  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 4*

Contents  
WHAT CHANGED IN v5.1 (final QA pass) 2  
WHAT CHANGED IN v5 3  
How to use this book 6  
PART A — Medical foundation (Parts 1–18) 9  
Part 1 — Medicine basics for the software engineering team 10  
Part 2 — The two operations, explained simply 14  
Part 3 — The patient's journey (the big picture) 17  
Part 4 — Vital signs, taught from zero 19  
Part 5 — Blood and urine tests, taught from zero 33  
5A — Full blood count (FBC): the blood cells 36  
5B — Kidneys and body salts (U&E) 39  
5C — Blood sugar and diabetes 43  
5D — Liver tests (LFTs): the heart of S1 45  
5E — Clotting tests and blood bank 48  
5F — Pregnancy test and urine tests 50  
5G — Emergency and special tests 52  
Part 6 — Heart tracing (ECG) and scans, taught from zero 54  
Part 7 — Scoring systems, taught from zero 58  
Part 8 — Medicines and anaesthesia, taught from zero 62  
Part 9 — Fasting, taught from zero 68  
Part 10 — Consent, taught from zero 70  
Part 11 — The steps, one by one 73  
STEP 1 — Patient arrival and identification 74  
STEP 2 — Asking questions (the history) 76  
STEP 3 — Checking vital signs 78  
STEP 4 — Examining the patient 80  
STEP 5 — Risk and anaesthetic assessment 82  
STEP 6 — Choosing the tests 84  
STEP 7 — Getting and understanding the results 86  
STEP 8 — Deciding, treating and optimising 88  
STEP 9 — Consent 91  
STEP 10 — Preparing the patient (ward/ED checklist) 93  
STEP 11 — Theatre preparation 95  
STEP 12 — Transfer and handover (SBAR) 97  
STEP 13 — WHO Sign In (before the patient goes to sleep) 99  
STEP 14 — WHO Time Out (before the first cut) — the finish line 101  
STEP 15 (optional) — WHO Sign Out and handover to recovery 102  
Part 12 — S1 as a story: a planned gallbladder operation 103  
Part 13 — S2 as a story: an emergency appendix 109  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 5*

Part 14 — WHAT IF something goes wrong? 114  
Part 14B — When several problems happen at once (stacked cases) 131  
Part 15 — The WHO Surgical Safety Checklist, explained 139  
Part 16 — What the player actually sees 141  
Part 17 — All the numbers in one place (with plain meanings) 145  
Part 18 — Dictionary of every medical word and abbreviation 148  
PART B — Clinical Accuracy Refinements (v5) 151  
PART C — Expanded VR Clinical Reasoning (Medical → Trainee → VR) 153  
PART C (continued) — Event-level Medical → VR table 160  
PART D — Expanded Stacked Cases (full playable scenarios) 163  
PART D — Stacked-case master table (overview) 198  
PART E — Equipment and Asset Mapping 200  
PART F — S1 Master Scenario (Elective Laparoscopic Cholecystectomy) 204  
PART G — S2 Master Scenario (Emergency Appendectomy) 210  
PART H — Clinical Evidence Audit (source-to-claim) 216  
PART I — Clinician Validation, Institutional Adaptation and Final Status 219  
FINAL STATUS 222  
Part 28 — Sources (references) 223  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 6*


## How to use this book

Teacher says:  
Hi Ruhan. Imagine you and I are sitting in front of the VR headset together. I'm going to explain what a hospital does  
with a patient before an operation, one small step at a time. Whenever I use a medical word, I'll stop and explain it.  
Whenever I give you a number, I'll tell you what it means, what's normal, what's worrying, and — most importantly —  
what the trainee should do about it.  
The order of this book  
Part What you learn Why it comes here  

# Part 1 — Medicine basics The 25 core ideas (patient, symptom, sign, diagnosis, surgeon,

anaesthetist…) and how the body works at a 'just enough' level.  
Everything else depends on these  
words.  

# Part 2 — The two

operations  
What the gallbladder and appendix are, what goes wrong, why we  
operate, and why S1 is planned but S2 is urgent.  
So the rest of the book makes  
sense.  

# Part 3 — The patient's

journey  
One big picture of every step from arrival to 'ready for the first cut'. Your mental map.  

# Part 4 — Vital signs from

zero  
Blood pressure, heart rate, temperature, breathing rate, oxygen level,  
alertness, pain, and the early-warning score.  
These are the first numbers the  
trainee sees.  

# Part 5 — Blood and urine

tests from zero  
Every test: what it is, why we order it, normal numbers, high, low, what to  
do, with an example patient.  
These numbers drive most  
decisions.  

# Part 6 — Heart tracing and

scans from zero  
ECG (heart tracing), ultrasound, CT, MRI/MRCP, ERCP. How we look inside the body.  

# Part 7 — Scoring systems

from zero  
How doctors turn many findings into one score (appendicitis scores,  
gallbladder scores, fitness for anaesthesia).  
Scores guide decisions.  

# Part 8 — Medicines and

anaesthesia from zero  
Painkillers, sickness medicines, drip fluids, antibiotics, blood thinners,  
diabetes medicines, and what 'being put to sleep' involves.  
The trainee recognises needs and  
initiates medicine decisions.  

# Part 9 — Fasting from zero Why patients must not eat before surgery, and what happens if they did. A very common simulator branch.


# Part 10 — Consent from

zero  
How we explain the operation and get permission. A required step.  

# Part 11 — The 14 steps,

one by one  
Every stage in the exact teaching structure you asked for. The core of the simulator.  

# Part 12 — S1 as a story A complete walk-through of a planned gallbladder patient, then abnormal

versions.  
Seeing it all joined up.  

# Part 13 — S2 as a story A complete walk-through of an emergency appendix patient, then a

patient who gets worse.  
Seeing the emergency version.  

# Part 14 — WHAT IF

something goes wrong?  
About 40 problems, each as: what happened → what it might mean →  
check → do → call → what next.  
The branches.  

# Part 15 — The WHO safety

checklist  
The three team checklists before and after surgery. The final gates.  

# Part 16 — What the player

actually sees  
Each stage described as the VR experience. For the engineers to build.  

# Part 17 — All the numbers

in one place  
Master tables with plain-English meanings. Quick reference.  

# Part 18 — Dictionary of

every medical word  
A–Z glossary. Look anything up.  
Parts B–I (v5) Clinical accuracy refinements; VR clinical reasoning cards; 8 full stacked  
cases; equipment; S1 and S2 master scenarios; evidence audit; clinician  
validation and final status  
See document map on page 2.  
The coloured labels and what they mean  
Medicine is full of rules that are firm, rules that are 'it depends', and rules that change from hospital to hospital.  
So every important statement carries a label:  
Label Meaning in plain English What the software team does  
CLINICAL REFERENCE — SOURCE  
BACKED  
Type A — directly supported: a value or rule taken directly  
from a recognised guideline or reference (WHO, NICE, RCP,  
ASA, WSES, Tokyo Guidelines, KDIGO, UKKA…). The source is  
named next to it.  
Build it as written. The clinician checks  
applicability, not the number itself.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 7*

Label Meaning in plain English What the software team does  
CLINICAL INTERPRETATION Type B — clinically derived interpretation: a clinically  
reasonable reading built from one or more sources, where no  
source publishes that exact gameplay band (e.g. 'postpone  
planned surgery if sodium is below 130').  
Build it as the default behaviour; expect  
possible adjustment.  
CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  
Type C — context-dependent: legitimately varies between  
institutions and countries (laboratory ranges, antibiotic  
formulary, consent law and forms, escalation structure,  
equipment). Final implementation is adapted to the target  
institution's approved clinical protocol.  
Leave a placeholder with the draft value;  
the hospital/clinician decides. (In dense  
tables at the end of the book this label is  
shortened to INSTITUTION / CLINICIAN.)  
SIMULATOR DESIGN VALUE  
(PROVISIONAL)  
Type D — ProcSense simulation design: a number or behaviour  
invented for the game because medicine does not define it  
(e.g. how many minutes a simulated patient takes to worsen).  
It is not a clinical rule.  
Keep it easy to change; it is a design  
choice, not medicine.  
EDUCATIONAL EXAMPLE DOSE  
(SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION  
A medicine dose that is widely standardised in official sources;  
shown for teaching only.  
Display only as an example; never as an  
instruction.  
PATIENT-SPECIFIC DOSE —  
CLINICIAN CONTROLLED  
There is no single safe number for everyone — the real doctor  
works it out for each patient.  
Show the decision, not a number.  
Reference range vs decision threshold — an important difference  
A reference range (laboratory 'normal range') describes what about 95% of healthy people show. A value just  
outside it is common and often harmless. A clinical concern threshold is where doctors start to worry; an  
emergency threshold is where immediate action is needed; a decision threshold is a published or agreed  
point at which a specific action (e.g. postponing planned surgery) is taken; a scoring threshold (like NEWS2  
bands) is designed to trigger a response, not to diagnose. This book labels which kind each number is. Being  
outside a reference range never automatically means surgery must be delayed.  
Five kinds of number (v5)  
Kind Meaning Example  
Reference value What is generally expected in healthy people Potassium 3.5–5.3 mmol/L  
Abnormal value Outside the usual range — often harmless, always interpreted Potassium 5.4  
Clinical concern threshold Should trigger additional assessment Potassium ≥5.5 (UKKA: ECG for all  
hyperkalaemia)  
Critical / emergency  
threshold  
Associated with potentially immediate danger Potassium ≥6.5 or any ECG change  
Simulator trigger A value ProcSense deliberately chooses to create a training  
scenario  
Variant S1-J is given K 6.4 'haemolysed' to  
teach repeat-testing  
The decision principle used everywhere (v5)  
Abnormal finding → verify (repeat, check technique/sample) → assess severity and context (the patient, trend,  
other findings, elective vs emergency) → investigate → senior / anaesthetic review where appropriate → the  
team decides whether to proceed, optimise, delay or escalate. Thresholds in this book tell the trainee when to  
think and who to involve; they are not automatic cancellation rules. A simulator trigger is never presented as a  
universal medical rule.  
How each number is explained  
Every time a number appears, I'll give you a 'zone' table like the one below. Read it like a traffic light.  
Example: how to read a zone table  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Inside the expected range Nothing worrying about this number Carry on (still look at the whole patient)  
Mildly abnormal A little outside Could be harmless or an early warning Look for a reason, repeat it, check related  
things  
Concerning Clearly outside Something is probably wrong Treat what you can and tell a senior doctor  
Severe /  
emergency  
Far outside The patient may be in danger now Call for help immediately; follow the emergency  
steps  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 8*

Teacher says:  
One golden rule before we start: a single number never tells the whole story. A doctor always asks "Does this  
number fit with how the patient looks and with the other numbers?" The simulator should reward the trainee for  
thinking like that.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 9*


# PART A — Medical foundation (Parts 1–18)

The original beginner foundation, preserved and corrected. v5 corrections are marked inline with 'v5  
correction' boxes where the teaching logic changed, and summarised in Part B.  
Role convention (applies to every part of this book)  
The trainee recognises problems, communicates (SBAR), and initiates the correct workflow. A qualified clinician  
confirms treatment decisions and doses; nurses/clinicians administer. The anaesthetist decides anaesthetic fitness;  
the senior surgeon decides whether and when to operate.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 10*


# Part 1 — Medicine basics for the software

engineering team  
Teacher says:  
Let's build your vocabulary first. Think of these as the 'classes' in the medical codebase — every later chapter uses  
them. I'll explain each as if you were 15.  
Word Explanation for a 15-year-old  
Patient A person receiving medical care. In ProcSense the patient is an animated character (an avatar) with a made-up  
name, age, history and body numbers that can change over time.  
Symptom Something the patient feels and tells you. You cannot see it; you must ask. Examples: "I have pain", "I feel sick",  
"I'm dizzy". In the simulator, symptoms come out of the patient's mouth — usually only if the trainee asks the right  
question.  
Sign Something the doctor finds or measures, whether or not the patient noticed it. Examples: a fast pulse, a high  
temperature, yellow eyes, the tummy being tender when pressed. In the simulator, signs appear when the trainee  
examines or measures.  
Diagnosis The doctor's conclusion about what illness the patient has. Example: 'acute appendicitis' (a suddenly inflamed  
appendix). A diagnosis is usually a best guess that becomes more certain as more evidence comes in.  
Differential diagnosis The list of possible illnesses that could explain the symptoms, before you know which one it is. Example:  
lower-right tummy pain in a young woman could be appendicitis, a pregnancy in the wrong place, a twisted ovary, a  
urine infection or a kidney stone. Good doctors keep this list in mind instead of jumping to the first idea.  
History (clerking) The structured conversation in which the doctor asks the patient questions — what's wrong, when it started, other  
illnesses, medicines, allergies, when they last ate, and so on. 'Clerking' is hospital slang for taking this history and  
writing it down.  
Examination The doctor looking at, listening to and gently feeling the body (for example pressing on the tummy) to find signs.  
Vital signs  
(observations / 'obs')  
The basic body measurements that tell you whether someone is stable: blood pressure, heart rate, breathing rate,  
oxygen level, temperature and alertness. 'Vital' means 'needed for life'. Nurses measure them repeatedly; a change  
is often the first clue that something is going wrong.  
Blood test A small amount of blood is taken from a vein (usually in the arm, with a needle) into a coloured tube and sent to the  
laboratory, which measures substances in it. Results come back after minutes to hours.  
Urine test Testing the patient's urine (wee), either with a paper strip dipped in it (results in about 2 minutes) or in the lab.  
Imaging (scans) Taking pictures of the inside of the body: ultrasound (sound waves), X-ray, CT (a detailed X-ray scanner), MRI  
(magnets). A specialist doctor called a radiologist writes a report explaining the pictures.  
Investigation Any test used to find out more: blood tests, urine tests, scans, heart tracing. 'Ordering investigations' means  
requesting them.  
Treatment Anything done to make the patient better: medicines, fluids through a drip, oxygen, an operation.  
Optimisation Getting a patient into the best possible condition before an operation. Example: a patient with badly  
controlled diabetes may have surgery postponed for a few weeks so their sugar can be brought under control,  
because that makes the operation safer.  
Emergency A situation where waiting could cause serious harm or death. Emergency patients are treated quickly, often while  
tests are still being done.  
Urgent Needs doing soon (hours), but not necessarily this minute.  
Elective surgery A planned operation booked in advance (weeks or months). Because it is planned, it can be postponed if the  
patient isn't safe that day. S1 is elective.  
Emergency surgery An operation that cannot wait for a booking because the illness will get worse. S2 is emergency (urgent) surgery.  
Anaesthesia Medicines that stop the patient feeling pain during an operation. General anaesthesia means the patient is put  
into a controlled, medicine-induced deep sleep and feels nothing. Both ProcSense operations use general  
anaesthesia.  
Surgeon The doctor who performs the operation. Junior surgeons (trainees) do much of the preparation; a senior surgeon  
(registrar or consultant) makes the big decisions.  
Anaesthetist  
(anesthesiologist)  
The doctor who puts the patient to sleep, keeps them safe and breathing during the operation, and decides  
whether they are fit for anaesthesia. They are very concerned about the airway (the breathing tube from mouth to  
lungs), the heart, the lungs and whether the stomach is empty.  
Nurse A trained professional who measures vital signs, gives medicines, prepares the patient, completes checklists and  
raises the alarm when something changes. Nurses often notice problems first.  
Pre-operative  
assessment  
'Pre-operative' means 'before the operation'. This is the whole process of checking the patient is ready and safe:  
questions, examination, tests, medicine plans, consent. ProcSense is essentially a game about this.  
Operating theatre (OR) The special sterile room where operations happen. 'Theatre' is the British word; Americans say 'operating room'.  
Sterile Free of germs. Surgical instruments and the surgical area must be sterile to prevent infection.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 11*

Word Explanation for a 15-year-old  
Complication Something that goes wrong as a result of an illness, test or treatment. Example: an inflamed appendix bursting is a  
complication of appendicitis; bleeding is a possible complication of surgery.  
Escalation Telling a more senior or specialist person about a problem and asking for help. In medicine this is considered a skill  
and a responsibility, not a weakness. Calling for help early is a correct action in the simulator.  
Referral Formally passing a patient (or a question) to another team. Example: referring a patient with a heart rhythm  
problem to the heart (cardiology) team.  
Senior In this book, 'senior' means the more experienced surgeon supervising the trainee.  
Contraindication A reason not to give a treatment to a particular patient. Example: an allergy to penicillin is a contraindication to  
giving penicillin.  
Allergy When the body's defence system over-reacts to a substance (a medicine, latex, food). Mild: rash. Severe: swelling  
of the throat, breathing difficulty, collapse — called anaphylaxis, which is an emergency.  
Comorbidity Another long-term illness the patient has besides the one being operated on (for example diabetes or high blood  
pressure). Comorbidities make surgery riskier and often need managing first.  
Acute / chronic Acute = started suddenly or recently (hours to days). Chronic = long-lasting (months to years).  
Inflammation The body's response to injury or infection: the area becomes red, swollen, hot and painful because extra blood and  
defence cells rush there. Words ending in -itis mean inflammation: appendic-itis = inflamed appendix;  
cholecyst-itis = inflamed gallbladder.  
Infection Germs (usually bacteria) growing where they shouldn't be. Infection causes inflammation, fever, and sometimes  
spreads to the whole body.  
Sepsis A life-threatening emergency where the body's reaction to an infection starts damaging its own organs (the  
kidneys, brain, lungs, circulation). Signs: fast heart, fast breathing, low blood pressure, confusion, high or low  
temperature. It needs antibiotics and fluids fast. More in Part 4.  
Shock Not 'surprise'. In medicine, shock means the blood is not delivering enough oxygen to the organs, usually because  
blood pressure is too low. Causes include bleeding, severe infection (septic shock), severe allergy and heart failure.  
It is an emergency.  
Deterioration The patient getting worse.  
Monitoring Repeatedly or continuously measuring the patient (for example a monitor screen showing the heart and oxygen  
level all the time).  
Cannula / IV / drip A thin plastic tube put into a vein (usually on the hand or arm) so fluids and medicines can go straight into the  
blood. IV = intravenous = 'into the vein'. A drip is a bag of fluid running into the cannula.  
Consent The patient's informed permission for the operation, after it has been properly explained.  
Handover Passing information and responsibility for a patient from one person/team to another, for example from the ward to  
the theatre team.  
NBM / NPO Nil by mouth (British) / nil per os (Latin; American). Nothing to eat or drink.  

## 1.2 Just-enough body knowledge

Teacher says:  
You don't need anatomy lectures, but a few facts make every number in this book understandable. Here's the body as  
a system — think of it like a city's infrastructure.  
Body part Simple explanation Why it matters in ProcSense  
Blood The delivery service. It carries oxygen, sugar and salts to every  
part of the body and carries waste away. An adult has about 5  
litres.  
Blood tests measure what's in it. Losing blood  
(bleeding) lowers blood pressure.  
Heart A pump that pushes blood around the body. Each squeeze is a  
heartbeat.  
Heart rate and blood pressure tell us how hard the  
pump is working and whether it's coping.  
Blood vessels Pipes. Arteries carry blood away from the heart under pressure;  
veins carry it back.  
Blood pressure is the pressure inside the arteries.  
Cannulas and blood samples go into veins.  
Lungs Where blood picks up oxygen from the air and gets rid of carbon  
dioxide (the waste gas).  
Breathing rate and oxygen level tell us if the lungs  
are working well.  
Oxygen The 'fuel' every cell needs. Without it, organs (especially the  
brain) are damaged within minutes.  
The oxygen-level number (SpO2) is one of the most  
important on the monitor.  
Kidneys Two filters in the back that clean the blood, control salt and  
water, and make urine.  
If they're struggling, waste builds up in the blood  
(raised creatinine) and salts like potassium go wrong.  
Some medicines become dangerous.  
Liver A big chemical factory under the right ribs. It makes bile (a  
digestive juice), makes the proteins that help blood clot, and  
processes medicines.  
Liver blood tests show if bile is blocked or liver cells  
are damaged — key for the gallbladder scenario.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 12*

Body part Simple explanation Why it matters in ProcSense  
Gallbladder and  
bile ducts  
A small storage bag for bile under the liver, and the tubes that  
carry bile to the gut.  
The S1 organ (Part 2).  
Stomach and  
intestines (gut)  
The food-processing tube. Food sits in the stomach for hours  
before moving on.  
A full stomach is dangerous during anaesthesia  
(stomach contents can go into the lungs) — that's  
why patients fast.  
Appendix A small finger-shaped dead-end tube attached to the large  
intestine in the lower right tummy.  
The S2 organ (Part 2).  
Immune system The body's army: white blood cells that attack germs, causing  
inflammation.  
White cell count and CRP blood tests measure how  
active the army is.  
Brain The control centre. Very sensitive to low oxygen, low sugar, low  
blood pressure and infection.  
New confusion is a serious warning sign.  
Uterus, ovaries  
and tubes (female)  
The womb and the organs that release eggs, right next to the  
appendix.  
Problems here — especially a pregnancy growing in  
the wrong place — can look exactly like appendicitis.  

## 1.3 How the body responds when something is wrong (the most useful

idea in this book)  
When the body is under stress — pain, fear, infection, blood loss, dehydration — it reacts in predictable ways.  
Understanding these reactions explains almost every vital-sign change you'll meet:  
- The heart speeds up to push blood around faster (to deliver more oxygen, or to make up for less blood).  
So a fast heart rate is a general 'alarm bell', not a diagnosis.  
- Breathing speeds up to get more oxygen in and more acid/carbon dioxide out. Fast breathing is one of the  
earliest signs of serious illness.  
- Blood vessels tighten (in bleeding/dehydration) to keep blood pressure up — so blood pressure often  
stays normal for a while even when the patient is quite unwell. When blood pressure finally falls, the body's  
back-up systems are failing. That's why low blood pressure is a late and serious sign.  
- Temperature rises (fever) when the immune system fights infection.  
- The kidneys save water, so the patient makes less urine.  
- The brain struggles if oxygen, sugar or blood pressure get too low — the patient becomes confused or  
drowsy.  
Teacher says:  
So when you see a fast heart rate, don't think 'heart problem'. Think: "The body is stressed. Why? Pain? Fear? Fever?  
Not enough fluid? Bleeding? Infection? An irregular heart rhythm?" Then look at the other clues to work out which.  
This kind of reasoning is exactly what ProcSense must teach.  

## 1.4 The people in the simulator (NPCs)

Person What they do What they say/do in the game  
Trainee (the player) A junior doctor learning to prepare patients for  
surgery.  
Talks to the patient, examines, orders tests, gives first  
treatments, calls for help.  
Ward nurse / Emergency  
Department (ED) nurse  
Measures vital signs, gives medicines, does  
checklists.  
Reports numbers, asks the trainee for decisions,  
challenges unsafe orders (e.g. 'She's allergic to that').  
Triage nurse (S2) Triage = deciding how urgently each arriving patient  
must be seen.  
Gives the first summary: 'Severe pain, fast pulse, needs  
seeing within 10 minutes.'  
Senior surgeon Experienced surgeon supervising the trainee. Answers the phone; confirms diagnosis and decision to  
operate; comes in emergencies.  
Anaesthetist Decides if the patient is fit for anaesthesia and plans  
it.  
Reviews the patient; asks about food, airway, medicines;  
may delay surgery.  
Gynaecologist Doctor for female reproductive organs and  
pregnancy.  
Called if a pregnancy test is positive with tummy pain.  
Laboratory staff Run blood tests. Phone the trainee with dangerous ('critical') results.  
Radiologist / sonographer Do scans and write reports. Provide the scan report.  
Porter Moves patients between departments. Takes the patient to scans or theatre.  
Theatre team (theatre  
nurse, scrub nurse,  
operating department  
practitioner)  
Prepare the operating theatre and instruments; check  
the patient in.  
Check identity, consent, allergies, fasting; run the WHO  
checklist.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 13*


---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 14*


# Part 2 — The two operations, explained simply


## 2.1 S1 — Laparoscopic cholecystectomy (planned keyhole gallbladder

removal)  
Medical word: Laparoscopic cholecystectomy  
Laparo- = abdomen (tummy), -scopic = looking with a camera. Chole- = bile, cyst = bag, -ectomy = cutting out.  
So: removing the bile bag (the gallbladder) using a camera through small cuts. Often shortened to 'lap chole'.  
What is the gallbladder and where is it?  
A small, pear-shaped bag (about 7–10 cm long) tucked under the liver, on the right side of the upper tummy,  
just under the bottom of the right ribs. Doctors call this area the right upper quadrant (RUQ) — imagine  
dividing the tummy into four squares with the belly button in the middle.  
What does it do?  
The liver constantly makes bile, a greenish-yellow liquid that helps digest fat. The gallbladder stores and  
concentrates bile. When you eat a fatty meal, the gallbladder squeezes and pushes bile down a tube called the  
common bile duct into the intestine. People can live normally without a gallbladder — bile simply trickles  
straight from the liver instead of being stored.  
What are gallstones?  
Hard lumps (like small pebbles, from grains of sand to golf-ball size) that form inside the gallbladder when  
substances in bile (mostly cholesterol) crystallise. They are very common; many people have them without  
knowing.  
Why can gallstones cause pain?  
When the gallbladder squeezes after a meal, a stone can block its exit. The gallbladder squeezes against the  
blockage and that hurts — usually in the upper right or middle of the tummy, often after fatty food, lasting from  
30 minutes to a few hours, then settling. This is called biliary colic (biliary = to do with bile; colic = cramping  
pain that comes and goes).  
What can go wrong if stones stay?  
- Acute cholecystitis — the gallbladder becomes inflamed and often infected (cholecyst-itis = inflamed  
gallbladder). Constant pain, fever, tenderness. Needs hospital treatment.  
- Stone in the common bile duct (doctors say choledocholithiasis — 'bile duct stone condition'). The  
stone blocks bile flow, bile backs up into the blood and the skin and eyes turn yellow (jaundice). Urine  
becomes dark and stools pale.  
- Cholangitis — infection of a blocked bile duct. Fever, shaking, jaundice and pain. This is an emergency  
because infection can spread into the blood.  
- Gallstone pancreatitis — a stone blocks the shared exit of the bile duct and the pancreas (a gland behind  
the stomach), inflaming the pancreas. Severe pain going through to the back, vomiting. Can be serious.  
Why remove the gallbladder?  
Once gallstones cause symptoms, attacks tend to come back and complications can occur. Removing the  
gallbladder removes the place where stones form. UK national guidance recommends offering this operation to  
adults with symptomatic gallstones CLINICAL REFERENCE — SOURCE BACKED (NICE CG188).  
What does 'laparoscopic' mean in practice?  
Instead of one big cut, the surgeon makes 3–4 small cuts (5–12 mm), fills the tummy with carbon dioxide gas to  
create space, and works with a camera and long thin instruments while watching a screen. Smaller cuts mean  
less pain and quicker recovery — many patients go home the same day ('day-case surgery'). Sometimes the  
surgeon has to switch to a normal larger cut (called conversion to open surgery) if it is unsafe to continue  
by keyhole.  
Why is S1 'elective' and why does that matter?  
The operation is booked weeks ahead. Nothing terrible happens if it is postponed for a few weeks. Therefore, if  
anything makes the patient unsafe today, the correct and responsible answer is usually to  
postpone and fix it. That is a success in the simulator, not a failure.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 15*

The S1 mindset  
"This patient is not in danger right now. My job is to make sure they are safe and in the best possible condition before  
a planned operation. If something isn't right, I fix it or postpone."  

## 2.2 S2 — Emergency appendectomy (urgent removal of an inflamed

appendix)  
Medical word: Appendectomy (also 'appendicectomy')  
Append- = the appendix, -ectomy = cutting out. Removing the appendix.  
What is the appendix and where is it?  
A small, finger-shaped, dead-end tube (about 5–10 cm long) attached to the start of the large intestine, in the  
right lower quadrant (RLQ) of the tummy — the lower right square. It has no essential job; people live  
normally without it.  
What is appendicitis?  
Inflammation of the appendix (append-itis). Usually its opening gets blocked (for example by a small hard lump  
of stool), germs multiply inside, it swells and becomes infected. Typical story: pain starts vaguely around the  
belly button, then over 12–24 hours moves to the lower right side and becomes sharp; the patient loses their  
appetite, feels sick or vomits, and may have a mild fever.  
Why can it become dangerous?  
If untreated, the swollen appendix can die (gangrene) and burst (perforation). Germs and pus then spill into  
the tummy, causing peritonitis (inflammation of the tummy lining — very painful and rigid tummy), an  
abscess (a pocket of pus), and sepsis (body-wide reaction to infection, which can kill). Risk of bursting  
increases as hours pass, particularly after about 48 hours of symptoms.  
Why remove it?  
Removing the inflamed appendix removes the source of infection. In some carefully chosen mild cases,  
antibiotics alone can be used instead, but the condition can come back — the patient must be told about this  
choice CLINICAL REFERENCE — SOURCE BACKED .  
Why is this an emergency/urgent pathway — and how is it different from S1?  
In S2, the illness is actively getting worse. The team cannot say 'let's wait three weeks and optimise'.  
Instead they must investigate and treat at the same time: give pain relief, fluids and antibiotics while tests  
are running, then operate promptly once the patient is safe to do so. Timing depends on the patient and the  
disease: the 2025 WSES guideline recommends laparoscopic appendectomy within 24 hours of admission when  
surgery is indicated, and a burst appendix or sepsis makes it more urgent CLINICAL REFERENCE — SOURCE BACKED  
(the surgeon and anaesthetist decide the actual time).  
Also, in S2 we don't yet know it is appendicitis. Several other conditions look the same — some of them (like a  
pregnancy growing outside the womb) need a completely different team and are even more dangerous. So the  
trainee must keep an open mind.  
S1 — planned gallbladder S2 — emergency appendix  
Is the patient in danger  
now?  
Usually no Possibly yes, and getting worse with time  
Is the diagnosis known? Yes (already confirmed by a scan) No — it must be worked out  
Time pressure Low High (a 'deterioration clock' runs)  
If a problem is found Fix it first or postpone Fix it in parallel; operate when safe; don't wait for  
perfection  
Has the patient eaten? Usually fasted as instructed Often unknown or recently ate — treated as a 'full  
stomach'  
Mood Calm, prepared In pain, frightened, maybe feverish  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 16*


## 2.3 What ProcSense does NOT simulate

The operation itself, the anaesthetic drugs and their doses, and hand skills are not simulated. The simulation  
ends at the moment the whole team says 'ready for the first cut' (the WHO Time Out). After that there's an  
optional short ending (Sign Out and handover to the recovery room).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 17*


# Part 3 — The patient's journey (the big picture)

Teacher says:  
Here is the whole journey in one picture. Every later part of the book zooms into one of these boxes. Read it top to  
bottom.  
THE PATIENT'S JOURNEY  
1\. PATIENT ARRIVES  
2\. IDENTIFY THE PATIENT (make sure it's the right  
person)  
3\. ASK QUESTIONS (the history)  
4\. CHECK VITAL SIGNS (blood pressure, heart rate,  
breathing, oxygen, temperature, alertness)  
5\. EXAMINE THE PATIENT (look, listen, feel)  
6\. DECIDE WHICH TESTS ARE NEEDED (only the useful  
ones)  
7\. GET THE TEST RESULTS  
8\. UNDERSTAND THE RESULTS (in context)  
9\. DECIDE: is it safe to continue?  
More investigation? Treatment / optimisation?  
Senior or anaesthetist review? EMERGENCY?  
(each explained below)  
NO  
YES  
10\. CONSENT (explain and get permission)  
11\. PREPARE THE PATIENT (checklist)  
12\. THEATRE (handover to theatre team)  
13\. WHO SIGN IN (safety check before going to sleep)  
14\. WHO TIME OUT (safety check before the first cut)  
→ READY  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 18*

Every box explained  
Box What happens Why it matters  
1\. Patient  
arrives  
S1: walks into the day-surgery unit at a booked time. S2: comes into  
the Emergency Department (ED), often in pain.  
Sets the pace: calm (S1) vs urgent (S2).  
2\. Identify Ask the patient to say their full name and date of birth; compare with  
their wristband and notes; check allergies.  
Treating the wrong patient, or giving a medicine  
they're allergic to, are among the worst hospital  
errors.  
3\. Ask questions What's wrong? Other illnesses? Medicines? Allergies? When did you last  
eat or drink? Could you be pregnant?  
Most important facts are only found by asking.  
4\. Vital signs Machine and nurse measure the basic body numbers. Tells us if the patient is stable right now.  
5\. Examine Look at the patient, feel the tummy, listen to heart and lungs, look in  
the mouth (for the anaesthetist).  
Finds signs that confirm or question the diagnosis.  
6\. Choose tests Pick the tests that will change decisions — and avoid ones that won't. Unnecessary tests waste time, money and can  
cause confusion.  
7\. Get results Wait; track pending results; answer phone calls about dangerous  
results.  
Some results take time — the trainee must keep  
working meanwhile.  
8\. Understand  
results  
Compare with normal ranges; see if they fit the story; repeat strange  
ones.  
Numbers mean nothing without context.  
9\. Decide SAFE → continue. MORE INVESTIGATION → another test/scan.  
TREATMENT/OPTIMISATION → fix the problem (S1 may postpone).  
SENIOR/ANAESTHETIC REVIEW → call them. EMERGENCY → call for help  
immediately and treat.  
This is the central skill of the whole simulator.  
10\. Consent Explain diagnosis, operation, benefits, risks, alternatives; check  
understanding; patient signs.  
Legally and ethically required; patient's right.  
11\. Prepare Gown, remove jewellery, confirm fasting, documents together,  
checklist complete.  
Small omissions cause cancellations or harm in  
theatre.  
12\. Theatre /  
handover  
Patient transferred; trainee tells the theatre team the important facts in  
a structured way.  
Information lost at handover is a classic cause of  
errors.  
13\. WHO Sign In Before the anaesthetic: team checks identity, consent, allergies,  
airway, blood-loss risk, equipment.  
Last chance before the patient is asleep and  
cannot answer.  
14\. WHO Time  
Out  
Before the first cut: whole team introduces themselves and confirms  
everything.  
Final safety gate. In ProcSense this is the 'win'  
moment.  
Teacher says:  
Notice box 9. It's not a yes/no gate only once — the decision is made again and again. Any time something new  
appears (a new number, a new answer), the trainee should ask "Is it still safe to continue?"  
The five possible outcomes of box 9 (used everywhere in this book)  
Outcome Plain meaning Typical example  
Safe to continue Everything fits and is within acceptable limits. Normal numbers, fasted, consent ready.  
Needs more  
investigation  
We don't yet know enough to decide. Liver tests abnormal → need a special scan of the bile  
ducts.  
Needs treatment /  
optimisation  
A problem can be fixed (now in S2, or before a re-booked  
date in S1).  
Dehydrated → give fluids. Diabetes badly controlled →  
postpone planned surgery to improve it.  
Needs senior or  
anaesthetic review  
Decision is beyond the trainee or affects anaesthesia. Patient ate 3 hours ago; heart rhythm irregular.  
Emergency The patient may be in danger now. Blood pressure very low, oxygen falling, severe allergic  
reaction.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 19*


# Part 4 — Vital signs, taught from zero

Teacher says:  
Vital signs are the first numbers the trainee will see, and they come back again and again. Most of the  
danger-detection in ProcSense is built on them. All numbers here are for adults aged 16 and over who are not  
pregnant — children and pregnant women have different normal values, which I'll flag where it matters.  

## 4.0 Evidence cards for the six core vital signs

Each card separates the reference range from concern, emergency and decision thresholds and says  
where every number comes from. The detailed teaching sections (4.1 onwards) then explain them step by  
step.  
Blood pressure (BP)  
What it measures Pressure of blood in the arteries: systolic (heart squeezing) / diastolic (heart relaxing), mmHg.  
How measured Automated upper-arm cuff (oscillometric). Patient seated, arm supported at heart level, correct cuff size, ≥5 min  
rest, no talking; repeat if abnormal CLINICAL REFERENCE — SOURCE BACKED (ESH 2023).  
Reference range Below 140/90 is not hypertensive; below 120/80 'optimal' (ESH 2023) / below 120/70 'non-elevated' (ESC 2024)  
CLINICAL REFERENCE — SOURCE BACKED . Sources differ in naming, not in surgical decisions.  
Clinically abnormal High: grade 1 140–159/90–99; grade 2 160–179/100–109; grade 3 ≥180/≥110 (ESH 2023) CLINICAL REFERENCE —  
SOURCE BACKED . Low side: NEWS2 scores systolic 101–110 = 1, 91–100 = 2 CLINICAL REFERENCE — SOURCE  
BACKED .  
Clinically concerning Systolic ≤100 (qSOFA criterion, Sepsis-3); fall of more than 40 mmHg below usual (NICE NG253 high-risk criterion)  
CLINICAL REFERENCE — SOURCE BACKED .  
Emergency Systolic ≤90 (NEWS2 = 3; NICE sepsis high risk) or MAP below 65 needing support (septic shock, Sepsis-3)  
CLINICAL REFERENCE — SOURCE BACKED . Severe hypertension with acute organ damage = hypertensive  
emergency (ESH 2023).  
Decision threshold  
(surgery)  
Planned surgery: proceed if GP readings below 160/100, or clinic readings below 180/110 when no GP record;  
postpone if ≥180/110 on repeat (AAGBI/BHS 2016) CLINICAL REFERENCE — SOURCE BACKED . Emergency surgery  
is not postponed for BP alone CLINICAL INTERPRETATION .  
Context Pain, anxiety, full bladder and wrong cuff raise BP; dehydration, sepsis, bleeding, anaphylaxis, opioids and BP  
tablets lower it. Compare with the patient's usual value.  
Trainee checks Repeat; technique; heart rate; temperature; capillary refill; urine; symptoms (chest pain, headache, confusion);  
medicines; pregnancy test if relevant.  
Repeat / escalate Repeat any abnormal value. Escalate urgently at NEWS2 single-parameter 3 (≤90) or NEWS2 total ≥5; emergency  
if ≥7 (RCP NEWS2) CLINICAL REFERENCE — SOURCE BACKED .  
ProcSense use Monitor value + NEWS2 scoring + S1 elective-decision rule + sepsis branch.  
Validation Anaesthetist confirms monitor alarm levels CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION .  
Heart rate (HR)  
What it measures Heartbeats per minute; rhythm regular or irregular.  
How measured Pulse oximeter/ECG monitor or palpating the radial pulse for 30–60 s (irregularity needs palpation or ECG).  
Reference range 60–100 beats/min at rest in adults aged 18 and over (American Heart Association, 'All About Heart Rate (Pulse)';  
well-trained athletes and some medicines can give lower resting rates) CLINICAL REFERENCE — SOURCE BACKED .  
It is a reference range, not a decision rule: what matters is the trend, symptoms and the other findings.  
Clinically abnormal Tachycardia above 100; bradycardia below 60 (often normal in athletes/beta-blockers) CLINICAL REFERENCE —  
SOURCE BACKED . NEWS2 scores 91–110 = 1; 41–50 = 1.  
Clinically concerning 111–130 (NEWS2 2) CLINICAL REFERENCE — SOURCE BACKED ; shock index (HR ÷ SBP) approaching or above 1.0  
as a supporting marker, never alone CLINICAL INTERPRETATION (Koch 2019).  
Emergency ≥131 or ≤40 (NEWS2 3; ≥131 is also a NICE sepsis high-risk criterion) CLINICAL REFERENCE — SOURCE BACKED .  
Decision threshold  
(surgery)  
No guideline sets an HR cancellation value. New/unexplained tachycardia or new AF in a planned patient →  
postpone and assess (ESC 2022 non-cardiac surgery principles) CLINICAL INTERPRETATION .  
Context Pain, anxiety, fever (roughly +10/min per °C CLINICAL INTERPRETATION ), dehydration, bleeding, sepsis,  
arrhythmia, PE, thyroid, drugs; beta-blockers mask tachycardia.  
Trainee checks Rhythm (ECG if irregular), BP, temperature, pain, fluid status, Hb, lactate, pregnancy test.  
Repeat / escalate Recheck after treating pain/fluids (30–60 min). Escalate per NEWS2 response.  
ProcSense use Display reference 60–100; score with NEWS2 bands; drives deterioration branches.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 20*

Validation Alarm thresholds CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Respiratory rate (RR)  
What it measures Breaths per minute — sensitive early marker of physiological stress.  
How measured Count chest movements for a full 30–60 s (no machine in most wards).  
Reference range 12–20 breaths/min (NEWS2 0 points) CLINICAL REFERENCE — SOURCE BACKED .  
Clinically abnormal 21–24 (NEWS2 2); 9–11 (NEWS2 1) CLINICAL REFERENCE — SOURCE BACKED .  
Clinically concerning ≥22 with infection (qSOFA criterion) CLINICAL REFERENCE — SOURCE BACKED .  
Emergency ≥25 (NEWS2 3; NICE sepsis high-risk criterion) or ≤8 (NEWS2 3; opioid toxicity concern) CLINICAL REFERENCE —  
SOURCE BACKED .  
Decision threshold No surgical cancellation value; abnormal RR triggers assessment.  
Context Pain, anxiety, fever, sepsis, metabolic acidosis (DKA, poor perfusion), aspiration, PE, opioids (slow).  
Trainee checks SpO2, temperature, BP, blood gas/lactate, glucose/ketones, chest exam, opioid chart.  
Repeat / escalate As NEWS2 response.  
ProcSense use Avatar breathes at the simulated rate; trainee must count.  
Validation None beyond NEWS2 adoption CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Temperature  
What it measures Core body temperature (°C), usually tympanic.  
How measured Tympanic or oral thermometer.  
Reference range 36.1–38.0 °C scores 0 in NEWS2; typical healthy 36.5–37.5 °C CLINICAL REFERENCE — SOURCE BACKED .  
Clinically abnormal Fever: commonly ≥38.0 °C (used in SIRS, TG18 cholangitis uses above 38) CLINICAL REFERENCE — SOURCE  
BACKED ; NEWS2 scores 38.1–39.0 = 1. Hypothermia (peri-operative): below 36.0 °C (NICE CG65) CLINICAL  
REFERENCE — SOURCE BACKED .  
Clinically concerning ≥39.1 (NEWS2 2); rigors at any temperature CLINICAL REFERENCE — SOURCE BACKED .  
Emergency ≤35.0 (NEWS2 3) — ominous in sepsis CLINICAL REFERENCE — SOURCE BACKED .  
Decision threshold  
(surgery)  
Do not transfer for planned surgery if below 36.0 °C unless urgent; warm first (NICE CG65) CLINICAL REFERENCE —  
SOURCE BACKED . Fever ≥38 °C in a planned gallbladder patient → reassess for acute cholecystitis/cholangitis  
CLINICAL INTERPRETATION .  
Context Fever + tachycardia + raised WBC/CRP suggests infection; elderly/immunosuppressed may not mount a fever.  
Trainee checks Source (abdomen, urine, chest), sepsis screen, cultures if ≥38.5/rigors CLINICAL INTERPRETATION .  
Repeat / escalate With each observation set; escalate per NEWS2 / sepsis tool.  
ProcSense use Thermometer value; affects scores (Alvarado ≥37.3, AIR ≥38.5).  
Validation —  
Oxygen saturation (SpO2)  
What it measures % of haemoglobin carrying oxygen, estimated by pulse oximetry.  
How measured Finger probe with a good waveform; record whether on air or oxygen.  
Reference range / target ≥96% scores 0 on air (NEWS2). Prescribing target 94–98% for most adults; 88–92% for those at risk of hypercapnic  
respiratory failure (BTS 2017) CLINICAL REFERENCE — SOURCE BACKED .  
Clinically abnormal 94–95% (NEWS2 1); 92–93% (NEWS2 2) CLINICAL REFERENCE — SOURCE BACKED .  
Clinically concerning Needing oxygen at all (NEWS2 +2) CLINICAL REFERENCE — SOURCE BACKED .  
Emergency ≤91% on Scale 1 (NEWS2 3) CLINICAL REFERENCE — SOURCE BACKED ; acute fall needing high-flow oxygen.  
Decision threshold Not a cancellation value; new hypoxia before planned surgery → postpone and investigate CLINICAL  
INTERPRETATION .  
Context Oximeters can overestimate SpO2 in darker skin (FDA 2021); poor perfusion, cold, nail polish and movement cause  
errors CLINICAL REFERENCE — SOURCE BACKED .  
Trainee checks Probe/waveform, RR, chest, fluid balance, opioids, aspiration, blood gas.  
Repeat / escalate Immediately recheck; escalate per NEWS2.  
ProcSense use Monitor value + pleth wave quality; Scale 2 for COPD variant.  
Validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 21*

Mean arterial pressure (MAP)  
What it measures Average arterial pressure across one heartbeat ≈ diastolic + ⅓ × (systolic − diastolic).  
How measured Calculated automatically by most monitors.  
Reference range Typically about 70–100 mmHg in healthy adults CLINICAL INTERPRETATION .  
Concern / emergency Target at least 65 mmHg in septic shock; vasopressors needed to maintain ≥65 define septic shock (Surviving  
Sepsis 2021; Sepsis-3) CLINICAL REFERENCE — SOURCE BACKED .  
Decision threshold MAP below 65 despite fluids → senior + anaesthetist/critical care immediately CLINICAL REFERENCE — SOURCE  
BACKED .  
ProcSense use Shown in brackets on the monitor; drives shock branch.  
Validation —  

## 4.1 Blood pressure (BP)

What is blood pressure, physically?  
Your heart is a pump; your arteries are pipes. Blood pressure is how hard the blood pushes against the walls  
of the arteries. Think of a garden hose: when you squeeze the tap the pressure goes up; if the hose leaks, the  
pressure drops.  
Organs (brain, kidneys, heart) need enough pressure to push blood through them. Too low → they don't get  
enough oxygen. Too high for a long time → pipes and organs get damaged (strokes, heart attacks, kidney  
damage).  
Why are there two numbers?  
BP is written like 120/80 (said 'one-twenty over eighty').  
- The top number = systolic pressure: the highest pressure, at the moment the heart squeezes (systole =  
squeeze).  
- The bottom number = diastolic pressure: the lowest pressure, while the heart relaxes and refills between  
beats (diastole = relaxation).  
What are the units?  
mmHg = millimetres of mercury. It's an old unit from the days when pressure was measured by how high it  
could push a column of mercury. You just need to know the numbers.  
How is it measured in the game?  
An inflatable cuff is wrapped around the upper arm. An automatic machine inflates it, slowly lets it down, and  
after about 30–40 seconds shows the numbers on the monitor. The wrong cuff size, talking, an arm hanging  
down, a full bladder, pain or anxiety can all push the reading up.  
Medical word: MAP (mean arterial pressure)  
The average pressure over one heartbeat. Rough formula: MAP ≈ diastolic + one-third × (systolic − diastolic).  
Example: BP 120/60 → 60 + (60 ÷ 3) = 80 mmHg. Organs need a MAP of at least about 65 mmHg CLINICAL REFERENCE  
— SOURCE BACKED (Surviving Sepsis Campaign 2021). Many monitors show MAP in brackets, e.g. '120/60 (80)'.  
Blood pressure — the LOW side (systolic, top number)  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal 111–219 mmHg Enough pressure for most adults. (Some fit  
young women normally sit around 100–110.)  
Nothing for BP; carry on  
Borderline low 101–110 May be normal for this person, OR an early  
sign of dehydration, bleeding, infection, or  
blood-pressure tablets  
Compare with their usual BP; check heart rate,  
hands (cold?), urine; repeat in 15–30 min  
Concerning low 91–100 Probably not enough fluid in the circulation, or  
blood vessels relaxing due to infection; one of  
the sepsis warning signs  
Lie patient flat, drip in, consider a fluid 'bolus' (a  
quick bag of fluid), check lactate blood test, tell  
senior urgently  
Severely low  
(shock)  
90 or below, OR MAP  
below 65  
Organs may not be getting enough blood.  
Causes: severe infection (septic shock),  
bleeding, severe allergy, heart failure  
EMERGENCY: call for help now, oxygen, fluids,  
two drips, find and treat the cause, senior +  
anaesthetist immediately  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 22*

Important: a drop of 40 mmHg or more from the patient's usual value is worrying even if the number is still  
above 100 — this 'more than 40 mmHg below normal' rule is a NICE sepsis high-risk criterion CLINICAL REFERENCE  
— SOURCE BACKED (NICE NG253, 2025). The low-side zones are the NEWS2 scoring bands CLINICAL REFERENCE —  
SOURCE BACKED — they are early-warning bands, not diagnostic cut-offs.  
Where this comes from: Royal College of Physicians, National Early Warning Score 2 (NEWS2), 2017; Singer et al., Sepsis-3 definitions,  
JAMA 2016; Surviving Sepsis Campaign 2021.  
Who must approve: Anaesthetist to confirm thresholds and alarm levels.  
Blood pressure — the HIGH side (both numbers)  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Below 140/90 (ideal  
below 120/80)  
Healthy Carry on  
Grade 1 high 140–159 / 90–99 Mild high blood pressure (or just nerves/pain) Repeat; note it; GP follow-up later; does NOT  
stop surgery  
Grade 2 high 160–179 / 100–109 Moderate. UK GPs are asked to get BP below  
160/100 before referring for planned surgery  
Repeat properly; look for a GP record; S1  
usually still proceeds if clinic reading below  
180/110; tell anaesthetist  
Grade 3 high 180 or more / 110 or  
more  
Severe. Planned surgery should be postponed  
and BP controlled first  
Repeat ×2 after rest. If still ≥180/110: S1:  
anaesthetist review — planned surgery is  
normally deferred and refer. S2: inform  
anaesthetist, treat pain, surgery not delayed by  
BP alone  
Dangerous  
(emergency)  
Usually ≥180/120 PLUS  
chest pain,  
breathlessness,  
confusion, weakness,  
severe headache  
Hypertensive emergency: the high pressure is  
damaging organs right now  
EMERGENCY medical review; do not proceed  
Disagreement between sources, explained simply: Europe's 2024 cardiology guideline calls 120–139/70–89  
'elevated', while the 2023 European hypertension guideline calls it 'normal/high-normal'. This doesn't affect  
surgery decisions, which follow the UK anaesthetists' rule of 160/100 (GP) and 180/110 (hospital clinic). CLINICAL  
REFERENCE — SOURCE BACKED  
Where this comes from: Hartle et al., AAGBI & British Hypertension Society guideline, Anaesthesia 2016; ESH 2023; ESC 2024.  
Who must approve: Anaesthetist must confirm the postpone threshold (180/110 draft).  
What causes high BP before surgery?  
- Long-standing high blood pressure (very common after age 40).  
- Anxiety ('white-coat effect' — BP rises just because a doctor is measuring it).  
- Pain (especially S2).  
- Forgot or stopped BP tablets.  
- Wrong (too small) cuff.  
- Rarely: kidney disease, thyroid or hormone problems, drugs (cocaine, decongestants).  
What causes low BP?  
- Dehydration (vomiting, not drinking, long fasting).  
- Bleeding (inside or outside the body).  
- Severe infection (sepsis) — blood vessels relax and leak.  
- Severe allergic reaction (anaphylaxis).  
- Heart problems (heart attack, very fast/slow rhythm).  
- Medicines (BP tablets, strong painkillers).  
- Normal for some healthy slim young people.  
Why does BP matter before surgery?  
Anaesthetic drugs lower blood pressure. A patient who is already low can drop dangerously at the moment  
they're put to sleep. A patient with very high BP has a higher risk of heart attack or stroke around the  
operation.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 23*

When should the trainee repeat it?  
Usually repeat an abnormal reading before acting on it (unless the patient clearly looks unwell — then act and  
repeat at the same time). Correct technique: patient seated, arm supported at heart level, right cuff size, 5  
minutes' rest, no talking; take two readings 1–2 minutes apart and use the lower/average CLINICAL REFERENCE —  
SOURCE BACKED .  
Decision diagram — HIGH blood pressure (S1 planned surgery)  
BP reading is 140/90 or higher  
Check technique: right cuff, arm supported, rested 5  
min, not talking. Repeat twice.  
Any symptoms of organ damage? (chest pain,  
breathless, confused, weak)  
EMERGENCY — urgent medical review, do not  
proceed  
YES  
NO  
Is the lowest repeat reading 180/110 or more?  
Anaesthetist/senior review: planned surgery is  
normally deferred for BP control (AAGBI/BHS);  
refer; explain; document  
YES  
NO  
BP below 180/110 → surgery can PROCEED. Tell  
anaesthetist; ask GP to follow up.  
In words: first make sure the reading is real (repeat it properly). If the patient has symptoms of damage, it's an  
emergency. If the pressure is very high (180/110 or more) but no symptoms, a planned operation is postponed  
so the BP can be controlled safely over weeks — trying to bring it down fast on the morning of surgery can  
itself be harmful CLINICAL REFERENCE — SOURCE BACKED . Below that, surgery goes ahead.  
Decision diagram — LOW blood pressure (both scenarios)  
Systolic BP 110 or lower  
Repeat it. Look at the patient: pale? sweaty?  
confused? cold hands?  
Is it 90 or lower, OR is the patient confused / looking  
very unwell?  
EMERGENCY: call for help, lie flat, oxygen, 2  
drips, fluid bolus, find cause (infection?  
bleeding? allergy?), senior + anaesthetist NOW  
YES  
NO  
Is the heart rate also fast (above 100) or  
temperature abnormal?  
Likely dehydration / infection / bleeding: drip,  
fluids, bloods incl. lactate, tell senior urgently;  
recheck in 15–30 min  
YES  
NO  
Compare with usual BP. If it's their normal and they  
look well → continue, recheck later  
How BP combines with other findings (the reasoning the game must reward)  
Combination What it suggests Why  
Low BP + fast heart + cold clammy  
skin  
Shock from bleeding or dehydration Heart racing to compensate for low blood volume; blood  
diverted away from skin  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 24*

Combination What it suggests Why  
Low BP + fast heart + fever + fast  
breathing  
Septic shock Infection making blood vessels relax and leak  
Low BP + rash/swollen lips/wheeze  
after a medicine  
Anaphylaxis Severe allergic reaction  
Low BP + slow heart Heart rhythm problem, medicine effect  
(e.g. beta-blocker), very late shock  
The heart can't speed up  
High BP + fast heart + patient in pain Pain response Treat pain, re-measure  
Normal BP + fast heart rising over  
time  
EARLY warning — the body is  
compensating  
BP often stays normal until late; don't be falsely  
reassured  
Young woman, low BP + fast heart +  
positive pregnancy test + tummy  
pain  
Differential diagnosis — bleeding from an  
ectopic pregnancy (a mimic of  
appendicitis, not a complication of it)  
Internal bleeding — gynaecology emergency  
Medical word: Shock index (SI)  
Shock index = heart rate ÷ systolic blood pressure (e.g. 120 ÷ 100 = 1.2). The range usually quoted as normal is  
0.5–0.7 (some studies accept up to about 0.9); values approaching or above 1.0 are associated with worse circulation  
and outcomes, even when HR and BP each look acceptable CLINICAL REFERENCE — SOURCE BACKED (Koch et al., Open  
Access Emerg Med 2019; Rivers et al., Ann Emerg Med 1994). It is an additional risk-supporting indicator, not a  
diagnosis and not a stand-alone rule: it is interpreted with the whole clinical picture, and it is less reliable with  
beta-blockers, pregnancy, older age and some populations. The 1.0 and 0.9 values are study-derived markers, not  
guideline cut-offs CLINICAL INTERPRETATION .  

## 4.2 Heart rate (HR, pulse)

What is it and how is it measured?  
The number of heartbeats per minute ('beats per minute', bpm). Each beat pushes a wave of blood you can  
feel at the wrist (the pulse).  
In the game it is shown by the clip on the finger (pulse oximeter) or the heart monitor leads, or the trainee can  
feel the wrist pulse for 30 seconds and double it. Feeling the pulse also tells you if it is regular (steady like a  
clock) or irregular (random) — the monitor number alone doesn't.  
Medical word: Tachycardia / bradycardia  
Tachy- = fast, brady- = slow, -cardia = heart. Tachycardia = heart rate above 100. Bradycardia = heart rate below  
60 (but usually only a concern below 50).  
Heart rate zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Severely slow 40 or lower Heart's electrical system may be failing;  
medicine effect; very cold; very high  
potassium  
Emergency: check patient, heart tracing (ECG),  
potassium blood test; senior + anaesthetist  
Slow 41–50 Often normal in athletes or people on  
'beta-blocker' heart tablets  
Check they feel well and BP is fine; tell  
anaesthetist  
Normal 51–90 Normal resting heart Carry on  
Mildly fast 91–110 Pain, anxiety, fever, mild dehydration, early  
infection  
Treat pain, check temperature and fluids,  
repeat in 30–60 min  
Fast 111–130 Infection/sepsis, dehydration, bleeding, severe  
pain, abnormal rhythm, lung clot  
Check rhythm (ECG if irregular), BP,  
temperature, breathing; bloods; urgent senior  
review  
Very fast 131 or higher Shock, sepsis, fast abnormal rhythm, major  
bleeding  
EMERGENCY: call for help, full assessment,  
ECG, senior + anaesthetist now  
Two different 'normal' numbers: the clinical reference range for adult resting heart rate is 60–100  
beats/min (American Heart Association, All About Heart Rate (Pulse)). NEWS2 gives 0 points only for 51–90 and  
1 point for 91–110 — NEWS2 is an early-warning score designed to trigger a timely review, so it deliberately  
starts counting earlier. ProcSense shows the reference range to the trainee and uses the NEWS2 bands for  
scoring CLINICAL REFERENCE — SOURCE BACKED . Rule of thumb: fever raises the heart rate by roughly 10 beats per  
minute per 1 °C. A heart rate much higher than the fever explains is a warning sign CLINICAL INTERPRETATION .  
Where this comes from: Royal College of Physicians, National Early Warning Score 2 (NEWS2), 2017; Resuscitation Council UK (2021).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 25*

Who must approve: Anaesthetist to confirm alarm levels.  
Teacher says:  
The heart rate is high. That does not automatically mean one particular disease. Possible causes include pain,  
anxiety, dehydration, fever, infection, bleeding or an abnormal heart rhythm. Therefore the trainee should look at the  
other vital signs and the rest of the story before deciding what is happening.  
Decision diagram — FAST heart rate  
Heart rate above 100  
Is it above 130, OR is BP 90 or less, OR is the patient  
confused?  
EMERGENCY — call for help, ABCDE assessment  
(Part 14), senior + anaesthetist  
YES  
NO  
Is the pulse irregular? Do an ECG (heart tracing) — possible atrial  
fibrillation (Part 6). Tell senior/anaesthetist  
YES  
NO  
Look for the cause: pain score? temperature? signs  
of dehydration? bleeding? pregnancy test? anxiety?  
Treat the cause (pain relief, fluids, fever treatment);  
re-measure in 30–60 min  
Has it come down below 100? Still fast despite treatment → suspect infection,  
bleeding or complication; senior review  
NO  
YES  
Continue the pathway; keep checking regularly  
Scenario Expected heart rate If higher than expected  
S1 (calm planned patient) 60–90 Unusual — look for infection (inflamed gallbladder), an irregular rhythm,  
anxiety, thyroid problem. A planned patient with HR above 100 needs a  
reason before proceeding.  
S2 (appendicitis,  
uncomplicated)  
90–110 (pain, mild fever) Above 120 after pain relief and fluids suggests the appendix may have burst,  
sepsis, or (in women) bleeding from an ectopic pregnancy.  

## 4.3 Temperature

What does temperature tell us?  
Body temperature is normally kept very steady (about 36.5–37.5 °C). The immune system deliberately raises it  
to fight infection — that's fever. A low temperature (hypothermia) can happen in cold operating rooms, in the  
elderly, and — worryingly — in very severe infection.  
How is it measured?  
A thermometer in the ear (tympanic) or mouth, shown in degrees Celsius (°C).  
Temperature zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Very low 35.0 °C or lower Serious: severe infection, cold exposure,  
underactive thyroid  
Warm actively; check for sepsis; senior  
Low 35.1–36.0 Mildly cold. Surgery patients who are cold  
bleed more and get more infections  
Warm the patient (warm air blanket). Don't  
send to theatre until ≥36.0 °C unless urgent  
(NICE)  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 26*

Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal 36.1–37.9 Normal Carry on  
Fever 38.0–39.0 Infection or inflammation somewhere Find the source (gallbladder? appendix? urine?  
chest?); blood tests; paracetamol for comfort  
High fever 39.1 or higher More serious infection — abscess, burst  
appendix, infected bile ducts, sepsis  
Sepsis check (Part 4.9), blood cultures,  
antibiotics, senior  
Shaking chills  
('rigors')  
Any temperature with  
uncontrollable shivering  
Germs may be in the blood Blood cultures, urgent review  
Where this comes from: Royal College of Physicians, National Early Warning Score 2 (NEWS2), 2017; NICE CG65 Hypothermia:  
prevention and management in adults having surgery.  
Who must approve: Surgeon to confirm fever thresholds for each variant.  
What does fever mean in S1 and S2?  
- S1: A planned gallbladder patient should not have a fever. Fever of 38.0 °C or more suggests the  
gallbladder has become inflamed/infected (acute cholecystitis) or — if the patient is also yellow — infected  
bile ducts (cholangitis, an emergency). The planned operation is cancelled and the patient moves to an  
urgent pathway.  
- S2: A mild fever (37.3–38.3) fits ordinary appendicitis. 38.5 or more, especially with a heart rate over 120,  
suggests the appendix may have burst or formed an abscess.  

## 4.4 Respiratory rate (breathing rate, RR)

What is it and how is it measured?  
The number of breaths per minute. The trainee watches the chest rise and fall for 30–60 seconds and counts.  
Doctors often count it badly — and it's one of the earliest signs that someone is getting sicker — so the game  
should make the avatar's breathing visible and require counting CLINICAL INTERPRETATION .  
Breathing rate zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Dangerously  
slow  
8 or fewer Too much strong painkiller (opioid), very  
drowsy, exhausted  
Emergency: wake/stimulate, oxygen, airway,  
check painkillers given; senior/anaesthetist  
Slow 9–11 Sedated by medicines Check alertness and oxygen level  
Normal 12–20 Normal Carry on  
Fast 21–24 Pain, anxiety, fever, early infection, chest  
problem, body becoming acidic  
Find the cause; check oxygen and temperature;  
consider sepsis  
Very fast 25 or more Serious illness: sepsis, lung clot, fluid on the  
lungs, inhaled vomit, diabetic emergency  
Urgent senior review; assess, and oxygen per  
protocol if indicated; blood gas test  
A breathing rate of 22 or more is also one of the three 'quick sepsis' warning signs (qSOFA) CLINICAL REFERENCE —  
SOURCE BACKED .  
Where this comes from: Royal College of Physicians, National Early Warning Score 2 (NEWS2), 2017; Sepsis-3 (2016).  

## 4.5 Oxygen saturation (SpO2)

What does it mean?  
Red blood cells carry oxygen using a protein called haemoglobin. Oxygen saturation is the percentage of  
haemoglobin that is carrying oxygen. 98% means almost every 'seat' on the oxygen bus is full.  
SpO2 = Saturation of peripheral (finger) Oxygen measured by Pulse oximetry.  
What is the pulse oximeter measuring?  
A small clip on the finger shines red and infrared light through the fingertip. Oxygen-filled blood absorbs light  
differently from oxygen-poor blood, so the machine calculates the percentage. It also shows a wave (the 'pleth'  
trace) and the heart rate. A poor, flat wave means the reading may be wrong.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 27*

Oxygen saturation zones (most patients)  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal 96% or higher on room air Plenty of oxygen Carry on  
Slightly low 94–95% Mild lung issue, shallow breathing from pain,  
obesity  
Check the probe; sit the patient up; recheck  
Low 92–93% Chest infection, inhaled vomit, fluid overload,  
lung clot, too much painkiller  
Assess airway/breathing and probe; if  
confirmed and clinically indicated, start oxygen  
per protocol to the target range (94–98%, or  
88–92% if hypercapnic risk) and tell senior  
Very low 91% or lower (critical  
below 88%)  
Not enough oxygen reaching the body —  
dangerous  
EMERGENCY: call for help, assess  
airway/breathing, start high-flow oxygen per  
emergency protocol while the cause is found;  
senior + anaesthetist  
Important exception: some people with long-term lung disease (for example severe COPD — chronic  
obstructive pulmonary disease, usually from smoking) normally live at 88–92%, and giving them too much  
oxygen can be harmful. For them the target is 88–92% and NEWS2 uses a special 'Scale 2' CLINICAL REFERENCE —  
SOURCE BACKED . Also: any patient who needs extra oxygen scores extra points in NEWS2 because needing  
oxygen is itself a warning sign. Finger probes can read falsely high in people with darker skin, and falsely  
low with cold fingers, nail polish or movement CLINICAL REFERENCE — SOURCE BACKED .  
Where this comes from: British Thoracic Society oxygen guideline 2017; Royal College of Physicians, National Early Warning Score 2  
(NEWS2), 2017; US FDA safety communication on pulse oximeters (2021).  
v5 correction — oxygen is a response to an ASSESSED problem, not to a number  
Earlier drafts implied 'SpO2 below 92 → give oxygen'. That is not how clinicians work. A low reading is first verified  
and the patient is assessed; oxygen is then given to a target range chosen for that patient (BTS 2017) while the  
cause is found.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 28*

Decision diagram — low SpO2 (the 11-step clinical process)  
1\. Recognise a low reading (below the patient's  
target range)  
2\. LOOK AT THE PATIENT: colour, effort, talking in  
sentences? 3. Airway open? Breathing rate and  
pattern?  
Obvious respiratory distress, cyanosis, falling  
consciousness, or SpO2 very low (e.g. ≤85% — a  
ProcSense simulator trigger, not a guideline cut-off)?  
EMERGENCY: call for help now; high-flow  
oxygen per emergency protocol (BTS: reservoir  
mask 15 L/min in critical illness) while assessing  
YES  
NO  
4–5. Is the reading reliable? Good pleth waveform?  
Warm finger? No nail polish/movement? Re-site  
probe  
Reading still low after checking the probe? Artefact: document; keep observing; consider a  
different site  
NO  
YES  
6\. Context: COPD/CO2 retention? opioids? recent  
vomiting (aspiration)? fluid overload? pain splinting?  
7–8. Choose target: 94–98% most adults; 88–92% if at  
risk of hypercapnic failure. Start/adjust oxygen per  
protocol if below target  
9\. Find the cause (chest exam, RR, fluid balance,  
drug chart, blood gas, chest X-ray as indicated)  
10\. Reassess SpO2, RR, effort within minutes; 11.  
Escalate: senior/anaesthetist if needing oxygen to  
reach target, worsening, or NEWS2 ≥5  
In words: the simulator should reward the trainee for looking at the patient and checking the probe before  
reacting, for choosing the right target (not 100%), for searching for a cause, and for re-checking. Needing new  
oxygen is itself worth 2 NEWS2 points CLINICAL REFERENCE — SOURCE BACKED . Oxygen start/titration in ProcSense is  
initiated per protocol and confirmed by a qualified clinician CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION .  

## 4.6 Alertness (level of consciousness)

What is it?  
Whether the patient is awake and thinking clearly. The brain is very sensitive to low oxygen, low blood sugar,  
low blood pressure, infection and drugs — so new confusion is a big alarm.  
Medical word: ACVPU  
A quick scale: Alert; new Confusion; responds only to Voice; responds only to Pain; Unresponsive. Anything other  
than A is serious. Some hospitals use the Glasgow Coma Scale (GCS), a 3–15 score where 15 is fully awake.  
Alertness zones  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 29*

Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Alert (A) Normal Carry on  
Serious New confusion (C) Sepsis, low oxygen, low sugar, low BP, drugs,  
low sodium  
Check blood sugar FIRST (finger-prick), then  
oxygen, BP, temperature; urgent senior  
Emergency Voice / Pain /  
Unresponsive  
Brain not getting what it needs; airway at risk Call for help immediately; protect the airway;  
emergency team  
A confused patient may not be able to give valid consent (Part 10).  
Where this comes from: Royal College of Physicians, National Early Warning Score 2 (NEWS2), 2017  

## 4.7 Pain score

What is it?  
The patient rates pain from 0 (none) to 10 (worst imaginable). It's a symptom, but recorded like a vital sign.  
Pain score zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Mild 0–3 Comfortable Simple painkiller if wanted  
Moderate 4–6 Typical untreated gallstone or appendix pain Request pain relief; recheck in 30–60 min  
Severe 7–10 Severe pain; or a complication (burst  
appendix, twisted ovary, bleeding)  
Request prompt pain relief (clinician confirms);  
recheck in 15–30 min; re-examine; tell senior if  
not improving  
Pain relief does not hide the diagnosis — giving it early is correct and kind CLINICAL REFERENCE — SOURCE  
BACKED (WSES 2020). Sudden, severe pain or pain that strong painkillers don't help is a warning sign.  

## 4.8 Other bedside clues

Clue How it's checked Normal Worrying What it means  
Capillary refill time Press a fingertip for 5 seconds,  
release, count seconds until pink  
returns  
2 seconds or less More than 2–3 seconds Poor circulation: dehydration,  
shock, cold CLINICAL  
INTERPRETATION (Surviving  
Sepsis Campaign 2021  
suggests capillary refill as an  
adjunct to lactate)  
Urine output Measured over hours (catheter or  
measuring jug)  
At least 0.5 mL per kg body  
weight per hour (e.g. 35  
mL/h for 70 kg)  
Less than that for 6  
hours  
Kidneys not getting enough  
blood; possible kidney injury  
CLINICAL REFERENCE —  
SOURCE BACKED  
Skin and mouth Look Moist mouth, warm pink  
skin  
Dry tongue, sunken  
eyes, cold clammy skin  
Dehydration or shock  
Skin/eye colour Look at the whites of the eyes in  
daylight  
White Yellow (jaundice) Bile backing up into the blood  
(S1)  
Weight and height  
(BMI)  
BMI = weight (kg) ÷ height (m)  
squared  
18.5–24.9 (South Asian  
guidance: overweight from  
23\)  
30+ obese; 40+  
severely obese  
Affects breathing tube, sleep  
apnoea, clots, medicine doses  

## 4.9 NEWS2 — the early-warning score, explained from zero

What is it?  
NEWS2 = National Early Warning Score 2, created by the UK's Royal College of Physicians. Instead of  
judging each vital sign separately, the nurse gives each one 0–3 points depending on how abnormal it is and  
adds them up. The total tells the team how urgently a doctor must see the patient.  
Think of it as a 'danger counter': 0 = all normal; the higher the number, the more urgent.  
What is measured 3 points 2 points 1 point 0 points  
(normal)  
1 point 2 points 3 points  
Breathing rate (per min) ≤8 9–11 12–20 21–24 ≥25  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 30*

What is measured 3 points 2 points 1 point 0 points  
(normal)  
1 point 2 points 3 points  
Oxygen level SpO2 (%) —  
usual scale  
≤91 92–93 94–95 ≥96  
On extra oxygen? Yes No (room air)  
Top BP number (mmHg) ≤90 91–100 101–110 111–219 ≥220  
Heart rate (per min) ≤40 41–50 51–90 91–110 111–130 ≥131  
Alertness Alert Confused / Voice /  
Pain /  
Unresponsive  
Temperature (°C) ≤35.0 35.1–36.0 36.1–38.0 38.1–39.0 ≥39.1  
Read across a row: find which box the patient's number falls in, take the points at the top of that column.  
(There's a separate oxygen scale for the long-term-lung-disease patients mentioned above.)  
Total score Risk How often to check Who responds  
0 Low At least every 12 hours Routine  
1–4 Low At least every 4–6 hours Nurse decides whether to call a doctor  
Any single item scoring  
3  
Low–medium At least hourly Urgent review by the ward doctor  
5–6 Medium At least hourly Urgent review by a doctor skilled in acutely ill patients  
7 or more High Continuous monitoring Emergency team response, including critical-care skills  
Worked example: breathing 26 (3 points) + oxygen 94% (1) + room air (0) + BP 92 (2) + heart rate 128 (2) +  
alert (0) + temperature 39.2 (2) = 10 → emergency response.  
What NEWS2 is NOT  
- It is not a diagnosis. A score of 7 says 'very sick — act now', not 'sepsis'.  
- A planned (S1) patient should score 0–1. Any higher score in a planned patient is itself a red flag.  
- It is not designed for pregnant women (they use a maternity version) or children.  
- Pain and anxiety push heart rate and breathing up; heart tablets (beta-blockers) can hide a fast heart rate.  
- A low score doesn't rule out serious illness — fit young people can look fine until late.  
- Hospitals worldwide use NEWS2, another validated early-warning score (e.g. MEWS, local variants), or none — the  
score used should be the institution's approved tool CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION .  
Where this comes from: Royal College of Physicians, National Early Warning Score 2 (NEWS2), 2017.  

## 4.10 Sepsis, explained from zero

What is it?  
Normally the immune system fights an infection locally (e.g. in the appendix). In sepsis, the body's reaction  
goes out of control and starts harming its own organs: blood vessels leak and relax (BP falls), the kidneys slow  
down, the brain gets confused, breathing speeds up. Septic shock is sepsis with BP so low that medicines are  
needed to support it. Sepsis kills if treatment is delayed; every hour matters.  
v5 correction — NEWS2 is a risk-stratification tool, not a diagnosis  
A NEWS2 total tells you how unwell the patient's physiology is and how quickly someone must respond. It does not  
say why. Sepsis is recognised by combining suspected infection + abnormal physiology + signs of organ  
dysfunction or hypoperfusion + clinical appearance and trajectory.  
How do doctors recognise possible sepsis? (the reasoning the trainee must show)  
1\. Is infection suspected or confirmed? S1: inflamed gallbladder, infected bile duct (cholangitis), urine or  
chest infection. S2: appendicitis, especially perforated, or an abscess.  
2\. Calculate NEWS2 from all seven parameters — and look at each parameter individually. A single  
parameter scoring 3 (e.g. SBP ≤90, RR ≥25, HR ≥131, new confusion) matters even when the total is low  
CLINICAL REFERENCE — SOURCE BACKED (RCP NEWS2).  
3\. Look for organ dysfunction / hypoperfusion: new confusion, low BP or a fall of more than 40 mmHg  
below usual, mottled/cold skin, low urine output, raised lactate (≥2 mmol/L) CLINICAL REFERENCE — SOURCE  
BACKED (Sepsis-3; NICE NG253).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 31*

4\. Look at the patient and the trend: is the patient worsening over the last hours? Do family or nurses  
report concern? Clinical concern can raise the risk category.  
5\. Assign a risk category per NICE NG253 (adults ≥16 in acute hospital settings):  
NICE NG253 risk  
category  
Criteria What should happen (team actions) Reassess  
High risk of severe  
illness or death from  
sepsis  
Suspected infection AND NEWS2 ≥7, or  
NEWS2 5–6 with a single parameter  
scoring 3 (or confirmed high risk on  
medical review)  
Senior review; IV antibiotics within 1 hour; IV  
fluid bolus within 1 hour (unless  
contraindicated); consider critical care  
NEWS2 every 30 min;  
escalate if no response  
within 1 hour  
Moderate risk NEWS2 5–6, or NEWS2 3–4 with a  
single parameter scoring 3  
Review by a clinician within 1 hour, including  
venous lactate; antibiotics within 3 hours if  
infection likely (time used to clarify diagnosis)  
NEWS2 hourly  
Low risk NEWS2 1–4 Clinical assessment; antibiotics within 6 hours if  
infection is the likely diagnosis  
NEWS2 every 4–6 hours  
Very low risk NEWS2 0 Routine care unless clinical concern With standard observations  
Source: NICE NG253 Suspected sepsis in over 16s: identification and early management in acute hospital  
settings using NEWS2 (November 2025) CLINICAL REFERENCE — SOURCE BACKED . 'qSOFA' (RR ≥22, altered mentation,  
SBP ≤100) predicts poor outcome but the Surviving Sepsis Campaign 2021 recommends against using it alone  
as a screening tool CLINICAL REFERENCE — SOURCE BACKED . The institution's approved sepsis pathway is used at  
implementation CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Decision diagram — possible sepsis  
Abnormal observations OR clinical concern in a  
patient who might have an infection  
Infection suspected/confirmed?  
Look for other causes of abnormal physiology  
(pain, bleeding, dehydration, PE, arrhythmia);  
still respond to NEWS2  
NO  
YES  
Calculate NEWS2; check single parameters; check  
confusion, BP vs usual, urine, skin, lactate  
NEWS2 ≥7, or 5–6 with a single 3?  
HIGH RISK: call senior now; flag antibiotics +  
fluid bolus within 1 h; cultures; lactate; NEWS2  
every 30 min; consider critical care  
YES  
NO  
NEWS2 5–6, or 3–4 with a single 3?  
MODERATE: clinician review within 1 h incl.  
lactate; antibiotics within 3 h if infection likely;  
NEWS2 hourly  
YES  
NO  
LOW: assess; antibiotics within 6 h if infection likely;  
reassess with each obs set; escalate if the trend  
worsens  
What does the trainee do? (all within their role)  
1\. Recognise and say it out loud: "I'm worried this could be sepsis."  
2\. Call for senior help using SBAR, stating the NEWS2, single red parameters and why infection is suspected.  
3\. Start what the protocol allows the team to start immediately: oxygen to target if SpO2 is below range; IV  
access; bloods including lactate and blood cultures (taken before antibiotics but without delaying them).  
4\. Flag that antibiotics and an IV fluid bolus are needed within the NG253 time; a qualified clinician prescribes;  
the nurse administers EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION (bolus example 500  
mL crystalloid over less than 15 min, NICE CG174; the clinician sets volume and type for the individual  
patient).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 32*

5\. Measure urine output; recalculate NEWS2 at the interval for the risk tier.  
6\. Identify the source and the plan for source control (S2: surgery; S1 cholangitis: biliary drainage).  
Where this comes from: Singer et al., Sepsis-3, JAMA 2016; Evans et al., Surviving Sepsis Campaign 2021; NICE NG253 Suspected  
sepsis in over 16s in acute hospital settings using NEWS2 (November 2025).  

## 4.11 Putting the vital signs together — pattern recognition

Teacher says:  
This is where beginners and experts differ. Experts don't look at one number; they look at the pattern. Here are the  
patterns ProcSense uses.  
Pattern Example numbers Most likely meaning Trainee's next move  
All normal, calm HR 76, BP 124/78, RR 14, SpO2 98%, T 36.7,  
alert  
Stable Continue  
Pain pattern HR 104, BP 146/88, RR 20, T 37.0, pain 8/10 Pain is driving the numbers Pain relief, then re-measure  
Infection pattern HR 108, T 38.6, RR 20, BP normal Infection/inflammation Find source, bloods, antibiotics per  
plan  
Dehydration pattern HR 112, BP 104/66, dry mouth, little urine,  
vomiting for 2 days  
Not enough fluid Drip + fluids, kidney blood tests,  
re-measure  
Sepsis pattern HR 128, BP 92/58, RR 26, T 39.2, lactate 3.8 Sepsis with poor circulation Sepsis bundle, emergency call  
Bleeding pattern HR 122, BP 96/60, pale, cold, Hb falling Blood loss (e.g. ectopic) Emergency: 2 drips, blood ready,  
senior/gynae  
Allergy pattern Minutes after a drug: rash, swollen lips,  
wheeze, BP 84/50  
Anaphylaxis Stop drug, adrenaline, call for help  
Too-much-painkiller  
pattern  
RR 8, SpO2 89%, very sleepy, small pupils Opioid overdose Stimulate, oxygen, call for help;  
antidote by clinician  
Rhythm pattern HR 128, pulse irregular, BP 132/84,  
otherwise well  
Atrial fibrillation (irregular  
heart rhythm)  
ECG, senior/medical review  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 33*


# Part 5 — Blood and urine tests, taught from zero

Teacher says:  
Before we look at individual tests, five things every engineer must understand about lab results.  
1\. How a blood test happens  
1\. The trainee checks the patient's identity (name + date of birth against wristband).  
2\. Blood is taken from a vein into coloured tubes (each colour contains a different chemical for different tests).  
3\. Tubes are labelled at the bedside — never pre-labelled away from the patient (that's how 'wrong blood in  
tube' errors happen).  
4\. The lab analyses them; results appear after minutes to hours.  
5\. The trainee reads, interprets, acts, and 'acknowledges' (signs off) each result.  
2\. What is a 'reference range'?  
Labs measure thousands of healthy people and call the middle 95% the 'reference range' (or 'normal range').  
That means 1 in 20 healthy people will be slightly outside it just by chance. Different labs and machines have  
slightly different ranges — the supervising hospital's lab ranges must replace the ones in this book  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . Where a harmonised range exists, this book  
uses the UK Pathology Harmony adult ranges (2011) CLINICAL REFERENCE — SOURCE BACKED ; otherwise typical ranges  
from named guidelines. These are reference ranges (what healthy people show), which are NOT the same as  
decision thresholds (when to act, delay or call for help) — each test below shows both.  
3\. A number is a clue, not a verdict  
A doctor always asks: does it fit the patient? Is it new or has it always been like this? Is it dangerous now? Could  
the sample be faulty?  
4\. Faulty samples exist  
- Haemolysed sample — the red blood cells burst in the tube (from a difficult blood draw or delay). They  
release potassium, so the potassium looks falsely high. The lab writes 'haemolysed' on the report.  
- Drip-arm sample — blood taken from the same arm as a running drip is diluted (or full of sugar from a  
sugar drip).  
- Wrong tube contamination — e.g. the purple tube's chemical (EDTA) falsely raises potassium and lowers  
calcium.  
- Mislabelled sample — results belong to someone else.  
- Correct behaviour: if a strange result doesn't fit a well-looking patient, repeat it — but if it's dangerous and  
could be real, treat and repeat at the same time.  
5\. Critical ('panic') results  
Some results are so dangerous the lab phones the doctor immediately. The trainee must answer, repeat the  
value back to confirm, act, and document. An ignored critical result is a safety failure in the game.  
Units cheat-sheet  
Laboratories worldwide use either SI units (as in the UK, Europe, much of Asia) or 'conventional' units (as in the  
US) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . Conversions: bilirubin 1 mg/dL = 17.1  
µmol/L; glucose 1 mmol/L = 18 mg/dL; creatinine 1 mg/dL = 88.4 µmol/L; haemoglobin 1 g/dL = 10 g/L.  
'µmol/L' = micromoles per litre; 'mmol/L' = millimoles per litre; 'U/L' = units per litre; '×109/L' = billions of cells  
per litre.  

## 5.0 Reference range vs decision threshold — every test at a glance

Read across: the reference range is what healthy people show; the other columns are where doctors worry, act  
urgently, or change the surgical plan. Final displayed ranges must be aligned with the target hospital  
laboratory CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 34*

Test Reference range (source) Clinical concern Emergency / critical Decision threshold /  
ProcSense rule  
Haemoglobin  
g/L  
M 130–170, F 120–150 (typical);  
anaemia = below 130 M / 120 F  
(WHO 2024)  
Moderate 80–109 (WHO) Below 70 or fall ≥20  
(transfusion threshold 70,  
NICE NG24)  
Unexplained anaemia →  
investigate before planned  
surgery (consensus, Muñoz  
2017\) CLINICAL  
INTERPRETATION  
WBC ×109/L 4.0–11.0 (typical) >11 or <4 (SIRS); >10  
scores in AIR/Alvarado  
\>18 (TG18 grade II);  
neutrophils <0.5  
Raised WBC in planned  
patient → reassess for  
acute infection CLINICAL  
INTERPRETATION  
Platelets ×109/L 150–400 (typical) <100 (TG18 organ  
dysfunction)  
<50 for surgery (BSH 2017) ≥50 for most surgery, ≥80  
for neuraxial (BSH 2017)  
CRP mg/L <5 (many labs <10) ≥10 (AIR scores 1) ≥50 (AIR 2); ≥300 critical  
(RCPath)  
Supports diagnosis; not a  
cancellation value  
Sodium mmol/L 133–146 (Pathology Harmony) or  
135–145  
130–134 / 146–150 <125 profound (European  
2014); ≤120 / ≥160 critical  
(RCPath)  
Planned surgery is usually  
deferred  
(senior/anaesthetist  
decide) if <130 or >150  
CLINICAL  
INTERPRETATION  
Potassium  
mmol/L  
3.5–5.3 (Pathology Harmony) ≥5.5 hyperkalaemia  
(UKKA); <3.5  
hypokalaemia  
≥6.5 or ECG change (UKKA);  
≤2.5 critical  
Planned surgery  
acceptable 3.0–5.5  
CLINICAL  
INTERPRETATION  
CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL  
/ CLINICIAN  
CONFIRMATION  
Urea mmol/L 2.5–7.8 (Pathology Harmony) Rising with creatinine — Interpreted with creatinine  
Creatinine  
µmol/L  
Lab/method specific (\~45–110) AKI stage 1: ×1.5–1.9  
baseline or +26.5 in 48 h  
(KDIGO)  
Stage 3: ×3 or ≥354 (KDIGO) AKI → planned surgery is  
usually deferred  
(senior/anaesthetist  
decide) CLINICAL  
INTERPRETATION ; correct  
then proceed in emergency  
eGFR  
mL/min/1.73m²  
≥90 (G1); 60–89 (G2) (KDIGO) <60 CKD G3 <15 G5 Drug-dose changes; NSAID  
avoidance  
Glucose mmol/L Fasting 3.9–5.5 (ADA <5.6; WHO  
<6.1)  
Peri-op target 6–10,  
acceptable 6–12  
(CPOC/JBDS)  
<4.0 hypo; >12 treat; >14  
check ketones (JBDS)  
—  
HbA1c  
mmol/mol  
<42 (NICE) 48+ diabetes (WHO 2011) — >69 (8.5%) → optimise  
before planned surgery  
(CPOC)  
Ketones mmol/L <0.6 (JBDS) 0.6–1.5 ≥3.0 with acidosis = DKA  
(JBDS)  
DKA → no planned surgery  
Bilirubin µmol/L <21 (Pathology Harmony) ≥34 (2 mg/dL) TG18  
cholangitis criterion  
\>68 (4 mg/dL) + CBD >6 mm  
\= ASGE high risk  
ASGE high → ERCP pathway  
before cholecystectomy  
ALT U/L Lab specific (often <35–40; ACG  
true ULN 29–33 M, 19–25 F)  
\>1.5× ULN (TG18  
cholangitis criterion)  
Very high (often >300–500)  
with passing stone  
Pattern-based (Part 5.16)  
ALP U/L 30–130 (Pathology Harmony) >1.5× ULN (TG18) — Cholestatic pattern → duct  
imaging (ASGE  
intermediate)  
GGT U/L Lab/sex specific (e.g. M <50–60,  
F <35–40)  
\>1.5× ULN (TG18) — Confirms hepatobiliary ALP  
Albumin g/L 35–50 (Pathology Harmony) <35 <25 CLINICAL  
INTERPRETATION  
Risk marker, not a  
cancellation value  
Lipase/amylase Lab ULN — >3× ULN = pancreatitis  
criterion (Atlanta 2012)  
Pancreatitis → planned list  
cancelled  
INR 0.8–1.2 (typical) >1.5 (TG18 hepatic  
dysfunction)  
≥5 on warfarin (critical list) ≤1.5 general surgery; ≤1.4  
neuraxial (AoA 2013)  
CLINICAL  
INTERPRETATION  
APTT ratio 0.8–1.2 (typical) >1.2 — Haematology advice if  
unexplained  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 35*

Test Reference range (source) Clinical concern Emergency / critical Decision threshold /  
ProcSense rule  
Lactate mmol/L <2.0 ≥2 (Sepsis-3 / SSC) ≥4 severe hypoperfusion  
(SSC)  
Sepsis pathway  
β-hCG IU/L  
(serum)  
<5 negative 5–25 equivocal (repeat 48  
h)  
Positive + pain/shock =  
ectopic emergency (NICE  
NG126)  
Positive → planned surgery  
postponed  
Urine dipstick Negative Leucocytes ± nitrite — Not a routine pre-op test  
(NICE NG45)  
Sources: Pathology Harmony Group (2011); WHO haemoglobin cut-offs (2024); NICE NG24, NG45, NG126; British Society for  
Haematology platelet guideline (2017); UK Kidney Association hyperkalaemia guideline (2023); European hyponatraemia guideline  
(2014); KDIGO AKI (2012) and CKD (2024); JBDS/CPOC peri-operative diabetes (2021/2023); WHO HbA1c (2011); ASGE  
choledocholithiasis (2019); Tokyo Guidelines 2018; ACG liver chemistries (2017); revised Atlanta (2012); Association of  
Anaesthetists/OAA/RA-UK regional anaesthesia & coagulation (2013); Royal College of Pathologists critical results (2017); Sepsis-3  
(2016); Surviving Sepsis Campaign (2021).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 36*


## 5A — Full blood count (FBC): the blood cells

FBC = full blood count (Americans say CBC, complete blood count). One purple tube; the machine counts  
three kinds of cells: red cells (carry oxygen), white cells (fight infection) and platelets (stop bleeding).  
When is an FBC ordered?  
S2 (emergency): routinely. S1 (planned): if the patient has significant other illnesses, signs of anaemia, heart or  
kidney disease, or local rules require it CLINICAL REFERENCE — SOURCE BACKED (NICE NG45) CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

## 5.1 Haemoglobin (Hb)

What is it?  
The oxygen-carrying protein inside red blood cells. The Hb number tells you how much oxygen-carrying  
capacity the blood has. Low Hb = anaemia ('not enough blood').  
Why do doctors check it?  
Before surgery we need enough 'oxygen trucks' because operations can lose blood and anaesthesia lowers  
oxygen delivery. In S2, a falling Hb can reveal internal bleeding.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Severely low Below 80 g/L (critical  
below 70 or a sudden fall  
of 20+)  
Major bleeding or severe long-term anaemia Urgent senior; blood transfusion decision (NICE  
NG24 restrictive threshold 70 g/L; 80 g/L in  
acute coronary syndrome); planned surgery is  
usually deferred (senior/anaesthetist decide)  
Moderately low 80–109 g/L Significant anaemia: slow blood loss (e.g. from  
the gut), poor diet/iron, blood disorders; or  
acute bleeding  
S1: discuss with senior/anaesthetist; investigate  
before planned surgery. S2: blood group  
sample, look for bleeding, repeat  
Mildly low Men 110–129; women  
110–119 g/L  
Mild anaemia — iron deficiency is the  
commonest cause worldwide (WHO); also  
thalassaemia trait, common in Mediterranean,  
Middle Eastern, South and South-East Asian  
populations (inherited, harmless form)  
Note; check red-cell size (MCV); S1 usually  
proceeds; tell anaesthetist  
Normal Men 130–170; women  
120–150 g/L  
Enough oxygen-carrying capacity Carry on  
High Above 170 (men) / 150  
(women)  
Dehydration (blood concentrated), smoking,  
lung disease, rare blood disorders  
Look for dehydration; usually not a surgery  
issue  
Does one abnormal result automatically diagnose something?  
No. A low Hb says 'anaemia' but not why. Early in sudden bleeding, Hb can still look normal because the blood  
lost is whole blood — the concentration only drops after the body adds fluid. So a normal Hb never rules out  
bleeding CLINICAL INTERPRETATION .  
What should the trainee check next?  
1\. Is it new? Compare with previous results.  
2\. Look at red cell size (MCV = mean cell volume; below 80 fL means small cells, suggesting iron deficiency or  
thalassaemia).  
3\. Ask about bleeding (black stools, heavy periods), diet, tiredness.  
4\. S2: is the patient bleeding now? (pregnancy test, falling Hb, low BP)  
5\. S1 with unexplained anaemia: senior decides whether to investigate before a planned operation.  
Example patient — let's think it through says:  
S1 patient, woman, 52, tired. Hb 98 g/L, MCV 72 fL. → "Hb is moderately low and the cells are small, which suggests  
iron deficiency. Why is she losing iron? It could be diet or periods, but unexplained iron deficiency at 52 could be slow  
bleeding from the gut. This is a planned operation, so there's time. I tell my senior and the anaesthetist; the likely  
decision is to postpone, check iron levels and investigate the cause." CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 37*

Where this comes from: WHO guideline on haemoglobin cut-offs (2024); NICE NG24 Blood transfusion; International consensus on  
peri-operative anaemia (Anaesthesia 2017).  
Clinician validation: Hospital lab ranges; surgeon/anaesthetist decide anaemia postponement rule.  

## 5.2 White blood cells (WBC) and neutrophils

What is it?  
WBC = white blood cell count — the number of immune-system 'soldier' cells. Neutrophils are the most  
common type and the first responders to bacterial infection; labs give their number and their percentage.  
Why do doctors check it?  
A raised WBC suggests the body is fighting infection or inflammation (appendicitis, inflamed gallbladder). It's  
used in appendicitis scores and gallbladder severity grading.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Very low WBC below 4.0;  
neutrophils below 1.5  
(critical below 0.5)  
Overwhelming infection exhausting the cells,  
viral illness, some medicines, bone-marrow  
problems. Some healthy people naturally run  
low  
Low WBC WITH infection signs is serious —  
urgent senior; neutrophils below 0.5 → urgent  
haematology advice  
Normal WBC 4.0–11.0 ×109/L;  
neutrophils 2.0–7.5  
(40–75%)  
Normal. NOTE: 10–20% of people with  
appendicitis have a normal WBC  
Don't rule out appendicitis on this alone  
Raised 11.1–15.0 Infection or inflammation; also stress, steroid  
tablets, pregnancy (up to about 15 can be  
normal)  
Fits appendicitis/cholecystitis in context; use in  
score  
High 15.1–20.0 More severe infection: burst appendix,  
gangrenous gallbladder, abscess  
Senior review; above 18 = 'moderate' grade  
gallbladder inflammation (TG18)  
Very high Above 20.0 Severe sepsis, abscess, or blood disorder Urgent senior; sepsis check  
Does one abnormal result automatically diagnose something?  
No. Stress, pain, steroids and pregnancy also raise WBC, and appendicitis can have a normal WBC. It's one clue  
that goes into a score with the history and examination.  
What should the trainee check next?  
1\. Look at neutrophil percentage (used in AIR and AAS scores).  
2\. Look at CRP (next test).  
3\. Look at temperature and heart rate.  
4\. Calculate the appendicitis score (S2) or gallbladder grade (S1).  
Example patient — let's think it through says:  
S2 patient, man, 19. WBC 13.6, neutrophils 82%. → "Raised white cells with lots of neutrophils — the body is fighting  
something, probably bacterial. With pain moving to the lower right, this supports appendicitis, but doesn't prove it. I'll  
add it to the appendicitis score and check CRP."  
Where this comes from: Typical UK laboratory ranges; Tokyo Guidelines 2018; Andersson 2008 (AIR); Sammalkorpi 2014 (AAS).  

## 5.3 Platelets (Plt)

What is it?  
Tiny cell fragments that clump together to plug holes in blood vessels — the first step in stopping bleeding.  
Why do doctors check it?  
Too few → bleeding during surgery. Very low numbers also appear in severe sepsis.  
Number zones  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 38*

Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Severely low Below 50 ×109  
/L (critical  
below 30)  
Bone-marrow problem, severe sepsis (clotting  
system going haywire), immune destruction,  
liver disease, dengue fever in endemic regions  
Planned surgery is usually deferred  
(senior/anaesthetist decide); S2: senior +  
haematology; platelet transfusion often needed  
for surgery  
Moderately low 50–99 As above; also reaction to heparin (a blood  
thinner)  
Senior/anaesthetist (spinal injections usually  
need 75–80+); repeat in a different tube  
Mildly low 100–149 Often harmless; liver disease; pregnancy Repeat; usually proceed  
Normal 150–400 Normal Carry on  
High Above 450 Usually a reaction to infection, iron deficiency  
or bleeding  
Usually no delay  
Does one abnormal result automatically diagnose something?  
No. Platelets sometimes clump in the purple tube and the machine under-counts them (a false low) — repeat in  
a blue (citrate) tube before acting in a well patient CLINICAL INTERPRETATION .  
Where this comes from: British Society for Haematology platelet transfusion guideline (2017); TG18 (platelets below 100 = organ  
dysfunction).  

## 5.4 CRP (C-reactive protein)

What is it?  
A protein the liver makes when there is inflammation anywhere in the body. It rises within about 6–8 hours and  
peaks around 48 hours. Measured in mg/L (milligrams per litre).  
Why do doctors check it?  
Shows how much inflammation there is. Part of appendicitis scores. Helps judge if an infection is severe.  
When should it NOT be ordered?  
Not needed for a well S1 patient with uncomplicated gallstones.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Below 5 (some labs use  
below 10)  
No significant inflammation Carry on  
Mild–moderate 5–49 (10–49 if lab normal  
is below 10)  
Inflammation present (early appendicitis, mild  
gallbladder inflammation, viral illness)  
Use in score with other findings  
Significant 50–149 More marked inflammation; complicated  
appendicitis more likely, especially after 24 h  
of symptoms  
Senior review; imaging  
Severe 150 or more (300+ is  
phoned as a critical result  
in many labs)  
Abscess, burst appendix, gangrenous  
gallbladder, severe infection  
Urgent senior; sepsis check  
Does one abnormal result automatically diagnose something?  
No. CRP rises with any inflammation (chest infection, urine infection, arthritis). And very early appendicitis  
(under 12 hours) can have a normal CRP. But if CRP and WBC are both normal after more than 24 hours of  
pain, appendicitis becomes less likely CLINICAL REFERENCE — SOURCE BACKED .  
What should the trainee check next?  
1\. Note how long the symptoms have lasted (the AAS score uses different CRP points for under/over 24  
hours).  
2\. Combine with WBC, temperature, examination.  
3\. If borderline and story unclear: repeat in 6–12 hours.  
Where this comes from: WSES Jerusalem guidelines 2020; Andersson 2008; Sammalkorpi 2014; Royal College of Pathologists critical  
results 2017.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 39*


## 5B — Kidneys and body salts (U&E)

U&E = urea and electrolytes. 'Electrolytes' are body salts dissolved in the blood (sodium, potassium).  
Together with creatinine and eGFR this panel tells us how well the kidneys are working and whether the salts  
are balanced.  
When is it ordered?  
S2: vomiting, dehydration, sepsis, before a CT scan with dye. S1: age 65+, diabetes, kidney disease, heart  
failure, on water tablets or certain BP tablets, or sicker patients CLINICAL REFERENCE — SOURCE BACKED (NICE NG45).  
Not needed for a healthy young S1 patient.  

## 5.5 Sodium (Na)

What is it?  
The main salt in blood. It controls how water moves in and out of cells — especially brain cells.  
Why do doctors check it?  
Too low or too high and brain cells swell or shrink → confusion, fits. It also hints at dehydration or medicine side  
effects.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Severely low Below 125 mmol/L  
(critical 120 or below)  
Too much water relative to salt: certain  
medicines, vomiting replaced with plain water,  
hormone problems, heart/liver failure  
Risk of confusion/fits: urgent senior; planned  
surgery is usually deferred (senior/anaesthetist  
decide); correct SLOWLY (too-fast correction  
damages the brain)  
Moderately low 125–129 As above S1 usually deferred after review CLINICAL  
INTERPRETATION (no guideline sets a sodium  
cancellation cut-off); S2 correct cause, tell  
anaesthetist  
Mildly low 130–134 Water tablets, vomiting, mild dehydration Usually proceed; review medicines; repeat  
Normal 135–145 Balanced Carry on  
High 146–155 Dehydration (losing more water than salt) Fluids; S1 usually deferred after review if above  
150 CLINICAL INTERPRETATION  
Severely high Above 155 (critical 160+) Severe dehydration Urgent senior  
Where this comes from: Reference range: UK Pathology Harmony 133–146 mmol/L; many labs (Ireland, Australasia, US) use  
135–145 — both are valid lab conventions, ProcSense displays 135–145 unless the hospital lab differs. Severity bands (mild  
130–134, moderate 125–129, profound below 125) and the correction speed limit (no more than 10 mmol/L in the first 24 h, 8  
mmol/L each later day) from the European clinical practice guideline on hyponatraemia (Spasovski et al., 2014). Critical values:  
Royal College of Pathologists (2017).  
Clinician validation: Anaesthetist: confirm postpone default (below 130 / above 150 — clinical interpretation).  

## 5.6 Potassium (K)

What is it?  
A salt that the heart's electrical system depends on. Tiny changes matter: the safe range is narrow.  
Why do doctors check it?  
Potassium that is too high or too low can make the heart beat in a dangerous rhythm or stop — and  
anaesthetic drugs make this more likely. That is why it is one of the most important pre-operative blood  
results.  
Number zones  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 40*

Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Severely low Below 2.5 mmol/L  
(critical)  
Lost through vomiting/diarrhoea, water  
tablets, insulin  
ECG, heart monitor, replace through drip  
(clinician sets rate), senior + anaesthetist;  
planned surgery is usually deferred  
(senior/anaesthetist decide)  
Moderately low 2.5–2.9 As above ECG; replace; S1 usually deferred after review;  
S2 correct before/at anaesthesia per  
anaesthetist  
Mildly low 3.0–3.4 Often from vomiting in S2 Replace by mouth or drip; usually proceed;  
check magnesium  
Normal 3.5–5.3 (many labs  
3.5–5.0)  
Normal Carry on  
Just above  
reference  
5.4 (above 5.3, below 5.5) Above the reference range but NOT  
hyperkalaemia by the UKKA definition  
Repeat with next bloods; review medicines; no  
emergency action  
Mild  
hyperkalaemia  
(UKKA)  
5.5–5.9 Most often a faulty (haemolysed) sample; also  
kidney injury, some BP tablets  
Urgent 12-lead ECG (UKKA: all hyperkalaemia);  
check 'haemolysed' comment; repeat; review  
kidneys and medicines  
Moderately high 6.0–6.4 Kidney injury, medicines, acid in the blood Urgent repeat + ECG; treat if confirmed; senior  
\+ anaesthetist; planned surgery is usually  
deferred (senior/anaesthetist decide)  
Severely high 6.5 or more (critical), or  
ANY high value with ECG  
changes  
Dangerous for the heart EMERGENCY: heart monitor; treatment to  
protect the heart and push potassium into cells  
(doses clinician-controlled); no anaesthetic until  
treated  
Reference range 3.5–5.3 mmol/L (Pathology Harmony) CLINICAL REFERENCE — SOURCE BACKED . Hyperkalaemia = 5.5  
or above; mild 5.5–5.9, moderate 6.0–6.4, severe 6.5+ (UK Kidney Association 2023) CLINICAL REFERENCE — SOURCE  
BACKED . There is NO guideline-defined potassium value for cancelling planned surgery; ProcSense default  
acceptance range for planned surgery 3.0–5.5 mmol/L CLINICAL INTERPRETATION CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION .  
Does one abnormal result automatically diagnose something?  
No — a high potassium in a well patient with a normal ECG and a 'haemolysed' lab comment is probably a  
faulty sample. Repeat urgently. But do not dismiss it: if the ECG shows changes, the team treats immediately.  
What should the trainee check next?  
1\. Read the lab comment (haemolysed?).  
2\. Do an ECG (Part 6): high potassium causes tall pointed 'T waves'; low potassium causes flat T waves and  
extra 'U waves'.  
3\. Check kidney numbers (creatinine) and medicines.  
4\. Repeat the test.  
5\. Tell the anaesthetist about any confirmed abnormal value.  
Example patient — let's think it through says:  
S1 patient, man, 50, feels fine. Potassium 6.4 with lab comment 'sample haemolysed'. ECG normal. → "6.4 is in the  
moderately high zone, which could be dangerous. But he's well, his kidneys are normal, the ECG is normal and the lab  
says the sample burst. Most likely it's false. I'll repeat it urgently and tell the anaesthetist." Repeat: 4.6 → "Normal.  
The first result was a faulty sample. Continue."  
Where this comes from: UK Kidney Association, Treatment of acute hyperkalaemia in adults (2023); Royal College of Pathologists  
(2017).  
Clinician validation: Anaesthetist must confirm acceptance range; hospital hyperkalaemia protocol.  

## 5.7 Urea

What is it?  
A waste product made when the body breaks down protein; the kidneys remove it.  
Why do doctors check it?  
Rises when the kidneys aren't working well or the patient is dehydrated (urea rises more than creatinine in  
dehydration). Also rises with bleeding in the upper gut.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 41*

Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal 2.5–7.8 mmol/L Normal Carry on  
Raised 7.9–12 Dehydration, kidney strain, high-protein diet Fluids; check creatinine  
High Above 12 Significant dehydration or kidney injury Fluids; repeat; senior  
Where this comes from: Typical lab ranges.  

## 5.8 Creatinine and eGFR — kidney function

What is it?  
Creatinine is a waste product from muscles, cleared by the kidneys. If the kidneys slow down, creatinine  
builds up. eGFR = estimated glomerular filtration rate: a calculated 'kidney score' (in mL/min/1.73m²)  
worked out from creatinine, age and sex. Higher is better (about 90+ is normal); think of it as the percentage  
of normal kidney filtering.  
Why do doctors check it?  
Kidney injury makes surgery riskier, changes which medicines and doses are safe (e.g. no anti-inflammatory  
painkillers, lower doses of blood thinners), and can raise potassium.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Creatinine men \~60–110,  
women \~45–90 µmol/L;  
eGFR 90+  
Kidneys working normally Carry on  
Mild reduction  
(long-term)  
eGFR 60–89 Common with age Usually no change  
Moderate  
long-term  
eGFR 30–59 Chronic kidney disease (CKD) Adjust medicines; avoid kidney-harming drugs  
Severe  
long-term  
eGFR below 30 Severe CKD Anaesthetist; medicine doses change  
Acute kidney  
injury stage 1  
Creatinine 1.5–1.9 × the  
patient's normal, OR up  
by 26.5+ in 48 h, OR low  
urine for 6+ h  
Kidneys suddenly struggling: dehydration,  
sepsis, medicines  
Fluids; stop harmful medicines; measure urine;  
repeat within 24 h; S1 usually deferred after  
review  
AKI stage 2 2.0–2.9 × normal More severe Senior + kidney/medical advice  
AKI stage 3 3 × normal or more, OR  
354+, OR very little urine  
Severe kidney failure Urgent senior/kidney team/intensive care  
Does one abnormal result automatically diagnose something?  
No. Creatinine depends on muscle mass (very muscular people run high; frail elderly run low), and eGFR is  
unreliable when kidney function is changing quickly. Always compare with the patient's previous value CLINICAL  
REFERENCE — SOURCE BACKED .  
What should the trainee check next?  
1\. Find a previous creatinine to compare.  
2\. Check fluid status (dehydrated?), urine output, BP.  
3\. Stop kidney-harming medicines: anti-inflammatory painkillers (NSAIDs such as ibuprofen), some BP tablets  
(ACE inhibitors/ARBs), metformin (diabetes).  
4\. Give fluids if dehydrated, then repeat the blood test.  
Example patient — let's think it through says:  
S2 patient, woman, 45, vomiting for 2 days. Urea 12.4, creatinine 160 (her GP result last year: 85). → "160 is almost  
double her normal — that's acute kidney injury stage 1 (just under stage 2). The high urea plus vomiting suggests  
dehydration. I'll ask the senior to prescribe IV fluids, flag that ibuprofen should be avoided, watch her urine, repeat  
the test and tell my senior and the anaesthetist. Her appendix operation still goes ahead once she's been given fluids,  
because waiting would let the infection worsen."  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 42*

Where this comes from: KDIGO Acute Kidney Injury guideline (2012); NICE NG148 Acute kidney injury (2019); KDIGO CKD (2024).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 43*


## 5C — Blood sugar and diabetes

Medical word: Diabetes  
A long-term condition where blood sugar (glucose) runs too high because the body doesn't make enough insulin (type  
1\) or doesn't respond to it properly (type 2). Insulin is the hormone that moves sugar from blood into cells. People  
with diabetes take tablets and/or insulin injections. Fasting for surgery can make their sugar drop dangerously low;  
stress and infection can push it very high.  

## 5.9 Blood glucose (blood sugar)

What is it?  
The amount of sugar in the blood, measured in mmol/L. Usually measured with a finger-prick machine  
(capillary blood glucose, CBG) in about a minute, or in the lab.  
Why do doctors check it?  
Both very low and very high sugar are dangerous. Low sugar starves the brain (confusion, fits, coma). High  
sugar increases infection and wound problems and can lead to diabetic emergencies.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Hypoglycaemia  
(low)  
Below 4.0 ('4 is the floor');  
severe below 3.0  
Too much diabetes medicine while fasting,  
severe infection, alcohol  
Initiate the hypoglycaemia protocol  
IMMEDIATELY (nurse administers per protocol;  
tell senior) (sugary drink if safe to swallow;  
sugar through the drip if nil by mouth); recheck  
in 10–15 min  
Low-normal in a  
diabetic  
4.0–5.9 (on  
insulin/sulfonylurea)  
Heading toward low Recheck within 30 min  
Target in  
surgery  
(diabetics)  
6.0–10.0 (6.0–12.0  
acceptable)  
Good control Check at least hourly while fasting  
Normal fasting  
(non-diabetic)  
3.9–5.5 Normal Carry on  
High 12.1–14.0 Stress, infection, missed medicines Treat per protocol (often insulin through a drip  
— set by anaesthetist/diabetes team)  
Very high Above 14.0 Risk of diabetic emergency (ketoacidosis) Check ketones; senior/diabetes team; S1  
usually deferred after review  
Does one abnormal result automatically diagnose something?  
A single high reading in someone not known to have diabetes may be stress. A random value of 11.1 or more  
with symptoms suggests undiagnosed diabetes — check HbA1c.  
What should the trainee check next?  
1\. If low: treat first, then think why.  
2\. If high: check ketones (5.11).  
3\. Check the diabetes medicine plan (Part 8).  
Where this comes from: Centre for Perioperative Care (CPOC) diabetes guideline 2021, updated 2023; Joint British Diabetes  
Societies.  
Clinician validation: Hospital hypoglycaemia and insulin-drip protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  

## 5.10 HbA1c (glycated haemoglobin)

What is it?  
Sugar sticks to haemoglobin in red cells. Because red cells live about 3 months, HbA1c shows the average  
sugar over the last 2–3 months — like a 'diabetes report card'. Given in mmol/mol (UK) or % (US).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 44*

Why do doctors check it?  
Badly controlled diabetes before a planned operation means more infections and complications. UK guidance  
suggests getting HbA1c below 69 mmol/mol (8.5%) before planned surgery where safe and possible CLINICAL  
REFERENCE — SOURCE BACKED .  
When should it NOT be ordered?  
Not for people without diabetes routinely; useless for emergency decisions (can't be fixed in hours).  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Below 42 mmol/mol  
(below 6.0%)  
No diabetes —  
Pre-diabetes 42–47 (6.0–6.4%) Higher risk of developing diabetes Note  
Diabetes,  
acceptable for  
planned surgery  
48–69 (6.5–8.5%) Diabetes, reasonably controlled Proceed with medicine plan  
Poorly  
controlled  
Above 69 (above 8.5%) High average sugar S1: discuss with anaesthetist/diabetes team —  
usually postpone and improve control. S2:  
proceed (can't wait), involve diabetes team  
Does one abnormal result automatically diagnose something?  
It can be falsely changed by anaemia, thalassaemia trait, recent blood transfusion or kidney disease  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Example patient — let's think it through says:  
S1 patient with type 2 diabetes. HbA1c 82 mmol/mol, finger-prick sugar 13.4. → "82 is above 69, so her diabetes has  
been poorly controlled for months, and today's sugar is high too. This is planned surgery, so it's safer to postpone, ask  
her GP/diabetes team to improve control, and rebook. I'll check ketones first to make sure she isn't becoming unwell."  
Where this comes from: CPOC 2021/2023; NICE NG45 (HbA1c within 3 months of surgery for people with diabetes).  

## 5.11 Ketones

What is it?  
Acids produced when the body burns fat because it can't use sugar (not enough insulin). Measured by  
finger-prick meter in mmol/L.  
Why do doctors check it?  
High ketones signal diabetic ketoacidosis (DKA), an emergency. Some diabetes tablets (SGLT2 inhibitors,  
e.g. dapagliflozin, empagliflozin) can cause DKA even when blood sugar looks almost normal.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Below 0.6 Normal —  
Raised 0.6–1.4 Starvation (fasting) or early problem Recheck; review diabetes plan  
High 1.5–2.9 Risk of DKA Senior/diabetes team  
DKA likely 3.0 or more (with acid in  
blood)  
Diabetic emergency Emergency DKA protocol; postpone surgery  
unless life-saving  
Where this comes from: Joint British Diabetes Societies DKA guideline.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 45*


## 5D — Liver tests (LFTs): the heart of S1

Teacher says:  
For the gallbladder scenario, liver tests are the most important blood tests, because a gallstone that slips into the bile  
duct shows up here first. I'll explain each test, then — more importantly — the patterns.  
LFTs = liver function tests. A set of measurements showing whether liver cells are damaged and whether  
bile is flowing freely. Indicated in all S1 patients because they have gallstone disease CLINICAL REFERENCE — SOURCE  
BACKED (NICE CG188).  

## 5.12 Bilirubin

What is it?  
A yellow pigment made when old red blood cells are recycled. The liver puts it into bile. If bile can't flow out  
(e.g. a stone blocks the bile duct), bilirubin backs up into the blood and the person turns yellow — jaundice.  
Measured in µmol/L (or mg/dL).  
Why do doctors check it?  
Raised bilirubin plus a widened bile duct strongly suggests a stone blocking the duct, which changes the S1  
plan.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal 3–21 µmol/L (0.2–1.2  
mg/dL)  
Bile flowing normally Carry on  
Mildly raised 22–30 Gilbert's syndrome (harmless inherited  
condition, worse when fasting), mild  
inflammation  
Check other liver tests; often harmless  
Raised 31–68 (1.8–4 mg/dL).  
Eyes usually look yellow  
above \~40–50  
Possible partial blockage, inflammation, liver  
disease  
Ultrasound of bile duct; senior; consider MRCP  
scan  
High Above 68 (above 4  
mg/dL)  
Likely blockage of the bile duct If bile duct is also wide (above 6 mm) → 'high  
risk' of a duct stone: senior + plan to clear duct  
(ERCP) before surgery  
Where this comes from: ASGE guideline on bile duct stones (2019); typical lab ranges.  

## 5.13 ALT and AST (liver cell enzymes)

What is it?  
ALT = alanine aminotransferase, AST = aspartate aminotransferase. Enzymes (chemical helpers) that  
live inside liver cells. When liver cells are damaged, they leak out into the blood. AST is also found in muscle.  
Why do doctors check it?  
Show liver-cell irritation or damage. A stone passing through the duct can cause a sudden, short spike.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal ALT about 7–40 U/L; AST  
about 10–40 U/L  
(lab-specific)  
Healthy liver cells Carry on  
Mild Up to 3 × upper limit  
(\~41–120)  
Fatty liver, alcohol, medicines, mild  
inflammation  
Repeat; history; usually proceed  
Moderate \~121–400 Inflammation, passing stone, hepatitis Senior; look at other tests and ultrasound  
Very high Above 400 (often  
300–1000 briefly with a  
passing stone)  
Sudden blockage/passing stone, viral  
hepatitis, drug injury  
Senior; repeat next day; MRCP if duct problem  
suspected  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 46*

Where this comes from: American College of Gastroenterology clinical guideline on abnormal liver chemistries (Kwo et al., 2017) —  
note it argues the TRUE healthy upper limit of ALT is lower (about 29–33 U/L in men, 19–25 U/L in women) than many lab ranges  
(often up to 40–55 U/L). ProcSense displays the hospital lab's range; the simulator treats values within that range as normal.  

## 5.14 ALP and GGT (bile duct enzymes)

What is it?  
ALP = alkaline phosphatase; GGT = gamma-glutamyl transferase. Enzymes from the cells lining the bile  
ducts. They rise when bile is blocked. ALP also comes from bone (and the placenta in pregnancy), so GGT is  
checked to confirm the ALP is coming from the liver. GGT also rises with alcohol.  
Why do doctors check it?  
A rise in ALP and GGT is the classic 'bile is blocked' signal.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal ALP \~30–130 U/L; GGT  
men below 60, women  
below 40  
Bile flowing Carry on  
Mildly raised ALP up to 2 × upper limit Mild blockage, medicines, bone conditions,  
pregnancy  
Check GGT and bilirubin  
Raised ALP 2–5 × upper limit  
(\~261–650)  
Bile duct blockage likely (stone, rarely tumour) Ultrasound + senior; MRCP  
Very raised ALP above 5 × upper limit Significant blockage Senior; duct imaging  
Where this comes from: ACG 2017; typical lab ranges.  

## 5.15 Albumin

What is it?  
A protein made by the liver that keeps fluid inside blood vessels.  
Why do doctors check it?  
Low albumin suggests long-term liver disease, poor nutrition or severe illness — wounds heal worse.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal 35–50 g/L Good —  
Low 25–34 Poor nutrition, long-term illness, inflammation Note; tell senior/anaesthetist  
Very low Below 25 Severe illness or liver disease Senior  

## 5.16 Reading liver tests as PATTERNS (most important)

Teacher says:  
Individual liver numbers are like single letters; patterns are words. Learn these five words.  
Medical word: Hepatocellular vs cholestatic  
Hepatocellular (hepato = liver, cellular = cells) pattern: ALT/AST rise much more than ALP — the liver cells are hurt.  
Cholestatic (chole = bile, stasis = standing still) pattern: ALP/GGT (and often bilirubin) rise more than ALT — the bile  
is stuck. Doctors can calculate an 'R ratio' = (ALT ÷ its upper limit) ÷ (ALP ÷ its upper limit): above 5 = liver-cell  
pattern; below 2 = bile-stuck pattern; 2–5 = mixed CLINICAL REFERENCE — SOURCE BACKED .  
Pattern Example numbers Plain meaning What the trainee does  
1\. All normal Bilirubin 12, ALT 25, ALP 80, GGT 30 Simple gallstones, nothing blocked Proceed (low risk of duct stone)  
2\. Bile stuck  
(cholestatic)  
Bilirubin 72, ALP 380, GGT 410, ALT  
160; ultrasound duct 9 mm  
A stone is probably blocking the  
main bile duct  
STOP the planned operation today;  
senior; MRCP or ERCP first  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 47*

Pattern Example numbers Plain meaning What the trainee does  
3\. Bile stuck + fever  
\+ shivers  
As above + temp 39, WBC 17 Infected blocked duct (cholangitis)  
— EMERGENCY  
Sepsis bundle, antibiotics, urgent duct  
drainage (ERCP), senior now  
4\. Short sharp ALT  
spike  
ALT 650 → 210 next day; ALP slightly  
up  
A stone probably passed through  
recently  
Senior; MRCP if still abnormal or duct  
wide  
5\. Inflammation +  
mild liver rise  
WBC 15, CRP 96, ALT 60, bilirubin 28 Inflamed gallbladder (acute  
cholecystitis); mild liver rise is  
common  
Acute pathway (Tokyo Guidelines);  
MRCP if bilirubin keeps rising  
6\. Bilirubin alone  
slightly up  
Bilirubin 35, everything else normal Probably Gilbert's syndrome  
(harmless)  
Proceed if confirmed; check for blood  
breakdown if anaemic  
Decision diagram — S1 liver tests  
Liver test results arrive  
Fever/shivers AND yellow skin/eyes AND right upper  
pain?  
Cholangitis — EMERGENCY: sepsis bundle,  
senior, urgent ERCP  
YES  
NO  
Stone seen in duct on scan, OR bilirubin above 68  
with duct wider than 6 mm?  
HIGH risk of duct stone: planned operation is  
usually deferred; senior; ERCP pathway  
YES  
NO  
Any liver test abnormal, OR age over 55, OR duct  
wide?  
INTERMEDIATE risk: senior; MRCP (or EUS) to  
look for a stone  
YES  
NO  
LOW risk → continue with planned operation  
In words: this follows the American Society for Gastrointestinal Endoscopy's 2019 risk tool CLINICAL REFERENCE —  
SOURCE BACKED . The European version is slightly different (it doesn't use age) — the surgeon chooses which  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . Note that age over 55 alone would push some  
patients into 'intermediate'; many surgeons don't act on age alone.  

## 5.17 Lipase / amylase (pancreas enzymes)

What is it?  
Enzymes from the pancreas. They leak into blood when the pancreas is inflamed (pancreatitis).  
Why do doctors check it?  
A gallstone can block the pancreas's exit and cause pancreatitis — severe upper tummy pain going through to  
the back, with vomiting. That changes the timing of surgery.  
When should it NOT be ordered?  
Not routine for a well S1 patient; order if upper-tummy pain is severe or diagnosis unclear.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Below the lab's upper  
limit (e.g. lipase below  
\~60 U/L)  
Pancreas fine —  
Raised 1–3 × upper limit Non-specific Interpret with symptoms  
Pancreatitis  
level  
More than 3 × upper limit Pancreatitis likely (with typical pain) Senior; pancreatitis pathway; planned op  
postponed  
Where this comes from: Revised Atlanta classification of acute pancreatitis (2012).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 48*


## 5E — Clotting tests and blood bank

Clotting = the blood's ability to form a plug and stop bleeding. If it's weak, surgery can bleed dangerously.  
Healthy patients don't need routine clotting tests CLINICAL REFERENCE — SOURCE BACKED (NICE NG45); they're for  
patients on blood thinners, with liver disease, sepsis, or a bleeding history.  

## 5.18 PT and INR

What is it?  
PT = prothrombin time: seconds for a blood sample to clot in the lab. INR = international normalised  
ratio: the PT converted into a standard number so labs everywhere agree. Normal INR is about 1. INR of 2  
means blood takes about twice as long to clot.  
Why do doctors check it?  
Patients on warfarin (a blood thinner) have deliberately high INR. Liver disease, long-standing bile blockage  
(vitamin K can't be absorbed) and sepsis can also raise it.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal INR 0.8–1.2 Normal clotting —  
Mildly raised 1.3–1.5 Liver problem, low vitamin K, early sepsis Check why; INR ≤1.5 is the commonly used  
target before surgery (e.g. day-before check  
after stopping warfarin) and ≤1.4 before  
spinal/epidural injections (Association of  
Anaesthetists/OAA/RA-UK 2013) CLINICAL  
INTERPRETATION  
Raised Above 1.5 (not on  
warfarin)  
Clotting impaired; also a 'severe' marker in  
gallbladder grading (TG18)  
Senior; correct before planned surgery  
On warfarin Usually 2–3 by design Intended effect Planned surgery: warfarin stopped 5 days  
before; check INR the day before (aim ≤1.5)  
Where this comes from: NICE NG45; TG18; ACC periprocedural anticoagulation pathway 2017.  
Clinician validation: Surgeon/anaesthetist to confirm acceptable INR CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  

## 5.19 APTT and fibrinogen

What is it?  
APTT = activated partial thromboplastin time: another clotting-speed test (a different part of the clotting  
chain). Fibrinogen: the protein that forms the clot's mesh.  
Why do doctors check it?  
Prolonged APTT: heparin (a blood thinner), inherited bleeding disorders (e.g. haemophilia), severe sepsis. Low  
fibrinogen: massive bleeding or sepsis using up clotting factors.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal APTT \~25–35 seconds  
(ratio 0.8–1.2); fibrinogen  
\~1.5/2.0–4.0 g/L  
Normal —  
Abnormal APTT ratio above 1.2;  
fibrinogen below 1.5  
Bleeding risk Repeat; senior/haematology  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 49*


## 5.20 Group and save / crossmatch (blood bank)

Group and save (G&S): the lab finds the patient's blood group (A, B, AB, O; Rh positive/negative) and checks  
for unusual antibodies, then keeps the sample so blood can be provided quickly if needed. Crossmatch:  
specific bags of blood are tested against the patient's blood and reserved. Which operations need these is set  
by each hospital's blood-ordering schedule CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Usually needed: suspected bleeding pregnancy, active bleeding, very low Hb, or emergency patients on blood  
thinners CLINICAL INTERPRETATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 50*


## 5F — Pregnancy test and urine tests


## 5.21 Pregnancy test (β-hCG)

What is it?  
β-hCG = beta human chorionic gonadotropin, a hormone made from early pregnancy. Urine test: a strip  
turns positive in about 5 minutes. Blood test: gives an exact level in IU/L (international units per litre).  
Why do doctors check it?  
Pregnancy changes everything: some medicines and X-rays/CT can harm a baby; anaesthesia is managed  
differently; planned surgery is usually postponed; and in a woman with tummy pain, a positive test raises the  
possibility of an ectopic pregnancy (a pregnancy growing outside the womb, usually in a tube) which can  
rupture and bleed massively.  
How the decision is made (v5 correction — no automatic age rule): (1) decide whether pregnancy is  
possible for this patient (could they be pregnant? last period, contraception, sexual activity, recent pregnancy);  
(2) ask privately and sensitively; (3) if there is any possibility or doubt, offer a pregnancy test and do it only  
with consent; (4) document the discussion; (5) if the result or uncertainty changes the plan, tell the senior and  
anaesthetist CLINICAL REFERENCE — SOURCE BACKED (NICE NG45: on the day of surgery, sensitively ask whether there  
is any possibility of pregnancy; with consent, test if there is any doubt). Some institutions also use age bands in  
their protocol CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION , but ProcSense teaches the  
clinical question, not an age rule.  
Why pregnancy changes management: diagnosis (ectopic pregnancy and ovarian causes must be  
excluded in pain); imaging (ultrasound and MRI preferred; avoid ionising radiation where possible); anaesthesia  
(aspiration risk rises from about the second trimester, left-lateral tilt from \~20 weeks, fetal considerations);  
medicines (e.g. NSAIDs avoided); timing of planned surgery (usually postponed; second trimester preferred if  
surgery is needed — SAGES 2017); and the team (obstetric input).  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Negative (blood) Below 5 IU/L Not pregnant Continue  
Uncertain  
(blood)  
5–25 IU/L Very early pregnancy, or recently ended  
pregnancy  
Repeat in 48 hours; treat as possibly pregnant  
meanwhile; senior  
Positive Above 25 IU/L / urine strip  
positive  
Pregnant S1: planned operation is usually deferred  
(senior, anaesthetist and obstetric advice). S2  
with pain: ectopic pregnancy must be  
considered as a differential diagnosis →  
gynaecology urgently if suspected  
Positive + pain  
\+ low BP  
Positive test with shock  
signs  
Possible ruptured ectopic — internal bleeding EMERGENCY: call for help, 2 drips, crossmatch  
blood, gynaecology now  
Does one abnormal result automatically diagnose something?  
A urine test can be falsely negative very early (first days after conception) or if urine is very dilute. It can stay  
positive for some weeks after a miscarriage.  
What should the trainee check next?  
1\. Ask the date of her last period (LMP), contraception, any vaginal bleeding.  
2\. Tell senior; in S2 call gynaecology.  
3\. Avoid CT and certain medicines until clarified; ultrasound is safe.  
4\. If she declines the test: document the discussion; senior and anaesthetist decide; treat as possibly  
pregnant.  
Where this comes from: NICE NG45; NICE NG126 Ectopic pregnancy and miscarriage (2019, updated 2023); SAGES laparoscopy in  
pregnancy (2017).  
Clinician validation: Hospital pregnancy-testing policy and age band CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 51*


## 5.22 Urine dipstick (urinalysis)

What is it?  
A paper strip with chemical squares dipped in urine; colours change in 1–2 minutes showing white cells  
(leucocytes), nitrites (made by many urine bacteria), blood, sugar, ketones, protein and bilirubin.  
Why do doctors check it?  
In S2 it helps spot look-alike conditions: urine infection (leucocytes + nitrites) and kidney stones (blood). Not a  
routine pre-operative test in S1 CLINICAL REFERENCE — SOURCE BACKED .  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal All negative Nothing in urine —  
Leucocytes +  
nitrites  
Positive Urine infection likely Send urine culture; but keep thinking —  
appendicitis can coexist  
Leucocytes only Positive, nitrite negative Can be caused by an inflamed appendix lying  
next to the bladder/ureter!  
Do NOT conclude 'urine infection' in lower-right  
pain  
Blood Positive Kidney stone, infection, period Consider kidney stone (CT KUB = CT of kidneys,  
ureters, bladder)  
Glucose /  
ketones  
Positive Diabetes (or SGLT2 medicine) / starving or  
diabetic emergency  
Finger-prick sugar and ketones  
Where this comes from: NICE NG45; WSES 2020.  
Urine culture: urine is sent to grow and identify bacteria (takes 24–48 h). Order if the dipstick suggests  
infection, in pregnancy, or in sepsis without an obvious source.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 52*


## 5G — Emergency and special tests


## 5.23 Lactate (and blood gas)

What is it?  
Lactate is produced when cells don't get enough oxygen and switch to 'emergency power'. Measured in  
mmol/L, usually on a blood gas machine (VBG = venous blood gas from a vein, ABG = arterial blood gas from  
an artery), result in about 5 minutes. A blood gas also shows acidity (pH; normal 7.35–7.45) and bicarbonate.  
Why do doctors check it?  
A high lactate means organs are short of oxygen — a key sign of sepsis or shock.  
Number zones  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Normal Below 2.0 Circulation coping —  
Raised 2.0–3.9 Poor circulation/sepsis concern Sepsis check; fluids; repeat within 2–4 h  
Severe 4.0 or more Severe hypoperfusion (organs starved) EMERGENCY sepsis/shock management; senior  
\+ anaesthetist  
Where this comes from: Surviving Sepsis Campaign 2021.  

## 5.24 Blood cultures

Bottles of blood sent to grow bacteria — to find which germ is in the bloodstream and which antibiotic will work.  
Taken when sepsis is suspected or with fever and shivering, ideally before antibiotics, but antibiotics must not  
be delayed to get them CLINICAL REFERENCE — SOURCE BACKED . Results take 24–72 hours.  

## 5.25 Troponin

A protein released from damaged heart muscle. Ordered only if there is chest pain, a worrying ECG or an  
unexplained fast abnormal rhythm — not routinely. A raised troponin before planned surgery → postpone and  
heart (cardiology) review CLINICAL REFERENCE — SOURCE BACKED (ESC 2022).  

## 5.26 Magnesium, calcium, iron, other local tests

Test Plain meaning Typical reference range  
CLINICAL REFERENCE —  
SOURCE BACKED (Pathology  
Harmony where  
harmonised)  
Why it may matter  
Magnesium Salt that works with potassium 0.7–1.0 mmol/L Low magnesium makes low potassium hard to  
fix and causes rhythm problems  
Calcium (adjusted) Salt for nerves, muscles, heart 2.2–2.6 mmol/L Abnormal in pancreatitis, some cancers; very  
low/high is dangerous  
Ferritin Iron stores Above 30 µg/L (higher if  
inflamed)  
Confirms iron deficiency in anaemic S1 patients  
Sickle/thalassaemia  
screen  
Inherited blood conditions Negative Anaesthetic planning CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION  
Hepatitis B/C, HIV Blood-borne viruses Negative Some institutions test before surgery  
CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION ; results  
should not delay emergency surgery; standard  
infection precautions apply to everyone  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 53*


## 5.27 Dangerous ('critical') results the lab will phone about CLINICAL REFERENCE —

SOURCE BACKED (biochemistry) / CLINICAL INTERPRETATION (haematology) CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
Test Phone the doctor if Why it's dangerous  
Potassium 2.5 or below, or 6.5 or above Heart rhythm/arrest  
Sodium 120 or below, or 160 or above Brain swelling/shrinking, fits  
Glucose 2.5 or below Brain starved of sugar  
Calcium 1.8 or below, or 3.5 or above Heart and nerves  
Creatinine 354 or above (without known kidney failure) Severe kidney failure  
CRP 300 or above Severe inflammation/infection  
Haemoglobin Below 70 g/L, or a fall of 20+ Bleeding/severe anaemia  
Platelets Below 30 Bleeding risk  
Neutrophils Below 0.5 Can't fight infection  
INR 5 or above on warfarin; above 1.5 unexpectedly Bleeding risk  
Lactate 4.0 or above Shock  
Where this comes from: Modelled on Royal College of Pathologists 'The communication of critical and unexpected pathology results'  
(2017); haematology values are provisional.  

## 5.28 When results are confusing — the trainee's playbook

Situation What it looks like What the trainee should do  
Borderline result Potassium 5.4; Hb 118 in a woman; CRP 9 Interpret with the whole picture; repeat only if it would change  
what you do  
Result doesn't match  
the patient  
Very high potassium in a well person; very low  
platelets in a well person  
Check for lab comments, repeat (different tube if platelets),  
examine again  
Results conflict Normal WBC and CRP but classic appendix pain  
after 10 hours  
Trust the clinical picture enough to keep investigating; repeat  
bloods/score in 6–12 h; imaging  
Unexpected result High sugar in someone not known diabetic Repeat; HbA1c; tell team  
Delayed result Still 'pending' long after expected Phone the lab; continue other tasks; in S2 do not delay  
life-saving treatment while waiting for it (senior judgement)  
Mislabelled / rejected  
sample  
Lab rejects sample Re-take with bedside labelling; report the incident  
Critical result phone call Lab calls Confirm patient identity, repeat the value back, act now,  
document, tell senior  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 54*


# Part 6 — Heart tracing (ECG) and scans, taught

from zero  

## 6.1 ECG (electrocardiogram)

What is it?  
ECG = electrocardiogram: a test that records the heart's electrical activity. Every heartbeat is triggered by a  
small electrical wave. Ten sticky pads (electrodes) on the chest, arms and legs pick it up, and a machine prints  
a wavy line — the 'heart tracing'. It takes about 2 minutes and doesn't hurt.  
A '12-lead ECG' looks at the heart from 12 angles. A continuous 'rhythm strip' on the monitor shows the  
heartbeat live but in less detail.  
What do the bumps mean? (just enough)  
- P wave — small bump: the top chambers (atria) squeeze.  
- QRS complex — tall spike: the main pumping chambers (ventricles) squeeze.  
- T wave — rounded bump: the ventricles recharge. Its shape changes with potassium.  
- Normal = 'sinus rhythm': a P before every QRS, evenly spaced, 60–100 per minute.  
Why is it ordered before surgery?  
To catch heart rhythm problems or heart strain that would make anaesthesia dangerous. UK guidance:  
consider for patients with heart, kidney or diabetes problems, and older patients having major surgery CLINICAL  
REFERENCE — SOURCE BACKED (NICE NG45). In ProcSense also: irregular pulse, chest pain, palpitations, fainting, very  
fast heart rate, abnormal potassium.  
What normal numbers appear on the printout?  
- Rate 60–100 per minute.  
- PR interval (time from top chambers to bottom chambers) 120–200 ms (milliseconds).  
- QRS (width of the main spike) roughly 80–100 ms in healthy adults; 120 ms or more is called a 'wide QRS' (a  
conduction delay) CLINICAL INTERPRETATION .  
- QTc (the 'recharge' time corrected for heart rate): AHA/ACCF/HRS 2009 treats 450 ms or more in men and  
460 ms or more in women as prolonged, and 500 ms or more as markedly prolonged (higher risk of a  
dangerous rhythm) CLINICAL REFERENCE — SOURCE BACKED . Some hospital protocols use higher limits (about 470  
ms men / 480 ms women) in inpatients CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
How ECG numbers are used (v5.1)  
The PR (120–200 ms) and QRS values are standard educational reference values CLINICAL INTERPRETATION ; the QTc  
values come from AHA/ACCF/HRS 2009 (Rautaharju et al., Part IV) CLINICAL REFERENCE — SOURCE BACKED . No single  
interval decides whether surgery proceeds. The trainee reports the finding; the anaesthetist/senior interprets it  
with the patient, symptoms, potassium/magnesium, medicines and urgency (an emergency may proceed with a  
prolonged QTc once risks are managed).  
What the trainee sees on the  
ECG  
Plain meaning S1 (planned) S2 (emergency)  
Fast but regular (sinus  
tachycardia, above 100)  
Normal rhythm, just fast — the body is  
stressed  
Find the reason;  
anaesthetist/senior decide  
whether to proceed once  
explained  
Expected with pain/fever; treat  
cause  
Irregularly irregular, no P  
waves = atrial fibrillation  
(AF)  
The top chambers quiver chaotically; the  
heartbeat becomes random. Can be  
triggered by infection, dehydration, salts,  
thyroid, alcohol, heart disease. Raises  
stroke risk  
New AF → usually deferred  
pending medical/anaesthetic  
review; medical team for rate  
control and blood-thinner  
decision  
Treat triggers (fluids,  
antibiotics, potassium); slow  
the rate if above \~110 per  
senior/anaesthetist; operate  
when stable  
Known AF, rate below  
100–110  
Long-standing, controlled Proceed; blood-thinner plan  
(Part 8)  
Proceed  
ST elevation (a raised  
segment after the QRS) with  
chest pain  
Heart attack happening now (STEMI) EMERGENCY — heart team;  
planned surgery is deferred by  
the senior/anaesthetist  
EMERGENCY — heart team;  
surgery only if life-saving, by  
senior decision  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 55*

What the trainee sees on the  
ECG  
Plain meaning S1 (planned) S2 (emergency)  
New ST depression / inverted  
T waves  
Heart muscle short of oxygen (or low  
potassium)  
Troponin blood test; medical  
review; senior/anaesthetist  
decide whether planned  
surgery proceeds  
Troponin; anaesthetist/heart  
team  
Heart block (P waves not  
followed by QRS)  
Electrical signal getting stuck — heart  
may stop under anaesthesia  
Heart team (may need  
pacemaker); planned surgery is  
usually deferred by  
senior/anaesthetist  
Urgent anaesthetist + heart  
team  
Tall pointed T waves, wide  
QRS  
Very high potassium Emergency potassium  
treatment  
Same  
QTc 500 ms or more Markedly prolonged: higher risk of a  
dangerous rhythm; some sickness  
medicines and antibiotics worsen it  
Check potassium/magnesium  
and medicines; anaesthetist  
reviews  
Same, in parallel  
Teacher says:  
The simulator doesn't need to turn the trainee into a heart specialist. They need to recognise: normal, fast, irregular  
(AF), heart attack pattern, and potassium pattern — and know who to call.  
Where this comes from: NICE NG45; ESC 2022 Guidelines on non-cardiac surgery; ESC 2024 AF guideline; Rautaharju et al.,  
AHA/ACCF/HRS recommendations for standardization and interpretation of the ECG, Part IV, Circulation 2009 (QTc).  
Decision diagram — abnormal ECG  
ECG done  
Chest pain with ST elevation, OR heart block, OR  
potassium pattern?  
EMERGENCY — call senior/medical/heart team  
now  
YES  
NO  
Irregular rhythm (AF) that is NEW?  
S1: medical/anaesthetic review — surgery  
usually deferred. S2: treat triggers, rate control  
per senior/anaesthetist; surgery proceeds when  
judged safe  
YES  
NO  
Any other new abnormality (ST depression, QTc 500  
ms or more)?  
Tell anaesthetist/senior; troponin; check salts  
and medicines; they decide whether S1  
proceeds  
YES  
NO  
Normal or known stable findings → continue  

## 6.2 Imaging (scans) — the basics

Scan How it works (simple) Good for Downsides  
Ultrasound (US) A handheld probe sends sound waves into  
the body; echoes make a live picture.  
Same machine as pregnancy scans.  
Gallstones, gallbladder inflammation,  
bile duct width; appendix in thin/young  
patients; pregnancy; ovaries  
No radiation (safe in pregnancy).  
Depends on operator skill; harder in  
larger patients; the appendix is  
often not seen  
CT (computed  
tomography)  
A rotating X-ray scanner takes hundreds  
of pictures to build 3-D slices. Often with  
dye ('contrast') injected into a vein.  
Appendicitis in adults when unclear;  
burst appendix/abscess; other causes  
of pain; older patients  
Radiation (avoid in pregnancy if  
possible); dye can harm weak  
kidneys and cause allergy — check  
kidney blood test and allergies first  
MRI (magnetic  
resonance  
imaging)  
A large magnet and radio waves make  
detailed pictures. No radiation.  
Appendicitis in pregnancy when  
ultrasound is unclear  
Slow, noisy, tight tube; not with  
some metal implants; availability  
CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 56*

Scan How it works (simple) Good for Downsides  
MRCP (magnetic  
resonance cholang  
iopancreatography  
)  
A special MRI showing the bile ducts and  
pancreatic duct as bright lines.  
Finding stones in the bile duct without  
any procedure  
Same as MRI; may not be available  
same day CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  
EUS (endoscopic  
ultrasound)  
A camera tube with an ultrasound probe  
passed through the mouth into the gut.  
Alternative to MRCP for duct stones Needs sedation; invasive  
ERCP (endoscopic  
retrograde cholang  
iopancreatography  
)  
A camera tube through the mouth to the  
opening of the bile duct; X-ray dye shows  
the duct; instruments can pull stones out.  
Treating duct stones and draining  
infected ducts  
A procedure, not just a scan: can  
cause pancreatitis (\~3–5%),  
bleeding, perforation  
Chest X-ray A single X-ray picture of the chest. Breathing problems, inhaled vomit,  
free air under the diaphragm (burst  
gut)  
Not a routine pre-op test CLINICAL  
REFERENCE — SOURCE BACKED  

## 6.3 Gallbladder ultrasound results (S1) — what each means and what

happens next  
Report says Plain meaning What happens next  
Gallstones; thin gallbladder wall (3 mm or  
less); no fluid; bile duct 6 mm or less  
Simple gallstones Proceed (if liver tests normal)  
Stones + wall thicker than 4 mm, fluid  
around gallbladder, tender when the probe  
presses ('sonographic Murphy's sign'),  
gallbladder enlarged (long axis over 8 cm,  
short axis over 4 cm), stone stuck in its neck  
(Tokyo Guidelines imaging criteria)  
Inflamed gallbladder (acute  
cholecystitis)  
Cancel planned list → acute pathway: antibiotics,  
senior, early operation usually within days  
Bile duct wider than 6 mm (older people and  
people who've had gallbladder surgery may  
normally be a bit wider)  
Possible blockage of the duct Check bilirubin; MRCP/ERCP; senior  
Stone seen in the bile duct Duct stone (high risk) ERCP pathway; planned operation is usually deferred  
Gas in the wall, pus pocket, gallbladder burst Severe complicated inflammation Urgent senior; CT; maybe drainage  
Polyp 10 mm or larger, mass, calcified wall Possible gallbladder cancer Senior; further imaging; consent changes  
No stones, only 'sludge' Diagnosis less certain Senior reviews whether operation is right  
Where this comes from: NICE CG188; Tokyo Guidelines 2018; ASGE 2019.  

## 6.4 Appendix scan results (S2) — what each means and what happens

next  
Report category What the radiologist saw Plain meaning What happens next  
Positive — simple Appendix swollen (wider than 6 mm), won't  
squash under the probe, inflamed fat around  
it, maybe a hard stone inside (appendicolith)  
Appendicitis Senior decision; antibiotics; consent;  
theatre (timing set by the surgeon:  
generally prompt; WSES 2025  
recommends within 24 h of admission  
when surgery is indicated)  
Positive —  
complicated  
Hole/gas outside the appendix, pus pocket  
(abscess), inflammatory lump, fluid  
Burst/complicated  
appendicitis  
Urgent surgery; big abscess may be  
drained by needle first per senior  
CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION ;  
longer antibiotics  
Negative —  
something else found  
Normal appendix; e.g. ovarian cyst, kidney  
stone, inflamed bowel  
Not appendicitis Switch to that pathway; senior  
Non-diagnostic /  
equivocal  
'Appendix not seen'; borderline size; no other  
signs  
We still don't know —  
this is NOT the same as  
negative  
Re-score; CT (or MRI if pregnant) or admit  
and re-examine in 6–12 h; senior  
Normal appendix,  
nothing else  
Appendix seen and normal Appendicitis unlikely Senior decides observe or discharge with  
safety advice  
Classic beginner error to build into the game  
Treating 'appendix not visualised' as 'appendix normal'. The correct trainee response is: "The scan didn't answer the  
question — what's my next step?"  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 57*

Which scan for which S2 patient? CLINICAL REFERENCE — SOURCE BACKED (WSES 2020 and 2025 editions) —  
local scanner availability CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
- Young people, women, pregnant women: ultrasound first.  
- Adults where ultrasound is unclear: CT (low-dose is acceptable).  
- Pregnant with unclear ultrasound: MRI.  
- Older adults (over \~50): CT preferred, because bowel cancer or diverticulitis (inflamed pouches in the bowel) can  
mimic appendicitis.  
- Very high score in a young man: guidelines allow going to surgery without a scan in selected cases, but many  
hospitals still scan.  
Where this comes from: Di Saverio et al., WSES Jerusalem guidelines 2020; ACR Appropriateness Criteria.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 58*


# Part 7 — Scoring systems, taught from zero

Teacher says:  
A 'score' is a recipe that turns several findings into one number, so different doctors reach consistent decisions.  
Scores support judgement; they don't replace it.  

## 7.1 Appendicitis scores (S2): Alvarado, AIR and AAS

Why use a score?  
Lower-right tummy pain has many causes. A score estimates how likely appendicitis is, and therefore whether  
the patient needs a scan, observation, or a surgical decision.  
Medical word: The three scores  
Alvarado score (also called MANTRELS, from its items) — the oldest (1986), 0–10 points. AIR = Appendicitis  
Inflammatory Response score (2008), 0–12 points, includes CRP. AAS = Adult Appendicitis Score (2014), 0–23  
points, designed for adults, uses age/sex and timing.  
What is scored Alvarado AIR AAS  
Pain moved from belly button to  
lower right  
1 — 2  
Pain in lower right — 1 2  
Loss of appetite 1 — —  
Nausea or vomiting 1 1 (vomiting only) —  
Lower-right tenderness when  
pressed  
2 — Women aged 16–49: 1. Everyone else: 3  
Rebound (pain when pressure is  
released) / guarding (tummy  
muscles tense)  
Rebound: 1 Light 1, medium 2, strong 3 Guarding: mild 2, moderate/severe 4  
Temperature 37.3 °C or more: 1 38.5 °C or more: 1 —  
White cell count (×109/L) Above 10.0: 2 10.0–14.9: 1; 15.0 or more: 2 7.2–10.8: 1; 10.9–13.9: 2; 14.0 or more: 3  
Neutrophils (% of white cells) More than 75%: 1 70–84%: 1; 85% or more: 2 62–74.9%: 2; 75–82.9%: 3; 83% or more:  
4  
CRP (mg/L) — 10–49: 1; 50 or more: 2 If pain under 24 h: 4–10.9 = 2; 11–24.9 =  
3; 25–82.9 = 5; 83 or more = 1. If pain 24  
h or more: 12–52.9 = 2; 53–151.9 = 2;  
152 or more = 1  
Maximum 10 12 23  
Meaning 0–4 unlikely; 5–6 unsure;  
7–8 probable; 9–10 very  
probable  
0–4 low; 5–8 unsure; 9–12 high 0–10 low; 11–15 intermediate; 16+ high  
Why does AAS give fewer points for a very high CRP? Because extremely high CRP early on is more typical of  
other illnesses, and the score was built from real data — it's counter-intuitive but intentional.  
Score Good at Weak at  
Alvarado Ruling OUT appendicitis when low; no CRP needed Should not be used alone to confirm appendicitis; over-calls  
in women  
AIR Simple; includes CRP; shown in trials to reduce unnecessary  
scans and operations  
'Strength' of rebound is subjective  
AAS Very accurate in comparisons; identifies low-risk patients  
well  
More complex; counter-intuitive CRP bands  
Worked example — calculate both for the same patient  
Patient S2-A: man, 19; pain moved to lower right 20 hours ago; vomited once; lower-right tenderness; mild  
guarding; medium rebound; temperature 37.9; WBC 13.6; neutrophils 82%; CRP 38.  
Item AIR points AAS points  
Vomiting 1 —  
Lower-right pain 1 2  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 59*

Item AIR points AAS points  
Pain moved — 2  
Tenderness (male) — 3  
Rebound medium / guarding mild 2 2  
Temperature 37.9 0 (needs 38.5) —  
WBC 13.6 1 2  
Neutrophils 82% 1 3  
CRP 38 (under 24 h) 1 5  
TOTAL 7 → 'unsure' (indeterminate) 19 → 'high'  
Teacher says:  
Notice the two scores disagree! AIR says 'get a scan'; AAS says 'high probability'. That's a great teaching moment and  
exactly why the surgeon must choose which score ProcSense uses. My draft suggestion is AIR as the default  
(simpler for trainees, backed by trials), with AAS optional CLINICAL INTERPRETATION CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION . The WSES guideline recommends AIR and AAS over Alvarado for adults CLINICAL  
REFERENCE — SOURCE BACKED .  
Decision diagram — what the appendicitis score changes  
Calculate AIR (or AAS) once blood results are back  
Pregnancy test positive?  
Differential diagnosis: ectopic pregnancy must  
be considered → gynaecology (the appendicitis  
score no longer applies)  
YES  
NO  
Low score (AIR 0–4 / AAS 0–10)? Appendicitis unlikely → look for other causes;  
senior decides observe/discharge  
YES  
NO  
Intermediate (AIR 5–8 / AAS 11–15)? Scan: ultrasound first; CT/MRI if unclear; or  
observe and re-score in 6–12 h  
YES  
NO  
High (AIR 9–12 / AAS 16+) → senior surgical decision;  
scan per local policy; antibiotics; consent  
Where this comes from: Alvarado 1986; Andersson & Andersson 2008; Sammalkorpi et al. 2014; WSES 2020; BMJ Best Practice.  

## 7.2 Tokyo Guidelines 2018 (TG18) — is the gallbladder inflamed, and

how badly? (S1)  
Medical word: TG18  
An international guideline (Tokyo Guidelines, 2018 version) for diagnosing and grading acute cholecystitis  
(inflamed gallbladder) and acute cholangitis (infected bile duct).  
Group What to look for  
A — local signs Murphy's sign (patient catches their breath in pain when the examiner presses under the right ribs while they  
breathe in); lump/pain/tenderness in the right upper tummy  
B — whole-body signs Fever; raised CRP; raised WBC  
C — scan Ultrasound/CT shows inflamed gallbladder  
Diagnosis Suspected = one from A + one from B. Definite = A + B + C.  
Grade Rule Plain meaning  
Grade III (severe) Any organ failing: BP needs medicine support; reduced consciousness;  
poor oxygen; low urine or creatinine above 2.0 mg/dL (\~177 µmol/L); INR  
above 1.5; platelets below 100  
Life-threatening — intensive care,  
antibiotics, drainage  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 60*

Grade Rule Plain meaning  
Grade II (moderate) Any of: WBC above 18; tender lump in right upper tummy; symptoms  
more than 72 hours; severe local inflammation on scan  
Harder operation; experienced surgeon;  
early surgery if fit  
Grade I (mild) Neither of the above Early keyhole operation  

## 7.3 Tokyo Guidelines — infected bile duct (cholangitis)

Group What to look for (TG18 numbers)  
A — inflammation Fever above 38 °C and/or shaking chills; WBC below 4 or above 10; CRP 10 mg/L or more  
B — bile blocked Jaundice (bilirubin 2 mg/dL ≈ 34 µmol/L or more); liver tests above 1.5 × upper limit  
C — scan Widened bile duct; cause seen (stone, narrowing)  
Diagnosis Definite = A + B + C; suspected = A + (B or C) → EMERGENCY: sepsis care + urgent drainage (ERCP)  
Where this comes from: Yokoe et al. and Kiriyama et al., Tokyo Guidelines 2018, J Hepatobiliary Pancreat Sci 2018.  

## 7.4 ASA physical status — 'how fit is this patient for anaesthesia?'

Medical word: ASA  
American Society of Anesthesiologists physical-status grade. A 1-to-6 scale the anaesthetist uses to describe  
how healthy the patient is before anaesthesia. An 'E' is added for emergencies (e.g. 'ASA 1E').  
Grade Meaning Examples  
ASA 1 Healthy Fit, non-smoker, little alcohol  
ASA 2 Mild disease, no real limits Smoker, social drinker, pregnant, BMI 30–40, well-controlled diabetes or BP  
ASA 3 Severe disease, limits activity Poorly controlled diabetes/BP, lung disease (COPD), BMI 40+, dialysis, old  
heart attack/stroke (over 3 months ago), pacemaker  
ASA 4 Severe disease, constant threat to life Recent heart attack/stroke (under 3 months), sepsis, severe heart valve  
disease  
ASA 5 Not expected to survive without the operation Ruptured major artery, massive trauma  
ASA 6 Brain-dead organ donor —  
\+ E Emergency operation All S2 patients  
Expected grades for ProcSense patients CLINICAL INTERPRETATION (derived from the ASA 2020 examples): healthy  
S1 = ASA 1; S1 with controlled BP or diabetes = 2; poorly controlled diabetes, AF on blood thinners, BMI 40+ =  
3; healthy S2 = 1E; septic S2 = 2E–4E. The anaesthetist character confirms; doctors often disagree slightly on  
ASA CLINICAL REFERENCE — SOURCE BACKED .  
Where this comes from: American Society of Anesthesiologists, ASA Physical Status Classification System (2020).  

## 7.5 Airway and sleep-apnoea checks (for the anaesthetist)

Before putting someone to sleep, the anaesthetist checks how easy it will be to put a breathing tube in. The  
trainee should do a quick look and pass it on.  
Check How Normal Concerning  
Mallampati class Patient opens mouth wide and sticks tongue  
out; what can you see?  
Class 1–2 (uvula — the dangly  
bit at the back — visible)  
Class 3–4 (only base of uvula or  
only roof of mouth)  
Mouth opening Gap between front teeth 3 cm or more (\~2–3 fingers) Less than 3 cm  
Chin-to-neck distance  
(thyromental)  
Chin tip to Adam's apple, head tilted back 6.5 cm or more Less than 6 cm  
Neck movement Look up / down Full Stiff  
STOP-BANG (sleep  
apnoea)  
8 yes/no questions: Snoring, Tired, Observed  
stopping breathing, high blood Pressure, BMI  
above 35, Age above 50, Neck above 40 cm,  
male Gender  
0–2 low risk 3–4 intermediate; 5–8 high risk  
Medical word: Obstructive sleep apnoea (OSA)  
The throat repeatedly collapses during sleep, stopping breathing for seconds. Anaesthetic and strong painkillers  
make it worse, so the anaesthetist needs to know.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 61*


## 7.6 Blood-clot (VTE) risk

Medical word: VTE (venous thromboembolism)  
Blood clots in the deep leg veins (DVT, deep vein thrombosis) that can break off and block the lungs (PE, pulmonary  
embolism) — potentially fatal. Surgery and lying still raise the risk.  
Every admitted patient needs a clot-risk and bleeding-risk assessment CLINICAL REFERENCE — SOURCE BACKED (NICE  
NG89). Risk factors include: age over 60, BMI over 30, cancer, previous clot, family history of clots, oestrogen  
pill or HRT, pregnancy, dehydration, reduced mobility, surgery longer than 90 minutes, acute tummy  
inflammation. Bleeding risk factors: active bleeding, blood thinners, platelets below 75, very high BP.  
Prevention: compression stockings/devices and/or a blood-thinning injection after surgery (Part 8).  

## 7.7 Overall surgical risk tools

NICE quality standard QS216 asks hospitals to use a validated risk tool. Options: SORT (Surgical Outcome Risk  
Tool, simple), P-POSSUM, ACS NSQIP online calculator. NELA (for emergency laparotomy) does not apply to  
appendectomy. Draft choice: SORT CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION (NICE  
QS216 requires a validated tool but does not name one).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 62*


# Part 8 — Medicines and anaesthesia, taught from

zero  

## 8.1 Why doses vary — and why this book mostly won't give them

The right dose of many medicines depends on the patient's weight, age, kidney and liver function, other  
medicines, allergies, how unwell they are, and how they respond to the first dose. Giving one number for  
'everyone' could be dangerous. So:  
Role convention used throughout this book (v5)  
The trainee identifies the clinical problem, recognises that a medicine may be needed, communicates it,  
and initiates the appropriate workflow (e.g. asks the senior/anaesthetist, flags it on the drug chart for  
confirmation). A qualified clinician confirms the final treatment decision and dose; a nurse or clinician  
administers. In the words of the curriculum: the trainee identifies the need, communicates the finding and initiates  
the appropriate escalation/workflow; the responsible clinician confirms the medication, dose, route and indication,  
and the medication is administered by the appropriate clinical professional. Where this book says 'give', 'treat' or  
'replace' in a trainee column, read it as 'recognise and initiate the workflow for'. The trainee never independently  
prescribes, selects a dose for, or administers a patient-specific medicine.  
- PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED — the simulator shows the decision (e.g. 'a strong painkiller  
titrated by the clinician') but not a number.  
- EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION — shown only where an authoritative  
source gives a standard adult dose (named beside it); the source applies to typical adults, so weight,  
kidney/liver function, allergy, pregnancy and other medicines can change the real dose. Display is for  
teaching only and is never an instruction.  
Medical word: Titrate  
Give a small amount, watch the effect, give more if needed — like slowly turning up a volume knob.  

## 8.2 Anaesthesia from zero

What is anaesthesia and why is it needed?  
Cutting into the body is extremely painful and the muscles tense up. Anaesthesia removes pain and awareness  
so the operation can be done safely. General anaesthesia = the patient is made deeply unconscious with  
medicines (through the drip and/or as a gas), feels nothing and remembers nothing, then wakes up when the  
medicines are stopped.  
What is intubation?  
Under general anaesthesia the patient's breathing reflexes are switched off and the throat muscles relax. The  
anaesthetist puts a plastic breathing tube through the mouth into the windpipe (intubation) and a machine  
(ventilator) breathes for the patient. In keyhole surgery, the gas pumped into the tummy pushes on the lungs,  
which is another reason the machine controls breathing.  
Why does a full stomach matter?  
When asleep, the patient can't protect their airway. Stomach contents can come back up and go into the lungs  
(aspiration), causing severe pneumonia. That's why fasting matters (Part 9). For emergency patients who may  
have eaten, the anaesthetist uses RSI (rapid sequence induction): lots of oxygen first, the sleep medicines  
and tube put in very quickly, with suction ready, to minimise the time the airway is unprotected.  
Why does the anaesthetist monitor the patient, and what is monitored?  
Anaesthetic drugs affect breathing, heart and blood pressure, and the patient can't tell anyone if something is  
wrong. International standards require continuous monitoring CLINICAL REFERENCE — SOURCE BACKED (WHO-WFSA  
2018; Association of Anaesthetists 2021):  
Monitor What it shows Why  
Oxygen saturation (pulse  
oximeter)  
% of blood carrying oxygen Detects low oxygen within seconds. WHO checklist won't allow  
anaesthesia without a working one  
Blood pressure (cuff, every few  
minutes)  
Circulation Anaesthetic drugs lower BP  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 63*

Monitor What it shows Why  
ECG (heart rhythm) Heartbeat pattern Detects dangerous rhythms  
Capnography (carbon dioxide) CO2 in each breath out, as a wave Proves the tube is in the windpipe and the patient is  
breathing/ventilated  
Temperature Body heat Patients get cold under anaesthesia (more bleeding/infection)  
Anaesthetic gas and oxygen  
levels  
What the machine delivers Right depth of anaesthesia, enough oxygen  
What about doses of anaesthetic drugs?  
PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED Induction drugs, muscle relaxants, strong painkillers during surgery  
and reversal drugs are chosen by the anaesthetist for that patient. ProcSense does not simulate them.  
Other anaesthetic concerns the trainee should pass on  
- PONV (post-operative nausea and vomiting): more likely in women, non-smokers, previous PONV or  
travel sickness, and after keyhole gallbladder surgery.  
- Malignant hyperthermia: a rare inherited reaction to some anaesthetic drugs (dangerous high  
temperature). Ask about family members who had serious problems or died under anaesthesia.  
- Previous difficult intubation: ask about previous anaesthetics.  
- Temperature: patient should be at least 36.0 °C before going to sleep; warm them if longer than 30  
minutes of anaesthesia CLINICAL REFERENCE — SOURCE BACKED (NICE CG65).  

## 8.3 Medicine groups the trainee deals with

Teacher says:  
For each group: what it's for, the main 'don't give if', allergy issues, timing, and dose policy.  
Medicine group What it's for Don't give / take care if Allergy  
issues  
Timing Dose policy  
Paracetamol  
(acetaminophen)  
First painkiller;  
lowers fever  
Severe liver disease; very low  
weight → lower dose  
Allergy rare Can be given  
early  
EDUCATIONAL EXAMPLE DOSE  
(SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION IV  
paracetamol (SmPC): 1 g as a  
15-minute infusion, up to four  
times a day with at least 4 hours  
between doses, maximum 4  
g/day for adults over 50 kg (3  
g/day if additional risk factors  
such as liver disease,  
malnutrition, dehydration); under  
50 kg weight-based (15 mg/kg  
per dose, maximum 60  
mg/kg/day, not above 3 g);  
longer interval in severe kidney  
impairment. Oral dosing: per  
BNF/SmPC — prescriber decides  
NSAIDs  
(non-steroidal  
anti-inflammatory  
drugs: ibuprofen,  
diclofenac,  
ketorolac)  
Pain +  
inflammation;  
good for gallstone  
pain  
Kidney injury/dehydration,  
stomach ulcers/bleeding,  
blood thinners, some asthma,  
pregnancy (avoid), heart  
failure  
Some people  
get asthma at  
tacks/allergy  
Avoid in S2  
patients with  
kidney injury  
PATIENT-SPECIFIC DOSE —  
CLINICIAN CONTROLLED  
Opioids (morphine,  
fentanyl,  
tramadol) — strong  
painkillers  
Moderate–severe  
pain  
Can slow breathing, cause  
drowsiness, sickness; build up  
in kidney failure  
True allergy  
rare; itch  
common  
Titrated through  
the drip in  
severe pain;  
watch breathing  
PATIENT-SPECIFIC DOSE —  
CLINICIAN CONTROLLED  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 64*

Medicine group What it's for Don't give / take care if Allergy  
issues  
Timing Dose policy  
Antiemetics  
(anti-sickness:  
ondansetron,  
cyclizine,  
metoclopramide)  
Nausea/vomiting Ondansetron: certain  
heart-rhythm (long QTc)  
patients; metoclopramide: not  
with bowel blockage  
— Whenever  
needed  
EDUCATIONAL EXAMPLE DOSE  
(SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION  
ondansetron (SmPC,  
post-operative nausea/vomiting):  
4 mg by slow IV injection (not  
less than 30 s), at induction for  
prevention or as a single dose for  
established PONV; use for other  
causes of nausea is the  
prescriber's decision; others  
PATIENT-SPECIFIC DOSE —  
CLINICIAN CONTROLLED  
IV fluids  
(salt-water  
solutions such as  
Hartmann's /  
Ringer's lactate, or  
0.9% saline)  
Replace lost fluid;  
keep hydrated  
while fasting; treat  
low BP from dehyd  
ration/sepsis  
Heart failure or kidney failure  
(fluid overload — lungs fill  
with water)  
— Quick 'bolus' to  
resuscitate;  
slower  
'maintenance'  
otherwise  
EDUCATIONAL EXAMPLE DOSE  
(SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION  
resuscitation (NICE CG174 rec  
1.3.1): crystalloid with sodium  
130–154 mmol/L as a 500 mL  
bolus over less than 15 minutes,  
then reassess. CG174 does not  
cover pregnancy, severe liver or  
kidney disease, diabetes or  
burns, and heart failure needs  
smaller boluses — in those  
groups, and for the overall plan,  
PATIENT-SPECIFIC DOSE —  
CLINICIAN CONTROLLED  
Antibiotics (germ-killing medicines)  
Situation What to do Timing Dose policy  
S1, low-risk planned keyhole  
gallbladder  
Preventive ('prophylactic') antibiotic  
NOT routinely needed CLINICAL  
REFERENCE — SOURCE BACKED  
— —  
S1, higher risk: age over 70,  
inflamed gallbladder,  
jaundice/duct stones, diabetes,  
ASA 3+, weak immunity,  
pregnancy, likely conversion to  
open  
One preventive dose; drug chosen by  
local list CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION  
Within 60 minutes before the  
first cut CLINICAL REFERENCE —  
SOURCE BACKED (WHO; NICE  
NG125 says at the start of  
anaesthesia); repeat if operation  
is long  
EDUCATIONAL EXAMPLE  
DOSE (SOURCE-BACKED) —  
NOT A TRAINEE  
PRESCRIPTION cefazolin  
(ASHP/IDSA/SIS/SHEA  
2013): 2 g IV, 3 g if body  
weight is 120 kg or more;  
choice of agent and any  
redose depend on  
institutional policy, allergy  
history and kidney function  
S2 appendicitis (all patients) Broad antibiotics covering gut germs  
before surgery CLINICAL REFERENCE  
— SOURCE BACKED (WSES)  
As soon as decided; if sepsis is  
suspected follow the NICE NG253  
risk tier (within 1 hour for high  
risk); confirm timing at Time Out  
Local formulary  
CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL  
/ CLINICIAN  
CONFIRMATION (common  
examples: cefazolin +  
metronidazole;  
co-amoxiclav)  
S2 simple appendicitis after  
removal  
Usually no more antibiotics — —  
S2 burst/complicated Short course after source control: 2–3  
days (WSES 2025 edition; the 2020  
edition said up to 3–5 days)  
— PATIENT-SPECIFIC DOSE —  
CLINICIAN CONTROLLED  
Antibiotic allergy — the key branch  
Many patients say 'allergic to penicillin'. The trainee must ask what happened:  
- Mild delayed rash as a child → most guidelines allow related antibiotics like cefazolin.  
- Immediate severe reaction (throat swelling, collapse = anaphylaxis) → some guidelines still allow cefazolin,  
others require a completely different antibiotic (e.g. clindamycin or vancomycin plus a gut-germ antibiotic).  
This is an important hospital decision CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 65*

- Severe skin peeling reaction (SJS/TEN) → avoid this whole antibiotic family CLINICAL REFERENCE — SOURCE BACKED .  
Blood thinners (anticoagulants and antiplatelets)  
Medical word: Blood thinners  
Medicines that make blood less likely to clot — prescribed to prevent strokes (e.g. in atrial fibrillation), treat/prevent  
clots, or after heart stents. Anticoagulants: warfarin, apixaban, rivaroxaban, dabigatran, edoxaban (the last four are  
'DOACs' — direct oral anticoagulants). Antiplatelets: aspirin, clopidogrel, ticagrelor, prasugrel. Problem: they  
increase bleeding during surgery. But stopping them increases clot/stroke risk — so it's a balance.  
Medicine Usual stop time before planned surgery (if  
stopping is planned)  
Key points  
Warfarin 5 days; check INR the day before (aim 1.5 or less) 'Bridging' with injections only for very high clot risk  
— specialist decision CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION  
Apixaban / rivaroxaban /  
edoxaban  
1 day (low-moderate bleeding risk surgery) or 2 days  
(high bleeding risk), if kidneys OK  
Whether lap chole counts as low or high bleeding  
risk CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION (PAUSE  
trial)  
Dabigatran 1–2 days with good kidneys; 2–4 days if kidneys weaker Cleared by kidneys  
Aspirin Usually continue if for heart/stroke protection; may stop  
7 days if only 'just in case'  
Surgeon decides  
Clopidogrel / ticagrelor /  
prasugrel  
5 / 3–5 / 7 days Stopping after a recent heart stent is a cardiology  
decision (stent-clot risk): do not stop without the  
heart team; planned surgery is usually deferred  
instead  
In emergencies (S2), reversing blood thinners is a senior/haematology decision PATIENT-SPECIFIC DOSE — CLINICIAN  
CONTROLLED .  
Where this comes from: ACC 2017 periprocedural anticoagulation pathway; Douketis et al. PAUSE, JAMA Intern Med 2019; ESC 2022.  
Diabetes medicines on the day of surgery  
Medicine Day before Day of surgery Why  
Metformin Normal Normal if only one meal missed (some  
hospitals stop if kidneys weak or CT dye)  
CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION  
Low risk of low sugar  
Sulfonylureas (gliclazide,  
glimepiride)  
Normal Omit Cause low sugar when fasting  
SGLT2 inhibitors  
(dapagliflozin, empagliflozin,  
canagliflozin)  
Sources disagree: UK 2025  
consensus (Association of  
Anaesthetists, CPOC, JBDS,  
RCoA) = omit the day  
before AND the day of  
surgery; US FDA labelling =  
stop 3 days before  
(ertugliflozin 4). ProcSense  
default: UK consensus  
CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL  
/ CLINICIAN  
CONFIRMATION  
Omit; check ketones if unwell Can cause DKA even with normal  
sugar  
GLP-1 agonists  
(semaglutide/Ozempic,  
liraglutide, tirzepatide)  
See fasting Part 9 See Part 9 Slow stomach emptying →  
aspiration risk  
DPP-4 inhibitors (sitagliptin) Normal Omit or take per local  
CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION  
Low risk  
Long-acting insulin Reduce by about 20% Reduce by about 20% —  
PATIENT-SPECIFIC DOSE — CLINICIAN  
CONTROLLED  
Avoid low sugar while fasting  
Other insulin Normal Adjust/omit per protocol; insulin drip if  
more than one meal missed or poor  
control  
PATIENT-SPECIFIC DOSE —  
CLINICIAN CONTROLLED  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 66*

Type 1 diabetes: insulin should not be stopped entirely (DKA can develop within hours); the dose is adjusted  
per protocol CLINICAL REFERENCE — SOURCE BACKED .  
Where this comes from: Centre for Perioperative Care (CPOC) diabetes guideline 2021/2023.  
Other regular medicines  
Medicine Usual plan Why  
ACE inhibitors / ARBs (BP tablets:  
ramipril, lisinopril, losartan…)  
Often omit for 24 h before surgery CONTEXT-DEPENDENT  
— INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
Can cause low BP when going to sleep  
Beta-blockers (bisoprolol, atenolol…) Usually continued; avoid stopping suddenly; not usually  
started on the day  
Stopping suddenly can cause fast  
heart/heart attack  
Water tablets (diuretics) Usually omit on the morning CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
Dehydration, low potassium  
Statins, calcium-channel blockers Continue Safe  
Steroid tablets (e.g. prednisolone 5  
mg+ for over 4 weeks)  
Continue + extra steroid cover if the patient has adrenal  
insufficiency or long-term glucocorticoid use (prednisolone  
5 mg/day or more for about a month). EDUCATIONAL  
EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE  
PRESCRIPTION hydrocortisone 100 mg IV at induction  
(Association of Anaesthetists / RCP / Society for  
Endocrinology, Anaesthesia 2020); the anaesthetist decides  
who needs it and what follows  
The body can't make its own stress  
steroids → collapse risk ('adrenal  
crisis')  
Combined contraceptive pill / HRT Clot risk; for major surgery may stop 4 weeks before (NICE  
NG89); lap chole usually continue with clot prevention  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  
Oestrogen raises clot risk  
Herbal remedies (ginkgo, garlic,  
ginseng, fish oil)  
Often stop 1–2 weeks before CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
Bleeding risk  
Epilepsy and Parkinson's medicines Usually continued on time; specialist advice if any dose will  
be missed  
Fits / severe stiffness  
Clot prevention (VTE prophylaxis)  
Mechanical: tight anti-embolism stockings or inflatable leg sleeves (not if poor leg circulation, leg ulcers,  
severe nerve damage). Medicine: a small daily blood-thinning injection (low-molecular-weight heparin,  
LMWH), usually starting 6–12 hours after surgery and continuing at least 7 days for tummy surgery if clot risk  
outweighs bleeding risk CLINICAL REFERENCE — SOURCE BACKED (NICE NG89). EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED)  
— NOT A TRAINEE PRESCRIPTION enoxaparin (SmPC): 20 mg once daily SC for moderate VTE risk, 40 mg once daily SC  
for high risk; 20 mg daily if creatinine clearance is 15–30 mL/min; the SmPC advises about 7–10 days minimum  
in moderate-risk patients; the risk category, start time (including timing relative to spinal/epidural anaesthesia)  
and duration PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED .  
Emergency medicines that appear in the game  
Emergency Key medicine Dose policy  
Anaphylaxis (severe allergy) Adrenaline (epinephrine) injected into the thigh  
muscle  
EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION adults and children over 12: 500  
micrograms (0.5 mL of 1 mg/mL) IM into the anterolateral  
thigh; repeat after 5 minutes if airway, breathing or  
circulation problems persist; IV adrenaline only by  
experienced specialists (Resuscitation Council UK 2021; this  
is a standard algorithm dose — the responsible clinician  
confirms)  
Low blood sugar, can swallow Fast sugar (e.g. glucose drink/tablets 15–20 g) EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION JBDS hospital hypoglycaemia  
guideline: 15–20 g quick-acting carbohydrate if the patient  
is conscious and able to swallow (and the protocol allows  
oral treatment — fasting status matters), recheck glucose  
after 10–15 minutes and repeat as the protocol directs  
Low blood sugar, nil by  
mouth/drowsy/unable to  
swallow  
Glucose through the drip EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION JBDS: IV 10% glucose 150–200 mL  
(or 20% glucose 75–100 mL) over about 15 minutes,  
recheck after 10 minutes; prescriber and hospital protocol  
confirm  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 67*

Emergency Key medicine Dose policy  
Very high potassium IV calcium (protects the heart without lowering  
potassium); insulin with glucose (moves  
potassium into cells)  
EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A  
TRAINEE PRESCRIPTION UK Kidney Association algorithm:  
with ECG changes, 30 mL of 10% calcium gluconate IV over  
10 min (or 10 mL of 10% calcium chloride); insulin–glucose  
10 units soluble insulin in 25 g glucose IV over 15–30 min,  
followed by 10% glucose 50 mL/h for 5 h if pre-treatment  
glucose is below 7 mmol/L; repeat ECG and monitor  
potassium and glucose. Emergency prescription by the  
responsible clinician  
Too much opioid (slow  
breathing)  
Naloxone (antidote) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED  
Sepsis IV antibiotics + fluids Antibiotic per institutional policy CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ;  
fluid EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) —  
NOT A TRAINEE PRESCRIPTION 500 mL crystalloid bolus  
over less than 15 minutes then reassess (NICE CG174;  
scope exclusions above)  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 68*


# Part 9 — Fasting, taught from zero

Why must patients not eat before surgery?  
Food stays in the stomach for hours. Under anaesthesia the protective reflexes stop, so stomach contents can  
flow up and into the lungs (aspiration) — causing severe lung damage. An empty stomach makes this very  
unlikely.  
But why not just fast for a long time to be safe?  
Long fasting causes thirst, dehydration, low blood sugar (especially in diabetics), headache and worse  
recovery. Modern guidance encourages clear drinks until 2 hours before CLINICAL REFERENCE — SOURCE BACKED .  
Minimum fasting times before PLANNED anaesthesia  
Zone Clinical value / band  
(see source line)  
What it means in plain English What the trainee recognises and initiates  
(a qualified clinician confirms treatment)  
Clear liquids 2 hours Water, pulp-free juice, black tea/coffee  
WITHOUT milk, fizzy drinks, special  
carbohydrate drinks. Leave the stomach fast  
Allowed until 2 h before (some hospitals allow  
sips until 1 h) CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION  
Breast milk  
(babies)  
4 hours Listed for completeness; ProcSense is adults —  
Milk, formula,  
tea/coffee WITH  
milk  
6 hours Milk behaves like food in the stomach Count as food  
Light meal (e.g.  
toast + clear  
drink)  
6 hours Small easily digested meal 6 h minimum  
Heavy, fried or  
fatty meal, meat  
(e.g. biryani,  
paratha, nihari)  
8 hours or more Slow to leave the stomach 8 h minimum CLINICAL REFERENCE — SOURCE  
BACKED ; classing meat-rich or fried dishes (e.g.  
biryani, paratha) as 'heavy' is CLINICAL  
INTERPRETATION  
Chewing gum Don't cancel ASA 2023: gum alone shouldn't delay surgery Remove before sleep  
Regular tablets With a sip of water Unless told to stop that medicine Follow medicine plan  
Where this comes from: ASA Practice Guidelines for Preoperative Fasting (2017) and 2023 update (Joshi et al.); ESAIC; Canadian  
Anesthesiologists' Society.  
Who must approve: Anaesthetist must confirm local fasting policy.  

### What if the patient ate recently? (S1)

Decision diagram — fasting in PLANNED surgery (S1)  
Ask: exact time AND type of last food, milky drink,  
clear drink  
Solid food/milk within 6 h, heavy meal within 8 h, or  
clear drink within 2 h?  
Fasting breach → tell the anaesthetist NOW  
(never 'round up' the time)  
YES  
NO  
Fasting OK → record times and continue  
After a breach, the anaesthetist usually moves the patient later on the list (so the right time passes) or  
postpones. The trainee does not decide alone CLINICAL REFERENCE — SOURCE BACKED .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 69*

What about emergencies? (S2)  
- Fasting status is often unknown or the patient ate recently. Vomiting does not empty the stomach.  
- Pain, strong painkillers and the illness itself slow stomach emptying.  
- So the anaesthetist treats every emergency patient as a 'full stomach' and uses RSI CLINICAL REFERENCE —  
SOURCE BACKED .  
- Emergency surgery is not delayed just because of food when waiting would be more dangerous than the  
aspiration risk. The anaesthetist and surgeon decide together; sometimes a short wait is safe for a simple  
appendix CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
- The trainee's job: record the exact time and content, tell the anaesthetist, and declare 'aspiration risk: YES'  
at Sign In. If vomiting persists, a tube through the nose to the stomach (NG tube) may be placed per senior.  
GLP-1 weight-loss/diabetes injections  
Medicines such as semaglutide (Ozempic/Wegovy), liraglutide and tirzepatide (Mounjaro) slow stomach  
emptying, so food can remain even after normal fasting. Guidance has changed quickly:  
Source (year) Recommendation Status  
ASA consensus (June 2023) Hold daily doses on the day; hold weekly doses 1 week before Superseded  
US multisociety guidance incl. ASA, AGA,  
ASMBS, ISPCOP, SAGES (Oct 2024)  
Most patients continue; higher-risk patients (dose escalation, GI  
symptoms) follow a 24-hour liquid diet; consider RSI / gastric  
ultrasound  
Current (US)  
UK consensus — Association of  
Anaesthetists, RCoA, CPOC, JBDS and  
others (Anaesthesia 2025;80:412–24)  
Continue; full aspiration risk assessment; use risk-reducing anaesthetic  
techniques  
Current (UK)  
SPAQI consensus (Br J Anaesth  
2025;135:48–78)  
Continue; but 24-hour solid-food fast and stop clear liquids 4 hours  
before  
Current (alternative  
view)  
ProcSense default: the patient continues the drug; the trainee must ask about it and about  
nausea/vomiting/bloating, and tell the anaesthetist, who decides between proceeding with precautions (RSI) or  
delaying CLINICAL INTERPRETATION CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Other aspiration risk factors to tell the anaesthetist about  
Emergency surgery; bowel blockage; strong painkillers; pregnancy beyond about 18–20 weeks; diabetes with  
slow stomach; GLP-1 medicines; obesity; reflux/heartburn; reduced alertness.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 70*


# Part 10 — Consent, taught from zero

What is consent?  
Permission from the patient for a treatment, given freely, by someone able to make the decision, after being  
told what they need to know. Operating without valid consent is legally and ethically wrong (in most places it's  
assault).  
Who can take consent?  
The operating surgeon or a trained doctor who knows the procedure and its risks CLINICAL REFERENCE — SOURCE  
BACKED (UK GMC 2020).  
What makes consent valid? (four ingredients)  
1\. Capacity — the patient can understand the information, remember it long enough, weigh it up, and  
communicate a decision. Pain, strong painkillers, sepsis, low oxygen, low sugar or low sodium can  
temporarily remove capacity — treat the cause and try again. (UK Mental Capacity Act model; capacity law  
differs between countries CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .)  
2\. Information — diagnosis, what the operation is, benefits, risks that matter to a reasonable person and to  
this patient (UK 'material risk' rule, Montgomery 2015), alternatives including doing nothing, and what  
happens without treatment.  
3\. Voluntary — no pressure. In many cultures family involvement is expected and welcome, but an adult with  
capacity makes their own decision CLINICAL REFERENCE — SOURCE BACKED (GMC 2020; WHO patient-rights  
principles).  
4\. Understanding checked — 'teach-back': ask the patient to explain it back in their own words.  
The consent conversation, step by step  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 71*

Consent flow  
1\. Is the patient ready? Pain controlled, privacy,  
interpreter if needed, alert  
Does the patient have capacity (understand, retain,  
weigh, communicate)?  
Treat the cause and retry. If still no and urgent:  
senior + best-interests/emergency pathway per  
local law  
NO  
YES  
2\. Explain diagnosis in plain words  
3\. Explain the operation (keyhole; may need open)  
4\. Benefits, then risks with numbers, then  
alternatives (incl. doing nothing)  
5\. Ask 'What matters most to you?'; answer  
questions honestly  
Teach-back: can they explain it back? Re-explain differently / interpreter / leaflet  
NO  
YES  
Decision? Respect; S1 rebook; S2 explain risks of delay,  
senior, document, safety-net  
Refuses / wants time  
Agrees  
6\. Sign form; give copy; explain next steps (fasting,  
timing)  
What to tell each patient — figures CLINICAL REFERENCE — SOURCE BACKED from named  
studies (the unit's own audit figures are preferred if available  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION )  
Item S1 — keyhole gallbladder removal S2 — appendix removal  
Diagnosis "You have gallstones which are causing attacks of pain." "Your appendix is inflamed — appendicitis."  
Operation "We remove your gallbladder through 3–4 small cuts using  
a camera; we fill your tummy with gas to see."  
"We remove your appendix through 3 small cuts, or  
sometimes one larger cut."  
Benefits No more attacks; prevents complications (inflammation,  
jaundice, pancreatitis)  
Removes the infection; prevents bursting, abscess and  
sepsis  
Common/important  
risks  
Wound infection (roughly 1–5% CLINICAL  
INTERPRETATION ); bleeding; bile leak (about 0.5% —  
pooled 0.46%); major injury to the main bile duct (about  
0.3% — pooled 0.28%, may need major repair) (SAGES  
review, Pucher et al. 2018); stone left in duct; conversion to  
open (about 2–5% planned operations, up to \~20% in  
emergency/inflamed cases); shoulder-tip pain from the gas;  
hernia at cut; ongoing symptoms; clots; bowel injury (rare);  
death (very rare, under \~0.1%)  
Wound infection (more in open/burst cases); pus collection  
inside (\~2–5% simple, more if burst); bleeding; injury to  
bowel/bladder (rare); conversion to open; appendix turns  
out normal; bowel slow to restart; clots  
Alternatives Watch and wait, low-fat diet (attacks often return;  
complications possible)  
For simple appendicitis without a stone inside: antibiotics  
only — 29% needed appendectomy by 90 days (25%  
without and 41% with an appendicolith) in the CODA trial  
(NEJM 2020); 39.1% had recurrence within 5 years in the  
APPAC trial (JAMA 2018)  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 72*

Item S1 — keyhole gallbladder removal S2 — appendix removal  
If no treatment More attacks, possible inflammation, jaundice, pancreatitis Bursting, abscess, peritonitis, sepsis, possibly death  
Anaesthetic General anaesthetic (anaesthetist explains) General anaesthetic with rapid sequence (stomach may not  
be empty)  
Common patient  
questions CONTEXT  
\-DEPENDENT —  
INSTITUTIONAL  
PROTOCOL /  
CLINICIAN  
CONFIRMATION  
"Can I go home today?" (often yes). "Scars?" (3–4 small).  
"Diet after?" (normal; some find fatty food upsets them).  
"Work?" (1–2 weeks)  
"Will I be able to have children?" (removing the appendix  
doesn't reduce fertility; a burst appendix with pelvic  
infection may slightly increase tube problems — surgeon to  
approve wording). "Scars?" "Time off?"  
Where this comes from: GMC Decision making and consent (2020); Montgomery v Lanarkshire (UK 2015); CODA Collaborative NEJM  
2020; Salminen et al. JAMA 2018; NICE CG188; WSES 2020.  
Consent problems and what to do  
Problem Correct action What happens in the game if done  
wrong  
Patient in severe pain Request pain relief first (clinician confirms), then consent (pain relief  
doesn't invalidate consent)  
Consent marked invalid  
Patient confused Treat cause (sugar, oxygen, sepsis), reassess; if urgent and still lacking  
capacity → senior + emergency/best-interests route  
Signing a confused patient = safety  
failure  
Doesn't speak the  
language  
Trained interpreter (in person or phone); note their name. Avoid family  
members, especially children, as interpreters where a trained  
interpreter is available CLINICAL REFERENCE — SOURCE BACKED (GMC  
2020\)  
Teach-back fails → invalid  
Wants family to decide Welcome family; confirm the patient themself agrees —  
Patient is under 18 Consent rules for under-18s differ by jurisdiction (e.g. parent/guardian  
consent; 'mature minor' or Gillick competence in some countries);  
involve the young person CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION  
Invalid consent  
Wants more time S1: allow, rebook. S2: explain risks of waiting, senior discussion —  
Refuses Explore why, correct misunderstandings, offer alternatives, senior,  
document, safety advice, door left open  
Pressuring = professionalism failure  
Form has wrong  
procedure/side  
Stop; new consent Blocked at checklist  
Consented weeks ago  
(S1)  
Re-confirm on the day; check nothing has changed Missed change → debrief  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 73*


# Part 11 — The steps, one by one

Teacher says:  
Now we walk through every step of the journey using the same 21 questions each time, so the engineering team can  
build each stage the same way. Where S1 and S2 differ, I'll say so inside the step.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 74*


## STEP 1 — Patient arrival and identification

What are we doing?  
Meeting the patient, making 100% sure we have the right person, checking allergies, and opening their  
hospital record.  
Why are we doing it?  
Wrong-patient errors (wrong blood sample, wrong medicine, wrong operation) are some of the most serious  
mistakes in hospitals. An allergy that isn't recorded can kill when a medicine is given. Everything later depends  
on this step CLINICAL REFERENCE — SOURCE BACKED .  
What does the trainee actually do?  
1\. Introduce yourself: name and role ("Hello, I'm Dr Ruhan, one of the surgical doctors").  
2\. Ask the patient to SAY their full name and date of birth. Never read it to them ("You're Ayesha Khan,  
right?") — anxious people say yes to anything.  
3\. Compare their answer with the wristband AND the notes. At least two identifiers must match CLINICAL  
REFERENCE — SOURCE BACKED (locally usually name + date of birth + hospital/MR number CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ).  
4\. Ask: "Do you have any allergies? What happened when you had it?" Check the allergy band (often red  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ).  
5\. Ask: "Can you tell me what operation you're here for / what's brought you in?"  
6\. S1: check a responsible adult can take them home and stay for 24 hours (needed for day surgery) CLINICAL  
INTERPRETATION .  
7\. Record arrival time; open the admission record.  
What does the trainee see?  
S1: day-surgery bay; calm patient sitting on a trolley in their own clothes; folder; nurse. S2: Emergency  
Department cubicle; patient curled up holding the lower right tummy; a relative; triage note on a clipboard.  
What information do they receive?  
Patient's spoken name and date of birth, wristband text, folder details, allergy answer. S2 triage note, e.g.  
"19M, RIF pain 20 h, HR 108, T 37.9, pain 8/10, category: very urgent" (RIF = right iliac fossa = lower right  
tummy).  
What does that information mean?  
If all identifiers match, we can safely take samples and give treatment. An allergy with a severe reaction  
(swelling, breathing trouble) means that medicine and related ones must be avoided.  
What is the normal/reference value?  
Identity: every identifier matches. Allergy: 'none known' OR allergy recorded with reaction and band in place.  
What counts as abnormal?  
Any mismatch (spelling, date, MR number); an allergy the patient mentions that isn't recorded; patient  
describing a different operation or side.  
What counts as seriously concerning?  
Patient cannot confirm identity (unconscious, confused, language barrier) and documents are missing; history  
of anaphylaxis.  
What could cause the abnormal result?  
Admin errors, two patients with similar names, patient confusion, missing wristband.  
What should the trainee check next?  
1\. Mismatch → stop; ask the nurse/admissions to resolve before anything else.  
2\. Patient can't confirm → use wristband + notes + relative + interpreter; follow the hospital unknown-patient  
policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
3\. New allergy → update record, apply band, tell the nurse.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 75*

4\. Different operation described → call the surgical team before continuing.  
What happens if the trainee finds nothing wrong?  
Go to STEP 2 (history). The game unlocks blood tests and medicines only now.  
What happens if the trainee finds something abnormal?  
Stop and fix it first. Nothing else is allowed until identity is certain.  
What happens if the abnormality is mild?  
Small spelling error in notes with all other identifiers matching → nurse corrects, then continue.  
What happens if it is severe?  
Real identity doubt → no samples, drugs, transfer. Severe allergy → flagged everywhere; affects antibiotic  
choice later.  
When do we call the senior doctor?  
Patient describes a different operation or side than booked; identity cannot be resolved.  
When do we call the anaesthetist?  
Severe allergy history (especially to anaesthetic drugs, latex, chlorhexidine).  
When might surgery be delayed?  
S1: if identity or the planned operation can't be confirmed.  
When should emergency surgery continue despite the abnormality?  
S2: an unconscious emergency patient can be treated using emergency identification procedures; life-saving  
treatment is not withheld while paperwork is fixed CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION .  
What happens if the trainee misses it?  
Reading the name to the patient → patient agrees with a wrong name → later the blood sample belongs to  
someone else ('wrong blood in tube' event). Missing an allergy → allergic reaction later (Part 14).  
What happens after the correct action?  
Nurse confirms: "Identity checked, allergy band on." The game marks the patient 'identified'.  
Medical words used here  
Identifier A piece of information that uniquely identifies a person (full name, date of birth, hospital number).  
MR number Medical record number — the hospital's ID for the patient.  
Anaphylaxis A severe, life-threatening allergic reaction.  
Triage Sorting patients by urgency on arrival.  
Where this comes from: WHO Surgical Safety Checklist (2009); NICE NG180 Perioperative care; Manchester Triage System concept.  
Clinician validation: Hospital: identifier set, allergy band colour, unknown-patient procedure, triage system.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 76*


## STEP 2 — Asking questions (the history)

What are we doing?  
A structured conversation to learn what's wrong, what other illnesses and medicines the patient has, and  
anything that makes surgery unsafe.  
Why are we doing it?  
Most of the information needed for a diagnosis comes from the history. Many dangers (blood thinners, recent  
food, pregnancy) are invisible unless you ask CLINICAL INTERPRETATION .  
What does the trainee actually do?  
1\. Main complaint: "What's the problem? Where's the pain? When did it start? Does it move? What makes it  
worse? 0–10?"  
2\. Red-flag questions (warning signs). S1: fever? yellow skin/eyes? dark urine/pale stools? constant pain  
over 24 h? pain through to back? S2: pain moved from belly button? vomiting? appetite? fever? urine  
symptoms? diarrhoea? women: last period, chance of pregnancy, vaginal bleeding, shoulder-tip pain; men:  
testicle pain.  
3\. Other illnesses: heart, lungs, blood pressure, diabetes, kidney, liver, bleeding problems, previous clots,  
sleep apnoea, thalassaemia/sickle cell.  
4\. Medicines: everything, especially blood thinners (and when last taken), diabetes medicines, GLP-1  
injections, steroids, BP tablets, the pill, herbal remedies.  
5\. Allergies: what and what happened.  
6\. Previous anaesthetics: any problems; family members with serious anaesthetic problems.  
7\. Social: smoking, alcohol, drugs, who's at home.  
8\. Last food and drink: exact time and what.  
9\. Pregnancy (whenever pregnancy is possible for this patient): asked privately and sensitively; test offered  
with consent if there is any doubt CLINICAL REFERENCE — SOURCE BACKED (NICE NG45).  
10\. Summarise back and ask "Anything else you want to tell me?"  
What does the trainee see?  
The patient (and maybe a relative); the clerking form fills in as answers are given.  
What information do they receive?  
Spoken answers, e.g. S1: "The pain comes after oily food, under my right ribs, then goes after an hour or two."  
If asked about blood thinners: "I take apixaban — I had it this morning." S2: "It started around my belly button  
last night and now it's here \[points lower right\]. I threw up once."  
What does that information mean?  
S1 'comes and goes after fatty food' = typical gallstone pain (biliary colic) — fits the plan. 'Constant pain with  
fever' = possible inflamed gallbladder. S2 'pain moved + vomiting + no appetite' = classic appendicitis story.  
What is the normal/reference value?  
S1: typical gallstone attacks, no red flags, medicines manageable, fasted correctly. S2: there is no 'normal' —  
we're collecting clues.  
What counts as abnormal?  
Any red-flag answer; a blood thinner not stopped as planned; food within fasting limits; possible pregnancy;  
new allergy.  
What counts as seriously concerning?  
Fever + jaundice + pain (possible cholangitis); positive pregnancy chance with pain and dizziness (possible  
ectopic); chest pain or breathlessness; severe allergy history.  
What could cause the abnormal result?  
The disease progressing (S1 becoming acute), other conditions mimicking it, patient not following instructions,  
missed medicine plan.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 77*

What should the trainee check next?  
1\. Red flags → focused examination (Step 4) and relevant tests.  
2\. Blood thinner/diabetes/GLP-1 → medicine plan (Part 8).  
3\. Food → fasting branch (Part 9).  
4\. Possible pregnancy → pregnancy test with consent (Step 6).  
What happens if the trainee finds nothing wrong?  
Go to STEP 3 (vital signs).  
What happens if the trainee finds something abnormal?  
Record it, then follow the branch in 'what next'.  
What happens if the abnormality is mild?  
e.g. smoker, mild reflux → note and tell anaesthetist; continue.  
What happens if it is severe?  
e.g. took apixaban this morning before planned surgery → S1 will be postponed; e.g. chest pain → emergency  
assessment.  
When do we call the senior doctor?  
Red flags suggesting complication (S1 jaundice/fever; S2 a possible appendicitis mimic such as ectopic  
pregnancy); patient describes a different problem than booked.  
When do we call the anaesthetist?  
Fasting breach; previous anaesthetic problems; sleep apnoea; GLP-1 injections; blood thinners; heart/lung  
disease.  
When might surgery be delayed?  
S1: blood thinner not stopped; recent food; possible pregnancy; new serious symptoms.  
When should emergency surgery continue despite the abnormality?  
S2: recent food or blood thinners do NOT stop emergency surgery — they change how it's done (RSI, reversal  
decisions).  
What happens if the trainee misses it?  
Hidden facts stay hidden: e.g. the morning apixaban is discovered by the anaesthetist at Sign In → case stops,  
debrief 'missed medicine history'. Missing the pregnancy question → blocked at checklist.  
What happens after the correct action?  
The clerking note is complete; decisions in later steps unlock.  
Medical words used here  
Red flag A warning symptom that suggests something more serious.  
Biliary colic Cramping gallstone pain that comes and goes.  
Jaundice Yellow skin/eyes from bilirubin build-up.  
LMP Last menstrual period — date the last period started.  
Ectopic pregnancy Pregnancy growing outside the womb; can rupture and bleed.  
Where this comes from: NICE NG45 (pregnancy question); NICE CG188; WSES 2020; StatPearls (acute appendicitis).  
Clinician validation: Surgeon: which facts are hidden until asked; patient answers per variant.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 78*


## STEP 3 — Checking vital signs

What are we doing?  
Measuring blood pressure, heart rate, breathing rate, oxygen level, temperature, alertness, and pain score,  
then calculating the NEWS2 early-warning score.  
Why are we doing it?  
Tells us if the patient is stable now and gives a baseline to compare against later (Part 4).  
What does the trainee actually do?  
1\. Attach the oxygen finger clip; read oxygen % and heart rate; look at the wave.  
2\. Put on the BP cuff; wait for the reading.  
3\. Count breaths for 30–60 seconds.  
4\. Measure temperature.  
5\. Check alertness (ACVPU).  
6\. Ask pain score 0–10.  
7\. Feel the pulse — regular or irregular?  
8\. Weight/height if not recorded; finger-prick sugar if diabetic.  
9\. Calculate NEWS2 and pick the right response.  
10\. Repeat any odd reading.  
What does the trainee see?  
A blank monitor that fills in as devices are attached; NEWS2 chart with any previous readings.  
What information do they receive?  
S1 typical: HR 76, BP 124/78, RR 14, SpO2 98% on air, T 36.7, Alert, pain 0, NEWS2 0. S2 typical: HR 104, BP  
128/76, RR 18, SpO2 98%, T 37.9, Alert, pain 7, NEWS2 1.  
What does that information mean?  
S1 values are all normal → stable. S2: fast heart and slightly raised temperature from pain and inflammation —  
expected in appendicitis but must be watched.  
What is the normal/reference value?  
Clinical reference ranges: HR 60–100 at rest (NEWS2 scores 0 only for 51–90); BP below 140/90; RR 12–20;  
SpO2 96%+ on air (target 94–98%); T 36.1–37.9 °C; Alert; NEWS2 0 CLINICAL REFERENCE — SOURCE BACKED (RCP  
NEWS2; BTS 2017; ESH 2023).  
What counts as abnormal?  
Any value in the amber zones of Part 4 (e.g. HR 91–110, BP 101–110, RR 21–24, SpO2 94–95, T 38.0–39.0);  
NEWS2 1–4.  
What counts as seriously concerning?  
Any red value: HR ≥131 or ≤40; top BP ≤90; RR ≥25 or ≤8; SpO2 ≤91; T ≤35.0; new confusion. NEWS2 ≥5 or  
any single parameter scoring 3; NEWS2 ≥7 = emergency.  
What could cause the abnormal result?  
See Part 4: pain, anxiety, dehydration, infection/sepsis, bleeding, abnormal heart rhythm, medicines, lung  
problems.  
What should the trainee check next?  
1\. Repeat the abnormal reading (correct technique).  
2\. Look at the whole pattern (Part 4.11).  
3\. Look at the patient: pale? sweaty? breathing hard?  
4\. Treat obvious causes (pain relief, fluids).  
5\. Re-measure after 30–60 minutes (sooner if red).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 79*

What happens if the trainee finds nothing wrong?  
Go to STEP 4 (examination).  
What happens if the trainee finds something abnormal?  
Follow the relevant decision diagram in Part 4.  
What happens if the abnormality is mild?  
Treat the likely cause and re-check; continue meanwhile.  
What happens if it is severe?  
Stop the routine pathway: ABCDE emergency assessment (Part 14) and call for help.  
When do we call the senior doctor?  
NEWS2 5–6 (urgent), ≥7 (emergency), any single 3, or persistent abnormality without explanation. In S1, ANY  
NEWS2 above 1 needs explaining.  
When do we call the anaesthetist?  
BP ≥180/110 (S1), low oxygen, very fast/slow/irregular heart, low BP, high BMI/sleep apnoea.  
When might surgery be delayed?  
S1: BP ≥180/110 on repeat; fever; new irregular rhythm; any unexplained abnormal vital sign.  
When should emergency surgery continue despite the abnormality?  
S2: fast heart, fever, even low BP from sepsis do NOT stop surgery — they make it more urgent, after  
resuscitation (fluids, antibiotics) because the infected appendix is the cause.  
What happens if the trainee misses it?  
Fever in an S1 patient missed → anaesthetist finds 38.6 °C at Sign In → case stops. S2 rising heart rate missed  
→ patient deteriorates into sepsis.  
What happens after the correct action?  
Nurse sets the observation frequency according to NEWS2; values become the baseline.  
Medical words used here  
NEWS2 National Early Warning Score 2 (Part 4.9).  
ACVPU Alert, Confusion, Voice, Pain, Unresponsive.  
Where this comes from: RCP NEWS2 2017; AAGBI/BHS 2016; BTS 2017; NICE CG65.  
Clinician validation: Hospital early-warning tool; anaesthetist alarm thresholds.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 80*


## STEP 4 — Examining the patient

What are we doing?  
Looking, listening and gently feeling to find physical signs.  
Why are we doing it?  
Signs confirm or challenge the diagnosis and reveal dangers (e.g. a rigid tummy means possible perforation;  
yellow eyes mean bile blockage).  
What does the trainee actually do?  
1\. Explain and ask permission; offer a chaperone (another staff member present) CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
2\. General look: distress, pale, yellow (look at whites of eyes), sweaty, dry mouth.  
3\. Listen to heart and lungs with a stethoscope.  
4\. Tummy: start pressing away from the pain and move toward it, gently.  
5\. S1: press under right ribs while the patient breathes in (Murphy's sign).  
6\. S2: lower right tenderness, guarding, rebound; Rovsing's sign (pressing lower LEFT hurts lower RIGHT);  
psoas/obturator signs (leg movements that hurt with a hidden appendix).  
7\. S2: check hernia areas; in males check testicles (with consent/chaperone).  
8\. Look in the mouth and at the neck for the anaesthetist (Part 7.5).  
9\. Thank the patient, cover them, record findings.  
What does the trainee see?  
The patient on the couch; the trainee's hands/stethoscope; the patient's face and voice react.  
What information do they receive?  
S1-A: "Soft tummy, mildly tender under right ribs, Murphy's negative." S1-E: patient catches breath and says  
"Ow!" when right ribs pressed = Murphy's positive. S2-A: "Tender lower right, guarding, rebound positive,  
Rovsing's positive."  
What does that information mean?  
Murphy's positive = inflamed gallbladder likely. Lower right tenderness + guarding + rebound = inflamed  
lining near the appendix (localised peritonitis). Whole tummy rigid = generalised peritonitis → possible burst.  
What is the normal/reference value?  
S1: soft, non-tender or mildly tender tummy; no jaundice; normal heart/lung sounds; airway easy. S2: there is  
no 'normal' — expected localised lower-right signs.  
What counts as abnormal?  
S1: Murphy's positive, tender lump, jaundice, irregular pulse, wheeze. S2: signs outside the lower right,  
testicle/pelvic findings.  
What counts as seriously concerning?  
Rigid board-like tummy; very tender everywhere; a mass; signs of shock; very difficult airway.  
What could cause the abnormal result?  
Inflammation spreading, abscess, perforation, alternative diagnosis.  
What should the trainee check next?  
1\. S1 positive signs → bloods (WBC, CRP, liver tests), urgent ultrasound, senior.  
2\. S2 generalised signs → senior immediately, sepsis check.  
3\. Airway concern → anaesthetist.  
What happens if the trainee finds nothing wrong?  
Go to STEP 5 (risk and anaesthetic assessment) and STEP 6 (tests).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 81*

What happens if the trainee finds something abnormal?  
Record and follow the branch.  
What happens if the abnormality is mild?  
Mild right-upper tenderness in S1 is common with gallstones — continue.  
What happens if it is severe?  
S1: inflamed gallbladder → planned op cancelled, acute pathway. S2: rigid tummy → emergency; senior; speed  
up surgery.  
When do we call the senior doctor?  
Any S1 sign of acute inflammation or jaundice; S2 generalised peritonitis or mass; anything not fitting the  
expected picture.  
When do we call the anaesthetist?  
Difficult airway signs; sleep apnoea; heart murmur or irregular pulse.  
When might surgery be delayed?  
S1: signs of acute cholecystitis or jaundice.  
When should emergency surgery continue despite the abnormality?  
S2: peritonitis makes surgery more urgent.  
What happens if the trainee misses it?  
Not checking Murphy's sign in a feverish S1 patient → inflamed gallbladder discovered in theatre (debrief).  
Examining without permission → professionalism debrief.  
What happens after the correct action?  
Diagnosis becomes more or less likely; test choice is guided.  
Medical words used here  
Murphy's sign Pain stops the patient breathing in when the gallbladder area is pressed.  
Guarding Tummy muscles tense to protect a painful area.  
Rebound tenderness Pain when pressing hand is suddenly released.  
Peritonitis Inflammation of the tummy lining.  
Chaperone A second staff member present during intimate examinations.  
Where this comes from: TG18; WSES 2020; StatPearls.  
Clinician validation: Surgeon: examination finding sets for each variant; chaperone policy.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 82*


## STEP 5 — Risk and anaesthetic assessment

What are we doing?  
Putting together everything so far to judge how risky anaesthesia and surgery are, and whether the patient is  
fit now.  
Why are we doing it?  
Some risks can be reduced before surgery (optimisation) or planned for (special equipment, senior  
anaesthetist).  
What does the trainee actually do?  
1\. Propose an ASA grade (Part 7.4); the anaesthetist confirms.  
2\. Complete a clot (VTE) and bleeding risk assessment (Part 7.6).  
3\. Do the airway and sleep-apnoea checks (Part 7.5).  
4\. S2: calculate the appendicitis score once bloods return (Part 7.1).  
5\. S1 if inflamed: apply Tokyo Guidelines grading (Part 7.2).  
6\. Note risks for consent and handover.  
7\. Decide: proceed / optimise / more tests / senior or anaesthetic review / postpone.  
What does the trainee see?  
Anaesthetist character arrives (or phone); risk form; ASA selector.  
What information do they receive?  
Anaesthetist: "What ASA grade would you give him and why?" "When did he last eat?" "Any problems with  
previous anaesthetics?"  
What does that information mean?  
ASA 1–2 = generally low anaesthetic risk; 3+ = needs planning; 'E' = emergency adds risk.  
What is the normal/reference value?  
S1 typical ASA 1–2, low VTE risk, normal airway, fasted. S2 typical ASA 1E.  
What counts as abnormal?  
ASA 3; STOP-BANG 3–4; moderate VTE risk; fasting breach.  
What counts as seriously concerning?  
ASA 4; STOP-BANG 5+; very difficult airway; uncontrolled illness.  
What could cause the abnormal result?  
Comorbidities, emergency situation, obesity, smoking.  
What should the trainee check next?  
1\. Optimisable problem (S1) → postpone and refer.  
2\. Planning problem → tell anaesthetist.  
What happens if the trainee finds nothing wrong?  
Go to STEP 6 (choosing tests).  
What happens if the trainee finds something abnormal?  
Discuss with anaesthetist.  
What happens if the abnormality is mild?  
Note and plan.  
What happens if it is severe?  
Anaesthetist leads decision; S1 often postponed; S2 senior anaesthetist involved.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 83*

When do we call the senior doctor?  
Any uncertainty about proceeding.  
When do we call the anaesthetist?  
Always at this step.  
When might surgery be delayed?  
S1: uncontrolled comorbidity, fasting breach (re-timed), recent heart event.  
When should emergency surgery continue despite the abnormality?  
S2: high-risk patients still need source control; risk is managed rather than avoided.  
What happens if the trainee misses it?  
Wrong ASA → anaesthetist corrects with explanation (teaching moment, not failure). Hiding a fasting breach →  
aspiration event or cancellation.  
What happens after the correct action?  
Anaesthetic plan agreed (e.g. 'standard GA' or 'RSI').  
Medical words used here  
ASA grade Fitness-for-anaesthesia scale (Part 7.4).  
VTE Blood clots in veins.  
GA General anaesthesia.  
Where this comes from: ASA PS 2020; NICE NG89; NICE QS216; STOP-BANG (Chung).  
Clinician validation: Anaesthetist: expected ASA per variant; risk tool.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 84*


## STEP 6 — Choosing the tests

What are we doing?  
Deciding which tests are needed for THIS patient — and deliberately NOT ordering tests that won't change  
anything.  
Why are we doing it?  
Useful tests guide decisions. Useless tests waste time, money, can delay surgery and can produce misleading  
'abnormal' results (remember: 1 in 20 healthy people is outside the range) CLINICAL REFERENCE — SOURCE BACKED  
(NICE NG45).  
What does the trainee actually do?  
1\. Choose tests from the order panel.  
2\. Write the reason and urgency on the request.  
3\. Explain tests to the patient (sensitively for pregnancy test).  
4\. Take blood after identity check; label at the bedside.  
5\. Do bedside tests (pregnancy test, urine dipstick, finger-prick sugar, ECG).  
6\. Request scans; arrange safe transfer (porter; nurse or doctor escort if unstable).  
7\. Track pending results.  
What does the trainee see?  
Order panel with tests; coloured tubes; labels; urine pot; pregnancy test kit; ECG machine; scan request form;  
porter; wheelchair/trolley; portable monitor.  
What information do they receive?  
Confirmation of what's ordered; expected times.  
What does that information mean?  
Each test answers a specific question (see table below).  
What is the normal/reference value?  
The 'right set' for each patient (see below).  
What counts as abnormal?  
Missing an indicated test or ordering unhelpful ones.  
What counts as seriously concerning?  
Missing a pregnancy test in someone who could be pregnant; sending an unstable patient to a scan without  
monitoring.  
What should the trainee check next?  
1\. While waiting: treat pain, fluids, continue history/consent preparation.  
What happens if the trainee finds nothing wrong?  
Go to STEP 7 (results).  
When do we call the senior doctor?  
Unsure which scan; patient too unstable for transfer.  
When do we call the anaesthetist?  
Patient on blood thinners (clotting tests), heart disease (ECG).  
What happens if the trainee misses it?  
Unnecessary tests are listed in the debrief with their 'cost'. Pre-labelled tubes → wrong-blood event.  
Unmonitored unstable patient → drop in oxygen in the corridor event.  
What happens after the correct action?  
Results start to arrive after their simulated times.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 85*

Medical words used here  
Indicated There is a good medical reason for it.  
Bedside test Done at the bedside with a quick result.  
Where this comes from: NICE NG45; NICE CG188; WSES 2020.  
Clinician validation: Surgeon/anaesthetist: test-indication list; transfer rule; simulated turnaround times.  
Which tests for which patient? CLINICAL REFERENCE — SOURCE BACKED (NICE NG45,  
CG188, WSES) — local blood-ordering rules CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  
Test S1 planned gallbladder S2 emergency appendix Why  
Liver tests (LFTs) YES (all) If upper pain or unclear diagnosis Look for bile blockage  
Pregnancy test If pregnancy is possible or uncertain —  
offered with consent  
If pregnancy is possible — offered with  
consent (urgent in abdominal pain)  
Changes diagnosis, imaging,  
anaesthesia, drugs and timing  
FBC (blood count) If sicker (ASA 3+), anaemia symptoms,  
heart/kidney disease, or local rule  
YES Infection, bleeding, anaemia  
CRP Only if inflammation suspected YES Inflammation; score  
U&E (kidneys, salts) If 65+, diabetes, kidney disease, water  
tablets/ACE-i, ASA 3+  
YES Kidneys, potassium, dehydration  
Blood sugar  
(finger-prick)  
If diabetic If diabetic or vomiting Low/high sugar  
HbA1c If diabetic and none in 3 months No Diabetes control  
Clotting (INR/APTT) If on blood thinners or liver disease If on blood thinners, septic, liver  
disease  
Bleeding risk  
Group & save Per hospital rule Per rule; always if bleeding suspected Blood ready if needed  
Urine dipstick No (not routine) YES Urine infection/stone look-alikes  
ECG If heart/kidney/diabetes disease,  
irregular pulse, 65+ (some)  
If 65+, heart disease, HR above 130,  
irregular, salt problems  
Heart rhythm  
Lactate + blood  
cultures  
If septic If sepsis suspected Sepsis  
Lipase/amylase If severe upper pain / back pain If diagnosis unclear Pancreatitis  
Ultrasound Already done before booking; repeat if  
acute  
Young/female/pregnant first See inside  
CT Only for complications Adults when unclear; older patients Detailed scan  
MRCP If liver tests abnormal / duct wide No Look for duct stones  
Chest X-ray No (not routine) Only if breathing problems / suspected  
burst gut  
—  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 86*


## STEP 7 — Getting and understanding the results

What are we doing?  
Opening every result, comparing to normal and previous values, working out what it means for this patient,  
acting, and signing it off.  
Why are we doing it?  
A result nobody looks at is useless; an ignored dangerous result can kill.  
What does the trainee actually do?  
1\. Open each result as it arrives.  
2\. Compare with reference range and previous results.  
3\. Classify: normal / abnormal but not important / abnormal and important.  
4\. Check it fits the patient; repeat if suspicious.  
5\. Decide the action (Part 5 for each test).  
6\. Acknowledge (sign) the result and document the action.  
7\. Answer lab phone calls about critical results: repeat the value back, act, document.  
What does the trainee see?  
Results panel with H (high) / L (low) flags and comments; scan viewer and report; ringing phone.  
What information do they receive?  
e.g. 'Potassium 6.4 H — sample haemolysed'; 'Bilirubin 72 H'; 'Ultrasound: appendix not visualised'.  
What does that information mean?  
See Part 5 and Part 6 for each test's meaning.  
What is the normal/reference value?  
All results within reference ranges, or abnormal results that are known, stable and irrelevant.  
What counts as abnormal?  
Any result in amber/red zones of Part 5.  
What counts as seriously concerning?  
Any critical value (Part 5.27) or a result showing the diagnosis is different/complicated.  
What could cause the abnormal result?  
See each test in Part 5.  
What should the trainee check next?  
1\. Look for linked tests (e.g. high potassium → ECG; high bilirubin → duct size).  
2\. Look at the patient again.  
3\. Decide whether to repeat.  
What happens if the trainee finds nothing wrong?  
Go to STEP 8 (decide and treat/optimise).  
What happens if the trainee finds something abnormal?  
Use the decision diagrams in Parts 5–7.  
What happens if the abnormality is mild?  
Note, maybe repeat, usually continue.  
What happens if it is severe?  
Act immediately; call help; S1 usually deferred after review; S2 treat in parallel.  
When do we call the senior doctor?  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 87*

Any critical or pathway-changing result (abnormal liver pattern, positive pregnancy test, positive scan, sepsis  
markers).  
When do we call the anaesthetist?  
Potassium/sodium abnormal, anaemia, clotting abnormal, ECG abnormal, kidney injury, HbA1c above 69.  
When might surgery be delayed?  
S1: any significant abnormality (see Part 5 for thresholds).  
When should emergency surgery continue despite the abnormality?  
S2: kidney injury, salt problems, raised infection markers usually get corrected alongside — surgery continues  
as soon as reasonably safe.  
What happens if the trainee misses it?  
Unacknowledged critical result → blocks progress; if it was real, the patient deteriorates. Treating a faulty  
sample without repeating → 'over-treatment' debrief.  
What happens after the correct action?  
Results marked reviewed; plan updated.  
Where this comes from: Royal College of Pathologists 2017 and test-specific sources in Part 5.  
Clinician validation: Hospital lab ranges and critical list.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 88*


## STEP 8 — Deciding, treating and optimising

What are we doing?  
Using all the information to choose one of the five outcomes (safe / more investigation / treat-optimise /  
senior-anaesthetic review / emergency), and giving any treatment needed before theatre.  
Why are we doing it?  
This is the core decision skill of ProcSense. A correct decision protects the patient; the 'right' answer is different  
in S1 and S2.  
What does the trainee actually do?  
1\. Ask: "Is it safe to continue?" using the decision diagram below.  
2\. Pain: assess score; after the allergy check, request analgesia (e.g. paracetamol ± stronger painkiller) — a  
qualified clinician confirms drug and dose; the nurse administers.  
3\. Nausea: request an anti-sickness medicine through the same workflow.  
4\. Drip (cannula): S2 always; S1 usually in the anaesthetic room CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION .  
5\. Fluids if dehydrated or low BP: request and agree the fluid plan with the senior/anaesthetist; re-check vital  
signs after.  
6\. Medicine plan: hold/continue diabetes, BP, blood-thinner, steroid medicines (Part 8).  
7\. Clot prevention: stockings if indicated; injection plan.  
8\. Antibiotics: S2 — flag the need once appendicitis is decided (sooner if septic, per NICE NG253 risk tier) so  
the agent per institutional policy is prescribed by a qualified clinician; S1 only if risk factors.  
9\. Re-assess: vital signs, pain, behaviour after each treatment.  
Decision diagram — 'Is it safe to continue?' (the master decision)  
All information gathered so far  
Any EMERGENCY sign? (NEWS2 7+, top BP 90 or less,  
oxygen 91% or less, new confusion, anaphylaxis,  
chest pain, positive pregnancy test with shock)  
EMERGENCY: call for help, ABCDE, treat (Part  
14\)  
YES  
NO  
Does the diagnosis look different or complicated? (S1  
inflamed GB/jaundice; S2 an appendicitis mimic such  
as ectopic pregnancy, a burst appendix, other cause)  
SENIOR review → change pathway / more  
investigation  
YES  
NO  
Is there a fixable problem? (BP 180/110+, HbA1c  
above 69, salts abnormal, anaemia, blood thinner not  
stopped, fasting breach, new AF)  
Verify → assess severity and context →  
senior/anaesthetic review. S1: usually optimise  
first (defer/re-time); S2: correct in parallel  
YES  
NO  
Anything affecting anaesthesia? (airway, sleep  
apnoea, aspiration risk, heart/lung disease) ANAESTHETIST review / plan  
YES  
NO  
SAFE TO CONTINUE → consent and preparation  
What does the trainee see?  
Drug chart, drip stand, fluid bags, medicine trolley, stockings; nurse asking for instructions.  
What information do they receive?  
Nurse: "Before I give this, can you confirm her allergy status?" Senior: "Agreed — I've prescribed it." Response  
of vital signs to treatment.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 89*

What does that information mean?  
Improvement after treatment (pain falls, heart rate slows) confirms the cause; no improvement suggests  
something more serious.  
What is the normal/reference value?  
After treatment: pain 0–3; HR below 100; BP stable; NEWS2 falling.  
What counts as abnormal?  
Pain not controlled; HR still above 100 after pain relief and fluids; BP not responding to fluid.  
What counts as seriously concerning?  
Worsening despite treatment; any reaction after a medicine.  
What could cause the abnormal result?  
Disease progressing (burst appendix, sepsis), bleeding, allergic reaction, wrong diagnosis.  
What should the trainee check next?  
1\. Re-examine.  
2\. Repeat key bloods (lactate, Hb).  
3\. Call senior.  
What happens if the trainee finds nothing wrong?  
Go to STEP 9 (consent).  
What happens if the trainee finds something abnormal?  
Follow the diagram and Part 14 'What if'.  
What happens if the abnormality is mild?  
Continue treating; re-check in 30–60 minutes.  
What happens if it is severe?  
Emergency pathway.  
When do we call the senior doctor?  
No improvement after first treatments; sepsis; any change of diagnosis; any S1 deferral decision (senior  
confirms).  
When do we call the anaesthetist?  
Fasting breach; aspiration risk; electrolyte, heart, airway issues; sepsis (anaesthetic plan changes).  
When might surgery be delayed?  
S1: any optimisable problem. S2: only briefly to resuscitate (fluids, antibiotics, correct potassium) — not to wait  
for perfection.  
When should emergency surgery continue despite the abnormality?  
S2: sepsis, kidney injury, mild salt problems, unknown fasting, high WBC — all treated alongside; surgery is the  
cure.  
What happens if the trainee misses it?  
S2: no request for pain relief/fluids/antibiotics → deterioration clock speeds up (heart rate +5–10 every \~30  
min SIMULATOR DESIGN VALUE (PROVISIONAL) ). Requesting a drug the patient is allergic to → nurse challenges  
(beginner mode) or reaction occurs (advanced).  
What happens after the correct action?  
Patient improves and is 'ready for consent'.  
Medical words used here  
Optimise Improve the patient's condition before surgery.  
Bolus A quick volume of fluid given through the drip.  
Where this comes from: NICE CG174; NICE NG89; NICE NG125; WSES 2020; CPOC 2023.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 90*

Clinician validation: Surgeon/anaesthetist: all treatment choices; postponement rules.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 91*


## STEP 9 — Consent

What are we doing?  
Explaining the diagnosis, the operation, benefits, risks and alternatives, checking understanding, and getting  
the patient's signature (Part 10).  
Why are we doing it?  
A legal and ethical requirement and the patient's right to decide.  
What does the trainee actually do?  
1\. Make sure pain is controlled and the patient is alert; arrange privacy and an interpreter if needed.  
2\. Explain diagnosis and operation in plain words.  
3\. Benefits, risks (with numbers), alternatives, consequences of no treatment.  
4\. Ask what matters to them; answer questions honestly.  
5\. Teach-back.  
6\. Sign the form; give a copy; explain what happens next.  
What does the trainee see?  
Patient (and family), consent form, leaflet, interpreter phone.  
What information do they receive?  
Patient questions: "Will I be able to have children?" "Can I go home today?" "Can't I just take tablets?"  
What does that information mean?  
Questions reveal fears and what matters to the patient; they must be answered.  
What is the normal/reference value?  
Patient understands, teach-back succeeds, signs.  
What counts as abnormal?  
Patient confused, language barrier, wants more time, refuses.  
What counts as seriously concerning?  
No capacity in an urgent situation; patient under 18; refusal of life-saving surgery.  
What should the trainee check next?  
1\. Treat causes of confusion.  
2\. Interpreter.  
3\. Senior for refusal or capacity problems.  
What happens if the trainee finds nothing wrong?  
Go to STEP 10 (prepare patient).  
What happens if the trainee finds something abnormal?  
See Part 10 consent problems table.  
When do we call the senior doctor?  
Refusal; lack of capacity; legal doubt; minors.  
When do we call the anaesthetist?  
Anaesthetic risks are explained by the anaesthetist.  
When might surgery be delayed?  
S1: patient wants more time/information → rebook.  
When should emergency surgery continue despite the abnormality?  
S2: if the patient lacks capacity and surgery is urgent, the senior follows the emergency/best-interests route  
under local law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 92*

What happens if the trainee misses it?  
Missing risks or alternatives → low consent score + 'patient complaint' in debrief; consenting a confused  
patient → safety failure.  
What happens after the correct action?  
Valid consent recorded; checklist item ticked.  
Where this comes from: GMC 2020; Montgomery 2015.  
Clinician validation: Hospital consent form and law; surgeon's risk figures.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 93*


## STEP 10 — Preparing the patient (ward/ED

checklist)  
What are we doing?  
Physical and paperwork preparation so nothing is missing when the patient reaches theatre.  
Why are we doing it?  
Small omissions — jewellery, missing consent, unknown fasting time — cause harm or cancellations.  
What does the trainee actually do?  
1\. Re-confirm fasting times.  
2\. Hospital gown; remove jewellery (tape rings that won't come off), make-up, nail polish (for the oxygen  
probe).  
3\. Note dentures, hearing aids, contact lenses, implants.  
4\. Hair removal only if needed, with clippers (not razors — they cause infection) CLINICAL REFERENCE — SOURCE  
BACKED .  
5\. Site marking if required (usually NOT for these midline/belly-button keyhole operations — no left/right  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ).  
6\. Stockings if prescribed.  
7\. Pregnancy status recorded.  
8\. Consent present and correct.  
9\. Allergy band on; identity band on.  
10\. Diabetic: finger-prick sugar recorded.  
11\. Temperature at least 36.0 °C (warm if not) CLINICAL REFERENCE — SOURCE BACKED .  
12\. All documents together: notes, consent, results, drug chart, scans.  
13\. Complete the checklist.  
What does the trainee see?  
Gown, jewellery tray, clippers, marker pen, stockings, checklist form, document folder.  
What information do they receive?  
Nurse: "Checklist is complete except pregnancy status." Patient might say: "I did have a cup of tea with milk at  
7."  
What does that information mean?  
Each item must be confirmed; any gap must be filled before transfer.  
What is the normal/reference value?  
Every item ticked.  
What counts as abnormal?  
Missing item; late fasting discovery; temperature below 36.0.  
What counts as seriously concerning?  
Missing/invalid consent; unknown pregnancy status; identity problem.  
What should the trainee check next?  
1\. Fix the missing item.  
2\. Tell anaesthetist about any late fasting discovery.  
What happens if the trainee finds nothing wrong?  
Call theatre: patient ready (STEP 11/12).  
What happens if the trainee finds something abnormal?  
Fix before transfer; do not transfer with missing consent.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 94*

When do we call the senior doctor?  
Consent problems.  
When do we call the anaesthetist?  
Late fasting breach; low temperature.  
When might surgery be delayed?  
Until all blockers fixed.  
When should emergency surgery continue despite the abnormality?  
S2: fast but complete; uncertain fasting recorded rather than waited for.  
What happens if the trainee misses it?  
Razor used → infection-risk debrief. Missing consent → theatre returns the patient.  
What happens after the correct action?  
'Ready for theatre'.  
Where this comes from: NICE NG125 (hair removal); NICE CG65; WHO SSC.  
Clinician validation: Hospital ward checklist form; site-marking policy.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 95*


## STEP 11 — Theatre preparation

What are we doing?  
Making sure the operating theatre, equipment and team are ready for this specific patient.  
Why are we doing it?  
Discovering a broken machine after the patient is asleep is dangerous.  
What does the trainee actually do?  
1\. Anaesthetic machine check done and logged.  
2\. Monitors ready (oxygen, BP, ECG, carbon dioxide, temperature).  
3\. Suction working; oxygen; airway equipment; difficult-airway trolley known.  
4\. Operating table and padding.  
5\. Keyhole 'tower' (camera, light, gas pump) + CO2 gas cylinder with spare; instruments; open set available in  
case of conversion.  
6\. Diathermy (electric cutting/sealing device) with its sticky return pad.  
7\. Warming device.  
8\. Emergency trolley and defibrillator checked.  
9\. Specimen pots and labels; count sheets.  
10\. Team brief if used locally.  
What does the trainee see?  
Theatre with machines, trays, screens, gas cylinders, checklist board.  
What information do they receive?  
Scrub nurse: "The gas pump shows low supply." Check log signed or unsigned.  
What does that information mean?  
Any fault must be fixed before the patient arrives.  
What is the normal/reference value?  
All equipment working.  
What counts as abnormal?  
Fault found.  
What counts as seriously concerning?  
Anaesthetic machine failure; no working suction; no oximeter.  
What should the trainee check next?  
1\. Report; replace/repair; delay start.  
What happens if the trainee finds nothing wrong?  
Send for the patient.  
What happens if the trainee finds something abnormal?  
Fix first.  
When do we call the senior doctor?  
Delay affecting an emergency case.  
When do we call the anaesthetist?  
Any anaesthetic equipment fault.  
When might surgery be delayed?  
Until equipment safe.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 96*

When should emergency surgery continue despite the abnormality?  
S2: a substitute theatre may be used CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
What happens if the trainee misses it?  
Time Out cannot be completed.  
What happens after the correct action?  
'Theatre ready'.  
Medical words used here  
Diathermy Electric device to cut and stop bleeding.  
Tower Stack of keyhole equipment: screen, camera, light, gas pump.  
Where this comes from: Association of Anaesthetists equipment checks (2019); WHO-WFSA 2018.  
Clinician validation: Theatre team: equipment lists; which items trainee vs staff verify.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 97*


## STEP 12 — Transfer and handover (SBAR)

What are we doing?  
Moving the patient safely to theatre and telling the theatre team everything important in a structured way.  
Why are we doing it?  
Information lost during handover is a classic cause of errors CLINICAL REFERENCE — SOURCE BACKED (WHO Patient  
Safety Solutions 2007).  
What does the trainee actually do?  
1\. Theatre calls for the patient.  
2\. Choose escort and monitoring based on stability (no international numeric rule exists; ProcSense uses  
SIMULATOR DESIGN VALUE (PROVISIONAL) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ): NEWS2  
0–4 porter + nurse; 5–6 nurse + portable oxygen monitor; 7+ doctor + nurse with full monitoring and  
oxygen.  
3\. Transfer with all documents.  
4\. Theatre reception nurse checks identity, consent, allergies, fasting, site.  
5\. Give SBAR handover to the anaesthetist.  
What does the trainee see?  
Trolley, porter, portable monitor, folder; theatre reception desk.  
What information do they receive?  
Reception nurse: "Name and date of birth? Any allergies? When did you last eat?"  
What does that information mean?  
SBAR = Situation (who and why now), Background (relevant history), Assessment (current state, results),  
Recommendation (what you need/plan).  
What is the normal/reference value?  
All key items handed over. S1 example: "S: Ayesha Khan, 42, for lap chole. B: no medical problems, no  
allergies. A: obs normal, fasted since 22:00 food / 06:00 water, LFTs normal, pregnancy test negative, consent  
signed. R: ready for theatre, no antibiotic needed per protocol."  
What counts as abnormal?  
Missing item (allergy, fasting time, antibiotic time, blood thinner).  
What counts as seriously concerning?  
Patient deteriorates during transfer.  
What should the trainee check next?  
1\. If deterioration in transit: stop, ABCDE, call for help.  
What happens if the trainee finds nothing wrong?  
STEP 13 (Sign In).  
What happens if the trainee finds something abnormal?  
Provide the missing information immediately.  
When do we call the senior doctor?  
Deterioration during transfer.  
When do we call the anaesthetist?  
This step IS the handover to the anaesthetist.  
When might surgery be delayed?  
Missing documents → fetched before proceeding.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 98*

When should emergency surgery continue despite the abnormality?  
S2: handover can be brief but must include aspiration risk, antibiotic time, fluids given, latest NEWS2, lactate,  
kidney status, pregnancy status, scan result.  
What happens if the trainee misses it?  
Anaesthetist asks for the missing items; repeated omissions scored; corridor oxygen drop ignored →  
emergency branch.  
What happens after the correct action?  
Theatre team accepts the patient.  
Medical words used here  
SBAR Situation, Background, Assessment, Recommendation.  
Where this comes from: WHO Patient Safety Solutions 2007; WHO-WFSA 2018.  
Clinician validation: Hospital transfer policy; required handover items.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 99*


## STEP 13 — WHO Sign In (before the patient goes to

sleep)  
What are we doing?  
A spoken checklist by the nurse and anaesthetist (with the patient awake) before anaesthesia starts — see Part  
15\.  
Why are we doing it?  
Last chance to catch errors while the patient can still answer.  
What does the trainee actually do?  
1\. Patient confirms identity, site, procedure, consent.  
2\. Site marked or 'not applicable'.  
3\. Anaesthesia machine and medication check complete.  
4\. Oximeter on and working.  
5\. Known allergy?  
6\. Difficult airway or aspiration risk? (equipment/help ready)  
7\. Risk of more than 500 mL blood loss? (two drips/fluids planned)  
What does the trainee see?  
Anaesthetic room; checklist board; anaesthetist and nurse.  
What information do they receive?  
Anaesthetist: "Is there a risk of more than 500 mL blood loss?"  
What does that information mean?  
Every item needs an answer; 'no' or doubt = stop.  
What is the normal/reference value?  
All confirmed.  
What counts as abnormal?  
Any discrepancy.  
What counts as seriously concerning?  
Identity/consent doubt; oximeter not working.  
What happens if the trainee finds nothing wrong?  
Anaesthesia (not simulated) → STEP 14.  
What happens if the trainee finds something abnormal?  
Stop and resolve.  
When do we call the anaesthetist?  
Leads Sign In.  
When might surgery be delayed?  
Until resolved.  
When should emergency surgery continue despite the abnormality?  
S2: aspiration risk answered YES and planned for — not a reason to stop.  
What happens if the trainee misses it?  
Team member challenges; repeated skip = case stops.  
What happens after the correct action?  
Patient anaesthetised; move to Time Out.  
Where this comes from: WHO Surgical Safety Checklist 2009.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 100*

Clinician validation: Theatre governance: local additions.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 101*


## STEP 14 — WHO Time Out (before the first cut) —

the finish line  
What are we doing?  
The whole team pauses, introduces itself and confirms everything before the first incision — see Part 15.  
Why are we doing it?  
Final protection against wrong patient/procedure and missed preparations (antibiotics, imaging, equipment).  
What does the trainee actually do?  
1\. Everyone states name and role.  
2\. Confirm the patient's name, procedure, and where the incision will be made.  
3\. Antibiotic prophylaxis given within the last 60 minutes? (or not applicable)  
4\. Surgeon: critical/non-routine steps, expected duration, anticipated blood loss.  
5\. Anaesthetist: patient-specific concerns.  
6\. Nurse: sterility (including indicator results) confirmed; equipment issues or concerns.  
7\. Essential imaging displayed?  
What does the trainee see?  
Theatre; whole team; checklist board; screens showing scans.  
What information do they receive?  
e.g. Surgeon: "Expected duration 60 minutes, minimal blood loss." Nurse: "Sterility indicators confirmed."  
What does that information mean?  
All aligned → safe to start.  
What is the normal/reference value?  
All confirmed.  
What counts as abnormal?  
Antibiotic needed but not given or given more than 60 minutes ago; imaging missing; equipment fault.  
What happens if the trainee finds nothing wrong?  
READY FOR INCISION — the scenario is complete.  
What happens if the trainee finds something abnormal?  
Fix (give antibiotic, display imaging, fix equipment), then repeat item.  
When do we call the senior doctor?  
Surgeon is present.  
When might surgery be delayed?  
Until fixed.  
When should emergency surgery continue despite the abnormality?  
S2: urgency doesn't skip Time Out.  
What happens if the trainee misses it?  
Antibiotic timing missed → debrief/stop per setting.  
What happens after the correct action?  
Optional time-lapse → Sign Out → debrief.  
Where this comes from: WHO SSC 2009; NICE NG125 antibiotic timing.  
Clinician validation: Antibiotic rule per scenario.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 102*


## STEP 15 (optional) — WHO Sign Out and handover

to recovery  
What are we doing?  
After the operation, before the patient leaves theatre: confirm the procedure name, counts of  
instruments/swabs/needles, specimen label, equipment problems, and key concerns for recovery.  
Why are we doing it?  
Prevents retained instruments, lost specimens and poor handover.  
What does the trainee actually do?  
1\. Nurse confirms procedure name recorded.  
2\. Counts correct.  
3\. Specimen (gallbladder/appendix) labelled — label read aloud including patient name.  
4\. Equipment problems recorded.  
5\. Team reviews key concerns for recovery; hand over to recovery nurse.  
What happens if the trainee finds nothing wrong?  
Case ends; debrief.  
What happens if the trainee finds something abnormal?  
Count wrong → search, X-ray per local, escalate.  
Where this comes from: WHO SSC 2009.  
Clinician validation: Recovery handover items.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 103*


# Part 12 — S1 as a story: a planned gallbladder

operation  
Teacher says:  
Let's follow one patient from the moment she walks in to the moment the surgeon is ready to make the first cut. Then  
we'll replay the story with things going wrong.  
Example Patient A — uncomplicated S1 (everything goes right)  
Who: Mrs Ayesha Khan, 42, teacher, born 14 March 1984. Why here: three attacks of pain under her right ribs  
over six months, each after an oily meal, lasting 1–2 hours. Her GP sent her to a surgeon; an ultrasound  
showed gallstones. She was booked for keyhole gallbladder removal today at 09:00.  
Scene 1 — Arrival (Step 1)  
Teacher says:  
"Okay Ruhan, she has arrived. First, introduce yourself and make sure she's the right person." You ask: "Could you tell  
me your full name and date of birth?" She says "Ayesha Khan, fourteenth of March 1984." You check the wristband:  
AYESHA KHAN, 14/03/1984, MR 102938 — matches the notes. You ask about allergies: "None." No allergy band  
needed. You ask what she's here for: "To have my gallbladder taken out with keyhole." Her husband will take her  
home and stay overnight. → Identity confirmed. Continue.  
Scene 2 — Questions (Step 2)  
What you ask and what she answers  
- Pain: "Attacks under my right ribs after oily food, settle in an hour or two. None in the last 3 weeks." →  
typical biliary colic, no current attack.  
- Red flags: no fever, no yellow eyes, urine normal colour, no back pain. → no sign of complications.  
- Other illnesses: none. Medicines: none except paracetamol sometimes. Herbal: none.  
- Previous anaesthetic: caesarean section 2012, no problems. Family: no anaesthetic problems.  
- Smoking: never. Alcohol: none.  
- Last food: dinner at 21:00 last night (rice and chicken). Last drink: water at 06:30. Time now: 08:00. → solid  
food 11 hours ago (≥8 h ✓), clear water 1.5 hours ago… wait — that's less than 2 hours!  
Teacher says:  
Good catch moment! Water at 06:30 and surgery at 09:00 means 2.5 hours by the time of anaesthesia — that's fine,  
because the 2-hour rule applies to the time of anaesthesia, not the time you're asking. Record exact times and let the  
anaesthetist decide. Pregnancy: you ask privately; last period started 10 days ago, she uses a coil. She agrees to a  
urine pregnancy test (hospital policy) CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Scene 3 — Vital signs (Step 3)  
Measurement Value Zone Plain meaning  
Heart rate 76 /min, regular Normal (51–90) Heart beating calmly  
Blood pressure 124/78 mmHg Normal Good pressure; not high  
Breathing rate 14 /min Normal (12–20) Breathing calmly  
Oxygen saturation 98% on room air Normal (96+) Lungs working well  
Temperature 36.7 °C Normal No fever → no sign of infection  
Alertness Alert Normal Brain fine  
Pain 0/10 Normal No current attack  
BMI 27 (70 kg, 1.61 m) Overweight (South  
Asian cut-off 23+)  
Note for anaesthetist; not a problem  
NEWS2 0 Low Routine observations  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 104*

Scene 4 — Examination (Step 4)  
Eyes white (no jaundice). Heart and lungs sound normal. Tummy soft; mild tenderness under right ribs only  
with deep pressure; Murphy's sign negative. Mouth opens well (3 fingers), Mallampati class 2, neck moves  
normally. STOP-BANG 1. → Fits uncomplicated gallstones; airway looks easy.  
Scene 5 — Risk (Step 5)  
ASA 1 (healthy). Clot risk: low (age under 60, no risk factors other than surgery). Anaesthetist agrees: "ASA 1,  
standard general anaesthetic."  
Scene 6 — Tests (Step 6) and results (Step 7)  
Ordered: liver tests (because she has gallstone disease) and urine pregnancy test (policy). Not ordered: chest  
X-ray, ECG, clotting, kidney tests — she's healthy and under 65, so these wouldn't change anything CLINICAL  
REFERENCE — SOURCE BACKED .  
Test Result Normal What you think  
Bilirubin 12 µmol/L 3–21 Bile flowing normally  
ALT 24 U/L \~7–40 Liver cells fine  
ALP 78 U/L \~30–130 Bile ducts not blocked  
GGT 22 U/L below 40 (women) Confirms ducts fine  
Albumin 42 g/L 35–50 Good nutrition  
Urine pregnancy test Negative — Not pregnant  
(Ultrasound from clinic) Several stones; wall 2 mm;  
duct 4 mm  
wall ≤3 mm; duct ≤6 mm Simple gallstones, duct normal  
Teacher says:  
Everything looks acceptable, so we continue. Why? Because: she has the diagnosis we planned for; there's no sign  
the gallbladder is inflamed (normal temperature, heart rate, no Murphy's sign) or that a stone is in the bile duct  
(normal liver tests and duct width — 'low risk'); she's healthy (ASA 1); she's fasted; she's not pregnant. Nothing needs  
fixing or a senior decision.  
Scene 7 — Treatment/optimisation (Step 8)  
Nothing to optimise. She needs no regular medicines held. Antibiotic: not needed for a low-risk patient CLINICAL  
REFERENCE — SOURCE BACKED . Clot prevention: stockings per local policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION . Drip will be placed in the anaesthetic room CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION .  
Scene 8 — Consent (Step 9)  
Pain-free, alert, private room, speaks the language of the consultation fluently (no interpreter needed). You  
explain diagnosis, the keyhole operation, benefits, main risks with numbers, alternatives (watch and wait), and  
what happens if nothing is done. She asks: "Can I go home today?" — "Most people can, if you're comfortable  
and eating." Teach-back: "You'll take out my gallbladder through small cuts; there's a small chance of bile leak  
or needing a bigger cut." ✓ She signs.  
Scene 9 — Preparation (Step 10)  
Gown on; wedding ring taped; no dentures; stockings on; temperature 36.7 ✓; pregnancy test documented;  
consent present; ID band ✓; documents together. Site marking: not applicable (midline operation)  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . Checklist complete.  
Scene 10 — Theatre ready, transfer, handover (Steps 11–12)  
Theatre confirms the keyhole tower, CO2 and spare cylinder, suction, warming and anaesthetic machine check.  
NEWS2 0 → porter + nurse. SBAR to anaesthetist: "S: Ayesha Khan, 42, for laparoscopic cholecystectomy. B:  
symptomatic gallstones, otherwise well, no allergies, no regular medicines, previous caesarean without  
problems. A: obs normal, NEWS2 0, last food 21:00, water 06:30, LFTs normal, pregnancy test negative, ASA 1,  
airway Mallampati 2, consent signed. R: ready for theatre; no antibiotic indicated per protocol; stockings on."  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 105*

Scene 11 — WHO Sign In (Step 13)  
Question Answer  
Patient confirms identity, site, procedure, consent? Yes — she says her name, DOB, 'gallbladder keyhole', consent signed  
Site marked? Not applicable  
Anaesthesia machine and medication check complete? Yes  
Oximeter on and working? Yes, 99%  
Known allergy? No  
Difficult airway / aspiration risk? No (fasted, Mallampati 2)  
Risk of >500 mL blood loss? No  
Scene 12 — WHO Time Out (Step 14)  
Team introduces itself. Patient, site, procedure confirmed. Surgeon: 'standard lap chole, about 60 minutes,  
minimal blood loss.' Anaesthetist: 'no concerns.' Nurse: 'sterility indicators confirmed, tower working.'  
Antibiotic: 'not applicable.' Imaging: 'ultrasound report displayed.' → READY FOR INCISION. Scenario  
complete.  
Example Patient B — abnormal S1 (the 'elective' patient who isn't well  
today)  
Who: Mrs Farzana Ahmed, 61, booked for planned keyhole gallbladder removal. Identity confirmed; allergic to  
nothing.  
The clue in the history  
You ask the red-flag questions. "Have your eyes or skin looked yellow?" — "My daughter said my eyes looked a  
bit yellow last week. And my urine has been dark like tea." "Any fever or shivering?" — "No." "Pain?" — "Some  
aching under the ribs on and off."  
Teacher says:  
We found a possible abnormality. Yellow eyes and dark urine suggest jaundice — bilirubin backing up into the blood.  
In a gallstone patient, the most likely cause is a stone that has slipped into the main bile duct. This is NOT what she  
was booked for. Now check this: look at her eyes, measure vitals, and send liver tests.  
Measurement Value Zone Plain meaning  
Heart rate 84 Normal Calm  
Blood pressure 138/84 Normal Fine  
Breathing 16 Normal Fine  
Oxygen 97% Normal Fine  
Temperature 37.2 °C Normal No fever — so probably no infection in the duct (yet)  
Eyes Yellow tinge to the  
whites  
Abnormal Jaundice confirmed on examination  
Test Result Normal Zone and meaning  
Bilirubin 72 µmol/L (4.2 mg/dL) 3–21 HIGH (above 68) — bile is backing up  
ALP 380 U/L \~30–130 About 3× normal — bile duct cells irritated by blockage  
GGT 410 U/L below 40 Confirms the ALP is coming from the bile system  
ALT 160 U/L \~7–40 Moderately raised — liver cells irritated by backed-up bile  
WBC 9.8 4.0–11.0 Normal — no strong sign of infection  
CRP 12 below 5 Mildly raised  
INR 1.1 0.8–1.2 Clotting normal (long blockage could raise it)  
Lipase 45 below \~60 Pancreas not inflamed  
Ultrasound today Gallstones; common bile duct 9  
mm; no stone seen in duct  
duct ≤6 mm Duct WIDE — something is blocking it lower down  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 106*

Teacher says:  
This result makes us more concerned. The pattern is 'cholestatic' — the bile-duct numbers (ALP, GGT, bilirubin) are  
raised more than the liver-cell number (ALT), and the duct is wide. Using the bile-duct-stone risk tool: bilirubin above  
68 µmol/L plus a duct wider than 6 mm = HIGH risk of a stone in the duct CLINICAL REFERENCE — SOURCE BACKED (ASGE  
2019).  
Teacher says:  
Therefore the next step is: do not proceed with today's planned operation. If the surgeon removed the  
gallbladder today, the stone in the duct would remain and could cause infection (cholangitis) or pancreatitis. Call the  
senior surgeon. The usual plan is to clear the duct first with ERCP (a camera through the mouth that pulls the stone  
out), or to image it with MRCP first if the risk were only intermediate — then remove the gallbladder later (local  
practice CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ). Explain this to Mrs Ahmed, document,  
and safety-net: "If you get a fever, shivering or worse pain, come straight back — that could be an infection in the  
duct."  
Mrs Ahmed's decision path  
Jaundice symptoms in history → examine → yellow  
eyes  
Fever, shivers, low BP (cholangitis)? EMERGENCY: sepsis bundle + urgent ERCP  
YES  
NO  
Bilirubin above 68 AND duct above 6 mm? HIGH RISK duct stone → postpone today, senior,  
ERCP pathway  
YES  
NO  
(not reached for Mrs Ahmed)  
If missed: If the trainee doesn't ask about yellow eyes and doesn't look, the liver tests (if ordered) would still  
flag it. If the trainee ignores the abnormal results, the senior/anaesthetist character stops the case and the  
debrief explains: "A stone left in the duct can cause life-threatening infection."  
More S1 example patients (short walkthroughs)  
Patient S1-E — the planned patient with an inflamed gallbladder  
What we see: Mrs Rabia, 48. Today: constant pain for 2 days, felt hot overnight. T 38.4, HR 108, BP 118/72,  
RR 20, SpO2 97%. Murphy's sign positive. WBC 15.2, CRP 96. Ultrasound: wall 6 mm, fluid around gallbladder.  
How the trainee thinks  
1\. Fever + fast heart = the body is fighting infection/inflammation.  
2\. Murphy's positive + right-upper tenderness = local gallbladder inflammation (TG18 group A).  
3\. Raised WBC and CRP = whole-body inflammation (group B).  
4\. Ultrasound wall thick + fluid = scan evidence (group C) → definite acute cholecystitis.  
5\. Grade: WBC 15.2 (not above 18), symptoms under 72 h, no lump, no organ failure → Grade I (mild).  
6\. Decision: she's not suitable for today's planned list as a routine case — she now has an acute illness. Senior  
surgeon reviews; antibiotics; she moves to the acute pathway. Early keyhole surgery (often within 72 hours;  
NICE: within a week) is usually the plan CLINICAL REFERENCE — SOURCE BACKED . NEWS2 = HR 108 (1) + RR 20 (0)  
\+ T 38.4 (1) = 2.  
Patient S1-B — high blood pressure  
What we see: Mr Tariq, 58, on BP tablets. No GP BP readings in the letter. First reading 172/104. Repeat after  
5 minutes rest: 168/102. No headache, chest pain or breathlessness.  
How the trainee thinks  
1\. The reading is high (grade 2). Is it real? Repeated properly — yes, still about 168/102.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 107*

2\. Any symptoms of organ damage? No.  
3\. Is it 180/110 or more? No.  
4\. Rule (AAGBI/BHS): below 180/110 in clinic → surgery can proceed. Tell the anaesthetist; ask the GP to  
review BP after surgery CLINICAL REFERENCE — SOURCE BACKED .  
5\. Variant B2: readings 188/114 and 186/112 → postpone and refer for control; do NOT try to drop it rapidly  
today.  
Patient S1-C — diabetes  
What we see: Mrs Nasreen, 55, type 2 diabetes on metformin and gliclazide. HbA1c 58 mmol/mol (2 months  
ago). Finger-prick sugar 7.8.  
How the trainee thinks  
1\. HbA1c 58 is below 69 → diabetes acceptably controlled for planned surgery CLINICAL REFERENCE — SOURCE  
BACKED .  
2\. Sugar 7.8 is inside the target 6–10.  
3\. Medicine plan: metformin as normal (one meal missed); omit gliclazide today (it could cause low sugar  
while fasting) CLINICAL REFERENCE — SOURCE BACKED .  
4\. Put her first on the list if possible; check sugar at least hourly; tell anaesthetist. → Proceed.  
5\. Variant C2: HbA1c 82, sugar 13.4, ketones 0.4 → HbA1c above 69 and sugar high → discuss with  
anaesthetist/diabetes team; usual answer: postpone and optimise CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL  
/ CLINICIAN CONFIRMATION .  
Patient S1-D — on a blood thinner  
What we see: Mr Imran, 67, atrial fibrillation (irregular heartbeat) on apixaban. Kidney function eGFR 68.  
How the trainee thinks  
1\. Ask exactly: "When did you last take apixaban?"  
2\. (a) "Three days ago, as the pre-op nurse told me." → stopped long enough (needs 1–2 days with good  
kidneys) → proceed; restart after surgery per surgeon.  
3\. (b) "This morning — I forgot." → the drug is still active → bleeding risk. The surgeon/anaesthetist usually  
defer and re-list; reversing a blood thinner is not used simply to make a planned operation possible CLINICAL  
INTERPRETATION .  
4\. Pulse irregular but rate 78 → known, controlled AF → fine; ECG to confirm.  
Patient S1-G — ate breakfast  
What we see: Ms Sana, 39. "I had toast and tea with milk at 07:00." Planned surgery at 09:00.  
How the trainee thinks  
1\. Toast + milky tea = light meal (milk counts as food) → needs 6 hours → not safe until 13:00.  
2\. Tell the anaesthetist immediately and honestly.  
3\. Likely outcome: moved to the end of the list (after 13:00) or postponed CLINICAL REFERENCE — SOURCE BACKED .  
Patient S1-H — possible pregnancy  
What we see: Ms Hina, 29. Last period 7 weeks ago. Urine pregnancy test: positive.  
How the trainee thinks  
1\. She is pregnant. Planned gallbladder surgery is usually postponed; the senior surgeon, anaesthetist and  
obstetric team decide.  
2\. If her gallbladder trouble became urgent during pregnancy, the second trimester is the preferred time for  
surgery where possible CLINICAL REFERENCE — SOURCE BACKED (SAGES).  
3\. Handle sensitively and privately; she may not have known.  
Patient S1-J — the 'high potassium' that wasn't  
What we see: Mr Kamran, 50, well. Potassium 6.4 'haemolysed'. ECG normal. Kidney function normal.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 108*

How the trainee thinks  
1\. 6.4 sits in the 'moderately high' zone.  
2\. But: well patient + normal ECG + normal kidneys + lab says the cells burst → probably false.  
3\. Repeat urgently; tell anaesthetist. Repeat = 4.6 → proceed.  
Patient S1-K — anaemia  
What we see: Mrs Shazia, 52, tired. Hb 98 g/L, MCV 72 (small cells).  
How the trainee thinks  
1\. Moderate anaemia with small cells → probably iron deficiency.  
2\. Why? Diet/periods, or slow bleeding from the gut — must be investigated at her age.  
3\. Planned operation → senior/anaesthetist discussion; likely postpone, iron studies, treat cause  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Patient S1-L — new irregular heartbeat  
What we see: Mr Aslam, 70. Palpitations since morning. HR 128, irregularly irregular. BP 132/84. ECG: atrial  
fibrillation (new).  
How the trainee thinks  
1\. New AF with fast rate → heart is not pumping efficiently and stroke risk rises.  
2\. Planned surgery is usually deferred (senior/anaesthetist decide); medical team for rate control and a  
blood-thinner decision; look for triggers (salts, thyroid, infection) CLINICAL REFERENCE — SOURCE BACKED .  
Patient S1-M — sleep apnoea risk  
What we see: Mr Rizwan, 44, BMI 41, loud snoring, wife says he stops breathing at night. STOP-BANG 6. SpO2  
95%.  
How the trainee thinks  
1\. High risk of obstructive sleep apnoea and BMI 40+ → ASA 3.  
2\. Tell anaesthetist: they may proceed with special precautions (monitoring after surgery, careful painkillers)  
or request a sleep study first CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Patient S1-I — penicillin anaphylaxis  
What we see: Mrs Amna, 45, penicillin caused throat swelling and collapse in 2015.  
How the trainee thinks  
1\. Severe immediate allergy → allergy band, record reaction.  
2\. If an antibiotic is needed (she is low-risk — probably not), choose per hospital penicillin-allergy rule  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 109*


# Part 13 — S2 as a story: an emergency appendix

Teacher says:  
This story feels different. In S1 we had time and a known diagnosis. Here, the patient is in pain, the diagnosis isn't  
certain, and the illness is getting worse while we work. So we investigate and treat at the same time, and we  
cannot wait for perfect conditions. Think of it like fixing a leak while the water is still running.  
Example Patient C — emergency S2 (classic appendicitis)  
Who: Mr Hamza Ali, 19, student. Why here: brought to the Emergency Department by his brother at 10:00  
with tummy pain since last night.  
Scene 1 — Arrival and triage (Step 1)  
Triage note: '19M, abdominal pain 14 h, HR 108, T 37.9, pain 8/10 — very urgent (see within 10 min)'. He's  
curled on the trolley holding his lower right tummy.  
Teacher says:  
"Okay Ruhan, the patient has arrived and he's in a lot of pain. First, the same identity check as always — but quickly."  
He says "Hamza Ali, 2nd of June 2007"; wristband matches. Allergies: "None." Then: don't make him wait in pain  
while you do paperwork. Check he's not allergic, and ask the nurse for pain relief now. Pain relief does not hide the  
diagnosis CLINICAL REFERENCE — SOURCE BACKED .  
Scene 2 — First vital signs (Step 3, done early in emergencies)  
Measurement Value Zone Plain meaning  
Heart rate 108, regular Fast (91–110) Body stressed — pain? fever? dehydration? infection?  
Blood pressure 128/76 Normal Circulation coping  
Breathing 18 Normal OK  
Oxygen 98% on air Normal Lungs fine  
Temperature 37.9 °C Normal-high  
(fever starts at  
38.0)  
Mild inflammation  
Alertness Alert Normal —  
Pain 8/10 Severe Needs pain relief now  
NEWS2 HR 108 → 1; others 0  
→ total 1  
Low Recheck at least every 4–6 h (we'll do more often because of  
pain/fluids)  
Teacher says:  
The heart rate is high. That doesn't mean a heart problem — possible causes are pain, anxiety, fever, dehydration or  
infection. Everything else is fine, and he's in severe pain. So the likeliest explanation is pain + inflammation. Treat the  
pain, give fluids, and re-measure. If the heart rate stays high after that, think harder.  
Scene 3 — First treatment while we work (Step 8 early)  
What you do in the first 15–30 minutes  
1\. Drip (cannula) in; take blood at the same time (FBC, CRP, kidneys/salts, group & save per policy).  
2\. Pain relief: request analgesia: IV paracetamol is a typical first step (SmPC example in Part 8), plus a  
stronger painkiller titrated by the clinician PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED — the clinician  
prescribes and the nurse administers.  
3\. Anti-sickness medicine if nauseated (he vomited once).  
4\. Start IV fluids (he hasn't eaten or drunk properly since last night).  
5\. Keep him nil by mouth (he may need surgery).  
6\. Re-check vital signs after treatment.  
Scene 4 — The history (Step 2), now he's more comfortable  
"It started around my belly button around 8 last night, then moved down here \[lower right\] by the morning. I  
don't want to eat. I threw up once. Hurts when the car went over bumps." No urine symptoms, no diarrhoea, no  
testicle pain. No illnesses, no medicines, no previous operations. Last food: chicken and rice at 19:00 last night;  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 110*

last drink: water at 06:00.  
Teacher says:  
That's the classic appendicitis story: pain moving from the belly button to the lower right, loss of appetite, vomiting  
after the pain, pain with movement. But we must still rule out look-alikes. For a young man, check the testicles  
(twisted testicle can cause tummy pain), urine (stones/infection), and ask about bowel symptoms.  
Scene 5 — Examination (Step 4)  
Tender lower right tummy (McBurney's point), mild guarding, rebound medium, Rovsing's sign positive. Rest of  
tummy soft. Testicles normal. → Localised peritonitis in the lower right — fits appendicitis, not burst.  
Scene 6 — Tests and results (Steps 6–7)  
Test Result Normal Meaning  
WBC 13.6 4.0–11.0 Raised — fighting infection  
Neutrophils 82% 40–75% First-responder cells high → bacterial  
CRP (20 h of symptoms) 38 mg/L below 5 Moderate inflammation  
Haemoglobin 148 g/L 130–170 Normal — no bleeding  
Creatinine 82 µmol/L 60–110 Kidneys fine  
Potassium 3.9 3.5–5.3 Normal  
Urine dipstick Ketones +, otherwise  
negative  
Negative Not eating (starvation ketones); no infection/blood  
Lactate Not needed (no sepsis signs) — —  
Appendicitis scores (calculated in Part 7.1): AIR 7 (indeterminate), AAS 19 (high).  
Teacher says:  
Here's a real-world wrinkle. If the hospital uses AIR, the score says 'unsure — get a scan'. If it uses AAS, it says 'high  
probability'. The WSES guideline says young patients with high scores may not need a scan in some cases, but many  
hospitals scan anyway. In our draft (AIR default) the trainee requests an ultrasound CLINICAL INTERPRETATION  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Ultrasound (60–120 min later): 'Non-compressible appendix 9 mm, surrounding fat inflamed, no free fluid, no  
abscess.' → Positive — simple (uncomplicated) appendicitis.  
Scene 7 — Decision (Step 8)  
Hamza's decision path  
Story + exam + raised WBC/CRP + positive  
ultrasound  
Emergency signs (NEWS2 7+, low BP, sepsis)? Sepsis branch  
YES  
NO  
Complicated on scan (perforation, abscess)? Urgent theatre, longer antibiotics  
YES  
NO  
Simple appendicitis → call senior surgeon (SBAR) →  
decision: laparoscopic appendectomy  
Antibiotics now (local choice) → anaesthetist review  
→ consent → theatre promptly (timing per surgeon;  
WSES 2025: within 24 h of admission when surgery is  
indicated)  
Call the senior: "S: Hamza Ali, 19, in ED with probable appendicitis. B: 14 hours of pain moving to the right iliac  
fossa, anorexia, one vomit, no history. A: HR 108 → 96 after analgesia and fluid, T 37.9, NEWS2 1; localised RIF  
peritonism; WBC 13.6, CRP 38; AIR 7; ultrasound shows uncomplicated appendicitis. R: I'd like you to review  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 111*

him for laparoscopic appendectomy; I've started antibiotics per protocol and kept him nil by mouth."  
Scene 8 — Anaesthetic review and fasting  
Food at 19:00 last night (15 hours ago) and water at 06:00. Even though the time looks enough, he's an  
emergency patient in pain who has had strong painkillers, so the anaesthetist still treats him as a possible 'full  
stomach' and plans RSI CLINICAL REFERENCE — SOURCE BACKED . ASA 1E.  
Scene 9 — Consent (Step 9)  
Pain now 3/10, alert → has capacity. Explained: appendicitis; keyhole removal; risks (infection, abscess,  
bleeding, conversion, bowel injury rare); alternative: antibiotics alone for simple appendicitis — about 7 in 10  
avoid surgery at 3 months (CODA: 29% needed surgery by 90 days) but about 4 in 10 have it come back within  
5 years (APPAC: 39.1%). Hamza chooses surgery. Teach-back ✓. Signed.  
Scene 10 — Preparation, handover, Sign In, Time Out  
Checklist (fast but complete): gown, no jewellery, antibiotic time recorded (e.g. 12:10), fluids running, fasting  
times recorded. Handover to anaesthetist includes: aspiration risk, antibiotic name and time, fluids given,  
latest NEWS2. Sign In: aspiration risk YES — RSI equipment and suction ready. Time Out at 12:55:  
antibiotic given 45 minutes ago (within 60) ✓; ultrasound displayed ✓. → READY FOR INCISION.  
Example Patient D — the deteriorating patient (watch the numbers  
change)  
Who: Mr Usman, 34. Three days of tummy pain he 'tried to sleep off'. Arrives at 14:00.  
Time HR BP RR SpO2 Temp Alert? NEWS2 What's happening  
14:00 102 124/78 20 97% 38.3 A 2 Infection, coping. Workup started.  
14:45 (no  
fluids/antibiotics  
given yet)  
116 112/70 22 96% 38.9 A 5 Something has changed. Heart faster, BP  
lower, breathing faster, fever higher.  
15:15 128 92/58 26 94% 39.2 A 10 Sepsis with poor circulation — EMERGENCY  
15:45 if nobody  
acts  
138 82/48 30 91% 39.4 Confused 16 Septic shock — near collapse  
Teacher says:  
At 14:45, something has changed. No single number screams 'disaster' yet, but look at the trend: heart rate up 14,  
BP down 12, breathing up, fever up — all pointing the same way. NEWS2 has jumped from 2 to 5, which means  
'urgent review'. A patient with three days of pain, fever and a rising heart rate may have a burst appendix.  
What the trainee should notice and do at 14:45  
1\. Notice the trend (the game shows the chart; the nurse may say "His NEWS2 is 5 now").  
2\. Go and look at him: sweaty? tummy now tender everywhere and rigid? → suggests perforation with  
spreading peritonitis.  
3\. Call for help: urgent senior review (NEWS2 5–6).  
4\. Start the sepsis bundle: blood cultures, lactate, IV antibiotics, fluid bolus (e.g. 500 mL quickly) and  
re-check, assess breathing and use oxygen per protocol if below the target range, measure urine.  
5\. Request urgent imaging (CT) if senior wants it — with a nurse escort and monitor because he's unstable.  
Results: lactate 3.8 (raised — organs short of oxygen); WBC 19.8; CRP 210; creatinine 142 (kidneys struggling).  
CT: perforated appendix with free fluid.  
At 15:15 (NEWS2 10 — emergency)  
1\. Emergency call: senior surgeon + anaesthetist now; critical-care team aware.  
2\. ABCDE: airway OK; breathing fast — oxygen; circulation — second drip, more fluid boluses with re-checks,  
antibiotics if not yet given (NICE NG253: within 1 hour for the high-risk tier), catheter for urine output;  
disability — alert, check sugar; exposure — rigid tummy.  
3\. This patient needs the source of infection removed urgently (source control = surgery) once resuscitation  
has started CLINICAL REFERENCE — SOURCE BACKED .  
4\. Consent: if he still has capacity, a brief but complete discussion; if confused, senior follows emergency  
route CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 112*

5\. Handover: lactate, fluids given, antibiotic time, NEWS2, kidney injury, aspiration risk.  
After the correct action  
BP rises to 108/64 after fluids, HR 112, lactate falls to 2.4 on repeat, urine starts flowing. He goes to theatre urgently.  
Debrief praises early recognition of the trend.  
If the trainee fails to act  
By 15:45 he becomes confused with BP 82/48 (septic shock). Medicines to support BP and intensive care are needed;  
the debrief shows the timeline: 'sepsis criteria met 14:45; antibiotics given 15:55 (70 minutes late).' If inaction  
continues, the case ends with a safety debrief.  
More S2 example patients (short walkthroughs)  
Patient S2-B — differential diagnosis / diagnostic diversion: it may NOT be appendicitis  
(ectopic pregnancy)  
What we see: Ms Sadia, 24. Lower-right pain, shoulder-tip pain, dizzy. HR 112, BP 98/60. Last period 7 weeks  
ago. Urine pregnancy test POSITIVE. Hb 104.  
How the trainee thinks  
1\. Positive pregnancy test + tummy pain: ectopic pregnancy must be considered and excluded — a  
differential diagnosis (a mimic of appendicitis, not a complication of it) CLINICAL REFERENCE — SOURCE BACKED  
(NICE NG126).  
2\. Shoulder-tip pain = blood in the tummy irritating the diaphragm.  
3\. Fast heart + lowish BP + low Hb = she may be bleeding inside.  
4\. STOP the appendicitis pathway. Emergency: call for help, two large drips, crossmatch blood, gynaecology  
NOW, keep nil by mouth.  
5\. If the trainee never did the pregnancy test: hard safety failure in the debrief.  
Patient S2-D — the scan didn't answer the question  
What we see: Ms Zainab, 28. Pain 5/10; T 37.4; HR 92; WBC 10.9; CRP 22; pregnancy test negative; AIR 5.  
Ultrasound: 'appendix not visualised, no free fluid.'  
How the trainee thinks  
1\. AIR 5 = intermediate.  
2\. 'Not visualised' = we don't know — NOT negative.  
3\. Options (senior decides CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ): CT scan, or admit,  
keep nil by mouth, pain relief, and re-examine + re-score in 6–12 hours; consider gynaecology causes  
(ovary).  
Patient S2-E — older patient  
What we see: Mr Akbar, 68. Lower-right pain, weight loss recently. T 37.6, HR 96, Hb 112, CRP 64.  
How the trainee thinks  
1\. In older people, bowel cancer or diverticulitis can mimic appendicitis; weight loss + anaemia is a warning.  
2\. CT is preferred before deciding on surgery CLINICAL INTERPRETATION .  
Patient S2-F — ate a big meal 2 hours ago  
What we see: Mr Bilal, 22, otherwise like Hamza. Ate biryani 2 hours ago.  
How the trainee thinks  
1\. Full stomach — record exact time and food.  
2\. Tell anaesthetist; they plan RSI.  
3\. Emergency surgery is NOT cancelled for this; it may be timed sensibly if he's stable CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 113*

Patient S2-G — dehydrated with kidney injury  
What we see: Mrs Rukhsana, 45, vomiting 2 days. HR 116, BP 104/66, dry mouth, little urine. Urea 12.4,  
creatinine 160 (usual 85).  
How the trainee thinks  
1\. Kidney injury stage 1 (creatinine \~1.9× her normal) from dehydration.  
2\. IV fluids with re-checks; stop kidney-harming medicines (no ibuprofen); measure urine output; repeat  
bloods.  
3\. Surgery proceeds once she's resuscitated — the infection is part of the cause.  
Patient S2-H — pregnant with appendicitis  
What we see: Mrs Mehwish, 30, 20 weeks pregnant. Right-side pain higher than usual (the growing womb  
pushes the appendix up). T 37.8, HR 102, WBC 14 (pregnancy raises WBC normally).  
How the trainee thinks  
1\. Obstetric team involved early.  
2\. Ultrasound first; MRI if unclear; avoid CT as first choice.  
3\. Keyhole appendectomy is acceptable in pregnancy CLINICAL REFERENCE — SOURCE BACKED (SAGES).  
4\. NEWS2 not used (maternity score instead). Tilt to the left side when lying (womb presses on a big vein).  
Patient S2-I — 16 years old  
What we see: Hamid, 16, classic appendicitis.  
How the trainee thinks  
1\. Minor (under 18): follow the applicable law for consent by a parent/guardian and/or a competent young  
person; involve Hamid in the discussion CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Patient S2-J — looks like a urine infection  
What we see: Ms Aqsa, 26. Burning when passing urine + lower-right pain. Dipstick: leucocytes 2+, nitrite  
negative, trace blood. Pregnancy negative.  
How the trainee thinks  
1\. Leucocytes without nitrites + lower-right tenderness → don't stop at 'UTI'. An inflamed appendix next to the  
bladder can cause this.  
2\. Send urine culture; continue appendicitis assessment (score, imaging).  
Patient S2-K — penicillin allergy  
What we see: Mr Faisal, 21. 'Allergic to penicillin — I got a rash as a child.'  
How the trainee thinks  
1\. Mild, delayed childhood rash → many guidelines allow cefazolin-type antibiotics; follow hospital rule  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
2\. Record allergy precisely.  
Patient S2-L — refuses surgery  
What we see: Mr Naveed, 37, simple appendicitis, capacity intact. "Can't I just take antibiotics?"  
How the trainee thinks  
1\. For simple appendicitis without a stone inside, antibiotics-only is a recognised option with 39.1% recurrence  
over 5 years (APPAC trial).  
2\. Explain honestly; senior discussion; respect his choice; document; antibiotics plan; safety-net ("come back  
if pain worsens, fever, vomiting").  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 114*


# Part 14 — WHAT IF something goes wrong?

Every scenario follows the same chain: what happened → what it might mean → what to check → what  
to do → who to call → what happens next. The sections are grouped: vital signs, blood tests, heart,  
pregnancy/allergy/medicines, fasting, scans and results, communication/consent, and system failures. After  
that, Part 14B covers what to do when several problems happen at once.  
Reading the 'Correct action' rows (role convention)  
Wherever a row says give, treat, replace or start a medicine, fluid or oxygen, read it as: the trainee recognises the  
need and initiates the workflow (asks, flags on the chart, calls the senior/anaesthetist); a qualified clinician  
confirms the drug, dose and route; a nurse or clinician administers. Emergency first aid steps (calling for help,  
positioning, starting oxygen per protocol) are initiated by whoever is present. No row teaches independent  
patient-specific prescribing.  

## 14.0 The universal emergency method: ABCDE

Teacher says:  
Whenever a patient suddenly gets worse, doctors use the same order of checks, called ABCDE. You fix each problem  
as you find it before moving on, because the earlier letters kill faster.  
Letter Check Example problem Example fix  
A — Airway Can they talk? Noisy breathing? Swelling? Swollen tongue (anaphylaxis), vomit Call help, position, suction,  
adrenaline if allergic  
B — Breathing Breathing rate, oxygen level, chest sounds Oxygen 89%, fast breathing Verify reading, assess, oxygen to  
the patient's target range per  
protocol  
C — Circulation Heart rate, BP, cap refill, urine, bleeding BP 84/50 Drips, fluids, stop bleeding,  
antibiotics if septic  
D — Disability Alertness, blood sugar, pupils, pain Confused, sugar 2.8 Treat low sugar  
E — Exposure Temperature, look at whole body (tummy,  
rash, wounds)  
Rigid tummy, rash Treat cause; keep warm  
Where this comes from: Resuscitation Council UK ABCDE approach (2021).  

## 14.1 Vital-sign problems


### What if blood pressure is too low? (top number 90 or less)

Trigger / what the  
trainee sees  
Monitor shows 86/52; patient pale, sweaty, dizzy.  
What it might mean Shock: from infection (sepsis), bleeding (e.g. ectopic, blood thinner), severe allergy, dehydration, heart problem, or  
a medicine (opioid, BP tablet). Or a faulty reading.  
What to check next Repeat manually/right cuff; look at the patient; heart rate (fast?); temperature; any recent medicine  
(rash/wheeze?); pregnancy test; Hb; lactate; tummy (rigid?).  
Correct action Call for help; lie flat; assess airway/breathing and start oxygen per protocol to the patient's target; two drips;  
request a fluid bolus (e.g. 500 mL crystalloid, NICE CG174; the clinician decides volume) and re-check  
EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ; treat cause (antibiotics if  
septic; adrenaline if anaphylaxis; blood if bleeding).  
Incorrect action Giving a BP tablet or opioid; sending to scan unmonitored; waiting for the next routine obs  
Missed abnormality →  
consequence  
Progresses to shock: HR rises, confusion, urine stops; lactate rises  
Escalation (who to call) Senior surgeon + anaesthetist immediately; gynaecology if pregnancy positive; critical care if not responding.  
Expected patient /  
monitor response  
After fluid bolus SBP rises above 100 and HR falls within minutes if hypovolaemic; septic shock may need  
vasopressors  
What happens next (S1 /  
S2 consequence)  
If BP rises with fluid → continue resuscitation and plan source control. If not → medicines to support BP and  
intensive care (clinician-controlled). S1: planned operation cancelled; emergency management. S2: resuscitate  
then urgent surgery if the appendix is the cause.  
Source RCP NEWS2; Sepsis-3; SSC 2021; NICE NG253  
Clinician validation Alarm thresholds CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 115*


### What if blood pressure is extremely high? (180/110 or more)

Trigger / what the  
trainee sees  
Monitor shows 192/116.  
What it might mean Untreated/poorly treated high BP, pain, anxiety, missed tablets; rarely a hypertensive emergency.  
What to check next Repeat after 5 minutes' rest with correct technique; ask about headache, chest pain, breathlessness, vision,  
weakness; check usual tablets taken.  
Correct action No symptoms: S1 → postpone and refer for control (don't rush BP down). With symptoms → emergency medical  
review.  
Incorrect action Rapidly lowering BP on the morning of planned surgery; cancelling after one unrepeated reading  
Missed abnormality →  
consequence  
Rare: stroke/heart event; more commonly an avoidable cancellation  
Escalation (who to call) Anaesthetist; medical team if symptoms.  
Expected patient /  
monitor response  
Repeat readings often lower after rest; patient rebooked  
What happens next (S1 /  
S2 consequence)  
S1 rebooked once BP controlled below 160/100 by GP. S2: treat pain, tell anaesthetist; emergency surgery not  
delayed for BP alone unless organ damage.  
Source AAGBI/BHS 2016; ESH 2023  
Clinician validation —  

### What if the heart rate is very high? (above 130)

Trigger / what the  
trainee sees  
Monitor 138; patient anxious, maybe palpitations.  
What it might mean Sepsis, bleeding, dehydration, severe pain, fast abnormal rhythm (AF/SVT), lung clot, fever, thyroid.  
What to check next Feel pulse: regular or irregular; ECG; BP; temperature; breathing/oxygen; Hb; lactate; pain score.  
Correct action ABCDE; treat cause (pain relief, fluids, antibiotics); ECG-guided rhythm treatment by senior/medical team.  
Incorrect action Treating the number with a rate-slowing drug before finding the cause (e.g. hypovolaemia)  
Missed abnormality →  
consequence  
Hidden sepsis/bleeding progresses to shock  
Escalation (who to call) Senior + anaesthetist (NEWS2 3 for this alone = urgent).  
Expected patient /  
monitor response  
HR falls with analgesia/fluids if pain/dehydration; persists if sepsis/AF  
What happens next (S1 /  
S2 consequence)  
Settles → continue. Persistent → investigate further; S1 usually deferred after review.  
Source RCP NEWS2; RCUK tachycardia algorithm 2021  
Clinician validation —  

### What if the heart rate is very slow? (40 or below)

Trigger / what the  
trainee sees  
Monitor 38; patient dizzy.  
What it might mean Heart block, beta-blocker/digoxin, very high potassium, hypothermia.  
What to check next ECG; potassium; medicines; BP; consciousness.  
Correct action ABCDE; stop causative drugs; emergency medicines/pacing per resuscitation team.  
Incorrect action Giving more beta-blocker/opioid; proceeding to anaesthesia  
Missed abnormality →  
consequence  
Collapse on induction  
Escalation (who to call) Senior + anaesthetist + medical/cardiology urgently.  
Expected patient /  
monitor response  
Improves when cause removed; pacing if heart block  
What happens next (S1 /  
S2 consequence)  
S1 usually deferred (senior/anaesthetist decide); S2 surgery only once rhythm managed.  
Source RCUK bradycardia algorithm 2021  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 116*


### What if the patient has a fever? (38.0 °C or more)

Trigger / what the  
trainee sees  
Thermometer 38.6.  
What it might mean S1: inflamed gallbladder or infected bile ducts (if jaundiced); urine/chest infection. S2: appendicitis, worse if 39+  
(possible perforation/abscess).  
What to check next Heart rate, BP, breathing (sepsis?); tummy exam (Murphy's / generalised tenderness); jaundice; urine dipstick;  
chest; WBC/CRP; blood cultures if 38.5+ or rigors; lactate.  
Correct action Paracetamol; fluids if dry; antibiotics according to cause; sepsis bundle if criteria met.  
Incorrect action S1: proceeding with the planned operation; treating fever only with paracetamol without looking for a source  
Missed abnormality →  
consequence  
Acute cholecystitis/cholangitis missed; sepsis develops  
Escalation (who to call) Senior (in S1 the pathway changes, so yes).  
Expected patient /  
monitor response  
Temperature falls with paracetamol but source signs remain → pathway changes  
What happens next (S1 /  
S2 consequence)  
S1: removed from planned list → acute pathway. S2: continue towards surgery; if complicated, faster.  
Source TG18; NICE NG253  
Clinician validation —  

### What if the patient is cold? (36.0 °C or less)

Trigger / what the  
trainee sees  
Temperature 35.6.  
What it might mean Cold environment, long fasting, older age — or severe sepsis (ominous if with other bad signs).  
What to check next Other vital signs; sepsis screen if unwell.  
Correct action Warm (forced-air blanket); don't send to theatre until 36.0 unless urgent CLINICAL REFERENCE — SOURCE BACKED  
(NICE CG65).  
Incorrect action Transferring a cold patient for planned surgery  
Missed abnormality →  
consequence  
More bleeding and wound infection; shivering  
Escalation (who to call) Anaesthetist; senior if septic.  
Expected patient /  
monitor response  
Warms \~0.5–1 °C per hour with forced air CLINICAL INTERPRETATION  
What happens next (S1 /  
S2 consequence)  
Warmed → continue.  
Source NICE CG65  
Clinician validation —  

### What if oxygen saturation is low? (below 94%)

Trigger / what the  
trainee sees  
Monitor 91%; patient breathing fast.  
What it might mean Probe error; aspiration (vomit in lungs); chest infection; fluid overload after many boluses; lung clot; too much  
opioid (slow breathing); collapsed lung areas from pain.  
What to check next Check probe and wave; look at patient (colour, effort); breathing rate; listen to chest; fluid balance; opioids given;  
blood gas; chest X-ray.  
Correct action Look at the patient and assess airway/breathing; check probe and waveform; if the low reading is real, sit up and  
start oxygen per protocol to the patient's target range (94–98%, or 88–92% if at risk of hypercapnic failure); find  
the cause (stop fluids if overloaded per senior; stimulate if over-sedated — antidote by clinician); reassess within  
minutes (process in Part 4.5).  
Incorrect action Ignoring because 'probe is wrong' without checking; high-flow O2 to a CO2 retainer without monitoring  
Missed abnormality →  
consequence  
Hypoxia worsens; confusion; arrest  
Escalation (who to call) Anaesthetist/senior urgently; emergency team if 88% or below.  
Expected patient /  
monitor response  
SpO2 returns to target with appropriate oxygen within minutes if cause treatable  
What happens next (S1 /  
S2 consequence)  
Improves with modest oxygen → continue, anaesthetist informed. Worsens → airway team; case may stop.  
Source BTS 2017; RCP NEWS2  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 117*

Clinician validation —  

### What if breathing is very fast? (25 or more)

Trigger / what the  
trainee sees  
RR counted at 28.  
What it might mean Sepsis, acid build-up (DKA, poor circulation), lung problem, pain, anxiety.  
What to check next Oxygen, temperature, BP, blood gas (acid), lactate, sugar/ketones, chest exam.  
Correct action Treat cause; sepsis bundle if infection.  
Incorrect action Giving a sedative to 'calm' the patient  
Missed abnormality →  
consequence  
Missed sepsis/acidosis/PE  
Escalation (who to call) Senior urgently (NEWS2 3).  
Expected patient /  
monitor response  
RR falls as cause treated  
What happens next (S1 /  
S2 consequence)  
Re-assess every 15–30 min.  
Source RCP NEWS2; Sepsis-3  
Clinician validation —  

### What if breathing is very slow? (8 or less)

Trigger / what the  
trainee sees  
RR 7, very sleepy, tiny pupils.  
What it might mean Too much opioid painkiller; severe brain problem.  
What to check next Oxygen level, alertness, drug chart, sugar.  
Correct action Wake/stimulate; oxygen; support airway; naloxone antidote PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ;  
stop further opioids.  
Incorrect action Giving further opioid for pain  
Missed abnormality →  
consequence  
Respiratory arrest  
Escalation (who to call) Emergency/anaesthetist immediately.  
Expected patient /  
monitor response  
RR and alertness improve after stimulation/antidote  
What happens next (S1 /  
S2 consequence)  
Recovers → review pain plan.  
Source RCP NEWS2; BNF (naloxone)  
Clinician validation Antidote dose PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED  

### What if the patient becomes confused?

Trigger / what the  
trainee sees  
"Where am I?" — new confusion.  
What it might mean Low sugar, low oxygen, sepsis, low BP, opioids, low sodium, alcohol withdrawal, stroke.  
What to check next Finger-prick sugar FIRST, oxygen, BP, temperature, drug chart, sodium, face/arm weakness.  
Correct action Treat what's found (sugar below 4 → glucose); sepsis bundle if infection; reassess capacity (consent may be invalid  
now).  
Incorrect action Assuming it is 'just pain medicine' without checking glucose  
Missed abnormality →  
consequence  
Hypoglycaemia/sepsis progresses; invalid consent  
Escalation (who to call) Senior urgently (NEWS2 3); medical team if stroke signs.  
Expected patient /  
monitor response  
Rapid improvement after glucose if hypoglycaemic  
What happens next (S1 /  
S2 consequence)  
Recovered → re-confirm consent.  
Source RCP NEWS2 (new confusion = 3); JBDS hypoglycaemia  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 118*


### What if pain gets much worse or the tummy becomes rigid?

Trigger / what the  
trainee sees  
Pain 9/10 despite strong painkiller; whole tummy hard as a board.  
What it might mean Perforation (burst appendix/gallbladder), ruptured ectopic, twisted ovary, blocked/ischaemic bowel.  
What to check next Re-examine; vital signs; lactate; pregnancy test; Hb; imaging.  
Correct action Pain relief; nil by mouth; fluids; antibiotics; prepare for urgent surgery.  
Incorrect action Giving more analgesia without re-examining or calling the senior  
Missed abnormality →  
consequence  
Perforation/peritonitis becomes sepsis  
Escalation (who to call) Senior immediately.  
Expected patient /  
monitor response  
Pain may ease with analgesia but rigidity persists  
What happens next (S1 /  
S2 consequence)  
Surgery expedited.  
Source WSES 2025; TG18  
Clinician validation —  

## 14.2 Blood-test problems


### What if potassium is high? (above 5.3)

Trigger / what the  
trainee sees  
K 6.2.  
What it might mean Faulty (haemolysed) sample; kidney injury; medicines (ACE inhibitors, potassium-sparing water tablets, potassium  
supplements); acid in blood.  
What to check next Lab comment; ECG (peaked T waves?); creatinine; medicines; repeat.  
Correct action 5.5–5.9 (mild, UKKA): ECG, repeat, review drugs. 6.0–6.4: urgent repeat + ECG, treat if real. 6.5+ or ECG changes:  
emergency treatment (calcium to protect the heart; insulin-glucose) PATIENT-SPECIFIC DOSE — CLINICIAN  
CONTROLLED ; heart monitor.  
Incorrect action Emergency treatment of an obviously haemolysed sample with normal ECG; ignoring a real 6.5+  
Missed abnormality →  
consequence  
Arrhythmia/cardiac arrest (especially at induction)  
Escalation (who to call) Senior + anaesthetist (6.0+); emergency team if ECG changes.  
Expected patient /  
monitor response  
K falls \~0.6–1.0 mmol/L after insulin–glucose (UKKA) — repeat level  
What happens next (S1 /  
S2 consequence)  
S1 usually deferred (senior/anaesthetist decide) until normal; S2 corrected before/at anaesthesia.  
Source UK Kidney Association 2023  
Clinician validation Treatment doses PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED  

### What if potassium is low? (below 3.5)

Trigger / what the  
trainee sees  
K 2.8 after 2 days of vomiting.  
What it might mean Vomiting, diarrhoea, water tablets.  
What to check next ECG (flat T, U waves); magnesium; ongoing losses.  
Correct action Replace (by mouth if mild; through drip if 2.5–2.9 or vomiting, rate PATIENT-SPECIFIC DOSE — CLINICIAN  
CONTROLLED ); replace magnesium too.  
Incorrect action Fast undiluted IV potassium (dangerous); not checking magnesium  
Missed abnormality →  
consequence  
Arrhythmia under anaesthesia  
Escalation (who to call) Anaesthetist; senior if below 2.5.  
Expected patient /  
monitor response  
K rises on repeat after replacement  
What happens next (S1 /  
S2 consequence)  
S1 usually deferred after review if below 3.0 CLINICAL INTERPRETATION ; S2 correct during resuscitation, proceed  
when safe.  
Source Typical bands (no single guideline); hospital potassium protocol  
Clinician validation Replacement protocol CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 119*


### What if sodium is abnormal?

Trigger / what the  
trainee sees  
Na 126.  
What it might mean Water tablets, vomiting replaced by plain water, hormonal causes, heart/liver failure.  
What to check next Alertness (confusion?), fluid status, medicines, repeat.  
Correct action Correct the cause slowly (no faster than \~8–10 mmol/L per day).  
Incorrect action Rapid correction (risk of osmotic demyelination)  
Missed abnormality →  
consequence  
Confusion, seizures  
Escalation (who to call) Senior + anaesthetist; medical team if below 125.  
Expected patient /  
monitor response  
Slow change on repeat (≤10 mmol/L first 24 h)  
What happens next (S1 /  
S2 consequence)  
S1 usually deferred after review if below 130 or above 150 CLINICAL INTERPRETATION ; S2 correct in parallel.  
Source European hyponatraemia guideline 2014  
Clinician validation —  

### What if haemoglobin is low?

Trigger / what the  
trainee sees  
Hb 92 g/L.  
What it might mean Long-term iron deficiency, thalassaemia trait, slow gut bleeding, or acute bleeding (S2 ectopic).  
What to check next Previous Hb; red cell size (MCV); vital signs (bleeding now?); pregnancy test; stool history.  
Correct action S1: investigate before planned surgery; S2: group & save/crossmatch, look for bleeding, transfusion decision if  
below 70 (80 heart disease).  
Incorrect action Proceeding with planned surgery without investigating unexplained anaemia; transfusing a stable patient above  
threshold  
Missed abnormality →  
consequence  
Missed GI malignancy; unnecessary transfusion  
Escalation (who to call) Senior + anaesthetist.  
Expected patient /  
monitor response  
Hb rises \~10 g/L per unit transfused CLINICAL INTERPRETATION  
What happens next (S1 /  
S2 consequence)  
S1 usually postponed for investigation CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION ; S2 proceeds with blood available.  
Source WHO 2024; NICE NG24; Muñoz 2017  
Clinician validation Postponement rule CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

### What if the white cell count is high?

Trigger / what the  
trainee sees  
WBC 17.5.  
What it might mean Infection/inflammation (appendicitis, gallbladder), steroids, stress, pregnancy.  
What to check next Temperature, heart rate, exam, CRP, score.  
Correct action Interpret in context; S1 high WBC = not routine → acute pathway; S2 supports diagnosis; above 18 = moderate  
gallbladder grade.  
Incorrect action Treating the number alone; ignoring it in a planned patient  
Missed abnormality →  
consequence  
Missed acute infection  
Escalation (who to call) Senior if S1 or if very high.  
Expected patient /  
monitor response  
Falls with treatment over days  
What happens next (S1 /  
S2 consequence)  
Pathway decided by the whole picture.  
Source TG18; WSES 2025  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 120*


### What if the white cell count is low?

Trigger / what the  
trainee sees  
WBC 3.1 with fever.  
What it might mean Overwhelming sepsis, viral illness, medicines, bone-marrow problem.  
What to check next Neutrophils (below 0.5 = critical); vital signs; sepsis screen.  
Correct action Treat as serious if infection present; urgent antibiotics if septic.  
Incorrect action Assuming 'no infection' because WBC is low  
Missed abnormality →  
consequence  
Overwhelming sepsis missed  
Escalation (who to call) Senior; haematology if neutrophils below 0.5.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Close monitoring.  
Source SIRS criteria; haematology critical list  
Clinician validation —  

### What if creatinine is high?

Trigger / what the  
trainee sees  
Creatinine 165, usual 80.  
What it might mean Acute kidney injury from dehydration, sepsis, medicines; or long-term kidney disease (compare old result).  
What to check next Previous result; urine output; BP; medicines (NSAIDs, ACE-i, metformin); potassium.  
Correct action Fluids if dry; stop kidney-harming drugs; catheter/urine measurement; repeat in 6–24 h; adjust medicine doses;  
avoid CT dye unless essential (senior decides).  
Incorrect action Giving NSAIDs, contrast or nephrotoxic drugs without senior decision  
Missed abnormality →  
consequence  
AKI progresses; hyperkalaemia  
Escalation (who to call) Senior + anaesthetist; kidney team if stage 2–3.  
Expected patient /  
monitor response  
Urine output and creatinine improve with fluids if pre-renal  
What happens next (S1 /  
S2 consequence)  
S1 usually deferred (senior/anaesthetist decide); S2 resuscitate then operate.  
Source KDIGO 2012; NICE NG148  
Clinician validation —  

### What if liver tests are abnormal?

Trigger / what the  
trainee sees  
ALP 300, GGT 350, ALT 140, bilirubin 45.  
What it might mean Bile duct blockage (stone), passing stone, inflamed gallbladder, hepatitis, alcohol, medicines.  
What to check next Pattern (bile-stuck vs liver-cell); duct width on ultrasound; fever/jaundice; lipase.  
Correct action Use the bile-duct-stone risk tool (Part 5.16): high → ERCP pathway; intermediate → MRCP; low → proceed.  
Incorrect action Proceeding with planned cholecystectomy despite high-risk duct-stone criteria  
Missed abnormality →  
consequence  
Retained duct stone → cholangitis/pancreatitis  
Escalation (who to call) Senior (in S1).  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
S1 is usually deferred for duct imaging/clearance (senior decides).  
Source ASGE 2019; ACG 2017  
Clinician validation MRCP/ERCP access CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 121*


### What if bilirubin is high?

Trigger / what the  
trainee sees  
Bilirubin 90, yellow eyes.  
What it might mean Blocked bile duct; liver disease; red cell breakdown; Gilbert's (if mild alone).  
What to check next Duct width; other liver tests; fever (cholangitis?); Hb (breakdown?); INR.  
Correct action Cholangitis signs → emergency; otherwise high-risk pathway (ERCP).  
Incorrect action Ignoring fever + jaundice  
Missed abnormality →  
consequence  
Cholangitis → septic shock  
Escalation (who to call) Senior.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
S1 planned op postponed.  
Source TG18; ASGE 2019  
Clinician validation —  

### What if blood sugar is low? (below 4.0)

Trigger / what the  
trainee sees  
Finger-prick 3.2; patient sweaty, shaky.  
What it might mean Diabetes medicine while fasting; sepsis; alcohol.  
What to check next Repeat; alertness; ability to swallow; medicines taken.  
Correct action Treat immediately per hypoglycaemia protocol (fast sugar if can swallow AND not about to have anaesthetic per  
anaesthetist; else IV glucose) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; recheck every 10–15 min.  
Incorrect action Waiting to 'recheck later'; giving oral sugar to a drowsy patient  
Missed abnormality →  
consequence  
Seizure, coma  
Escalation (who to call) Anaesthetist (affects fasting); diabetes team.  
Expected patient /  
monitor response  
Glucose rises within 10–15 min; recheck  
What happens next (S1 /  
S2 consequence)  
Sugar above 4 and stable → continue; review medicine plan.  
Source JBDS hypoglycaemia guideline  
Clinician validation Glucose dose PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED  

### What if blood sugar is high? (above 12)

Trigger / what the  
trainee sees  
Finger-prick 15.8.  
What it might mean Infection, stress, missed medicine, steroids, undiagnosed diabetes, DKA.  
What to check next Ketones (above 14 or if unwell/type 1/SGLT2); HbA1c; infection signs.  
Correct action Insulin per protocol (often variable-rate drip) PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; fluids if dry.  
Incorrect action Ignoring ketones in an SGLT2 user  
Missed abnormality →  
consequence  
DKA develops  
Escalation (who to call) Anaesthetist; diabetes team.  
Expected patient /  
monitor response  
Glucose falls on VRIII  
What happens next (S1 /  
S2 consequence)  
S1 with HbA1c above 69 or ketones → postpone; S2 control in parallel.  
Source JBDS / CPOC 2023  
Clinician validation Insulin protocol CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 122*


### What if the clotting test (INR) is high?

Trigger / what the  
trainee sees  
INR 1.9, not on warfarin.  
What it might mean Liver disease, long bile blockage (vitamin K not absorbed), sepsis, error.  
What to check next Repeat; liver tests; platelets; bleeding.  
Correct action S1 usually deferred after review and correct (vitamin K per senior); S2 correct before surgery PATIENT-SPECIFIC  
DOSE — CLINICIAN CONTROLLED .  
Incorrect action Proceeding with planned surgery; giving neuraxial anaesthesia  
Missed abnormality →  
consequence  
Bleeding  
Escalation (who to call) Senior + anaesthetist; haematology.  
Expected patient /  
monitor response  
INR falls after vitamin K/PCC (clinician)  
What happens next (S1 /  
S2 consequence)  
Proceed when corrected.  
Source TG18; AoA/OAA/RA-UK 2013  
Clinician validation Reversal PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED  

### What if lactate is raised?

Trigger / what the  
trainee sees  
Lactate 3.4.  
What it might mean Organs short of oxygen: sepsis, dehydration, bleeding (rarely: some medicines, liver disease).  
What to check next Vital signs, NEWS2, infection source.  
Correct action Sepsis bundle/fluids; repeat within 2–4 h.  
Incorrect action Ignoring because BP is 'normal'  
Missed abnormality →  
consequence  
Cryptic shock progresses  
Escalation (who to call) Senior; anaesthetist.  
Expected patient /  
monitor response  
Lactate falls on repeat in 2–4 h if resuscitation working  
What happens next (S1 /  
S2 consequence)  
Falling lactate = responding.  
Source SSC 2021; Sepsis-3  
Clinician validation —  

## 14.3 Heart tracing problems


### What if the ECG is abnormal?

Trigger / what the  
trainee sees  
ECG: irregular, no P waves, rate 132 (new AF). Or ST elevation with chest pain.  
What it might mean AF triggered by sepsis/dehydration/salts; heart attack; potassium problem.  
What to check next Symptoms (chest pain?); BP; potassium/magnesium; troponin; old ECGs.  
Correct action AF: treat triggers, rate control per senior/medical; heart attack: emergency heart pathway.  
Incorrect action Relying on the machine's automatic interpretation; proceeding with new AF/ischaemia  
Missed abnormality →  
consequence  
Peri-operative MI, stroke or arrest  
Escalation (who to call) Senior + anaesthetist; medical/heart team.  
Expected patient /  
monitor response  
Rate settles with treatment of triggers  
What happens next (S1 /  
S2 consequence)  
S1: postpone. S2: stabilise, then surgery (heart attack → surgery only if life-saving).  
Source ESC 2022; ESC 2024 AF; NICE NG45  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 123*


## 14.4 Pregnancy, allergy and medicine problems


### What if the patient is pregnant?

Trigger / what the  
trainee sees  
Pregnancy test positive.  
What it might mean Normal early pregnancy; ectopic pregnancy (if pain/bleeding); miscarriage.  
What to check next Last period; pain; bleeding; vital signs; Hb.  
Correct action S1: postpone; obstetric advice. S2 with pain: ectopic until proven otherwise → gynaecology urgently. Avoid  
CT/unsafe drugs; ultrasound ok.  
Incorrect action Continuing appendicitis pathway; ordering CT first  
Missed abnormality →  
consequence  
Ruptured ectopic → haemorrhagic shock  
Escalation (who to call) Senior; gynaecology/obstetrics.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Pathway changes completely.  
Source NICE NG126; SAGES 2017; NICE NG45  
Clinician validation —  

### What if pregnancy status is unknown or the patient declines a test?

Trigger / what the  
trainee sees  
"I don't want a test."  
What it might mean Patient choice; privacy concerns; family present.  
What to check next Ask privately; explain why it matters.  
Correct action Respect decision; document; treat as possibly pregnant (scan/medicine choices); senior + anaesthetist decide  
whether to proceed.  
Incorrect action Testing without consent; skipping documentation  
Missed abnormality →  
consequence  
Teratogenic drug/radiation exposure  
Escalation (who to call) Senior + anaesthetist.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Proceed only with their agreement CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION .  
Source NICE NG45  
Clinician validation Policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

### What if the patient is allergic to an antibiotic?

Trigger / what the  
trainee sees  
"Penicillin gave me a rash" / "…made my throat swell".  
What it might mean Mild vs severe allergy (or intolerance like nausea).  
What to check next Exact reaction, timing, severity; allergy band.  
Correct action Record; choose antibiotic by hospital rule (mild rash: cefazolin often allowed; severe: per policy)  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Incorrect action Giving the drug anyway; recording 'penicillin allergy' without reaction details  
Missed abnormality →  
consequence  
Anaphylaxis  
Escalation (who to call) Pharmacist/microbiology per policy; anaesthetist.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Correct antibiotic given; 'allergy' confirmed at Sign In.  
Source NICE CG183 drug allergy; ASHP 2013  
Clinician validation Alternative CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 124*


### What if an allergic reaction happens after a medicine?

Trigger / what the  
trainee sees  
Minutes after an antibiotic: hives, swollen lips, wheeze, BP 80/45.  
What it might mean Anaphylaxis.  
What to check next ABCDE; what was given.  
Correct action STOP the drug; call for help; lie flat (sit up if struggling to breathe); call the emergency team; the responsible  
clinician gives IM adrenaline (adult standard dose 500 micrograms, repeat after 5 min if no improvement  
EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION ); high-flow oxygen; IV fluid  
bolus per Resuscitation Council UK (adults 500–1000 mL crystalloid).  
Incorrect action Giving antihistamine first instead of adrenaline; continuing the drug  
Missed abnormality →  
consequence  
Airway obstruction, cardiac arrest  
Escalation (who to call) Emergency team + anaesthetist.  
Expected patient /  
monitor response  
Improvement within minutes of IM adrenaline  
What happens next (S1 /  
S2 consequence)  
Recovers → observe; allergy recorded; S1 usually deferred (senior/anaesthetist decide); S2 later with alternative  
drug.  
Source Resuscitation Council UK 2021  
Clinician validation —  

### What if the patient takes blood thinners?

Trigger / what the  
trainee sees  
"I take apixaban / warfarin / clopidogrel."  
What it might mean Bleeding risk in surgery; clot/stroke risk if stopped.  
What to check next Which drug; why (AF, clot, stent — when?); last dose time; kidney function; INR if warfarin.  
Correct action S1: proceed only if stopped per plan (Part 8); otherwise postpone; never stop antiplatelets after a recent stent  
without the heart team. S2: senior/haematology decide reversal PATIENT-SPECIFIC DOSE — CLINICIAN  
CONTROLLED .  
Incorrect action Proceeding with planned surgery after a morning DOAC dose; stopping DAPT after a recent stent  
Missed abnormality →  
consequence  
Bleeding; stent thrombosis  
Escalation (who to call) Senior + anaesthetist; haematology/cardiology as needed.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Plan documented; restart plan after surgery.  
Source PAUSE 2019; ACC 2017; ESC 2022  
Clinician validation Stop plans CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

### What if the patient has diabetes?

Trigger / what the  
trainee sees  
"I have type 2 diabetes, on metformin and gliclazide."  
What it might mean Fasting may cause low sugar; stress/infection may cause high sugar.  
What to check next HbA1c (within 3 months); today's sugar; ketones if high/unwell; medicines and doses taken today.  
Correct action Medicine plan (Part 8); first on list if possible; check sugar at least hourly; target 6–10.  
Incorrect action Giving usual sulfonylurea while fasting; no CBG monitoring  
Missed abnormality →  
consequence  
Hypoglycaemia  
Escalation (who to call) Anaesthetist; diabetes team if poorly controlled.  
Expected patient /  
monitor response  
CBG stays 6–10  
What happens next (S1 /  
S2 consequence)  
S1: HbA1c above 69 → usually postpone. S2: proceed with insulin plan.  
Source CPOC/JBDS 2023  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 125*


### What if the patient takes steroids, GLP-1 injections or SGLT2 tablets?

Trigger / what the  
trainee sees  
"I take prednisolone / Ozempic / dapagliflozin."  
What it might mean Steroids: needs extra cover. GLP-1: stomach may be full. SGLT2: hidden DKA risk.  
What to check next Dose/duration; last dose; nausea/bloating; ketones.  
Correct action Per Part 8/9.  
Incorrect action Not asking; missing steroid cover  
Missed abnormality →  
consequence  
Adrenal crisis; aspiration; euglycaemic DKA  
Escalation (who to call) Anaesthetist.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Plan made or surgery delayed per anaesthetist.  
Source AoA 2020 (steroids); UK consensus 2025 (GLP-1/SGLT2)  
Clinician validation —  

## 14.5 Fasting problems


### What if the patient hasn't fasted?

Trigger / what the  
trainee sees  
Ate paratha at 06:00; surgery at 09:00.  
What it might mean Full stomach → aspiration risk.  
What to check next Exact time and type of food/drink.  
Correct action Tell anaesthetist honestly.  
Incorrect action 'Rounding' the time; proceeding without telling the anaesthetist  
Missed abnormality →  
consequence  
Aspiration pneumonitis  
Escalation (who to call) Anaesthetist.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
S1: moved later (8 h after a heavy meal) or postponed. S2: RSI; surgery not cancelled for food alone.  
Source ASA 2017/2023  
Clinician validation —  

### What if fasting status is unknown?

Trigger / what the  
trainee sees  
Confused/unwell patient can't remember; no relative.  
What it might mean Assume full stomach.  
What to check next Ask relatives/ambulance notes.  
Correct action Record 'unknown'; tell anaesthetist; aspiration risk YES at Sign In.  
Incorrect action Recording 'fasted' by assumption  
Missed abnormality →  
consequence  
Aspiration  
Escalation (who to call) Anaesthetist.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
RSI planned.  
Source ASA 2017/2023  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 126*


## 14.6 Scan and result problems


### What if imaging is inconclusive?

Trigger / what the  
trainee sees  
'Appendix not visualised'; 'possible early cholecystitis'.  
What it might mean We still don't know.  
What to check next Score; exam; bloods; duration.  
Correct action Next imaging (CT/MRI/MRCP) or observe and repeat assessment in 6–12 h.  
Incorrect action Treating 'not visualised' as negative and discharging  
Missed abnormality →  
consequence  
Perforation  
Escalation (who to call) Senior.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Decision once clearer.  
Source WSES 2025  
Clinician validation —  

### What if a test result doesn't make sense?

Trigger / what the  
trainee sees  
K 7.1 in a chatty, well man; platelets 40 in a well woman.  
What it might mean Faulty sample (haemolysis, clumping, drip arm, wrong patient).  
What to check next Lab comments; ECG (for K); look at patient; previous results.  
Correct action Repeat urgently (different tube/arm); treat meanwhile only if dangerous signs.  
Incorrect action Acting on an implausible value without repeating; ignoring a dangerous one  
Missed abnormality →  
consequence  
Over-treatment or missed danger  
Escalation (who to call) Senior if dangerous possibility.  
Expected patient /  
monitor response  
Repeat is normal (artefact) or confirms  
What happens next (S1 /  
S2 consequence)  
Repeat normal → continue; incident report if mislabelled.  
Source RCPath 2017  
Clinician validation —  

### What if a laboratory result is delayed?

Trigger / what the  
trainee sees  
CRP still 'pending' after 2 h.  
What it might mean Lab busy, sample lost/rejected.  
What to check next Phone the lab.  
Correct action Continue other tasks; repeat sample if lost; S2: don't delay antibiotics/fluids/surgery decision for a non-critical  
result.  
Incorrect action Waiting idle; delaying antibiotics in sepsis for results  
Missed abnormality →  
consequence  
Time-critical treatment delayed  
Escalation (who to call) Senior if it blocks a decision.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Result arrives or decision made without it.  
Source NICE NG253  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 127*


### What if a critical result is phoned through?

Trigger / what the  
trainee sees  
Lab: "Potassium 6.9 for Hamza Ali, MR 556677."  
What it might mean Possibly dangerous.  
What to check next Confirm patient identity; read back the value.  
Correct action Act immediately (ECG, emergency treatment if real); document; acknowledge.  
Incorrect action Not reading back; not acting  
Missed abnormality →  
consequence  
Arrhythmia/other harm  
Escalation (who to call) Senior + anaesthetist.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Unacknowledged → safety blocker.  
Source RCPath 2017  
Clinician validation —  

## 14.7 Deterioration and sepsis


### What if the patient deteriorates?

Trigger / what the  
trainee sees  
Vital signs trending worse; NEWS2 rising (e.g. 2 → 5).  
What it might mean Complication (perforation, sepsis, bleeding), wrong diagnosis, medicine reaction.  
What to check next ABCDE; re-examine; bloods (lactate, Hb); pregnancy test; review treatments given.  
Correct action Treat as found; increase monitoring.  
Incorrect action Waiting for next routine obs; not escalating  
Missed abnormality →  
consequence  
Cardiac arrest  
Escalation (who to call) NEWS2 5–6: urgent senior. 7+: emergency team.  
Expected patient /  
monitor response  
NEWS2 falls after effective treatment  
What happens next (S1 /  
S2 consequence)  
Improves → continue; worsens → emergency pathway (see Patient D).  
Source RCP NEWS2; RCUK ABCDE  
Clinician validation —  

### What if the patient becomes septic?

Trigger / what the  
trainee sees  
Infection + NEWS2 ≥5 or lactate ≥2 or BP ≤100 or confusion.  
What it might mean Sepsis.  
What to check next ABCDE; lactate; blood cultures; source.  
Correct action Sepsis bundle within 1 hour (oxygen, cultures, antibiotics, fluids, lactate, urine output).  
Incorrect action Antibiotics late; no cultures; no fluids  
Missed abnormality →  
consequence  
Septic shock; death  
Escalation (who to call) Senior + anaesthetist; critical care if shock.  
Expected patient /  
monitor response  
Lactate and NEWS2 fall; source control  
What happens next (S1 /  
S2 consequence)  
S2: urgent source control (surgery). S1: acute pathway (cholangitis → ERCP).  
Source NICE NG253; SSC 2021  
Clinician validation Sepsis tool CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 128*


## 14.8 Consent and communication problems


### What if the patient refuses surgery?

Trigger / what the  
trainee sees  
"I don't want the operation."  
What it might mean Fear, misunderstanding, cost/family concerns, cultural reasons, informed choice.  
What to check next Capacity; reasons; understanding.  
Correct action Explore and correct misunderstandings; offer alternatives (antibiotics-only for simple appendicitis); never pressure;  
document; safety advice.  
Incorrect action Pressuring; operating without consent  
Missed abnormality →  
consequence  
Legal/ethical violation  
Escalation (who to call) Senior.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Respect decision; alternative plan; door left open.  
Source GMC 2020; WMA Declaration of Lisbon on patient rights  
Clinician validation Law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

### What if the patient cannot understand the consent discussion?

Trigger / what the  
trainee sees  
Teach-back fails.  
What it might mean Language barrier, hearing problem, confusion/no capacity, low health literacy.  
What to check next Language; hearing; alertness and causes of confusion.  
Correct action Interpreter; simpler words/diagrams; treat confusion; if no capacity and urgent → senior emergency route per law  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Incorrect action Using a child relative as interpreter; proceeding without teach-back  
Missed abnormality →  
consequence  
Invalid consent  
Escalation (who to call) Senior; interpreter service.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Valid consent obtained or appropriate legal route.  
Source GMC 2020  
Clinician validation Interpreter policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

### What if the patient is under 18?

Trigger / what the  
trainee sees  
Age 16.  
What it might mean A minor in most jurisdictions (age of majority and medical-consent rules vary CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ).  
What to check next Parent/guardian present?  
Correct action Parent/guardian consent; involve the young person.  
Incorrect action Accepting the minor's signature alone  
Missed abnormality →  
consequence  
Invalid consent  
Escalation (who to call) Senior.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Proceed with valid consent.  
Source Jurisdiction-specific consent law (GMC 2020 as educational model)  
Clinician validation Legal team CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 129*


## 14.9 System problems


### What if equipment fails?

Trigger / what the  
trainee sees  
Gas pump low, suction not working, oximeter not reading.  
What it might mean Equipment fault.  
What to check next Which item; spare available?  
Correct action Report; replace; don't start.  
Incorrect action Starting anyway  
Missed abnormality →  
consequence  
Intra-operative harm  
Escalation (who to call) Theatre lead; anaesthetist.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Time Out only after fixed.  
Source WHO SSC; AoA equipment check 2019  
Clinician validation —  

### What if documents are missing?

Trigger / what the  
trainee sees  
No consent form in folder.  
What it might mean Lost/not done.  
What to check next Search; ask team.  
Correct action Re-consent or find; don't transfer.  
Incorrect action Transferring without consent  
Missed abnormality →  
consequence  
Wrong/unconsented procedure  
Escalation (who to call) Senior if re-consent needed.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Transfer once complete.  
Source WHO SSC  
Clinician validation —  

### What if the patient deteriorates during transfer?

Trigger / what the  
trainee sees  
Oxygen falls to 89% in the corridor.  
What it might mean Aspiration, sedation, sepsis progressing.  
What to check next ABCDE.  
Correct action Stop, oxygen, call for help; return or proceed per stability.  
Incorrect action Continuing to theatre without assessment  
Missed abnormality →  
consequence  
Arrest in corridor  
Escalation (who to call) Emergency team/anaesthetist.  
Expected patient /  
monitor response  
Stabilises with oxygen/ABCDE  
What happens next (S1 /  
S2 consequence)  
Stabilised before continuing.  
Source WHO-WFSA 2018; ICS transfer principles  
Clinician validation Transfer policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 130*


### What if there's an identity mismatch at theatre?

Trigger / what the  
trainee sees  
Wristband MR number differs from list.  
What it might mean Admin error or wrong patient.  
What to check next All identifiers.  
Correct action STOP; resolve with ward/admissions.  
Incorrect action Proceeding 'because it's obviously the right person'  
Missed abnormality →  
consequence  
Wrong-patient surgery  
Escalation (who to call) Senior; theatre lead.  
Expected patient /  
monitor response  
—  
What happens next (S1 /  
S2 consequence)  
Proceed only when certain.  
Source WHO SSC; NPSA  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 131*


# Part 14B — When several problems happen at once

(stacked cases)  
Teacher says:  
Real patients rarely have just one problem. A septic patient might also be allergic to penicillin, diabetic, on a blood  
thinner and unsure when they ate. The trick is not to memorise every combination — it's to use a fixed priority  
order, then handle the conflicts between treatments. This part teaches the method, then gives many worked  
combinations so the engineering team can build stacked variants.  

## 14B.1 The priority order (use it every time)

Universal priority ladder for multiple problems  
List every problem found  
1\. Is anything immediately life-threatening? (Airway,  
Breathing, Circulation, low sugar, anaphylaxis,  
NEWS2 7+)  
Fix that FIRST using ABCDE and call for help —  
everything else waits  
YES  
NO / done  
2\. Is there a hard safety blocker? (identity doubt,  
allergy unknown before a drug, consent invalid,  
positive pregnancy test with pain)  
Resolve before any non-emergency treatment  
or transfer  
YES  
NO / done  
3\. Is there a time-critical treatment? (antibiotics  
within 1 h in sepsis, pain relief, fluids for shock,  
potassium 6.5+)  
Do it now — in parallel with the remaining work  
YES  
NO / done  
4\. Does any finding change the diagnosis or  
pathway? (ectopic, cholangitis, burst appendix, duct  
stone)  
Senior/specialist; change pathway  
YES  
NO / done  
5\. Do treatments conflict? (e.g. fluids vs heart  
failure; best antibiotic vs allergy; painkiller vs  
kidneys)  
Choose the safest alternative (table 14B.3); ask  
senior/anaesthetist  
YES  
NO / done  
6\. Is it S1 with any fixable problem left? Senior/anaesthetic review → usually optimise  
first (defer / re-time / rebook)  
YES  
NO  
7\. Prepare, consent, hand over — listing EVERY  
problem and its plan in the SBAR  
In words: life threats first; then safety blockers; then anything where time is tissue; then 'does this change  
what we think is going on'; then resolve clashes between treatments; then — for planned surgery only —  
optimise or postpone. Finally, make sure the handover lists every problem. CLINICAL INTERPRETATION  

## 14B.2 Golden rules for stacking

- The most dangerous problem sets the pace. If any problem makes it an emergency, the whole case  
becomes an emergency.  
- In S1, problems add up to 'postpone'. One fixable problem is usually enough to postpone; several make  
it certain.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 132*

- In S2, problems add up to 'more preparation in parallel', not 'cancel'. Each problem gets its own fix  
while the team moves toward surgery — unless a problem shows it isn't appendicitis.  
- A new diagnosis beats the old plan. Positive pregnancy test with pain, jaundice with fever, chest pain  
with ST elevation — these redirect the patient.  
- Every drug decision must pass three checks: allergy? kidneys/liver? pregnancy? (plus 'blood thinner?'  
for anything that might cause bleeding).  
- When treatments clash, the senior/anaesthetist decides — but the trainee must spot the clash.  
- Re-check after every intervention. Stacked patients change faster.  
- The handover must include all problems — the commonest stacked-case error is fixing one and  
forgetting to mention the others.  

## 14B.3 Common treatment clashes and the safe resolution

Clash Why it's a problem Safer approach (draft) Decided by  
Needs fluid  
(sepsis/dehydration) + has  
heart failure or severe kidney  
failure  
Fluid may flood the lungs Smaller boluses (e.g. 250 mL) with re-check of  
breathing/oxygen after each; earlier senior/critical  
care  
Senior/anaesthetist  
PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
Needs pain relief + kidney  
injury  
NSAIDs (ibuprofen) worsen  
kidneys; morphine builds up  
Paracetamol + carefully titrated opioid (often not  
morphine in severe kidney injury); no NSAIDs  
PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
Needs pain relief +  
pregnancy  
NSAIDs avoided in pregnancy Paracetamol; opioid if needed per senior PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
Needs antibiotic + penicillin  
allergy  
Usual drug may be unsafe Use hospital allergy-alternative rule  
CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION  
Microbiology/policy  
Needs CT + pregnancy Radiation Ultrasound → MRI; CT only if senior decides benefit  
outweighs risk  
Senior/radiology  
Needs CT with dye + kidney  
injury  
Dye can worsen kidneys Fluids first; senior weighs benefit; non-contrast or  
ultrasound alternatives  
Senior/radiology  
Needs CT with dye + dye  
allergy  
Allergic reaction Alternative imaging or pre-medication per  
radiology policy  
Radiology  
CONTEXT-DEPENDENT  
— INSTITUTIONAL  
PROTOCOL / CLINICIAN  
CONFIRMATION  
Needs anti-sickness + long  
QTc on ECG  
Ondansetron can worsen QT Alternative antiemetic; fix potassium/magnesium Anaesthetist  
PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
Needs metoclopramide +  
bowel obstruction  
Can worsen obstruction Different antiemetic; NG tube per senior PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
On blood thinner + needs  
urgent surgery  
Bleeding vs clot risk Senior/haematology decide reversal; blood  
products ready  
PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
On antiplatelets after a  
recent heart stent + needs  
surgery  
Stopping risks stent clot  
(heart attack)  
S1 usually deferred after review; S2 operate on  
antiplatelets with heart-team advice  
Cardiology + senior  
Diabetic on insulin + nil by  
mouth  
Low sugar risk Insulin adjusted, sugar hourly, glucose-containing  
fluid or VRIII per protocol  
PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
SGLT2 user + sepsis/vomiting Euglycaemic DKA Check ketones even if sugar near normal; diabetes  
team  
Diabetes team  
Steroid user + sepsis/surgery Adrenal crisis (collapse) Extra steroid cover per anaesthetist PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
High potassium + needs  
suxamethonium (an  
anaesthetic relaxant)  
Raises potassium further Anaesthetist chooses a different drug; correct  
potassium first  
Anaesthetist  
Full stomach + emergency  
surgery  
Aspiration vs delay of surgery RSI; don't wait unless safe to Anaesthetist + surgeon  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 133*

Clash Why it's a problem Safer approach (draft) Decided by  
Low oxygen + long-term lung  
disease (CO2 retainer)  
Too much oxygen can be  
harmful  
Target 88–92%; blood gas Senior  
Confused patient + consent  
needed + emergency  
No capacity Treat cause; if still lacking and urgent →  
emergency/best-interests route  
CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION  
Senior  

## 14B.4 Worked stacked cases

Each case lists the problems, the priority order the trainee should follow, what to do, who to call, and the  
outcome for the simulator. Clinical values come from the sourced sections above; only the timing of how fast  
the combined case worsens is a SIMULATOR DESIGN VALUE (PROVISIONAL) .  

### SC-1 (S2): Sepsis + penicillin anaphylaxis + AKI

Problems found: Burst appendix: HR 126, BP 94/58, RR 24, T 39.0, lactate 3.6; penicillin caused throat  
swelling in the past; creatinine 170 (usual 80).  
Why these can coexist (clinical plausibility): Perforated appendicitis causes sepsis; sepsis lowers kidney  
perfusion causing AKI; penicillin allergy is a common, independent pre-existing history (reported in roughly  
10% of patients in UK/US data). CLINICAL INTERPRETATION  
Priority order and actions  
1\. Life threat: sepsis with poor circulation → ABCDE, call senior + anaesthetist, oxygen if needed, cultures.  
2\. Safety blocker before the drug: allergy is severe → antibiotic must follow the anaphylaxis alternative rule  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION ; still within 1 hour.  
3\. Fluids: kidneys are injured BY poor circulation → fluids help; give bolus and re-check; no NSAIDs;  
dose-adjust antibiotics for kidneys (pharmacist/clinician).  
4\. Diagnosis: perforation → urgent source control.  
5\. Handover: allergy + reaction, antibiotic name/time, fluids total, lactate trend, creatinine, aspiration risk.  
Simulator outcome: Urgent theatre after resuscitation; debrief checks antibiotic choice AND timing.  

### SC-2 (S2): Positive pregnancy test + lower-right pain + low BP

Problems found: 24 F, HR 118, BP 92/56, hCG positive, Hb 98.  
Why these can coexist (clinical plausibility): A ruptured tubal ectopic pregnancy presents with one-sided  
lower abdominal pain and intra-abdominal bleeding that lowers BP — a classic appendicitis mimic. CLINICAL  
INTERPRETATION  
Priority order and actions  
1\. Life threat: possible internal bleeding → call for help, two large drips, crossmatch, fluids/blood.  
2\. Diagnosis changes: ectopic until proven otherwise → gynaecology NOW; appendicitis pathway stops.  
3\. Avoid CT; pelvic ultrasound by gynaecology.  
4\. Consent for gynae surgery (by gynae team).  
Simulator outcome: Transfer to gynaecology emergency surgery; appendectomy NOT performed. Missing  
the pregnancy test = safety failure.  

### SC-3 (S2): Pregnant (20 weeks) + appendicitis + fever

Problems found: T 38.2, HR 108, WBC 15, ultrasound non-diagnostic.  
Why these can coexist (clinical plausibility): Appendicitis is the commonest non-obstetric surgical  
emergency in pregnancy; inflammation causes fever. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Not a life threat yet; obstetrics involved early.  
2\. Imaging: MRI (not CT first).  
3\. Analgesia: paracetamol ± opioid; avoid NSAIDs.  
4\. Antibiotics safe in pregnancy per local list.  
5\. Left-side tilt; fetal assessment by obstetrics; maternity early-warning score instead of NEWS2.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 134*

Simulator outcome: Laparoscopic appendectomy with obstetric support.  

### SC-4 (S1): High BP + poorly controlled diabetes

Problems found: BP 176/104 repeated; HbA1c 78; sugar 11.2.  
Why these can coexist (clinical plausibility): Hypertension and type 2 diabetes commonly occur together  
(shared risk factors). CLINICAL INTERPRETATION  
Priority order and actions  
1\. No life threat.  
2\. BP below 180/110 → BP alone would allow proceeding.  
3\. HbA1c above 69 → optimisation recommended.  
4\. Stack: one clear reason to postpone (diabetes) → postpone; refer GP for both BP and diabetes.  
Simulator outcome: Appropriately postponed (success).  

### SC-5 (S1): On apixaban (taken this morning) + new fast AF

Problems found: HR 124 irregular, BP 128/80.  
Why these can coexist (clinical plausibility): Apixaban is prescribed for AF; AF episodes can become fast  
with stress/dehydration; patients sometimes take the dose by habit. CLINICAL INTERPRETATION  
Priority order and actions  
1\. New fast AF → medical review (rate control).  
2\. Apixaban not stopped → bleeding risk.  
3\. Two independent reasons to postpone.  
Simulator outcome: Usually deferred (senior decides); medical team; plan re-listing with a proper  
anticoagulation stop plan.  

### SC-6 (S1): Fasting breach + diabetes on insulin

Problems found: Ate toast at 07:00 because sugar felt low; on basal insulin; CBG 4.6.  
Why these can coexist (clinical plausibility): A patient on insulin who feels hypoglycaemic while fasting  
may eat to correct it — the hypoglycaemia caused the breach. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Low-ish sugar: treat if below 4; recheck in 30 min (4.0–5.9 = looming low).  
2\. Fasting breach (light meal 6 h) → tell anaesthetist.  
3\. Insulin plan reviewed (likely dose too high for fasting).  
Simulator outcome: Re-timed later on list or postponed; diabetes plan corrected.  

### SC-7 (S2): Unknown fasting + vomiting + opioid given + sleepy

Problems found: RR 10, SpO2 93%, drowsy after morphine, vomiting.  
Why these can coexist (clinical plausibility): Appendicitis causes vomiting; opioids given for pain cause  
sedation and slow gastric emptying. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Life threat: airway at risk (drowsy + vomiting) and slow breathing → position on side, suction, oxygen, call  
anaesthetist.  
2\. Consider antidote (clinician).  
3\. Aspiration risk YES; NG tube per senior.  
4\. Stop further opioid; alternative pain plan.  
Simulator outcome: Stabilised → RSI planned; if aspiration occurred → chest problems branch.  

### SC-8 (S2): High potassium + AKI + sepsis

Problems found: K 6.6, creatinine 210, lactate 4.2, BP 88/50.  
Why these can coexist (clinical plausibility): Sepsis causes AKI; AKI reduces potassium excretion; acidosis  
shifts potassium out of cells. CLINICAL INTERPRETATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 135*

Priority order and actions  
1\. Two life threats: shock and dangerous potassium → emergency team; heart monitor; ECG; potassium  
emergency treatment PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED ; sepsis bundle.  
2\. Fluids for shock (also helps kidneys); choice of fluid type PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED .  
3\. Anaesthetist: no suxamethonium; correct K first.  
4\. Critical care involvement.  
Simulator outcome: Surgery once K below 6.0 and circulation responding — source control still essential.  

### SC-9 (S1): Jaundice + fever + shaking (cholangitis) + on clopidogrel

Problems found: Bilirubin 85, T 39.1, HR 118, BP 100/62; clopidogrel for stent 2 years ago.  
Why these can coexist (clinical plausibility): A duct stone causes obstruction (jaundice) then infection  
(fever/rigors); antiplatelets are common in older gallstone patients with heart disease. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Life threat: cholangitis/sepsis → sepsis bundle, antibiotics within 1 h.  
2\. Diagnosis change: emergency biliary drainage (ERCP) instead of gallbladder surgery.  
3\. Antiplatelet: affects ERCP bleeding (sphincter cut) — gastro/cardiology decide; stent >12 months reduces  
stop-risk CLINICAL INTERPRETATION .  
Simulator outcome: Gastroenterology urgent ERCP; elective cholecystectomy later.  

### SC-10 (S2): Language barrier + 17 years old + needs urgent surgery

Problems found: Patient speaks Pashto only; father present.  
Why these can coexist (clinical plausibility): Appendicitis peaks in adolescents/young adults; many health  
systems serve multilingual populations. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Interpreter (trained, not a sibling child).  
2\. Minor → parent/guardian consent per law; involve the patient CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION .  
3\. Pain relief first.  
Simulator outcome: Valid consent via interpreter; proceed.  

### SC-11 (S2): Refuses surgery + becomes septic later

Problems found: Initially capable refusal; 4 h later NEWS2 8, confused.  
Why these can coexist (clinical plausibility): An untreated appendix can perforate within hours to days,  
and sepsis causes confusion. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Life threat → sepsis bundle.  
2\. Capacity now lost (confusion from sepsis) → treat cause; re-assess.  
3\. Previous capable refusal must be respected where it clearly applied; senior + legal/ethics guidance on  
emergency treatment CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Simulator outcome: Senior-led decision; full documentation.  

### SC-12 (S1): Haemolysed high potassium + genuinely low sodium + water tablets

Problems found: K 6.1 'haemolysed', Na 127, on bendroflumethiazide.  
Why these can coexist (clinical plausibility): Haemolysis is a common sampling artefact; thiazide diuretics  
commonly cause hyponatraemia. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Repeat potassium (likely false).  
2\. Sodium 127 is real and likely from the water tablet → postpone; GP/medical review.  
3\. Don't let the false alarm distract from the real abnormality.  
Simulator outcome: Postponed for sodium.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 136*


### SC-13 (S1): Anaemia + anticoagulant + needs G&S

Problems found: Hb 96, on warfarin INR 2.6 today (should have stopped).  
Why these can coexist (clinical plausibility): Anticoagulants increase occult GI bleeding, causing  
iron-deficiency anaemia. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Two reasons: warfarin not stopped (INR too high) and unexplained anaemia (bleeding on warfarin?).  
2\. Ask about black stools/bleeding; G\&S; senior.  
Simulator outcome: Usually deferred (senior decides); investigate anaemia; anticoagulation clinic plan.  

### SC-14 (S2): Equivocal ultrasound + deteriorating vital signs

Problems found: Appendix not seen; HR 104 → 122; T 38.9; CRP 160.  
Why these can coexist (clinical plausibility): Ultrasound often fails to see the appendix, while the disease  
continues to progress. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Deterioration overrides 'wait and see' → senior urgently; sepsis screen.  
2\. Proceed to CT now (with escort and monitoring) or straight to surgery if senior decides.  
Simulator outcome: Faster pathway; observation option withdrawn.  

### SC-15 (S2): Anaphylaxis to antibiotic + still needs antibiotics

Problems found: Reaction to first antibiotic; treated with adrenaline; now stable.  
Why these can coexist (clinical plausibility): Antibiotics are among the commonest triggers of  
peri-operative anaphylaxis (NAP6), and the infection still needs treatment. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Record new allergy; alternative antibiotic from a different family per policy CONTEXT-DEPENDENT — INSTITUTIONAL  
PROTOCOL / CLINICIAN CONFIRMATION .  
2\. Anaesthetist informed (reaction may recur; airway).  
3\. Surgery timing per senior once stable.  
Simulator outcome: Proceed later with alternative.  

### SC-16 (S1): GLP-1 injection + nausea + high BMI + sleep apnoea

Problems found: Semaglutide weekly, took 2 days ago; bloated; BMI 42; STOP-BANG 6.  
Why these can coexist (clinical plausibility): GLP-1 drugs are prescribed for obesity/diabetes; obesity is a  
major OSA risk; nausea is a common GLP-1 side effect. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Delayed stomach emptying likely (symptoms) → aspiration risk even if fasted.  
2\. Airway/OSA risk.  
3\. Anaesthetist decides: postpone with 24 h liquid diet plan, or proceed with RSI and stomach ultrasound  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Simulator outcome: Usually rescheduled with a plan.  

### SC-17 (S2): Sickle cell trait/thalassaemia + dehydration

Problems found: Known thalassaemia trait; Hb 108 with small cells; dehydrated.  
Why these can coexist (clinical plausibility): Beta-thalassaemia trait is common in many populations  
(Mediterranean, Middle East, South Asia); vomiting from appendicitis causes dehydration. CLINICAL  
INTERPRETATION  
Priority order and actions  
1\. Anaemia explained by trait (chronic); dehydration → fluids.  
2\. HbA1c unreliable (irrelevant in S2).  
3\. Tell anaesthetist.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 137*

Simulator outcome: Proceed after fluids.  

### SC-18 (S1): Missing consent + equipment fault + late list

Problems found: Form not signed; insufflator fault.  
Why these can coexist (clinical plausibility): System failures cluster on busy lists. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Neither is a medical emergency; both are blockers.  
2\. Re-consent properly (not rushed); report equipment.  
3\. If time runs out → postpone rather than rush CLINICAL INTERPRETATION .  
Simulator outcome: Either fixed and proceed, or postponed.  

### SC-19 (S2): Low sugar + confusion + sepsis in a diabetic on SGLT2

Problems found: CBG 3.4, confused, T 38.8, HR 120, ketones 3.5.  
Why these can coexist (clinical plausibility): Sepsis plus reduced intake causes hypoglycaemia in treated  
diabetes and ketoacidosis with SGLT2 inhibitors; both cause confusion. CLINICAL INTERPRETATION  
Priority order and actions  
1\. Life threats: low sugar (treat immediately), sepsis (bundle), ketoacidosis (DKA protocol).  
2\. Diabetes team + senior + anaesthetist.  
3\. Consent/capacity reassessed after treatment.  
Simulator outcome: Surgery after metabolic stabilisation; critical care likely.  

### SC-20 (S2): Older patient + appendicitis picture + weight loss + anaemia + on aspirin

Problems found: 70 M, CRP 80, Hb 102, aspirin for stroke prevention.  
Why these can coexist (clinical plausibility): Caecal cancer can present like appendicitis in older patients  
and causes iron-deficiency anaemia and weight loss; aspirin is common at this age. CLINICAL INTERPRETATION  
Priority order and actions  
1\. CT before deciding (possible tumour).  
2\. Continue aspirin usually CLINICAL INTERPRETATION .  
3\. G&S.  
Simulator outcome: Pathway decided by CT (surgery vs colorectal team).  

## 14B.5 How to build ANY new combination (recipe for the engineering

team)  
Steps  
1\. Write down each problem and its individual branch from Part 14.  
2\. Mark each as: life threat / safety blocker / time-critical / pathway-changing / optimisable / information-only.  
3\. Order them using the 14B.1 ladder.  
4\. Check every planned drug, fluid and scan against: allergy, kidneys, liver, pregnancy, blood thinners, heart  
rhythm (QTc), diabetes (table 14B.3).  
5\. Decide the end state: S1 → proceed only if NO optimisable/blocking problems remain; S2 → proceed once  
life threats are treated and blockers resolved.  
6\. Define the deterioration clock: the most dangerous untreated problem sets the speed.  
7\. List the handover items: every problem + action + time.  
8\. Send the combination to the surgeon/anaesthetist for approval before building CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  
Problem type Examples What it does to the case  
Life threat Shock, low oxygen, anaphylaxis, low sugar, K 6.5+, NEWS2 7+ Stops everything; emergency mode  
Safety blocker Identity doubt, unknown allergy, invalid consent, unknown  
pregnancy status, unacknowledged critical result, incomplete  
checklist  
Cannot pass checkpoints until resolved  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 138*

Problem type Examples What it does to the case  
Time-critical Sepsis antibiotics, analgesia, fluids for dehydration Must be done within a clock  
Pathway-changing Ectopic, cholangitis, perforation, duct stone, new AF, MI Switches to a different branch/team  
Optimisable BP 180/110+, HbA1c above 69, anaemia, salts, anticoagulant  
timing, fasting  
S1 usually deferred after review/re-time; S2  
correct in parallel  
Information-only Smoker, mild obesity, controlled AF Recorded and handed over  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 139*


# Part 15 — The WHO Surgical Safety Checklist,

explained  
The World Health Organization (WHO) Surgical Safety Checklist (revised January 2009) has three pauses. The  
wording below is the WHO wording (lightly simplified in the 'what it means' column). WHO states the checklist  
is not comprehensive and encourages additions to fit local practice; anything ProcSense adds is labelled  
ProcSense educational extension and is NOT presented as a WHO item CLINICAL REFERENCE — SOURCE BACKED  
(WHO Surgical Safety Checklist and Implementation Manual, 2009).  
Pause 1 — SIGN IN: before induction of anaesthesia (at least nurse and  
anaesthetist; patient awake)  
WHO item (2009 wording) What it means S1 typical  
answer  
S2 typical answer If 'no' or unsure  
Has the patient confirmed  
his/her identity, site, procedure  
and consent?  
Patient says who they are  
and what is being done;  
consent present  
Yes Yes STOP — resolve  
Is the site marked? (Yes / Not  
applicable)  
Skin mark where  
laterality/level applies  
Usually not  
applicable CONTE  
XT-DEPENDENT —  
INSTITUTIONAL  
PROTOCOL /  
CLINICIAN  
CONFIRMATION  
Usually not applicable  
CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION  
Ask surgeon  
Is the anaesthesia machine and  
medication check complete?  
Machine and drugs  
checked by the anaesthetic  
team  
Yes Yes STOP — complete check  
Is the pulse oximeter on the  
patient and functioning?  
Oxygen monitor reading  
with waveform  
Yes Yes STOP — replace  
Does the patient have a known  
allergy? (No / Yes)  
Any allergy No/Yes + what No/Yes + what Prepare alternatives  
Difficult airway or aspiration  
risk? (No / Yes, and  
equipment/assistance available)  
Hard intubation? Full  
stomach?  
Usually no Yes — aspiration risk; RSI  
equipment, suction and  
assistance available  
Plan, don't stop  
Risk of >500 mL blood loss (7  
mL/kg in children)? (No / Yes,  
and two IVs/central access and  
fluids planned)  
Major bleeding expected? Usually no Usually no; yes if  
bleeding/coagulopathy  
Two IVs + fluids/blood  
planned  
Pause 2 — TIME OUT: before skin incision (nurse, anaesthetist and  
surgeon)  
WHO item (2009 wording) What it means S1 S2  
Confirm all team members have  
introduced themselves by name and role  
Everyone knows who's who  
and can speak up  
Yes Yes  
Confirm the patient's name, procedure,  
and where the incision will be made  
Final identity/procedure/site  
check  
Yes Yes  
Has antibiotic prophylaxis been given  
within the last 60 minutes? (Yes / Not  
applicable)  
Infection-prevention timing Only if indicated; otherwise  
'Not applicable'  
Yes — time confirmed; redose  
decision if needed  
Anticipated critical events — To surgeon:  
What are the critical or non-routine steps?  
How long will the case take? What is the  
anticipated blood loss?  
Team prepared \~60 min; minimal \~45–60 min; minimal  
To anaesthetist: Are there any  
patient-specific concerns?  
e.g. OSA, diabetes, aspiration As relevant Aspiration, sepsis, kidneys  
To nursing team: Has sterility (including  
indicator results) been confirmed? Are  
there equipment issues or any concerns?  
Instruments truly sterile;  
equipment works  
Tower working Lap + open sets  
Is essential imaging displayed? (Yes / Not  
applicable)  
Scans on screen Ultrasound ± MRCP Ultrasound/CT  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 140*

Pause 3 — SIGN OUT: before the patient leaves the operating room  
(nurse, anaesthetist and surgeon)  
WHO item (2009 wording) Meaning  
Nurse verbally confirms: the name of the procedure What was actually done is recorded  
Nurse verbally confirms: completion of instrument, sponge and  
needle counts  
Nothing left inside  
Nurse verbally confirms: specimen labelling (read specimen  
labels aloud, including patient name)  
Gallbladder/appendix goes to the lab correctly  
Nurse verbally confirms: whether there are any equipment  
problems to be addressed  
Faults reported  
To surgeon, anaesthetist and nurse: What are the key concerns  
for recovery and management of this patient?  
Handover to recovery  
ProcSense educational extensions (NOT WHO checklist items)  
Extension Why ProcSense adds it Basis  
VTE prophylaxis confirmed Reinforces NICE NG89 teaching NICE NG89; included in some national  
adaptations (e.g. NPSA UK 2010)  
Capillary glucose recorded for  
patients with diabetes  
Reinforces CPOC teaching CPOC 2021/2023  
Patient temperature ≥36.0 °C before  
induction  
Reinforces hypothermia prevention NICE CG65  
Pregnancy status documented where  
relevant  
Reinforces NICE NG45 NICE NG45  
Team brief before the list / debrief  
after  
Teamwork teaching UK 'Five steps to safer surgery' (NPSA 2010)  
Corrections in v5  
Time Out identity item now uses the 2009 wording 'Confirm the patient's name, procedure, and where the incision  
will be made'; Sign In uses 'anaesthesia machine and medication check'; Time Out order follows the WHO form;  
additions are separated as ProcSense educational extensions.  
Where this comes from: World Health Organization, Surgical Safety Checklist (revised 1/2009) and Implementation Manual (2009).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 141*


# Part 16 — What the player actually sees

For each stage: on screen, the patient, the monitor, what can be touched, what appears after touching it, the  
decision, and the results of correct, incorrect, missed and escalated actions. No code — this is a description of  
the experience.  

### Stage 1 — Arrival and identity

On screen / in front of the  
player  
S1: day-surgery bay, patient seated, folder on the trolley, nurse nearby. S2: ED cubicle, patient curled on  
trolley, relative, triage clipboard.  
Patient — what they say S1: "Hello, I'm here for my gallbladder operation." S2: groaning; "It really hurts…"  
Monitor — numbers Blank until devices attached (S2 triage numbers on clipboard).  
Interaction Wristband (rotate wrist to read), folder, allergy band, speak to patient/relative.  
Information revealed Name/DOB spoken by patient; folder pages; allergy details after asking 'what happened?'.  
Decision Is this the right patient? Any allergy?  
Correct action → Nurse: "Thanks, I'll update the record." Tests/medicines unlock.  
Incorrect action → Reading the name to the patient → patient agrees (even if wrong in a variant) → later mismatch event.  
Missed abnormality → Unrecorded allergy → reaction later.  
Escalation → Can call admissions/senior for mismatch.  

### Stage 2 — History

On screen / in front of the  
player  
Patient (and relative); clerking form.  
Patient — what they say Volunteers only the main complaint; other facts only if asked (e.g. "I took apixaban this morning").  
Monitor — numbers Unchanged.  
Interaction Question menu or voice: pain, red flags, medicines, allergies, anaesthetics, social, fasting, pregnancy (privacy  
option to ask relatives to step out).  
Information revealed Each answer adds a line to the clerking note.  
Decision What's the story? Any red flags?  
Correct action → Relevant facts unlock correct branches.  
Incorrect action → Skipping fasting/medicine/pregnancy questions → blockers later.  
Missed abnormality → Yellow-eyes comment ignored → abnormal LFTs force it later.  
Escalation → Red flags → call senior.  

### Stage 3 — Vital signs

On screen / in front of the  
player  
Monitor, finger clip, BP cuff, thermometer, ECG leads, NEWS2 chart.  
Patient — what they say "Is this going to hurt?"  
Monitor — numbers SpO2 + wave + HR after clip; BP after \~30–40 s; RR must be counted by watching chest; temperature from  
thermometer.  
Interaction Attach devices; count breaths; feel pulse; fill NEWS2.  
Information revealed Numbers; NEWS2 total.  
Decision Normal? Concerning? Emergency?  
Correct action → Nurse sets observation frequency.  
Incorrect action → Accepting a high BP without repeat → anaesthetist flags later.  
Missed abnormality → Rising trend ignored → deterioration branch.  
Escalation → NEWS2 5+ → call senior; 7+ → emergency call.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 142*


### Stage 4 — Examination

On screen / in front of the  
player  
Patient on couch; trainee's hands/stethoscope.  
Patient — what they say Winces/catches breath when tender areas pressed (Murphy's, rebound).  
Monitor — numbers Unchanged.  
Interaction Ask permission/offer chaperone; look at eyes; listen; press tummy regions; airway look.  
Information revealed Findings text (e.g. 'tender RIF, guarding'); eye colour visible.  
Decision Do signs fit the diagnosis?  
Correct action → Findings added to notes.  
Incorrect action → Examining without permission → patient discomfort; debrief.  
Missed abnormality → Missed Murphy's → acute cholecystitis found later.  
Escalation → Rigid tummy → senior immediately.  

### Stage 5 — Risk and anaesthetic assessment

On screen / in front of the  
player  
Anaesthetist arrives / phone; ASA selector.  
Patient — what they say Anaesthetist asks: "When did he last eat?"  
Monitor — numbers —  
Interaction Select ASA, VTE form, airway findings.  
Information revealed Anaesthetist's response.  
Decision Fit now? Plan?  
Correct action → Plan agreed.  
Incorrect action → Wrong ASA corrected with explanation.  
Missed abnormality → Hidden fasting breach → aspiration event/cancellation.  
Escalation → Anaesthetist can postpone.  

### Stage 6 — Tests and moving the patient

On screen / in front of the  
player  
Order panel; tubes; labels; kits; ECG machine; scan request; porter; portable monitor.  
Patient — what they say "What are these tests for?"  
Monitor — numbers —  
Interaction Select tests; take blood; label at bedside; dipstick; pregnancy test; ECG; request scan; choose escort.  
Information revealed Pending list with expected times.  
Decision Which tests? Who escorts?  
Correct action → Tests queued; patient moves safely.  
Incorrect action → Unneeded tests listed in debrief; pre-labelled tubes → wrong-blood event.  
Missed abnormality → Unstable patient sent with porter only → oxygen drop in corridor.  
Escalation → Senior for imaging choice.  

### Stage 7 — Results

On screen / in front of the  
player  
Results panel with H/L flags; scan viewer; phone.  
Patient — what they say —  
Monitor — numbers Can update after treatment.  
Interaction Open, compare, acknowledge; answer phone.  
Information revealed Lab comments, previous values, radiology report.  
Decision What does it mean; act?  
Correct action → Plan updates.  
Incorrect action → Over-treating a faulty sample → debrief.  
Missed abnormality → Unacknowledged critical result → blocker; real deterioration if true.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 143*

Escalation → Critical results → senior/anaesthetist.  

### Stage 8 — Decide, treat, optimise

On screen / in front of the  
player  
Drug chart, drip stand, fluid bags, medicine trolley, stockings.  
Patient — what they say Pain reduces after analgesia ("That's a bit better").  
Monitor — numbers HR/BP respond to treatment over time.  
Interaction Recognises the need (e.g. analgesia, fluids, stockings), requests it through the drug-chart / nurse workflow  
and proposes the drug class; a qualified clinician confirms drug and dose; the nurse NPC administers; trainee  
calls senior.  
Information revealed Response of vital signs.  
Decision Safe / investigate / optimise / review / emergency?  
Correct action → Patient improves; case progresses.  
Incorrect action → Requesting a drug the patient is allergic to → nurse challenges at the check; if ignored, reaction branch.  
Missed abnormality → No treatment in S2 → deterioration clock speeds up.  
Escalation → Escalation answered by NPC with advice/arrival.  

### Stage 9 — Consent

On screen / in front of the  
player  
Patient, family, form, leaflet, interpreter phone.  
Patient — what they say Questions: fertility, going home, tablets instead?  
Monitor — numbers —  
Interaction Dialogue choices; teach-back; sign form.  
Information revealed Patient's understanding level.  
Decision Valid consent?  
Correct action → Signed form.  
Incorrect action → Missing risks/alternatives → low score/complaint.  
Missed abnormality → Consenting confused patient → safety failure.  
Escalation → Refusal → senior.  

### Stage 10 — Ward preparation

On screen / in front of the  
player  
Gown, jewellery tray, clippers, marker, stockings, checklist, folder.  
Patient — what they say "Oh — I did have tea with milk at 7."  
Monitor — numbers Temperature check.  
Interaction Tick items; tape ring; clippers (not razor).  
Information revealed Missing items highlighted when checked.  
Decision Ready?  
Correct action → 'Ready for theatre'.  
Incorrect action → Razor → infection debrief; missing consent → transfer refused.  
Missed abnormality → —  
Escalation → Late fasting → anaesthetist.  

### Stage 11 — Theatre preparation

On screen / in front of the  
player  
Theatre: machines, tower, CO2 cylinder gauge, trays, diathermy, warming, resus trolley.  
Patient — what they say —  
Monitor — numbers Anaesthetic machine check log.  
Interaction Inspect/confirm items.  
Information revealed Fault messages (e.g. low gas).  
Decision Ready?  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 144*

Correct action → 'Theatre ready'.  
Incorrect action → Fault ignored → Time Out fails.  
Missed abnormality → —  
Escalation → Report to theatre lead.  

### Stage 12 — Transfer and handover

On screen / in front of the  
player  
Trolley, porter, monitor, reception desk, anaesthetist.  
Patient — what they say Reception nurse questions.  
Monitor — numbers Portable monitor if unstable.  
Interaction Choose escort; deliver SBAR (structured choices or speech).  
Information revealed Missing items prompted by anaesthetist.  
Decision Is handover complete?  
Correct action → Handover accepted.  
Incorrect action → Omissions scored.  
Missed abnormality → Corridor deterioration ignored → arrest branch.  
Escalation → Call for help in transit.  

### Stages 13–14 — Sign In and Time Out

On screen / in front of the  
player  
Anaesthetic room/theatre; checklist board; team.  
Patient — what they say Patient answers Sign In identity questions.  
Monitor — numbers Live monitor.  
Interaction Lead or answer each item; direct question to right person.  
Information revealed Each item lights green when confirmed.  
Decision All safe?  
Correct action → 'READY FOR INCISION' — case complete.  
Incorrect action → Skipped item → challenge; repeated → case stops.  
Missed abnormality → Antibiotic timing missed → debrief/stop.  
Escalation → —  

### End states

End state Meaning  
Ready for incision — safe All blockers resolved; correct pathway.  
Appropriately postponed (S1) Correct reason found and communicated — counts as SUCCESS.  
Appropriately redirected Diagnostic diversion (e.g. suspected ectopic pregnancy → gynaecology); cholangitis → biliary  
drainage pathway; inflamed gallbladder → acute pathway.  
Safe but delayed Right outcome with avoidable delays.  
Safety failure A blocker was violated or a life-threatening event missed.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 145*


# Part 17 — All the numbers in one place (with plain

meanings)  
These tables collect values from the sourced sections of this book. Unless a row says otherwise, the 'Normal'  
column is a reference range and the other columns are clinical bands from the cited guidelines (NEWS2,  
UKKA, KDIGO, WHO, JBDS, ASGE, TG18, etc.) CLINICAL REFERENCE — SOURCE BACKED . Only rows tagged SIMULATOR  
DESIGN VALUE (PROVISIONAL) are game-specific. The source of each number is listed in the Clinical Evidence Audit  
(Part H).  
Vital signs (adults, not pregnant)  
What Normal Watch (amber) Worry (red) Emergency Plain meaning  
Heart rate /min 51–90 91–110 or 41–50 111–130 ≥131 or ≤40 Pump speed  
Top BP (low side)  
mmHg  
111–219 101–110 91–100 ≤90 / MAP below 65 Pressure to feed organs  
BP (high side) below 140/90 140–179 / 90–109 ≥180/110 (S1 usually  
deferred after review)  
≥180/120 + symptoms Strain on vessels  
Breathing /min 12–20 9–11 or 21–24 ≥22 with infection ≥25 or ≤8 Effort/acid/sedation  
Oxygen % ≥96 94–95 92–93 ≤91 (critical below 88) Oxygen in blood  
Temperature °C 36.1–37.9 35.1–36.0 / 38.0–39.0 ≥39.1 ≤35.0 Infection/cold  
Alertness Alert — New confusion Voice/Pain/Unresponsive Brain function  
Pain /10 0–3 4–6 7–10 Uncontrolled Comfort/complication  
Capillary refill ≤2 s 2–3 s >3 s Mottled skin Circulation  
Urine ≥0.5 mL/kg/h below for under 6 h below for 6 h+ Almost none Kidney blood supply  
Shock index (HR ÷  
SBP) — supporting  
indicator only  
0.5–0.7 (up to  
\~0.9 in some  
studies)  
0.8–0.9 approaching/above 1.0  
(study-derived marker)  
No validated higher  
cut-off used  
Hidden circulatory stress  
NEWS2 0 1–4 5–6 or any single 3 ≥7 Danger counter  
Blood tests  
Test Normal Mild Significant Severe/critical Plain meaning  
Haemoglobin g/L M 130–170; F 120–150 110–129/119 80–109 below 80 (crit. below  
70\)  
Oxygen carriers  
WBC ×109/L 4.0–11.0 11.1–15.0 15.1–20.0 >20 or <4 Infection fighters  
Neutrophils 2.0–7.5 — <1.5 <0.5 First responders  
Platelets 150–400 100–149 50–99 <50 (crit. <30) Clot-starters  
CRP mg/L <5 10–49 50–149 ≥150 (≥300 critical) Inflammation  
Sodium mmol/L 135–145 130–134 / 146–150 125–129 / 151–155 <125 / >155 Water/salt balance  
Potassium mmol/L 3.5–5.3 3.0–3.4 / 5.5–5.9 2.5–2.9 / 6.0–6.4 <2.5 / ≥6.5 Heart electrics  
Urea mmol/L 2.5–7.8 7.9–12 >12 — Waste/dehydration  
Creatinine µmol/L M 60–110; F 45–90 ×1.5–1.9 baseline ×2–2.9 ×3 or ≥354 Kidney filter  
eGFR ≥90 60–89 30–59 <30 Kidney score  
Glucose (diabetic,  
peri-op)  
6–10 (to 12 OK) 12.1–14 >14 <4.0 (severe <3.0) Blood sugar  
HbA1c mmol/mol <42 42–47 / 48–69 >69 (postpone S1) — 3-month sugar  
average  
Ketones mmol/L <0.6 0.6–1.4 1.5–2.9 ≥3.0 Fat-burning acids  
Bilirubin µmol/L 3–21 22–30 31–68 >68 Yellow pigment  
ALT U/L ≤40 41–120 121–400 >400 Liver-cell damage  
ALP U/L 30–130 ≤2× upper 2–5× upper >5× upper Bile duct blockage  
GGT U/L M<60; F<40 ≤3× upper >3× upper — Confirms bile/alcohol  
Albumin g/L 35–50 30–34 25–29 <25 Nutrition/liver  
Lipase \<upper limit 1–3× >3× (pancreatitis) — Pancreas  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 146*

Test Normal Mild Significant Severe/critical Plain meaning  
INR (no warfarin) 0.8–1.2 1.3–1.5 >1.5 >2.0 Clotting speed  
Lactate mmol/L <2.0 — 2.0–3.9 ≥4.0 Oxygen debt  
β-hCG IU/L <5 5–25 (repeat 48 h) >25 positive + pain/shock Pregnancy  
QTc ms (ECG) M<450; F<460 Prolonged ≥450 M /  
≥460 F  
— ≥500 markedly  
prolonged  
Heart recharge time  
(AHA/ACCF/HRS  
2009\)  
Key decision thresholds  
Rule Number Tag  
Fasting: clear / milk & light meal / heavy meal 2 h / 6 h / 8 h CLINICAL  
REFERENCE —  
SOURCE BACKED  
S1 BP postpone ≥180/110 on repeat (GP target <160/100) CLINICAL  
REFERENCE —  
SOURCE BACKED  
S1 diabetes postpone HbA1c >69 mmol/mol (8.5%) CLINICAL  
REFERENCE —  
SOURCE BACKED  
Peri-op sugar target 6–10 (6–12 acceptable) mmol/L CLINICAL  
REFERENCE —  
SOURCE BACKED  
Bile-duct stone high risk Bilirubin >68 µmol/L (4 mg/dL) + duct >6 mm; or stone seen; or  
cholangitis  
CLINICAL  
REFERENCE —  
SOURCE BACKED  
Gallbladder 'moderate' grade WBC >18; lump; >72 h; severe local inflammation CLINICAL  
REFERENCE —  
SOURCE BACKED  
Appendix abnormal width >6 mm CLINICAL  
REFERENCE —  
SOURCE BACKED  
AIR bands 0–4 low, 5–8 unsure, 9–12 high CLINICAL  
REFERENCE —  
SOURCE BACKED  
AAS bands 0–10 low, 11–15 intermediate, ≥16 high CLINICAL  
REFERENCE —  
SOURCE BACKED  
Sepsis antibiotics Within 1 h if NEWS2 ≥7 (or 5–6 with a single 3); 3 h if 5–6; 6 h if  
1–4 (NICE NG253, 2025)  
CLINICAL  
REFERENCE —  
SOURCE BACKED  
Antibiotic before incision Within 60 min CLINICAL  
REFERENCE —  
SOURCE BACKED  
Theatre temperature ≥36.0 °C CLINICAL  
REFERENCE —  
SOURCE BACKED  
Blood loss question (Sign In) >500 mL (7 mL/kg children) CLINICAL  
REFERENCE —  
SOURCE BACKED  
Appendectomy timing Generally prompt; WSES 2025: laparoscopic appendectomy  
within 24 h of admission when surgery is indicated  
(context-dependent: severity, sepsis, anaesthetic assessment,  
resources)  
CLINICAL  
REFERENCE —  
SOURCE BACKED  
Acute cholecystitis surgery Early (ideally ≤72 h; NICE ≤1 week) CLINICAL  
REFERENCE —  
SOURCE BACKED  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 147*

Rule Number Tag  
Transfer escort NEWS2 ≤4 porter+nurse; 5–6 nurse+monitor; ≥7  
doctor+nurse+full monitoring  
SIMULATOR  
DESIGN VALUE  
(PROVISIONAL) C  
ONTEXT-DEPENDE  
NT —  
INSTITUTIONAL  
PROTOCOL /  
CLINICIAN  
CONFIRMATION  
Deterioration clock (untreated S2) HR +5–10 and NEWS2 +1 about every 30 min SIMULATOR  
DESIGN VALUE  
(PROVISIONAL)  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 148*


# Part 18 — Dictionary of every medical word and

abbreviation  
Word / abbreviation Meaning  
AAS Adult Appendicitis Score (0–23).  
ABCDE Airway, Breathing, Circulation, Disability, Exposure — emergency check order.  
ABG / VBG Arterial / venous blood gas — quick test of oxygen, acid and lactate.  
Abscess A pocket of pus.  
ACE inhibitor / ARB Types of blood-pressure tablet (e.g. ramipril / losartan).  
ACVPU Alert, Confusion, Voice, Pain, Unresponsive.  
Acute Sudden/recent.  
AF (atrial fibrillation) Irregular, often fast heart rhythm.  
AIR score Appendicitis Inflammatory Response score (0–12).  
AKI Acute kidney injury — sudden drop in kidney function.  
Albumin Blood protein made by the liver.  
ALP Alkaline phosphatase — bile-duct enzyme.  
ALT Alanine aminotransferase — liver-cell enzyme.  
Alvarado score Older appendicitis score (0–10).  
Anaemia Low haemoglobin.  
Anaesthetist Doctor who gives anaesthesia.  
Anaphylaxis Severe life-threatening allergic reaction.  
Antiemetic Anti-sickness medicine.  
Antiplatelet Medicine stopping platelets sticking (aspirin, clopidogrel).  
Anticoagulant Blood thinner (warfarin, apixaban…).  
Appendicitis Inflamed appendix.  
Appendicolith Hard stone inside the appendix.  
APTT Activated partial thromboplastin time — clotting test.  
ASA grade Fitness-for-anaesthesia scale 1–6 (+E).  
Aspiration Stomach contents going into the lungs.  
AST Aspartate aminotransferase — liver/muscle enzyme.  
β-hCG Pregnancy hormone.  
Biliary colic Gallstone pain that comes and goes.  
Bilirubin Yellow pigment cleared in bile.  
BMI Body mass index = weight ÷ height².  
Bolus Quick dose of fluid/medicine.  
BP Blood pressure.  
Bradycardia Slow heart rate.  
Cannula Plastic tube in a vein.  
CBD Common bile duct.  
CBG Capillary blood glucose (finger-prick sugar).  
Cholangitis Infected bile duct — emergency.  
Cholecystectomy Gallbladder removal.  
Cholecystitis Inflamed gallbladder.  
Choledocholithiasis Stone in the common bile duct.  
Cholestatic Bile-stuck pattern of liver tests.  
Chronic Long-lasting.  
CKD Chronic kidney disease.  
Comorbidity Other long-term illness.  
Contraindication Reason not to give a treatment.  
Conversion Switching from keyhole to open surgery.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 149*

Word / abbreviation Meaning  
COPD Chronic obstructive pulmonary disease (long-term lung disease).  
CRP C-reactive protein — inflammation marker.  
CT Computed tomography — detailed X-ray scan.  
Crossmatch Reserving matched blood.  
Diastolic Bottom BP number (heart relaxing).  
Differential diagnosis List of possible causes.  
DKA Diabetic ketoacidosis — diabetic emergency.  
DOAC Direct oral anticoagulant (apixaban, rivaroxaban, edoxaban, dabigatran).  
ECG Electrocardiogram — heart electrical tracing.  
Ectopic pregnancy Pregnancy outside the womb.  
ED Emergency Department.  
eGFR Estimated glomerular filtration rate — kidney score.  
Elective Planned in advance.  
ERCP Endoscopic retrograde cholangiopancreatography — camera procedure to clear bile duct stones.  
EUS Endoscopic ultrasound.  
FBC / CBC Full / complete blood count.  
G&S Group and save (blood bank).  
GA General anaesthesia.  
GCS Glasgow Coma Scale (3–15).  
GGT Gamma-glutamyl transferase — bile/alcohol enzyme.  
GLP-1 agonist Diabetes/weight injection (e.g. semaglutide) that slows stomach emptying.  
Guarding Tummy muscles tensing over a painful area.  
Haemoglobin (Hb) Oxygen-carrying protein.  
Haemolysis Red cells bursting in a sample (false high potassium).  
HbA1c 3-month average sugar.  
HR Heart rate.  
Hyper- / hypo- Too high / too low.  
Hypertension / hypotension High / low blood pressure.  
Hypoglycaemia Low blood sugar.  
Hypothermia Low body temperature.  
INR International normalised ratio — clotting time.  
Intubation Putting a breathing tube in the windpipe.  
IV Intravenous — into a vein.  
Jaundice Yellow skin/eyes.  
Ketones Acids from fat-burning.  
Lactate Marker of oxygen shortage in tissues.  
Laparoscopic Keyhole surgery with a camera.  
LFTs Liver function tests.  
Lipase / amylase Pancreas enzymes.  
LMP Last menstrual period.  
LMWH Low-molecular-weight heparin — clot-prevention injection.  
MAP Mean arterial pressure.  
MCV Mean cell volume — red-cell size.  
MRCP Magnetic resonance cholangiopancreatography — MRI of bile ducts.  
MRI Magnetic resonance imaging.  
Murphy's sign Pain stopping breath-in when gallbladder pressed.  
NBM / NPO Nil by mouth.  
Neutrophils Main bacteria-fighting white cells.  
NEWS2 National Early Warning Score 2.  
NG tube Nasogastric tube — nose to stomach.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 150*

Word / abbreviation Meaning  
NICE UK National Institute for Health and Care Excellence (guideline body).  
NSAID Non-steroidal anti-inflammatory drug (ibuprofen etc.).  
Opioid Strong painkiller (morphine etc.).  
OSA Obstructive sleep apnoea.  
Optimisation Improving fitness before surgery.  
Perforation A hole/burst in an organ.  
Peritonitis Inflamed tummy lining.  
Platelets Clot-starting cell fragments.  
PONV Post-operative nausea and vomiting.  
Pre-operative Before the operation.  
PT Prothrombin time (clotting).  
qSOFA Quick Sequential Organ Failure Assessment (sepsis risk).  
RBC Red blood cell.  
Rebound tenderness Pain when pressure released.  
RIF / RLQ Right iliac fossa / right lower quadrant — lower right tummy.  
RR Respiratory (breathing) rate.  
RSI Rapid sequence induction.  
RUQ Right upper quadrant — upper right tummy.  
SBAR Situation, Background, Assessment, Recommendation.  
Sepsis Organ-damaging response to infection.  
SGLT2 inhibitor Diabetes tablet (dapagliflozin etc.) that can cause hidden DKA.  
Shock Organs not getting enough blood/oxygen.  
Sign / symptom Found by examiner / felt by patient.  
SpO2 Oxygen saturation by pulse oximeter.  
Sterile Germ-free.  
STOP-BANG Sleep apnoea questionnaire.  
Systolic Top BP number (heart squeezing).  
Tachycardia Fast heart rate.  
Teach-back Patient explains back to check understanding.  
TG18 Tokyo Guidelines 2018 for gallbladder/bile duct infection.  
Titrate Give small doses and adjust to effect.  
Triage Sorting by urgency.  
Troponin Heart-damage blood marker.  
U&E Urea and electrolytes.  
Ultrasound (US) Sound-wave scan.  
Urinalysis Urine dipstick test.  
VRIII Variable-rate intravenous insulin infusion (insulin drip).  
VTE / DVT / PE Clots in veins / leg / lungs.  
WBC White blood cell count.  
WHO World Health Organization.  
WSES World Society of Emergency Surgery.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 151*


# PART B — Clinical Accuracy Refinements (v5)


## B.1 Corrections and clarifications

Area v4 wording / logic v5 wording / logic Source Type  
(A  
direct,  
B deri  
ved, C  
contex  
t, D si  
mulati  
on)  
Trainee role 'Give pain relief', 'treat',  
'prescribe' in trainee columns  
Recognise → communicate → initiate workflow;  
clinician confirms and prescribes  
Educational design; professional  
scope  
B  
SpO2 'SpO2 92–93 → give oxygen' Verify reading, assess patient/airway/breathing,  
choose target (94–98 / 88–92), oxygen if  
indicated, find cause, reassess, escalate  
BTS 2017; RCP NEWS2 A/B  
Sepsis NEWS2 thresholds presented as  
the decision  
NEWS2 = risk stratification; combine with  
suspected infection, organ dysfunction,  
appearance, trend; NG253 tiers  
NICE NG253 (2025); Sepsis-3;  
SSC 2021  
A  
Pregnancy Default age band 12–55 Clinical question: is pregnancy possible? Ask  
privately; test with consent if any doubt  
NICE NG45 A  
WHO Time Out 'Surgeon, anaesthetist and  
nurse confirm patient, site,  
procedure'  
'Confirm the patient's name, procedure, and  
where the incision will be made'  
WHO 2009 A  
WHO Sign In 'Anaesthesia safety check' 'Anaesthesia machine and medication check  
complete'  
WHO 2009 A  
WHO additions Listed as 'local additions' Labelled 'ProcSense educational extension' —  
not WHO items  
WHO 2009 (encourages local  
additions)  
D  
Postponement  
wording  
'S1: POSTPONE' in diagrams 'Verify → review → usually optimise first  
(defer/re-time)'  
AAGBI/BHS 2016; CPOC B  
BP decision Grade 3 'S1 usually deferred  
after review'  
Anaesthetist review — planned surgery  
normally deferred for BP control  
AAGBI/BHS 2016 A/B  
Scope Pakistan legal/regulator layer in  
core  
International principles; institution-specific  
items flagged CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION  
Scope decision C  
Sepsis  
antibiotics  
(S2)  
'Within 1 hour if sepsis' Per NG253 tier: 1 h high, 3 h moderate, 6 h low;  
WSES: pre-operative antibiotics for all  
appendectomies  
NICE NG253; WSES 2025 A  

## B.2 Consistency audit — concepts checked across the whole document

Concept Rule used everywhere in v5 Sections checked  
Potassium Reference 3.5–5.3; hyperkalaemia ≥5.5 (mild 5.5–5.9, moderate 6.0–6.4, severe ≥6.5  
or ECG change); planned-surgery acceptance 3.0–5.5 is interpretation (B)  
4/5/14/17/D/H  
Sodium Reference 133–146 (Pathology Harmony) or 135–145 by lab; postpone <130/>150 =  
interpretation  
5/14/17/H  
BP (elective) Proceed <180/110 clinic; normally defer ≥180/110 on repeat after anaesthetist  
review; emergency not delayed for BP alone  
4/11/12/14/F/H  
SpO2 Target 94–98 (88–92 hypercapnic risk); verify and assess before oxygen 4/14/16/C/H  
Sepsis NG253 tiers; NEWS2 not a diagnosis; lactate ≥2 4/11/13/14/D/G/H  
Fasting 2 h clear / 6 h light meal or milk / 8 h heavy meal; emergency = full stomach, RSI;  
GLP-1 continue + anaesthetist risk assessment  
8/9/11/14/D  
Antibiotic timing Prophylaxis within 60 min before incision (WHO/NICE NG125); S2 pre-op antibiotics all  
(WSES); sepsis per NG253  
8/13/15/C/H  
Appendectomy  
timing  
Generally prompt; WSES 2025: within 24 h of admission when surgery is indicated;  
sooner if complicated/septic; set by surgeon and anaesthetist  
2/5/13/G/H  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 152*

Concept Rule used everywhere in v5 Sections checked  
Complicated  
appendicitis  
antibiotics  
2–3 days post-op after source control (WSES 2025) 8/13/G/H  
Pregnancy Clinical question + consent; no age rule 5/11/13/14/D/H  
WHO checklist 2009 wording; extensions labelled 11/12/15/C/F/G  
Trainee  
prescribing  
Never independent; workflow initiation All parts  
Where a concept is deliberately treated differently (e.g. S1 deferral vs S2 correction-in-parallel), the context is  
stated next to the rule.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 153*


# PART C — Expanded VR Clinical Reasoning (Medical

→ Trainee → VR)  
Each card is a bridge from medical truth to what the student sees and does. Read with Part 16 (player view)  
and the maps in Parts F and G. No code. Where a VR event is a deliberate teaching device rather than  
something taken from a clinical guideline, it is labelled ProcSense educational/simulation extension —  
clinician validation required.  

### C.1 Identity check

Why Prevent wrong-patient care  
Trainee action Open questions; compare wristband + notes; allergy  
What the trainee sees Patient, wristband, folder  
Information Name/DOB/MR no.; allergy  
Interpretation Match = safe to proceed  
Decision Is identity certain?  
Correct action Actions unlock  
Incorrect action Reading name to patient → agrees wrongly  
Missed abnormality Wrong-blood event later  
Escalation Admissions/nurse  
Consequence Mismatch blocks all actions  
Next step History  

### C.2 History

Why Most diagnoses and dangers come from questions  
Trainee action Structured questions incl. red flags, drugs, fasting, pregnancy  
What the trainee sees Patient, clerking form  
Information Answers (hidden until asked)  
Interpretation Red flags change pathway  
Decision Any blocker or red flag?  
Correct action Correct branch triggered  
Incorrect action Skipped questions  
Missed abnormality Hidden facts surface at Sign In  
Escalation Senior for red flags  
Consequence Case may stop later  
Next step Observations  

### C.3 Observations / NEWS2

Why Detect instability; baseline  
Trainee action Attach devices; count RR; calculate NEWS2  
What the trainee sees Monitor, cuff, probe, chart  
Information Values + score  
Interpretation Score = urgency of response, not diagnosis  
Decision Which response tier?  
Correct action Correct obs frequency/escalation  
Incorrect action Accepting one reading without repeat  
Missed abnormality Trend missed → deterioration  
Escalation Senior if NEWS2 ≥5/single 3  
Consequence Deterioration clock  
Next step Examination  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 154*


### C.4 Low SpO2

Why Possible hypoxaemia or artefact  
Trainee action Look, assess A/B, check probe, target, oxygen per protocol, cause, reassess  
What the trainee sees Patient, waveform, masks  
Information SpO2 + waveform quality  
Interpretation Real vs artefact; target range  
Decision Oxygen needed? Cause?  
Correct action SpO2 to target; cause found  
Incorrect action Blind high-flow O2 / ignoring  
Missed abnormality Hypoxia worsens  
Escalation Anaesthetist  
Consequence Breathing branch  
Next step Continue  

### C.5 High BP (S1)

Why Peri-operative CV risk  
Trainee action Repeat correctly; GP readings; symptoms  
What the trainee sees Cuff, monitor  
Information Repeat values  
Interpretation <180/110 proceed; ≥180/110 review  
Decision Defer or proceed?  
Correct action Correct decision documented  
Incorrect action Cancel on one reading / rapid lowering  
Missed abnormality Avoidable cancellation  
Escalation Anaesthetist  
Consequence Deferral or proceed  
Next step Continue  

### C.6 Fever (S1)

Why Infection in planned patient  
Trainee action Examine (Murphy's, jaundice), bloods, US  
What the trainee sees Thermometer, patient  
Information T, WBC, CRP, LFTs  
Interpretation Acute cholecystitis/cholangitis  
Decision Is this still elective?  
Correct action Acute pathway  
Incorrect action Paracetamol and proceed  
Missed abnormality Stopped at Sign In / sepsis  
Escalation Senior  
Consequence Pathway change  
Next step Acute branch  

### C.7 Possible sepsis

Why Organ dysfunction from infection  
Trainee action Infection? NEWS2 + single params + organ dysfunction + trend; SBAR  
What the trainee sees Monitor, patient, lactate  
Information NEWS2 tier, lactate  
Interpretation NG253 risk tier  
Decision High/moderate/low?  
Correct action Antibiotics/fluids timed by team  
Incorrect action Delay for results  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 155*

Missed abnormality Shock  
Escalation Senior; critical care  
Consequence Timed bundle  
Next step Source control  

### C.8 Potassium abnormal

Why Arrhythmia risk  
Trainee action Lab comment; ECG; kidneys; drugs; repeat  
What the trainee sees Results, ECG  
Information K value, ECG  
Interpretation Artefact vs real; severity  
Decision Emergency or repeat?  
Correct action Correct response  
Incorrect action Treat artefact / ignore real  
Missed abnormality Arrhythmia  
Escalation Anaesthetist; emergency team  
Consequence Treatment or proceed  
Next step Continue  

### C.9 Hypoglycaemia

Why Brain at risk  
Trainee action Check glucose; initiate protocol; recheck  
What the trainee sees Meter, patient  
Information CBG  
Interpretation <4 = treat now  
Decision Start protocol?  
Correct action Glucose recovers  
Incorrect action Recheck later only  
Missed abnormality Seizure/coma  
Escalation Nurse; anaesthetist  
Consequence Fasting implications  
Next step Medication review  

### C.10 Liver tests abnormal (S1)

Why Possible duct stone  
Trainee action Pattern + duct width + ASGE risk  
What the trainee sees Results, US report  
Information LFTs, CBD  
Interpretation Risk tier  
Decision MRCP/ERCP?  
Correct action Planned op deferred appropriately  
Incorrect action Proceed anyway  
Missed abnormality Retained stone → cholangitis  
Escalation Senior  
Consequence Biliary pathway  
Next step Rebook  

### C.11 Pregnancy possible

Why Changes diagnosis and safety  
Trainee action Ask privately; consent; test  
What the trainee sees Test kit  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 156*

Information Result  
Interpretation Positive + pain: ectopic pregnancy is a differential diagnosis that can redirect the pathway  
Decision Pathway change?  
Correct action Correct team  
Incorrect action Not asking  
Missed abnormality Safety failure  
Escalation Senior; gynaecology/obstetrics  
Consequence Pathway switch  
Next step Continue/redirect  

### C.12 Appendicitis score

Why Probability guides imaging  
Trainee action Calculate AIR/AAS  
What the trainee sees Score UI  
Information Band  
Interpretation Low/intermediate/high  
Decision Imaging?  
Correct action Right scan  
Incorrect action Skipping score  
Missed abnormality Over/under-imaging  
Escalation Senior  
Consequence Imaging choice  
Next step Imaging  

### C.13 Imaging result

Why Confirms/redirects  
Trainee action Read report; act  
What the trainee sees Viewer, report  
Information Category  
Interpretation Positive/complicated/equivocal/negative  
Decision Next step?  
Correct action Correct pathway  
Incorrect action 'Not seen' = negative  
Missed abnormality Perforation  
Escalation Senior/radiology  
Consequence Decision  
Next step Decision  

### C.14 Medicine needed

Why Pain, nausea, fluids, antibiotics  
Trainee action Recognise need; allergy check; request via workflow  
What the trainee sees Drug chart, nurse  
Information Confirmed prescription  
Interpretation Who prescribes? (clinician)  
Decision Initiate?  
Correct action Clinician confirms; nurse gives  
Incorrect action Requests allergen  
Missed abnormality Nurse challenge/reaction  
Escalation Prescriber  
Consequence Treatment given  
Next step Reassess  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 157*


### C.15 Anticoagulant

Why Bleeding vs clot risk  
Trainee action Ask last dose time; renal function  
What the trainee sees Drug list  
Information Times  
Interpretation Within stop window?  
Decision Proceed/defer?  
Correct action Correct decision  
Incorrect action Not asking  
Missed abnormality Sign In stop  
Escalation Anaesthetist/senior  
Consequence Deferral  
Next step Plan  

### C.16 Fasting

Why Aspiration risk  
Trainee action Exact times/type; GLP-1  
What the trainee sees Patient  
Information Times  
Interpretation Breach? Full stomach?  
Decision Inform anaesthetist?  
Correct action Re-time / RSI  
Incorrect action Rounding times  
Missed abnormality Aspiration  
Escalation Anaesthetist  
Consequence Timing change  
Next step Prep  

### C.17 Consent

Why Legal/ethical permission  
Trainee action Explain, risks, alternatives, teach-back  
What the trainee sees Patient, form  
Information Questions  
Interpretation Capacity, understanding  
Decision Valid?  
Correct action Signed  
Incorrect action Missing risks / confused patient  
Missed abnormality Invalid consent  
Escalation Senior  
Consequence Blocker  
Next step Prep  

### C.18 Ward preparation

Why Prevent theatre errors  
Trainee action Checklist items  
What the trainee sees Checklist  
Information Gaps  
Interpretation Complete?  
Decision Transfer?  
Correct action Ready  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 158*

Incorrect action Razor; missing consent  
Missed abnormality Cancellation  
Escalation Nurse  
Consequence Transfer  
Next step Transfer  

### C.19 Transfer & handover

Why Safe movement; information  
Trainee action Escort by stability; SBAR  
What the trainee sees Trolley, monitor  
Information Handover  
Interpretation Complete?  
Decision Safe?  
Correct action Accepted  
Incorrect action Omissions  
Missed abnormality Questions/stop  
Escalation Anaesthetist  
Consequence Sign In  
Next step Sign In  

### C.20 WHO Sign In

Why Pre-induction safety  
Trainee action Answer/lead WHO items  
What the trainee sees Board  
Information Items  
Interpretation All satisfied?  
Decision Proceed?  
Correct action Induction  
Incorrect action Skipped item  
Missed abnormality Challenge/stop  
Escalation Team  
Consequence Induction  
Next step Theatre prep  

### C.21 WHO Time Out

Why Pre-incision safety  
Trainee action WHO items in order  
What the trainee sees Board, team  
Information Items  
Interpretation All satisfied?  
Decision Ready?  
Correct action Ready for incision  
Incorrect action Antibiotic timing missed  
Missed abnormality Stop/debrief  
Escalation Surgeon  
Consequence Procedure start  
Next step Procedure  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 159*


### C.22 Intra-operative event — ProcSense educational/simulation extension — clinician

validation required.  
Why e.g. conversion to open, bleeding needing suction, normal appendix found (surgeon's decisions; the trainee  
observes and communicates)  
Trainee action Observe; respond to team requests  
What the trainee sees Theatre  
Information Event  
Interpretation Team management  
Decision Communicate?  
Correct action Smooth teamwork  
Incorrect action Freezing/no communication  
Missed abnormality Debrief  
Escalation Surgeon/anaesthetist  
Consequence Plan update  
Next step Sign Out  

### C.23 WHO Sign Out

Why Counts, specimen, concerns  
Trainee action Participate  
What the trainee sees Board  
Information Items  
Interpretation Complete?  
Decision Leave OR?  
Correct action Handover  
Incorrect action Specimen not read aloud  
Missed abnormality Lost specimen  
Escalation Team  
Consequence Recovery  
Next step Recovery  

### C.24 Recovery handover & debrief

Why Continuity of care; learning  
Trainee action Hand over key concerns  
What the trainee sees Recovery bay  
Information Summary  
Interpretation —  
Decision —  
Correct action Case closes  
Incorrect action Omits antibiotic plan  
Missed abnormality Debrief  
Escalation Recovery nurse  
Consequence End  
Next step Debrief  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 160*

PART C (continued) — Event-level Medical → VR table  
The bridge between medical truth and what must be built. No code — plain descriptions.  
Medical event Clinical  
meaning  
Trainee action VR  
interaction  
NPC  
response  
Monitor  
response  
Info shown Correct  
outcome  
Incorrect  
outcome  
Escalation Equipment Source Validation  
Wrong identity Risk of  
wrong-patient  
care  
Ask name/DOB;  
compare band  
Read band;  
dialogue  
Nurse confirms  
or flags  
— Band text,  
notes  
ID unlocks  
actions  
Wrong-blood  
event  
Admissions Wristband WHO SSC —  
Undeclared  
allergy  
Risk of  
anaphylaxis  
Ask reaction;  
apply band  
Dialogue;  
band  
Nurse updates — Allergy  
record  
Safe drug  
choice  
Reaction  
branch  
— Allergy band NICE CG183 —  
Fever in  
planned  
patient  
Possible acute  
cholecystitis  
Examine, bloods,  
US  
Thermomete  
r, exam  
Senior reviews T 38.4, HR  
108  
WBC/CRP/US Acute pathway Stop at Sign In Senior Thermomete  
r  
TG18 —  
Jaundice +  
cholestatic  
LFTs  
Possible duct  
stone  
ASGE risk; senior Results  
panel  
Senior plans  
ERCP/MRCP  
— LFTs, duct  
width  
Postpone  
(success)  
Cholangitis  
later  
Senior — ASGE 2019 Access  
INSTITUTION /  
CLINICIAN  
BP ≥180/110  
(S1)  
Severe  
hypertension  
Repeat properly Cuff Anaesthetist  
postpones  
BP values GP record? Usually  
deferred +  
refer  
Anaesthetist  
stops case  
Anaesthetist Cuff AAGBI/BHS  
2016  
—  
HbA1c >69  
(S1)  
Poor diabetes  
control  
Discuss  
optimisation  
Results Anaesthetist/di  
abetes team  
— HbA1c, CBG Usually  
deferred  
(senior  
decides)  
Debrief Anaesthetist Glucose  
meter  
CPOC —  
K 6.4  
haemolysed  
Likely artefact ECG + repeat Results, ECG Lab comment Normal ECG Repeat 4.6 Proceed Over-treatmen  
t  
Anaesthetist ECG UKKA 2023 —  
K ≥6.5 / ECG  
change  
Arrhythmia risk Emergency  
treatment  
(clinician-dosed)  
ECG, phone Senior arrives Peaked T on  
monitor  
K value K falls on  
repeat  
Arrest at  
induction  
Emergency  
team  
ECG,  
monitor  
UKKA 2023 Protocol  
INSTITUTION /  
CLINICIAN  
New AF Rhythm disorder ECG; triggers Pulse  
palpation,  
ECG  
Medical review Irregular HR  
128  
ECG strip S1 usually  
deferred after  
review / S2  
stabilise  
Instability at  
induction  
Medical  
team  
ECG ESC 2024 —  
Positive hCG +  
pain  
Ectopic until  
proven otherwise  
Gynae,  
resuscitate  
Test kit Gynaecologist  
arrives  
HR rises/BP  
falls if  
bleeding  
hCG, Hb Redirect Safety failure Gynaecology Test kit NICE NG126 —  
Rising NEWS2  
(S2)  
Deterioration /  
sepsis  
ABCDE; sepsis  
tier  
Chart,  
monitor  
Nurse alerts Trend  
worsening  
NEWS2  
value  
Antibiotics/flui  
ds timed  
Shock Senior/critic  
al care  
Monitor NICE NG253 Tool INSTITUTION  
/ CLINICIAN  
Lactate ≥2 Hypoperfusion Fluids, repeat Blood gas — — Lactate Falls on repeat Shock Senior — SSC 2021 —  
Hypotension  
≤90  
Shock ABCDE, fluids,  
cause  
Monitor,  
drips  
Team arrives BP rises with  
fluid  
BP/MAP Stabilised Arrest Emergency Monitor,  
fluids  
NEWS2; SSC —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 161*

Medical event Clinical  
meaning  
Trainee action VR  
interaction  
NPC  
response  
Monitor  
response  
Info shown Correct  
outcome  
Incorrect  
outcome  
Escalation Equipment Source Validation  
SpO2 below  
target  
Possible  
hypoxaemia (or  
artefact)  
Look, assess air  
way/breathing,  
check probe,  
then oxygen to  
target per  
protocol; find  
cause  
Probe,  
masks, chest  
exam  
Nurse confirms  
reading;  
anaesthetist if  
worsening  
SpO2 rises if  
real and  
treated;  
unchanged if  
artefact  
fixed  
SpO2 +  
waveform  
Target  
reached; cause  
found  
Blind high-flow  
O2 to a CO2  
retainer /  
ignoring a real  
low  
Anaesthetist O2 kit BTS 2017;  
NEWS2  
Protocol  
INSTITUTION /  
CLINICIAN  
Opioid toxicity RR ≤8, drowsy Stimulate, O2,  
antidote  
(clinician)  
Avatar,  
monitor  
Anaesthetist RR rises RR, sedation Recovers Arrest Anaesthetist O2 NEWS2; BNF Dose  
PATIENT-SPECIFIC  
DOSE — CLINICIAN  
CONTROLLED  
Anaphylaxis Severe allergy Stop drug; IM  
adrenaline  
Drug UI Team arrives BP falls,  
SpO2 falls →  
recover  
Rash,  
wheeze  
Recovers Arrest Emergency  
team  
Resus trolley RCUK 2021 —  
Hypoglycaemia Low sugar Treat per  
protocol  
Glucose  
meter  
Nurse — CBG Alert again Seizure Diabetes  
team  
Meter JBDS —  
Fasting breach Aspiration risk Tell anaesthetist Dialogue Anaesthetist  
re-times  
— Food  
time/type  
Re-time/postp  
one (S1) / RSI  
(S2)  
Aspiration Anaesthetist — ASA  
2017/2023  
Policy  
INSTITUTION /  
CLINICIAN  
Anticoagulant  
taken  
Bleeding risk Last dose time Dialogue Anaesthetist/s  
urgeon  
— Drug chart Defer (S1;  
senior  
decision) /  
reversal  
decision (S2)  
Bleeding Senior/haem  
atology  
— PAUSE 2019 Plan INSTITUTION  
/ CLINICIAN  
GLP-1 user  
with nausea  
Delayed gastric  
emptying  
Report to  
anaesthetist  
Dialogue Anaesthetist  
decides  
— Drug history Plan / delay Aspiration Anaesthetist — UK  
consensus  
2025  
Policy  
INSTITUTION /  
CLINICIAN  
Equivocal  
ultrasound  
Uncertain  
diagnosis  
CT/MRI or  
observe  
Report Senior — Report Clarified Perforation Senior US WSES 2025 —  
Perforation on  
CT  
Complicated  
appendicitis  
Urgent surgery Report Senior Fever/tachyc  
ardia  
CT report Theatre Sepsis Senior CT WSES 2025 —  
Consent with  
confusion  
No capacity Treat cause;  
senior  
Dialogue Senior — Teach-back  
fails  
Valid route Invalid consent Senior Form GMC Law INSTITUTION /  
CLINICIAN  
Minor patient Guardian  
consent  
Involve guardian Dialogue Guardian  
arrives  
— Age Valid consent Invalid Senior Form Contract Act  
s.11  
Law INSTITUTION /  
CLINICIAN  
Equipment  
fault  
Unsafe theatre Report, replace Inspect Theatre staff  
fix  
— Fault  
message  
Ready Time Out fails Theatre lead Tower/CO2 AoA 2019 —  
Corridor  
deterioration  
Instability Stop, ABCDE Portable  
monitor  
Help arrives SpO2 falls Monitor Stabilised Arrest Emergency Transfer kit WHO-WFSA Rule INSTITUTION  
/ CLINICIAN  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 162*

Medical event Clinical  
meaning  
Trainee action VR  
interaction  
NPC  
response  
Monitor  
response  
Info shown Correct  
outcome  
Incorrect  
outcome  
Escalation Equipment Source Validation  
Sign In item  
missing  
Checklist failure Resolve Board Team  
challenges  
— Item Induction Case stops — Board WHO SSC —  
Antibiotic  
timing >60 min  
Prophylaxis  
outside window  
Re-dose/decide Board Anaesthetist — Time given Ready Debrief — Chart WHO SSC;  
NICE NG125  
Re-dose rule  
INSTITUTION /  
CLINICIAN  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 163*


# PART D — Expanded Stacked Cases (full playable

scenarios)  
Each case is a complete clinical reasoning scenario. Values marked as case values are synthetic  
simulator-generated numbers chosen to sit clearly inside the relevant clinical band; the bands themselves are  
source-backed. The trainee is never asked to make decisions outside their role. Part 14B keeps 20 shorter  
stacked cases for variety.  
How to prioritise (used in every case)  
Priority 1 — immediate threat to airway, breathing or circulation (or brain: glucose). Priority 2 — potentially serious  
physiological abnormality. Priority 3 — condition needing optimisation before proceeding. Priority 4 — important but  
non-urgent finding.  

## STACKED CASE S1-A — Diabetes medication error: hypoglycaemia +

SGLT2 inhibitor taken + poor long-term control (with raised BP)  
Procedure: Elective laparoscopic cholecystectomy (S1). Clinical plausibility: Type 2 diabetes and  
hypertension commonly coexist; patients on several diabetes drugs frequently misunderstand fasting  
instructions, and taking a sulfonylurea while fasting causes hypoglycaemia (JBDS/CPOC). SGLT2 inhibitors are  
increasingly common and carry euglycaemic-DKA risk when fasting (UK consensus 2025).  
1\. Case introduction  
Patient Mr Imran Siddiqui, 58, accountant. Synthetic patient.  
Why in hospital Planned keyhole gallbladder removal for recurrent biliary colic (ultrasound-confirmed gallstones).  
Background Type 2 diabetes for 9 years; hypertension for 6 years; BMI 31; ex-smoker.  
Medicines Metformin, gliclazide (sulfonylurea), dapagliflozin (SGLT2 inhibitor), amlodipine, ramipril.  
Allergies None known.  
Pre-assessment  
instructions  
Omit gliclazide on the day; omit dapagliflozin the day before and the day of surgery (UK 2025 consensus); fast  
from food after midnight; water until 06:30.  
2\. Initial patient presentation  
08:05, day-surgery bay. Mr Siddiqui is sitting on the trolley, slightly pale and sweaty, talking normally but says  
he feels 'a bit shaky and hungry'. He is fully alert and orientated. Breathing looks comfortable. The nurse has  
done a first set of observations and a finger-prick glucose because he is diabetic.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
BP 174/102 →  
repeat  
172/100  
<140/90 reference;  
grade 2  
160–179/100–109 (ESH  
2023\)  
Raised, not severe  
(<180/110) — moderate  
concern for planned surgery  
Chronic hypertension,  
anxiety, ramipril taken  
today?  
GP readings in letter;  
repeat after rest  
HR 96 regular 60–100 reference;  
NEWS2 91–110 = 1  
Mildly raised Adrenaline response to  
low sugar, anxiety  
Recheck after glucose  
treated  
RR 16 12–20 Normal — —  
Temperature 36.6 °C 36.1–38.0 (NEWS2 0) Normal — no sign of infection — —  
SpO2 97% air ≥96% (NEWS2 0) Normal — —  
Capillary  
glucose  
3.6 mmol/L Target 6–10  
peri-operatively; <4.0 =  
hypoglycaemia (JBDS)  
Hypoglycaemia —  
immediate concern  
Gliclazide taken while  
fasting  
Ask what he took this  
morning; recheck after  
treatment  
Pain 0/10 0–3 comfortable No attack — —  
NEWS2 1 (HR) 0 expected in planned  
patients  
Low total, but a NEWS2 of 0–1  
does not detect  
hypoglycaemia — glucose is  
not part of NEWS2  
— —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 164*

Teaching point: the scariest-looking number (BP 172/100) is NOT the most urgent problem here; the glucose of  
3.6 is.  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- Booked operation, diagnosis, medication list from  
pre-assessment  
- Pre-assessment fasting/medication instructions  
- Last GP BP readings: not included in the referral letter  
- "I took all my morning tablets as usual — I forgot about the  
instructions." (includes gliclazide, dapagliflozin, ramipril)  
- Last food: dinner at 21:00; water at 06:30  
- Last HbA1c: "about a year ago" — none in last 3 months  
- No previous anaesthetic problems; no chest pain; walks 2 km  
- Symptoms now: sweaty, shaky, hungry  
5\. First abnormality — hypoglycaemia (glucose 3.6 mmol/L)  
What the trainee sees Sweaty, shaky patient; glucose meter reads 3.6.  
What it means Blood sugar below the safe floor of 4.0 mmol/L.  
Why it matters The brain depends on glucose; untreated hypoglycaemia progresses to confusion, seizures and coma.  
What could cause it Gliclazide taken while fasting; insulin; alcohol; sepsis.  
What to verify Repeat finger-prick (clean finger); check symptoms; ask what he took.  
If confirmed Initiate the hypoglycaemia protocol immediately (he is alert and can swallow, so the protocol's oral fast-acting  
carbohydrate may apply — in a fasting patient some protocols specify IV glucose; the nurse administers);  
recheck in 10–15 min. Note: taking glucose by mouth will itself affect fasting — the anaesthetist must know.  
Who to inform Nurse (now), anaesthetist (fasting and anaesthetic plan), senior surgeon.  
6\. Second abnormality — SGLT2 inhibitor taken today (euglycaemic DKA risk)  
What the trainee sees Medication history reveals dapagliflozin taken at 07:00.  
What it means An SGLT2 inhibitor plus fasting and surgical stress can cause ketoacidosis even when glucose is normal or low  
(euglycaemic DKA).  
Why it matters DKA is dangerous and can be missed because the glucose looks 'fine'.  
What could cause it Instructions not followed.  
What to verify Check capillary ketones; ask about nausea/abdominal pain; venous blood gas if ketones raised.  
If confirmed Ketones 0.9 mmol/L (raised, not DKA). The UK 2025 consensus advises omitting SGLT2 inhibitors the day before  
and the day of surgery, so surgery today is normally deferred; the anaesthetist/senior decide.  
Who to inform Anaesthetist and senior surgeon.  
Relationship to the earlier  
problem(s)  
Related: both are failures of the diabetes medication plan. Hypoglycaemia (A) plus SGLT2 (B) means starvation  
ketosis and drug-related ketosis add together — the ketone level must be watched while he is fasting.  
7\. Third abnormality — no recent HbA1c → result 76 mmol/mol  
What the trainee sees HbA1c sent (none in last 3 months, NICE NG45/CPOC); result 76 mmol/mol.  
What it means Average glucose over 2–3 months has been high (above the 69 mmol/mol / 8.5% optimisation threshold).  
Why it matters Poor control increases infection, wound and cardiovascular complications.  
What could cause it Undertreated diabetes, adherence.  
What to verify Confirm no interfering condition (anaemia, haemoglobinopathy).  
If confirmed Refer for optimisation before rebooking (CPOC).  
Who to inform Anaesthetist; GP/diabetes team via letter.  
Relationship to the earlier  
problem(s)  
B + C: poor control plus a multi-drug regimen that he could not follow safely → the peri-operative diabetes plan  
needs redesign, not just a new date.  
8\. Prioritisation — what is the most important problem right now?  
Priority Problem Why it is ranked here  
1 — Immediate Hypoglycaemia 3.6 mmol/L Disability (D in ABCDE): can deteriorate within minutes to confusion/seizure;  
quick and simple to treat.  
2 — Potentially  
serious  
SGLT2 taken today; ketones 0.9 Risk of euglycaemic DKA during fasting/surgery; needs monitoring, and today's  
surgery is unlikely to proceed (the anaesthetist decides).  
3 —  
Optimisation  
HbA1c 76 mmol/mol Not dangerous today, but a recognised reason to optimise before planned  
surgery (CPOC).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 165*

Priority Problem Why it is ranked here  
4 — Important,  
non-urgent  
BP 172/100 Below the AAGBI/BHS 180/110 threshold: on its own would NOT stop surgery;  
needs GP follow-up.  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? Sweaty, shaky, glucose 3.6  
What does it mean? Hypoglycaemia  
What do I check? Repeat reading; alertness; can he swallow?  
What do I do? (within my  
role)  
Ask nurse to start the hypoglycaemia protocol now (oral fast-acting glucose)  
Who do I tell? Nurse; then anaesthetist  
What happens next? Recheck at 10–15 min: 5.8 mmol/L, symptoms resolve  
What changes if  
confirmed?  
Fasting status changes (oral glucose) — anaesthetist must decide  
Decision 2  
What do I notice? "I took all my tablets"  
What does it mean? Gliclazide and dapagliflozin taken  
What do I check? Exact drugs and times  
What do I do? (within my  
role)  
Check capillary ketones  
Who do I tell? Anaesthetist  
What happens next? Ketones 0.9  
What changes if  
confirmed?  
Surgery today not appropriate per guidance  
Decision 3  
What do I notice? BP 174/102  
What does it mean? Raised, but below 180/110  
What do I check? Repeat after rest; GP readings  
What do I do? (within my  
role)  
Document; not a reason by itself to cancel  
Who do I tell? Anaesthetist in the same call  
What happens next? Repeat 172/100  
What changes if  
confirmed?  
None today; GP follow-up  
Decision 4  
What do I notice? No HbA1c in 3 months  
What does it mean? Optimisation status unknown  
What do I check? Send HbA1c  
What do I do? (within my  
role)  
Await result  
Who do I tell? —  
What happens next? 76 mmol/mol  
What changes if  
confirmed?  
Optimise before rebooking  
Decision 5  
What do I notice? Overall picture  
What does it mean? Medication plan failed + poor control  
What do I check? —  
What do I do? (within my  
role)  
SBAR to senior + anaesthetist proposing deferral  
Who do I tell? Senior, anaesthetist  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 166*

What happens next? Team defers surgery; diabetes plan rewritten  
What changes if  
confirmed?  
Rebook after optimisation  
10\. Interaction between abnormalities  
A + B Hypoglycaemia + SGLT2 inhibitor: correcting low sugar is essential, but the SGLT2 inhibitor still makes ketosis  
more likely while fasting — glucose alone does not tell you the patient is safe.  
B + C SGLT2 taken + HbA1c 76: the regimen is complex and the control poor — a sign that peri-operative diabetes  
management needs specialist input.  
A + B + C — overall picture Together: a planned patient whose diabetes is unsafe for surgery today and suboptimal overall. The BP is real  
but secondary.  
11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
Repeat capillary  
glucose  
Confirm hypoglycaemia and  
response  
≥4.0, ideally 6–10 <4.0 again → repeat protocol Safety now  
Capillary ketones SGLT2 + fasting <0.6 mmol/L ≥1.5 high risk; ≥3.0 DKA  
(JBDS)  
Escalation; venous gas  
HbA1c None within 3 months (NICE  
NG45)  
≤69 mmol/mol for  
planned surgery  
\>69 → optimise (CPOC) Rebooking  
U&E Diabetes, ACE inhibitor (NICE  
NG45)  
Normal K and creatinine AKI / K abnormality Drug plan  
ECG Diabetes + hypertension, ASA  
III  
Sinus rhythm Ischaemia/LVH Cardiac assessment  
12\. Result interpretation  
Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
Glucose  
(repeat)  
5.8 mmol/L <4 hypo; 6–10 target  
(JBDS) — A  
Corrected Treated  
hypoglycaemia  
Recheck every 30–60  
min while fasting  
None alone  
Ketones 0.9 mmol/L <0.6 normal; 0.6–1.5  
raised (JBDS) — A  
Mildly raised Fasting + SGLT2 Recheck; venous gas if  
≥1.5  
Contributes to  
deferral  
HbA1c 76  
mmol/mol  
(9.1%)  
\>69 optimise (CPOC)  
— A  
Suboptimal control Long-term Diabetes team referral Optimise before  
rebooking  
Potassium 4.6 mmol/L 3.5–5.3 — A Normal — — None  
Creatinine 88 µmol/L  
(eGFR 82)  
Lab range — A Normal — — None  
ECG Sinus 92,  
LVH voltage  
Normal rhythm — A Long-standing  
hypertension  
Hypertension Note None  
13\. Surgery decision  
Can the patient proceed? Unlikely today: SGLT2 inhibitor taken on the day against UK 2025 consensus advice, a hypoglycaemic  
episode with oral glucose altering fasting, and HbA1c 76; the anaesthetist and senior surgeon decide.  
Should the patient be optimised  
first?  
Yes — diabetes optimisation and a written peri-operative medication plan; BP review by GP.  
Should surgery be delayed? Yes — deferral is the safe and correct outcome (counts as success).  
Emergency where treatment  
continues despite abnormalities?  
No — this is elective surgery.  
Who makes the final decision? Senior surgeon and anaesthetist together, with the patient.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 167*

14\. Anaesthesia decision  
Airway / breathing No concerns; BMI 31 — STOP-BANG check.  
Circulation BP 172/100 below threshold; LVH on ECG noted.  
Aspiration / fasting Oral glucose taken — fasting clock affected.  
Glucose / ketones Hypoglycaemia corrected; ketones raised → euglycaemic DKA risk with SGLT2.  
Medicines Rewrite plan: omit gliclazide on the day; SGLT2 omitted day before and day of; ACE-i plan.  
Who decides Anaesthetist leads the anaesthetic fitness decision.  
15\. Escalation  
Nurse Glucose result, symptoms, what was taken, recheck plan.  
Anaesthetist Immediately after the hypoglycaemia is treated — medications taken, ketones, fasting change, BP.  
Senior surgeon Same call — propose deferral; agree documentation and patient explanation.  
Emergency / critical-care  
team  
Only if hypoglycaemia recurs with reduced consciousness or ketones rise with acidosis (DKA).  
16\. Correct path  
Notice sweaty patient Check glucose → 3.6 Hypo protocol via nurse Medication history  
Ketones + HbA1c SBAR to  
anaesthetist/senior  
Deferral + diabetes plan +  
GP letter  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Focuses on BP 172/100  
first  
Nurse: "His sugar is 3.6." Delayed hypoglycaemia  
treatment  
Becomes confused at 15  
min (glucose 2.9)  
Treat hypo now; re-prioritise  
using ABCDE  
Treats hypo but sends  
to theatre  
Anaesthetist at Sign In asks  
about medicines  
SGLT2 + fasting → DKA  
risk  
Case stopped at Sign In Disclose medication history;  
defer  
Cancels for BP alone  
and ignores glucose  
Debrief Wrong reasoning,  
unrecognised hypo  
Glucose falls further Use AAGBI/BHS rule; treat  
glucose  
Orders glucose drink  
without telling  
anaesthetist  
Anaesthetist discovers  
later  
Fasting breach  
undeclared  
Re-timing/cancellation Report honestly  
18\. Missed-abnormality path  
Glucose 3.6 not noticed Patient becomes quieter Nurse: "He's not making  
sense" Glucose 2.4, confused  
Emergency hypo  
treatment; serious debrief  
19\. Recovery path  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 168*

Trainee recognises the  
miss  
Calls for help; nurse starts  
protocol Glucose recovers Full medication review  
Correct deferral and plan  
20\. End-of-case debrief  
What did you miss? (common misses) Usually: the medication history (what was taken today) and the HbA1c timing.  
What did you recognise correctly?  
(expected recognitions)  
Hypoglycaemia; that BP below 180/110 is not itself a cancellation reason.  
What was the highest-priority  
problem?  
Hypoglycaemia.  
Why was it the priority? It threatens brain function within minutes and is easily reversed.  
Which findings were related? Hypoglycaemia, SGLT2 use and HbA1c — all diabetes-management problems.  
Which findings were independent? BP (related only by shared risk factors).  
What should have triggered  
escalation?  
Any glucose <4.0 and any SGLT2 inhibitor taken on the day.  
What would have happened if it had  
been ignored?  
Seizure/coma from hypoglycaemia; DKA during or after surgery.  
What should you remember next  
time?  
Check glucose in every diabetic patient and ask exactly which tablets were taken today.  
Where this comes from: JBDS-IP hypoglycaemia guideline; CPOC/JBDS peri-operative diabetes guideline 2021/2023; El-Boghdadly et  
al., Anaesthesia 2025 (SGLT2/GLP-1 consensus); AAGBI/BHS 2016; ESH 2023; NICE NG45; RCP NEWS2.  
Clinician validation: Anaesthetist: diabetes medication plan, ketone thresholds and deferral decision; institutional  
hypoglycaemia protocol CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

## STACKED CASE S1-B — Anticoagulated AF + iron-deficiency anaemia +

anticoagulant taken this morning  
Procedure: Elective laparoscopic cholecystectomy (S1). Clinical plausibility: Atrial fibrillation is common in  
older adults and is treated with DOACs; anticoagulants increase occult gastrointestinal bleeding, which  
presents as iron-deficiency anaemia; patients commonly take regular doses out of habit.  
1\. Case introduction  
Patient Mr George Mensah, 69, retired teacher. Synthetic patient.  
Why in hospital Planned lap chole for recurrent biliary colic.  
Background Atrial fibrillation (CHA2DS2-VASc 3), hypertension, CKD stage G3a.  
Medicines Apixaban 5 mg twice daily, bisoprolol, amlodipine.  
Allergies None.  
Pre-assessment plan Last apixaban dose 2 days before surgery (PAUSE protocol, moderate-bleeding-risk category chosen by the  
surgeon); no bridging.  
2\. Initial patient presentation  
Calm, well-looking, slightly pale. Talks normally. Pulse feels irregular. Comfortable breathing.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
BP 138/82 <140/90 Normal — —  
HR 84  
irregularly  
irregular  
60–100 Rate controlled; rhythm  
irregular  
Known AF ECG to confirm AF rather  
than new rhythm  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 169*

Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
RR 15 12–20 Normal — —  
Temp 36.7 36.1–38.0 Normal — —  
SpO2 97% air ≥96 Normal — —  
Pain 0 — — — —  
Conjunctival  
pallor  
Present — Possible anaemia Iron deficiency, blood  
loss  
FBC, ferritin; ask about  
bleeding  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- AF, apixaban, CKD G3a on medication list  
- Pre-assessment anticoagulation instructions  
- "I took my apixaban at 7 this morning — I always do."  
- "My stools have been darker some weeks."  
- Tiredness for 3 months  
- No chest pain, no breathlessness at rest  
- Last food 20:00; water 06:00  
5\. First abnormality — irregular pulse  
What the trainee sees Irregular pulse 84.  
What it means Known AF, rate controlled.  
Why it matters Need to confirm it is the known rhythm, not something new.  
What could cause it AF.  
What to verify 12-lead ECG.  
If confirmed Known AF, rate 84 → not a new problem; anticoagulation plan is the issue.  
Who to inform Note for anaesthetist.  
6\. Second abnormality — haemoglobin 104 g/L, MCV 74 fL, ferritin 9 µg/L  
What the trainee sees Results panel flags low Hb and small cells.  
What it means Iron-deficiency anaemia (WHO 2024: anaemia <130 g/L in men).  
Why it matters Unexplained iron deficiency in an older man is a red flag for gastrointestinal blood loss, including cancer; it also  
reduces tolerance of surgical bleeding.  
What could cause it Occult GI bleeding (made more likely by anticoagulation), diet, malabsorption.  
What to verify Repeat/confirm; stool history; previous Hb.  
If confirmed Senior decision: investigate (e.g. GI referral) before elective surgery; iron replacement.  
Who to inform Senior surgeon, anaesthetist.  
Relationship to the earlier  
problem(s)  
Related to AF/apixaban: the anticoagulant may be unmasking a bleeding source.  
7\. Third abnormality — apixaban taken 07:00 today  
What the trainee sees Patient volunteers it only when asked.  
What it means Apixaban still active at the time of surgery.  
Why it matters Increases surgical bleeding risk; neuraxial techniques contraindicated.  
What could cause it Habit; instructions not understood.  
What to verify Exact time and dose; renal function (clearance).  
If confirmed Planned surgery today is normally deferred (anaesthetist/surgeon decide); a DOAC is not reversed simply to  
allow elective surgery.  
Who to inform Anaesthetist, senior surgeon.  
Relationship to the earlier  
problem(s)  
A + C: the patient needs anticoagulation for stroke prevention, so the restart plan matters; B + C: active  
anticoagulant plus anaemia increases bleeding consequences.  
8\. Prioritisation — what is the most important problem right now?  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 170*

Priority Problem Why it is ranked here  
1 — Safety  
blocker today  
Apixaban active Directly determines whether surgery can safely happen today.  
2 — Potentially  
serious  
Unexplained iron-deficiency anaemia May indicate GI bleeding or malignancy — could change whether this operation  
is the priority at all.  
3 — Plan AF (known, controlled) Requires anticoagulation interruption/restart plan, not urgent action.  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? Irregular pulse  
What does it mean? Probably known AF  
What do I check? ECG  
What do I do? (within my  
role)  
Request ECG  
Who do I tell? —  
What happens next? AF 84  
What changes if  
confirmed?  
None (known)  
Decision 2  
What do I notice? Pallor + dark stools  
What does it mean? Possible GI blood loss  
What do I check? FBC, ferritin  
What do I do? (within my  
role)  
Send bloods  
Who do I tell? Senior when results back  
What happens next? Hb 104, ferritin 9  
What changes if  
confirmed?  
Investigation needed  
Decision 3  
What do I notice? Apixaban 07:00  
What does it mean? Anticoagulated now  
What do I check? Time, creatinine  
What do I do? (within my  
role)  
Stop pathway; document  
Who do I tell? Anaesthetist + senior  
What happens next? Team defers  
What changes if  
confirmed?  
No reversal for elective surgery  
Decision 4  
What do I notice? Overall  
What does it mean? Two independent reasons to defer  
What do I check? —  
What do I do? (within my  
role)  
SBAR with plan proposal  
Who do I tell? Senior  
What happens next? Deferral; GI referral; iron; new apixaban plan  
What changes if  
confirmed?  
Rebook later  
10\. Interaction between abnormalities  
A + B AF on apixaban + anaemia: anticoagulation can unmask a bleeding lesion; the anaemia must be explained, not  
just treated.  
B + C Anaemia + apixaban today: reduced reserve plus active anticoagulant — bleeding would be poorly tolerated.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 171*

A + B + C — overall picture Together: a planned patient with an unexplained, possibly serious anaemia and an active anticoagulant —  
investigate first, re-plan anticoagulation, then rebook.  
11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
12-lead ECG Irregular pulse Sinus rhythm or known  
AF  
New arrhythmia/ischaemia Cardiac plan  
FBC Pallor, anticoagulant Hb ≥130 (men) Anaemia; MCV pattern Investigation  
Ferritin/iron  
studies  
Microcytosis Ferritin >30 Iron deficiency GI referral  
U&E / creatinine  
clearance  
Apixaban clearance (PAUSE) CrCl ≥30 Longer interruption needed Plan timing  
Coagulation Generally not useful for DOAC  
level  
— — Not routinely helpful  
12\. Result interpretation  
Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
ECG AF 84 — A Known AF Rate controlled Note None  
Hb 104 g/L <130 men anaemia  
(WHO 2024) — A  
Moderate anaemia  
(80–109)  
Iron deficiency Senior; investigate Defer for  
investigation  
MCV 74 fL \~80–100 — A Microcytic Iron deficiency — —  
Ferritin 9 µg/L <30 iron deficiency  
— A  
Iron deficient Blood loss likely GI referral —  
Creatinine 118 µmol/L  
(CrCl \~55)  
— A CKD G3a Chronic Apixaban timing —  
13\. Surgery decision  
Can the patient proceed? Normally not today (senior/anaesthetist decide).  
Should the patient be optimised  
first?  
Yes — investigate and treat iron deficiency; re-plan apixaban interruption.  
Should surgery be delayed? Deferral is the usual and safe outcome.  
Emergency where treatment  
continues despite abnormalities?  
No (elective).  
Who makes the final decision? Senior surgeon with anaesthetist; GI team for the anaemia.  
14\. Anaesthesia decision  
Circulation AF rate controlled; anaemia lowers oxygen-carrying reserve.  
Medicines Apixaban timing; neuraxial blocks contraindicated while active.  
Renal CKD affects DOAC clearance.  
Who decides Anaesthetist with surgeon.  
15\. Escalation  
Nurse ECG request; confirm drug times.  
Anaesthetist On discovering apixaban taken today and the Hb result.  
Senior surgeon Same — propose deferral; arrange anaemia investigation.  
Emergency / critical-care  
team  
Not needed unless active bleeding or instability.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 172*

16\. Correct path  
Ask about anticoagulant  
timing ECG + FBC + ferritin Recognise unexplainedanaemia  
SBAR to  
senior/anaesthetist  
Defer, refer, re-plan  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Doesn't ask when  
apixaban was last taken  
Anaesthetist asks at Sign In Bleeding risk Case stopped Take a full drug-timing history  
Suggests 'just give a  
reversal agent'  
Senior declines Unjustified  
reversal/thrombosis risk  
Teaching moment Learn: no reversal for elective  
surgery  
Treats anaemia as 'just  
mild'  
Debrief Missed GI malignancy Patient returns later with  
bleed  
Investigate unexplained iron  
deficiency  
18\. Missed-abnormality path  
Apixaban timing never  
asked Proceeds to theatre Anaesthetist asks Case cancelled at Sign In  
Debrief: missed medication  
history  
19\. Recovery path  
Trainee asks belatedly Informs anaesthetist Deferral documented Anaemia referral made  
20\. End-of-case debrief  
What did you miss? (common misses) Drug timing and stool history.  
What did you recognise correctly?  
(expected recognitions)  
Known AF; anaemia pattern.  
What was the highest-priority  
problem?  
Active anticoagulant today.  
Why was it the priority? It makes today's surgery unsafe.  
Which findings were related? Apixaban and anaemia (bleeding).  
Which findings were independent? AF itself is stable and independent.  
What should have triggered  
escalation?  
Any anticoagulant dose inside the stop window; unexplained anaemia.  
What would have happened if it had  
been ignored?  
Surgical bleeding; missed GI cancer.  
What should you remember next  
time?  
Ask the exact time of the last dose of every blood thinner.  
Where this comes from: Douketis et al., PAUSE (JAMA Intern Med 2019); WHO haemoglobin cut-offs (2024); Muñoz et al.  
peri-operative anaemia consensus (Anaesthesia 2017); ESC 2024 AF; NICE NG45.  
Clinician validation: Surgeon: bleeding-risk category of lap chole; anaesthetist: anticoagulation plan CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 173*


## STACKED CASE S1-C — True hyperkalaemia with ECG changes + acute

kidney injury + bradycardia/low BP (heart-failure medicines)  
Procedure: Elective laparoscopic cholecystectomy (S1). Clinical plausibility: ACE inhibitors and  
spironolactone (common in heart failure) raise potassium; diarrhoea/dehydration causes AKI, which reduces  
potassium excretion; hyperkalaemia and beta-blockers both slow the heart (UKKA 2023; KDIGO).  
1\. Case introduction  
Patient Mrs Helena Novak, 71. Synthetic patient.  
Why in hospital Planned lap chole.  
Background Heart failure with reduced ejection fraction; CKD G3b (baseline creatinine 112 µmol/L).  
Medicines Ramipril, spironolactone, bisoprolol, furosemide.  
Recent events Diarrhoea for 3 days last week; poor oral intake.  
Allergies None.  
2\. Initial patient presentation  
Tired, a little dizzy on standing, otherwise alert. Dry mouth. Slow pulse.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
BP 104/62  
(usual  
\~130/80)  
NEWS2 101–110 = 1 Lower than her usual (−26  
mmHg)  
Dehydration, drugs,  
slow heart  
Postural BP, fluid status  
HR 52 regular 60–100; NEWS2 41–50  
\= 1 (52 = 0)  
Bradycardia Bisoprolol,  
hyperkalaemia  
ECG, potassium  
RR 18 12–20 Normal — —  
Temp 36.5 — Normal — —  
SpO2 95% air 94–95 = NEWS2 1 Slightly low Heart failure Chest exam  
Urine Little today ≥0.5 mL/kg/h Possibly reduced Dehydration/AKI U&E  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- Medication list  
- Heart failure, CKD  
- Diarrhoea last week  
- Still taking all tablets despite poor intake  
- Dizzy on standing  
- No chest pain  
5\. First abnormality — slow heart (52) and BP lower than usual  
What the trainee sees Monitor HR 52, BP 104/62.  
What it means Bradycardia with relative hypotension.  
Why it matters May reflect dangerous potassium or drug effect; risk of collapse at induction.  
What could cause it Beta-blocker, hyperkalaemia, dehydration.  
What to verify Manual pulse, repeat BP, 12-lead ECG.  
If confirmed ECG ordered urgently.  
Who to inform Nurse; senior.  
6\. Second abnormality — potassium 6.6 mmol/L (not haemolysed) with peaked T  
waves  
What the trainee sees Lab phones a critical result; ECG shows tall peaked T waves.  
What it means Severe hyperkalaemia with ECG changes (UKKA: ≥6.5 severe; any ECG change = emergency).  
Why it matters Risk of fatal arrhythmia.  
What could cause it ACE-i + spironolactone + AKI.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 174*

What to verify Confirm patient identity on the call; lab comment (not haemolysed); ECG already abnormal so treat without  
waiting for repeat.  
If confirmed Emergency: call for senior/medical help; continuous cardiac monitoring; team gives IV calcium to protect the  
heart and insulin–glucose to shift potassium (clinician-prescribed per UKKA protocol).  
Who to inform Senior + medical/emergency team immediately; anaesthetist.  
Relationship to the earlier  
problem(s)  
Explains the bradycardia (A): hyperkalaemia slows conduction, worsened by bisoprolol.  
7\. Third abnormality — creatinine 178 µmol/L (baseline 112)  
What the trainee sees U&E result.  
What it means Acute kidney injury stage 1 (1.59 × baseline, KDIGO).  
Why it matters Kidneys cannot clear potassium; drug doses change.  
What could cause it Dehydration from diarrhoea + ACE-i/diuretic.  
What to verify Baseline value, urine output.  
If confirmed Clinician holds nephrotoxic/potassium-raising drugs; cautious fluids (heart failure).  
Who to inform Medical team.  
Relationship to the earlier  
problem(s)  
Causes B; B makes C more dangerous; fluids for C must be balanced against heart failure.  
8\. Prioritisation — what is the most important problem right now?  
Priority Problem Why it is ranked here  
1 — Immediate  
threat  
(Circulation)  
K 6.6 with ECG changes Can cause cardiac arrest within minutes; UKKA emergency.  
2 — Serious AKI with dehydration Driving the hyperkalaemia; needs careful fluids and drug review.  
3 — Linked Bradycardia / low BP Mostly a consequence of 1 and 2 plus beta-blocker; improves as they are  
treated.  
4 — Planning Elective surgery Cancelled today; medical admission.  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? HR 52, BP 104/62  
What does it mean? Possible drug/electrolyte effect  
What do I check? ECG, U&E  
What do I do? (within my  
role)  
Urgent ECG and bloods  
Who do I tell? Nurse, senior  
What happens next? ECG peaked T waves  
What changes if  
confirmed?  
Emergency  
Decision 2  
What do I notice? Critical K 6.6 phoned  
What does it mean? Severe hyperkalaemia  
What do I check? Read-back; ECG  
What do I do? (within my  
role)  
Call for help; cardiac monitor; IV access  
Who do I tell? Senior + medical/emergency team  
What happens next? Calcium + insulin–glucose by team  
What changes if  
confirmed?  
Planned surgery cancelled  
Decision 3  
What do I notice? Creatinine 178  
What does it mean? AKI  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 175*

What do I check? Baseline, urine  
What do I do? (within my  
role)  
Flag drugs to hold; fluid plan with HF caution  
Who do I tell? Medical team  
What happens next? Admitted  
What changes if  
confirmed?  
—  
Decision 4  
What do I notice? Repeat K after 1 h  
What does it mean? Response  
What do I check? Glucose monitoring (insulin)  
What do I do? (within my  
role)  
Recheck  
Who do I tell? Team  
What happens next? K 5.7  
What changes if  
confirmed?  
Continue medical care  
10\. Interaction between abnormalities  
A + B Bradycardia + hyperkalaemia: hyperkalaemia slows conduction; with a beta-blocker the heart rate falls further.  
B + C Hyperkalaemia + AKI: AKI prevents potassium excretion; ACE-i/spironolactone add to it.  
A + B + C — overall picture Together: a medical emergency created by dehydration on heart-failure drugs — surgery is irrelevant until  
resolved.  
11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
12-lead ECG Bradycardia Sinus rhythm Peaked T, wide QRS Emergency  
U&E AKI risk drugs K 3.5–5.3 Hyperkalaemia/AKI Emergency  
Venous blood gas Rapid K, acid–base pH 7.35–7.45 Acidosis worsens K Treatment  
Capillary glucose Insulin treatment 4–10 Hypoglycaemia after insulin Monitoring  
12\. Result interpretation  
Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
Potassium 6.6 mmol/L ≥6.5 severe (UKKA)  
— A  
Severe, critical Drugs + AKI Emergency team Cancelled  
ECG Peaked T,  
HR 52  
— A Hyperkalaemic  
changes  
K Emergency Cancelled  
Creatinine 178 µmol/L Stage 1 AKI ≥1.5×  
(KDIGO) — A  
AKI stage 1 Dehydration +  
drugs  
Medical Cancelled  
Repeat K (1  
h)  
5.7 — A Improving Treatment effect Continue —  
13\. Surgery decision  
Can the patient proceed? Not while hyperkalaemia with ECG changes is untreated.  
Should the patient be optimised  
first?  
Yes — medical treatment; review heart-failure drugs.  
Should surgery be delayed? Yes — the senior surgeon defers the planned operation.  
Emergency where treatment  
continues despite abnormalities?  
No (elective).  
Who makes the final decision? Senior surgeon and anaesthetist; medical team leads treatment.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 176*

14\. Anaesthesia decision  
Circulation Hyperkalaemia + bradycardia = high anaesthetic risk; suxamethonium contraindicated.  
Renal AKI; drug dosing.  
Fluids Heart failure — cautious.  
Who decides Anaesthetist would not proceed.  
15\. Escalation  
Nurse Monitor request; observation frequency.  
Anaesthetist Informed; not leading.  
Senior surgeon Immediately.  
Emergency / critical-care  
team  
Yes — emergency/medical team for ECG-change hyperkalaemia.  
16\. Correct path  
Notice slow HR ECG + bloods Critical K read-back Call for help  
Cancel; medical admission Team treats; monitor  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Assumes K is  
haemolysed and waits  
ECG shows peaked T Arrhythmia HR drops to 38 Act on ECG changes  
Gives fluids rapidly SpO2 falls (HF) Pulmonary oedema Crackles Cautious fluids with senior  
18\. Missed-abnormality path  
Bradycardia ignored No ECG K result unseen Collapse at induction  
Debrief  
19\. Recovery path  
Trainee opens result late Calls for help Emergency treatment Stabilised  
20\. End-of-case debrief  
What did you miss? (common misses) Linking bradycardia to potassium.  
What did you recognise correctly?  
(expected recognitions)  
Need for ECG.  
What was the highest-priority  
problem?  
Hyperkalaemia with ECG change.  
Why was it the priority? Immediate arrhythmia risk.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 177*

Which findings were related? All three.  
Which findings were independent? None really independent.  
What should have triggered  
escalation?  
Any K ≥6.5 or ECG change; drug combination.  
What would have happened if it had  
been ignored?  
Cardiac arrest.  
What should you remember next  
time?  
Slow heart + kidney/heart-failure drugs → think potassium.  
Where this comes from: UK Kidney Association hyperkalaemia guideline 2023; KDIGO AKI 2012; NICE NG148; RCP NEWS2;  
Resuscitation Council UK.  
Clinician validation: Medical/anaesthetic emergency workflow per institutional protocol CONTEXT-DEPENDENT —  
INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

## STACKED CASE S1-D — Patient awaiting elective cholecystectomy who

develops acute cholangitis  
Procedure: Original plan: elective laparoscopic cholecystectomy (S1). The patient's condition has changed, so  
the elective pathway no longer simply continues. Clinical plausibility: Gallstones can migrate into the  
common bile duct while patients wait for planned surgery; obstruction plus infection causes cholangitis (TG18);  
infection then produces sepsis physiology.  
1\. Case introduction  
Patient Mrs Aisha Bello, 62. Synthetic patient.  
Why in hospital Admitted for her booked (elective) lap chole after 3 episodes of biliary colic.  
What has changed Since pre-assessment she has developed fever, jaundice and rigors. The original surgical plan was elective; an  
acute infection now changes the clinical pathway.  
Background Hypertension.  
Medicines Amlodipine.  
Allergies None.  
Recent Shivering last night, dark urine, pain under right ribs.  
2\. Initial patient presentation  
Flushed, shivering intermittently, yellow tinge to eyes, uncomfortable, alert.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
BP 108/66  
(usual  
132/78)  
NEWS2 1 Lower than usual Sepsis, dehydration Repeat; lactate  
HR 112 NEWS2 2 Tachycardia Fever, sepsis Fluids response  
RR 22 NEWS2 2; qSOFA  
criterion  
Raised Sepsis SpO2, gas  
Temp 39.2 °C NEWS2 2 High fever Infection Cultures  
SpO2 96% air 0 Normal — —  
Alertness Alert 0 — — Monitor  
NEWS2 total 7 ≥7 high risk (NICE  
NG253) with infection  
High risk of severe illness  
from sepsis  
Cholangitis Senior now  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- Booked diagnosis  
- Gallstones on ultrasound  
- Rigors last night  
- Dark urine, pale stools  
- Itching  
- Constant pain since yesterday  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 178*

5\. First abnormality — fever 39.2 °C with tachycardia  
What the trainee sees Thermometer and monitor.  
What it means Infection with systemic response.  
Why it matters A planned patient should not be febrile; possible cholangitis or cholecystitis.  
What could cause it Biliary infection, urine/chest infection.  
What to verify Examine (Murphy's, jaundice), urine, chest.  
If confirmed Sepsis assessment.  
Who to inform Senior.  
6\. Second abnormality — WBC 16.4, CRP 142, bilirubin 64, ALP 410, GGT 520, ALT  
210; CBD 10 mm  
What the trainee sees Results and bedside ultrasound.  
What it means Cholestatic pattern with duct dilatation + inflammation = TG18 acute cholangitis (A + B + C definite).  
Why it matters Obstructed infected bile can rapidly cause septic shock; needs drainage.  
What could cause it Common bile duct stone.  
What to verify TG18 criteria; severity grade.  
If confirmed TG18 acute cholangitis, Grade II (WBC above 12 and fever ≥39 °C; no organ failure). TG18 advises early biliary  
drainage for moderate (Grade II) disease and urgent drainage for severe (Grade III) disease; the timing and type  
(e.g. ERCP) are decided by the senior surgeon and gastroenterology/radiology according to response and  
resources.  
Who to inform Senior surgeon; gastroenterology/ERCP team.  
Relationship to the earlier  
problem(s)  
Explains the fever: the source is the bile duct.  
7\. Third abnormality — lactate 2.6, creatinine 96 (baseline 70)  
What the trainee sees Blood gas and U&E.  
What it means Early hypoperfusion and kidney strain.  
Why it matters Organ dysfunction signals sepsis rather than simple infection.  
What could cause it Sepsis, dehydration.  
What to verify Urine output; repeat lactate after fluids.  
If confirmed Sepsis bundle per NICE NG253 high risk.  
Who to inform Senior; consider critical care outreach.  
Relationship to the earlier  
problem(s)  
A + B produce C: infection → sepsis → hypoperfusion.  
8\. Prioritisation — what is the most important problem right now?  
Priority Problem Why it is ranked here  
1 — Immediate High-risk sepsis (NEWS2 7, lactate 2.6) Circulation threat; time-critical antibiotics and fluids within 1 hour.  
2 — Serious Obstructed infected bile duct Source control (biliary drainage) is the definitive treatment.  
3 — Linked Early kidney strain Responds to resuscitation; monitor.  
4 — Planning Cholecystectomy Later, after drainage (often same admission per local practice).  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? Shivering, T 39.2, HR 112  
What does it mean? Sepsis possible  
What do I check? NEWS2, source  
What do I do? (within my  
role)  
Call senior; IV access; cultures; lactate  
Who do I tell? Senior  
What happens next? NEWS2 7 → high risk  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 179*

What changes if  
confirmed?  
Sepsis pathway  
Decision 2  
What do I notice? Yellow eyes, dark urine  
What does it mean? Biliary obstruction  
What do I check? LFTs, ultrasound  
What do I do? (within my  
role)  
Request urgently  
Who do I tell? Senior  
What happens next? Cholestatic + CBD 10 mm  
What changes if  
confirmed?  
Cholangitis  
Decision 3  
What do I notice? Lactate 2.6  
What does it mean? Hypoperfusion  
What do I check? Repeat after fluid  
What do I do? (within my  
role)  
Flag fluids/antibiotics within 1 h (prescribed by clinician)  
Who do I tell? Senior  
What happens next? Lactate 1.7 after fluids  
What changes if  
confirmed?  
Responding  
Decision 4  
What do I notice? TG18 Grade II  
What does it mean? Needs drainage  
What do I check? —  
What do I do? (within my  
role)  
Refer for ERCP  
Who do I tell? Gastro/ERCP  
What happens next? ERCP same/next day  
What changes if  
confirmed?  
Elective list cancelled  
10\. Interaction between abnormalities  
A + B Fever + cholestatic LFTs + dilated duct: the source is the bile duct (cholangitis), not the gallbladder alone.  
B + C Cholangitis + raised lactate/creatinine: sepsis with early organ dysfunction — escalate urgency.  
A + B + C — overall picture Together: the patient's condition has changed from an elective problem to an acute one; the elective pathway  
should not simply continue, and senior review, investigation, treatment and escalation follow.  
11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
Blood cultures Sepsis Negative Bacteraemia Antibiotic targeting  
Lactate Hypoperfusion <2 ≥2 Urgency  
LFTs Jaundice Normal Cholestatic Biliary pathway  
Ultrasound Duct CBD ≤6 mm Dilated ERCP  
U&E, INR, platelets Organ function; ERCP safety Normal AKI/coagulopathy Severity grade  
12\. Result interpretation  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 180*

Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
WBC 16.4 >10 abnormal (TG18)  
— A  
Raised Infection — Cancelled  
CRP 142 ≥10 (TG18) — A Raised — — —  
Bilirubin 64 µmol/L ≥34 (TG18) — A Jaundice Obstruction ERCP —  
ALP / GGT /  
ALT  
410 / 520 /  
210  
\>1.5× ULN (TG18) —  
A  
Cholestatic Duct stone — —  
CBD 10 mm >6 mm dilated  
(ASGE) — A  
Obstruction Stone ERCP —  
Lactate 2.6 → 1.7 ≥2 (Sepsis-3) — A Hypoperfusion,  
responding  
Sepsis Repeat —  
13\. Surgery decision  
Can the patient proceed? The original elective operation does not simply continue today.  
Should the patient be optimised  
first?  
Resuscitation and infection control first; biliary drainage is considered (TG18).  
Should surgery be delayed? The elective listing is changed by the senior surgeon; definitive cholecystectomy is planned later  
according to the clinical course.  
Emergency where treatment  
continues despite abnormalities?  
The acute infection is treated as an emergency; the type and timing of intervention depend on the  
clinical situation and the responsible clinicians.  
Who makes the final decision? Senior surgeon with gastroenterology and anaesthetist.  
14\. Anaesthesia decision  
Circulation Sepsis; fluids; possible vasopressor need if unresponsive.  
Sedation for ERCP Anaesthetic/sedation assessment.  
Coagulation INR 1.3 — acceptable; monitor.  
15\. Escalation  
Nurse Obs every 30 min (NG253 high risk).  
Anaesthetist If ERCP under GA or deterioration.  
Senior surgeon Immediately.  
Emergency / critical-care  
team  
Critical care outreach if BP does not respond to fluids.  
16\. Correct path  
Notice fever in planned  
patient NEWS2 7 + infection Call senior; sepsis bundle LFTs + ultrasound  
ERCP pathway TG18 Grade II  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Gives paracetamol and  
continues list  
Anaesthetist finds T 39 at  
Sign In  
Septic patient  
anaesthetised  
Case stopped Escalate  
Antibiotics delayed for  
results  
NEWS2 rises to 9 Shock BP 86/50 Act within 1 h  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 181*

18\. Missed-abnormality path  
Fever unaddressed Rigors worsen BP falls Septic shock  
Debrief  
19\. Recovery path  
Recognises sepsis late Calls senior Bundle Stabilises  
ERCP  
20\. End-of-case debrief  
What did you miss? (common misses) Planned patients can arrive unwell.  
What did you recognise correctly?  
(expected recognitions)  
Jaundice + fever pattern.  
What was the highest-priority  
problem?  
Sepsis.  
Why was it the priority? Time-critical.  
Which findings were related? All three.  
Which findings were independent? —  
What should have triggered  
escalation?  
Fever in an elective patient; NEWS2 7.  
What would have happened if it had  
been ignored?  
Septic shock.  
What should you remember next  
time?  
Check temperature and jaundice in every gallstone patient.  
Where this comes from: Tokyo Guidelines 2018 (cholangitis criteria and severity); ASGE 2019; NICE NG253 (2025); Sepsis-3;  
Surviving Sepsis Campaign 2021.  
Clinician validation: Gastroenterology/ERCP availability and timing CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL /  
CLINICIAN CONFIRMATION .  

## STACKED CASE S2-A — Appendicitis + dehydration/tachycardia + acute

kidney injury + low potassium  
Procedure: Emergency laparoscopic appendectomy (S2). Clinical plausibility: Two days of vomiting and  
poor intake cause dehydration; dehydration lowers kidney blood flow (pre-renal AKI) and vomiting loses  
potassium (WSES 2025; KDIGO).  
1\. Case introduction  
Patient Mrs Sofia Rossi, 45. Synthetic patient.  
Why in hospital 48 h of abdominal pain moving to the right lower quadrant, vomiting 6–8 times/day.  
Background Healthy; takes ibuprofen for the pain at home.  
Allergies None.  
Pregnancy Possible? Not using contraception — the trainee asks.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 182*

2\. Initial patient presentation  
Lying still, pale, dry lips, says she feels faint on standing. Talks in full sentences. RLQ guarding on examination.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
BP 102/64  
(usual  
\~125/80)  
NEWS2 101–110 = 1 Lower than usual Dehydration Postural BP, CRT, urine  
HR 118 NEWS2 111–130 = 2 Tachycardia Dehydration, pain, fever Recheck after  
fluids/analgesia  
RR 20 0 Upper normal Pain —  
Temp 38.1 NEWS2 1 Fever Appendicitis —  
SpO2 97% 0 Normal — —  
CRT 3 s ≤2 s Prolonged Dehydration Fluids  
Pain 7/10 Severe Needs analgesia Appendicitis Request analgesia  
NEWS2 4 1–4 low (NG253) Infection likely → low-risk tier  
but clinical concern  
— Hourly reassessment given  
trend  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- Triage note  
- ED observations  
- Ibuprofen 400 mg three times daily for 2 days  
- Little urine today  
- LMP 2 weeks ago; pregnancy test agreed  
- Last food 36 h ago; sips of water 3 h ago  
5\. First abnormality — tachycardia 118 with relative hypotension and slow  
capillary refill  
What the trainee sees Monitor and bedside signs.  
What it means Hypovolaemia (dehydration).  
Why it matters Reduced circulating volume — anaesthesia would drop BP further.  
What could cause it Vomiting, poor intake, fever; less likely bleeding.  
What to verify Postural BP, CRT, urine output, pregnancy test.  
If confirmed Request IV access, bloods and an IV fluid bolus (prescribed by clinician; e.g. 500 mL crystalloid, NICE CG174) and  
reassess.  
Who to inform Senior.  
6\. Second abnormality — creatinine 158 µmol/L (GP baseline 82), urea 13.2  
What the trainee sees U&E.  
What it means AKI stage 1 (1.93 × baseline, KDIGO) — pre-renal pattern.  
Why it matters Drug doses change; NSAIDs harmful; potassium handling affected.  
What could cause it Dehydration + ibuprofen.  
What to verify Baseline; urine output.  
If confirmed Flag: stop NSAIDs; fluids; urine output monitoring; repeat U&E.  
Who to inform Senior, anaesthetist.  
Relationship to the earlier  
problem(s)  
Caused by A (dehydration) and worsened by ibuprofen.  
7\. Third abnormality — potassium 3.0 mmol/L  
What the trainee sees U&E.  
What it means Hypokalaemia (below 3.5; 3.0–3.4 commonly called mild).  
Why it matters Arrhythmia risk under anaesthesia.  
What could cause it Vomiting losses.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 183*

What to verify ECG; magnesium.  
If confirmed Replacement prescribed by clinician (IV, as vomiting); recheck.  
Who to inform Anaesthetist.  
Relationship to the earlier  
problem(s)  
Same cause as A (vomiting); AKI (B) affects replacement rate decisions.  
8\. Prioritisation — what is the most important problem right now?  
Priority Problem Why it is ranked here  
1 — Circulation Hypovolaemia Correct before anaesthesia; anaesthesia on a dry patient causes hypotension.  
2 — Serious AKI + hypokalaemia Affect drugs and anaesthetic safety; correctable within hours.  
3 — Definitive Appendicitis needing surgery Surgery is generally planned promptly (WSES 2025: within 24 h of admission  
when indicated); resuscitation proceeds in parallel and the team does not wait  
for perfect numbers.  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? HR 118, CRT 3 s  
What does it mean? Dehydrated  
What do I check? Postural BP, urine  
What do I do? (within my  
role)  
Ask for IV access, bloods, fluid bolus to be prescribed  
Who do I tell? Senior  
What happens next? HR 104 after first bolus  
What changes if  
confirmed?  
Continue fluids  
Decision 2  
What do I notice? Pregnancy possible  
What does it mean? Pregnancy status should be established (differential diagnosis)  
What do I check? Urine hCG, offered with consent  
What do I do? (within my  
role)  
Offer test  
Who do I tell? —  
What happens next? Negative  
What changes if  
confirmed?  
Appendicitis pathway continues  
Decision 3  
What do I notice? Creatinine 158  
What does it mean? AKI stage 1  
What do I check? Baseline  
What do I do? (within my  
role)  
Flag NSAIDs stopped; urine monitoring  
Who do I tell? Senior/anaesthetist  
What happens next? Plan  
What changes if  
confirmed?  
Drug choices  
Decision 4  
What do I notice? K 3.0  
What does it mean? Hypokalaemia  
What do I check? ECG  
What do I do? (within my  
role)  
Flag replacement  
Who do I tell? Anaesthetist  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 184*

What happens next? Recheck 3.5  
What changes if  
confirmed?  
Proceed when safe  
Decision 5  
What do I notice? Score + US  
What does it mean? AAS high; appendicitis on US  
What do I check? —  
What do I do? (within my  
role)  
Antibiotics flagged per WSES/policy  
Who do I tell? Senior  
What happens next? Theatre same day  
What changes if  
confirmed?  
—  
10\. Interaction between abnormalities  
A + B Dehydration + AKI: the kidneys are under-perfused; NSAIDs add injury.  
B + C AKI + hypokalaemia: both from vomiting; AKI changes how fast potassium can safely be replaced.  
A + B + C — overall picture Together: an emergency appendicitis patient who needs resuscitation first, then surgery the same day.  
11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
FBC, CRP Appendicitis, score Normal Raised Score  
U&E Vomiting Normal AKI, K Resuscitation  
Urine hCG Possible pregnancy Negative Positive → ectopic pathway Diagnosis  
Lactate Hypoperfusion? <2 ≥2 Sepsis tier  
ECG K 3.0 Normal U waves, QT Anaesthesia  
Ultrasound Diagnosis — Appendicitis Decision  
12\. Result interpretation  
Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
WBC/neutro  
phils  
15.1 / 86% — A Raised Infection — Supports surgery  
CRP (>24 h) 96 AAS bands — A Moderate — — —  
Creatinine 158  
(baseline  
82\)  
KDIGO — A AKI stage 1 Pre-renal Fluids, no NSAIDs Resuscitate first  
Potassium 3.0 → 3.5 <3.5 low — A Mild → corrected Vomiting Recheck Proceed  
Lactate 1.8 <2 — A Normal — — —  
Ultrasound Non-compre  
ssible 9 mm  
appendix  
\>6 mm (WSES) — A Appendicitis — Surgery Same day  
13\. Surgery decision  
Can the patient proceed? Yes, after resuscitation.  
Should the patient be optimised  
first?  
Short optimisation (hours): fluids, potassium.  
Should surgery be delayed? Only briefly; timing is set by the surgeon and anaesthetist (WSES 2025 recommends within 24 h of  
admission when surgery is indicated).  
Emergency where treatment  
continues despite abnormalities?  
Yes — emergency surgery continues despite AKI and low K once corrected/stable.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 185*

Who makes the final decision? Senior surgeon with anaesthetist.  
14\. Anaesthesia decision  
Circulation Correct hypovolaemia before induction.  
Aspiration Vomiting → RSI.  
Electrolytes K ≥3.5 preferred; anaesthetist decides.  
Renal Avoid nephrotoxins; drug dosing.  
15\. Escalation  
Nurse Fluid balance chart; hourly urine.  
Anaesthetist Before theatre — AKI, K, aspiration.  
Senior surgeon Diagnosis and timing.  
Emergency / critical-care  
team  
If BP does not respond to fluids or lactate rises.  
16\. Correct path  
Recognise hypovolaemia IV access, bloods, fluids via  
senior hCG with consent AKI + K flagged  
Theatre same day Antibiotics per policy  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Continues ibuprofen for  
pain  
Creatinine rises to 210 Worse AKI Stage 2 AKI Stop NSAIDs; renal review  
Sends to theatre before  
fluids  
BP 78/40 at induction Hypotension Anaesthetist stops Resuscitate first  
Skips pregnancy test Debrief Missed ectopic risk Safety failure Ask and test with consent  
18\. Missed-abnormality path  
Tachycardia ignored Urine stops Creatinine rises Hypotension  
Delayed surgery  
19\. Recovery path  
Notices trend Fluids via senior Urine returns Proceeds safely  
20\. End-of-case debrief  
What did you miss? (common misses) NSAID history; pregnancy.  
What did you recognise correctly?  
(expected recognitions)  
Dehydration signs.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 186*

What was the highest-priority  
problem?  
Hypovolaemia.  
Why was it the priority? Anaesthesia on an empty circulation is dangerous.  
Which findings were related? All three (vomiting).  
Which findings were independent? Appendicitis is the cause but separate problem to fix.  
What should have triggered  
escalation?  
HR >110 with CRT >2 s.  
What would have happened if it had  
been ignored?  
Collapse at induction; AKI progression.  
What should you remember next  
time?  
Resuscitate, then operate — don't wait for perfection.  
Where this comes from: WSES Jerusalem guidelines 2025; KDIGO AKI 2012; NICE CG174; NICE NG253; NICE NG45.  
Clinician validation: Fluid and potassium protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

## STACKED CASE S2-B — Perforated appendicitis + high-risk sepsis +

penicillin anaphylaxis  
Procedure: Emergency appendectomy (S2). Clinical plausibility: Delayed presentation leads to perforation  
and sepsis; penicillin allergy is a common pre-existing history; antibiotics are a leading cause of peri-operative  
anaphylaxis (NAP6).  
1\. Case introduction  
Patient Mr Ravi Patel, 34. Synthetic patient.  
Why in hospital 3 days of abdominal pain, now generalised.  
Allergy Penicillin — throat swelling and collapse in 2019 (anaphylaxis).  
Background Healthy.  
2\. Initial patient presentation  
Hot, sweaty, breathing fast, lying very still; abdomen rigid; alert but frightened.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
BP 92/58 NEWS2 91–100 = 2 Low Sepsis Lactate  
HR 128 2 Tachycardia Sepsis —  
RR 26 3 (single red) High Sepsis/acidosis Gas  
Temp 39.2 2 High fever Infection Cultures  
SpO2 94% air 1 Slightly low Splinting, sepsis Assess; oxygen to target  
NEWS2 10 ≥7 high risk (NG253) High risk — Senior now  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- Allergy band (red)  
- Triage note  
- Exact reaction to penicillin  
- Pain duration 3 days  
- Last meal 10 h ago  
5\. First abnormality — sepsis physiology (NEWS2 10, SBP 92)  
What the trainee sees Monitor/NEWS2 chart.  
What it means High risk of severe illness from sepsis.  
Why it matters Time-critical.  
What could cause it Perforated appendix.  
What to verify ABCDE; lactate.  
If confirmed Senior now; antibiotics and fluid bolus within 1 h (NG253) — prescribed by clinician; cultures first if no delay.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 187*

Who to inform Senior, anaesthetist, critical care outreach.  
6\. Second abnormality — penicillin anaphylaxis history  
What the trainee sees Allergy band and history.  
What it means Must avoid penicillins; cross-reactivity decisions for other β-lactams.  
Why it matters Wrong antibiotic could cause anaphylaxis in an already shocked patient.  
What could cause it —  
What to verify Exact reaction details.  
If confirmed Antibiotic chosen by the prescriber per institutional allergy pathway (non-penicillin regimen).  
Who to inform Prescriber/pharmacist.  
Relationship to the earlier  
problem(s)  
Makes the time-critical antibiotic decision harder — must not delay it.  
7\. Third abnormality — lactate 3.8; creatinine 142; CT perforation with free fluid  
What the trainee sees Results.  
What it means Hypoperfusion + early AKI; complicated appendicitis.  
Why it matters Needs urgent source control.  
What could cause it Perforation.  
What to verify Repeat lactate after fluids.  
If confirmed Urgent theatre after resuscitation starts.  
Who to inform Senior, anaesthetist.  
Relationship to the earlier  
problem(s)  
Source of A.  
8\. Prioritisation — what is the most important problem right now?  
Priority Problem Why it is ranked here  
1 — Circulation Sepsis with hypoperfusion Mortality rises with delay.  
2 — Safety Allergy-safe antibiotic Prevent anaphylaxis while giving antibiotics within 1 h.  
3 — Source  
control  
Urgent surgery Definitive treatment.  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? NEWS2 10  
What does it mean? High-risk sepsis  
What do I check? ABCDE  
What do I do? (within my  
role)  
Call senior; IV access; cultures; lactate  
Who do I tell? Senior  
What happens next? Bundle started  
What changes if  
confirmed?  
—  
Decision 2  
What do I notice? Allergy band  
What does it mean? Anaphylaxis history  
What do I check? Reaction details  
What do I do? (within my  
role)  
Tell prescriber  
Who do I tell? Prescriber  
What happens next? Non-penicillin regimen given  
What changes if  
confirmed?  
—  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 188*

Decision 3  
What do I notice? Lactate 3.8  
What does it mean? Hypoperfusion  
What do I check? —  
What do I do? (within my  
role)  
Fluid bolus via clinician; reassess  
Who do I tell? Anaesthetist  
What happens next? BP 106/64; lactate 2.4  
What changes if  
confirmed?  
Proceed to theatre  
10\. Interaction between abnormalities  
A + B Sepsis + allergy: time pressure increases the risk of a wrong drug.  
B + C Allergy + perforation: complicated appendicitis needs effective antibiotic cover pre- and post-op (2–3 days,  
WSES 2025).  
A + B + C — overall picture Together: critically ill patient needing fast, safe antibiotics and urgent surgery.  
11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
Blood cultures Sepsis — Bacteraemia Targeting  
Lactate Perfusion <2 ≥2/≥4 Urgency  
CT Complications — Perforation/abscess Surgery  
U&E AKI — AKI Doses  
12\. Result interpretation  
Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
Lactate 3.8 → 2.4 ≥2 (Sepsis-3) — A Hypoperfusion,  
responding  
Sepsis Repeat Proceed  
WBC/CRP 19.8 / 210 — A Severe inflammation Perforation — —  
Creatinine 142  
(baseline  
unknown)  
Treat as possible AKI  
— B  
Possible AKI Sepsis Monitor —  
CT Perforation,  
free fluid  
— A Complicated — Urgent theatre Yes  
13\. Surgery decision  
Can the patient proceed? Yes — urgently after resuscitation starts.  
Should the patient be optimised  
first?  
Only resuscitation.  
Should surgery be delayed? No.  
Emergency where treatment  
continues despite abnormalities?  
Yes.  
Who makes the final decision? Senior surgeon and anaesthetist.  
14\. Anaesthesia decision  
Circulation Septic shock risk; vasopressors may be needed.  
Aspiration RSI.  
Allergy Anaesthetic drug allergy check.  
Renal Doses.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 189*

15\. Escalation  
Nurse Obs every 30 min.  
Anaesthetist Immediately.  
Senior surgeon Immediately.  
Emergency / critical-care  
team  
Critical care if unresponsive to fluids.  
16\. Correct path  
Recognise high-risk sepsis Senior now Allergy-safe antibiotics ≤1  
h  
Fluids, lactate  
Urgent theatre  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Requests standard  
co-amoxiclav  
Nurse challenges allergy Anaphylaxis risk If given → anaphylaxis  
branch  
Stop drug; allergy pathway  
Waits for CT before  
antibiotics  
NEWS2 rises Delay Shock Give within 1 h  
18\. Missed-abnormality path  
Trend unnoticed BP falls Confusion Septic shock  
19\. Recovery path  
Recognises late Calls senior Bundle Stabilises  
20\. End-of-case debrief  
What did you miss? (common misses) Allergy detail; antibiotic timing.  
What did you recognise correctly?  
(expected recognitions)  
Sepsis.  
What was the highest-priority  
problem?  
Sepsis.  
Why was it the priority? Time-critical.  
Which findings were related? All.  
Which findings were independent? Allergy is independent history.  
What should have triggered  
escalation?  
NEWS2 ≥7.  
What would have happened if it had  
been ignored?  
Death/shock.  
What should you remember next  
time?  
Check allergy before every drug, especially in a rush.  
Where this comes from: NICE NG253 (2025); WSES 2025; Surviving Sepsis Campaign 2021; RCoA NAP6 (2018); NICE CG183.  
Clinician validation: Antibiotic regimen for penicillin anaphylaxis CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 190*


## STACKED CASE S2-C — Opioid sedation + vomiting + unknown/recent

food → airway and aspiration risk with falling SpO2  
Procedure: Emergency appendectomy (S2). Clinical plausibility: Opioids given for severe pain cause  
sedation, slow breathing and nausea; appendicitis causes vomiting and slows gastric emptying; a drowsy  
vomiting patient can aspirate (ASA fasting guidance; BTS 2017).  
1\. Case introduction  
Patient Mr Leo Chen, 22. Synthetic patient.  
Why in hospital Suspected appendicitis; pain 9/10.  
Events IV morphine titrated by the ED doctor 40 min ago.  
Food Ate a kebab 3 hours ago.  
2\. Initial patient presentation  
Drowsy, rousable to voice, retching, slow shallow breathing.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
RR 9 NEWS2 9–11 = 1 Slow Opioid Sedation level  
SpO2 90% air ≤91 = 3 Low Hypoventilation/aspirati  
on  
Probe, chest  
Alertness V (voice) 3 Reduced Opioid Glucose  
HR 96 1 Mild Pain —  
BP 122/70 0 Normal — —  
Temp 37.8 0 — — —  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- Drug chart: morphine given  
- Triage  
- Kebab 3 h ago  
- Vomited twice  
5\. First — reduced consciousness and RR 9  
What the trainee sees Drowsy, slow breathing.  
What it means Opioid effect.  
Why it matters Airway protection lost.  
What could cause it Opioid; less likely hypoglycaemia.  
What to verify Glucose; pupils; drug chart.  
If confirmed Call for help; stimulate; airway position; clinician considers naloxone.  
Who to inform Anaesthetist/senior now.  
6\. Second — vomiting while drowsy, recent meal  
What the trainee sees Retching.  
What it means Aspiration risk.  
Why it matters Stomach contents into lungs.  
What could cause it Appendicitis, opioid nausea, full stomach.  
What to verify Airway clear?  
If confirmed Recovery position/side, suction.  
Who to inform Anaesthetist.  
Relationship to the earlier  
problem(s)  
A + B: reduced protective reflexes + vomiting = high aspiration risk.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 191*

7\. Third — SpO2 90%  
What the trainee sees Monitor.  
What it means Hypoxaemia — hypoventilation or aspiration.  
Why it matters Brain/heart risk.  
What could cause it Opioid, aspiration.  
What to verify Probe/waveform; look at patient; chest.  
If confirmed Oxygen to target per protocol; reassess; chest exam/X-ray if aspiration suspected.  
Who to inform Anaesthetist.  
Relationship to the earlier  
problem(s)  
Result of A and possibly B.  
8\. Prioritisation — what is the most important problem right now?  
Priority Problem Why it is ranked here  
1 — Airway Vomiting with reduced consciousness Airway first in ABCDE.  
2 — Breathing RR 9, SpO2 90% Oxygenation and ventilation.  
3 — Cause Opioid sedation Reverse/adjust (clinician).  
4 — Planning Full stomach for anaesthesia RSI plan.  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? Retching, drowsy  
What does it mean? Airway at risk  
What do I check? Airway  
What do I do? (within my  
role)  
Call for help; side position; suction  
Who do I tell? Nurse/anaesthetist  
What happens next? Airway clear  
What changes if  
confirmed?  
—  
Decision 2  
What do I notice? SpO2 90, RR 9  
What does it mean? Hypoventilation  
What do I check? Probe; chest  
What do I do? (within my  
role)  
Oxygen to target; stimulate  
Who do I tell? Anaesthetist  
What happens next? SpO2 95  
What changes if  
confirmed?  
Clinician may give naloxone  
Decision 3  
What do I notice? Morphine on chart  
What does it mean? Opioid effect  
What do I check? Dose/time  
What do I do? (within my  
role)  
Report  
Who do I tell? Clinician  
What happens next? RR 14, alert  
What changes if  
confirmed?  
Revise analgesia plan  
Decision 4  
What do I notice? Kebab 3 h ago  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 192*

What does it mean? Full stomach  
What do I check? —  
What do I do? (within my  
role)  
Tell anaesthetist  
Who do I tell? Anaesthetist  
What happens next? RSI plan  
What changes if  
confirmed?  
Surgery proceeds  
10\. Interaction between abnormalities  
A + B Sedation + vomiting = aspiration.  
B + C Aspiration + hypoventilation = hypoxaemia.  
A + B + C — overall picture Together: an airway emergency caused by treatment of pain in an emergency patient with a full stomach.  
11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
Capillary glucose Drowsy 4–10 Low Treat  
Chest exam ±  
X-ray  
Aspiration? Clear Consolidation Antibiotics/anaesthesia  
Blood gas Ventilation Normal CO2 High CO2 Severity  
12\. Result interpretation  
Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
Glucose 5.6 — A Normal — — —  
CO2  
(venous)  
7.4 kPa \~5.5–6.8 venous — B Raised Hypoventilation Improves —  
Chest Clear — No aspiration — — Proceed  
13\. Surgery decision  
Can the patient proceed? Yes, when airway/breathing stable.  
Should the patient be optimised  
first?  
Short stabilisation.  
Should surgery be delayed? Not because of food alone.  
Emergency where treatment  
continues despite abnormalities?  
Yes.  
Who makes the final decision? Senior surgeon + anaesthetist.  
14\. Anaesthesia decision  
Airway Full stomach → RSI.  
Breathing Opioid sensitivity.  
Analgesia Revised plan.  
15\. Escalation  
Nurse Continuous SpO2; sedation score.  
Anaesthetist Immediately.  
Senior surgeon Informed.  
Emergency / critical-care  
team  
If airway cannot be maintained.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 193*

16\. Correct path  
Notice drowsy + retching Call for help; position;  
suction Oxygen to target Clinician reverses opioid  
RSI plan  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Gives more morphine  
for pain  
RR 6 Respiratory arrest Emergency Stop opioid; call  
High-flow O2 without  
assessing airway  
Vomits, aspirates Pneumonitis SpO2 85 ABCDE order  
18\. Missed-abnormality path  
Drowsiness unnoticed Vomits Aspirates Hypoxia  
19\. Recovery path  
Recognise Help + airway Stabilise Plan  
20\. End-of-case debrief  
What did you miss? (common misses) Sedation level after opioids.  
What did you recognise correctly?  
(expected recognitions)  
Hypoxia.  
What was the highest-priority  
problem?  
Airway.  
Why was it the priority? A before B.  
Which findings were related? All.  
Which findings were independent? —  
What should have triggered  
escalation?  
Drowsy + vomiting.  
What would have happened if it had  
been ignored?  
Aspiration pneumonitis.  
What should you remember next  
time?  
Watch breathing after every opioid dose.  
Where this comes from: Resuscitation Council UK ABCDE; BTS 2017; ASA fasting guidance; RCP NEWS2.  
Clinician validation: Opioid reversal protocol CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION .  

## STACKED CASE S2-D — Appendicitis in pregnancy (18 weeks) +

physiology that masks severity + imaging limits  
Procedure: Emergency laparoscopic appendectomy in pregnancy (S2). Clinical plausibility: Appendicitis is  
the commonest non-obstetric surgical emergency in pregnancy; normal pregnancy raises heart rate and white  
cell count, making interpretation harder; ionising imaging is avoided where possible (SAGES 2017; WSES  
2025).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 194*

1\. Case introduction  
Patient Mrs Maria Santos, 29, 18 weeks pregnant (second pregnancy). Synthetic patient.  
Presentation 20 h right-sided abdominal pain, nausea.  
Allergies None.  
2\. Initial patient presentation  
Uncomfortable, alert, right-sided tenderness slightly higher than usual for appendicitis.  
3\. Initial observations \[case value — simulator-generated\]  
Measurement Value Reference / scoring  
band  
Interpretation and concern  
level  
Possible causes What to check next  
HR 108 Pregnancy raises HR  
\~10–20/min  
Higher than expected Pain, infection,  
pregnancy  
Recheck  
BP 112/68 Often lower in  
mid-pregnancy  
Normal for pregnancy — —  
Temp 37.9 — Low-grade fever Inflammation —  
RR/SpO2 18 / 98% — Normal — —  
Score Maternity ea  
rly-warning  
score (not  
NEWS2)  
NEWS2 not validated in  
pregnancy  
— — Obstetric team  
4\. History  
Immediately available (notes, wristband, nurse handover) Only revealed if the trainee asks  
- Antenatal record • Fetal movements  
- Vaginal bleeding/discharge  
- Urinary symptoms  
5\. First — tachycardia 108 and fever  
What the trainee sees Monitor.  
What it means Higher than pregnancy alone explains.  
Why it matters Possible infection; masked severity.  
What could cause it Appendicitis, pyelonephritis, chorioamnionitis.  
What to verify Repeat; exam; urine.  
If confirmed Senior + obstetric team.  
Who to inform Senior, obstetrics.  
6\. Second — WBC 15.8, CRP 64  
What the trainee sees Results.  
What it means WBC partly raised by pregnancy; CRP raised.  
Why it matters Supports inflammation.  
What could cause it Appendicitis.  
What to verify Trend.  
If confirmed Imaging.  
Who to inform Senior.  
Relationship to the earlier  
problem(s)  
A + B: suggestive but less specific in pregnancy.  
7\. Third — ultrasound non-diagnostic  
What the trainee sees Report: appendix not visualised.  
What it means Not negative.  
Why it matters Delay increases perforation and fetal loss.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 195*

What could cause it Displaced appendix.  
What to verify —  
If confirmed MRI (no ionising radiation) → appendicitis.  
Who to inform Radiology, senior.  
Relationship to the earlier  
problem(s)  
Imaging limits mean the decision relies more on clinical picture + MRI.  
8\. Prioritisation — what is the most important problem right now?  
Priority Problem Why it is ranked here  
1 Maternal stability (ABC) Mother's health protects the fetus.  
2 Timely diagnosis and surgery Perforation increases maternal and fetal risk.  
3 Obstetric/fetal assessment Team input.  
4 Anaesthetic adjustments Aspiration risk, positioning.  
9\. Trainee decision process (chronological)  
Decision 1  
What do I notice? HR 108 + fever  
What does it mean? Possible appendicitis  
What do I check? Exam, urine, bloods  
What do I do? (within my  
role)  
Call senior + obstetrics  
Who do I tell? Both  
What happens next? Joint review  
What changes if  
confirmed?  
—  
Decision 2  
What do I notice? US non-diagnostic  
What does it mean? Unknown  
What do I check? MRI  
What do I do? (within my  
role)  
Request  
Who do I tell? Radiology  
What happens next? Appendicitis  
What changes if  
confirmed?  
Surgery  
Decision 3  
What do I notice? Plan  
What does it mean? Laparoscopic appendectomy acceptable in pregnancy (SAGES)  
What do I check? —  
What do I do? (within my  
role)  
Consent incl. fetal risks  
Who do I tell? Senior, obstetrics, anaesthetist  
What happens next? Theatre  
What changes if  
confirmed?  
—  
10\. Interaction between abnormalities  
A + B Pregnancy + tachycardia: harder to judge severity.  
B + C Raised WBC + pregnancy: less specific.  
A + B + C — overall picture Together: higher stakes, less certainty → early senior and obstetric involvement.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 196*

11\. Investigation phase  
Investigation Why Expected if normal Abnormal result would  
mean  
What it changes  
Urinalysis/culture Pyelonephritis differential — Infection Diagnosis  
Ultrasound First line — Appendicitis —  
MRI US non-diagnostic — Appendicitis Decision  
Fetal assessment Wellbeing — — Obstetric plan  
12\. Result interpretation  
Test Result  
\[case value  
— simulato  
r-generate  
d\]  
Reference /  
context (source)  
Interpretation &  
severity  
Likely  
explanation  
Next action /  
escalation  
Effect on  
surgery  
WBC 15.8 Pregnancy can raise  
WBC — B  
Mildly raised — — —  
CRP 64 — A Raised Inflammation — —  
MRI Inflamed  
appendix  
— A Appendicitis — Surgery Yes  
13\. Surgery decision  
Can the patient proceed? Yes.  
Should the patient be optimised  
first?  
No delay for optimisation.  
Should surgery be delayed? No — delay is harmful.  
Emergency where treatment  
continues despite abnormalities?  
Yes.  
Who makes the final decision? Senior surgeon with obstetric and anaesthetic teams.  
14\. Anaesthesia decision  
Aspiration Increased in pregnancy → RSI.  
Positioning Left lateral tilt from about 20 weeks.  
Drugs Pregnancy-safe choices.  
Monitoring Fetal monitoring per obstetrics.  
15\. Escalation  
Nurse Maternity early-warning observations.  
Anaesthetist Early.  
Senior surgeon Early.  
Emergency / critical-care  
team  
If instability.  
16\. Correct path  
Recognise pregnancy  
context Obstetrics + senior early US → MRI Laparoscopicappendectomy  
17\. Incorrect paths (realistic mistakes)  
What the trainee did  
wrong  
What the NPC / monitor  
shows  
Risk created How the scenario  
escalates  
How the trainee can  
recover  
Orders CT first Radiologist queries Radiation exposure Delay Use MRI  
Attributes HR to  
pregnancy and waits  
Pain worsens Perforation Fever 39 Reassess  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 197*

18\. Missed-abnormality path  
Tachycardia dismissed Delay Perforation  
19\. Recovery path  
Reassess Escalate MRI Surgery  
20\. End-of-case debrief  
What did you miss? (common misses) Interpreting vitals in pregnancy.  
What did you recognise correctly?  
(expected recognitions)  
Need for obstetrics.  
What was the highest-priority  
problem?  
Timely diagnosis.  
Why was it the priority? Delay harms both.  
Which findings were related? HR/WBC with pregnancy.  
Which findings were independent? —  
What should have triggered  
escalation?  
HR >100 + fever.  
What would have happened if it had  
been ignored?  
Perforation, fetal loss.  
What should you remember next  
time?  
Pregnancy changes ranges, imaging, drugs and team.  
Where this comes from: SAGES guidelines for laparoscopy in pregnancy (2017); WSES 2025; NICE NG45; RCOG.  
Clinician validation: Obstetric and anaesthetic protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN  
CONFIRMATION .  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 198*


# PART D — Stacked-case master table (overview)

Case Procedure Problem 1 Problem 2 Problem 3 Highest priority Investigation Escalation  
(senior)  
Surgical  
decision  
Anaesthetic  
decision  
S1-A Elective  
laparoscopic  
cholecystectomy  
hypoglycaemia (glucose  
3.6 mmol/L)  
SGLT2 inhibitor taken  
today (euglycaemic DKA  
risk)  
no recent HbA1c →  
result 76 mmol/mol  
Hypoglycaemia 3.6  
mmol/L  
Repeat capillary  
glucose, Capillary  
ketones, HbA1c, U&E  
Same call —  
propose deferral;  
agree  
documentation and  
patien  
Unlikely today:  
SGLT2 inhibitor  
taken on the  
day against UK  
2025  
consensus  
advice, a  
hypoglycaemic  
episode with  
oral glucose  
altering fasting,  
and HbA1c 76;  
the  
anaesthetist  
and senior  
surgeon  
decide.  
No concerns;  
BMI 31 —  
STOP-BANG  
check.  
S1-B Elective  
laparoscopic  
cholecystectomy  
irregular pulse haemoglobin 104 g/L,  
MCV 74 fL, ferritin 9  
µg/L  
apixaban taken 07:00  
today  
Apixaban active 12-lead ECG, FBC,  
Ferritin/iron studies,  
U&E / creatinine  
clearance  
Same — propose  
deferral; arrange  
anaemia  
investigation.  
Normally not  
today (senior/a  
naesthetist  
decide).  
AF rate  
controlled;  
anaemia lowers  
oxygen-carryin  
g reserve.  
S1-C Elective  
laparoscopic  
cholecystectomy  
slow heart (52) and BP  
lower than usual  
potassium 6.6 mmol/L  
(not haemolysed) with  
peaked T waves  
creatinine 178 µmol/L  
(baseline 112)  
K 6.6 with ECG  
changes  
12-lead ECG, U&E,  
Venous blood gas,  
Capillary glucose  
Immediately. Not while  
hyperkalaemia  
with ECG  
changes is  
untreated.  
Hyperkalaemia  
\+ bradycardia  
\= high  
anaesthetic  
risk; suxameth  
onium contrain  
dicated.  
S1-D Original plan:  
elective  
laparoscopic  
cholecystectomy  
fever 39.2 °C with  
tachycardia  
WBC 16.4, CRP 142,  
bilirubin 64, ALP 410,  
GGT 520, ALT 210; CBD  
10 mm  
lactate 2.6, creatinine  
96 (baseline 70)  
High-risk sepsis  
(NEWS2 7, lactate  
2.6)  
Blood cultures, Lactate,  
LFTs, Ultrasound  
Immediately. The original  
elective  
operation does  
not simply  
continue today.  
Sepsis; fluids;  
possible  
vasopressor  
need if  
unresponsive.  
S2-A Emergency  
laparoscopic  
appendectomy  
tachycardia 118 with  
relative hypotension  
and slow capillary refill  
creatinine 158 µmol/L  
(GP baseline 82), urea  
13.2  
potassium 3.0 mmol/L Hypovolaemia FBC, CRP, U&E, Urine  
hCG, Lactate  
Diagnosis and  
timing.  
Yes, after  
resuscitation.  
Correct  
hypovolaemia  
before  
induction.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 199*

Case Procedure Problem 1 Problem 2 Problem 3 Highest priority Investigation Escalation  
(senior)  
Surgical  
decision  
Anaesthetic  
decision  
S2-B Emergency  
appendectomy  
sepsis physiology  
(NEWS2 10, SBP 92)  
penicillin anaphylaxis  
history  
lactate 3.8; creatinine  
142; CT perforation with  
free fluid  
Sepsis with  
hypoperfusion  
Blood cultures, Lactate,  
CT, U&E  
Immediately. Yes — urgently  
after  
resuscitation  
starts.  
Septic shock  
risk;  
vasopressors  
may be  
needed.  
S2-C Emergency  
appendectomy  
reduced consciousness  
and RR 9  
vomiting while drowsy,  
recent meal  
SpO2 90% Vomiting with  
reduced  
consciousness  
Capillary glucose, Chest  
exam ± X-ray, Blood  
gas  
Informed. Yes, when airw  
ay/breathing  
stable.  
Full stomach →  
RSI.  
S2-D Emergency  
laparoscopic  
appendectomy in  
pregnancy  
tachycardia 108 and  
fever  
WBC 15.8, CRP 64 ultrasound  
non-diagnostic  
Maternal stability  
(ABC)  
Urinalysis/culture,  
Ultrasound, MRI, Fetal  
assessment  
Early. Yes. Increased in  
pregnancy →  
RSI.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 200*


# PART E — Equipment and Asset Mapping

A medical inventory (not a software plan). Use: ✓ always, cond. when indicated. 'Where' = stage numbers in Part 11. Interactions that involve medicines follow the role  
convention: the trainee identifies the need and initiates the workflow; a qualified clinician confirms; a nurse/clinician administers.  
Equipment Use (S1/S2) What it is and why it is used Where it appears; what the trainee  
sees and does  
Information it provides Clinical significance; failure /  
missing scenario  
Source • visual/3D  
asset • validation  
Patient wristband  
(identity)  
S1 ✓ / S2 ✓ Band with name, DOB, MR number;  
prevents wrong-patient errors  
Stage 1, all. Reads band; compares  
with spoken answer and notes  
Name, DOB, MR number  
(text must be readable)  
Mismatch → stop; missing band  
→ apply after verification  
WHO SSC • 3D band  
with readable text •  
hospital format  
INSTITUTION /  
CLINICIAN  
Allergy band S1 ✓ / S2 ✓ Coloured band flagging allergy Stage 1, 7, 13. Checks before any drug Presence of an allergy flag  
(details only in notes/by  
asking)  
Missing band with reported  
allergy → reaction branch  
NPSA (UK) • 3D band •  
colour INSTITUTION /  
CLINICIAN  
Hospital trolley / bed S1 ✓ / S2 ✓ Patient positioning and transport Stage 1–12. Adjusts head-up/flat (flat  
for low BP)  
Visual/physical cue only (no  
numeric data)  
— • 3D asset  
Wheelchair S1 ✓ / S2 — Moving stable S1 patient Stage 1, 6. Optional transport choice Visual/physical cue only (no  
numeric data)  
Unstable patient must not go by  
wheelchair  
- 3D asset  
Vital-sign monitor S1 ✓ / S2 ✓ Displays SpO2, NIBP, ECG, (temp) Stage 3, 8, 12–14. Attaches leads;  
reads numbers; hears alarms  
SpO2, pulse, NIBP/MAP, ECG  
rhythm; alarms  
Alarms at set thresholds;  
artefact if probe poor  
WHO-WFSA 2018 • 3D  
monitor with live values  
- alarm limits  
INSTITUTION /  
CLINICIAN  
BP cuff (NIBP) S1 ✓ / S2 ✓ Measures blood pressure Stage 3. Wraps arm, starts cycle  
(\~30–40 s)  
Systolic/diastolic/MAP after  
\~30–40 s cycle  
Wrong size → false reading ESH 2023 • 3D cuff  
Pulse oximeter probe S1 ✓ / S2 ✓ SpO2 and pulse Stage 3, 13. Clips on finger; checks  
waveform  
SpO2, pulse, pleth waveform  
(quality cue)  
No waveform → recheck; WHO  
Sign In requires working  
oximeter  
WHO SSC; FDA 2021 •  
3D probe  
Thermometer (tympanic) S1 ✓ / S2 ✓ Temperature Stage 3, 10. Uses in ear Temperature in °C <36.0 → warm before theatre NICE CG65 • 3D device  
12-lead ECG machine +  
electrodes  
S1 cond. / S2  
cond.  
Heart electrical tracing Stage 6–7. Places 10 electrodes; reads  
printout  
Rhythm, rate, intervals, ST/T  
changes (printout +  
machine read-out that may  
be wrong)  
Wrong lead placement →  
misleading trace  
NICE NG45 • 3D  
machine + printout  
Stethoscope S1 ✓ / S2 ✓ Listen to heart/lungs/bowel Stage 4. Auscultation interaction Visual/physical cue only (no  
numeric data)  
— • 3D asset  
Blood collection set +  
tourniquet  
S1 ✓ / S2 ✓ Venepuncture Stage 6. Takes blood after ID check Visual/physical cue only (no  
numeric data)  
Drip-arm sample → artefact • 3D asset  
Specimen tubes  
(colour-coded) + labels  
S1 ✓ / S2 ✓ Different additives per test Stage 6. Labels at bedside Which tests the tube is for;  
labelled identity  
Pre-labelled tubes →  
wrong-blood event; EDTA  
contamination → false high K  
RCPath; • 3D tubes  
INSTITUTION /  
CLINICIAN  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 201*

Equipment Use (S1/S2) What it is and why it is used Where it appears; what the trainee  
sees and does  
Information it provides Clinical significance; failure /  
missing scenario  
Source • visual/3D  
asset • validation  
Blood culture bottles S1 — / S2  
cond.  
Grow bacteria in sepsis Stage 6, 8. Fill before antibiotics Visual/physical cue only (no  
numeric data)  
Delaying antibiotics for cultures  
→ harm  
NICE NG253 • 3D  
bottles  
Capillary glucose/ketone  
meter  
S1 cond. / S2  
cond.  
Finger-prick sugar/ketones Stage 3, 8. Uses on diabetic/confused  
patients  
Glucose (mmol/L), ketones  
(mmol/L)  
Hypoglycaemia missed JBDS • 3D meter  
Urine pot + dipstick;  
pregnancy test kit  
S1 cond. / S2  
✓  
Urinalysis; β-hCG Stage 6. Dips; reads at 1–5 min Dipstick colours; hCG  
positive/negative  
Missed positive hCG → safety  
failure  
NICE NG45 • 3D kit  
Blood gas analyser  
(result display)  
S1 — / S2  
cond.  
Lactate, pH Stage 7. Result screen Visual/physical cue only (no  
numeric data)  
— SSC 2021 • UI display  
IV cannula kit S1 ✓ / S2 ✓ Venous access Stage 8. Inserts (abstracted) Visual/physical cue only (no  
numeric data)  
Failed cannulation → delay • 3D cannula  
IV giving set + fluid bags S1 — / S2 ✓ Fluids Stage 8. Hangs bag; sets bolus Fluid type, volume running Fluid overload in heart failure NICE CG174 • 3D bags  
Infusion pump S1 cond. / S2  
cond.  
Controlled infusion (insulin,  
antibiotics)  
Stage 8. Programmes rate (abstracted) Visual/physical cue only (no  
numeric data)  
Pump alarm • 3D pump  
Oxygen supply, nasal  
cannula, face masks  
(simple, non-rebreather)  
S1 cond. / S2  
cond.  
Oxygen therapy Stage 8, 12. Selects device to reach  
target  
Device type and flow once  
set per protocol  
Over-oxygenating CO2 retainer BTS 2017 • 3D masks  
Suction (ward and  
theatre)  
S1 ✓ / S2 ✓ Clears airway/vomit Stage 11, 13. Checks working Visual/physical cue only (no  
numeric data)  
Not working → RSI unsafe AoA 2019 • 3D unit  
Nasogastric tube S1 — / S2  
cond.  
Decompress stomach in  
vomiting/ileus  
Stage 8. Senior-directed Visual/physical cue only (no  
numeric data)  
— • 3D asset  
Urinary catheter +  
urometer  
S1 — / S2  
cond.  
Hourly urine output in sepsis/AKI Stage 8. Senior-directed Visual/physical cue only (no  
numeric data)  
— KDIGO • 3D asset  
Medication trolley / drug  
chart  
S1 ✓ / S2 ✓ Medicines and the prescription  
record  
Stage 8. Identifies need and initiates  
request; views confirmed prescription;  
observes/assists administration  
Confirmed prescriptions  
(drug, route, time) entered  
by a clinician; allergy alert  
Request for an allergen → nurse  
challenge  
- 3D trolley + chart UI  
Sharps bin S1 ✓ / S2 ✓ Needle disposal Stage 6, 8. Disposes needles Visual/physical cue only (no  
numeric data)  
Needlestick if ignored Infection control • 3D  
bin  
PPE (gloves, apron,  
mask; theatre cap, gown)  
S1 ✓ / S2 ✓ Infection prevention Stage All. Dons appropriate PPE Visual/physical cue only (no  
numeric data)  
— WHO hand hygiene • 3D  
assets  
Observation (NEWS2)  
chart  
S1 ✓ / S2 ✓ Records obs and score Stage 3. Completes score Trend of obs and NEWS2  
score  
Missed trend RCP NEWS2 • chart UI  
Patient notes / folder S1 ✓ / S2 ✓ History, referral, pre-assessment Stage 1–12. Reads, documents Visual/physical cue only (no  
numeric data)  
Missing documents block  
transfer  
- 3D folder/UI  
Consent form S1 ✓ / S2 ✓ Records consent Stage 9. Completes with patient Procedure, risks discussed,  
signatures  
Wrong procedure → blocker GMC; local form  
INSTITUTION /  
CLINICIAN  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 202*

Equipment Use (S1/S2) What it is and why it is used Where it appears; what the trainee  
sees and does  
Information it provides Clinical significance; failure /  
missing scenario  
Source • visual/3D  
asset • validation  
Results display / lab  
report  
S1 ✓ / S2 ✓ Shows results with flags Stage 7. Opens, acknowledges Values, H/L flags, lab  
comments (e.g.  
haemolysed), critical call  
Unacknowledged critical →  
blocker  
- UI  
Ultrasound machine /  
report  
S1 cond. / S2  
✓  
Gallbladder/appendix imaging Stage 6–7. Requests; views  
images/report  
Images + radiologist  
category (positive/negative/  
equivocal)  
'Not visualised' ≠ negative WSES; TG18 • 3D  
machine + viewer  
CT scanner  
(representation) / report  
S1 cond. / S2  
cond.  
Detailed imaging Stage 6–7. Arranges monitored transfer Report text and key images Contrast with AKI/allergy • Room or report only  
MRCP / ERCP (report  
only)  
S1 cond. / S2  
—  
Bile-duct imaging / treatment Stage 7. Reads report Duct size, stones Duct stone missed ASGE 2019 • report UI  
Anti-embolism stockings  
/ IPC device  
S1 ✓ / S2 ✓ VTE prevention Stage 8, 10. Fits if indicated Visual/physical cue only (no  
numeric data)  
Fitted with arterial disease →  
harm  
NICE NG89 • 3D asset  
Clippers S1 cond. / S2  
cond.  
Hair removal if needed Stage 10. Uses clippers not razor Visual/physical cue only (no  
numeric data)  
Razor → infection risk NICE NG125 • 3D  
clipper  
Skin marker S1 cond. / S2  
cond.  
Site marking where policy requires Stage 10. Surgeon marks Visual/physical cue only (no  
numeric data)  
Usually N/A here WHO SSC • policy  
INSTITUTION /  
CLINICIAN  
Forced-air warming  
device  
S1 ✓ / S2 ✓ Prevents hypothermia Stage 10–11. Applies if <36 °C or long  
case  
Visual/physical cue only (no  
numeric data)  
— NICE CG65 • 3D device  
Transfer trolley +  
portable monitor +  
oxygen cylinder  
S1 ✓ / S2 ✓ Safe transfer Stage 12. Selects monitoring by  
stability  
Visual/physical cue only (no  
numeric data)  
Corridor deterioration WHO-WFSA • 3D assets  
- rule INSTITUTION /  
CLINICIAN  
Handover sheet (SBAR) S1 ✓ / S2 ✓ Structured handover Stage 12. Delivers SBAR Visual/physical cue only (no  
numeric data)  
Omissions WHO 2007 • UI  
Operating table +  
positioning padding  
S1 ✓ / S2 ✓ Positioning, pressure care Stage 11. Checked by team Visual/physical cue only (no  
numeric data)  
— • 3D table  
Anaesthesia machine  
(with ventilator) + check  
log  
S1 ✓ / S2 ✓ Delivers anaesthetic  
gases/ventilation  
Stage 11, 13. Confirms check done Check completed/not; gas  
and ventilation settings  
Check not done → Sign In fails AoA 2019 • 3D machine  
Capnograph S1 ✓ / S2 ✓ Exhaled CO2 — confirms tube in  
airway  
Stage 11, 13. Sees waveform  
(post-induction abstracted)  
Exhaled CO2 waveform and  
value  
— WHO-WFSA 2018 •  
monitor trace  
Airway equipment  
(laryngoscope, tubes) +  
difficult-airway trolley  
S1 ✓ / S2 ✓ Intubation; backup Stage 11, 13. Confirms availability Visual/physical cue only (no  
numeric data)  
Difficult airway not declared DAS • 3D trolley  
Surgical lights S1 ✓ / S2 ✓ Illuminate field Stage 11, 14. Environment Visual/physical cue only (no  
numeric data)  
— • 3D asset  
Laparoscopic tower  
(camera, light source,  
monitor, insufflator)  
S1 ✓ / S2 ✓ Keyhole surgery Stage 11, 14. Checks working Image, insufflation  
pressure/flow, fault  
messages  
Fault → delay SAGES • 3D tower  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 203*

Equipment Use (S1/S2) What it is and why it is used Where it appears; what the trainee  
sees and does  
Information it provides Clinical significance; failure /  
missing scenario  
Source • visual/3D  
asset • validation  
CO2 cylinder (+ spare) S1 ✓ / S2 ✓ Insufflation gas Stage 11. Reads gauge Gauge level Empty → fault scenario • 3D cylinder  
Electrosurgical  
(diathermy) unit + return  
electrode  
S1 ✓ / S2 ✓ Cut/seal tissue Stage 11. Confirms pad placement Visual/physical cue only (no  
numeric data)  
Pad missing → burn risk • 3D unit  
Instrument trays (lap  
chole / lap + open  
appendix)  
S1 ✓ / S2 ✓ Sterile instruments Stage 11, 14. Confirms sterility  
indicators  
Visual/physical cue only (no  
numeric data)  
Indicator fail → stop WHO SSC • 3D trays  
Drapes S1 ✓ / S2 ✓ Sterile field Stage 14. Environment Visual/physical cue only (no  
numeric data)  
— • 3D asset  
Specimen container +  
label  
S1 ✓ / S2 ✓ Gallbladder/appendix to histology Stage 15. Label read aloud at Sign Out Visual/physical cue only (no  
numeric data)  
Mislabelled specimen WHO SSC • 3D pot  
Resuscitation trolley +  
defibrillator + emergency  
drugs (representation)  
S1 ✓ / S2 ✓ Cardiac arrest/anaphylaxis Stage All. Checked; used in emergency  
branches  
Visual/physical cue only (no  
numeric data)  
Not checked → delay in  
emergency  
RCUK 2021 • 3D trolley  
WHO checklist board /  
form  
S1 ✓ / S2 ✓ Surgical safety checklist Stage 13–15. Leads/answers items Item status  
(pending/confirmed)  
Skipped item → stop WHO SSC 2009 • board  
UI  
Recovery bay equipment  
(monitor, oxygen,  
suction)  
S1 ✓ / S2 ✓ Post-op care Stage 15. Handover to recovery nurse Visual/physical cue only (no  
numeric data)  
— AoA 2021 • 3D bay  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 204*


# PART F — S1 Master Scenario (Elective

Laparoscopic Cholecystectomy)  
S1 master flow  
Arrival → Initial assessment → Observations → History  
Red flags (jaundice, fever, constant pain)? Acute branch: TG18 / ASGE / cholangitis → senior  
review; planned list changed  
YES  
NO  
Investigations → Results  
Abnormal/optimisable finding? Verify → assess → review → optimise / defer /  
escalate  
YES  
NO  
Optimisation → Consent → Anaesthetic assessment  
WHO Sign In → (theatre ready) → WHO Time Out  
Procedure → intra-op events → WHO Sign Out →  
Recovery/handover → Debrief  
Deterioration and emergency branches (stages 18–19) can start at any earlier point.  
1\. Patient arrival  
Medical detail Elective patient arrives at day-surgery unit at booked time  
Trainee action Greets, introduces self; checks bands  
What the trainee sees /  
information  
Bay, folder, nurse; calm patient  
Normal pathway Identity next  
Abnormal branches Unwell on arrival (fever, jaundice, acute pain) → assess for acute illness  
Escalation / NPC Nurse; senior if unwell  
Equipment and monitor  
response  
Trolley, folder; monitor off  
If missed Poor start; acute illness missed  
Source WHO SSC  
Clinician validation —  
2\. Initial assessment  
Medical detail Identity (≥2 identifiers, open question), allergy, procedure confirmation, escort/discharge plan  
Trainee action Compare answer vs band + notes; ask allergy reaction details  
What the trainee sees /  
information  
Wristband, allergy band, referral  
Normal pathway History  
Abnormal branches Mismatch → stop; wrong procedure described → surgical team  
Escalation / NPC Admissions/nurse  
Equipment and monitor  
response  
Wristband, allergy band  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 205*

If missed Wrong-patient / allergy events  
Source WHO SSC; NICE NG180  
Clinician validation Identifier set CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
3\. Observations  
Medical detail HR, BP, RR, SpO2, temp, alertness, pain, BMI, glucose if diabetic; NEWS2  
Trainee action Attach devices; count RR; repeat abnormal readings  
What the trainee sees /  
information  
Monitor values; NEWS2 chart  
Normal pathway NEWS2 0–1, BP <180/110  
Abnormal branches Fever, tachycardia, BP ≥180/110, low SpO2 → verify, assess, review  
Escalation / NPC Anaesthetist/senior per finding  
Equipment and monitor  
response  
Monitor, cuff, probe, thermometer; alarms at set limits  
If missed Unexplained abnormality surfaces at Sign In  
Source RCP NEWS2; AAGBI/BHS 2016; BTS 2017  
Clinician validation Alarm limits CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
4\. History  
Medical detail Biliary colic story; red flags; comorbidities; drugs (anticoagulants, diabetes, GLP-1, steroids); allergies; fasting;  
pregnancy if possible  
Trainee action Structured questions; hidden facts only if asked  
What the trainee sees /  
information  
Patient answers; clerking form  
Normal pathway No red flags; drugs manageable; fasted  
Abnormal branches Jaundice/fever/constant pain; drug or fasting problem; pregnancy possible  
Escalation / NPC Senior; anaesthetist  
Equipment and monitor  
response  
Clerking UI  
If missed Hidden facts cause late stops  
Source NICE CG188; NG45; CPOC; PAUSE  
Clinician validation —  
5\. Investigations  
Medical detail Only indicated tests: LFTs (all); hCG if pregnancy possible (consent); FBC/U&E/ECG/HbA1c by comorbidity; G&S  
per policy  
Trainee action Order panel; bedside labelling after ID check  
What the trainee sees /  
information  
Tubes, kits, ECG machine  
Normal pathway Samples sent  
Abnormal branches Unindicated tests logged; mislabelled → incident  
Escalation / NPC —  
Equipment and monitor  
response  
Tubes, labels, ECG  
If missed Over/under-testing in debrief  
Source NICE NG45; CG188  
Clinician validation Ordering rules CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
6\. Results  
Medical detail Compare with reference; interpret in context; acknowledge  
Trainee action Open each; repeat suspicious; sign off  
What the trainee sees /  
information  
Results panel; lab comments  
Normal pathway Within acceptable limits  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 206*

Abnormal branches See 7  
Escalation / NPC Senior/anaesthetist  
Equipment and monitor  
response  
Results UI; critical call  
If missed Unacknowledged critical result blocks progress  
Source Part 5 sources  
Clinician validation Lab ranges CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
7\. Abnormal branches  
Medical detail Cholestatic LFTs ± dilated duct (ASGE tiers); fever/WBC/CRP (TG18 cholecystitis/cholangitis); K/Na; Hb; HbA1c  
\>69; new AF; positive hCG; anticoagulant timing  
Trainee action Verify → assess severity/context → investigate → senior/anaesthetic review  
What the trainee sees /  
information  
Variant data  
Normal pathway —  
Abnormal branches Per branch; cholangitis = emergency  
Escalation / NPC Senior; gastroenterology; medical  
Equipment and monitor  
response  
Monitor/ECG as relevant  
If missed Unsafe proceed → safety failure  
Source ASGE 2019; TG18; UKKA 2023; CPOC; PAUSE  
Clinician validation —  
8\. Optimisation  
Medical detail Medication plan; VTE prophylaxis; antibiotic only if risk factors; warming; carbohydrate drink per policy  
Trainee action Identify needs; request via workflow; clinician confirms  
What the trainee sees /  
information  
Drug chart; stockings  
Normal pathway Plan agreed  
Abnormal branches Drug clash/allergy → nurse challenge  
Escalation / NPC Anaesthetist/pharmacist  
Equipment and monitor  
response  
Chart, stockings, warmer  
If missed Wrong plan → debrief  
Source CPOC; NICE NG89; NG125; ASHP 2013  
Clinician validation Antibiotic policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
9\. Consent  
Medical detail Diagnosis, procedure, benefits, material risks (with figures), alternatives, no-treatment consequences; capacity;  
teach-back  
Trainee action Dialogue choices; teach-back  
What the trainee sees /  
information  
Patient, form  
Normal pathway Valid consent signed  
Abnormal branches No capacity / language / minor / refusal  
Escalation / NPC Senior  
Equipment and monitor  
response  
Consent form  
If missed Invalid consent blocker  
Source GMC 2020; SAGES review 2018  
Clinician validation Law/form CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 207*

10\. Anaesthetic assessment  
Medical detail ASA grade, airway, STOP-BANG, fasting, aspiration risk, drug plan, VTE  
Trainee action Propose ASA; report findings  
What the trainee sees /  
information  
Anaesthetist NPC  
Normal pathway Fit; standard GA  
Abnormal branches Uncontrolled comorbidity; breach; difficult airway  
Escalation / NPC Anaesthetist  
Equipment and monitor  
response  
Airway equipment  
If missed Wrong ASA corrected  
Source ASA 2020; ASA fasting 2017/2023; UK 2025 consensus  
Clinician validation —  
11\. WHO Sign In  
Medical detail WHO 2009 items (identity/site/procedure/consent; site mark; machine and medication check; oximeter; allergy;  
airway/aspiration; blood loss)  
Trainee action Answer/lead items  
What the trainee sees /  
information  
Checklist board  
Normal pathway All confirmed  
Abnormal branches Any 'no' → stop and resolve  
Escalation / NPC Nurse + anaesthetist  
Equipment and monitor  
response  
Oximeter live  
If missed Skipped item → challenge/stop  
Source WHO SSC 2009  
Clinician validation —  
12\. Theatre preparation (runs in parallel before the patient arrives)  
Medical detail Machine check, monitoring, suction, airway kit, laparoscopic tower + CO2, diathermy, warming, resus trolley  
Trainee action Inspect/confirm; report faults  
What the trainee sees /  
information  
Theatre  
Normal pathway Ready  
Abnormal branches Fault → fix/delay  
Escalation / NPC Theatre staff  
Equipment and monitor  
response  
Tower, cylinder gauge  
If missed Time Out cannot complete  
Source AoA 2019; WHO-WFSA 2018  
Clinician validation —  
13\. WHO Time Out  
Medical detail WHO 2009 items (introductions; name/procedure/incision site; antibiotic ≤60 min or N/A; critical events; sterility  
incl. indicators; equipment; imaging)  
Trainee action Take part in order  
What the trainee sees /  
information  
Team, board  
Normal pathway READY FOR INCISION  
Abnormal branches Antibiotic/imaging/equipment gap → fix, repeat  
Escalation / NPC Surgeon leads  
Equipment and monitor  
response  
Trays, tower  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 208*

If missed Stop/debrief  
Source WHO SSC 2009; NICE NG125  
Clinician validation —  
14\. Procedure start  
Medical detail Abstracted time-lapse; operation not simulated  
Trainee action Observe  
What the trainee sees /  
information  
Time-lapse  
Normal pathway Proceeds  
Abnormal branches —  
Escalation / NPC —  
Equipment and monitor  
response  
Stable monitor  
If missed —  
Source —  
Clinician validation —  
15\. Relevant intra-operative training events (ProcSense extension)  
Medical detail e.g. unexpected adhesions → conversion discussion; bleeding needing suction; unexpected stone on  
cholangiogram (surgeon's decision)  
Trainee action Communicate; respond to requests  
What the trainee sees /  
information  
Theatre team  
Normal pathway Smooth  
Abnormal branches Team decision  
Escalation / NPC Surgeon/anaesthetist  
Equipment and monitor  
response  
Suction, open set  
If missed Freezing → debrief  
Source ProcSense educational/simulation extension — clinician validation required.  
Clinician validation Surgeon to approve events  
16\. WHO Sign Out  
Medical detail Procedure name; instrument/swab/needle counts; specimen label read aloud incl. patient name; equipment  
problems; recovery concerns  
Trainee action Participate  
What the trainee sees /  
information  
Board  
Normal pathway Complete  
Abnormal branches Count discrepancy → search/escalate  
Escalation / NPC Nurse leads  
Equipment and monitor  
response  
Specimen pot  
If missed Specimen error  
Source WHO SSC 2009  
Clinician validation —  
17\. Recovery / handover  
Medical detail SBAR to recovery nurse; analgesia, nausea, VTE, discharge plan (day case)  
Trainee action Hand over  
What the trainee sees /  
information  
Recovery bay  
Normal pathway Handover accepted  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 209*

Abnormal branches Pain, PONV, bleeding → recovery team  
Escalation / NPC Recovery nurse  
Equipment and monitor  
response  
Recovery monitor  
If missed Omissions  
Source AoA 2021; NICE NG89  
Clinician validation Discharge criteria CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
18\. Deterioration branches  
Medical detail Fever/sepsis, hypoglycaemia, hyperkalaemia/ECG change, AF, anaphylaxis, hypoxia, hypotension  
Trainee action ABCDE; call; initiate workflow  
What the trainee sees /  
information  
Monitor trends  
Normal pathway Recovers  
Abnormal branches Emergency escalation  
Escalation / NPC Senior, anaesthetist, emergency team  
Equipment and monitor  
response  
Monitor, resus trolley  
If missed Worsening per Part 12 clocks  
Source RCUK; NICE NG253; UKKA (clinical content). Worsening speed: ProcSense educational/simulation extension —  
clinician validation required.  
Clinician validation Clocks (design) — clinician realism  
19\. Emergency branches  
Medical detail Cholangitis (TG18), anaphylaxis (RCUK), hyperkalaemia with ECG change  
Trainee action Recognise; call; support team  
What the trainee sees /  
information  
Team arrives  
Normal pathway Stabilised  
Abnormal branches Emergency team  
Escalation / NPC As above  
Equipment and monitor  
response  
Resus trolley, defibrillator  
If missed Arrest branch  
Source TG18; RCUK 2021; UKKA 2023  
Clinician validation Protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
20\. Debrief  
Medical detail Timeline, missed cues, delays, unnecessary tests, correct reasoning with sources  
Trainee action Review  
What the trainee sees /  
information  
Debrief screen  
Normal pathway Learning  
Abnormal branches —  
Escalation / NPC —  
Equipment and monitor  
response  
—  
If missed —  
Source —  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 210*


# PART G — S2 Master Scenario (Emergency

Appendectomy)  
Teacher says:  
S2 does NOT follow the S1 timeline: assessment, treatment and preparation overlap, and the clock runs. Surgery for  
appendicitis is generally planned promptly; WSES 2025 recommends laparoscopic appendectomy within 24 hours of  
admission when surgery is indicated, with the actual timing set by the surgeon and anaesthetist according to  
severity, sepsis, anaesthetic assessment and resources CLINICAL REFERENCE — SOURCE BACKED .  
S2 master flow  
Arrival/triage → Initial assessment (+ early  
analgesia/IV) → Observations → History  
Differential-diagnosis check: pregnancy possible,  
test positive, with pain?  
DIAGNOSTIC DIVERSION (not an appendicitis  
complication): ectopic pregnancy must be  
considered → gynaecology  
YES  
NO  
High-risk sepsis (NG253) / shock? Sepsis branch in parallel  
YES  
NO  
Investigations → Results (score, imaging) → Abnormal  
branches → Optimisation in parallel  
Complicated disease? Urgent source control; antibiotics 2–3 days  
post-op  
YES  
NO  
Consent → Anaesthetic assessment (RSI) → Sign In →  
Theatre → Time Out  
Procedure → intra-op events → Sign Out →  
Recovery/handover → Debrief  
1\. Patient arrival  
Medical detail ED arrival in pain; triage category  
Trainee action Note triage; start pain pathway  
What the trainee sees /  
information  
Cubicle, triage note, relative  
Normal pathway Assessment  
Abnormal branches Shocked on arrival → resus area  
Escalation / NPC Triage nurse  
Equipment and monitor  
response  
Trolley, monitor  
If missed Delay  
Source Triage concept  
Clinician validation Triage system CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
2\. Initial assessment  
Medical detail Rapid identity + allergy; ABC; analgesia pathway started  
Trainee action ID and allergy; request analgesia and IV access via workflow  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 211*

What the trainee sees /  
information  
Bands  
Normal pathway Observations  
Abnormal branches Mismatch → resolve  
Escalation / NPC Nurse  
Equipment and monitor  
response  
Cannula kit  
If missed Wrong patient/drug  
Source WHO SSC; WSES 2025  
Clinician validation —  
3\. Observations  
Medical detail Full set; NEWS2; repeated with trend  
Trainee action Attach; count RR; repeat  
What the trainee sees /  
information  
Monitor; NEWS2 chart  
Normal pathway NEWS2 low  
Abnormal branches NEWS2 ≥5 / single 3 → review; ≥7 high risk  
Escalation / NPC Senior  
Equipment and monitor  
response  
Monitor  
If missed Trend missed → deterioration  
Source RCP NEWS2; NICE NG253  
Clinician validation Tool CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
4\. History  
Medical detail Migration, anorexia, vomiting; LMP/pregnancy possible; urinary/testicular/bowel; drugs; allergy; last food/drink  
Trainee action Structured questions  
What the trainee sees /  
information  
Patient answers  
Normal pathway Classic story  
Abnormal branches Differential diagnosis / diagnostic diversion: conditions that mimic appendicitis (e.g. ectopic pregnancy, ovarian  
torsion, urinary stone) — pregnancy status and clinical assessment can redirect the pathway; allergy  
Escalation / NPC Senior; gynaecology  
Equipment and monitor  
response  
—  
If missed Ectopic missed  
Source WSES 2025; NICE NG126  
Clinician validation —  
5\. Investigations  
Medical detail FBC, CRP, U&E, urinalysis, hCG if possible/uncertain (consent), G&S per policy; lactate/cultures if septic; imaging  
by score/sex/age/pregnancy  
Trainee action Order; arrange monitored transfer if unstable  
What the trainee sees /  
information  
Tubes, kits, scan form  
Normal pathway Samples/scan  
Abnormal branches Unstable → monitored transfer  
Escalation / NPC Porter/nurse/doctor  
Equipment and monitor  
response  
Tubes, transfer kit  
If missed Corridor event  
Source NICE NG45; WSES 2025  
Clinician validation Availability CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 212*

6\. Results  
Medical detail Interpret; score (AIR default/AAS); imaging category  
Trainee action Acknowledge; score  
What the trainee sees /  
information  
Results, report  
Normal pathway Score/imaging consistent  
Abnormal branches Equivocal/negative/complicated  
Escalation / NPC Senior; radiology  
Equipment and monitor  
response  
Viewer  
If missed 'Not seen' treated as negative  
Source WSES 2025; Andersson; Sammalkorpi  
Clinician validation Score choice (surgeon)  
7\. Abnormal branches  
Medical detail Uncomplicated vs complicated; sepsis; dehydration/AKI; K/Na; pregnancy; abnormal SpO2/BP; fasting/aspiration  
Trainee action Verify → assess → initiate workflow  
What the trainee sees /  
information  
Variant data  
Normal pathway —  
Abnormal branches Per branch  
Escalation / NPC Senior; anaesthetist; obstetrics  
Equipment and monitor  
response  
Monitor  
If missed Deterioration  
Source WSES; NG253; KDIGO; UKKA  
Clinician validation —  
8\. Optimisation (in parallel)  
Medical detail Analgesia, antiemetic, fluids, antibiotics (pre-op for all appendectomies), potassium/AKI care  
Trainee action Identify need; request; clinician confirms; nurse administers  
What the trainee sees /  
information  
Chart, fluids  
Normal pathway Improves  
Abnormal branches No response → escalate  
Escalation / NPC Senior; anaesthetist  
Equipment and monitor  
response  
Drips, pumps  
If missed Clock speeds up  
Source WSES 2025; NICE CG174; NG253  
Clinician validation Antibiotic policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
9\. Consent  
Medical detail Urgent but complete; antibiotics-first alternative if uncomplicated (CODA/APPAC); capacity; minors  
Trainee action Dialogue; teach-back  
What the trainee sees /  
information  
Patient, form  
Normal pathway Signed  
Abnormal branches No capacity → senior/emergency route  
Escalation / NPC Senior  
Equipment and monitor  
response  
Form  
If missed Invalid consent  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 213*

Source GMC 2020; CODA; APPAC  
Clinician validation Law CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
10\. Anaesthetic assessment  
Medical detail ASA + E; full stomach/RSI; opioids; vomiting; pregnancy; GLP-1; electrolytes  
Trainee action Report fasting times; findings  
What the trainee sees /  
information  
Anaesthetist  
Normal pathway RSI plan  
Abnormal branches Airway/aspiration issues  
Escalation / NPC Anaesthetist  
Equipment and monitor  
response  
Suction, airway kit  
If missed Aspiration  
Source ASA 2020; ASA fasting  
Clinician validation —  
11\. WHO Sign In  
Medical detail WHO 2009 items; aspiration risk = Yes with equipment/assistance  
Trainee action Answer/lead  
What the trainee sees /  
information  
Board  
Normal pathway Induction (RSI)  
Abnormal branches Any 'no' → resolve  
Escalation / NPC Nurse + anaesthetist  
Equipment and monitor  
response  
Oximeter, suction  
If missed Stop  
Source WHO SSC 2009  
Clinician validation —  
12\. Theatre preparation  
Medical detail Emergency list; lap + open sets; RSI kit  
Trainee action Confirm  
What the trainee sees /  
information  
Theatre  
Normal pathway Ready  
Abnormal branches Fault → substitute  
Escalation / NPC Theatre lead  
Equipment and monitor  
response  
Tower, trays  
If missed Delay  
Source AoA 2019  
Clinician validation —  
13\. WHO Time Out  
Medical detail WHO 2009 items; antibiotic timing confirmed  
Trainee action Participate  
What the trainee sees /  
information  
Team  
Normal pathway READY FOR INCISION  
Abnormal branches Re-dose/fix  
Escalation / NPC Surgeon  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 214*

Equipment and monitor  
response  
Trays  
If missed Stop  
Source WHO SSC; NICE NG125  
Clinician validation —  
14\. Procedure start  
Medical detail Laparoscopic appendectomy (WSES 2025 standard); abstracted  
Trainee action Observe  
What the trainee sees /  
information  
Time-lapse  
Normal pathway Proceeds  
Abnormal branches —  
Escalation / NPC —  
Equipment and monitor  
response  
Monitor  
If missed —  
Source WSES 2025  
Clinician validation —  
15\. Relevant intra-operative training events (ProcSense extension)  
Medical detail Perforation/abscess found; conversion to open; normal appendix with alternative pathology (surgeon decision)  
Trainee action Communicate  
What the trainee sees /  
information  
Theatre  
Normal pathway —  
Abnormal branches Team plan  
Escalation / NPC Surgeon/anaesthetist  
Equipment and monitor  
response  
Open set  
If missed Debrief  
Source WSES 2025 (clinical background); ProcSense educational/simulation extension — clinician validation required.  
Clinician validation Surgeon to approve  
16\. WHO Sign Out  
Medical detail As WHO 2009; specimen = appendix (histology)  
Trainee action Participate  
What the trainee sees /  
information  
Board  
Normal pathway Complete  
Abnormal branches Count/specimen issue  
Escalation / NPC Nurse  
Equipment and monitor  
response  
Specimen pot  
If missed Error  
Source WHO SSC 2009  
Clinician validation —  
17\. Recovery / handover  
Medical detail SBAR; post-op antibiotics: none if uncomplicated; 2–3 days if complicated (WSES 2025); analgesia; fluids  
Trainee action Hand over  
What the trainee sees /  
information  
Recovery bay  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 215*

Normal pathway Accepted  
Abnormal branches Sepsis/ileus/bleeding signs  
Escalation / NPC Recovery nurse; senior  
Equipment and monitor  
response  
Recovery bay  
If missed Omission  
Source WSES 2025  
Clinician validation Policy CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
18\. Deterioration branches  
Medical detail Sepsis/shock, perforation of the appendix, AKI, K abnormality, hypoxia, aspiration  
Trainee action ABCDE; SBAR; initiate workflow  
What the trainee sees /  
information  
Trends  
Normal pathway Recovers  
Abnormal branches Emergency  
Escalation / NPC Senior; critical care  
Equipment and monitor  
response  
Monitor  
If missed Worsening  
Source NICE NG253; RCUK (clinical content). Worsening speed: ProcSense educational/simulation extension — clinician  
validation required.  
Clinician validation Clocks (design)  
19\. Emergency branches  
Medical detail Septic shock; anaphylaxis; airway/aspiration. (A ruptured ectopic pregnancy is a differential diagnosis handled at  
stages 4–7 — a diagnostic diversion, not an appendicitis or appendectomy complication.)  
Trainee action Call; support  
What the trainee sees /  
information  
Team  
Normal pathway Stabilised  
Abnormal branches Emergency team  
Escalation / NPC As above  
Equipment and monitor  
response  
Resus trolley  
If missed Arrest  
Source RCUK; NICE NG126  
Clinician validation Protocols CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION  
20\. Debrief  
Medical detail Timeline against guideline timing (context-dependent); sepsis timing; allergy; pregnancy; unnecessary tests  
Trainee action Review  
What the trainee sees /  
information  
Debrief screen  
Normal pathway Learning  
Abnormal branches —  
Escalation / NPC —  
Equipment and monitor  
response  
—  
If missed —  
Source —  
Clinician validation —  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 216*


# PART H — Clinical Evidence Audit (source-to-claim)

Evidence type — A directly supported by the source; B clinically derived interpretation of authoritative guidance; C context-dependent (patient, institution or clinician  
judgement); D ProcSense simulation design (not a clinical rule). A context-dependent claim is not downgraded if a source directly supports it, and a simulation decision  
is never upgraded to a clinical fact. 'Exact context' states the population/setting the source applies to.  
Clinical claim Value / rule used Source Exact context Type ProcSense interpretation Clinician  
validation  
Adult resting HR 60–100/min reference (resting adults ≥18); NEWS2 scores  
91–110 =1, 111–130 =2, ≥131 =3, 41–50 =1, ≤40 =3  
AHA All About Heart Rate (Pulse);  
RCP NEWS2 2017  
Adults ≥16, non-pregnant,  
hospital  
A Reference ≠ early-warning  
band  
Alarm limits  
BP bands NEWS2 SBP ≤90 =3, 91–100 =2, 101–110 =1, 111–219 =0,  
≥220 =3; hypertension grades ESH 2023  
RCP NEWS2; ESH 2023 Adults; clinic readings A — —  
Elective BP rule Proceed if clinic <180/110; GP target <160/100; defer if  
≥180/110 on repeat after review  
AAGBI/BHS 2016 Elective surgery, adults A Review, not automatic  
cancellation  
Anaesthetist  
MAP 65 Target ≥65 in septic shock SSC 2021; Sepsis-3 Adults with septic shock A — —  
SpO2 targets 94–98% most; 88–92% hypercapnic risk; NEWS2 bands BTS 2017; RCP NEWS2 Acutely ill adults A Process (assess first), not  
threshold reflex  
—  
Hypothermia <36.0 °C → warm before induction NICE CG65 Peri-operative adults A — —  
Sepsis tiers High ≥7 (or 5–6 + single 3): antibiotics + fluid ≤1 h;  
moderate: review ≤1 h, antibiotics ≤3 h; low: ≤6 h  
NICE NG253 (Nov 2025) Over-16s, acute hospital;  
not pregnancy-specific  
A NEWS2 = risk stratification Sepsis tool  
qSOFA Not for stand-alone screening SSC 2021 Adults A — —  
Lactate ≥2 hypoperfusion; ≥4 severe SSC 2021; Sepsis-3 Suspected sepsis A — —  
Potassium Reference 3.5–5.3; hyperkalaemia ≥5.5  
(5.5–5.9/6.0–6.4/≥6.5); ECG for all  
Pathology Harmony; UKKA 2023 Adults A Planned-surgery acceptance  
3.0–5.5 is interpretation  
Anaesthetist  
Planned-surgery K  
acceptance  
3.0–5.5 No guideline value Elective B Default for teaching Anaesthetist  
Sodium 133–146 (UK) / 135–145; severity and correction limits  
(≤10 mmol/L in 24 h)  
Pathology Harmony; European  
2014  
Adults A Postpone <130/>150 =  
interpretation (B)  
Lab range  
Haemoglobin Anaemia <130 M / <120 F (non-pregnant); transfusion  
threshold 70 (80 in ACS)  
WHO 2024; NICE NG24 Adults A — —  
Platelets ≥50 for most surgery; ≥80 for neuraxial BSH 2017 Adults A — —  
AKI ×1.5–1.9 / +26.5 in 48 h; stages 2–3 KDIGO 2012 Adults A Needs baseline —  
Glucose Peri-op 6–10 (6–12); <4 hypo; >12 treat; >14 ketones CPOC/JBDS 2021/2023 Peri-operative diabetes A — Insulin protocol  
HbA1c >69 mmol/mol → optimise before elective CPOC 2021/2023 Elective surgery in diabetes A Optimise, not automatic  
cancel  
—  
Bile-duct stone risk High: stone on imaging, or bilirubin >68 µmol/L + CBD >6  
mm, or cholangitis; intermediate: abnormal LFT/age  
\>55/dilated CBD  
ASGE 2019 Suspected  
choledocholithiasis  
A ESGE differs (no age criterion) Tool choice  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 217*

Clinical claim Value / rule used Source Exact context Type ProcSense interpretation Clinician  
validation  
TG18  
cholecystitis/cholangitis  
Criteria, grades; wall >4 mm; GB >8×4 cm Tokyo Guidelines 2018 Adults A — —  
Appendicitis scores AIR 0–4/5–8/9–12; AAS 0–10/11–15/≥16; Alvarado Original papers; WSES 2025 Adults A Score choice Surgeon  
Appendectomy timing Within 24 h of admission; laparoscopic standard WSES 2025 Uncomplicated/complicated A — —  
Appendicitis antibiotics Pre-op for all; none post-op if uncomplicated; 2–3 days if  
complicated  
WSES 2025 Adults A Agent per policy (D) Policy  
Antibiotics-first CODA 29% appendectomy by 90 days; APPAC 39.1%  
recurrence at 5 y  
CODA 2020; APPAC 2018 Selected uncomplicated  
cases  
A Shared decision —  
Fasting 2 h clear / 6 h light meal, milk / 8 h heavy ASA 2017/2023 Elective A Emergency = full stomach (B) Policy  
GLP-1 RA Continue; risk assess; mitigations; 24 h liquid diet variants Anaesthesia 2025; US 2024;  
SPAQI 2025  
Elective A Guidance differs (shown) Policy  
SGLT2 inhibitors UK: omit day before + day of; FDA: 3 days Anaesthesia 2025; FDA Elective A Disagreement shown Policy  
DOAC stop 1 day (low–moderate) / 2 days (high bleeding risk),  
CrCl-dependent  
PAUSE 2019 Elective, AF patients A Bleeding category for lap chole  
(D)  
Surgeon  
Pregnancy testing Ask if possible; test with consent if any doubt; no age rule NICE NG45 Surgery/anaesthesia A Institutions may add age  
bands (D)  
Protocol  
hCG bands <5 / 5–25 / >25 IU/L Lab convention; NICE NG126 Serum A — —  
WHO checklist 2009 items; additions labelled extension WHO SSC 2009 Surgery A — Local additions  
Consent risks (lap chole) BDI \~0.3%; leak \~0.5%; conversion \~2–5% elective Pucher (SAGES) 2018; large  
series  
Pooled series A Unit data preferred Surgeon  
Educational doses Paracetamol, ondansetron, adrenaline, hydrocortisone,  
enoxaparin, cefazolin examples  
BNF; RCUK 2021; AoA 2020;  
ASHP 2013  
Typical adults A Educational only; not trainee  
prescribing  
Anaesthetist  
Shock index (HR/SBP);  
CRT  
SI 0.5–0.7 normal, approaching/above 1.0 concerning; CRT  
≤2 s  
Koch 2019; Rivers 1994; SSC  
2021 (CRT)  
Supporting indicators only B Not guideline cut-offs;  
interpret with whole picture  
—  
Deterioration clock HR +5–10 and NEWS2 +1 every \~30 min if untreated — Simulation D Design value Clinician  
realism  
Transfer escort by  
NEWS2  
≤4 porter+nurse; 5–6 nurse+monitor; ≥7  
doctor+nurse+full monitoring  
— Simulation D No numeric guideline Institution  
Turnaround times FBC 45 min, imaging 60–120 min, etc. — Simulation D Design value —  
Antibiotic agent; sepsis  
tool; consent law;  
escalation structure; lab  
ranges  
— — — C Institution-specific Institution  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 218*


## H.2 v5.1 verification log — the four previously unverified items

Item What was checked Source Outcome  
Adult resting  
heart rate  
60–100 beats/min American Heart Association 'All About Heart Rate (Pulse)'  
/ target-heart-rate pages: adults 18+ 60–100; athletes  
may be \~40  
Kept. Labelled a reference range  
(not a decision rule); NEWS2  
bands kept separate (51–90  
scores 0).  
ECG intervals PR, QRS, QTc values QTc: AHA/ACCF/HRS 2009 Part IV (prolonged ≥450 ms  
men, ≥460 ms women; ≥500 ms markedly prolonged;  
some inpatient protocols use \~470/480). PR 120–200 ms  
and QRS (normal \~80–100 ms; ≥120 ms wide) are  
standard ECG-reference values  
QRS wording corrected; QTc  
cited; PR/QRS labelled  
educational reference values;  
added statement that no single  
interval decides surgery.  
Educational  
doses  
Adrenaline 500 micrograms  
IM, repeat at 5 min  
Resuscitation Council UK 2021 / SmPC Verified.  
Hydrocortisone 100 mg IV at  
induction  
Association of Anaesthetists/RCP/SfE 2020 (long-term  
glucocorticoid / adrenal insufficiency)  
Verified; scope stated.  
Cefazolin 2 g; 3 g if ≥120 kg ASHP/IDSA/SIS/SHEA 2013; FDA label Verified; agent per institutional  
policy.  
IV paracetamol Marketing-authorisation SmPCs: 1 g up to 4×/day, ≥4 h  
interval, 4 g/day max (3 g with risk factors),  
weight-based under 50 kg  
Corrected from '6-hourly'.  
Ondansetron 4 mg slow IV SmPC (PONV prevention at induction/treatment of  
established PONV)  
Verified; indication stated.  
Enoxaparin SmPC: 20 mg moderate risk; 40 mg high risk; 20 mg if  
CrCl 15–30  
Corrected: no longer a single '40  
mg' example.  
Glucose for hypoglycaemia JBDS hospital hypoglycaemia guideline: 15–20 g oral  
quick-acting carbohydrate if conscious/able to swallow;  
IV 10% 150–200 mL or 20% 75–100 mL over \~15 min  
Verified; fasting-status caveat  
added.  
Hyperkalaemia treatment UK Kidney Association algorithm: 30 mL 10% calcium  
gluconate over 10 min (or 10 mL 10% calcium chloride);  
10 units insulin in 25 g glucose over 15–30 min; 10%  
glucose 50 mL/h × 5 h if glucose <7  
Added as a labelled educational  
example.  
Fluid bolus NICE CG174 rec 1.3.1: crystalloid Na 130–154 mmol/L,  
500 mL over <15 min; scope excludes pregnancy,  
severe liver/kidney disease, diabetes, burns  
Verified; scope caveat added.  
Shock index Formula and thresholds Koch et al., Open Access Emerg Med 2019 (normal  
0.5–0.7, some accept to 0.9, approaching 1.0  
worsening); Rivers 1994 (SI >0.9 associated with critical  
illness)  
Re-sourced; described as a  
supporting indicator; '>1.3'  
removed as unsupported.  
Not freshly re-verified in this pass (carried from earlier passes and labelled accordingly): naloxone and other  
doses that the document leaves clinician-controlled have no numeric value; the BTS statement that critically ill  
patients start on a reservoir mask at 15 L/min was not re-fetched this pass.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 219*


# PART I — Clinician Validation, Institutional

Adaptation and Final Status  

## I.1 Institutional adaptation (context-dependent items)

ProcSense teaches internationally defensible principles. Where practice legitimately varies, the rule is marked  
CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION . Final implementation should be adapted to the  
target institution's approved clinical protocol. The items are:  
Area What varies between institutions / countries  
Laboratory Reference ranges, units, critical-value lists, turnaround times  
Antimicrobials Prophylaxis and treatment agent/dose/redose; allergy alternatives (formulary and local resistance)  
Consent Form, witness, age of majority and minor consent, capacity law, emergency treatment rules, interpreter  
provision  
Escalation Early-warning tool, sepsis tool, who responds at each level  
Anaesthesia Fasting details, GLP-1/SGLT2 policy choice, pre-assessment workflow  
Surgery/theatre WHO checklist additions, site-marking policy, imaging pathways, MRCP/ERCP availability  
Blood bank, transfer,  
discharge  
Group-and-save schedule; escort rules; day-case criteria  

## I.2 Final Clinician / Institutional Decisions Required

Only items that cannot be settled reliably from international sources:  
\# Decision Owner  
1 Laboratory reference ranges, units and critical-value list Hospital laboratory  
2 Antibiotic protocol: prophylaxis agent/dose/redose; appendicitis regimen; penicillin-allergy  
alternative (incl. cefazolin after anaphylaxis)  
Microbiology / surgeon  
3 Consent/legal workflow: form, witness, age/guardian rules, mature minors, emergency treatment,  
family involvement  
Institution legal / surgeon  
4 Fasting policy details (clear fluids 2 h vs 1 h; GLP-1 and SGLT2 policy choice between UK and US  
guidance)  
Anaesthesia department  
5 Local anaesthetic protocols (insulin, steroid cover, potassium acceptance range 3.0–5.5 and INR  
limits as defaults)  
Anaesthesia department  
6 Emergency escalation: early-warning tool, who responds at each level, sepsis tool Hospital  
7 Equipment availability (MRCP, ERCP, CT, gastric ultrasound, capnography, warming) Hospital  
8 Documentation: ward checklist, handover form, WHO checklist version/additions, site-marking policy Theatre governance  
9 Blood-bank procedure (G&S/crossmatch schedule, labelling) Transfusion lab  
10 Patient identification policy (identifiers, band colours) Hospital  
11 Transfer escort/monitoring rule Hospital  
12 Post-operative/day-case discharge criteria Surgeon / anaesthetist  
13 Choice of appendicitis score (AIR default vs AAS) and imaging pathway for high-score young patients Surgeon  
14 Unit-specific complication rates for consent (if audited data exist) Surgeon  
15 Realism of simulator design values (deterioration clocks, turnaround times) Surgeon + anaesthetist  

## I.3 Clinician Final Validation Checklist

A. Medical facts  
Item OK Correction / comment  
Anatomy explanations ☐  
Disease definitions (biliary colic, cholecystitis,  
choledocholithiasis, cholangitis, pancreatitis, appendicitis,  
perforation, sepsis)  
☐  
Symptoms and examination findings ☐  
Investigations and their indications ☐  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 220*

Item OK Correction / comment  
Interpretation (patterns, artefacts) ☐  
Thresholds (reference vs decision) ☐  
Complications ☐  
B. Numbers  
Item OK Correction / comment  
Vital-sign ranges and NEWS2 bands ☐  
Laboratory values (after local lab alignment) ☐  
Imaging thresholds (wall >4 mm, CBD >6 mm, appendix >6  
mm)  
☐  
Scoring systems (AIR/AAS/Alvarado, TG18, ASGE, ASA) ☐  
Fasting intervals ☐  
Timing (antibiotics, sepsis, surgery) ☐  
Medication-related educational values ☐  
C. Surgery  
Item OK Correction / comment  
S1 workflow ☐  
S2 workflow ☐  
Indications ☐  
Timing ☐  
Escalation points ☐  
Complications and consent figures ☐  
D. Anaesthesia  
Item OK Correction / comment  
Fasting and GLP-1/SGLT2 policy ☐  
Aspiration risk and RSI ☐  
Monitoring ☐  
Emergency pathway ☐  
Medication considerations ☐  
E. Consent  
Item OK Correction / comment  
Capacity ☐  
Refusal ☐  
Interpreter ☐  
Minors ☐  
Emergency situations ☐  
Applicable legal requirements for the target jurisdiction ☐  
F. Hospital / local policy  
Item OK Correction / comment  
Antibiotics ☐  
Labs ☐  
Blood bank ☐  
Theatre ☐  
Transfer ☐  
Emergency escalation ☐  
Documentation ☐  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 221*

G. VR realism  
Item OK Correction / comment  
Equipment inventory ☐  
Patient reactions ☐  
Monitor behaviour ☐  
NPC behaviour ☐  
Consequences ☐  
Workflow order ☐  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 222*


## FINAL STATUS

Final status  
ProcSense Medical Walkthrough is an educational clinical-training foundation and has not been clinically  
approved for real-world patient care. Clinical content, thresholds, medication examples, workflows,  
simulation behaviour and scenario logic require review and validation by appropriately qualified  
clinicians before being treated as final clinical training content.  
Document status — v5.1  
READY FOR CLINICIAN VALIDATION. Clinical content has been researched against current authoritative guidance  
and structured for undergraduate simulation. It is NOT clinically approved and NOT ready for real-world clinical use.  
Review should focus on clinical applicability, context-dependent (type C) items, scenario realism and the ProcSense  
simulation design values (type D), particularly for surgical, anaesthetic, medication and escalation decisions.  
Safety statement  
- ProcSense is an educational VR simulation, not medical advice and not a clinical decision-support system.  
- All patients and results are synthetic; case values are simulator-generated and distinct from the clinically sourced  
bands.  
- The trainee identifies clinical requirements, communicates and initiates workflows; a qualified clinician confirms  
treatment decisions and a clinical professional administers. Doses shown are educational examples only.  
- Local institutional protocols take precedence; sources should be re-checked before release because guidance  
changes.  
Sign-off  
Role Name / registration Signature Date  
Surgeon  
Anaesthetist  
Institution representative  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 223*


# Part 28 — Sources (references)

Versions cited are those reviewed for this draft. Where a source could not be fully accessed, it is marked  
'verify'. No reference has been invented; the reviewer should still confirm current versions.  
Safety / process  
- World Health Organization. WHO Surgical Safety Checklist (2009) and Implementation Manual. Geneva: WHO; 2009.  
- WHO Collaborating Centre for Patient Safety Solutions. Communication during patient hand-overs. Patient Safety Solutions  
vol 1, solution 3; 2007.  
- Gelb AW et al. World Health Organization–World Federation of Societies of Anaesthesiologists (WHO-WFSA) International  
Standards for a Safe Practice of Anesthesia. Can J Anesth 2018.  
- Association of Anaesthetists. Checking anaesthetic equipment (2019); Recommendations for standards of monitoring  
during anaesthesia and recovery (2021).  
- General Medical Council. Decision making and consent (2020). Montgomery v Lanarkshire Health Board \[2015\] UKSC 11.  
- Resuscitation Council UK. Emergency treatment of anaphylaxis (2021); ABCDE approach; ALS algorithms (2021).  
Pre-operative assessment / perioperative  
- NICE NG45. Routine preoperative tests for elective surgery (2016).  
- NICE NG180. Perioperative care in adults (2020). NICE QS216 Perioperative care (2022).  
- NICE NG89. Venous thromboembolism in over 16s (2018, updated 2019).  
- NICE NG125. Surgical site infections: prevention and treatment (2019, updated 2020).  
- NICE CG65. Hypothermia: prevention and management in adults having surgery (2008, updated 2016).  
- NICE CG174. Intravenous fluid therapy in adults in hospital (2013, updated 2017).  
- NICE NG24. Blood transfusion (2015).  
- Hartle A et al. The measurement of adult blood pressure and management of hypertension before elective surgery: AAGBI  
and British Hypertension Society. Anaesthesia 2016;71:326–37.  
- Mancia G et al. 2023 ESH Guidelines for the management of arterial hypertension. J Hypertens 2023. McEvoy JW et al.  
2024 ESC Guidelines for elevated blood pressure and hypertension. Eur Heart J 2024.  
- Halvorsen S et al. 2022 ESC Guidelines on cardiovascular assessment and management of patients undergoing  
non-cardiac surgery. Eur Heart J 2022.  
- American Society of Anesthesiologists. ASA Physical Status Classification System (updated 2020).  
- American Society of Anesthesiologists. Practice guidelines for preoperative fasting (2017) and Joshi GP et al. modular  
update, Anesthesiology 2023.  
- Kindel TL et al. Multisociety clinical practice guidance for the safe use of GLP-1 receptor agonists in the perioperative  
period (ASA/AGA/ASMBS/ISPCOP/SAGES). Surg Obes Relat Dis 2024;20:1183–6; Clin Gastroenterol Hepatol 2024.  
- Centre for Perioperative Care (CPOC). Guideline for perioperative care for people with diabetes mellitus undergoing  
elective and emergency surgery (2021, updated 2023). JBDS-IP hypoglycaemia and DKA guidelines.  
- Woodcock T et al. Guidelines for the management of glucocorticoids during the peri-operative period (Association of  
Anaesthetists / Society for Endocrinology). Anaesthesia 2020.  
- Douketis JD et al. Perioperative management of patients with atrial fibrillation receiving a DOAC (PAUSE). JAMA Intern Med  
2019\. Doherty JU et al. 2017 ACC Expert Consensus Decision Pathway for periprocedural management of anticoagulation.  
- Bratzler DW et al. Clinical practice guidelines for antimicrobial prophylaxis in surgery (ASHP/IDSA/SIS/SHEA). Am J  
Health-Syst Pharm 2013.  
- British Thoracic Society. Guideline for oxygen use in healthcare and emergency settings (2017).  
- Chung F et al. STOP-Bang questionnaire. Anesthesiology 2008; Chest 2016.  
Early warning / sepsis / renal  
- Royal College of Physicians. National Early Warning Score (NEWS) 2 (2017).  
- Singer M et al. The Third International Consensus Definitions for Sepsis and Septic Shock (Sepsis-3). JAMA 2016.  
- Evans L et al. Surviving Sepsis Campaign: international guidelines 2021. Crit Care Med 2021.  
- NICE NG253. Suspected sepsis in over 16s: identification and early management in acute hospital settings using NEWS2  
(November 2025); supersedes the NEWS2-related parts of NG51.  
- KDIGO Clinical Practice Guideline for Acute Kidney Injury (2012); KDIGO CKD guideline (2024). NICE NG148 Acute kidney  
injury (2019).  
- UK Kidney Association. Clinical practice guideline: treatment of acute hyperkalaemia in adults (2023).  
- Royal College of Pathologists. The communication of critical and unexpected pathology results (2017).  
- US FDA. Pulse oximeter accuracy and limitations: safety communication (2021). NHS Race and Health Observatory. Pulse  
oximetry and racial bias (2021).  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 224*

Gallstone disease  
- NICE CG188. Gallstone disease: diagnosis and management (2014). NICE QS104 (2015).  
- Yokoe M et al. Tokyo Guidelines 2018: diagnostic criteria and severity grading of acute cholecystitis; Kiriyama S et al. TG18  
acute cholangitis. J Hepatobiliary Pancreat Sci 2018.  
- Buxbaum JL et al. ASGE guideline on the role of endoscopy in the evaluation and management of choledocholithiasis.  
Gastrointest Endosc 2019;89:1075–1105.  
- Manes G et al. ESGE guideline: endoscopic management of common bile duct stones (2019).  
- Kwo PY et al. ACG Clinical Guideline: evaluation of abnormal liver chemistries. Am J Gastroenterol 2017.  
- Banks PA et al. Revised Atlanta classification of acute pancreatitis (2012).  
- StatPearls: Laparoscopic cholecystectomy; Cholecystitis (NCBI Bookshelf, current editions).  
Appendicitis  
- Di Saverio S et al. Diagnosis and treatment of acute appendicitis: 2020 update of the WSES Jerusalem guidelines. World J  
Emerg Surg 2020;15:27. (2025 special-populations update — verify access.)  
- Alvarado A. A practical score for the early diagnosis of acute appendicitis. Ann Emerg Med 1986.  
- Andersson M, Andersson RE. The Appendicitis Inflammatory Response score. World J Surg 2008.  
- Sammalkorpi HE et al. A new adult appendicitis score improves diagnostic accuracy. BMC Gastroenterol 2014.  
- Ghali MS et al. Adult appendicitis score versus Alvarado score: comparative study. Surg Open Sci 2023;14:96–102.  
- CODA Collaborative. A randomized trial comparing antibiotics with appendectomy for appendicitis. NEJM 2020.  
- Salminen P et al. Five-year follow-up of antibiotic therapy for uncomplicated acute appendicitis (APPAC). JAMA 2018.  
- StatPearls: Acute appendicitis; Appendectomy (NCBI Bookshelf).  
- BMJ Best Practice. Acute appendicitis — criteria (accessed 2026).  
Pregnancy / haematology / other  
- NICE NG126. Ectopic pregnancy and miscarriage (2019, updated 2023).  
- Pearl JP et al. SAGES guidelines for the use of laparoscopy during pregnancy. Surg Endosc 2017.  
- WHO. Guideline on haemoglobin cutoffs to define anaemia in individuals and populations (2024).  
- Muñoz M et al. International consensus statement on the peri-operative management of anaemia and iron deficiency.  
Anaesthesia 2017.  
- Estcourt LJ et al. British Society for Haematology guidelines for the use of platelet transfusions (2017).  
- WHO Expert Consultation. Appropriate body-mass index for Asian populations. Lancet 2004.  
- British National Formulary (BNF) — paracetamol, ondansetron, enoxaparin monographs (current edition).  
- openFDA Drug Label API — contraindications/warnings only (no doses imported).  
- Target institution's approved clinical protocols (applied at implementation).  
Additional and updated sources used in v4  
- Pathology Harmony Group. Agreed adult clinical biochemistry reference intervals (January 2011). UK.  
- UK Kidney Association. Clinical practice guideline: treatment of acute hyperkalaemia in adults (2023).  
- Spasovski G et al. Clinical practice guideline on diagnosis and treatment of hyponatraemia. Eur J Endocrinol 2014.  
- Kwo PY, Cohen SM, Lim JK. ACG clinical guideline: evaluation of abnormal liver chemistries. Am J Gastroenterol 2017.  
- NICE NG253. Suspected sepsis in over 16s: identification and early management in acute hospital settings using NEWS2  
(November 2025).  
- Di Saverio S et al. Diagnosis and treatment of acute appendicitis: 2025 edition of the WSES Jerusalem guidelines. JAMA  
Surg 2026 (doi:10.1001/jamasurg.2025.6218).  
- Yokoe M et al. Tokyo Guidelines 2018 (and TG07/TG13 imaging criteria: wall >4 mm; long axis >8 cm, short axis >4 cm).  
- Pucher PH et al. (SAGES Safe Cholecystectomy Task Force). Outcome trends and safety measures after 30 years of  
laparoscopic cholecystectomy: systematic review and pooled data analysis. Surg Endosc 2018.  
- CODA Collaborative. A randomized trial comparing antibiotics with appendectomy for appendicitis. N Engl J Med  
2020;383:1907–19.  
- Salminen P et al. Five-year follow-up of antibiotic therapy for uncomplicated acute appendicitis in the APPAC randomized  
clinical trial. JAMA 2018;320:1259–65.  
- El-Boghdadly K et al. Elective peri-operative management of adults taking GLP-1 receptor agonists, GIP agonists and  
SGLT2 inhibitors: multidisciplinary consensus statement (Association of Anaesthetists, ABCD, BOMSS, CPOC, JBDS-IP,  
RCoA, SOBA, UKCPA). Anaesthesia 2025;80:412–24.  
- Kindel TL et al. Multisociety clinical practice guidance for the safe use of GLP-1 receptor agonists in the perioperative  
period. Surg Obes Relat Dis 2024.  
- SPAQI. Perioperative management of patients taking GLP-1 receptor agonists: multidisciplinary consensus statement. Br J  
Anaesth 2025;135:48–78.  
- Association of Anaesthetists, Obstetric Anaesthetists' Association, Regional Anaesthesia UK. Regional anaesthesia and  
patients with abnormalities of coagulation. Anaesthesia 2013.  

---

*ProcSense Medical Walkthrough v5.1 — READY FOR CLINICIAN VALIDATION (not clinically approved) Educational simulation • not for patient care*  
*Sources cited at point of use and in Part H • Institutional protocol takes precedence Page 225*

- Rautaharju PM et al. AHA/ACCF/HRS recommendations for the standardization and interpretation of the ECG, part IV  
(2009); Drew BJ et al. AHA scientific statement on prevention of torsade de pointes (2010).  
- Koch E et al. Shock index in the emergency department: utility and limitations. Open Access Emerg Med 2019;11:179–99.  
Rivers E et al. Comparison of the shock index and conventional vital signs to identify acute critical illness in the emergency  
department. Ann Emerg Med 1994. Allgöwer M, Burri C. Dtsch Med Wochenschr 1967 (origin).  
- NICE CG183. Drug allergy: diagnosis and management (2014).  
- Royal College of Anaesthetists. 6th National Audit Project (NAP6): perioperative anaphylaxis (2018).  
