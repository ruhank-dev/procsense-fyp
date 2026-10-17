"""A flat embedding index over short approved texts (p2-ai-extensions-s02): exact cosine search, no vector database.

    index = FlatIndex.build(embedder, [("h.medicines", "I take ramipril and amlodipine."), ...])
    index.top_k(embedder("what tablets do you take?"), 3)       # [("h.medicines", 0.71), ...]

The index is a list of unit vectors and ids; a query is ranked by dot product. It is built at content-package time (tools/ai/eval_retrieval.py shows how) and is small enough (a case has tens of facts) that a
linear scan is instant. Nothing here decides what a character may say: the Core's disclosure decision comes first and retrieval could only ever choose among facts it already allows. The measurement in
tools/ai/eval_retrieval.py records why retrieval is not switched on for the dialogue or the debrief (reports/ai/retrieval-eval.json).
"""
from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Callable


def unit(v: list[float]) -> list[float]:
    n = math.sqrt(sum(x * x for x in v)) or 1.0
    return [x / n for x in v]


@dataclass
class FlatIndex:
    ids: list[str]
    vectors: list[list[float]]

    @classmethod
    def build(cls, embedder: Callable[[str], list[float] | None], items: list[tuple[str, str]]) -> "FlatIndex":
        ids, vecs = [], []
        for item_id, text in items:
            v = embedder(text)
            if v is None:
                raise RuntimeError(f"the embedding service could not embed {item_id}")
            ids.append(item_id)
            vecs.append(unit(v))
        return cls(ids, vecs)

    def top_k(self, query: list[float], k: int) -> list[tuple[str, float]]:
        q = unit(query)
        scored = sorted(((sum(a * b for a, b in zip(q, v)), i) for i, v in zip(self.ids, self.vectors)), key=lambda t: (-t[0], t[1]))
        return [(i, round(s, 4)) for s, i in scored[:k]]
