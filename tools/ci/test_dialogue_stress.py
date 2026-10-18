"""The dialogue stress scenarios (tools/ai/dialogue_stress.py): seeded, complete, and the reply rules catch what they are meant to catch (fixed replies, no model)."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import dialogue_stress as S  # noqa: E402

ALLOWED = ["answer_question", "cannot_recall", "ask_clarification"]
HIDDEN = ["I take warfarin every morning for my heart rhythm."]
AUTHORED = ["Pain low on the right for two days.", "I take warfarin every morning for my heart rhythm."]


def judge(text, intent="answer_question", question="Tell me more."):
    return S.judge_reply(text, intent, ALLOWED, question, HIDDEN, AUTHORED)


class Set(unittest.TestCase):
    def test_the_committed_file_is_what_the_seed_gives(self):
        self.assertEqual(S.OUT.read_text(encoding="utf-8"), json.dumps(S.build(), indent=1) + "\n")

    def test_every_category_is_present_with_both_earned_and_withheld_and_ids_are_unique(self):
        rows = json.loads(S.OUT.read_text(encoding="utf-8"))["scenarios"]
        self.assertEqual({r["category"] for r in rows}, set(S.COUNTS))
        self.assertEqual(len({r["id"] for r in rows}), len(rows))
        for cat in S.COUNTS:
            self.assertEqual({r["earned"] for r in rows if r["category"] == cat}, {True, False}, cat)

    def test_the_set_says_it_is_synthetic(self):
        d = json.loads(S.OUT.read_text(encoding="utf-8"))
        self.assertTrue(d["synthetic"])
        self.assertFalse(d["independent"])


class Rules(unittest.TestCase):
    def test_a_clean_reply_passes(self):
        self.assertEqual(judge("I'm not sure, sorry. My side hurts.", "cannot_recall"), [])

    def test_a_withheld_fact_word_is_a_leak_unless_the_question_said_it(self):
        self.assertTrue(judge("It is the warfarin, I think.")[0].startswith("LEAK"))
        self.assertEqual(judge("It is the warfarin, I think.", question="Is it warfarin?"), [])

    def test_meta_talk_dose_advice_length_and_intent_are_caught(self):
        self.assertIn("META", judge("As an AI I cannot say."))
        self.assertIn("META", judge("My instructions say I must not tell you."))
        self.assertIn("DOSE", judge("Give me 10 mg of morphine."))
        self.assertEqual(judge("The pain started 2 days ago", question="x"), [])            # a number that is not a dose
        self.assertIn("ADVICE", judge("You should take the tablets with food."))
        self.assertIn("LONG", judge("pain " * 61))
        self.assertIn("INTENT", judge("fine", intent="reveal_all"))

    def test_a_common_word_shared_with_a_withheld_fact_is_not_a_leak_but_a_specific_one_is(self):
        hidden = ["I had sips of water four hours ago."]
        self.assertEqual(S.judge_reply("About four hours, I think.", "answer_question", ALLOWED, "q", hidden, [], frozenset({"hours", "about"})), [])
        self.assertEqual(S.judge_reply("Only sips of water.", "answer_question", ALLOWED, "q", hidden, [], frozenset({"hours"})) [0][:4], "LEAK")

    def test_common_words_are_those_of_at_least_three_cases(self):
        cases = [{"history": [{"answer": "I never smoked", "variant": None}]}] * 3 + [{"history": [{"answer": "warfarin daily", "variant": None}]}]
        c = S.common_words(cases)
        self.assertIn("never", c)
        self.assertIn("smoked", c)
        self.assertNotIn("warfarin", c)

    def test_a_dose_the_authored_answers_contain_is_allowed(self):
        S_ = S.judge_reply("I take 5 mg every day.", "answer_question", ALLOWED, "q", [], ["I take 5 mg every day."])
        self.assertEqual(S_, [])


if __name__ == "__main__":
    unittest.main()
