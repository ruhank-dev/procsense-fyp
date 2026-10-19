"""The deterministic matcher, the rules-then-model hybrid and the benchmark builder (server/classify/rules.py, llm_hybrid.py, tools/bench/build_act_dataset.py; ADR-0020):
the rules credit one act or none, a critical-adjacent topic, a denial, a hypothetical or a self-correction is never credited, the model can only propose and every proposal is validated, a rule refusal on safety grounds is final,
a down or lying model gives no credit, and the benchmark files are the ones the builder makes, split by family with no sentence in both splits."""
import json
import subprocess
import sys
import unittest
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.llm_hybrid import RulesThenModel  # noqa: E402
from classify.rules import RuleActClassifier  # noqa: E402
from llm import client as C  # noqa: E402

R = RuleActClassifier()


def host(answer):
    calls = []

    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        calls.append(1)
        return httpx.Response(200, json={"message": {"content": json.dumps(answer)}})
    return C.OllamaClient(transport=httpx.MockTransport(h)), calls


U = "Honestly this is going to be absolutely fine, truly."          # no rule covers it, so the model is asked
GOOD = {"act": "REASSURE", "assertion": "actual", "actor": "trainee", "evidence": "absolutely fine", "unsure": False}


class Rules(unittest.TestCase):
    def test_a_plain_sentence_is_credited_under_its_one_act(self):
        for text, act in (("Does that make sense so far?", "CHECK_UNDERSTANDING"), ("Do you mean the left side or the right?", "CLARIFY"), ("The call bell is not working in this bay.", "REPORT_FAULT")):
            r = R.classify(text)
            self.assertEqual((r.act, r.credited), (act, True), text)

    def test_nothing_critical_or_critical_adjacent_is_credited(self):
        for text in ("Which medicines should we keep away from you?", "Has your tummy been empty since last night?", "Is it still your wish to have the operation?", "The numbers on the monitor are heading the wrong way fast.", "I'm calling about the man in bed six."):
            self.assertFalse(R.classify(text).credited, text)

    def test_a_denial_a_hypothetical_a_report_a_broken_off_sentence_and_a_correction_earn_nothing(self):
        for text in ("I haven't asked about your sleep yet.", "If I were to explain the scan, I would start slowly.", "The nurse said she reassured you.", "Does that make sense and then I should -", "Do you understand, no, I mean, do you follow?"):
            self.assertFalse(R.classify(text).credited, text)

    def test_two_acts_in_one_sentence_or_none_gives_no_credit(self):
        self.assertFalse(R.classify("Hmm, let me think.").credited)
        self.assertEqual(R.classify("Hmm, let me think.").refused, "NO_RULE")
        self.assertTrue(R.classify("Does that make sense, and I will call you when the porter arrives?").refused.startswith("RULES_DISAGREE") or not R.classify("Does that make sense, and I will call you when the porter arrives?").credited)

    def test_the_matcher_never_names_a_critical_act(self):
        import act_dataset_seeds as S
        for fams in S.CRITICAL.values():
            for _, surfaces in fams:
                for s in surfaces:
                    r = R.classify(s)
                    self.assertNotIn(r.act, R.critical)
                    self.assertFalse(r.credited, s)


class Hybrid(unittest.TestCase):
    def test_a_valid_proposal_for_an_undecided_sentence_is_credited_after_one_model_call(self):
        client, calls = host(GOOD)
        r = RulesThenModel(client).classify(U)
        self.assertEqual((r.act, r.credited, len(calls)), ("REASSURE", True, 1))

    def test_every_check_of_the_validation_can_refuse(self):
        client, _ = host(GOOD)
        m = RulesThenModel(client)
        s = U
        for change, reason in (({"assertion": "intended"}, "MODEL_ASSERTION"), ({"actor": "other"}, "MODEL_ACTOR"), ({"unsure": True}, "MODEL_UNSURE"), ({"evidence": "not in the sentence"}, "EVIDENCE"), ({"act": "NONE"}, "MODEL_NONE")):
            self.assertTrue(m.validate(s, {**GOOD, **change}).startswith(reason), change)
        self.assertEqual(m.validate(s, GOOD), "")

    def test_a_rule_refusal_on_safety_grounds_is_final_and_the_model_is_not_asked(self):
        client, calls = host(GOOD)
        r = RulesThenModel(client).classify("Which medicines should we keep away from you?")
        self.assertFalse(r.credited)
        self.assertEqual(len(calls), 0)

    def test_the_model_cannot_name_a_critical_act_and_a_down_model_gives_nothing(self):
        client, _ = host({**GOOD, "act": "STATE_ALLERGY"})
        self.assertFalse(RulesThenModel(client).classify(U).credited)

        def down(request):
            raise httpx.ConnectError("down")
        r = RulesThenModel(C.OllamaClient(transport=httpx.MockTransport(down))).classify(U)
        self.assertFalse(r.credited)
        self.assertFalse(RulesThenModel(None).classify(U).credited)


class Benchmark(unittest.TestCase):
    def test_the_committed_files_are_what_the_builder_makes_and_the_splits_do_not_overlap(self):
        r = subprocess.run([sys.executable, str(ROOT / "tools" / "bench" / "build_act_dataset.py"), "--check"], capture_output=True, text=True)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        dev = [json.loads(l) for l in (ROOT / "tests" / "action_bench_v1" / "dev.jsonl").read_text(encoding="utf-8").splitlines()]
        test = [json.loads(l) for l in (ROOT / "tests" / "action_bench_v1" / "test.jsonl").read_text(encoding="utf-8").splitlines()]
        self.assertFalse({r["family"] for r in dev} & {r["family"] for r in test})
        self.assertFalse({r["text"] for r in dev} & {r["text"] for r in test})
        self.assertTrue(all(not r["expect_credit"] for r in dev + test if r["critical"] or r["status"] != "actual"))


if __name__ == "__main__":
    unittest.main()
