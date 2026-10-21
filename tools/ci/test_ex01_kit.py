"""The EX-01 phase-two kit (tools/bench/ex01_kit.py): the author brief shows no example sentence, coverage and the real-recording rule for ASR wording are enforced, the blind annotator files carry nothing but the scene and the sentence, disagreements are kept (never forced), a label is never invented, and the freeze refuses unless different people filled the roles, coverage is met and nothing awaits adjudication; a set changed after the freeze is detected.
Every sentence and label below is a TEST FIXTURE made by this file (placeholder text, labels derived mechanically from the fixture), never a dataset, never frozen into tests/ex01_semantic."""
import copy
import csv
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import ex01_kit as K  # noqa: E402
import rubric_labelling_kit as RK  # noqa: E402

N = 0


def sentence() -> str:
    global N
    N += 1
    return f"zebra fixture sentence number {N} about quartz and nothing clinical"


def authored() -> list[dict]:
    rows = []
    out_act = next(a for a in RK.ACTS if a not in RK.CRITICAL and a != "NONE" and K.window_of(a) and len(K.window_of(a)) < 15)
    out_step = next(s for s in range(1, 16) if s not in K.window_of(out_act))
    for fid, f in K.FAMILIES.items():
        for _ in range(f["min"]):
            r = {"item_id": f"a{len(rows) + 1}", "author_code": "A1" if len(rows) % 2 == 0 else "A2", "family": fid, "scenario_step": "3", "scenario_context": "a fixture scene", "prior_turns": "", "speaker_role": "trainee", "utterance": sentence(),
                 "patient_question_pending": "no", "readback_pending": "no", "asr_source": "", "author_intended_acts": "ASK_TOPIC"}
            if fid == "F12":
                r.update(prior_turns="patient: a fixture question", patient_question_pending="yes", author_intended_acts="ANSWER_QUESTION")
            if fid == "F13":
                r.update(prior_turns="colleague: a fixture order", readback_pending="yes", author_intended_acts="CLOSED_LOOP")
            if fid == "F16":
                r.update(scenario_step=str(out_step), author_intended_acts=out_act)
            rows.append(r)
    return rows


def labelled(blind: list[dict], tweak=None) -> list[dict]:
    out = []
    for r in blind:
        x = dict(r)
        x.update(acts_performed="ASK_TOPIC", actor="trainee", modality="actual", evidence=" ".join(r["utterance"].split()[:3]), credit_eligible="yes", ambiguous="no", definition_insufficient="no", note="")
        out.append(x)
    if tweak:
        tweak(out)
    return out


def write_csv(p: Path, cols, rows):
    K.write(p, cols, rows)


def read_norm(p):
    return p.read_text(encoding="utf-8").replace(chr(13) + chr(10), chr(10))


class Brief(unittest.TestCase):
    def test_the_brief_lists_every_family_and_act_and_no_sentence(self):
        with tempfile.TemporaryDirectory() as t:
            K.cmd_brief(Path(t))
            text = (Path(t) / "AUTHOR_BRIEF.md").read_text(encoding="utf-8")
            for fid in K.FAMILIES:
                self.assertIn(f"| {fid} |", text)
            for a in RK.ACTS:
                if a != "NONE":
                    self.assertIn(f"`{a}`", text)
            known = RK.known_sentences()
            lines = {RK.norm(x) for x in text.splitlines()}
            self.assertFalse(known & lines, "the brief must not repeat a repository sentence")
            self.assertIn("Do not invent typos", text)
            for f in ("author_template.csv", "annotator_template.csv", "roles_record.template.json", "specification_issues.template.csv"):
                self.assertTrue((Path(t) / f).exists())
            with open(Path(t) / "annotator_template.csv", encoding="utf-8") as fh:
                self.assertEqual(next(csv.reader(fh)), K.ANNOT_COLUMNS)

    def test_the_committed_brief_and_templates_are_current(self):
        with tempfile.TemporaryDirectory() as t:
            K.cmd_brief(Path(t))
            for f in ("AUTHOR_BRIEF.md", "author_template.csv", "annotator_template.csv", "roles_record.template.json", "specification_issues.template.csv"):
                self.assertEqual(read_norm(Path(t) / f), read_norm(ROOT / "docs" / "testing" / "ex01_templates" / f), f)

    def test_the_design_covers_the_families_the_owner_named(self):
        names = {f["name"] for f in K.DESIGN["families"]}
        for want in ("ordinary_paraphrase", "indirect_natural_speech", "question_vs_assertion", "request_vs_completion_claim", "hypothetical", "reported_speech", "negation", "actor_recipient", "escalation_recommendation_handover_wording",
                     "emotion_acknowledged_vs_reassured", "explain_action_vs_explain_procedure", "answer_with_patient_question_context", "closed_loop_readback_context", "critical_topic_discussion", "adversarial_or_misleading_wording", "out_of_window_valid_speech", "asr_evidence"):
            self.assertIn(want, names)
        self.assertEqual(K.FAMILIES["F17"]["min"], 0)
        self.assertFalse(K.FAMILIES["F17"]["allowed_in_phase_two"])
        self.assertEqual(sum(f["min"] for f in K.DESIGN["families"]), 300)
        self.assertEqual(K.DESIGN["target_items_total"], 300)
        heavy = [f for f in K.DESIGN["families"] if f["safety_sensitive"]]
        self.assertGreater(sum(f["min"] for f in heavy), 150)                # the safety-sensitive families carry more than half
        self.assertEqual(K.FAMILIES["F09"]["min"], max(f["min"] for f in K.DESIGN["families"] if f["id"] not in ("F14",)))


class AuthorsCheck(unittest.TestCase):
    def test_a_good_file_passes(self):
        bad, cov = K.check_authors(authored())
        self.assertEqual(bad, [])
        self.assertGreaterEqual(sum(cov.values()), K.DESIGN["min_items_total"])

    def problems(self, mutate):
        rows = authored()
        mutate(rows)
        return K.check_authors(rows)[0]

    def test_each_rule_is_enforced(self):
        self.assertTrue(any("coverage: family F05" in b for b in self.problems(lambda r: [r.remove(x) for x in [y for y in r if y["family"] == "F05"][:2]])))
        leak = sorted(RK.known_sentences())[5]
        self.assertTrue(any("leak" in b for b in self.problems(lambda r: r[0].update(utterance=leak))))
        self.assertTrue(any("same sentence appears twice" in b for b in self.problems(lambda r: r[1].update(utterance=r[0]["utterance"]))))
        self.assertTrue(any("out of scope in this phase" in b for b in self.problems(lambda r: r[0].update(family="F17"))))
        self.assertTrue(any("asr_source must be empty" in b for b in self.problems(lambda r: r[0].update(asr_source="clip-1"))))
        self.assertTrue(any("author_code" in b for b in self.problems(lambda r: r[0].update(author_code="Dr Smith, the nurse"))))
        self.assertTrue(any("far above" in b for b in self.problems(lambda r: r.extend({**r[0], "item_id": f"x{n}", "utterance": sentence()} for n in range(6)))))
        self.assertTrue(any("F12 needs" in b for b in self.problems(lambda r: next(x for x in r if x["family"] == "F12").update(patient_question_pending="no"))))
        self.assertTrue(any("F13 needs" in b for b in self.problems(lambda r: next(x for x in r if x["family"] == "F13").update(prior_turns=""))))
        self.assertTrue(any("F16 needs" in b for b in self.problems(lambda r: next(x for x in r if x["family"] == "F16").update(scenario_step=str(sorted(K.window_of("ASK_TOPIC"))[0]), author_intended_acts="ASK_TOPIC"))))
        self.assertTrue(any("scenario_step" in b for b in self.problems(lambda r: r[0].update(scenario_step="99"))))
        self.assertTrue(any("prior_turns" in b for b in self.problems(lambda r: r[0].update(prior_turns="nobody: x"))))
        self.assertTrue(any("author_intended_acts" in b for b in self.problems(lambda r: r[0].update(author_intended_acts="MADE_UP"))))
        self.assertTrue(any("minimum is" in b for b in self.problems(lambda r: [r.pop() for _ in range(5)])))

    def test_real_asr_wording_is_out_of_scope_for_this_phase_and_documented_so(self):
        plan = (ROOT / "docs" / "testing" / "ex01_semantic_evaluation_plan.md").read_text(encoding="utf-8")
        self.assertIn("independently of speech-recognition quality", plan)
        self.assertIn("separate later phase", plan)
        brief = K.brief_text()
        self.assertIn("independently of speech-recognition quality", brief)
        self.assertIn("no deliberate spelling damage", brief)

    def test_a_near_copy_of_a_repository_sentence_is_refused_without_showing_the_repository_sentence(self):
        known = sorted(k for k in RK.known_sentences() if len(k.split()) >= 6)[3]
        words = known.split()
        near = " ".join(words[:-1] + ["zebra"])
        rows = authored()
        rows[0]["utterance"] = near
        bad = K.check_authors(rows)[0]
        self.assertTrue(any("near-copy" in b for b in bad))
        self.assertFalse(any(known in b for b in bad))
        unrelated = authored()
        self.assertFalse(any("near-copy" in b for b in K.check_authors(unrelated)[0]))


class AuthorPackages(unittest.TestCase):
    def test_each_author_gets_a_pre_structured_file_with_no_sentence_and_the_quotas_add_up(self):
        with tempfile.TemporaryDirectory() as t:
            res = K.author_packages(2, Path(t))
            self.assertEqual(res["per_author_totals"], [150, 150])
            self.assertEqual(res["family_totals"], {fid: f["min"] for fid, f in K.FAMILIES.items()})
            for code in ("A1", "A2"):
                d = Path(t) / f"author_{code}"
                self.assertEqual(sorted(x.name for x in d.iterdir()), ["AUTHOR_BRIEF.md", "AUTHOR_INSTRUCTIONS.md", "author_template.csv"])
                rows = K.read(d / "author_template.csv")
                self.assertEqual(len(rows), 150)
                self.assertTrue(all(r["utterance"] == "" and r["author_code"] == code and r["family"] in K.FAMILIES and r["family"] != "F17" for r in rows))
                self.assertEqual(len({r["item_id"] for r in rows}), 150)
                text = (d / "AUTHOR_INSTRUCTIONS.md").read_text(encoding="utf-8")
                self.assertIn("do not look for, copy or rewrite any existing benchmark", text)
                self.assertIn("no deliberate typos", text)
                self.assertFalse(RK.known_sentences() & {RK.norm(x) for x in text.splitlines()})
            ids = [r["item_id"] for c in ("A1", "A2") for r in K.read(Path(t) / f"author_{c}" / "author_template.csv")]
            self.assertEqual(len(ids), len(set(ids)))
        with self.assertRaises(SystemExit):
            K.author_packages(1, Path(tempfile.gettempdir()) / "ex01_one_author_must_fail")

    def test_the_committed_author_packages_are_current(self):
        with tempfile.TemporaryDirectory() as t:
            K.author_packages(2, Path(t))
            for code in ("A1", "A2"):
                for f in ("AUTHOR_BRIEF.md", "AUTHOR_INSTRUCTIONS.md", "author_template.csv"):
                    self.assertEqual(read_norm(Path(t) / f"author_{code}" / f), read_norm(ROOT / "docs" / "testing" / "ex01_author_packages" / f"author_{code}" / f), f)

    def test_three_authors_share_the_quota_evenly_and_combine_checks_the_returned_files(self):
        q = K.split_quota(3)
        self.assertEqual(sum(sum(x.values()) for x in q), 300)
        self.assertLessEqual(max(sum(x.values()) for x in q) - min(sum(x.values()) for x in q), 2)
        with tempfile.TemporaryDirectory() as t:
            rows = authored()
            a1 = [r for r in rows if r["author_code"] == "A1"]
            a2 = [r for r in rows if r["author_code"] == "A2"]
            K.write(Path(t) / "a1.csv", K.AUTHOR_COLUMNS, a1)
            K.write(Path(t) / "a2.csv", K.AUTHOR_COLUMNS, a2)
            res = K.combine([Path(t) / "a1.csv", Path(t) / "a2.csv"], Path(t) / "all.csv")
            self.assertEqual(res, {"items": 300, "authors": 2})
            self.assertEqual(K.check_authors(K.read(Path(t) / "all.csv"))[0], [])
            a1[0]["utterance"] = ""
            K.write(Path(t) / "bad.csv", K.AUTHOR_COLUMNS, a1)
            with self.assertRaises(SystemExit):
                K.combine([Path(t) / "bad.csv", Path(t) / "a2.csv"], Path(t) / "x.csv")
            K.write(Path(t) / "mixed.csv", K.AUTHOR_COLUMNS, rows[:10])
            with self.assertRaises(SystemExit):
                K.combine([Path(t) / "mixed.csv"], Path(t) / "x.csv")


class Pipeline(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.d = Path(self.tmp.name)
        self.rows = authored()
        write_csv(self.d / "authors.csv", K.AUTHOR_COLUMNS, self.rows)
        self.pkg = self.d / "pkg"
        K.package(self.d / "authors.csv", self.pkg, 7)
        self.blindA, self.blindB = K.read(self.pkg / "annotator_A.csv"), K.read(self.pkg / "annotator_B.csv")

    def tearDown(self):
        self.tmp.cleanup()

    def test_the_blind_files_hold_only_the_scene_and_the_sentence(self):
        for f in ("annotator_A.csv", "annotator_B.csv"):
            with open(self.pkg / f, encoding="utf-8") as fh:
                head = next(csv.reader(fh))
            self.assertEqual(head, K.ANNOT_COLUMNS)
            self.assertFalse({"family", "author_intended_acts", "asr_source", "patient_question_pending", "readback_pending"} & set(head))
            text = (self.pkg / f).read_text(encoding="utf-8")
            self.assertNotIn("author_intended", text)
        self.assertEqual({r["item_id"] for r in self.blindA}, {r["item_id"] for r in self.blindB})
        self.assertTrue(all(r["item_id"].startswith("X") for r in self.blindA))
        self.assertNotEqual([r["item_id"] for r in self.blindA], [r["item_id"] for r in self.blindB])
        self.assertTrue(all(r["acts_performed"] == "" for r in self.blindA))
        self.assertEqual(json.loads((self.pkg / "id_map.json").read_text(encoding="utf-8"))["note"], "KEPT BY THE OWNER; never given to an annotator")

    def test_packaging_is_reproducible_and_refuses_a_bad_authors_file(self):
        other = self.d / "pkg2"
        K.package(self.d / "authors.csv", other, 7)
        self.assertEqual((other / "annotator_A.csv").read_text(encoding="utf-8"), (self.pkg / "annotator_A.csv").read_text(encoding="utf-8"))
        bad = self.rows[:-30]
        write_csv(self.d / "short.csv", K.AUTHOR_COLUMNS, bad)
        with self.assertRaises(SystemExit):
            K.package(self.d / "short.csv", self.d / "pkg3", 7)

    def test_annotator_checks(self):
        good = labelled(self.blindA)
        self.assertEqual(K.check_annotator(good, self.blindA), [])
        c = copy.deepcopy(good)
        c[0]["utterance"] = "changed"
        self.assertTrue(any("changed" in b for b in K.check_annotator(c, self.blindA)))
        c = copy.deepcopy(good)
        c[1]["evidence"] = "words that are not there"
        self.assertTrue(any("evidence" in b for b in K.check_annotator(c, self.blindA)))
        c = copy.deepcopy(good)
        c[2]["definition_insufficient"] = "yes"
        self.assertTrue(any("needs a note" in b for b in K.check_annotator(c, self.blindA)))
        self.assertTrue(any("not labelled" in b for b in K.check_annotator(good[:-1], self.blindA)))
        c = copy.deepcopy(good)
        c[3]["acts_performed"] = ""
        self.assertTrue(any("empty" in b for b in K.check_annotator(c, self.blindA)))
        c = copy.deepcopy(good)
        c[4]["credit_eligible"] = "no"
        self.assertTrue(any("approved rule" in b for b in K.check_annotator(c, self.blindA)))

    def make(self, a_tweak=None, b_tweak=None, adjudicate=None):
        A = labelled(self.blindA, a_tweak)
        B = labelled(self.blindB, b_tweak)
        write_csv(self.d / "A.csv", K.ANNOT_COLUMNS, A)
        write_csv(self.d / "B.csv", K.ANNOT_COLUMNS, B)
        J = []
        if adjudicate:
            by = {r["item_id"]: r for r in labelled(self.blindA)}
            for i, f in adjudicate.items():
                row = dict(by[i])
                f(row)
                J.append(row)
        write_csv(self.d / "J.csv", K.ANNOT_COLUMNS, J)
        return K.merge(self.d / "A.csv", self.d / "B.csv", self.d / "J.csv", self.d / "authors.csv", self.pkg / "id_map.json", self.d / "merged")

    def test_disagreements_are_kept_and_nothing_is_invented(self):
        ids = sorted(r["item_id"] for r in self.blindA)
        a_tweak = lambda rows: next(r for r in rows if r["item_id"] == ids[0]).update(acts_performed="EXPLAIN_ACTION")      # noqa: E731
        b_tweak = lambda rows: (next(r for r in rows if r["item_id"] == ids[1]).update(acts_performed="CHECK_UNDERSTANDING"), next(r for r in rows if r["item_id"] == ids[2]).update(ambiguous="yes", note="two readings"),      # noqa: E731
                                next(r for r in rows if r["item_id"] == ids[3]).update(definition_insufficient="yes", note="the definitions do not say"))
        res = self.make(a_tweak, b_tweak, adjudicate={ids[0]: lambda r: r.update(acts_performed="ASK_TOPIC"), ids[2]: lambda r: r.update(ambiguous="yes", note="two readings"), ids[3]: lambda r: r.update(definition_insufficient="yes", note="the definitions do not say")})
        self.assertEqual(res["disagreements"], 4)
        self.assertEqual(res["awaiting_adjudication"], 1)                                   # ids[1]: differs and no adjudicator row: never forced
        self.assertEqual(res["specification_issues"], 1)
        items, excl = K.read(self.d / "merged" / "items.csv"), K.read(self.d / "merged" / "excluded.csv")
        self.assertEqual(len(items) + len(excl), len(self.rows))
        self.assertNotIn(ids[1], {r["item_id"] for r in items})
        reasons = {e["item_id"]: e["reason"] for e in excl}
        self.assertEqual(reasons[ids[1]], "awaiting adjudication")
        self.assertEqual(reasons[ids[3]], "definition insufficient")
        self.assertEqual(reasons[ids[2]], "ambiguous")
        self.assertEqual(next(r for r in items if r["item_id"] == ids[0])["gold_status"], "adjudicated")
        dis = K.read(self.d / "merged" / "disagreements.csv")
        self.assertEqual({r["item_id"] for r in dis}, set(ids[:4]))
        self.assertTrue(all(r["a_acts"] != r["b_acts"] or r["a_credit"] != r["b_credit"] or r["fields"] for r in dis))
        ag = json.loads((self.d / "merged" / "agreement.json").read_text(encoding="utf-8"))
        self.assertIn(ids[3], ag["definition_insufficient"])
        self.assertTrue(all(r["family"] in K.FAMILIES for r in items))

    def test_the_summary_reports_everything_the_owner_must_see_before_the_freeze(self):
        ids = sorted(r["item_id"] for r in self.blindA)
        a_tweak = lambda rows: next(r for r in rows if r["item_id"] == ids[0]).update(acts_performed="EXPLAIN_ACTION")      # noqa: E731
        b_tweak = lambda rows: next(r for r in rows if r["item_id"] == ids[3]).update(definition_insufficient="yes", note="the definitions do not say")      # noqa: E731
        self.make(a_tweak, b_tweak, adjudicate={ids[0]: lambda r: r.update(acts_performed="ASK_TOPIC"), ids[3]: lambda r: r.update(definition_insufficient="yes", note="the definitions do not say")})
        roles = self.freeze_args()
        out = K.summary(self.d / "merged", roles)
        self.assertEqual((out["items_authored"], out["items_scored"], out["items_excluded"]), (300, 299, 1))
        self.assertEqual(out["excluded_by_reason"], {"definition insufficient": 1})
        self.assertEqual(sum(v["authored"] for v in out["by_family"].values()), 300)
        self.assertTrue(all(v["quota_met"] for v in out["by_family"].values()))
        self.assertEqual(out["annotator_disagreement"]["items_with_any_difference"], 2)
        self.assertEqual(out["annotator_disagreement"]["rate"], round(2 / 300, 4))
        self.assertEqual(sum(out["adjudication_outcomes"].values()), 2)
        self.assertEqual(out["definition_insufficient"]["count"], 1)
        self.assertEqual(out["leak_check"], {"exact_repository_matches": 0, "near_copies": 0, "items_checked": 300})
        self.assertEqual(sum(out["composition_scored"].values()), 299)
        self.assertEqual(out["by_objective_gold_acts_scored"].get("ASK_TOPIC"), 299)
        chk = {c["requirement"]: c["satisfied"] for c in out["independence_checklist"]}
        self.assertTrue(all(v for k, v in chk.items() if "saw a model prediction" not in k))
        self.assertIsNone(chk["no author, annotator or adjudicator saw a model prediction, probability, guard decision or intended output"])
        self.assertIn("pending the owner", out["every_independence_requirement_satisfied"])
        self.assertTrue((self.d / "merged" / "summary.md").exists())
        bad = K.summary(self.d / "merged", self.freeze_args(authors=["Ann Author"]))
        self.assertTrue(bad["roles_problems"])
        self.assertFalse(bad["every_independence_requirement_satisfied"] is True)

    def freeze_args(self, **over):
        roles = {"set": "ex01-semantic", "attested_by": "Owner Person", "attested_on": "2026-11-01", "authors": ["Ann Author", "Bo Author"], "annotators": ["Cy Annotator", "Di Annotator"], "adjudicator": "Ed Adjudicator",
                 "builder_names": ["Fay Builder"], "statement": "fixture statement"}
        roles.update(over)
        p = self.d / "roles.json"
        p.write_text(json.dumps(roles), encoding="utf-8")
        return p

    def test_freeze_refuses_until_the_people_and_the_coverage_are_right_and_detects_later_changes(self):
        self.make()
        merged, out = self.d / "merged", self.d / "frozen"
        with self.assertRaises(SystemExit):
            K.freeze(merged, self.freeze_args(authors=["Ann Author"]), out)
        with self.assertRaises(SystemExit):
            K.freeze(merged, self.freeze_args(annotators=["Cy Annotator", "Ann Author"]), out)
        with self.assertRaises(SystemExit):
            K.freeze(merged, self.freeze_args(builder_names=["ed adjudicator"]), out)
        with self.assertRaises(SystemExit):
            K.freeze(merged, self.freeze_args(authors=["<person>", "<person>"]), out)
        self.assertFalse((out / "manifest.json").exists())
        man = K.freeze(merged, self.freeze_args(), out)
        self.assertTrue(man["independent"] and man["scored_runs"] == 0 and man["version"] == "ex01-semantic-1")
        self.assertEqual(set(man["files"]), set(K.FILES))
        self.assertEqual(K.verify(out), [])
        (out / "items.csv").write_text((out / "items.csv").read_text(encoding="utf-8") + "\n", encoding="utf-8")
        self.assertTrue(any("changed after the set was frozen" in b for b in K.verify(out)))
        self.assertTrue(any("not frozen" in b for b in K.verify(self.d / "nothing")))

    def test_freeze_refuses_when_an_item_still_awaits_an_adjudicator(self):
        ids = sorted(r["item_id"] for r in self.blindA)
        self.make(b_tweak=lambda rows: next(r for r in rows if r["item_id"] == ids[0]).update(acts_performed="CHECK_UNDERSTANDING"))
        with self.assertRaises(SystemExit):
            K.freeze(self.d / "merged", self.freeze_args(), self.d / "frozen")

    def test_the_repository_set_is_not_created_by_the_tests(self):
        self.assertFalse((K.DEFAULT_SET / "manifest.json").exists() and "fixture" in (K.DEFAULT_SET / "manifest.json").read_text(encoding="utf-8").lower())


if __name__ == "__main__":
    unittest.main()
