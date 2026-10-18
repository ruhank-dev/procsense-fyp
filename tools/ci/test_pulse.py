"""The Pulse adapter's data (tools/pulse): the mapping is consistent with the Core's effects and variables, every recorded trace can be started by a Core effect, the parity page is what the traces give, and the PC
build record does not claim a headset result."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "pulse"))
import parity  # noqa: E402
import pulse_bridge as B  # noqa: E402

TRACES = ROOT / "content" / "physio" / "pulse_traces"


class Data(unittest.TestCase):
    def test_the_mapping_and_traces_are_consistent(self):
        self.assertEqual(B.problems(), [])

    def test_every_trace_is_started_by_an_effect_the_core_can_apply(self):
        effects = {e["id"]: e for e in json.loads((ROOT / "content" / "physio" / "effects.json").read_text(encoding="utf-8"))["effects"]}
        for f in TRACES.glob("PS-*.json"):
            t = json.loads(f.read_text(encoding="utf-8"))
            anchor = t["actions"][0]["key"]
            self.assertTrue(effects[anchor]["targets"], f.name)          # an effect with targets reaches PhysioEffect and Apply
            self.assertEqual(t["engine"]["name"], "Pulse Physiology Engine")
            self.assertTrue(t["recorded_on"].startswith("the development desktop"))

    def test_the_parity_page_is_what_the_traces_give(self):
        for name, text in parity.build().items():
            self.assertEqual((parity.OUT / name).read_text(encoding="utf-8"), text, name)

    def test_the_parity_report_does_not_claim_a_reviewer_or_a_headset(self):
        r = json.loads((parity.OUT / "results.json").read_text(encoding="utf-8"))
        self.assertIn("No reviewer has judged", r["note"])
        md = (parity.OUT / "parity.md").read_text(encoding="utf-8")
        self.assertIn("headset figure is Sannan's", md)

    def test_unmapped_effects_have_reasons(self):
        m = B.mapping()
        self.assertTrue(all(isinstance(v, str) and len(v) > 10 for v in m["unmapped"].values()))


if __name__ == "__main__":
    unittest.main()
