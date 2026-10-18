"""remote_status.classify: a run GitHub never started is never a pass and never a code failure."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import remote_status as rs  # noqa: E402

RAN = [{"conclusion": "success", "steps": [{}, {}]}]


class Classify(unittest.TestCase):
    def test_success_with_ran_jobs_is_a_pass(self):
        self.assertEqual(rs.classify("success", RAN), "passed")

    def test_failure_with_no_steps_is_never_started(self):
        self.assertEqual(rs.classify("failure", [{"conclusion": "failure", "steps": []}] * 3), "never_started")

    def test_failure_with_steps_is_a_code_failure(self):
        self.assertEqual(rs.classify("failure", [{"conclusion": "failure", "steps": [{}]}]), "failed")

    def test_no_job_information_is_not_a_pass(self):
        self.assertNotEqual(rs.classify("success", []), "passed")

    def test_skipped_and_other(self):
        self.assertEqual(rs.classify("skipped", []), "skipped")
        self.assertEqual(rs.classify("cancelled", RAN), "other")


if __name__ == "__main__":
    unittest.main()
