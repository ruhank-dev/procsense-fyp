"""Builds and freezes the specifications of the FRESH HELD-OUT set HO1 for the final acceptance of the speech evaluator (ADR-0025).

Same rules as tools/synth_dev/plan.py (ground truth from the approved rules only, specifications frozen with a SHA-256 manifest before any sentence exists), but with new seeds (tools/synth_heldout/seeds.py), the reserved personas P5 and P7, four reserved topic
groups and new semantic combinations. 259 items. Usage: uv run --project tools tools/synth_heldout/plan.py [--write] [--check]"""
from __future__ import annotations

import hashlib
import json
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
import importlib.util  # noqa: E402

DEV = HERE.parent / "synth_dev"
sys.path.insert(0, str(DEV))
import gold as G  # noqa: E402


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


DP = _load('dev_plan', DEV / 'plan.py')  # the development plan: its helpers only, none of its seeds
HS = _load('heldout_seeds', HERE / 'seeds.py')

OUT = G.ROOT / "tests" / "synthetic_heldout_v1"
SEED = 20261201
DP.STYLES = {  # style -> (weight, length, personas): only the reserved personas
    "formal": (22, "medium", ["P5"]), "methodical": (22, "medium", ["P7"]), "concise": (14, "short", ["P5", "P7"]), "indirect": (14, "medium", ["P7"]), "conversational": (14, "medium", ["P5", "P7"]),
    "hesitant": (8, "medium", ["P7"]), "verbose": (6, "long", ["P5"]),
}
DP.PERSONAS = {"P5": "a formal, by-the-book student: complete sentences, exact terms", "P7": "a calm, methodical student: unhurried, step by step"}


class Builder(DP.Builder):
    def __init__(self):
        super().__init__()
        self.rng = random.Random(SEED)

    def add(self, **kw):
        item = super().add(**kw)
        item["id"] = "HO1-%04d" % len(self.items)
        return item


def build() -> list[dict]:
    B = Builder()
    for act, d in HS.ORDINARY.items():
        n_oow, n_state = 1, (2 if act in G.NEEDS_STATE else 0)
        for k, core in enumerate(d["cores"]):
            prior, content = DP.split_seed(core)
            sid = "SET-%s-%d" % (act, k)
            step = B.step_in(act)
            form_rng = random.Random("%s-%s" % (SEED, sid))
            for sit in ["performed"] + G.NOT_PERFORMED:
                B.add(cls="A" if sit == "performed" else "C", objective=act, situation=sit, form=DP.pick_form(act, content, sit, form_rng), recipient=DP.recipient_for(act, content), step=step,
                      window="in_window", prior_turn=prior, prior_speaker=DP.prior_speaker(prior), seed=content, core_id=sid, set_id=sid, set_role=sit, **DP.flags_for(act))
        seeds = list(d["seeds"])
        a_seeds, rest = seeds[:5], seeds[5:]
        assert len(rest) == n_oow + n_state, (act, len(rest))
        for text in a_seeds:
            prior, content = DP.split_seed(text)
            B.add(cls="A", objective=act, situation="performed", form=DP.pick_form(act, content, "performed", B.rng), recipient=DP.recipient_for(act, content), step=B.step_in(act), window="in_window",
                  prior_turn=prior, prior_speaker=DP.prior_speaker(prior), seed=content, **DP.flags_for(act))
        for text in rest[:n_oow]:
            prior, content = DP.split_seed(text)
            B.add(cls="B", objective=act, situation="performed", form=DP.pick_form(act, content, "performed", B.rng), recipient=DP.recipient_for(act, content), step=B.step_out(act), window="out_of_window",
                  prior_turn=prior, prior_speaker=DP.prior_speaker(prior), seed=content, tags=["out_of_window"], **DP.flags_for(act))
        for text in rest[n_oow:]:
            prior, content = DP.split_seed(text)
            B.add(cls="B", objective=act, situation="performed", form=DP.pick_form(act, content, "performed", B.rng), recipient=DP.recipient_for(act, content), step=B.step_in(act), window="in_window",
                  prior_turn=prior, prior_speaker=DP.prior_speaker(prior), seed=content, tags=["state_absent"], **DP.flags_for(act, pending=False))
    for act, d in HS.CRITICAL.items():
        sid = "SET-%s-0" % act
        step = None if act not in G.WINDOW else B.step_in(act)
        form_rng = random.Random("%s-%s" % (SEED, sid))
        for sit in ["performed"] + G.NOT_PERFORMED:
            B.add(cls="D" if sit == "performed" else "E", objective=act, situation=sit, form=DP.pick_form(act, d["core"], sit, form_rng), recipient=d["recipient"], step=step, window=DP.window_label(act, step),
                  seed=d["core"], core_id=sid, set_id=sid, set_role=sit, critical_topic=True, tags=["critical"])
        for text in d["seeds"]:
            st = None if act not in G.WINDOW else B.step_in(act)
            B.add(cls="D", objective=act, situation="performed", form=DP.pick_form(act, text, "performed", B.rng), recipient=d["recipient"], step=st, window=DP.window_label(act, st), seed=text, critical_topic=True, tags=["critical"])
    for text in HS.CRITICAL_DISCUSSION:
        B.add(cls="E", objective="NONE", situation="critical_topic_discussion", form="statement", recipient="patient", step=B.rng.choice(G.ALL_STEPS), seed=text, critical_topic=True, tags=["critical"])
    for sub, lst in HS.NONE_SEEDS.items():
        for text in lst:
            tags = ["no_act"] + (["critical_word"] if sub == "near_miss" and DP.CRITICAL_WORDS.search(text) else [])
            B.add(cls="F", objective="NONE", situation=sub, form="statement" if sub != "filler_or_noise_words" else "noise", recipient="patient", step=B.rng.choice(G.ALL_STEPS), seed=text,
                  critical_topic="critical_word" in tags, tags=tags)
    for (a1, a2), text in HS.MULTI_PERFORMED:
        inter = sorted(G.WINDOW[a1] & G.WINDOW[a2])
        assert inter, (a1, a2)
        prior, content = DP.split_seed(text)
        fl = {"patient_question_pending": "ANSWER_QUESTION" in (a1, a2), "readback_pending": "CLOSED_LOOP" in (a1, a2)}
        B.add(cls="G", objective=a1, acts=[{"act": a1, "situation": "performed"}, {"act": a2, "situation": "performed"}], situation="performed", form="multi_clause", recipient=DP.recipient_for(a1, content),
              step=B.rng.choice(inter), window="in_window", prior_turn=prior, prior_speaker=DP.prior_speaker(prior), seed=content, tags=["multi_intent"], **fl)
    for (a1, sit2, a2), text in HS.MULTI_MIXED:
        B.add(cls="G", objective=a1, acts=[{"act": a1, "situation": "performed"}, {"act": a2, "situation": sit2}], situation="performed", form="multi_clause", recipient=DP.recipient_for(a1, text),
              step=B.rng.choice(sorted(G.WINDOW[a1])), window="in_window", seed=text, tags=["multi_intent", "mixed_performed"], patient_question_pending=False, readback_pending=False)
    items = B.items
    DP.assign_style(items, B.rng)
    for it in items:
        it["hedge"] = False                      # hedged performance is an open definition (DT-242): never in authoritative gold
        it["topic_pool"] = "eval"
        it["tags"] = sorted(set(it["tags"] + DP.tags_of(it)))
        it["gold"] = G.derive(it)
    return items


def freeze(items: list[dict]) -> tuple[dict, bytes]:
    spec_bytes = (json.dumps(items, indent=1, ensure_ascii=False) + "\n").encode("utf-8")
    h = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    return {"set": "synthetic_heldout_v1", "kind": "FRESH HELD-OUT acceptance data: AI-written and AI-reviewed (the same assistant), synthetic; not independent, not human-labelled, not real audio, not a headset result",
            "frozen_before_utterances": True, "seed": SEED, "reserved_personas_used": ["P5", "P7"], "topic_groups": ["H1 cataract and eye drops", "H2 dental extraction", "H3 hand surgery under an arm block", "H4 varicose veins and leg compression"],
            "n": len(items), "specs_sha256": hashlib.sha256(spec_bytes).hexdigest(), "plan_sources_sha256": {n: h(HERE / n) for n in ("seeds.py", "plan.py")},
            "development_sources_sha256": {n: h(HERE.parent / "synth_dev" / n) for n in ("gold.py", "plan.py")},
            "sources_of_truth": ["content/assessment/taxonomy.json", "content/assessment/mapping.json", "tests/classifier_synthetic_v2/rubric.md"], "coverage": DP.coverage(items)}, spec_bytes


def main(argv):
    items = build()
    manifest, spec_bytes = freeze(items)
    if "--write" in argv:
        OUT.mkdir(parents=True, exist_ok=True)
        (OUT / "specs.json").write_bytes(spec_bytes)
        (OUT / "manifest.json").write_text(json.dumps(manifest, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
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
