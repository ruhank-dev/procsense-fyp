"""Offline extraction of candidate relations from case and walkthrough text by a local language model (p1-dataset-analysis-s06, feature A66).

    uv run --project tools tools/kb/extract_relations.py --source cases --limit 20           # ask the model, write content/kb/review_queue.json
    uv run --project tools tools/kb/extract_relations.py --sheet 100 > sheet.csv              # a sample of queued relations for a person to mark correct yes/no
    uv run --project tools tools/kb/extract_relations.py --score sheet.csv                    # precision from the marked sheet (the step's gate is 0.8)
    uv run --project tools tools/kb/extract_relations.py --approve ID [ID ...] --by NAME      # a person approves queued relations into content/kb/approved_llm_relations.json

The model only PROPOSES. A relation is kept in the queue only if its quote appears word for word in the chunk and both its ends appear in that quote; anything else is counted and dropped.
Queued relations are `llm_unreviewed` and are never part of the knowledge base until a person approves them (`--approve`), which records who. The model and its digest are recorded with every queued relation.
Needs Ollama (default http://127.0.0.1:11434, never "localhost": that costs 2 s a call from Python on Windows) with the offline batch model of ADR-0010. CI never calls a model; it tests the checks with a fake one.
"""
from __future__ import annotations

import csv
import hashlib
import io
import json
import re
import sys
from pathlib import Path
from typing import Callable

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "ai"))
QUEUE = ROOT / "content" / "kb" / "review_queue.json"
APPROVED = ROOT / "content" / "kb" / "approved_llm_relations.json"
WALKTHROUGH = ROOT / "docs" / "context" / "ProcSense_Medical_Walkthrough_v5_1.md"
DEFAULT_MODEL = "qwen3.5:4b-q4_K_M"
PREDICATES = ["indicates", "requires", "increases_risk_of", "contraindicates", "leads_to", "is_a_sign_of", "should_be_checked_with"]
SCHEMA = {"type": "object", "properties": {"relations": {"type": "array", "items": {"type": "object", "properties": {
    "subject": {"type": "string"}, "predicate": {"enum": PREDICATES}, "object": {"type": "string"}, "quote": {"type": "string"}}, "required": ["subject", "predicate", "object", "quote"]}}}, "required": ["relations"]}
PROMPT = """You read one passage of a medical training case and list the relations it states between a condition, a finding, a test, a medicine or an action.
Rules: use only what the passage says; do not add medical knowledge; each relation needs a quote copied word for word from the passage that states it; subject and object must be words that appear in the quote.
Predicates allowed: {predicates}. If the passage states no relation, return an empty list.

Example passage: "Acute kidney injury caused by poor circulation. Fluids help; no anti-inflammatories."
Example answer: {{"relations": [{{"subject": "poor circulation", "predicate": "leads_to", "object": "acute kidney injury", "quote": "Acute kidney injury caused by poor circulation"}}, {{"subject": "acute kidney injury", "predicate": "contraindicates", "object": "anti-inflammatories", "quote": "no anti-inflammatories"}}]}}

Passage:
{chunk}
"""


def norm(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip().lower()


def verify(rel: dict, chunk: str) -> str | None:
    """None if the relation is grounded in the chunk; otherwise why not."""
    if rel.get("predicate") not in PREDICATES:
        return "predicate not allowed"
    q, c = norm(rel.get("quote", "")), norm(chunk)
    if len(q) < 12 or q not in c:
        return "quote not found word for word"
    for end in ("subject", "object"):
        text = norm(rel.get(end, ""))
        if len(text) < 3 or text not in q:
            return f"{end} not in the quote"
    return None


def chunks_from_cases(limit: int) -> list[dict]:
    out = []
    for path in sorted((ROOT / "content" / "case_files").glob("*.json")):
        c = json.loads(path.read_text(encoding="utf-8"))
        parts = [("summary", c["presentation"]["summary"])]
        for p in c["problems"]:
            parts.append((f"problem.{p['id']}", f"{p['label']}. {p['why']}"))
        d = c["debrief"]
        for k in ("related", "independent", "trigger_escalation", "if_ignored", "remember"):
            if d.get(k):
                parts.append((f"debrief.{k}", d[k]))
        for name, text in parts:
            if len(text) >= 100:
                out.append({"id": f"{c['id']}:{name}", "source": f"content/case_files/{c['id']}.json", "text": text})
    return out[:limit]


def chunks_from_walkthrough(limit: int) -> list[dict]:
    text = WALKTHROUGH.read_text(encoding="utf-8")
    out, buf, start = [], [], 0
    for i, line in enumerate(text.splitlines(), 1):
        if line.strip():
            if not buf:
                start = i
            buf.append(line.strip())
        elif buf:
            para = " ".join(buf)
            if 200 <= len(para) <= 1500:
                out.append({"id": f"walkthrough:{start}", "source": "docs/context/ProcSense_Medical_Walkthrough_v5_1.md", "text": para})
            buf = []
    return out[:limit]


def call_model(model: str, url: str) -> Callable[[str], str]:
    import benchmark
    def run(prompt: str) -> str:
        r = benchmark.generate(url, model, prompt, think=False, ctx=4096, schema=SCHEMA, seed=1, num_predict=700)
        return r["response"]
    return run


def extract(chunks: list[dict], ask: Callable[[str], str], model: str, digest: str) -> dict:
    kept, stats = [], {"chunks": len(chunks), "proposed": 0, "kept": 0, "dropped": {}, "bad_json": 0}
    for ch in chunks:
        try:
            reply = json.loads(ask(PROMPT.format(predicates=", ".join(PREDICATES), chunk=ch["text"])))
            proposals = reply["relations"]
        except (json.JSONDecodeError, KeyError, TypeError):
            stats["bad_json"] += 1
            continue
        for r in proposals:
            stats["proposed"] += 1
            why = verify(r, ch["text"])
            if why:
                stats["dropped"][why] = stats["dropped"].get(why, 0) + 1
                continue
            rid = "llm." + hashlib.sha256("|".join((ch["id"], norm(r["subject"]), r["predicate"], norm(r["object"]))).encode("utf-8")).hexdigest()[:12]
            if any(k["id"] == rid for k in kept):
                continue
            kept.append({"id": rid, "chunk": ch["id"], "source": ch["source"], "subject": r["subject"].strip(), "predicate": r["predicate"], "object": r["object"].strip(), "quote": r["quote"].strip(),
                         "model": model, "digest": digest, "confidence": "llm_unreviewed", "status": "review"})
            stats["kept"] += 1
    return {"note": "Candidate relations from an offline model. Unreviewed: not part of the knowledge base until a person approves them.", "stats": stats, "relations": sorted(kept, key=lambda k: k["id"])}


def sheet(queue: dict, n: int) -> str:
    rows = sorted(queue["relations"], key=lambda r: hashlib.sha256(r["id"].encode()).hexdigest())[:n]       # a fixed pseudo-random order
    out = io.StringIO()
    w = csv.writer(out, lineterminator="\n")
    w.writerow(["id", "chunk", "subject", "predicate", "object", "quote", "correct (yes/no)"])
    for r in rows:
        w.writerow([r["id"], r["chunk"], r["subject"], r["predicate"], r["object"], r["quote"], ""])
    return out.getvalue()


def score(path: str) -> dict:
    with open(path, encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    marked = [r for r in rows if r["correct (yes/no)"].strip().lower() in ("yes", "no")]
    yes = sum(1 for r in marked if r["correct (yes/no)"].strip().lower() == "yes")
    return {"marked": len(marked), "correct": yes, "precision": round(yes / len(marked), 3) if marked else None, "gate_0_8_met": bool(marked) and yes / len(marked) >= 0.8 and len(marked) >= 100}


def approve(ids: list[str], by: str) -> int:
    queue = json.loads(QUEUE.read_text(encoding="utf-8"))
    approved = json.loads(APPROVED.read_text(encoding="utf-8")) if APPROVED.exists() else {"note": "Relations a person approved from the model's queue (tools/kb/extract_relations.py --approve).", "relations": []}
    have = {r["id"] for r in approved["relations"]}
    n = 0
    for r in queue["relations"]:
        if r["id"] in ids and r["id"] not in have:
            approved["relations"].append({"id": r["id"], "type": "llm_proposed", "subject": {"kind": "term", "id": norm(r["subject"]).replace(" ", "_")[:60], "label": r["subject"]}, "predicate": r["predicate"],
                                          "object": {"kind": "term", "id": norm(r["object"]).replace(" ", "_")[:60], "label": r["object"]}, "sources": [f"LLM:{r['id']}"],
                                          "evidence": {"kind": "llm", "file": r["source"], "row": r["chunk"], "quote": r["quote"]}, "confidence": "authored", "status": "approved",
                                          "note": f"proposed by {r['model']} ({r['digest'][:12]}), approved by {by}"})
            n += 1
    approved["relations"].sort(key=lambda r: r["id"])
    APPROVED.write_text(json.dumps(approved, indent=1, sort_keys=True, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"approved {n} relation(s); rebuild the base with tools/kb/build_kb.py")
    return 0


def main(argv: list[str]) -> int:
    global QUEUE
    sys.stdout.reconfigure(encoding="utf-8")
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d   # noqa: E731
    if opt("--queue"):
        QUEUE = Path(opt("--queue"))                                        # write (or read) another queue file, for example to compare two models without touching the committed queue
    if "--sheet" in argv:
        sys.stdout.write(sheet(json.loads(QUEUE.read_text(encoding="utf-8")), int(opt("--sheet"))))
        return 0
    if "--score" in argv:
        print(json.dumps(score(opt("--score")), indent=1))
        return 0
    if "--approve" in argv:
        i = argv.index("--approve") + 1
        ids = []
        while i < len(argv) and not argv[i].startswith("--"):
            ids.append(argv[i])
            i += 1
        return approve(ids, opt("--by", "unknown"))
    model, url = opt("--model", DEFAULT_MODEL), opt("--url", "http://127.0.0.1:11434")
    import benchmark
    digest = benchmark.digest(url, model)
    limit = int(opt("--limit", "20"))
    chunks = (chunks_from_walkthrough if opt("--source", "cases") == "walkthrough" else chunks_from_cases)(limit)
    result = extract(chunks, call_model(model, url), model, digest)
    QUEUE.write_text(json.dumps(result, indent=1, sort_keys=True, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    s = result["stats"]
    print(f"{s['chunks']} chunks, {s['proposed']} proposed, {s['kept']} kept (quote found word for word, both ends in the quote), bad JSON {s['bad_json']}, dropped {s['dropped']} -> content/kb/review_queue.json")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
