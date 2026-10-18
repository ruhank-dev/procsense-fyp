"""Communication act classifier from TF-IDF word features + logistic regression, in plain Python (p1-ai-language-s07; the alternative the benchmark found equal to or ahead of the embedding head, ADR-0013).

    clf = TfidfActClassifier()                       # reads content/assessment/act_head_tfidf.json
    r = clf.classify("Could you tell me about your last meal?")     # the same Result as server/classify/embedding.py

It reproduces scikit-learn's TfidfVectorizer(ngram_range=(1, 2), lowercase=True, sublinear_tf=True) without scikit-learn or numpy: tokens are the runs of two or more word characters of the lower-cased sentence, features are
the tokens and the pairs of neighbouring tokens that are in the head's vocabulary, the weight of a feature is (1 + ln count) times its idf (ln((1 + n) / (1 + df)) + 1), and the vector is scaled to length 1. A logistic
regression head turns it into one probability per act. No service is called: a sentence costs about a millisecond, there is no model to load and nothing that can be down. The safety rules are those of the embedding
classifier, which this class extends: a critical act and NONE are never credited; credit needs the act's own threshold (tuned on dev only), no critical cue (server/classify/guard.py) and, when the head carries
`critical_mass_max`, a summed critical-act probability below it. The head is made by tools/bench/export_tfidf.py; a head made by anything else must fit the taxonomy and the mapping (load_head checks).
"""
from __future__ import annotations

import math
import re
from collections import Counter
from pathlib import Path

try:
    from classify.classifier import Result
    from classify.embedding import ROOT, EmbeddingActClassifier, load_head
except ImportError:
    from server.classify.classifier import Result
    from server.classify.embedding import ROOT, EmbeddingActClassifier, load_head

HEAD_TFIDF = ROOT / "content" / "assessment" / "act_head_tfidf.json"
TOKEN = re.compile(r"(?u)\b\w\w+\b")


def features(text: str) -> Counter:
    """Counts of the words and pairs of neighbouring words of a sentence, as scikit-learn's word analyser with ngram_range (1, 2) makes them."""
    tokens = TOKEN.findall(text.lower())
    return Counter(tokens + [f"{a} {b}" for a, b in zip(tokens, tokens[1:])])


class TfidfActClassifier(EmbeddingActClassifier):
    def __init__(self, head: dict | None = None, path: Path = HEAD_TFIDF):
        super().__init__(None, head if head is not None else load_head(path))
        self.index = {term: i for i, term in enumerate(self.head["vocabulary"])}
        if len(self.index) != self.head["dimension"] or len(self.head["idf"]) != self.head["dimension"]:
            raise ValueError("the TF-IDF head's vocabulary, idf and dimension disagree")
        self.model = "tfidf"

    def sparse(self, text: str) -> dict[int, float]:
        """The sentence as {feature index: weight}, length 1."""
        raw = {self.index[t]: (1.0 + math.log(c)) * self.head["idf"][self.index[t]] for t, c in features(text).items() if t in self.index}
        norm = math.sqrt(sum(v * v for v in raw.values()))
        return {i: v / norm for i, v in raw.items()} if norm else {}

    def probabilities_of(self, text: str) -> dict[str, float]:
        x = self.sparse(text)
        logits = [sum(row[i] * v for i, v in x.items()) + b for row, b in zip(self.head["coef"], self.head["intercept"])]
        top = max(logits)
        exp = [math.exp(z - top) for z in logits]
        total = sum(exp)
        return {c: e / total for c, e in zip(self.head["classes"], exp)}

    def classify(self, sentence: str) -> Result:
        probs = self.probabilities_of(sentence)
        act = max(probs, key=probs.get)
        return Result(act, probs[act], self.credit_allowed(act, probs[act], sentence, self.critical_mass(probs)), "tfidf", act in self.critical)
