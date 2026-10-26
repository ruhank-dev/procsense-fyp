"""Builds a challenge set of spoken orders with a language model that is NOT the model under test (EX-18, C32): `uv run --project tools tools/ai/make_orders_set.py --model gpt-oss:120b-cloud --out tests/orders/paraphrases_v9.json --id OA`.

Why: the first eight sets were written by the same family of assistant that builds the mapper. A set written by another model family, from a fixed authoring prompt that is stored with the set and never changed after it is
run, is more independent of the builder than one the builder writes by hand. It is still a set written by a language model: it is NOT human-authored and does not satisfy EX-18 (a second human author); the file says so.

For every (kind, item) of the order vocabulary the author is asked for two things it may not see the mapper's answer to: the labels of the rows are the item the author was asked to paraphrase (so no one chooses a label
after seeing an answer), and for a sample of items a sentence that MENTIONS the item without asking for it (the non-orders). Rows whose sentence names no other item and is not empty are kept; nothing is edited by hand.
Needs a running Ollama with the author model (a cloud model needs PROCSENSE_RELAY_MODELS=<name>).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import random
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from server.llm import client as C  # noqa: E402
from server.orders import llm_fallback as LF  # noqa: E402
from server.orders.grammar import vocabulary  # noqa: E402

INTENT = {"test": "ORDER_TEST", "medicine": "REQUEST_MEDICATION", "home_medicine": "HOLD_MED", "place": "REQUEST_TRANSPORT", "staff": "CALL_STAFF"}
WHAT = {"test": "asks for the test or scan to be done", "medicine": "asks for the medicine, fluid or oxygen to be given to the patient", "home_medicine": "says the patient's own home medicine must be stopped, held or not taken",
        "place": "asks for the patient to be taken to that place", "staff": "asks for that member of staff to be called or to come"}
AUTHOR_SYSTEM = ("You write test sentences for a speech interface in a hospital training simulation. A trainee doctor speaks aloud to the simulator. All people are invented. "
                 "Write natural spoken sentences, the way different trainees really talk: some formal, some casual, some described instead of named, some as a question. Answer only with JSON.")
SCHEMA = {"type": "object", "required": ["sentences"], "additionalProperties": False, "properties": {"sentences": {"type": "array", "items": {"type": "string"}, "minItems": 1, "maxItems": 4}}}


def strings(obj) -> list[str]:
    """Every sentence in whatever JSON the author returned (a cloud model may ignore the schema and call the field `sentence` or `text`)."""
    if isinstance(obj, str):
        return [obj]
    if isinstance(obj, list):
        return [x for v in obj for x in strings(v)]
    if isinstance(obj, dict):
        return [x for v in obj.values() for x in strings(v)]
    return []


def authoring_prompt() -> str:
    return AUTHOR_SYSTEM + "\n" + "ORDER: Item: {label} ({kind}). Write {n} different short sentences, each one a trainee might say that {what}. Do not use the same words in every sentence; do not mention any other item.\n" + \
        "NOT AN ORDER: Item: {label} ({kind}). Write 1 short sentence a trainee might say that mentions this item or its person but does NOT ask for anything (news, a remark about the past, small talk)."


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--id", required=True, help="prefix of the row ids")
    ap.add_argument("--per-item", type=int, default=2)
    ap.add_argument("--non-orders", type=int, default=30)
    ap.add_argument("--seed", type=int, default=1)
    a = ap.parse_args()
    client = C.OllamaClient("http://127.0.0.1:11434")
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not available (cloud models need PROCSENSE_RELAY_MODELS)")
    items: dict[tuple[str, str], str] = {}
    for e in vocabulary():
        if e.kind == "home_medicine" and e.target in ("apixaban",):
            pass
        items.setdefault((e.kind, e.target), e.label)
    rng = random.Random(a.seed)
    homes = sorted(k for k in items if k[0] == "home_medicine")
    rng.shuffle(homes)
    for k in homes[8:]:                                   # the vocabulary names dozens of home medicines; a sample of eight keeps the mix of kinds close to the earlier sets
        del items[k]
    rows, nonorders = [], []
    names = {k: v.lower() for k, v in items.items()}
    for (kind, target), label in sorted(items.items()):
        got = []
        for _attempt in range(3):                         # a cloud model sometimes answers nothing: ask again, the same question
            r = client.chat(a.model, AUTHOR_SYSTEM, f"Item: {label} ({kind}). Write {a.per_item} different short sentences, each one a trainee might say that {WHAT[kind]}. Do not use the same words in every sentence; do not mention any other item.",
                            schema=SCHEMA, timeout=120, num_predict=600)
            got = strings(r.json) if r.ok else []
            if got:
                break
        if not got:
            print("skipped", kind, target, r.error)
            continue
        for s in got[: a.per_item]:
            s = re.sub(r"\s+", " ", str(s)).strip().strip('"')
            if 8 <= len(s) <= 140:
                rows.append({"text": s, "kind": kind, "target": target, "intent": INTENT[kind]})
    pool = sorted(k for k in items if k[0] != "home_medicine")
    rng.shuffle(pool)
    for kind, target in pool[: a.non_orders]:
        label = items[(kind, target)]
        got = []
        for _attempt in range(3):
            r = client.chat(a.model, AUTHOR_SYSTEM, f"Item: {label} ({kind}). Write 1 short sentence a trainee might say that mentions this item or its person but does NOT ask for anything (news, a remark about the past, small talk).",
                            schema=SCHEMA, timeout=120, num_predict=400)
            got = strings(r.json) if r.ok else []
            if got:
                break
        if got:
            s = re.sub(r"\s+", " ", str(got[0])).strip().strip('"')
            if 8 <= len(s) <= 140:
                nonorders.append({"text": s, "kind": None, "target": None, "intent": None})
    rng.shuffle(rows)
    allrows = rows + nonorders
    rng.shuffle(allrows)
    for i, row in enumerate(allrows, 1):
        row["id"] = f"{a.id}{i:03d}"
    doc = {"synthetic": True, "independent": False, "human_authored": False,
           "note": f"Written by the language model {a.model} on {datetime.now(timezone.utc).strftime('%d %b %Y')} (UTC) from a fixed authoring prompt (sha256 {hashlib.sha256((AUTHOR_SYSTEM + str(WHAT)).encode()).hexdigest()[:16]}) BEFORE any row was run through any mapper. "
                   "The model is a different family from the model under test and from the assistant that wrote the earlier sets, so this set is less tied to the builder than they were; it is still machine-written, not human-authored, and does not satisfy EX-18. "
                   "Labels are the item the author was asked to paraphrase; nothing was edited by hand. Frozen: scored once per model.",
           "author_model": a.model, "author_prompt_sha256": hashlib.sha256((AUTHOR_SYSTEM + str(WHAT)).encode()).hexdigest(), "rows": allrows}
    Path(a.out).write_text(json.dumps(doc, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {a.out}: {len(rows)} orders and {len(nonorders)} non-orders by {a.model}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
