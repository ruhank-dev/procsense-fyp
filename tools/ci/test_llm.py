"""The model-call layer against a fake Ollama (server/llm/client.py, prompts.py, gateway.py, narrative.py): no model is needed, so this runs everywhere (p1-ai-language-s02, p1-live-scenario-generation-s04)."""
import json
import sys
import unittest
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT))
from llm import client as C, gateway, prompts  # noqa: E402
from generation import narrative  # noqa: E402

SCHEMA_LINE = {"type": "object", "required": ["text"], "additionalProperties": False, "properties": {"text": {"type": "string", "minLength": 1, "maxLength": 400}}}


class FakeOllama:
    """Answers /api/tags and /api/chat from a script: each chat call pops the next scripted behaviour."""

    def __init__(self, script):
        self.script, self.calls = list(script), []

    def __call__(self, request: httpx.Request) -> httpx.Response:
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": [{"name": "m", "digest": "abc123"}]})
        body = json.loads(request.content)
        self.calls.append(body)
        step = self.script.pop(0) if self.script else {"text": json.dumps({"text": "fallthrough"})}
        if "raise" in step:
            raise step["raise"]
        if "status" in step:
            return httpx.Response(step["status"], text=step.get("body", ""))
        return httpx.Response(200, json={"message": {"content": step["text"], "thinking": step.get("thinking", "")}, "prompt_eval_count": 10, "eval_count": 5})


def client_for(*script):
    fake = FakeOllama(script)
    return C.OllamaClient(transport=httpx.MockTransport(fake)), fake


class Client(unittest.TestCase):
    def test_a_good_answer_carries_the_digest_and_the_parsed_json(self):
        c, fake = client_for({"text": '{"text": "hello"}'})
        r = c.chat("m", "sys", "user", schema=SCHEMA_LINE)
        self.assertTrue(r.ok)
        self.assertEqual((r.json, r.digest, r.output_tokens), ({"text": "hello"}, "abc123", 5))
        self.assertEqual(fake.calls[0]["think"], False)
        self.assertEqual(fake.calls[0]["format"], SCHEMA_LINE)
        self.assertEqual(fake.calls[0]["options"]["num_ctx"], 4096)

    def test_every_failure_is_a_reply_with_a_code_and_never_an_exception(self):
        for step, code in (({"raise": httpx.ConnectError("down")}, "UNREACHABLE"), ({"raise": httpx.ReadTimeout("slow")}, "TIMEOUT"), ({"status": 404, "body": "model not found"}, "MODEL_MISSING"),
                           ({"status": 500, "body": "boom"}, "HTTP_ERROR"), ({"text": "not json at all"}, "NOT_JSON"), ({"text": "  "}, "EMPTY")):
            c, _ = client_for(step)
            r = c.chat("m", "s", "u", schema=SCHEMA_LINE)
            self.assertFalse(r.ok, code)
            self.assertEqual(r.error, code)
            self.assertIn(code, C.ERRORS)

    def test_health_says_whether_the_host_answers(self):
        c, _ = client_for()
        self.assertTrue(c.health()["up"])
        down = C.OllamaClient(transport=httpx.MockTransport(lambda r: (_ for _ in ()).throw(httpx.ConnectError("x"))))
        self.assertEqual(down.health()["up"], False)


class Prompts(unittest.TestCase):
    def test_the_registry_is_whole(self):
        self.assertEqual(prompts.check(), [])

    def test_render_fills_the_slots_and_refuses_a_wrong_set(self):
        p = prompts.get("compose.narrative")
        system, user = p.render(style="brief", age="40", sex_word="man", topic="allergies", original="I am allergic to penicillin.")
        self.assertIn("allergic to penicillin", user)
        self.assertNotIn("{{", system + user)
        with self.assertRaises(prompts.PromptError):
            p.render(style="brief")
        with self.assertRaises(prompts.PromptError):
            p.render(style="x", age="1", sex_word="m", topic="t", original="o", extra="e")

    def test_a_changed_prompt_file_without_a_new_version_is_refused(self):
        p = prompts.PROMPTS / "compose" / "v1.txt"
        original = p.read_bytes()
        try:
            p.write_bytes(original + b"\nextra words\n")
            with self.assertRaises(prompts.PromptError):
                prompts.get("compose.narrative", 1)
        finally:
            p.write_bytes(original)
        self.assertEqual(prompts.check(), [])


class Gateway(unittest.TestCase):
    def test_a_valid_answer_passes_and_the_audit_holds_hashes_not_text(self):
        c, _ = client_for({"text": '{"text": "I am allergic to penicillin, it made my throat swell."}'})
        r = gateway.run(c, "compose.narrative", dict(style="brief", age="40", sex_word="man", topic="allergies", original="Penicillin: my throat swelled."), model="m")
        self.assertTrue(r.ok and not r.fallback)
        a = r.audit
        self.assertEqual((a["outcome"], a["attempts"], a["model_digest"], a["task"]), ("ok", 1, "abc123", "compose.narrative"))
        self.assertNotIn("penicillin", json.dumps(a).lower())
        self.assertEqual(len(a["input_hash"]), 16)

    def test_a_bad_answer_gets_one_retry_with_the_reasons_and_then_falls_back(self):
        bad = {"text": '{"text": "x"}'}
        c, fake = client_for(bad, bad)
        r = gateway.run(c, "compose.narrative", dict(style="s", age="1", sex_word="man", topic="t", original="o"), validate=lambda v: ["NUMBERS: wrong"], model="m")
        self.assertFalse(r.ok)
        self.assertTrue(r.fallback)
        self.assertEqual(len(fake.calls), 2)
        self.assertIn("NUMBERS: wrong", fake.calls[1]["messages"][1]["content"])
        self.assertEqual(r.audit["outcome"], "fallback")

    def test_a_schema_violation_is_a_defect(self):
        c, _ = client_for({"text": '{"wrong": 1}'}, {"text": '{"text": "fixed"}'})
        r = gateway.run(c, "compose.narrative", dict(style="s", age="1", sex_word="man", topic="t", original="o"), model="m")
        self.assertTrue(r.ok)
        self.assertEqual(len(r.attempts), 2)
        self.assertTrue(r.attempts[0]["defects"][0].startswith("SCHEMA"))

    def test_a_host_that_is_down_is_not_retried(self):
        c, fake = client_for({"raise": httpx.ConnectError("down")}, {"text": '{"text": "never reached"}'})
        r = gateway.run(c, "compose.narrative", dict(style="s", age="1", sex_word="man", topic="t", original="o"), model="m")
        self.assertTrue(r.fallback)
        self.assertEqual(len(fake.calls), 1)


class Narrative(unittest.TestCase):
    CASE = {"identity": {"sex": "M", "age": 60}, "reason_answer": "I am here for my gallbladder operation tomorrow.",
            "history": [{"id": "h1", "topic": "past_history", "answer": "I take 5 mg of amlodipine every day for my blood pressure.", "variant": None},
                        {"id": "h2", "topic": "smoking", "answer": "No.", "variant": None},
                        {"id": "h3", "topic": "medicines", "answer": "Only the tablets I had in the other bed.", "variant": "B2"}]}

    def test_lines_of_takes_long_answers_of_the_base_path_only(self):
        self.assertEqual([x["id"] for x in narrative.lines_of(self.CASE)], ["reason_answer", "h1"])

    def test_defects_catch_a_changed_number_a_new_medicine_a_dropped_fact_and_a_prescription(self):
        lex = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))
        o = "I take 5 mg of amlodipine every day for my blood pressure."
        self.assertEqual(narrative.defects(o, "Every day I take 5 mg of amlodipine for my blood pressure.", lex), [])
        self.assertTrue(any(d.startswith("NUMBERS") for d in narrative.defects(o, "I take 10 mg of amlodipine every day for my blood pressure.", lex)))
        self.assertTrue(any(d.startswith("NEW_ENTITY") for d in narrative.defects(o, "I take 5 mg of amlodipine and penicillin every day for my blood pressure.", lex)))
        self.assertTrue(any(d.startswith("DROPPED_ENTITY") for d in narrative.defects(o, "I take 5 mg of something every day for my blood pressure.", lex)))
        self.assertTrue(any(d.startswith("PRESCRIBE") for d in narrative.defects(o, "I recommend you prescribe 5 mg of amlodipine every day for my blood pressure.", lex)))

    def test_defects_catch_what_a_real_run_let_through(self):
        lex = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))
        self.assertTrue(any(d.startswith("TIME") for d in narrative.defects("No anaesthetic problems in the family.", "No anaesthetic problems in the family, and it is only about ten minutes since I arrived.", lex)))
        self.assertTrue(any(d.startswith("NUMBER_WORD") for d in narrative.defects("Pain in my right side.", "Pain in my right side, for a couple of days.", lex)))
        self.assertTrue(any(d.startswith("NEGATION") for d in narrative.defects("No previous anaesthetics.", "I have had an anaesthetic before.", lex)))
        self.assertTrue(any(d.startswith("FACTS_LOST") for d in narrative.defects("No previous anaesthetics.", "Nothing like that ever happened.", lex)))

    def test_accepted_lines_replace_the_authored_ones_and_a_failure_keeps_them(self):
        import copy
        case = copy.deepcopy(self.CASE)
        c, _ = client_for({"text": '{"text": "Well, I am here for my gallbladder operation tomorrow."}'},
                          {"text": '{"text": "I take 9 mg of amlodipine."}'}, {"text": '{"text": "I take 9 mg of amlodipine."}'})
        report = narrative.Narrator(c, model="m").apply(case, 7)
        by = {x["id"]: x for x in report["lines"]}
        self.assertEqual(by["reason_answer"]["method"], "model")
        self.assertIn("Well,", case["reason_answer"])
        self.assertEqual(by["h1"]["method"], "template")
        self.assertEqual(case["history"][0]["answer"], self.CASE["history"][0]["answer"])
        self.assertEqual(len(report["persona"]), 2)

    def test_without_a_model_every_line_stays_as_authored(self):
        import copy
        case = copy.deepcopy(self.CASE)
        report = narrative.Narrator(None).apply(case, 1)
        self.assertEqual(case, self.CASE)
        self.assertTrue(all(x["method"] == "template" for x in report["lines"]))


class Image(unittest.TestCase):
    def test_the_server_image_carries_what_the_gateway_reads(self):
        docker = (ROOT / "server" / "Dockerfile").read_text(encoding="utf-8")
        ignore = (ROOT / "server" / "Dockerfile.dockerignore").read_text(encoding="utf-8")
        for need in ("COPY prompts ./prompts", "COPY content/ai ./content/ai"):
            self.assertIn(need, docker)
        for need in ("!prompts/**", "!content/ai/**"):
            self.assertIn(need, ignore)


class Hosts(unittest.TestCase):
    def test_check_hosts_names_what_to_fix_and_passes_a_ready_host(self):
        sys.path.insert(0, str(ROOT / "tools" / "ai"))
        import check_hosts
        full = {"qwen3.5:4b-q4_K_M": "d8b0f5e9760cd1682034f292d7ef72ec46f432149be0df7574bf2d6e92e38c04", "qwen3.5:9b-q4_K_M": "56671c2ab9385f9cfcb404638e32cd62d88e3501d44822208363c010179a3c90", "all-minilm:22m": "1b226e2802db" + "0" * 52}

        def host(models):
            return C.OllamaClient(transport=httpx.MockTransport(lambda r: httpx.Response(200, json={"models": [{"name": n, "digest": d} for n, d in models.items()]})))
        down = C.OllamaClient(transport=httpx.MockTransport(lambda r: (_ for _ in ()).throw(httpx.ConnectError("x"))))
        r = check_hosts.check(client=down, which=lambda x: "x", find_spec=lambda n: object())
        self.assertFalse(r["ready"])
        self.assertTrue(any("does not answer" in p for p in r["problems"]))
        r = check_hosts.check(client=host({"qwen3.5:4b-q4_K_M": full["qwen3.5:4b-q4_K_M"]}), which=lambda x: "x", find_spec=lambda n: object())
        self.assertTrue(any("9b-q4_K_M is not installed" in p for p in r["problems"]))
        r = check_hosts.check(client=host({**full, "qwen3.5:4b-q4_K_M": "0" * 64}), which=lambda x: "x", find_spec=lambda n: object())
        self.assertTrue(any("licence register records" in p for p in r["problems"]))
        r = check_hosts.check(client=host(full), which=lambda x: None, find_spec=lambda n: None)
        self.assertTrue(any("faster-whisper" in p for p in r["problems"]) and any("ffmpeg" in p for p in r["problems"]))


class InPipeline(unittest.TestCase):
    """The narrator inside the generation pipeline, against a fake host that rewrites each line by prefixing a harmless word."""

    @classmethod
    def setUpClass(cls):
        sys.path.insert(0, str(ROOT / "tools"))
        from composer import blocks as B
        from generation import objectives as ob, retrieve
        cls.cases = B.load_cases()
        cls.base = dict(index=retrieve.build_index(), cases=cls.cases, tables=ob.Tables.load())

    def request(self, seed):
        return {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced", "seed": seed}

    @staticmethod
    def echo(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": [{"name": "qwen3.5:9b-q4_K_M", "digest": "d9"}]})
        user = json.loads(request.content)["messages"][1]["content"]
        original = user.split("Original line: ", 1)[1].splitlines()[0]
        return httpx.Response(200, json={"message": {"content": json.dumps({"text": "Well, " + original[0].lower() + original[1:]})}, "eval_count": 7})

    def test_a_package_records_the_model_the_prompt_and_which_lines_changed(self):
        from generation import narrative, package, pipeline
        ctx = pipeline.Context(**self.base, narrator=narrative.Narrator(C.OllamaClient(transport=httpx.MockTransport(self.echo))))
        r = pipeline.generate(self.request(3), [], [], ctx)
        self.assertIsNotNone(r.package, r.defects[:2])
        p = r.package
        self.assertTrue(any(x["method"] == "model" for x in p["narrative"]["lines"]))
        self.assertEqual(p["provenance"]["models"][0]["digest"], "d9")
        self.assertIn("compose.narrative@1", p["provenance"]["prompts"])
        self.assertIn("LG-06", p["provenance"]["stages"])
        self.assertTrue(package.verify_seal(p))
        self.assertTrue(package.validate(p, self.cases)["passed"])
        self.assertNotIn("Original line", json.dumps(p["quality"]["narrative_audit"]))

    def test_with_the_model_down_the_package_is_the_authored_one(self):
        from generation import narrative, package, pipeline
        down = C.OllamaClient(transport=httpx.MockTransport(lambda r: (_ for _ in ()).throw(httpx.ConnectError("x"))))
        plain = pipeline.generate(self.request(3), [], [], pipeline.Context(**self.base)).package
        withdown = pipeline.generate(self.request(3), [], [], pipeline.Context(**self.base, narrator=narrative.Narrator(down))).package
        self.assertEqual(withdown["case"], plain["case"])
        self.assertNotIn("models", withdown["provenance"])
        self.assertTrue(all(x["method"] == "template" for x in withdown["narrative"]["lines"]))


class Planner(unittest.TestCase):
    """The plan composer against a fake host that answers the way a well-behaved or a badly-behaved model would."""

    @classmethod
    def setUpClass(cls):
        sys.path.insert(0, str(ROOT / "tools"))
        from composer import blocks as B
        from generation import objectives as ob, retrieve
        cls.cases = B.load_cases()
        cls.base = dict(index=retrieve.build_index(), cases=cls.cases, tables=ob.Tables.load())

    def request(self, seed):
        return {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced", "seed": seed}

    @staticmethod
    def host(mode):
        import re
        seen = {"blocks_calls": 0}

        def handler(request: httpx.Request) -> httpx.Response:
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": [{"name": "qwen3.5:9b-q4_K_M", "digest": "d9"}]})
            body = json.loads(request.content)
            user = body["messages"][1]["content"]
            if "Approved cases you may choose from" in user:
                first = re.findall(r"^([A-Z0-9-]+): ", user.split("Approved cases you may choose from:", 1)[1], re.M)[0]
                return httpx.Response(200, json={"message": {"content": json.dumps({"spine": first, "reason": "It exercises the objectives."})}})
            seen["blocks_calls"] += 1
            chosen, slot = {}, None
            for line in user.split("Slots and the blocks you may choose for each:", 1)[1].splitlines():
                m = re.match(r"^([A-Z_]+) \(choose (\d+)", line)
                if m:
                    slot, n = m.group(1), int(m.group(2))
                    chosen[slot] = []
                    chosen.setdefault("_n", {})[slot] = n
                    continue
                m = re.match(r"^  ([A-Za-z0-9-]+): ", line)
                if m and slot and len(chosen[slot]) < chosen["_n"][slot]:
                    chosen[slot].append(m.group(1))
            chosen.pop("_n")
            if mode == "bad" and chosen.get("NAME_FIRST") and chosen.get("NAME_LAST"):
                chosen["NAME_LAST"] = list(chosen["NAME_FIRST"])      # a first-name block offered as the last name: it is not in that slot's enum
            return httpx.Response(200, json={"message": {"content": json.dumps({"blocks": chosen, "reason": "A believable background."})}})
        return C.OllamaClient(transport=httpx.MockTransport(handler)), seen

    def test_a_good_proposal_is_used_recorded_and_passes_the_gate(self):
        from generation import compose_plan, package, pipeline
        client, _ = self.host("good")
        ctx = pipeline.Context(**self.base, planner=compose_plan.Planner(client))
        r = pipeline.generate(self.request(4), [], [], ctx)
        self.assertIsNotNone(r.package, r.defects[:2])
        p = r.package
        self.assertEqual(p["provenance"]["plan"]["method"], "model")
        self.assertIn("LG-04", p["provenance"]["stages"])
        self.assertEqual(p["provenance"]["models"][0]["role"], "planner")
        self.assertIn("compose_plan.spine@1", p["provenance"]["prompts"])
        self.assertTrue(package.validate(p, self.cases)["passed"])
        self.assertTrue(package.verify_seal(p))

    def test_a_proposal_the_composer_refuses_is_retried_then_dropped_and_the_solver_carries_on(self):
        from generation import compose_plan, package, pipeline
        client, seen = self.host("bad")
        ctx = pipeline.Context(**self.base, planner=compose_plan.Planner(client))
        r = pipeline.generate(self.request(4), [], [], ctx)
        self.assertIsNotNone(r.package)
        self.assertEqual(r.package["provenance"]["plan"]["method"], "solver")
        self.assertEqual(seen["blocks_calls"], compose_plan.ROUNDS)                      # 3 rounds at most
        self.assertTrue(package.validate(r.package, self.cases)["passed"])

    def test_the_schema_lists_only_allowed_ids_so_an_invented_one_cannot_be_answered(self):
        from generation import compose_plan
        s = compose_plan.blocks_schema({"NAME": ["NAME-A", "NAME-B"]}, {"NAME": (2, 2)})
        import jsonschema
        jsonschema.validate({"blocks": {"NAME": ["NAME-A", "NAME-B"]}, "reason": "ok fine"}, s)
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.validate({"blocks": {"NAME": ["NAME-A", "NAME-INVENTED"]}, "reason": "ok fine"}, s)
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.validate({"blocks": {"NAME": ["NAME-A"]}, "reason": "ok fine"}, s)

    def test_with_the_model_down_the_package_is_the_deterministic_one(self):
        from generation import compose_plan, pipeline
        down = C.OllamaClient(transport=httpx.MockTransport(lambda r: (_ for _ in ()).throw(httpx.ConnectError("x"))))
        plain = pipeline.generate(self.request(4), [], [], pipeline.Context(**self.base)).package
        withdown = pipeline.generate(self.request(4), [], [], pipeline.Context(**self.base, planner=compose_plan.Planner(down))).package
        self.assertEqual(withdown["case"], plain["case"])
        self.assertEqual(withdown["provenance"]["plan"]["method"], "solver")


class ReportsAndCritic(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        sys.path.insert(0, str(ROOT / "tools"))
        sys.path.insert(0, str(ROOT / "tools" / "generation"))
        from composer import blocks as B
        cls.cases = {c["id"]: c for c in B.load_cases()}

    def test_report_lines_are_built_from_the_result_rows_and_numbers_are_locked(self):
        from generation import reports
        case = self.cases["STK-S1-C"]
        texts = reports.render(case, {"slots": [{"test": "ecg", "report": "Regular rhythm, heart rate 64 per minute.", "measurements": {"rate_bpm": "64"}}]})
        self.assertTrue(any(t.startswith("Potassium: ") for t in texts.values()))
        self.assertEqual(reports.check(texts, case, {"slots": [{"test": "ecg", "report": "Regular rhythm, heart rate 64 per minute.", "measurements": {"rate_bpm": "64"}}]}), [])
        tampered = dict(texts)
        key = next(k for k in tampered if k.startswith("lab."))
        tampered[key] = tampered[key] + " It was 9.9 yesterday."
        self.assertTrue(any("9.9" in d for d in reports.check(tampered, case, None)))

    def test_every_authored_case_has_locked_report_lines(self):
        from generation import reports
        for c in self.cases.values():
            self.assertEqual(reports.check(reports.render(c, None), c, None), [], c["id"])

    def test_the_critic_keeps_only_flags_on_real_lines_and_real_facts(self):
        from generation import critic
        case = self.cases["VAR-S1-G"]
        facts, lines = critic.facts_of(case), critic.lines_of(case)
        good = {"flags": [{"line": "allergy", "claim": "denies any allergy", "fact": facts[2]}]}

        def host(answer, verdict="yes"):
            def h(request):
                if request.url.path == "/api/tags":
                    return httpx.Response(200, json={"models": []})
                if "suspected contradiction" in json.loads(request.content)["messages"][0]["content"]:        # the verification pass: one flag, one yes-or-no question
                    return httpx.Response(200, json={"message": {"content": json.dumps({"conflict": verdict})}})
                return httpx.Response(200, json={"message": {"content": json.dumps(answer)}})
            return C.OllamaClient(transport=httpx.MockTransport(h))
        flags = critic.Critic(host(good)).review(case)
        self.assertEqual([(f.line, f.fact) for f in flags], [("allergy", facts[2])])
        self.assertEqual(critic.Critic(host(good, "no")).review(case), [])                                       # a flag the model does not confirm is dropped
        self.assertEqual([f.line for f in critic.Critic(host(good, "no")).review(case, verify=False)], ["allergy"])      # the raw first pass is still available for measurement
        self.assertIsNone(critic.Critic(host({"flags": [{"line": "nonsense", "claim": "x y z", "fact": facts[0]}]})).review(case))      # an invented line id is refused by the schema, then the retry fails too
        self.assertIsNone(critic.Critic(None).review(case))
        self.assertIn("summary", lines)

    def test_a_flag_from_the_critic_is_a_defect_that_triggers_repair_and_a_down_critic_is_skipped(self):
        from generation import critic, objectives as ob, package, pipeline, retrieve
        seen = {"n": 0}

        class Flagging:
            def review(self, case):
                seen["n"] += 1
                return [critic.Flag("reason", "wrong operation", "x")] if seen["n"] == 1 else []

        class Silent:
            def review(self, case):
                return None
        base = dict(index=retrieve.build_index(), cases=list(self.cases.values()), tables=ob.Tables.load())
        req = {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced", "seed": 3}
        r = pipeline.generate(req, [], [], pipeline.Context(**base, critic=Flagging()))
        self.assertIsNotNone(r.package)
        self.assertEqual(r.package["provenance"]["repairs"][0]["codes"], ["CRITIC"])
        r2 = pipeline.generate(req, [], [], pipeline.Context(**base, critic=Silent()))
        self.assertEqual(r2.package["quality"]["critic"], "unavailable")
        self.assertTrue(package.verify_seal(r2.package))

    def test_the_seeded_set_is_deterministic_and_the_baseline_is_measured(self):
        import inconsistency_set as I
        a, b = I.build(list(self.cases.values())), I.build(list(self.cases.values()))
        self.assertEqual(a, b)
        self.assertGreaterEqual(len(a["members"]), 200)
        self.assertEqual(json.loads((ROOT / "tests" / "generation" / "text_inconsistencies.json").read_text(encoding="utf-8")), a)
        hit = sum(m["line"] in I.baseline_flags(I.apply(self.cases[m["case"]], m["line"], m["text"])) for m in a["members"])
        self.assertGreater(hit / len(a["members"]), 0.5)
        self.assertLess(hit / len(a["members"]), 0.8)                  # the baseline leaves room: the side of the body and denied pain are invisible to it


class Classifier(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        sys.path.insert(0, str(ROOT / "tools"))
        from classify import classifier
        cls.mod = classifier

    def host(self, act, conf):
        def h(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": []})
            return httpx.Response(200, json={"message": {"content": json.dumps({"act": act, "confidence": conf})}})
        return C.OllamaClient(transport=httpx.MockTransport(h))

    def test_the_schema_is_the_taxonomy_and_an_invented_act_is_refused(self):
        clf = self.mod.ActClassifier(self.host("MADE_UP", 0.9), thresholds={"per_act": {}})
        r = clf.classify("hello")
        self.assertEqual((r.source, r.credited), ("unavailable", False))
        self.assertEqual({a["id"] for a in self.mod.taxonomy()["acts"]} - set(self.mod.schema(self.mod.taxonomy())["properties"]["act"]["enum"]), set())

    def test_an_act_is_credited_only_at_or_above_its_calibrated_threshold(self):
        t = {"per_act": {"ASK_TOPIC": 0.7, "CLARIFY": None}}
        self.assertTrue(self.mod.ActClassifier(self.host("ASK_TOPIC", 0.8), thresholds=t).classify("x").credited)
        self.assertFalse(self.mod.ActClassifier(self.host("ASK_TOPIC", 0.6), thresholds=t).classify("x").credited)
        self.assertFalse(self.mod.ActClassifier(self.host("CLARIFY", 0.99), thresholds=t).classify("x").credited)       # never reached the precision: never credited

    def test_critical_acts_are_recorded_and_never_credited_from_speech(self):
        t = {"per_act": {"STATE_ALLERGY": 0.1, "SBAR_ELEMENT": 0.1}}
        for act in ("STATE_ALLERGY", "SBAR_ELEMENT", "CONFIRM_IDENTITY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT"):
            r = self.mod.ActClassifier(self.host(act, 1.0), thresholds=t).classify("x")
            self.assertEqual((r.act, r.critical, r.credited), (act, True, False), act)

    def test_calibration_picks_the_lowest_threshold_that_reaches_the_precision_and_skips_hopeless_acts(self):
        dev = [("ASK_TOPIC", "ASK_TOPIC", 0.95), ("ASK_TOPIC", "ASK_TOPIC", 0.9), ("ASK_TOPIC", "ASK_TOPIC", 0.85), ("CLARIFY", "ASK_TOPIC", 0.5),
               ("CLARIFY", "CLARIFY", 0.4), ("ASK_TOPIC", "CLARIFY", 0.9), ("EXPLAIN_TEST", "CLARIFY", 0.8), ("NONE", "CLARIFY", 0.7)]
        out = self.mod.calibrate(dev, self.mod.taxonomy())
        self.assertEqual(out["ASK_TOPIC"], 0.85)
        self.assertIsNone(out["CLARIFY"])
        self.assertNotIn("NONE", out)

    def test_without_a_model_nothing_is_credited(self):
        r = self.mod.ActClassifier(None).classify("x")
        self.assertEqual((r.source, r.credited, r.error), ("unavailable", False, "NO_MODEL"))

    def test_the_classifier_dataset_is_current_and_split_by_speaker(self):
        import subprocess
        p = subprocess.run([sys.executable, str(ROOT / "tools" / "synth" / "gen_utterances.py"), "--check"], capture_output=True, text=True)
        self.assertEqual(p.returncode, 0, p.stdout)


class DialogueTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        sys.path.insert(0, str(ROOT / "tools"))
        from dialogue import dialogue as D
        from composer import blocks as B
        cls.D = D
        case = {c["id"]: c for c in B.load_cases()}["VAR-S1-B"]
        cls.case = case
        hist = [h for h in case["history"] if h.get("variant") is None]
        cls.volunteered = next(h for h in hist if h["volunteered"])
        cls.hidden = next(h for h in hist if not h["volunteered"] and len(h["answer"]) > 25 and h["topic"] != cls.volunteered["topic"])
        cls.facts = [{"id": "h." + h["topic"], "value": h["answer"], "disclosed": False, "disclosable_now": bool(h["volunteered"])} for h in hist]
        cls.ctx = {"role": "patient", "case": case["id"], "allowed_intents": ["answer_question", "express_symptom", "cannot_recall"], "facts": cls.facts, "last_turns": [], "results": []}

    def host(self, answers, seen=None):
        calls = []

        def h(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": []})
            body = json.loads(request.content)
            calls.append(body)
            a = answers[min(len(calls) - 1, len(answers) - 1)]
            return httpx.Response(200, json={"message": {"content": json.dumps(a)}})
        c = C.OllamaClient(transport=httpx.MockTransport(h))
        c.calls = calls
        return c

    def test_an_answer_from_the_shown_fact_is_accepted_and_the_hidden_fact_is_never_in_the_prompt(self):
        f = "h." + self.volunteered["topic"]
        client = self.host([{"intent": "express_symptom", "text": self.volunteered["answer"], "fact_ids": [f]}])
        r = self.D.Dialogue(client).reply(self.ctx, "How are you feeling?", authored={f: self.volunteered["answer"]})
        self.assertEqual((r.source, r.intent, r.fact_ids), ("model", "express_symptom", [f]))
        sent = json.dumps(client.calls)
        self.assertNotIn(self.hidden["answer"], sent)
        self.assertNotIn("h." + self.hidden["topic"], sent)

    def test_a_hidden_fact_told_by_the_model_is_refused_twice_and_the_cannot_recall_line_is_used(self):
        leak = {"intent": "answer_question", "text": self.hidden["answer"], "fact_ids": []}
        client = self.host([leak, leak])
        r = self.D.Dialogue(client).reply(self.ctx, "And what about the other thing?")
        self.assertEqual((r.source, r.intent, len(client.calls)), ("fallback", "cannot_recall", 2))
        self.assertIn("DISCLOSURE_LEAK", r.reasons)
        self.assertNotIn(self.hidden["answer"], r.text)

    def test_a_fact_id_the_model_was_not_shown_is_refused_by_the_schema(self):
        client = self.host([{"intent": "answer_question", "text": "x", "fact_ids": ["h." + self.hidden["topic"]]}] * 2)
        r = self.D.Dialogue(client).reply(self.ctx, "q")
        self.assertEqual(r.source, "fallback")

    def test_without_a_model_the_authored_line_of_the_asked_fact_is_used_and_a_repeat_comes_from_the_cache(self):
        f = "h." + self.volunteered["topic"]
        d = self.D.Dialogue(None)
        r = d.reply(self.ctx, "How are you?", authored={f: self.volunteered["answer"]}, asked_fact=f)
        self.assertEqual((r.source, r.text), ("fallback", self.volunteered["answer"]))
        good = self.host([{"intent": "express_symptom", "text": self.volunteered["answer"], "fact_ids": [f]}])
        d2 = self.D.Dialogue(good)
        d2.reply(self.ctx, "How are you?")
        again = d2.reply(self.ctx, "how are you?")
        self.assertEqual((again.source, len(good.calls)), ("cache", 1))

    def test_a_staff_member_uses_only_the_allowed_intents_and_falls_back_to_the_prerendered_line(self):
        ctx = {"role": "nurse", "case": "X", "allowed_intents": ["ask_clarification", "confirm_order"], "facts": [{"id": "s.1", "value": "The cannula is in place.", "disclosed": False, "disclosable_now": True}], "last_turns": []}
        bad = self.host([{"intent": "prescribe_dose", "text": "Give 5 mg now.", "fact_ids": []}] * 2)
        r = self.D.Dialogue(bad).reply(ctx, "Give the antibiotic.", role="nurse", fallback_lines={"ask_clarification": "Can you confirm that order?"})
        self.assertEqual((r.source, r.text, r.intent), ("fallback", "Can you confirm that order?", "ask_clarification"))
        ok = self.host([{"intent": "confirm_order", "text": "The cannula is in place.", "fact_ids": ["s.1"]}])
        self.assertEqual(self.D.Dialogue(ok).reply(ctx, "Is the cannula in?", role="nurse").source, "model")
        sent = json.dumps(ok.calls)
        self.assertIn("Do not give orders, doses", sent)


class DebriefTests(unittest.TestCase):
    REPORT = {"end_state": "SAFE_BUT_DELAYED", "result": "SUCCESS_WITH_DELAY", "items": [
        {"id": "credit.decision_correct", "status": "credit"}, {"id": "major.postponed_unnecessarily", "status": "penalty"}, {"id": "credit.capacity_checked", "status": "credit"},
        {"id": "critical.checklist_contradiction", "status": "critical"}, {"id": "info.checklist_stopped_honestly", "status": "info"}]}

    @classmethod
    def setUpClass(cls):
        sys.path.insert(0, str(ROOT / "tools"))
        from debrief import rewrite as R
        cls.R = R
        cls.facts = R.facts_of(cls.REPORT)
        cls.lex = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))

    def good(self):
        by = {f["id"]: f for f in self.facts}
        return [{"title": "What went well", "text": "You made the right decision and you checked capacity, well done.", "fact_ids": ["f1", "f3"]},
                {"title": "What to improve", "text": "The case was postponed when it did not need to be, which is a mistake to work on.", "fact_ids": ["f2"]},
                {"title": "Critical points", "text": "A checklist answer contradicted the state of the theatre, a critical mistake.", "fact_ids": ["f4"]},
                {"title": "For your information", "text": "You stopped the checklist honestly, and the session ended safe but delayed.", "fact_ids": ["f5", "f6"]}] if by else []

    def host(self, sections):
        def h(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": []})
            return httpx.Response(200, json={"message": {"content": json.dumps({"sections": sections})}})
        return C.OllamaClient(transport=httpx.MockTransport(h))

    def test_the_template_is_complete_and_valid_by_itself(self):
        t = self.R.template(self.facts)
        self.assertEqual({i for s in t for i in s["fact_ids"]}, {f["id"] for f in self.facts})
        self.assertEqual(self.R.check(t, self.facts, self.lex), [])

    def test_a_good_rewrite_replaces_the_template(self):
        out = self.R.Rewriter(self.host(self.good())).make(self.REPORT)
        self.assertEqual((out["used"], out["reasons"]), ("ai", []))
        self.assertEqual(self.R.check(out["ai"], self.facts, self.lex), [])

    def test_a_down_model_leaves_the_complete_template(self):
        out = self.R.Rewriter(None).make(self.REPORT)
        self.assertEqual((out["used"], out["ai"], out["reasons"]), ("template", None, ["NO_MODEL"]))

    def test_mutated_debriefs_are_rejected(self):
        """The strict validator against the ways a rewrite goes wrong: a fact dropped, praise turned into a mistake and the reverse, a new medicine, a new event, a changed or invented number."""
        import copy
        base = self.good()
        mutations = []
        for i in range(len(base)):
            m = copy.deepcopy(base); m[i]["fact_ids"] = m[i]["fact_ids"][:-1] or ["f1"]; mutations.append(("drop_fact", m))
        m = copy.deepcopy(base); m[0]["text"] = "You made the wrong decision and forgot to check capacity."; mutations.append(("praise_to_mistake", m))
        m = copy.deepcopy(base); m[1]["text"] = "The postponement was a good and correct decision, excellent."; mutations.append(("mistake_to_praise", m))
        m = copy.deepcopy(base); m[2]["text"] = "A critical mistake: great and well done on the checklist."; mutations.append(("critical_to_praise", m))
        for word in ("penicillin", "adrenaline", "warfarin", "morphine"):
            m = copy.deepcopy(base); m[0]["text"] += f" You also gave {word}."; mutations.append(("new_medicine", m))
        for n in ("12", "40", "7"):
            m = copy.deepcopy(base); m[0]["text"] += f" It took {n} minutes."; mutations.append(("new_number", m))
        for text in ("The patient had an anaphylaxis during the session.", "The patient had low blood pressure.", "The patient showed bradycardia.", "There was a desaturation."):
            m = copy.deepcopy(base); m[3]["text"] += " " + text; mutations.append(("new_event", m))
        m = copy.deepcopy(base); m[0]["text"] += " Give 500 mg of paracetamol next time."; mutations.append(("dose", m))
        m = copy.deepcopy(base); m[0]["text"] += " You should prescribe more fluids."; mutations.append(("prescribe", m))
        rejected = [k for k, m in mutations if self.R.check(m, self.facts, self.lex)]
        missed = [k for k, m in mutations if not self.R.check(m, self.facts, self.lex)]
        self.assertGreaterEqual(len(rejected) / len(mutations), 0.95, missed)

    def test_a_section_that_mixes_praise_and_mistakes_is_refused(self):
        m = [{"title": "What went well", "text": "You made the wrong decision.", "fact_ids": [f["id"] for f in self.facts]}]
        self.assertIn("DEBRIEF_MIXED_VERDICT", self.R.check(m, self.facts, self.lex))

    def test_known_gap_an_event_that_is_not_in_the_lexicon_is_not_caught(self):
        """The new-event check knows only the engine's event words (content/ai/lexicon.json). An invented event outside them passes: DT-211. If this test starts failing the gap has been closed, which is good news."""
        m = self.good()
        m[3]["text"] += " The patient went into cardiac arrest."
        self.assertEqual(self.R.check(m, self.facts, self.lex), [])

    def test_a_rewrite_that_changes_the_verdict_is_refused_twice_and_the_template_stays(self):
        bad = self.good()
        bad[0]["text"] = "You made the wrong decision and forgot to check capacity."
        out = self.R.Rewriter(self.host(bad)).make(self.REPORT)
        self.assertEqual(out["used"], "template")
        self.assertIn("DEBRIEF_VERDICT_CHANGED", out["reasons"])


if __name__ == "__main__":
    unittest.main()
