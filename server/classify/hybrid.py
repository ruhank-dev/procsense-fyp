"""Communication act classifier from sentence embeddings AND word features in one logistic regression (the first reader of the credit gate; server/classify/ensemble.py).

    clf = HybridActClassifier(OllamaEmbedder("http://127.0.0.1:11434"))
    r = clf.classify("Could you tell me about your last meal?")      # the same Result as the other classifiers

The sentence is embedded (all-minilm:22m, 384 numbers, length 1) and turned into TF-IDF word and word-pair features (the vocabulary and idf of server/classify/tfidf.py); the two are joined into one vector and one logistic
regression head (content/assessment/act_head_hybrid.json, made by tools/bench/build_safe_heads.py) gives a probability for each act. On the synthetic v2 dev split it names the act far better than either part alone
(macro-F1 0.87 against 0.76 for the embedding head and 0.83 for the TF-IDF head), which is where the credit gate lost most of its legitimate credit. Every safety rule of the other heads is inherited unchanged: a critical act
and NONE are never credited, an act needs its own threshold, a critical-content cue or a critical-act probability at or above `critical_mass_max` removes credit. It is still not trusted alone: the gate also needs the
separate TF-IDF head to name the same act, and the sentence to be an assertion. A down embedder gives `source: unavailable` and credits nothing.
"""
from __future__ import annotations

import math
from pathlib import Path

try:
    from classify.classifier import Result
    from classify.embedding import ROOT, EmbeddingActClassifier, load_head
    from classify.tfidf import features
except ImportError:
    from server.classify.classifier import Result
    from server.classify.embedding import ROOT, EmbeddingActClassifier, load_head
    from server.classify.tfidf import features

HEAD_HYBRID = ROOT / "content" / "assessment" / "act_head_hybrid.json"


class HybridActClassifier(EmbeddingActClassifier):
    def __init__(self, embedder, head: dict | None = None, path: Path = HEAD_HYBRID):
        head = head if head is not None else load_head(path)
        super().__init__(embedder, head)
        self.dim = int(head["embedding_dimension"])
        self.index = {term: i for i, term in enumerate(head["vocabulary"])}
        if len(self.index) != len(head["idf"]) or any(len(row) != self.dim + len(self.index) for row in head["coef"]):
            raise ValueError("the hybrid head's vocabulary, idf and weights disagree")
        self.model = f"{getattr(embedder, 'model', 'embeddings')}+words"

    def sparse(self, text: str) -> dict[int, float]:
        raw = {self.index[t]: (1.0 + math.log(c)) * self.head["idf"][self.index[t]] for t, c in features(text).items() if t in self.index}
        norm = math.sqrt(sum(v * v for v in raw.values()))
        return {i: v / norm for i, v in raw.items()} if norm else {}

    def probabilities_of(self, vector: list[float], text: str) -> dict[str, float]:
        h = self.head
        norm = math.sqrt(sum(x * x for x in vector)) or 1.0
        x = [v / norm for v in vector]
        sp = self.sparse(text)
        logits = []
        for row, b in zip(h["coef"], h["intercept"]):
            z = sum(w * v for w, v in zip(row[:self.dim], x)) + b
            z += sum(row[self.dim + i] * v for i, v in sp.items())
            logits.append(z)
        top = max(logits)
        exp = [math.exp(z - top) for z in logits]
        total = sum(exp)
        return {c: e / total for c, e in zip(h["classes"], exp)}

    def classify(self, sentence: str) -> Result:
        if self.embedder is None:
            return Result("NONE", 0.0, False, "unavailable", error="NO_MODEL")
        vec = self.embedder(sentence)
        if vec is None or len(vec) != self.dim:
            return Result("NONE", 0.0, False, "unavailable", error="EMBEDDING_UNAVAILABLE")
        probs = self.probabilities_of(vec, sentence)
        act = max(probs, key=probs.get)
        return Result(act, probs[act], self.credit_allowed(act, probs[act], sentence, self.critical_mass(probs)), "hybrid", act in self.critical)
