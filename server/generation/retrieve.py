"""Retrieval over approved content (p1-live-scenario-generation-s02: OFF-05 the index, LG-03 the retrieval).

    from generation.retrieve import build_index, retrieve, query_from_objectives
    index = build_index(content_dir)                               # in memory, about a second
    hits = retrieve(index, query_from_objectives(objectives, ...), k=20)

What is indexed: the authored cases (their story), the composer's blocks and the knowledge-base relations. Every document carries its tags, its review status and the source rows (SR ids) behind it.
Ranking is a tag filter and then BM25 (Okapi, k1 1.5, b 0.75) over lower-case words: deterministic, no model. Embeddings are only to be added if recall at 20 on the evaluation set falls under 0.9.
Nothing below the allowed review status is ever returned (DRAFT and CLINICAL_REVIEW never; the default is APPROVED_FOR_TRAINING or above, and generated practice-only material when a caller allows it).
The index remembers the hash of the files it was made from, so a caller can tell when it is stale (`is_current`).
"""
from __future__ import annotations

import hashlib
import json
import math
import re
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STATUS_ORDER = ["DRAFT", "CLINICAL_REVIEW", "GENERATED_PRACTICE_ONLY", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"]
DEFAULT_ALLOWED = {"APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"}
K1, B = 1.5, 0.75
WORD = re.compile(r"[a-z0-9]+")
STOP = frozenset("a an and are as at be by for from has have in is it its of on or that the this to was were will with".split())


def words(text: str) -> list[str]:
    return [w for w in WORD.findall(text.lower()) if w not in STOP and len(w) > 1]


@dataclass
class Document:
    id: str
    kind: str                 # case, block or relation
    title: str
    text: str
    tags: list[str]
    status: str
    sources: list[str]
    terms: Counter = field(default_factory=Counter, repr=False)
    length: int = 0


@dataclass
class Hit:
    doc: Document
    score: float


class Index:
    def __init__(self, docs: list[Document], content_hash: str, files: dict[str, str]):
        self.docs, self.content_hash, self.files = docs, content_hash, files
        self.by_id = {d.id: d for d in docs}
        for d in docs:
            d.terms = Counter(words(d.title + " " + d.text))
            d.length = sum(d.terms.values())
        self.avgdl = sum(d.length for d in docs) / max(1, len(docs))
        self.postings: dict[str, list[Document]] = defaultdict(list)
        for d in docs:
            for t in d.terms:
                self.postings[t].append(d)
        self.n = len(docs)

    def idf(self, term: str) -> float:
        df = len(self.postings.get(term, ()))
        return math.log(1 + (self.n - df + 0.5) / (df + 0.5))

    def is_current(self, content_dir: Path) -> bool:
        return self.content_hash == file_hash(content_dir, list(self.files))[0]


def file_hash(content_dir: Path, rel_paths: list[str]) -> tuple[str, dict[str, str]]:
    files = {r: hashlib.sha256((content_dir / r).read_bytes()).hexdigest() for r in sorted(rel_paths)}
    return hashlib.sha256(json.dumps(files, sort_keys=True).encode("utf-8")).hexdigest(), files


def _case_text(c: dict) -> str:
    parts = [c["scenario"], c["presentation"]["summary"]]
    for p in c["problems"]:
        parts += [p["label"], p["why"], *p.get("expected_actions", [])]
    for d in c["decisions"]:
        parts += [d["prompt"]]
    for key in ("recognised", "missed", "priority", "why_priority", "related", "independent", "trigger_escalation", "if_ignored", "remember"):
        v = c["debrief"].get(key)
        parts += v if isinstance(v, list) else [v or ""]
    parts += [r["test"] + " " + r["flag"] for r in c["results"]]
    return " ".join(str(p) for p in parts)


def build_index(content_dir: Path = ROOT / "content") -> Index:
    docs: list[Document] = []
    paths: list[str] = []
    for p in sorted((content_dir / "case_files").glob("*.json")):
        c = json.loads(p.read_text(encoding="utf-8"))
        paths.append(f"case_files/{p.name}")
        docs.append(Document(f"case:{c['id']}", "case", c["walkthrough_label"], _case_text(c),
                             [f"scenario:{c['scenario']}", f"tier:{c['tier']}", *(f"problem:{x['type']}" for x in c["problems"])], c["status"],
                             sorted({c["sources"]["story"], c["sources"]["authored"], *c["sources"].get("measurements", [])})))
    for p in sorted((content_dir / "blocks").glob("*/*.json")):
        if p.name == "types.json":
            continue
        b = json.loads(p.read_text(encoding="utf-8"))
        paths.append(f"blocks/{p.parent.name}/{p.name}")
        docs.append(Document(f"block:{b['id']}", "block", b["id"], b["type"] + " " + json.dumps(b["attributes"], ensure_ascii=False), [f"type:{b['type']}", *b["tags"]], b["status"], b["sources"]))
    kb = content_dir / "kb" / "relations.json"
    if kb.exists():
        paths.append("kb/relations.json")
        for r in json.loads(kb.read_text(encoding="utf-8"))["relations"]:
            if r["status"] != "approved":
                continue
            docs.append(Document(f"rel:{r['id']}", "relation", r["id"], " ".join(filter(None, [r["subject"].get("label") or r["subject"]["id"], r["predicate"].replace("_", " "), r["object"].get("label") or r["object"]["id"], r.get("note", "")])),
                                 [f"type:{r['type']}", f"confidence:{r['confidence']}"], "APPROVED_FOR_TRAINING", [s for s in r["sources"] if s.startswith("SR-")]))
    h, files = file_hash(content_dir, paths)
    return Index(docs, h, files)


def retrieve(index: Index, query: str, k: int = 20, allowed: set[str] | None = None, kinds: set[str] | None = None, require_tags: set[str] | None = None, exclude_ids: set[str] | None = None) -> list[Hit]:
    """The k best documents for the query, in score order (ties by id, so the answer is the same every time). Only documents of an allowed review status are considered."""
    allowed = DEFAULT_ALLOWED if allowed is None else allowed
    terms = Counter(words(query))
    scores: dict[str, float] = defaultdict(float)
    for t, qtf in terms.items():
        idf = index.idf(t)
        for d in index.postings.get(t, ()):
            tf = d.terms[t]
            scores[d.id] += idf * tf * (K1 + 1) / (tf + K1 * (1 - B + B * d.length / index.avgdl)) * (1 + 0.1 * (qtf - 1))
    hits = []
    for did, s in scores.items():
        d = index.by_id[did]
        if d.status not in allowed or (kinds and d.kind not in kinds) or (require_tags and not require_tags <= set(d.tags)) or (exclude_ids and did in exclude_ids):
            continue
        hits.append(Hit(d, s))
    hits.sort(key=lambda h: (-h.score, h.doc.id))
    return hits[:k]


def query_from_objectives(objectives: dict, behaviour_labels: dict[str, str], dimension_labels: dict[str, str]) -> str:
    """The words to search for: what the weakest behaviours and dimensions are called, and the scenario family."""
    parts = [objectives["scenario"]]
    parts += [behaviour_labels.get(c["behaviour"], c["behaviour"]) for c in objectives["competencies"]]
    parts += [dimension_labels.get(d, d) for d in objectives["dimensions"]]
    return " ".join(parts)


def recall_at_k(index: Index, queries: list[dict], k: int = 20) -> dict:
    """Mean recall of the expected cases among the top k case documents, over the evaluation queries (an expected list longer than k counts k)."""
    per, weak = [], []
    for q in queries:
        got = {h.doc.id.removeprefix("case:") for h in retrieve(index, q["query"], k, kinds={"case"})}
        want = set(q["expected_cases"])
        r = len(want & got) / min(len(want), k)
        per.append(r)
        if r < 1:
            weak.append({"query": q["query"], "recall": r})
    return {"queries": len(per), "recall_at_k": round(sum(per) / len(per), 4) if per else 0.0, "min": round(min(per), 4) if per else 0.0, "k": k, "weak": sorted(weak, key=lambda w: w["recall"])}
