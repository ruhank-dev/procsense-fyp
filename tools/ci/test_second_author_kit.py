"""The second-author kit (tools/bench/second_author_kit.py, EX-18): the instructions are made from the content and show no example sentence, a short or malformed return is refused with reasons, a sentence already in a repository set is refused (it would leak a development sentence into a frozen set), and a good return is accepted."""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
KIT = [sys.executable, str(ROOT / "tools" / "bench" / "second_author_kit.py")]


def run(*args):
    p = subprocess.run([*KIT, *args], capture_output=True, text=True, encoding="utf-8")
    return p.returncode, p.stdout + p.stderr


def good_files(d: Path):
    orders = ["text,kind,target"]
    for i in range(100):
        orders.append(f"zebra phrase {i} for the full blood count,test,fbc")
    for i in range(20):
        orders.append(f"unrelated remark number {i} about nothing in particular,,")
    clf = ["text,act,expect_credit"]
    for i in range(60):
        clf.append(f"first group sentence {i} about nothing but wording alpha,ASK_TOPIC,yes")
        clf.append(f"second group sentence {i} about wording beta,ASK_TOPIC,no")
        clf.append(f"third group sentence {i} about wording gamma,STATE_ALLERGY,no")
    (d / "o.csv").write_text("\n".join(orders) + "\n", encoding="utf-8")
    (d / "c.csv").write_text("\n".join(clf) + "\n", encoding="utf-8")


class Kit(unittest.TestCase):
    def test_the_sheet_lists_what_can_be_ordered_and_shows_no_example_sentence(self):
        with tempfile.TemporaryDirectory() as t:
            rc, out = run("sheet", "--out", t)
            self.assertEqual(rc, 0, out)
            text = (Path(t) / "INSTRUCTIONS.md").read_text(encoding="utf-8")
            self.assertIn("`fbc`", text)
            self.assertIn("STATE_ALLERGY", text)
            self.assertNotIn("Does that make sense so far?", text)

    def test_a_good_return_is_accepted_and_a_bad_one_is_refused_with_reasons(self):
        with tempfile.TemporaryDirectory() as t:
            d = Path(t)
            good_files(d)
            rc, out = run("check", "--orders", str(d / "o.csv"), "--classifier", str(d / "c.csv"))
            self.assertEqual(rc, 0, out)
            (d / "o.csv").write_text("text,kind,target\nshort,test,fbc\nanother,test,not_a_test\nsolo,test,\n", encoding="utf-8")
            rc, out = run("check", "--orders", str(d / "o.csv"), "--classifier", str(d / "c.csv"))
            self.assertEqual(rc, 1)
            for needle in ("not something that can be ordered", "both be given or both be empty", "too few orders"):
                self.assertIn(needle, out)

    def test_a_sentence_already_in_a_repository_set_is_refused(self):
        with tempfile.TemporaryDirectory() as t:
            d = Path(t)
            good_files(d)
            c = (d / "c.csv").read_text(encoding="utf-8") + "Does that make sense so far?,CHECK_UNDERSTANDING,yes\n"
            (d / "c.csv").write_text(c, encoding="utf-8")
            rc, out = run("check", "--orders", str(d / "o.csv"), "--classifier", str(d / "c.csv"))
            self.assertEqual(rc, 1)
            self.assertIn("already in a set of the repository", out)

    def test_wrong_columns_and_a_credit_for_a_critical_act_are_refused(self):
        with tempfile.TemporaryDirectory() as t:
            d = Path(t)
            good_files(d)
            (d / "c.csv").write_text("text,act,expect_credit\nsome words here,STATE_ALLERGY,yes\n", encoding="utf-8")
            rc, out = run("check", "--orders", str(d / "o.csv"), "--classifier", str(d / "c.csv"))
            self.assertIn("only an ordinary act can be credited", out)
            (d / "c.csv").write_text("sentence,act\nx,y\n", encoding="utf-8")
            rc, out = run("check", "--orders", str(d / "o.csv"), "--classifier", str(d / "c.csv"))
            self.assertNotEqual(rc, 0)


if __name__ == "__main__":
    unittest.main()
