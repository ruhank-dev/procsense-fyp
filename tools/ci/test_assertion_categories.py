"""The assertion layer, category by category (server/classify/assertion.py, ADR-0020): denied, intended, hypothetical, reported, said-by-someone-else and broken-off sentences are flagged; plain statements of what the speaker does or has done are not.
The sentences are ordinary English written for this test (not the benchmark's wrappers), and the test makes no clinical claim: it checks only whether the speaker performs the act in the sentence. Completed and uncompleted actions: "I have explained X" is done, "I have not explained X yet", "I was about to explain X but" and "I explained the... -" are not.
Also: the matcher of server/classify/rules.py is deterministic (the same sentence gives the same answer on every call) and never credits a flagged sentence."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify.assertion import non_assertive  # noqa: E402
from classify.rules import RuleActClassifier  # noqa: E402

FLAGGED = {
    "denied": ["I haven't explained the scan to you yet.", "I did not ask about your sleep.", "I'm not going to reassure you with empty words.", "I never got to confirm the plan with you.", "I have not clarified which side."],
    "hypothetical": ["If I were to explain the operation I would start with the camera.", "Imagine I asked you about your tablets.", "What if I told you the scan was normal?", "Suppose I checked your pulse now.", "Picture me explaining the mask."],
    "intended": ["Later I'll explain the test in more detail.", "Give me a minute and I'll check the plan with you.", "I'm planning to ask about your stairs after this.", "Shortly I shall reassure you about the pain."],
    "reported": ["The nurse said she reassured you earlier.", "Apparently the porter explained the scan.", "According to the chart, the registrar checked your pulse.", "My colleague says she asked about your sleep.", "Nobody told you what happens next."],
    "other_actor": ["The registrar will explain the scan for you.", "Dr Khan is going to ask you about your sleep.", "The consultant is about to reassure you.", "Let the senior nurse explain the plan."],
    "incomplete": ["Let me explain the operation and then I should -", "Do you understand what I..."],
}
NOT_FLAGGED = [
    "I have explained the procedure and the risks to you.", "I asked about your sleep and you said it was poor.", "I am going to put the cuff on your arm now.", "I explained the scan a moment ago.", "Could you tell me about your last meal?",
    "No need to panic, just breathe.", "Sorry, I didn't catch that, could you say it again?", "I can only imagine how frightening that must have been.", "Can you ask the senior nurse to come over?", "Okay, I will let you know as soon as the porter comes.",
    "I checked your pulse and it is regular.", "The surgeon will make a small cut and look inside with a camera.", "A porter will take you back to the ward.", "I reassured the family and they are happy to wait.",
]


class Categories(unittest.TestCase):
    def test_each_non_assertive_category_is_flagged(self):
        for kind, sentences in FLAGGED.items():
            for s in sentences:
                self.assertNotEqual(non_assertive(s), "", f"{kind}: {s}")

    def test_the_category_name_is_the_right_one_for_the_clear_cases(self):
        self.assertEqual(non_assertive("I haven't explained the scan to you yet."), "denied")
        self.assertEqual(non_assertive("If I were to explain the operation I would start with the camera."), "hypothetical")
        self.assertEqual(non_assertive("Let me explain the operation and then I should -"), "incomplete")
        self.assertEqual(non_assertive("Dr Khan is going to ask you about your sleep."), "other_actor")

    def test_statements_of_what_the_speaker_does_or_has_done_are_not_flagged(self):
        for s in NOT_FLAGGED:
            self.assertEqual(non_assertive(s), "", s)

    def test_a_flagged_sentence_is_never_credited_by_the_rules_and_the_rules_are_deterministic(self):
        r = RuleActClassifier()
        every = [s for v in FLAGGED.values() for s in v]
        for s in every:
            self.assertFalse(r.classify(s).credited, s)
        for s in every + NOT_FLAGGED:
            self.assertEqual(r.classify(s), r.classify(s), s)


if __name__ == "__main__":
    unittest.main()
