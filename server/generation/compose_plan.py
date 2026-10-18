"""The AI plan composer: a language model proposes, the solver and the validators decide (p1-live-scenario-generation-s13, stage LG-04; p1-scenario-composer-difficulty-s03).

    plan = Planner(client).propose(objectives, ranked, template, library, names, mrns, case_id)    # a Plan, or None: the deterministic skeleton then goes ahead alone

Two small questions go to the model, each with a JSON Schema whose enums list only what is allowed, so an invented id cannot be answered:
  1. which of the best-ranked approved cases the session keeps the story of (the ranking and the recent cases are already applied: the model chooses among the shortlist and says why in one sentence);
  2. for that case, which background blocks fill each slot (demographics, names, social history, anaesthetic history, allergy), from the blocks that fit the case on their own.
The proposal is then checked by the composer's own rules (`Solver.violations`, slot counts, status): a refused proposal is sent back once or twice with the reasons as an avoid-hint (3 rounds at most) and then
dropped. A dropped or missing proposal is not an error: the skeleton builder carries on with its deterministic choice, which is also the baseline this module has to beat. The model never sees a hidden clinical
fact, a result or a number: the shortlist carries the case label, tier and problem labels (what a trainee would be told about the case), the blocks carry a one-line description. Its reasons are logged
(advisory), never shown to a trainee and never trusted: the explanation a trainee reads is built from the selector's facts.
"""
from __future__ import annotations

import hashlib
from dataclasses import dataclass, field

try:
    from llm import gateway
except ImportError:
    from server.llm import gateway

SHORTLIST = 5                                  # the same pool the deterministic skeleton draws from (skeleton.TOP_CHOICES): the model may not reach below it
ROUNDS = 3


@dataclass
class Plan:
    spine: str
    blocks: list[str]
    reasons: dict[str, str] = field(default_factory=dict)
    audit: list[dict] = field(default_factory=list)
    rounds: int = 1


def describe_case(case: dict) -> str:
    return f"{case['id']}: {case['walkthrough_label']} (tier {case['tier']}); problems: " + "; ".join(p["label"] for p in sorted(case["problems"], key=lambda p: p["rank"]))


def describe_block(b: dict) -> str:
    a = b["attributes"]
    t = b["type"]
    if t == "DEM":
        return f"{a['title']}, {a['occupation']}, about {a['age']}"
    if t == "NAME":
        return f"{a['part']} name {a['text']}"
    if t == "SOC":
        return f"smoking: {a['smoking']} alcohol: {a['alcohol']}"
    if t == "PSH":
        return f"anaesthetics: {a['previous_anaesthetic']} family: {a['family_anaesthetic']}"
    if t == "ALG":
        al = a["allergies"]
        return "allergy: " + ("; ".join(f"{x['substance']} ({x['reaction']})" for x in al) if al else "none")
    return b["id"]


def spine_schema(ids: list[str]) -> dict:
    return {"type": "object", "required": ["spine", "reason"], "additionalProperties": False,
            "properties": {"spine": {"enum": ids}, "reason": {"type": "string", "minLength": 3, "maxLength": 240}}}


def blocks_schema(options: dict[str, list[str]], counts: dict[str, tuple[int, int]]) -> dict:
    props = {t: {"type": "array", "items": {"enum": ids}, "minItems": counts[t][0], "maxItems": counts[t][1], "uniqueItems": True} for t, ids in options.items()}
    return {"type": "object", "required": ["blocks", "reason"], "additionalProperties": False,
            "properties": {"blocks": {"type": "object", "required": list(props), "additionalProperties": False, "properties": props}, "reason": {"type": "string", "minLength": 3, "maxLength": 240}}}


class Planner:
    def __init__(self, client, model: str | None = None, timeout: float | None = None, rounds: int = ROUNDS, spine_version: int | None = None):
        self.client, self.model, self.timeout, self.rounds, self.spine_version = client, model, timeout, rounds, spine_version

    def propose(self, objectives: dict, ranked: list, template: dict, library: list[dict], names: set[str], mrns: set[str], case_id: str, solve, difficulty: float = 0.0,
                avoid_blocks: list[str] | None = None) -> Plan | None:
        """`ranked` is [(score, spine_case)] best first; `solve` is the tools/composer solve module (passed in so this package does not import tools at load time)."""
        short = [c for _, c in ranked[:SHORTLIST]]
        if not short or self.client is None:
            return None
        ids = [c["id"] for c in short]
        by_id = {c["id"]: c for c in short}
        audits: list[dict] = []
        objective_text = ("; ".join(f"{c['behaviour']} (rating {c['rating']})" for c in objectives["competencies"]) or "the standard first-session objectives") + \
            (" ; dimensions: " + ", ".join(objectives["dimensions"]) if objectives["dimensions"] else "")
        r1 = gateway.run(self.client, "compose_plan.spine", {"objectives": objective_text, "difficulty": str(round(difficulty)), "avoid": ", ".join(objectives["exclusions"]["spines"]) or "none",
                                                              "candidates": "\n".join(describe_case(c) for c in short)}, schema=spine_schema(ids), model=self.model, timeout=self.timeout,
                         retries=1, task="compose_plan_spine", version=self.spine_version)
        audits.append(r1.audit)
        if not r1.ok:
            return None
        spine = by_id[r1.value["spine"]]
        solver = solve.Solver(library, template, spine, __import__("random").Random(0), names)
        options: dict[str, list[dict]] = {}
        counts: dict[str, tuple[int, int]] = {}
        for slot in solver.slots:
            fit = [b for b in solver.candidates(slot) if solver.consistent([b])]
            if slot["type"] == "NAME":                          # a name is one first-name block and one last-name block: two slots for the model, not one slot of two
                for part in ("first", "last"):
                    options[f"NAME_{part.upper()}"] = [b for b in fit if b["attributes"]["part"] == part]
                    counts[f"NAME_{part.upper()}"] = (1, 1)
            elif fit or slot["min"] > 0:
                options[slot["type"]] = fit
                counts[slot["type"]] = (slot["min"], min(slot["max"], len(fit)))
        if any(counts[t][0] > len(options[t]) for t in options):
            return None
        for t in [t for t in options if counts[t][1] == 0]:
            options.pop(t)
            counts.pop(t)
        text = "\n".join(f"{t} (choose {counts[t][0]}" + (f" to {counts[t][1]}" if counts[t][1] != counts[t][0] else "") + "):\n" + "\n".join(f"  {b['id']}: {describe_block(b)}" for b in options[t]) for t in options)
        schema = blocks_schema({t: [b["id"] for b in bs] for t, bs in options.items()}, counts)
        state = {"card_error": ""}

        def check(value) -> list[str]:
            chosen = [i for t in options for i in value["blocks"].get(t, [])]
            try:
                solve.build_card_from_blocks(spine, objectives["scenario"], objectives["seed"], case_id, library, template, names, set(mrns), chosen)
            except solve.Unsatisfiable as e:
                return ["COMPOSER: " + str(e)[:300]]
            return []
        r2 = gateway.run(self.client, "compose_plan.blocks", {"case": describe_case(spine), "avoid": ", ".join(avoid_blocks or []) or "none", "slots": text}, validate=check, schema=schema,
                         model=self.model, timeout=self.timeout, retries=self.rounds - 1, task="compose_plan_blocks")
        audits.append(r2.audit)
        if not r2.ok:
            return None
        chosen = sorted(i for t in options for i in r2.value["blocks"].get(t, []))
        return Plan(spine["id"], chosen, {"spine": r1.value["reason"], "blocks": r2.value["reason"]}, audits, rounds=len(r2.attempts))


def _propose_extras(self, case: dict, objectives: dict):
    """Events, what-if branches and interactions for a case already built (server/generation/plan_extras.py): closed choices, checked by code; None when the model cannot be asked."""
    try:
        from generation.plan_extras import ExtrasPlanner
    except ImportError:
        from server.generation.plan_extras import ExtrasPlanner
    return ExtrasPlanner(self.client, self.model, self.timeout).propose(case, objectives)


Planner.propose_extras = _propose_extras


def plan_hash(plan: Plan) -> str:
    return hashlib.sha256((plan.spine + "|" + ",".join(plan.blocks)).encode("utf-8")).hexdigest()[:16]
