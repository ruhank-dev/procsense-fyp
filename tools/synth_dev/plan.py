"""Builds and freezes the specifications of the direct synthetic development set (SD1, 500 items).

Everything here is decided BEFORE any sentence is written and without reading one: the objective, situation, actor and recipient, step and window, waiting-question / read-back state, contrast-set membership, style and persona, and the gold derived from the approved rules (gold.py).
Usage: uv run --project tools tools/synth_dev/plan.py [--write] [--check]
--write creates tests/synthetic_dev_v1/specs.json and manifest.json (SHA-256) ; --check fails when they differ from what the code produces (the specifications are frozen once utterances exist)."""
from __future__ import annotations

import collections
import hashlib
import json
import random
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gold as G
import seeds as S

OUT = G.ROOT / "tests" / "synthetic_dev_v1"
SEED = 20261101
RESERVED_PERSONAS = ["P5", "P7"]
PERSONAS = {"P1": "a nervous first-year student: hesitant, apologetic", "P2": "a confident final-year student: fluent, efficient", "P3": "a chatty, warm student: friendly, conversational",
            "P4": "a terse VR user: as few words as the sense allows", "P6": "a student whose first language is not English: simple, understandable English"}
STYLES = {  # style -> (weight, length, personas)
    "concise": (17, "short", ["P4"]), "conversational": (16, "medium", ["P3", "P2"]), "formal": (9, "medium", ["P2"]), "informal": (12, "medium", ["P4", "P3"]), "hesitant": (10, "medium", ["P1"]),
    "indirect": (10, "medium", ["P1", "P2"]), "verbose": (8, "long", ["P3", "P1"]), "nonnative": (10, "medium", ["P6"]), "selfcorrect": (9, "medium", ["P2", "P1"]),
}
COLLEAGUE_WORDS = re.compile(r"\b(nurse|porter|registrar|sister|consultant|charge nurse|anaesthetist|healthcare assistant|surgeon|coordinator|on-call|colleague)\b")
CRITICAL_WORDS = re.compile(r"allerg|identity|consent|latex|wristband|handover|sign\b|signature")
HEDGEABLE = {"EXPLAIN_PROCEDURE", "EXPLAIN_TEST", "ANSWER_QUESTION", "REASSURE", "ACKNOWLEDGE_FACT"}
WEIGHTED_FORMS = {  # the forms an act can be performed in, with weights (statement/question/request)
    "ASK_TOPIC": {"question": 6, "request_or_instruction": 4}, "CLARIFY": {"question": 6, "request_or_instruction": 4}, "CHECK_UNDERSTANDING": {"question": 5, "request_or_instruction": 5},
    "CLOSED_LOOP": {"statement": 7, "question": 3}, "ESCALATE_SPEECH": {"request_or_instruction": 4, "question": 3, "statement": 3}, "VOICE_ORDER": {"request_or_instruction": 5, "question": 3, "statement": 2},
    "CONFIRM_IDENTITY": {"question": 5, "request_or_instruction": 3, "statement": 2}, "STATE_ALLERGY": {"question": 6, "statement": 4}, "CONSENT_SIGN": {"question": 5, "request_or_instruction": 3, "statement": 2},
}


def split_seed(text: str):
    if text.startswith("P: ") and " || " in text:
        prior, content = text[3:].split(" || ", 1)
        return prior, content
    return "", text


def prior_speaker(prior: str) -> str:
    if prior.startswith("(nurse)") or prior.startswith("(registrar)"):
        return "colleague"
    return "patient" if prior else ""


def recipient_for(act: str, content: str) -> str:
    if act in ("CLOSED_LOOP", "VOICE_ORDER", "REPORT_FAULT", "HANDOVER_CRITICAL_ELEMENT", "SBAR_ELEMENT"):
        return "colleague"
    if act == "ESCALATE_SPEECH":
        return "senior_or_colleague"
    if act in ("CLARIFY", "ASK_TOPIC") and COLLEAGUE_WORDS.search(content):
        return "colleague"
    return "patient"


def pick_form(act: str, content: str, situation: str, rng: random.Random) -> str:
    if situation != "performed":
        return "statement"
    first = content.split()[0].lower() if content.split() else ""
    if act in WEIGHTED_FORMS and first in ("ask", "offer", "repeat", "read"):
        opts = dict(WEIGHTED_FORMS[act])
        if first in ("repeat", "read"):
            opts = {"statement": 7, "question": 3}
        elif act in ("ESCALATE_SPEECH", "VOICE_ORDER"):
            opts = {"request_or_instruction": 5, "question": 3, "statement": 2}
        else:
            opts.pop("statement", None)
        names, w = zip(*opts.items())
        return rng.choices(names, w)[0]
    if act in ("ESCALATE_SPEECH", "VOICE_ORDER") and first not in ("ask",):
        return "statement"
    return "statement"


def window_label(act: str, step) -> str:
    return "n/a" if act not in G.WINDOW else ("in_window" if step in G.WINDOW[act] else "out_of_window")


class Builder:
    def __init__(self):
        self.items: list[dict] = []
        self.rng = random.Random(SEED)

    def add(self, **kw) -> dict:
        item = {"id": "", "speaker": "trainee", "acts": None, "form": "statement", "recipient": "patient", "step": None, "window": "n/a", "patient_question_pending": False, "readback_pending": False,
                "prior_turn": "", "prior_speaker": "", "seed": "", "core_id": "", "set_id": "", "set_role": "", "critical_topic": False, "hedge": False, "tags": []}
        item.update(kw)
        item["id"] = "SD1-%04d" % (len(self.items) + 1)
        self.items.append(item)
        return item

    def step_in(self, act):
        return self.rng.choice(sorted(G.WINDOW[act]))

    def step_out(self, act):
        return self.rng.choice([s for s in G.ALL_STEPS if s not in G.WINDOW[act]])


def flags_for(act, pending=True):
    return {"patient_question_pending": pending and act == "ANSWER_QUESTION", "readback_pending": pending and act == "CLOSED_LOOP"}


def build() -> list[dict]:
    B = Builder()
    # ---- ordinary acts: two contrast sets each (A performed + C five non-performed), then standalone A and B
    for act, d in S.ORDINARY.items():
        n_a = 11 if act in ("ASK_TOPIC", "CLARIFY", "EXPLAIN_PROCEDURE", "EXPLAIN_TEST", "EXPLAIN_ACTION", "CHECK_UNDERSTANDING", "ACKNOWLEDGE_EMOTION", "REASSURE", "ESCALATE_SPEECH", "VOICE_ORDER") else 10
        n_oow = 3 if act == "ASK_TOPIC" else 2
        n_state = 8 if act in G.NEEDS_STATE else 0
        for k, core in enumerate(d["cores"]):
            prior, content = split_seed(core)
            sid = "SET-%s-%d" % (act, k)
            step = B.step_in(act)
            form_rng = random.Random("%s-%s" % (SEED, sid))
            for sit in ["performed"] + G.NOT_PERFORMED:
                B.add(cls="A" if sit == "performed" else "C", objective=act, situation=sit, form=pick_form(act, content, sit, form_rng), recipient=recipient_for(act, content), step=step,
                      window="in_window", prior_turn=prior, prior_speaker=prior_speaker(prior), seed=content, core_id=sid, set_id=sid, set_role=sit, **flags_for(act))
        seeds = list(d["seeds"])
        a_seeds, rest = seeds[:n_a - 2], seeds[n_a - 2:]
        assert len(rest) == n_oow + n_state, (act, len(rest), n_oow, n_state)
        for text in a_seeds:
            prior, content = split_seed(text)
            B.add(cls="A", objective=act, situation="performed", form=pick_form(act, content, "performed", B.rng), recipient=recipient_for(act, content), step=B.step_in(act), window="in_window",
                  prior_turn=prior, prior_speaker=prior_speaker(prior), seed=content, **flags_for(act))
        for text in rest[:n_oow]:
            prior, content = split_seed(text)
            st = B.step_out(act)
            B.add(cls="B", objective=act, situation="performed", form=pick_form(act, content, "performed", B.rng), recipient=recipient_for(act, content), step=st, window="out_of_window",
                  prior_turn=prior, prior_speaker=prior_speaker(prior), seed=content, tags=["out_of_window"], **flags_for(act))
        for text in rest[n_oow:]:
            prior, content = split_seed(text)
            B.add(cls="B", objective=act, situation="performed", form=pick_form(act, content, "performed", B.rng), recipient=recipient_for(act, content), step=B.step_in(act), window="in_window",
                  prior_turn=prior, prior_speaker=prior_speaker(prior), seed=content, tags=["state_absent"], **flags_for(act, pending=False))
    # ---- critical acts: one set each (D genuine + E five non-performed), then ten standalone D each
    for act, d in S.CRITICAL.items():
        sid = "SET-%s-0" % act
        step = None if act not in G.WINDOW else B.step_in(act)
        form_rng = random.Random("%s-%s" % (SEED, sid))
        for sit in ["performed"] + G.NOT_PERFORMED:
            B.add(cls="D" if sit == "performed" else "E", objective=act, situation=sit, form=pick_form(act, d["core"], sit, form_rng), recipient=d["recipient"], step=step, window=window_label(act, step),
                  seed=d["core"], core_id=sid, set_id=sid, set_role=sit, critical_topic=True, tags=["critical"])
        for text in d["seeds"]:
            st = None if act not in G.WINDOW else B.step_in(act)
            B.add(cls="D", objective=act, situation="performed", form=pick_form(act, text, "performed", B.rng), recipient=d["recipient"], step=st, window=window_label(act, st), seed=text, critical_topic=True, tags=["critical"])
    for text in S.CRITICAL_DISCUSSION:
        B.add(cls="E", objective="NONE", situation="critical_topic_discussion", form="statement", recipient="patient", step=B.rng.choice(G.ALL_STEPS), seed=text, critical_topic=True, tags=["critical"])
    # ---- no-act items
    for sub, lst in S.NONE_SEEDS.items():
        for text in lst:
            sit = {"filler_or_noise_words": "filler_or_noise_words", "near_miss": "near_miss"}.get(sub, sub)
            tags = ["no_act"] + (["critical_word"] if sub == "near_miss" and CRITICAL_WORDS.search(text) else [])
            B.add(cls="F", objective="NONE", situation=sit, form="statement" if sub != "filler_or_noise_words" else "noise", recipient="patient", step=B.rng.choice(G.ALL_STEPS), seed=text,
                  critical_topic="critical_word" in tags, tags=tags)
    # ---- multi-intent
    for (a1, a2), text in S.MULTI_PERFORMED:
        inter = sorted(G.WINDOW[a1] & G.WINDOW[a2])
        assert inter, (a1, a2)
        prior, content = split_seed(text)
        fl = {"patient_question_pending": "ANSWER_QUESTION" in (a1, a2), "readback_pending": "CLOSED_LOOP" in (a1, a2)}
        B.add(cls="G", objective=a1, acts=[{"act": a1, "situation": "performed"}, {"act": a2, "situation": "performed"}], situation="performed", form="multi_clause", recipient=recipient_for(a1, content),
              step=B.rng.choice(inter), window="in_window", prior_turn=prior, prior_speaker=prior_speaker(prior), seed=content, tags=["multi_intent"], **fl)
    for (a1, sit2, a2), text in S.MULTI_MIXED:
        inter = sorted(G.WINDOW[a1])
        B.add(cls="G", objective=a1, acts=[{"act": a1, "situation": "performed"}, {"act": a2, "situation": sit2}], situation="performed", form="multi_clause", recipient=recipient_for(a1, text),
              step=B.rng.choice(inter), window="in_window", seed=text, tags=["multi_intent", "mixed_performed"], patient_question_pending=False, readback_pending=False)
    items = B.items
    assign_style(items, B.rng)
    for it in items:
        it["tags"] = sorted(set(it["tags"] + tags_of(it)))
        it["gold"] = G.derive(it)
    return items


def tags_of(it: dict) -> list[str]:
    t = []
    if it["set_id"]:
        t.append("contrast_set")
    if it["prior_turn"]:
        t.append("context_reference")
    if it["hedge"]:
        t.append("hedged_content")
    if it["situation"] in G.NOT_PERFORMED:
        t.append("non_performed")
    if it["objective"] in G.CRITICAL_ACTS or it["critical_topic"]:
        t.append("safety_sensitive")
    if it["form"] == "question":
        t.append("question")
    if it["objective"] in ("ESCALATE_SPEECH", "SBAR_ELEMENT", "HANDOVER_CRITICAL_ELEMENT", "VOICE_ORDER"):
        t.append("escalation_handover")
    if it["objective"] in ("ACKNOWLEDGE_EMOTION", "REASSURE"):
        t.append("emotion_reassurance")
    if it["objective"] in ("EXPLAIN_ACTION", "EXPLAIN_PROCEDURE", "EXPLAIN_TEST"):
        t.append("explain_family")
    if it["cls"] == "G":
        t.append("multi_intent")
    return t


def assign_style(items: list[dict], rng: random.Random) -> None:
    """one style and persona per unit (a contrast set shares its style so the only difference inside it is the marked meaning)."""
    units = []
    seen = set()
    for it in items:
        key = it["set_id"] or it["id"]
        if key not in seen:
            seen.add(key)
            units.append((key, it))
    n = len(units)
    total = sum(w for w, _, _ in STYLES.values())
    pool = []
    for name, (w, _, _) in STYLES.items():
        pool += [name] * round(n * w / total)
    while len(pool) < n:
        pool.append("conversational")
    pool = pool[:n]
    rng.shuffle(pool)
    legal = lambda style, it: not (style == "selfcorrect" and (it["set_id"] or it["situation"] != "performed" or it["cls"] in ("F", "G")))
    for i, (key, it) in enumerate(units):
        if not legal(pool[i], it):
            cands = [j for j in range(n) if j != i and legal(pool[i], units[j][1]) and legal(pool[j], it)]
            j = rng.choice(cands)
            pool[i], pool[j] = pool[j], pool[i]
    chosen = {key: pool[i] for i, (key, _) in enumerate(units)}
    pc = collections.Counter()
    for it in items:
        style = chosen[it["set_id"] or it["id"]]
        _, length, personas = STYLES[style]
        pc[style] += 1
        persona = personas[pc[style] % len(personas)]
        it["style"], it["length"], it["persona"] = style, length, persona
        if it["objective"] == "NONE" and it["situation"] == "filler_or_noise_words":
            it["length"] = "short"
        it["hedge"] = bool(it["situation"] == "performed" and it["objective"] in HEDGEABLE and not it["set_id"] and it["cls"] in ("A", "B") and rng.random() < (0.8 if style in ("hesitant", "indirect", "nonnative") else 0.3))
        it["topic_pool"] = "dev"


def coverage(items: list[dict]) -> dict:
    c = collections.Counter
    return {"n": len(items), "by_class": dict(c(i["cls"] for i in items)), "by_objective": dict(c(i["objective"] for i in items)), "by_situation": dict(c(i["situation"] for i in items)),
            "credit_eligible": sum(i["gold"]["credit_eligible"] for i in items), "no_credit": sum(not i["gold"]["credit_eligible"] for i in items),
            "safety_sensitive": sum("safety_sensitive" in i["tags"] or "critical_word" in i["tags"] for i in items), "contrast_sets": len({i["set_id"] for i in items if i["set_id"]}),
            "contrast_items": sum(1 for i in items if i["set_id"]), "by_style": dict(c(i["style"] for i in items)), "by_persona": dict(c(i["persona"] for i in items)), "by_form": dict(c(i["form"] for i in items)),
            "hedged": sum(i["hedge"] for i in items), "context_reference": sum(1 for i in items if i["prior_turn"]), "out_of_window": sum(1 for i in items if i["window"] == "out_of_window"),
            "state_absent": sum(1 for i in items if "state_absent" in i["tags"])}


def freeze(items: list[dict]) -> dict:
    spec_bytes = (json.dumps(items, indent=1, ensure_ascii=False) + "\n").encode("utf-8")
    h = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    here = Path(__file__).resolve().parent
    return {"set": "synthetic_dev_v1", "kind": "DEVELOPMENT data: AI-written and AI-reviewed (the same model), synthetic; not independent, not human-labelled, not real-world validation",
            "frozen_before_utterances": True, "seed": SEED, "reserved_personas": RESERVED_PERSONAS, "reserved_topic_pools": ["eval"], "dev_topic_pool": "dev", "n": len(items), "specs_sha256": hashlib.sha256(spec_bytes).hexdigest(),
            "plan_sources_sha256": {n: h(here / n) for n in ("seeds.py", "gold.py", "plan.py")}, "sources_of_truth": ["content/assessment/taxonomy.json", "content/assessment/mapping.json", "tests/classifier_synthetic_v2/rubric.md"],
            "coverage": coverage(items)}, spec_bytes


def main(argv):
    items = build()
    manifest, spec_bytes = freeze(items)
    text = json.dumps(manifest, indent=1, ensure_ascii=False) + "\n"
    if "--write" in argv:
        OUT.mkdir(parents=True, exist_ok=True)
        (OUT / "specs.json").write_bytes(spec_bytes)
        (OUT / "manifest.json").write_text(text, encoding="utf-8", newline="\n")
        print("wrote", len(items), "specifications")
    elif "--check" in argv:
        ok = (OUT / "specs.json").read_bytes() == spec_bytes
        print("OK specs match the plan" if ok else "FAIL specs differ from the plan")
        return 0 if ok else 1
    else:
        print(json.dumps(manifest["coverage"], indent=1))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
