"""The text consistency critic (p1-live-scenario-generation-s15, features B18 and B22, stage LG-11).

    flags = Critic(client).review(case)         # [] or [Flag(line, claim, fact)]; None if the model could not be asked

Free text hides the contradictions rules miss: a patient who says she is allergic to nothing while the case lists penicillin, a pain on the left in a case whose examination is on the right, an age that is not the
identity's. The critic reads the structured facts of the case (identity, allergies, the operation, the problems, the examination findings, the abnormal results) and the patient's own lines (the reason for
admission, the allergy answer, the history answers, the summary) and lists claims in the lines that conflict with the facts. A flag is kept only if its line id is one of the lines it was given and the fact it
cites is a sentence of the fact list (so it cannot cite a fact that is not there). Unresolved flags reject the package into repair; they are never ignored. The critic is advisory about nothing: it only adds
defects, never removes one. Its false alarms are measured on clean cases (tools/ai/eval_critic.py) and a critic that rejects too many good cases is not switched on.
"""
from __future__ import annotations

from dataclasses import dataclass

try:
    from llm import gateway
except ImportError:
    from server.llm import gateway

PROCEDURE = {"S1": "an operation to remove the gallbladder (cholecystectomy)", "S2": "an operation to remove the appendix (appendicectomy)"}
MAX_FLAGS = 6


@dataclass
class Flag:
    line: str
    claim: str
    fact: str


def facts_of(case: dict) -> list[str]:
    i = case["identity"]
    f = [f"The patient is {i['age']} years old and {'female' if i['sex'] == 'F' else 'male'}.", f"The planned operation is {PROCEDURE[case['scenario']]}."]
    if case["allergies"]:
        f += [f"The patient is allergic to {a['substance']} ({a['reaction']})." for a in case["allergies"]]
    else:
        f.append("The patient has no known allergies.")
    f += [f"A problem in this case: {p['label']}." for p in sorted(case["problems"], key=lambda p: p["rank"])[:4]]
    f += [f"On examination, {e['system']}: {e['finding']}" for e in case["examination"] if e["abnormal"]][:5]
    f += [f"Result: {r['test']} is {r['value']} {r.get('unit', '')} ({r['flag']})." for r in case["results"] if r["flag"] not in ("normal", "negative") and not r.get("variant")][:5]
    return f


def lines_of(case: dict) -> dict[str, str]:
    out = {"summary": case["presentation"].get("summary", ""), "reason": case["reason_answer"], "allergy": case["allergy_answer"]}
    for h in case["history"]:
        if h.get("variant") is None and h.get("answer"):
            out[f"h.{h['topic']}"] = h["answer"]
    return {k: v for k, v in out.items() if v and v.strip()}


def schema(line_ids: list[str], facts: list[str]) -> dict:
    return {"type": "object", "required": ["flags"], "additionalProperties": False, "properties": {"flags": {"type": "array", "maxItems": MAX_FLAGS, "items": {
        "type": "object", "required": ["line", "claim", "fact"], "additionalProperties": False,
        "properties": {"line": {"enum": line_ids}, "claim": {"type": "string", "minLength": 3, "maxLength": 200}, "fact": {"enum": facts}}}}}}


VERIFY_SCHEMA = {"type": "object", "required": ["conflict"], "additionalProperties": False, "properties": {"conflict": {"enum": ["yes", "no"]}}}


class Critic:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout

    def review(self, case: dict, verify: bool = True) -> list[Flag] | None:
        """The flags that survive verification (or all of them with verify=False). Measured (tools/ai/eval_critic.py): listing contradictions in one pass makes the small model pair a line with an unrelated fact and flag
        nearly every clean case, so each flag is then put to the model alone, as a yes-or-no question about one fact and one line, and kept only on yes."""
        flags = self.review_raw(case)
        if flags is None or not verify:
            return flags
        return self.verified(case, flags)

    def verified(self, case: dict, flags: list[Flag]) -> list[Flag]:
        lines = lines_of(case)
        keep = []
        for f in flags:
            r = gateway.run(self.client, "critic.verify", {"fact": f.fact, "line": lines.get(f.line, ""), "claim": f.claim}, schema=VERIFY_SCHEMA, model=self.model, timeout=self.timeout, retries=1, task="critic_verify")
            if r.ok and r.value["conflict"] == "yes":
                keep.append(f)                                           # a flag the model cannot confirm (or cannot be asked about) is dropped: the critic only adds defects it can stand behind
        return keep

    def review_raw(self, case: dict) -> list[Flag] | None:
        if self.client is None:
            return None
        facts, lines = facts_of(case), lines_of(case)
        text = "\n".join(f"{k}: {v}" for k, v in lines.items())
        r = gateway.run(self.client, "critic.claims", {"facts": "\n".join(facts), "lines": text}, schema=schema(sorted(lines), facts), model=self.model, timeout=self.timeout, retries=1, task="critic")
        if not r.ok:
            return None
        return [Flag(f["line"], f["claim"], f["fact"]) for f in r.value["flags"]]
