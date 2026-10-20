"""Scenarios for the safety mutation tests (p1-ai-language-s16, ADR-0023): `python tools/ci/safety_scenarios.py --impl DIR [--cli-project DIR/src/ProcSense.Cli] [--no-engine]` prints one JSON object, scenario id -> outcome.

A scenario puts ONE guard in the position where it is the barrier the others are not: a stub classifier that credits at full confidence stands in for the gate when the scenario is about a guard behind it, the context is the most permissive one that still contains the guard's case,
and the outcome recorded is the FINAL one: the decision of the interpretation path (OFFER_EVIDENCE or not, which act, the reasons) AND what the engine's own EvidenceStore does with an offer (`evidence-check`: accepted, or the reason it refused). `layer` scenarios call one guard directly,
so a guard that a second layer would mask at system level is still tested on its own. tools/ci/mutation_safety.py runs this against an unmutated copy and against copies with one guard disabled; a mutant is killed when an outcome changes.
Needs the engine CLI built for the engine part (`dotnet build src/ProcSense.Cli -c Release`); standard library plus the numpy of the tools environment. Nothing here is a model call: the real gate runs on the embeddings stored in tests/classifier_regression/*.json.
"""
from __future__ import annotations

import argparse
import copy
import json
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

SELF = Path(__file__).resolve()
ROOT = SELF.parents[2]


@dataclass
class Entry:
    act: str
    behaviour: str
    critical: bool = False


@dataclass
class Context:
    steps: list
    eligible: list
    accepted: list = field(default_factory=list)
    patient_question_pending: bool = False
    readback_pending: bool = False


def main_run(impl: Path, cli_project: Path | None, with_engine: bool) -> dict:
    sys.path.insert(0, str(impl / "server"))
    from classify import assertion
    from classify.classifier import Result
    from classify.ensemble import EnsembleActClassifier
    from classify.guard import critical_cue
    from interpret.decide import CLARIFY_LIMIT, Interpreter, safe_interpret
    from interpret.status import status_of
    from interpret.validate import validate

    mapping = json.loads((impl / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
    critical = set(mapping["critical_acts"])
    all_steps = list(range(1, 16))

    def window(act):
        return sorted({s for b in mapping["behaviours"] if act in b["ai_acts"] for s in b["steps"]})

    def context(steps, pending=False, readback=False, accepted=()):
        eligible = [Entry(a, b["id"], a in critical) for b in mapping["behaviours"] if set(b["steps"]) & set(steps) for a in b["ai_acts"]]
        return Context(list(steps), eligible, list(accepted), pending, readback)

    def behaviour_of(act):
        return next(b["id"] for b in mapping["behaviours"] if act in b["ai_acts"])

    class Stub:
        """A gate that credits whatever it is told at the stated probability."""
        model = "stub"

        def __init__(self, act, p=0.95, credited=True, mass=0.0, cap=0.05, source="ensemble", refused="", probs=None):
            self.result = Result(act, p, credited, source, act in critical, "", refused)
            self.probs = (probs or {act: p}) if source != "unavailable" else {}
            outer = mass

            class Emb:
                head = {"critical_mass_max": cap}

                @staticmethod
                def critical_mass(probs):
                    return outer
            self.embedding = Emb

        def classify_with_probs(self, sentence):
            return self.result, self.probs

    class Boom:
        model = "boom"

        def classify_with_probs(self, sentence):
            raise RuntimeError("the embedder fell over")

    out: dict[str, dict] = {}
    offers: list[dict] = []

    def system(sid, interp, ctx, text, uid, used=0, steps=None, group="", utterance=""):
        try:
            o = interp.interpret(ctx, text, uid, used)
        except Exception as e:                                   # a crash is an outcome (fail closed in production: safe_interpret turns it into the menu), not the end of the run
            out[sid] = {"kind": "system", "decision": "CRASH", "offer": None, "reasons": [type(e).__name__], "engine": None}
            return
        offer = o["evidence"]["act"] if o["decision"] == "OFFER_EVIDENCE" else None
        out[sid] = {"kind": "system", "decision": o["decision"], "offer": offer, "reasons": o["reasons"][:3], "engine": None}
        if offer:
            offers.append({"id": sid, "act": offer, "confidence": o["evidence"]["confidence"], "steps": steps if steps is not None else ctx.steps, "group": group, "utterance": utterance or uid})

    # ---- system scenarios (interpretation path under a stub gate, then the engine)
    sid_ctx = {act: (window(act)[:1] or all_steps) for act in ("ASK_TOPIC", "EXPLAIN_PROCEDURE", "ANSWER_QUESTION", "CLOSED_LOOP", "CHECK_UNDERSTANDING")}
    system("S01a_critical_act_from_gate", Interpreter(Stub("CONFIRM_IDENTITY")), context(all_steps, True, True), "Hello there, good morning.", "s01a")
    system("S01b_critical_sbar_act_from_gate", Interpreter(Stub("SBAR_ELEMENT")), context(all_steps, True, True), "Hello there, good morning.", "s01b")
    system("S02_critical_cue_with_ordinary_act", Interpreter(Stub("ASK_TOPIC")), context(sid_ctx["ASK_TOPIC"]), "Do you have any allergies to medicines?", "s02")
    system("S03_negated_sentence", Interpreter(Stub("ASK_TOPIC")), context(sid_ctx["ASK_TOPIC"]), "I did not ask about your sleep.", "s03")
    system("S04_statement_act_asked_as_question", Interpreter(Stub("EXPLAIN_PROCEDURE")), context(sid_ctx["EXPLAIN_PROCEDURE"]), "Will you explain the operation to me?", "s04")
    outside = [s for s in all_steps if s not in window("ASK_TOPIC")]
    elsewhere = next(s for s in outside if context([s]).eligible)                 # a running step where OTHER acts are eligible, so only the act's own window refuses it
    system("S05_outside_the_behaviour_window", Interpreter(Stub("ASK_TOPIC")), context([elsewhere]), "How have you been sleeping lately?", "s05", steps=[elsewhere])
    system("S06_answer_without_a_waiting_question", Interpreter(Stub("ANSWER_QUESTION")), context(sid_ctx["ANSWER_QUESTION"], pending=False), "You can go home this evening.", "s06")
    system("S07_readback_without_a_waiting_result", Interpreter(Stub("CLOSED_LOOP")), context(sid_ctx["CLOSED_LOOP"], readback=False), "Understood, I will tell you when the bloods have gone.", "s07")
    system("S08_behaviour_already_credited", Interpreter(Stub("ASK_TOPIC")), context(sid_ctx["ASK_TOPIC"], accepted=[behaviour_of("ASK_TOPIC")]), "How have you been sleeping lately?", "s08")
    system("S09_gate_did_not_credit", Interpreter(Stub("ASK_TOPIC", credited=False)), context(sid_ctx["ASK_TOPIC"]), "How have you been sleeping lately?", "s09")
    system("S10_critical_mass_over_the_cap", Interpreter(Stub("ASK_TOPIC", mass=0.2)), context(sid_ctx["ASK_TOPIC"]), "How have you been sleeping lately?", "s10")
    system("S11_classifier_unavailable", Interpreter(Stub("ASK_TOPIC", source="unavailable")), context(sid_ctx["ASK_TOPIC"]), "How have you been sleeping lately?", "s11")
    step_two = next(s for s in all_steps if len({e.act for e in context([s]).eligible if e.act not in critical}) >= 2)       # a step where two ordinary acts are eligible, so an ambiguity can be asked about
    a1, a2 = sorted({e.act for e in context([step_two]).eligible if e.act not in critical})[:2]
    two = {a1: 0.5, a2: 0.3}
    system("S12a_first_clarification", Interpreter(Stub(a1, p=0.5, credited=False, probs=two)), context([step_two]), "Tell me about the evening meal.", "s12a", used=0)
    system("S12b_second_clarification", Interpreter(Stub(a1, p=0.5, credited=False, probs=two)), context([step_two]), "Tell me about the evening meal.", "s12b", used=CLARIFY_LIMIT)
    system("S13_positive_control", Interpreter(Stub("ASK_TOPIC")), context(sid_ctx["ASK_TOPIC"]), "How have you been sleeping lately?", "s13")
    # a session: the same behaviour twice, the same utterance id twice (the service is told nothing about the first, so only the engine can refuse the second)
    system("S14a_first_of_a_pair", Interpreter(Stub("ASK_TOPIC")), context(sid_ctx["ASK_TOPIC"]), "How have you been sleeping lately?", "s14a", group="pair", utterance="u-a")
    system("S14b_same_behaviour_again_stale_context", Interpreter(Stub("ASK_TOPIC")), context(sid_ctx["ASK_TOPIC"]), "Do you sleep well at night?", "s14b", group="pair", utterance="u-b")
    system("S15a_first_of_a_resend", Interpreter(Stub("CHECK_UNDERSTANDING")), context(sid_ctx["CHECK_UNDERSTANDING"]), "Does that explanation make sense to you?", "s15a", group="resend", utterance="u-same")
    system("S15b_same_utterance_id_again_stale_context", Interpreter(Stub("CHECK_UNDERSTANDING")), context(sid_ctx["CHECK_UNDERSTANDING"]), "Does that explanation make sense to you?", "s15b", group="resend", utterance="u-same")
    # a failing classifier: the menu, no evidence; a cancellation is not swallowed
    try:
        o, failed = safe_interpret(Interpreter(Boom()), context(sid_ctx["ASK_TOPIC"]), "How have you been sleeping lately?", "s16")
        out["S16_classifier_raises"] = {"kind": "system", "decision": o["decision"], "offer": None, "reasons": o["reasons"], "failed": failed, "engine": None}
    except Exception as e:
        out["S16_classifier_raises"] = {"kind": "system", "decision": "CRASH", "offer": None, "reasons": [type(e).__name__], "failed": False, "engine": None}
    try:
        class Cancel:
            model = "cancel"

            def classify_with_probs(self, sentence):
                raise KeyboardInterrupt()
        safe_interpret(Interpreter(Cancel()), context(sid_ctx["ASK_TOPIC"]), "x", "s17")
        out["S17_cancellation"] = {"kind": "system", "decision": "RETURNED", "offer": None, "reasons": [], "engine": None}
    except KeyboardInterrupt:
        out["S17_cancellation"] = {"kind": "system", "decision": "PROPAGATED", "offer": None, "reasons": [], "engine": None}

    # ---- the real gate (hybrid head, TF-IDF agreement, cap, cues, assertion layer) on stored embeddings, then the interpreter
    rc = json.loads((ROOT / "tests" / "classifier_regression" / "recall_cases.json").read_text(encoding="utf-8"))["rows"]
    rg = json.loads((ROOT / "tests" / "classifier_regression" / "cases.json").read_text(encoding="utf-8"))["cases"]
    look = {r["text"]: r["embedding"] for r in rc}
    look.update({c["text"]: c["embedding"] for c in rg})

    def real():
        return EnsembleActClassifier(lambda t: look.get(t))

    cap_rows = [r for r in rc if r["group"] == "cap_blocked_unsafe"]
    counter = [r for r in rc if r["group"] in ("counter_denied", "counter_intended", "counter_hypothetical", "counter_reported", "counter_other_actor", "counter_incomplete")]
    crit_rows = [c for c in rg if c["group"] == "must_not_credit_critical"]

    def real_group(sid, rows, ctx_of, gate_view=None):
        interp = Interpreter(real())
        offered_n, credited_n, rows_out = 0, 0, []
        for r in rows:
            c = ctx_of(r)
            o = interp.interpret(c, r["text"], r.get("id", "x"), 0)
            offer = o["evidence"]["act"] if o["decision"] == "OFFER_EVIDENCE" else None
            rows_out.append(offer)
            if offer:
                offered_n += 1
                offers.append({"id": f"{sid}|{r['text'][:40]}", "act": offer, "confidence": o["evidence"]["confidence"], "steps": c.steps, "group": "", "utterance": ""})
        out[sid] = {"kind": "real_gate", "rows": len(rows), "offered": offered_n, "engine_accepted": None, "tag": sid}

    def own_ctx(r):
        act = r.get("act") or "ASK_TOPIC"
        w = window(act)
        return context(w[:1], pending=act == "ANSWER_QUESTION", readback=act == "CLOSED_LOOP") if w else context(all_steps, True, True)

    real_group("G01_cap_blocked_unsafe_real_gate", cap_rows, lambda r: context(all_steps, True, True))
    real_group("G02_non_performed_real_gate", counter, own_ctx)
    real_group("G03_critical_sentences_real_gate", crit_rows, lambda r: context(all_steps, True, True))

    # ---- layer scenarios (one guard called directly)
    asked = context(sid_ctx["ASK_TOPIC"])
    layer = {
        "L01_validate_refuses_a_critical_act": validate("SBAR_ELEMENT", context(all_steps), "x", "actual", True),
        "L02_validate_refuses_a_denied_sentence": validate("ASK_TOPIC", asked, "x", "denied", True),
        "L03_validate_refuses_a_question_for_a_statement_act": validate("EXPLAIN_PROCEDURE", context(sid_ctx["EXPLAIN_PROCEDURE"]), "x", "question", True),
        "L04_validate_refuses_an_ineligible_act": validate("ASK_TOPIC", context([elsewhere]), "x", "actual", True),
        "L05_validate_needs_a_waiting_question": validate("ANSWER_QUESTION", context(sid_ctx["ANSWER_QUESTION"]), "x", "actual", True),
        "L06_validate_needs_a_waiting_readback": validate("CLOSED_LOOP", context(sid_ctx["CLOSED_LOOP"]), "x", "actual", True),
        "L07_validate_refuses_a_credited_behaviour": validate("ASK_TOPIC", context(sid_ctx["ASK_TOPIC"], accepted=[behaviour_of("ASK_TOPIC")]), "x", "actual", True),
        "L08_validate_needs_the_gate": validate("ASK_TOPIC", asked, "x", "actual", False),
        "L09_status_of_names_a_denial": status_of("I did not ask about your sleep."),
        "L10_status_of_names_a_hypothetical": status_of("If I were to explain the operation, what would that change?"),
        "L11_status_of_names_a_question": status_of("Do you sleep well at night?"),
        "L12_assertion_layer_names_other_kinds": [assertion.non_assertive(s) for s in ("The nurse asked what you meant by strange.", "Maybe I noted where you lived.", "I will explain it later.", "Should I ask about your appetite?", 'The tutor said, "Does that make sense?"', "I asked about your appetite, no wait, I didn't.", "Thank you.")],
        "L13_critical_cue": [critical_cue(s) for s in ("Do you have any allergies to medicines?", "Let me check your wristband.", "Are you happy to sign the consent form?")],
        "L14_gate_cap_blocks_the_cap_sentences": [real().classify_with_probs(r["text"])[0].credited for r in cap_rows],
        "L15_gate_assertion_blocks_non_performed": [real().classify_with_probs(r["text"])[0].credited for r in counter],
        "L16_gate_critical_sentences_not_credited": [real().classify_with_probs(c["text"])[0].credited for c in crit_rows],
    }
    for k, v in layer.items():
        out[k] = {"kind": "layer", "value": v}
    # the TF-IDF agreement and the cue, each with the other layers unable to help: the embedding side is fixed to a sentence that is a plain, credited REASSURE
    ask_row = next(r for r in rc if r["group"] == "legit_paraphrase" and r["act"] == "REASSURE")
    fixed = EnsembleActClassifier(lambda text: ask_row["embedding"])
    fixed_ok = fixed.classify_with_probs(ask_row["text"])[0]

    class Disagree:
        @staticmethod
        def classify(sentence):
            return Result("NONE", 0.9, False, "tfidf")
    dis = EnsembleActClassifier(lambda text: ask_row["embedding"])
    dis.tfidf = Disagree()
    out["L17_gate_control_credits_a_plain_sentence"] = {"kind": "layer", "value": bool(fixed_ok.credited)}
    out["L18_gate_needs_the_tfidf_head_to_agree"] = {"kind": "layer", "value": bool(dis.classify_with_probs(ask_row["text"])[0].credited)}

    class Agree:                                             # the TF-IDF reader agrees, so only the cue can stop the sentence
        @staticmethod
        def classify(sentence):
            return Result(ask_row["act"], 0.9, True, "tfidf")
    fixed.tfidf = Agree()
    base_probs = fixed.embedding.probabilities_of(ask_row["embedding"], ask_row["text"])
    fixed.embedding.probabilities_of = lambda vec, sentence: dict(base_probs)         # the head reads this sentence as the plain one whatever the words, so neither the cap nor the other act can help the cue
    out["L19_gate_cue_on_an_ordinary_looking_embedding"] = {"kind": "layer", "value": [bool(fixed.classify_with_probs(s)[0].credited) for s in ("Do you have any allergies to penicillin?", "Let me check your wristband against your notes.", "Are you happy to sign the consent form?")]}

    # ---- the engine's own EvidenceStore on direct offers (what the engine refuses whatever the service said)
    direct = [
        ("E01_critical_act_offered", {"act": "SBAR_ELEMENT", "confidence": 0.99, "steps": all_steps}),
        ("E02_unknown_act_offered", {"act": "MAKE_UP_AN_ACT", "confidence": 0.99, "steps": all_steps}),
        ("E03_below_the_act_threshold", {"act": "ASK_TOPIC", "confidence": 0.01, "steps": sid_ctx["ASK_TOPIC"]}),
        ("E04_no_running_step", {"act": "ASK_TOPIC", "confidence": 0.99, "steps": []}),
        ("E05_outside_the_window", {"act": "ASK_TOPIC", "confidence": 0.99, "steps": outside[:1]}),
        ("E06a_first_in_a_session", {"act": "ASK_TOPIC", "confidence": 0.99, "steps": sid_ctx["ASK_TOPIC"], "group": "e06", "utterance": "a"}),
        ("E06b_same_behaviour_again", {"act": "ASK_TOPIC", "confidence": 0.99, "steps": sid_ctx["ASK_TOPIC"], "group": "e06", "utterance": "b"}),
        ("E07a_first_utterance", {"act": "CHECK_UNDERSTANDING", "confidence": 0.99, "steps": sid_ctx["CHECK_UNDERSTANDING"], "group": "e07", "utterance": "same"}),
        ("E07b_same_utterance_id_again", {"act": "CHECK_UNDERSTANDING", "confidence": 0.99, "steps": sid_ctx["CHECK_UNDERSTANDING"], "group": "e07", "utterance": "same"}),
        ("E07c_first_for_another_behaviour", {"act": "REASSURE", "confidence": 0.99, "steps": window("REASSURE")[:1], "group": "e07c", "utterance": "shared"}),
        ("E07d_same_utterance_id_for_another_behaviour", {"act": "ASK_TOPIC", "confidence": 0.99, "steps": window("ASK_TOPIC")[:1], "group": "e07c", "utterance": "shared"}),
        ("E08_positive_control", {"act": "REASSURE", "confidence": 0.99, "steps": window("REASSURE")[:1]}),
    ]
    for sid, o in direct:
        out[sid] = {"kind": "engine", "engine": None}
        offers.append({"id": sid, "group": "", "utterance": "", **o})
    crit_acts = sorted(critical)
    for a in crit_acts:
        out[f"E09_{a}"] = {"kind": "engine", "engine": None}
        offers.append({"id": f"E09_{a}", "act": a, "confidence": 1.0, "steps": all_steps, "group": "", "utterance": ""})

    if with_engine:
        verdicts = engine(offers, cli_project, ROOT / "content")          # the engine reads the whole content package; the mutants never change content
        for v in verdicts:
            sid = v["id"]
            tag = sid.split("|")[0]
            res = "accepted" if v["accepted"] else v["reason"].split(":")[0]
            if sid in out:
                out[sid]["engine"] = res
            else:                                              # a row of a real-gate group
                out[tag]["engine_accepted"] = (out[tag]["engine_accepted"] or 0) + (1 if v["accepted"] else 0)
        for k, v in out.items():
            if v.get("kind") == "real_gate":
                v["engine_accepted"] = v["engine_accepted"] or 0
    return out


def engine(offers: list[dict], cli_project: Path | None, content: Path) -> list[dict]:
    proj = cli_project or ROOT / "src" / "ProcSense.Cli"
    with tempfile.TemporaryDirectory() as t:
        f, o = Path(t) / "offers.json", Path(t) / "verdicts.json"
        f.write_text(json.dumps(offers), encoding="utf-8")
        p = subprocess.run(["dotnet", "run", "--no-build", "-c", "Release", "--project", str(proj), "--", "evidence-check", "--offers", str(f), "--out", str(o), "--content", str(content)], capture_output=True, text=True, encoding="utf-8", cwd=str(proj.parents[1]))
        if p.returncode != 0:
            raise RuntimeError("evidence-check failed: " + (p.stderr or p.stdout)[:500])
        return json.loads(o.read_text(encoding="utf-8"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--impl", default=str(ROOT))
    ap.add_argument("--cli-project", default="")
    ap.add_argument("--no-engine", action="store_true")
    a = ap.parse_args()
    res = main_run(Path(a.impl).resolve(), Path(a.cli_project).resolve() if a.cli_project else None, not a.no_engine)
    sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps(res, ensure_ascii=False, sort_keys=True))
