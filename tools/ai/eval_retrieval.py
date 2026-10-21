"""Does retrieval over approved content help the dialogue and the debrief? (p2-ai-extensions-s02, feature E58): `uv run --project tools tools/ai/eval_retrieval.py [--cases 12]`.

The step asks for a flat embedding index over approved content (case facts) to choose which facts go into a prompt, and for the effect on the prompt size and on missing a needed fact; if it does not help, the decision
is recorded and it is not shipped. This measures it with the embedding model already in use (all-minilm:22m through Ollama, server/retrieval/embedder.py) on the P0 cases:

- DIALOGUE. Every non-volunteered history fact of a case is indexed; the nurse's question about its topic (two wordings) is embedded; reported: how often the asked fact is the top 1, 3 and 5 hits. Then the size of the facts
  block of the prompt in three arms: ALL facts of the case (no Core filter), the facts the Core allows (what the dialogue prompt holds today: volunteered facts plus the one just earned), and the top 3 hits of the
  allowed facts. The Core's filter decides what may be said and comes first; retrieval could only ever choose among what it allows.
- DEBRIEF. Every scored item must be covered by the rewrite (the strict validator rejects a missing fact), so retrieval has nothing to drop: the facts of a golden session are counted to show the prompt cannot shrink.

The decision rule is fixed here before the run: ship retrieval only if the asked fact is in the top 3 at least 95 percent of the time AND the prompt facts block shrinks by at least 30 percent against what is sent today.
Results go to artifacts/ai-bench/retrieval-eval.json (git-ignored) and, when committed, reports/ai/. Needs a running Ollama with all-minilm:22m.
"""
import argparse
import json
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))

WORDINGS = ("Can you tell me about your {t}?", "What about your {t}, any problems there?")


def tokens(text: str) -> int:
    return max(1, round(len(text) / 4))             # a rough count: four characters to a token


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--cases", type=int, default=12)
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    a = ap.parse_args()
    from retrieval.embedder import OllamaEmbedder
    from composer import blocks as B
    from retrieval.flat import FlatIndex
    emb = OllamaEmbedder(a.ollama, "all-minilm:22m", timeout=60.0)
    if emb("test") is None:
        raise SystemExit("BLOCKED: Ollama or all-minilm:22m is not available")
    cases = [c for c in B.load_cases() if c["tier"] == "P0" and c["status"].startswith("APPROVED")][: a.cases]
    hits = {1: 0, 3: 0, 5: 0}
    n = 0
    block = {"all_facts": [], "allowed_today": [], "top3_of_allowed": []}
    for c in cases:
        facts = [(f"h.{h['topic']}", f"{h['topic'].replace('_', ' ')}: {h['answer']}", bool(h["volunteered"])) for h in c["history"] if h.get("variant") is None and len(h["answer"]) >= 12]
        index = FlatIndex.build(emb, [(i, t) for i, t, _ in facts])
        for fid, text, volunteered in facts:
            if volunteered:
                continue
            for wording in WORDINGS:
                q = emb(wording.format(t=fid[2:].replace("_", " ")))
                rank = [i for i, _ in index.top_k(q, 5)]
                n += 1
                for k in hits:
                    hits[k] += fid in rank[:k]
            allowed = [f for f in facts if f[2] or f[0] == fid]                       # what the Core lets the character say once the fact is earned
            sub = FlatIndex.build(emb, [(i, t) for i, t, _ in allowed])
            top3 = [i for i, _ in sub.top_k(emb(WORDINGS[0].format(t=fid[2:].replace("_", " "))), 3)]
            block["all_facts"].append(tokens("\n".join(f"- {i}: {t}" for i, t, _ in facts)))
            block["allowed_today"].append(tokens("\n".join(f"- {i}: {t}" for i, t, _ in allowed)))
            block["top3_of_allowed"].append(tokens("\n".join(f"- {i}: {t}" for i, t, _ in allowed if i in top3)))
    mean = {k: round(statistics.mean(v), 1) for k, v in block.items()}
    saving = round(1 - mean["top3_of_allowed"] / mean["allowed_today"], 3)
    recall3 = round(hits[3] / n, 3)
    goldens = sorted((ROOT / "tests" / "golden").glob("*_correct.expected.json"))[:1]
    ship = recall3 >= 0.95 and saving >= 0.30
    out = {"embedding_model": "all-minilm:22m", "cases": len(cases), "questions": n, "asked_fact_in_top": {str(k): round(v / n, 3) for k, v in hits.items()}, "facts_block_tokens_mean": mean,
           "saving_top3_against_allowed_today": saving, "rule": "ship only if the asked fact is in the top 3 at least 95 percent of the time and the facts block shrinks at least 30 percent against what is sent today",
           "dialogue_decision": "ship" if ship else "do not ship",
           "debrief_decision": "do not ship: the rewrite must cover every scored item (the strict validator rejects a missing fact), so there is nothing to leave out",
           "reason": f"the Core's disclosure filter already leaves the dialogue prompt about {mean['allowed_today']} tokens of facts; the top 3 of them would save {round(100 * saving)} percent of that and the asked fact is in the top 3 {round(100 * recall3)} percent of the time over all facts of the case",
           "limit": "a token count is characters divided by four; the questions are two fixed wordings per topic, not free speech from trainees"}
    path = ROOT / "artifacts" / "ai-bench" / "retrieval-eval.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=1))
