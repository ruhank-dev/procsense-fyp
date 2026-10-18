"""The plain-Python TF-IDF classifier (server/classify/tfidf.py): it reproduces scikit-learn's vectoriser, it shares the embedding classifier's safety rules, and the committed head fits the taxonomy and the mapping.

scikit-learn is not in the CI environment, so the parity test runs only where it is installed (`uv run --with scikit-learn`); the arithmetic is also checked against numbers worked out by hand.
"""
import math
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify import classifier as LLM  # noqa: E402
from classify import tfidf as T  # noqa: E402
from classify.embedding import load_head  # noqa: E402

TAX = LLM.taxonomy()["acts"]
IDS = [a["id"] for a in TAX]
CRITICAL = {a["id"] for a in TAX if a["critical"]}


def tiny_head(vocab, idf, winner, threshold=0.5, cap=None):
    coef = [[6.0] * len(vocab) if c == winner else [0.0] * len(vocab) for c in IDS]
    head = {"kind": "tfidf_logreg_head.v1", "dimension": len(vocab), "classes": IDS, "vocabulary": vocab, "idf": idf, "coef": coef, "intercept": [0.0] * len(IDS),
            "thresholds": {c: (None if c in CRITICAL or c == "NONE" else threshold) for c in IDS}, "_critical": CRITICAL}
    if cap is not None:
        head["critical_mass_max"] = cap
    return head


class Arithmetic(unittest.TestCase):
    def test_words_and_neighbouring_pairs_are_counted_like_the_scikit_learn_analyser(self):
        f = T.features("Any pain? Any PAIN!")
        self.assertEqual(f["any"], 2)
        self.assertEqual(f["pain"], 2)
        self.assertEqual(f["any pain"], 2)
        self.assertEqual(f["pain any"], 1)
        self.assertNotIn("a", T.features("a b"))                    # one-letter words are not tokens

    def test_the_weights_are_sublinear_tf_times_idf_scaled_to_length_one(self):
        clf = T.TfidfActClassifier(tiny_head(["pain", "any", "any pain"], [2.0, 1.5, 3.0], "ASK_TOPIC"))
        v = clf.sparse("pain pain any")                              # pain twice, any once, no pair "any pain" in this order
        raw = {0: (1 + math.log(2)) * 2.0, 1: 1.0 * 1.5}
        norm = math.sqrt(sum(x * x for x in raw.values()))
        self.assertEqual(set(v), {0, 1})
        for i in raw:
            self.assertAlmostEqual(v[i], raw[i] / norm, places=9)
        self.assertAlmostEqual(sum(x * x for x in v.values()), 1.0, places=9)
        self.assertEqual(clf.sparse("nothing known here"), {})

    def test_a_sentence_with_no_known_word_gets_the_intercepts_only(self):
        clf = T.TfidfActClassifier(tiny_head(["pain"], [1.0], "ASK_TOPIC"))
        probs = clf.probabilities_of("zzz")
        self.assertAlmostEqual(sum(probs.values()), 1.0, places=9)
        self.assertAlmostEqual(max(probs.values()), 1.0 / len(IDS), places=9)


class Safety(unittest.TestCase):
    def test_an_ordinary_act_above_its_threshold_is_credited_and_the_rules_of_the_embedding_classifier_hold(self):
        clf = T.TfidfActClassifier(tiny_head(["pain", "any"], [1.0, 1.0], "ASK_TOPIC"))
        r = clf.classify("Any pain?")
        self.assertEqual((r.act, r.credited, r.source, r.critical), ("ASK_TOPIC", True, "tfidf", False))
        self.assertFalse(T.TfidfActClassifier(tiny_head(["pain", "any"], [1.0, 1.0], "STATE_ALLERGY")).classify("Any pain?").credited)       # a critical act is never credited
        self.assertFalse(T.TfidfActClassifier(tiny_head(["pain", "any"], [1.0, 1.0], "NONE")).classify("Any pain?").credited)
        self.assertFalse(clf.classify("Any pain, and is the wristband right?").credited)                                                  # a critical cue blocks credit under an ordinary act

    def test_the_critical_mass_cap_blocks_credit_when_the_head_may_think_it_critical(self):
        head = tiny_head(["pain", "any"], [1.0, 1.0], "ASK_TOPIC", cap=0.5)
        head["coef"] = [[6.0, 6.0] if c in ("ASK_TOPIC", "STATE_ALLERGY") else [0.0, 0.0] for c in IDS]       # the head splits its belief between an ordinary and a critical act
        r = T.TfidfActClassifier(head).classify("Any pain?")
        self.assertFalse(r.credited)

    def test_a_head_that_disagrees_with_itself_is_refused(self):
        head = tiny_head(["pain", "any"], [1.0], "ASK_TOPIC")
        with self.assertRaises(ValueError):
            T.TfidfActClassifier(head)


class CommittedHead(unittest.TestCase):
    def test_it_fits_the_taxonomy_the_mapping_and_says_what_its_data_was(self):
        head = load_head(T.HEAD_TFIDF)
        self.assertEqual(head["kind"], "tfidf_logreg_head.v1")
        self.assertEqual(len(head["vocabulary"]), head["dimension"])
        self.assertEqual(len(head["idf"]), head["dimension"])
        self.assertFalse(head["valid_for_claims"])
        self.assertIn("NOT INDEPENDENT", head["provenance"])
        self.assertTrue(all(head["thresholds"][c] is None for c in CRITICAL | {"NONE"}))
        clf = T.TfidfActClassifier()
        self.assertEqual(clf.classify("Can you tell me your full name and date of birth?").act, "CONFIRM_IDENTITY")
        self.assertFalse(clf.classify("Can you tell me your full name and date of birth?").credited)

    def test_it_matches_scikit_learn_where_that_is_installed(self):
        try:
            from sklearn.feature_extraction.text import TfidfVectorizer
        except ImportError:
            self.skipTest("scikit-learn is not installed")
        head = load_head(T.HEAD_TFIDF)
        vec = TfidfVectorizer(ngram_range=(1, 2), lowercase=True, sublinear_tf=True, vocabulary=head["vocabulary"])
        vec.fit(["x"])
        vec.idf_ = __import__("numpy").array(head["idf"])
        clf = T.TfidfActClassifier()
        for text in ("Could you tell me about your last meal?", "I will put the cuff on your arm now.", "Penicillin gave you a rash, I will note it.", "Hmm, where did I put my pen."):
            ref = vec.transform([text])
            mine = clf.sparse(text)
            for i, v in zip(ref.indices, ref.data):
                self.assertAlmostEqual(mine.get(int(i), 0.0), float(v), places=6)


if __name__ == "__main__":
    unittest.main()
