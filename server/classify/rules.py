"""Deterministic communication-act matcher (approach A of docs/decisions/ADR-0020): rules only, no model, no training data, no service.

    r = RuleActClassifier().classify("Does that make sense so far?")      # Result(act="CHECK_UNDERSTANDING", credited=True, source="rules")

A sentence is credited under an ordinary act only when (1) exactly ONE act's patterns match, (2) no critical-content cue matches (server/classify/guard.py: a sentence that touches an allergy, an identity check, consent, fasting, a worsening sign or a handover is
never credited under an ordinary act), (3) the sentence is an assertion of the act and not a denial, a hypothetical, a report or broken off (server/classify/assertion.py), and (4) it is not a self-correction that changes what was said
("... no, I mean ..."). Two acts matching, no act matching or a rule that fires on a refused sentence gives no credit: the debrief records the sentence and the trainee can use the menu. Critical acts are never named by this matcher; a critical sentence is
only ever recorded as NONE. The patterns are written from the act definitions of content/assessment/taxonomy.json and general clinical-communication phrasing, not from the held-out sets; they are deliberately narrow because a missed sentence costs
only evidence for the debrief, while a wrong credit is a false record.
"""
from __future__ import annotations

import re

try:
    from classify.assertion import non_assertive
    from classify.classifier import Result
    from classify.guard import critical_cue
except ImportError:
    from server.classify.assertion import non_assertive
    from server.classify.classifier import Result
    from server.classify.guard import critical_cue

I = re.IGNORECASE
_TESTS = r"(x ?ray|chest film|scan|ct|mri|ultrasound|ecg|echo|blood test|bloods|urine test|urine sample|swab|blood count|crp|kidney function|liver function|clotting|group and save|cultures|lactate|troponin|test|tests)"
_TOPIC = (r"(appetite|waterworks|bowels?|sleep|cough|pain|hurt|breath|chest|stomach|tummy|belly|headache|dizz|nausea|sick|vomit|temperature|fever|weight|smok|drink|alcohol|family|tablets|medicines?|medication|"
          r"operations?|surgery|anaesthetic|history|symptoms?|problems?|complaint|brought you|work|live|exercise|stairs|heart|kidney|diabet|asthma|blood pressure|thin your blood|teeth|dentures|crowns|mobility|walk)")
PATTERNS: dict[str, list[re.Pattern]] = {act: [re.compile(p, I) for p in ps] for act, ps in {
    "ASK_TOPIC": [rf"^(do|does|did|have|has|are|is|were|was|can|could|would|any)\b.*\b{_TOPIC}\b.*\?$", rf"^(what|when|how|where|which|who)\b.*\b{_TOPIC}\b.*\?$", rf"^tell me (a little |a bit )?about (the |your )?\b{_TOPIC}\b", rf"^(any|how is|how are) .*\b{_TOPIC}\b"],
    "CLARIFY": [r"\bwhen you say\b", r"\bdo you mean\b", r"\bwhat (exactly )?do you mean\b", r"\bwhat does .{1,30} mean (to you)?\b", r"\b(could|can) you (say|repeat) that again\b", r"\b(i did(n'?t| not)|sorry,? i didn'?t) (quite )?(follow|catch|hear|understand)\b",
                r"\bsorry,? did you say\b", r"\bcan you (tell me )?more about\b", r"\b(could|can) you (put|say) (it|that) (differently|another way)\b", r"\bcould you rephrase\b", r"\bjust so i follow\b", r"\bsay that (last part )?once more\b"],
    "ANSWER_QUESTION": [r"^(yes|no),? (the|it|you|there|we|most)\b.*\b(operation|procedure|scan|test|stay|drain|hospital|home|wake|sleep|pain|hurt|result|recovery|will|won'?t|usually|normally)\b", r"^(it|the (operation|procedure|scan|surgery)) (usually |normally |will |should )?(takes?|lasts?|is over)\b.*\b(minutes?|hours?|day|days)\b",
                        r"^most people (go|are|feel|recover|stay)\b", r"^you (will|won'?t) (be|need|feel|have)\b.*\b(awake|asleep|drain|stay|pain|discomfort|home|hospital)\b", r"^(roughly|about|around|usually) (an? |one |two |three |\d+)\s?(hour|hours|minutes|days|nights)\b"],
    "EXPLAIN_PROCEDURE": [r"^(we|the surgeon|the surgical team|they) (will |would )?(make|use|put|take|pass|insert|place|remove|clean|cover|give you)\b.*\b(cuts?|incisions?|camera|sleep|airway|tube|cannula|drip|sterile|operation|anaesthetic)\b", r"^you will be (put to sleep|asleep|given|taken|moved|connected|monitored)\b",
                          r"^(after|during|before) the (operation|procedure|surgery)\b.*\b(you|we|will)\b", r"^(the )?(operation|procedure|surgery|keyhole|anaesthetic) (is|works|involves|means|takes)\b", r"^a (tube|line|drip|cannula) (is|will be) (passed|put|placed|inserted)\b"],
    "EXPLAIN_TEST": [rf"^(the |this |an? )?{_TESTS}( test| scan)?( (is|are|will|tells|shows|checks|looks|uses|measures|lets|helps))\b", rf"^(the |this |an? )?{_TESTS} (tells|shows|checks|looks for|measures|uses|helps|lets)\b", rf"^(this|that) {_TESTS}\b.*\b(for|to see|to check|looks)\b", rf"^{_TESTS} (for|to see|to check)\b.*\b(infection|kidney|liver|rhythm|stones|clot|bleeding|blockage)\b"],
    "EXPLAIN_ACTION": [r"^(i am|i'?m|i will|i'?ll|i'?m just|i am just) (going to |now |just )?(put|place|wrap|lift|raise|listen|take|check|measure|press|feel|examine|clip|attach|clean|insert|give|start|turn|help)\b.*\b(your|you|the bed|a small|this)\b",
                       r"^(putting|placing|wrapping|lifting|raising|listening|taking|checking|measuring|pressing|feeling|examining|attaching|cleaning) (the |a |your |on |this )", r"^(now|just) (putting|placing|lifting|taking|checking|feeling|listening)\b"],
    "CHECK_UNDERSTANDING": [r"\bin your own words\b", r"\bdoes that make sense\b", r"\b(do you have|any) (any )?questions\b", r"\b(shall|should|would you like me to) (i|me) (repeat|go over|explain)\b", r"\bcan you (tell|explain to|show) me (what|how|which)\b.*\b(understand|will happen|you need to|you stop|you carry)\b",
                            r"\bwhat (have|did) you (understood|understand)\b", r"\bdo you understand\b", r"\b(are you|is that) clear\b", r"\bwhat questions do you have\b", r"\bwould you like me to go over\b", r"\bplease tell me (again )?what you understand\b"],
    "ACKNOWLEDGE_EMOTION": [r"\bi can (see|tell|hear) (that )?(this|you|it)\b.*\b(frighten|scar|worr|anxious|nervous|upset|distress|afraid|stress|difficult|hard|overwhelm|tearful|pain)", r"\b(completely |perfectly |totally )?(normal|natural|understandable|okay|ok) to (feel|be)\b", r"\byou (sound|seem|look) (very |quite |really |a bit )?(worried|anxious|nervous|upset|frightened|scared|afraid|tearful|stressed|overwhelmed)\b",
                            r"\b(that|this) (must|would) (have been|be|feel)\b.*\b(upsetting|frightening|scary|hard|difficult|awful|stressful)\b", r"\bit makes sense (that )?you (feel|are)\b", r"\bi (am|'m) sorry,? (that|this|you)\b.*\b(upset|frighten|hard|difficult|going through|distress)"],
    "REASSURE": [r"\byou are in good hands\b", r"\b(please |do )?(don'?t|not) (worry|be afraid|be frightened)\b", r"\bnothing (here )?(is |to be )?(to worry|a cause for alarm|to be afraid)\b", r"\bwe (do|perform) (these|this|them)\b.*\b(every|all the time|daily|regularly|often)\b", r"\b(routine|common|very safe|straightforward) (procedure|operation|test|scan)\b", r"\bwe will (look after|take care of|stay with) you\b",
                 r"\byou will be (well )?(looked after|cared for|fine|okay|safe)\b", r"\bthe (team|staff) (here )?(is|are) (highly |very )?experienced\b", r"\bthe pain (should|will) ease\b"],
    "ACKNOWLEDGE_FACT": [r"^(thank you|thanks|right|okay|ok|understood|noted|i see|got it|i have noted|i've noted),? (that |so |i |you )", r"^(noted|understood|got it)\b[,.]", r"\bi (have |'ve )?noted (that )?you\b", r"^(so|right,? so|okay,? so) (you|the|your|it|this|that|they)\b.*\b(since|ago|years?|last|began|started|live|take|took|had|have)\b"],
    "CLOSED_LOOP": [r"\b(repeating|reading) (that )?back\b", r"\bread ?back\b", r"\bto confirm,? (you|that|i)\b", r"\bjust to confirm\b", r"\bconfirming\b", r"\bi (will|'ll) (tell|let|call|confirm|report|update|inform|come back|bleep)\b.*\b(when|once|after|in)\b", r"^(okay|understood|got it|right),? .*\bi (will|'ll)\b.*\b(when|once|after|in)\b"],
    "ESCALATE_SPEECH": [r"\b(call|bleep|page|get|ask|want|need|request|escalate to|contact)\b.*\b(senior|registrar|consultant|on[- ]call|anaesthetist|surgeon|boss|specialist)\b", r"\b(beyond|outside) (what|my)\b.*\b(manage|competence|level|skills)\b", r"\bi (can'?t|cannot) (decide|manage) this\b", r"\bescalat"],
    "REPORT_FAULT": [r"\b(alarm(ing|s)?|beeping|not working|isn'?t working|won'?t (start|print|close|work|turn)|broken|faulty|jammed|displaying an error|error message|no trace|reads empty|is empty|has run out|not ringing|leaking|out of (order|paper|battery))\b"],
    "VOICE_ORDER": [rf"^(please |could you |can you |can we |could we |i would like to |i'?d like to |i want to )?(order|request|arrange|book|organise|send for|get|do|run|start|set up)\b.*\b({_TESTS}|saline|fluids?|oxygen|antibiotics?|porter|theatre|a bed)\b", rf"^(order|request) (a |an |the |some )?\w"],
}.items()}
# Topics next to the five critical acts. A sentence that touches one of them is never credited under an ordinary act by the rules, because the rules cannot tell "what tablets do you take" (history) from "which tablets must we avoid" (an allergy), "did you eat" (history) from a fasting
# check, or "tell me your wishes" (talk) from a consent check. This is a topic list, not a list of sentences; it costs credit on ordinary sentences about medicines, food, names or pressure, by design.
ADJACENT = re.compile(r"\b(allerg\w*|react\w*|rash|hives|swell\w*|itch\w*|wheez\w*|throat|anaphyla\w*|intoleran\w*|sensitiv\w*|warned|flagged|keep away|stay away|avoid\w*|drugs?|pills?|penicillin|antibiotics?|tablets?|medicines?|medications?|"
                       r"eat|ate|eaten|eating|food|drink\w*|breakfast|lunch|dinner|meals?|stomach|tummy|belly|hungry|fast|fasted|fasting|empty|overnight|midnight|nil by mouth|nothing (by|to)|"
                       r"name|names|date of birth|birth|born|wrist ?band|tag|bracelet|address|who you are|details|hospital number|nhs number|identity|"
                       r"consent|agree\w*|wish\w*|happy (for|to|with)|willing|sign\w*|forms?|initial\w*|decision|change your mind|proceed|go ahead|permission|risks?|want (this|the|to have)|"
                       r"worse|worsen\w*|deteriorat\w*|drift\w*|falling|dropp\w*|climbing|rising|heading|slipping|pressure|pulse|sats|saturations?|bp|breathing|heart rate|"
                       r"(are you|is it) (ok|okay|fine|alright) (for|with) me|reason for my call|i am calling|i'?m calling|background|recommend\w*|situation|assessment|review (her|him|them)|someone senior|handing over|hand over|handover|before i go|next shift|nurse said|latex)\b", I)
SELF_CORRECTION = re.compile(r"\b(no,? (i mean|sorry)|i mean|sorry,? (i mean|no)|correction|rather|scratch that|actually,? no|oh no)\b", I)
THINKING_ALOUD = re.compile(r"^(hmm|um|uh|ok,? ok)\b|\bi (keep|might|think i will|'ll maybe)\b.*\b(maybe|might)\b", I)


DEVICE = re.compile(r"\b(pump|monitor|machine|bell|line|drip|ecg|lead|cylinder|oxygen|battery|door|light|printer|bed|syringe|driver|trolley|alarm|scanner|probe|cuff|screen|computer|phone|heating|lift|tap|sink|equipment|device|thermometer|trace)\b", I)
COMMITMENT = re.compile(r"\d|\bi(' ?ll| will| shall| am going)\b|\b(repeat|read(ing)? ?back|confirm|call when|let you know)\b", I)
UNDERSTANDING = re.compile(r"\b(understand|in your own words|explain to me|tell me what|remind me)\b", I)


def match_acts(sentence: str) -> list[str]:
    s = sentence.strip()
    acts = [act for act, pats in PATTERNS.items() if any(p.search(s) for p in pats)]
    if "REPORT_FAULT" in acts and not DEVICE.search(s):
        acts.remove("REPORT_FAULT")                                       # 'alarm' in 'no cause for alarm' is reassurance, not a faulty machine
    if "ACKNOWLEDGE_FACT" in acts and COMMITMENT.search(s):
        acts.remove("ACKNOWLEDGE_FACT")                                   # a number, a promise or a read-back is a closed loop, not a plain acknowledgement
    if "ASK_TOPIC" in acts and UNDERSTANDING.search(s):
        acts.remove("ASK_TOPIC")                                          # asking the patient what they understand is a check of understanding
    return acts


class RuleActClassifier:
    def __init__(self, embedder=None, head=None):
        self.model = "rules"
        self.critical = {"SBAR_ELEMENT", "CONFIRM_IDENTITY", "STATE_ALLERGY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT"}

    def classify(self, sentence: str) -> Result:
        acts = match_acts(sentence)
        if len(acts) != 1:
            return Result("NONE", 0.0, False, "rules", False, "", "NO_RULE" if not acts else "RULES_DISAGREE:" + "+".join(acts))
        act = acts[0]
        if critical_cue(sentence):
            return Result(act, 1.0, False, "rules", False, "", "CRITICAL_CUE")
        if ADJACENT.search(sentence):
            return Result(act, 1.0, False, "rules", False, "", "CRITICAL_CUE:adjacent topic")
        if (kind := non_assertive(sentence)):
            return Result(act, 1.0, False, "rules", False, "", f"NON_ASSERTIVE:{kind}")
        if SELF_CORRECTION.search(sentence) or THINKING_ALOUD.search(sentence):
            return Result(act, 1.0, False, "rules", False, "", "SELF_CORRECTION")
        return Result(act, 1.0, True, "rules", False)
