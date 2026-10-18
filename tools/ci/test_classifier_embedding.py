"""The embedding + logistic regression communication classifier and the critical-content guard (server/classify/embedding.py, guard.py; docs/research/classifier_benchmark).

No model is called: a fake embedder stands in for Ollama. What is checked: the head must fit the taxonomy and the mapping; a critical act or NONE is never credited whatever its probability; an act needs its own
threshold; a sentence that touches critical content is never credited under a non-critical act (the cases the language-model classifier got wrong in the benchmark are replayed as regressions); a down embedder credits
nothing; the committed head is consistent with the taxonomy and says what its data was."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify import classifier as LLM  # noqa: E402
from classify import embedding as E  # noqa: E402
from classify.guard import critical_cue  # noqa: E402

TAX = LLM.taxonomy()["acts"]
IDS = [a["id"] for a in TAX]
CRITICAL = {a["id"] for a in TAX if a["critical"]}


def tiny_head(winner: str, threshold: float | None = 0.5) -> dict:
    """A 3-number head in which `winner` is the class a vector of [1, 0, 0] points at, with a large margin."""
    coef = [[8.0, 0.0, 0.0] if c == winner else [0.0, 0.0, 0.0] for c in IDS]
    return {"kind": "embedding_logreg_head.v1", "dimension": 3, "classes": IDS, "coef": coef, "intercept": [0.0] * len(IDS),
            "thresholds": {c: (None if c in CRITICAL or c == "NONE" else threshold) for c in IDS}, "_critical": CRITICAL}


def fixed(vec):
    return lambda text: vec


class Guard(unittest.TestCase):
    def test_critical_content_is_recognised(self):
        for text, kind in (("Please note she is allergic to penicillin", "allergy"), ("The wristband says a different name", "identity"), ("Please sign the form if you agree", "consent"),
                           ("He is not fasted, he had toast", "fasting"), ("Her blood pressure has been dropping for an hour", "worsening"), ("Situation: Mr Khan for a hernia repair", "handover")):
            self.assertEqual(critical_cue(text), kind, text)

    def test_ordinary_sentences_pass(self):
        for text in ("Can you tell me about your breathing?", "I am going to listen to your chest now.", "Sorry, can you say that again?", "It usually takes about an hour.", "Hmm."):
            self.assertEqual(critical_cue(text), "", text)


class Head(unittest.TestCase):
    def classify(self, act, sentence, threshold=0.5, vec=(1.0, 0.0, 0.0)):
        return E.EmbeddingActClassifier(fixed(list(vec)), tiny_head(act, threshold)).classify(sentence)

    def test_an_ordinary_act_above_its_threshold_is_credited(self):
        r = self.classify("ASK_TOPIC", "Can you tell me about your last meal?")
        self.assertEqual((r.act, r.credited, r.critical, r.source), ("ASK_TOPIC", True, False, "embedding"))
        self.assertGreater(r.confidence, 0.9)

    def test_an_act_without_a_threshold_or_below_it_is_not_credited(self):
        self.assertFalse(self.classify("ASK_TOPIC", "Can you tell me about your last meal?", threshold=None).credited)
        self.assertFalse(self.classify("ASK_TOPIC", "Can you tell me about your last meal?", threshold=0.9999999).credited)

    def test_a_critical_act_is_recorded_and_never_credited_whatever_its_probability(self):
        for act in sorted(CRITICAL):
            head = tiny_head(act)
            head["thresholds"][act] = 0.0                                # even a threshold of zero cannot buy credit
            r = E.EmbeddingActClassifier(fixed([1.0, 0.0, 0.0]), head).classify("x")
            self.assertEqual((r.act, r.critical, r.credited), (act, True, False), act)

    def test_none_is_never_credited(self):
        head = tiny_head("NONE")
        head["thresholds"]["NONE"] = 0.0
        self.assertFalse(E.EmbeddingActClassifier(fixed([1.0, 0.0, 0.0]), head).classify("Hmm.").credited)

    def test_a_sentence_about_critical_content_is_never_credited_under_an_ordinary_act(self):
        for sentence in ("Please note she is allergic to penicillin.", "Let me check your wristband against the notes.", "Are you happy to go ahead? Please sign.", "He is not fasted."):
            r = self.classify("EXPLAIN_ACTION", sentence)
            self.assertEqual((r.act, r.credited), ("EXPLAIN_ACTION", False), sentence)

    def test_the_critical_mass_cap_removes_credit_when_the_head_thinks_the_sentence_may_be_critical(self):
        head = tiny_head("ASK_TOPIC")
        head["critical_mass_max"] = 0.2
        coef = [[8.0, 0.0, 0.0] if c == "ASK_TOPIC" else [0.0, 0.0, 0.0] for c in IDS]
        head["coef"] = coef
        clf = E.EmbeddingActClassifier(fixed([1.0, 0.0, 0.0]), head)
        probs = clf.probabilities([1.0, 0.0, 0.0])
        self.assertLess(clf.critical_mass(probs), 0.2)                                      # a clear ordinary act: nothing critical in the mass
        self.assertTrue(clf.classify("Any pain?").credited)
        self.assertTrue(clf.credit_allowed("ASK_TOPIC", 0.9, "Any pain?", mass=0.1))
        self.assertFalse(clf.credit_allowed("ASK_TOPIC", 0.9, "Any pain?", mass=0.3))        # over the cap: recorded, not credited
        self.assertTrue(clf.credit_allowed("ASK_TOPIC", 0.9, "Any pain?", mass=None))        # a caller that gives no mass is not blocked by it
        del head["critical_mass_max"]
        self.assertTrue(E.EmbeddingActClassifier(fixed([1.0, 0.0, 0.0]), head).credit_allowed("ASK_TOPIC", 0.9, "Any pain?", mass=0.9))      # a head made before the cap existed behaves as before

    def test_the_embedder_being_down_credits_nothing(self):
        for embedder in (None, lambda t: None, lambda t: [1.0, 2.0]):
            r = E.EmbeddingActClassifier(embedder, tiny_head("ASK_TOPIC")).classify("x")
            self.assertEqual((r.credited, r.source), (False, "unavailable"))

    def test_a_head_that_does_not_fit_the_taxonomy_or_the_mapping_is_refused(self):
        with tempfile.TemporaryDirectory() as t:
            good = json.loads((ROOT / "content" / "assessment" / "act_head.json").read_text(encoding="utf-8")) if (ROOT / "content" / "assessment" / "act_head.json").exists() else None
            if good is None:
                self.skipTest("no committed head")
            p = Path(t) / "h.json"
            p.write_text(json.dumps({**good, "classes": good["classes"][:-1]}), encoding="utf-8")
            with self.assertRaises(E.HeadError):
                E.load_head(p)
            old = E.MAPPING
            m = json.loads(old.read_text(encoding="utf-8"))
            m["critical_acts"] = [a for a in m["critical_acts"] if a != "SBAR_ELEMENT"]
            (Path(t) / "m.json").write_text(json.dumps(m), encoding="utf-8")
            p.write_text(json.dumps(good), encoding="utf-8")
            E.MAPPING = Path(t) / "m.json"
            try:
                with self.assertRaises(E.HeadError):
                    E.load_head(p)
            finally:
                E.MAPPING = old

    def test_the_embedder_asks_ollama_for_one_sentence_and_survives_a_bad_answer(self):
        import httpx
        calls = []

        def handler(request):
            calls.append(json.loads(request.content))
            return httpx.Response(200, json={"embeddings": [[0.1, 0.2, 0.3]]})
        emb = E.OllamaEmbedder("http://x", "all-minilm:22m", transport=httpx.MockTransport(handler))
        self.assertEqual(emb("hello"), [0.1, 0.2, 0.3])
        self.assertEqual((calls[0]["model"], calls[0]["input"]), ("all-minilm:22m", ["hello"]))
        self.assertIsNone(E.OllamaEmbedder("http://x", transport=httpx.MockTransport(lambda r: httpx.Response(500))) ("hello"))
        self.assertIsNone(E.OllamaEmbedder("http://x", transport=httpx.MockTransport(lambda r: httpx.Response(200, json={"oops": 1}))) ("hello"))


class CommittedHead(unittest.TestCase):
    path = ROOT / "content" / "assessment" / "act_head.json"

    def setUp(self):
        if not self.path.exists():
            self.skipTest("the head has not been exported yet")
        self.head = E.load_head(self.path)

    def test_it_fits_the_taxonomy_the_mapping_and_the_embedding_model(self):
        self.assertEqual(sorted(self.head["classes"]), sorted(IDS))
        self.assertEqual((self.head["dimension"], self.head["embedding_model"]), (384, "all-minilm:22m"))
        self.assertTrue(self.head["normalised_input"])

    def test_critical_acts_and_none_have_no_threshold_and_the_others_a_sane_one(self):
        for act, t in self.head["thresholds"].items():
            if act in CRITICAL or act == "NONE":
                self.assertIsNone(t, act)
            else:
                self.assertTrue(t is None or 0.0 < t <= 1.0, act)

    def test_it_says_its_data_was_synthetic_and_not_independent(self):
        self.assertFalse(self.head["valid_for_claims"])
        self.assertIn("NOT INDEPENDENT", self.head["provenance"])


class QwenRegressions(unittest.TestCase):
    """The sentences where a critical act was said and the language-model classifier's answer would have credited a different, ordinary act (benchmark: tests/classifier_synthetic/qwen_critical_cases.json): the
    real ActClassifier.classify is run against a fake Ollama that gives the answer the model gave, with the most generous thresholds, and must not credit any of them."""
    path = ROOT / "tests" / "classifier_synthetic" / "qwen_critical_cases.json"

    @staticmethod
    def host(act, conf):
        import httpx
        from llm import client as C

        def h(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": []})
            return httpx.Response(200, json={"message": {"content": json.dumps({"act": act, "confidence": conf})}})
        return C.OllamaClient(transport=httpx.MockTransport(h))

    def test_none_of_them_is_credited_by_the_language_model_classifier(self):
        if not self.path.exists():
            self.skipTest("no cases recorded yet")
        cases = json.loads(self.path.read_text(encoding="utf-8"))["cases"]
        self.assertGreater(len(cases), 0)
        for c in cases:
            self.assertNotIn(c["predicted"], CRITICAL)                   # the model named an ordinary act for a critical sentence
            clf = LLM.ActClassifier(self.host(c["predicted"], 0.99), thresholds={"per_act": {c["predicted"]: 0.1}})
            r = clf.classify(c["text"])
            self.assertEqual(r.act, c["predicted"])
            self.assertFalse(r.credited, c)

    def test_the_same_answers_are_credited_when_the_sentence_touches_nothing_critical(self):
        clf = LLM.ActClassifier(self.host("ASK_TOPIC", 0.99), thresholds={"per_act": {"ASK_TOPIC": 0.1}})
        self.assertTrue(clf.classify("Can you tell me about your breathing?").credited)


if __name__ == "__main__":
    unittest.main()
