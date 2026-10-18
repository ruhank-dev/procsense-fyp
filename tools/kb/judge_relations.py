"""AI-judged precision of the extracted relations (p1-dataset-analysis-s06), a stand-in while no two people can label 100 of them: `uv run --project tools tools/kb/judge_relations.py [--n 100] [--model M]`.

The step's gate is "extraction precision at least 0.8 on 100 HAND-labelled relations". That needs people, and it stays open. What this adds is a measured, reproducible proxy that is clearly not that gate: the first N
relations of the review queue (the fixed pseudo-random order of extract_relations.py --sheet) are put to two configurations of a local model that did not extract them (qwen3.5:4b; J1 asks 'does the quote state
this relation', J2 words it as a reader checking a claim and reverses the answer order), each blind to the other and to the extractor's reasoning, and a relation counts as supported only when BOTH say yes.
Reported: the share supported by both, by either, agreement between the judges (Cohen kappa), and the relations the judges split on (for a person to read first). The deterministic check of extract_relations.py
(the quote appears word for word in the passage and both ends appear in the quote) has already been applied to every queued relation, so what is judged here is whether the quote really states the relation.
The result carries `independent: false` and `hand_labelled: false`; it never switches anything on and never writes to the knowledge base.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "kb"))
import extract_relations as X  # noqa: E402

REPORT = ROOT / "reports" / "ai" / "kb-relations-ai-judged.json"
MODEL = "qwen3.5:4b-q4_K_M"
SCHEMA = {"type": "object", "required": ["supported"], "additionalProperties": False, "properties": {"supported": {"enum": ["yes", "no"]}}}
READABLE = {"indicates": "indicates", "requires": "requires", "increases_risk_of": "increases the risk of", "contraindicates": "makes unsafe (contraindicates)", "leads_to": "leads to", "is_a_sign_of": "is a sign of",
            "should_be_checked_with": "should be checked together with"}
SYSTEM = {
    "J1": "You check one claim against one quote from a medical training text. Answer yes only if the quote itself states the claim (or states it in other words). Answer no if the quote only mentions the words, states something else, or states the opposite. Do not use outside knowledge.",
    "J2": "A reader is checking whether a text really says what someone claims it says. Look only at the quote. If the claim follows from the quote alone, reply yes; if it needs anything the quote does not say, or the quote says the reverse, reply no.",
}


def prompt(r: dict, config: str) -> str:
    claim = f"{r['subject']} {READABLE[r['predicate']]} {r['object']}"
    if config == "J1":
        return f"Quote: \"{r['quote']}\"\nClaim: {claim}\nDoes the quote state the claim?"
    return f"Claim: {claim}\nQuote: \"{r['quote']}\"\nDoes the claim follow from the quote alone?"


def kappa(a: list[bool], b: list[bool]) -> float | None:
    n = len(a)
    if not n:
        return None
    po = sum(x == y for x, y in zip(a, b)) / n
    pa, pb = sum(a) / n, sum(b) / n
    pe = pa * pb + (1 - pa) * (1 - pb)
    return None if pe == 1 else round((po - pe) / (1 - pe), 3)


def summarise(rows: list[dict], j1: list[bool], j2: list[bool]) -> dict:
    both = sum(x and y for x, y in zip(j1, j2))
    either = sum(x or y for x, y in zip(j1, j2))
    n = len(rows)
    split = [r for r, x, y in zip(rows, j1, j2) if x != y]
    return {"relations": n, "supported_by_both": both, "precision_both": round(both / n, 3) if n else None, "supported_by_either": either, "precision_either": round(either / n, 3) if n else None,
            "kappa": kappa(j1, j2), "split_judgements": [{"id": r["id"], "subject": r["subject"], "predicate": r["predicate"], "object": r["object"], "quote": r["quote"]} for r in split]}


def run(n: int, model: str) -> dict:
    from llm import client as L
    queue = json.loads(X.QUEUE.read_text(encoding="utf-8"))
    import csv
    import io
    rows = list(csv.DictReader(io.StringIO(X.sheet(queue, n))))
    byid = {r["id"]: r for r in queue["relations"]}
    client = L.OllamaClient()
    votes: dict[str, list[bool]] = {"J1": [], "J2": []}
    for row in rows:
        r = byid[row["id"]]
        for cfg in votes:
            rep = client.chat(model, SYSTEM[cfg], prompt(r, cfg), schema=SCHEMA, think=False, timeout=90, num_predict=20, options={"temperature": 0, "seed": 7})
            ok = False
            if rep.ok:
                try:
                    ok = json.loads(rep.text)["supported"] == "yes"
                except (ValueError, KeyError):
                    pass
            votes[cfg].append(ok)
    out = summarise([byid[r["id"]] for r in rows], votes["J1"], votes["J2"])
    out.update({"judge_model": model, "extraction_model": queue["relations"][0]["model"] if queue["relations"] else "", "synthetic": True, "independent": False, "hand_labelled": False,
                "gate_0_8_met": False, "note": "A proxy for the step's gate, not the gate: that needs 100 relations labelled by people. Judges are the same family as the extractor."})
    return out


def main(argv: list[str]) -> int:
    n = int(argv[argv.index("--n") + 1]) if "--n" in argv else 100
    model = argv[argv.index("--model") + 1] if "--model" in argv else MODEL
    global REPORT
    if "--queue" in argv:
        X.QUEUE = Path(argv[argv.index("--queue") + 1])                      # judge another queue than the committed one
    if "--out" in argv:
        REPORT = Path(argv[argv.index("--out") + 1])
    out = run(n, model)
    REPORT.write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({k: v for k, v in out.items() if k != "split_judgements"}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
