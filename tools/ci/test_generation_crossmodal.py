"""Cross-modal consistency and the package gate (server/generation/crossmodal.py and package.py, p1-live-scenario-generation-s07, stages LG-09 and LG-10)."""
import copy
import csv
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from generation import contracts, crossmodal as cm, media, objectives as ob, package, retrieve, skeleton  # noqa: E402

sys.path.insert(0, str(ROOT / "tools"))
from composer import blocks as B  # noqa: E402

CASES = {c["id"]: c for c in B.load_cases()}
PICS, REPS = media.pictures(), media.reports()


def media_of(case_id, case=None):
    return media.select_media(copy.deepcopy(case or CASES[case_id]), case_id, 1, PICS, REPS)


def slots(*codes):
    tests = {"ECG": "ecg", "US-AP": "ultrasound_appendix", "CXR": "chest_xray", "CT-AB": "ct_abdomen_midlevel", "PREG": "pregnancy_test", "CT-AP": "ct_appendix_level"}
    return {"slots": [{"test": next(v for k, v in tests.items() if c.startswith(k)), "finding_code": c, "picture_id": "x", "measurements": {}} for c in codes]}


def rules_hit(case, m):
    return {d.rule for d in cm.check(case, m)}


class Rules(unittest.TestCase):
    def test_all_authored_cases_pass_every_rule_with_their_own_pictures(self):
        bad = {cid: cm.check(c, media_of(cid)) for cid, c in CASES.items()}
        self.assertEqual({k: v for k, v in bad.items() if v}, {})

    def test_the_rule_file_and_the_code_agree_and_cite_register_rows(self):
        doc = json.loads((ROOT / "content" / "generation" / "crossmodal_rules.json").read_text(encoding="utf-8"))
        self.assertEqual([r["id"] for r in doc["rules"]], list(cm.RULES))
        register = {r["id"] for r in csv.DictReader((ROOT / "docs" / "research" / "source_register.csv").open(encoding="utf-8"))}
        for r in doc["rules"]:
            self.assertTrue(r["refs"] and set(r["refs"]) <= register, r["id"])

    # one positive and one negative fixture for each rule (the positive one is an authored case)
    def test_VR_CM_01_ecg_rhythm_against_the_pulse(self):
        c = copy.deepcopy(CASES["VAR-S1-D"])
        m = media_of("VAR-S1-D", c)
        self.assertEqual(rules_hit(c, m), set())
        c["observations"][0]["heart_rhythm"] = "regular"
        self.assertIn("VR-CM-01", rules_hit(c, m))
        c2 = copy.deepcopy(CASES["VAR-S1-A"])
        m2 = {"slots": [{"test": "ecg", "finding_code": "ECG-01", "picture_id": "x", "measurements": {"regular": "yes"}}]}
        c2["observations"][0]["heart_rhythm"] = "irregular"
        self.assertIn("VR-CM-01", rules_hit(c2, m2))

    def test_VR_CM_02_potassium_against_the_ecg(self):
        c = copy.deepcopy(CASES["STK-S1-C"])
        self.assertEqual(rules_hit(c, media_of("STK-S1-C", c)), set())
        for r in c["results"]:
            if r["test"].startswith("Potassium"):
                r["value"], r["flag"] = "4.2", "normal"
        self.assertIn("VR-CM-02", rules_hit(c, media_of("STK-S1-C", CASES["STK-S1-C"])))
        k = copy.deepcopy(CASES["STK-S1-C"])
        self.assertIn("VR-CM-02", rules_hit(k, slots("ECG-01")))               # K 6.6 with a normal strip
        j = CASES["VAR-S1-J"]
        self.assertEqual(rules_hit(j, slots("ECG-01")), set())                  # K 6.4 haemolysed with a normal strip is the teaching case

    def test_VR_CM_03_scan_against_inflammatory_markers(self):
        c = next(c for c in CASES.values() if media.slots_of(c["id"]) and any(s["finding_code"] == "US-AP-01" for s in media.slots_of(c["id"])) and any(r["test"].startswith(("WBC", "CRP")) for r in c["results"]))
        self.assertEqual(rules_hit(c, slots("US-AP-01")), set())
        c = copy.deepcopy(c)
        for r in c["results"]:
            if r["test"].startswith(("WBC", "CRP")):
                r["flag"] = "normal"
        self.assertIn("VR-CM-03", rules_hit(c, slots("US-AP-01")))

    def test_VR_CM_04_free_air_against_the_abdomen(self):
        c = copy.deepcopy(CASES["SC-8"])
        self.assertEqual(rules_hit(c, slots("CXR-08")), set())
        for e in c["examination"]:
            if e["system"] == "abdomen":
                e["finding"], e["abnormal"] = "Soft and not tender.", False
        self.assertIn("VR-CM-04", rules_hit(c, slots("CXR-08")))

    def test_VR_CM_05_faulty_sample_against_its_repeat(self):
        c = copy.deepcopy(CASES["VAR-S1-J"])
        self.assertEqual(rules_hit(c, {"slots": []}), set())
        c["results"] = [r for r in c["results"] if "(repeat" not in r["test"].lower()]
        self.assertIn("VR-CM-05", rules_hit(c, {"slots": []}))

    def test_VR_CM_06_lactate_against_the_patients_state(self):
        c = copy.deepcopy(CASES["SC-8"])
        self.assertEqual(rules_hit(c, {"slots": []}), set())
        c["observations"][0]["news2"] = 0
        self.assertIn("VR-CM-06", rules_hit(c, {"slots": []}))

    def test_VR_CM_07_pregnancy_test_against_sex_and_age(self):
        f = copy.deepcopy(CASES["VAR-S1-H"])
        self.assertEqual(rules_hit(f, slots("PREG-01")), set())
        m = copy.deepcopy(f)
        m["identity"]["sex"] = "M"
        self.assertIn("VR-CM-07", rules_hit(m, slots("PREG-01")))
        old = copy.deepcopy(f)
        old["identity"]["age"] = 70
        self.assertIn("VR-CM-07", rules_hit(old, slots("PREG-00")))

    def test_VR_CM_08_order_of_times(self):
        c = copy.deepcopy(CASES["VAR-S1-B"])
        self.assertEqual(rules_hit(c, {"slots": []}), set())
        c["observations"][0]["time"], c["observations"][1]["time"] = c["observations"][1]["time"], c["observations"][0]["time"]
        self.assertIn("VR-CM-08", rules_hit(c, {"slots": []}))
        d = copy.deepcopy(CASES["VAR-S1-A"])
        d["results"][0]["minutes_to_result"] = -5 if d["results"] else 0
        if d["results"]:
            self.assertIn("VR-CM-08", rules_hit(d, {"slots": []}))


class Gate(unittest.TestCase):
    tables, index = ob.Tables.load(), retrieve.build_index()

    def make(self, seed, scenario="S1"):
        req = contracts.accept_request({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": scenario, "level": "advanced", "seed": seed}, lambda n: 0)
        o = ob.derive(req, [], self.tables)
        return package.assemble(skeleton.build_skeleton(o, self.index, list(CASES.values()), self.tables), o, self.index), o

    def test_assembled_packages_are_valid_and_carry_their_provenance(self):
        for seed in range(12):
            pkg, o = self.make(seed, "S1" if seed % 2 == 0 else "S2")
            self.assertEqual(contracts.errors("scenario_package", {k: v for k, v in pkg.items() if k not in ("media", "discovery")}), [])
            self.assertEqual(pkg["provenance"]["content_hash"], self.index.content_hash)
            self.assertTrue(pkg["provenance"]["packs"])
            report = package.validate(pkg, list(CASES.values()))
            self.assertTrue(report["passed"], report["defects"][:3])

    def test_the_gate_catches_a_break_at_each_stage(self):
        pkg, _ = self.make(3)
        broken = copy.deepcopy(pkg)
        broken["case"]["identity"]["age"] += 9
        r = package.validate(broken, list(CASES.values()))
        self.assertFalse(r["passed"])
        self.assertTrue(any(d["stage"] == "stage_b" for d in r["defects"]))
        broken = copy.deepcopy(pkg)
        broken["status"] = "APPROVED_FOR_TRAINING"
        self.assertTrue(any(d["stage"] == "package" for d in package.validate(broken, list(CASES.values()))["defects"]))
        broken = copy.deepcopy(pkg)
        broken["media"]["slots"] = [{"test": "ultrasound_gallbladder", "finding_code": "US-GB-02", "picture_id": next(p["id"] for p in PICS if p["finding_code"] == "US-GB-01")}]
        self.assertTrue(any(d["stage"] == "media" for d in package.validate(broken, list(CASES.values()))["defects"]))
        broken = copy.deepcopy(pkg)
        broken["case"]["observations"][0]["heart_rhythm"] = "irregular"
        broken["media"]["slots"] = [{"test": "ecg", "finding_code": "ECG-01", "picture_id": "ecg-01-r0", "measurements": {"regular": "yes"}}]
        self.assertTrue(any(d["stage"] == "crossmodal" for d in package.validate(broken, list(CASES.values()))["defects"]))

    def test_every_cross_modal_mutation_of_a_generated_package_is_caught(self):
        pkg, _ = self.make(5)
        mutations = {
            "VR-CM-01": lambda p: (p["case"]["observations"][0].update(heart_rhythm="irregular"), p["media"].update(slots=[{"test": "ecg", "finding_code": "ECG-01", "picture_id": "x", "measurements": {"regular": "yes"}}])),
            "VR-CM-07": lambda p: (p["case"]["identity"].update(sex="M"), p["media"].update(slots=[{"test": "pregnancy_test", "finding_code": "PREG-01", "picture_id": "x", "measurements": {}}])),
            "VR-CM-08": lambda p: p["case"]["observations"].append({**p["case"]["observations"][0], "time": "07:00"}),
        }
        for rule, mutate in mutations.items():
            m = copy.deepcopy(pkg)
            mutate(m)
            hit = {d["code"] for d in package.validate(m, list(CASES.values()))["defects"]}
            self.assertIn(rule, hit, rule)


if __name__ == "__main__":
    unittest.main()
