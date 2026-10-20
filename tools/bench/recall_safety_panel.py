"""The safety panel for any change to the credit path (ADR-0022 follow-up): `tools/.venv/Scripts/python.exe tools/bench/recall_safety_panel.py [--write]`.

Every existing set of evasive critical sentences and of non-performed speech, run through the FULL interpretation path under the MOST PERMISSIVE context (every workflow step running, a patient question and a read-back waiting), so that the state cannot hide a leak. A sentence is a LEAK if the path offers it as evidence for any ordinary act.
Sets (all synthetic, builder-written, NOT independent; their status matters, so it is printed with every number):
  trained     adversarial_critical, _holdout, _holdout2 and the critical_evasive rows of challenge v4: the 179 + 60 sentences are TRAINING ROWS of both heads (hard negatives), so a pass on them is memory and is reported apart.
  tuned-on    adversarial_critical_holdout3 and the critical_evasive rows of challenge v5: held out of training, but the critical-mass cap was chosen as the largest value that credits none of them (build_safe_heads.py), so they are development data for the cap.
  final       the critical_evasive rows of challenge v6: held out of training, never used to choose a cap; scored once in the fifth session. The most honest evidence about the cap here, and itself one look.
  non-assertive  the non_assertive rows of challenge v4, v5 and v6.
  v2          the critical-labelled rows of the AI-judged synthetic v2 dev split (cap and thresholds were tuned on it) and test split (scored once before); model-written, labelled by AI judges and the builder.
  bench       the critical and adversarial rows of tests/action_bench_v1 (all three splits).
Embeddings of the sentences come from the local all-minilm:22m through Ollama and are cached in artifacts/ai-bench/interp/emb-panel.json (git-ignored). No language model.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from interpret.decide import Interpreter  # noqa: E402
import interpretation_bench as B  # noqa: E402
import nli_zero_shot as N  # noqa: E402

CACHE = ROOT / "artifacts" / "ai-bench" / "interp" / "emb-panel.json"
T = ROOT / "tests"


def sets() -> dict[str, list[dict]]:
    j = lambda p: json.loads((T / p).read_text(encoding="utf-8"))["rows"]            # noqa: E731
    out = {
        "trained: adversarial_critical + holdout + holdout2": [r for n in ("adversarial_critical", "adversarial_critical_holdout", "adversarial_critical_holdout2") for r in j(f"classifier_synthetic_v2/{n}.json")],
        "trained: challenge v4 critical_evasive": [r for r in j("classifier_challenge_v4/challenge.json") if r["bucket"] == "critical_evasive"],
        "tuned-on: adversarial_critical_holdout3": j("classifier_synthetic_v2/adversarial_critical_holdout3.json"),
        "tuned-on: challenge v5 critical_evasive": [r for r in j("classifier_challenge_v5/challenge.json") if r["bucket"] == "critical_evasive"],
        "final: challenge v6 critical_evasive": [r for r in j("classifier_challenge_v6/challenge.json") if r["bucket"] == "critical_evasive"],
        "non-assertive: challenge v4 + v5 + v6": [r for v in (4, 5, 6) for r in j(f"classifier_challenge_v{v}/challenge.json") if r["bucket"] == "non_assertive"],
    }
    crit = {"CONFIRM_IDENTITY", "STATE_ALLERGY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT", "SBAR_ELEMENT"}
    for n, tag in (("dev", "tuned-on"), ("test", "scored-before")):
        rows = [json.loads(l) for l in (T / "classifier_synthetic_v2" / f"{n}.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        out[f"{tag}: AI-judged v2 {n} critical-labelled rows"] = [r for r in rows if r["act"] in crit]
    for s in ("dev", "test", "test2"):
        rows = N.load_rows(s)
        out[f"bench {s}: critical rows"] = [r for r in rows if r["category"].startswith("critical")]
        out[f"bench {s}: non-performed rows"] = [r for r in rows if r["category"] in B.NC.ADVERSARIAL]
    return out


class Cached:
    def __init__(self, url):
        self.inner = OllamaEmbedder(url)
        self.c = json.loads(CACHE.read_text(encoding="utf-8")) if CACHE.exists() else {}

    def __call__(self, text):
        if text not in self.c:
            v = self.inner(text)
            if v is None:
                return None
            self.c[text] = v
        return self.c[text]

    def save(self):
        CACHE.parent.mkdir(parents=True, exist_ok=True)
        CACHE.write_text(json.dumps(self.c), encoding="utf-8")


PERMISSIVE = B.Context(B.ALL_STEPS, pending=True, readback=True)


def leaks(interp: Interpreter, rows: list[dict]) -> list[dict]:
    out = []
    for i, r in enumerate(rows):
        o = interp.interpret(PERMISSIVE, r["text"], f"p{i}", 0)
        if o["decision"] == "OFFER_EVIDENCE":
            _, probs = interp.clf.classify_with_probs(r["text"])
            out.append({"text": r["text"], "truth": r.get("act", ""), "offered": o["evidence"]["act"], "critical_mass": round(interp.clf.embedding.critical_mass(probs), 4)})
    return out


def run(interp_factory, url: str = "http://127.0.0.1:11434", names: dict | None = None) -> dict:
    emb = Cached(url)
    interp = interp_factory(emb)
    res = {}
    for name, rows in sets().items():
        l = leaks(interp, rows)
        res[name] = {"rows": len(rows), "leaks": len(l), "examples": l[:6], "leak_masses": [x["critical_mass"] for x in l]}
    emb.save()
    return res


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    import copy
    table = {}
    for name, cap in (("cap 0.03 (shipped)", "keep"), ("cap removed", None), ("cap 0.05", 0.05), ("cap 0.10", 0.10), ("cap 0.20", 0.20)):
        def factory(emb, cap=cap):
            c = EnsembleActClassifier(emb)
            if cap != "keep":
                c.embedding.head = copy.copy(c.embedding.head)
                if cap is None:
                    c.embedding.head.pop("critical_mass_max", None)
                else:
                    c.embedding.head["critical_mass_max"] = cap
            return Interpreter(c)
        table[name] = run(factory)
    for name, res in table.items():
        print("==", name)
        for s, v in res.items():
            print(f"  {s}: {v['leaks']}/{v['rows']}" + (f"   e.g. {v['examples'][0]}" if v["leaks"] else ""))
    if a.write:
        p = ROOT / "reports" / "ai" / "recall-safety-panel.json"
        p.write_text(json.dumps({"synthetic": True, "independent": False, "context": "most permissive: every step running, patient question and read-back waiting", "cap_variants": table}, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
