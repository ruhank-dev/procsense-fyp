"""The credit gate the service uses: two independent heads must agree, and the sentence must be an assertion (p1-ai-language-s07, classifier safety review of 18 October 2026, third session).

    clf = EnsembleActClassifier(OllamaEmbedder("http://127.0.0.1:11434"))
    r = clf.classify("Could you tell me about your last meal?")      # the same Result as the other classifiers; `refused` names the guard that removed credit

Since the false-reject analysis of the fourth session the first reader is the hybrid head, not the embedding head alone: it names the act much better and lost far less legitimate credit to the second reader (docs/research/classifier_benchmark/safety_gate.md).

Why: the embedding head alone credited 3 of 48 fresh evasive critical sentences under an ordinary act (docs/research/classifier_benchmark/safety_gate.md). A 22-million-parameter embedding places an unusual wording
of an identity check, a meal or a recommendation next to ordinary speech, so no cap on its critical probability catches it. The two heads read the sentence differently (meaning versus words), so a wrong ordinary
answer is rarely the same wrong answer in both: credit needs

  1. the hybrid head (embedding and word features in one regression, server/classify/hybrid.py) to be willing to credit the act (its own threshold, critical-mass cap and critical-content cues),
  2. the TF-IDF head's most probable act to be the same act (server/classify/tfidf.py),
  3. the sentence to be an assertion of the act, not a denial, a hypothetical or a report (server/classify/assertion.py).

Every rule only removes credit. A critical act is never credited from speech (the engine credits it from the action), the answer is always `source: ensemble`, and with the embedding service down nothing is
credited (the TF-IDF head alone is not trusted). This is evidence for the debrief only; deterministic action-based scoring stays authoritative.
"""
from __future__ import annotations

from typing import Callable

try:
    from classify.assertion import non_assertive
    from classify.classifier import Result
    from classify.embedding import load_head
    from classify.hybrid import HEAD_HYBRID, HybridActClassifier
    from classify.tfidf import TfidfActClassifier
except ImportError:
    from server.classify.assertion import non_assertive
    from server.classify.classifier import Result
    from server.classify.embedding import load_head
    from server.classify.hybrid import HEAD_HYBRID, HybridActClassifier
    from server.classify.tfidf import TfidfActClassifier


class EnsembleActClassifier:
    def __init__(self, embedder: Callable[[str], list[float] | None] | None, embedding_head: dict | None = None, tfidf_head: dict | None = None):
        self.embedding = HybridActClassifier(embedder, embedding_head if embedding_head is not None else load_head(HEAD_HYBRID))
        self.tfidf = TfidfActClassifier(tfidf_head)
        self.head = self.embedding.head
        self.critical = self.embedding.critical
        self.model = f"{self.embedding.model}+tfidf"

    def classify(self, sentence: str) -> Result:
        r = self.embedding.classify(sentence)
        if r.source == "unavailable":
            return r
        refused = ""
        if r.credited:
            other = self.tfidf.classify(sentence)
            if other.act != r.act:
                refused = f"HEADS_DISAGREE:{other.act}"
            elif (kind := non_assertive(sentence)):
                refused = f"NON_ASSERTIVE:{kind}"
        return Result(r.act, r.confidence, r.credited and not refused, "ensemble", r.critical, "", refused)
