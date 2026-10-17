"""Builds the retrieval index over approved content and measures it (p1-live-scenario-generation-s02, OFF-05).

    uv run --project tools tools/generation/build_index.py            # build in memory, print what is indexed and the recall at 20 on tests/generation/retrieval_queries.json
    uv run --project tools tools/generation/build_index.py --check    # fail if the recall at 20 is under 0.9, or a document below the allowed review status is returned (CI)
    uv run --project tools tools/generation/build_index.py --ask "diabetes tablets before surgery" [--k 10]   # look at what a query finds

The index is built from the content folder each time it is needed (about a second) and remembers the hash of the files it was made from, so it cannot go stale unnoticed (`Index.is_current`).
The code is server/generation/retrieve.py, because the hosted service uses it live.
"""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from generation import retrieve as rt  # noqa: E402

QUERIES = ROOT / "tests" / "generation" / "retrieval_queries.json"
GATE = 0.9


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    t0 = time.perf_counter()
    index = rt.build_index()
    built = time.perf_counter() - t0
    if "--ask" in argv:
        k = int(argv[argv.index("--k") + 1]) if "--k" in argv else 10
        for h in rt.retrieve(index, argv[argv.index("--ask") + 1], k):
            print(f"{h.score:6.2f}  {h.doc.id:40}  {h.doc.status:24} {','.join(h.doc.sources[:3])}")
        return 0
    kinds = {k: sum(1 for d in index.docs if d.kind == k) for k in ("case", "block", "relation")}
    rec = rt.recall_at_k(index, json.loads(QUERIES.read_text(encoding="utf-8"))["queries"])
    print(f"index: {kinds}, built in {built:.2f} s, content hash {index.content_hash[:12]}")
    print(f"recall at {rec['k']} over {rec['queries']} queries: {rec['recall_at_k']} (worst query {rec['min']}); gate {GATE}")
    for q in rec["weak"][:5]:
        print(f"   weak: {q['recall']:.2f}  {q['query'][:90]}")
    problems = []
    if rec["recall_at_k"] < GATE:
        problems.append(f"recall at 20 is {rec['recall_at_k']}, under {GATE}: add embeddings only if this stays low (plan)")
    leaked = [h.doc.id for q in ("gallstones diabetes allergy", "appendicitis fasting", "potassium sepsis consent") for h in rt.retrieve(index, q, 200) if h.doc.status not in rt.DEFAULT_ALLOWED]
    if leaked:
        problems.append(f"documents below the allowed review status were returned: {leaked[:3]}")
    only_draft = {h.doc.status for h in rt.retrieve(index, "gallstones diabetes allergy", 200, allowed={"DRAFT"})}
    if only_draft - {"DRAFT"}:
        problems.append(f"asking only for DRAFT returned {only_draft}")
    if not index.is_current(ROOT / "content"):
        problems.append("the index is not current against the content files it was made from")
    for p in problems:
        print("FAIL  " + p)
    if "--check" in argv:
        if not problems:
            print("PASS  retrieval recall at 20 meets the gate, the status filter holds, and the index knows the hash of its content")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
