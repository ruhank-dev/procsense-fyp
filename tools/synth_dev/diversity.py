"""Diversity and leakage helpers for the direct synthetic development set: normalisation, skeletons, openings, distinct-n, self-BLEU, concentration, and the leak check against every historical sentence of the repository (exact, word-set Jaccard, embedding cosine from the local all-minilm cache; counts and rules only, never the matched sentence)."""
from __future__ import annotations

import collections
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
def norm(t: str) -> str:
    return " ".join(re.sub(r"[^a-z0-9' ]+", " ", t.lower().replace("’", "'")).split())


JACCARD = 0.7
COS_HISTORICAL = 0.92
COS_WITHIN = 0.90
FUNCTION_WORDS = set("a an the and or but if so then than that this those these it its i you he she they we me my your his her their our us him them is are was were be been being am do does did have has had will would can could should shall may might must to of in on at for with from by as about into over after before up out off not no yes please there here what which who whom whose when where why how also just very too".split())


def historical_texts(exclude: str = "synthetic_dev_v1") -> set[str]:
    """every sentence of the repository's test sets (the same pattern as tools/bench/rubric_labelling_kit.known_sentences), without the set under construction."""
    out = set()
    for p in list((ROOT / "tests").glob("**/*.jsonl")) + list((ROOT / "tests").glob("**/*.json")):
        if exclude in p.parts:
            continue
        try:
            txt = p.read_text(encoding="utf-8")
        except OSError:
            continue
        out |= {norm(m) for m in re.findall(r'"(?:text|sentence)"\s*:\s*"([^"]{3,300})"', txt)}
    return out


def historical_vectors() -> list[list[float]]:
    out = []
    for p in (ROOT / "artifacts" / "ai-bench" / "interp").glob("emb-*.json"):
        try:
            d = json.loads(p.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        out += [unit(v) for v in d.values() if isinstance(v, list) and len(v) == 384]
    return out


def unit(v):
    n = math.sqrt(sum(x * x for x in v)) or 1.0
    return [x / n for x in v]


def cos(a, b) -> float:
    return sum(x * y for x, y in zip(a, b))


class Leaks:
    def __init__(self, texts: set[str] | None = None, vectors: list | None = None):
        self.texts = historical_texts() if texts is None else texts
        self.sets = [(frozenset(t.split()), t) for t in self.texts if len(t.split()) >= 4]
        self.vectors = historical_vectors() if vectors is None else vectors

    def check(self, text: str, vec=None) -> str:
        """'' when clean, else the rule that fired: exact, jaccard or embedding (never the matched sentence)."""
        n = norm(text)
        if n in self.texts:
            return "leak_exact"
        w = frozenset(n.split())
        if len(w) >= 4:
            for kw, _ in self.sets:
                i = len(w & kw)
                if i and i / len(w | kw) >= JACCARD:
                    return "leak_jaccard"
        if vec is not None and self.vectors:
            u = unit(vec)
            if max(cos(u, h) for h in self.vectors) >= COS_HISTORICAL:
                return "leak_embedding"
        return ""


def skeleton(text: str) -> str:
    return " ".join(w if w in FUNCTION_WORDS else "_" for w in norm(text).split())


def opening(text: str, n: int = 3) -> str:
    return " ".join(norm(text).split()[:n])


def distinct_n(texts: list[str], n: int) -> float:
    grams = []
    for t in texts:
        w = norm(t).split()
        grams += [tuple(w[i:i + n]) for i in range(len(w) - n + 1)]
    return round(len(set(grams)) / len(grams), 4) if grams else 0.0


def _ngrams(w, n):
    return collections.Counter(tuple(w[i:i + n]) for i in range(len(w) - n + 1))


def self_bleu(texts: list[str], max_n: int = 4) -> float:
    """mean BLEU-4 of each text against all the others (lower = more diverse); a plain implementation with brevity penalty."""
    toks = [norm(t).split() for t in texts]
    if len(toks) < 2:
        return 0.0
    total, cnt = 0.0, 0
    refs_n = {n: [_ngrams(t, n) for t in toks] for n in range(1, max_n + 1)}
    for i, hyp in enumerate(toks):
        if not hyp:
            continue
        logs, ok = 0.0, True
        for n in range(1, max_n + 1):
            h = _ngrams(hyp, n)
            if not h:
                ok = False
                break
            maxref = collections.Counter()
            for j, r in enumerate(refs_n[n]):
                if j != i:
                    for g, c in r.items():
                        if c > maxref[g]:
                            maxref[g] = c
            clip = sum(min(c, maxref[g]) for g, c in h.items())
            logs += math.log(max(clip, 0.1) / sum(h.values())) / max_n
        near = min((abs(len(t) - len(hyp)) for j, t in enumerate(toks) if j != i), default=0)
        bp = 1.0
        total += bp * math.exp(logs)
        cnt += 1
    return round(total / cnt, 4) if cnt else 0.0


def concentration(values: list[str]) -> dict:
    c = collections.Counter(values)
    n = len(values) or 1
    top = c.most_common(3)
    return {"distinct": len(c), "top_share": round(top[0][1] / n, 4) if top else 0.0, "top": [(k, v) for k, v in top]}
