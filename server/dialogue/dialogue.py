"""Patient and staff dialogue (p1-ai-language-s04 and s05, EXP-AI-2): the Core decides what may be said; the model only phrases it; the validator checks; a fixed line is the fallback.

    d = Dialogue(client)
    out = d.reply(context, question, role="patient", style=..., age=62, sex="M")
    out.text, out.intent, out.source          # source: "model", "cache", "fallback"

`context` is what the Core's context builder produces for one character at one moment (src/ProcSense.Core/AIContext/ContextBuilder.cs, ContextDto): the allowed intents, the facts with the Core's decision
about each (`disclosable_now`: the trainee has earned it; the Core, not the model, decides this), the recent turns and the results. The model is shown ONLY the facts marked disclosable now; a hidden fact is not in
its prompt, so it cannot tell it, and the grounding validator (server/validation/validator.py, the same algorithm as the C# validator) is the second line: unknown intent, unknown fact id, an entity or number the
context does not hold, a hidden fact told, a dose, a prescribing phrase. An answer that fails is retried once with the reasons and then replaced by the fallback: for the patient the approved authored line of the
fact being asked (the quoted line from the case file, passed in `authored`) or the approved `cannot_recall`; for staff the pre-rendered line of the intent (`fallback_lines`) or a clarification request. Repeated
questions are answered from a cache seeded with the authored lines. Staff intents are the closed lists of content/ai/intents.json: a staff member can ask, confirm, report or raise an approved concern, never
instruct. The prompt and the context never contain the hidden facts, scoring rules or the case's end state.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

try:
    from llm import gateway
    from validation.validator import has_term, norm, numbers, validate
except ImportError:
    from server.llm import gateway
    from server.validation.validator import has_term, norm, numbers, validate

ROOT = Path(__file__).resolve().parents[2]
LEXICON = ROOT / "content" / "ai" / "lexicon.json"
CANNOT_RECALL = "I am sorry, I am not sure about that."
CLARIFY = "Could you say that another way?"
SILENT = "stay_silent"                    # a staff member who is not being spoken to and has nothing to add chooses this: no line is spoken (the model decides who has a turn, ADR-0026)


@dataclass
class Reply:
    text: str
    intent: str
    fact_ids: list[str] = field(default_factory=list)
    source: str = "model"                  # model | cache | fallback
    reasons: list[str] = field(default_factory=list)
    audit: dict = field(default_factory=dict)


def schema_for(intents: list[str], fact_ids: list[str]) -> dict:
    ids = {"type": "array", "items": {"type": "string", "enum": fact_ids}, "maxItems": len(fact_ids)} if fact_ids else {"type": "array", "maxItems": 0}
    return {"type": "object", "additionalProperties": False, "required": ["intent", "text", "fact_ids"],
            "properties": {"intent": {"type": "string", "enum": intents}, "text": {"type": "string", "minLength": 1, "maxLength": 400}, "fact_ids": ids}}


def validator_context(ctx: dict, question: str, lexicon: dict) -> dict:
    """The validator's view: every fact (so a hidden one told is caught) and the allowed words and numbers taken from what the character may know (shown facts, results, recent turns, the question)."""
    shown = [f for f in ctx["facts"] if f.get("disclosable_now")]
    text = " ".join([f["value"] for f in shown] + list(ctx.get("results", [])) + [t.get("text", "") for t in ctx.get("last_turns", [])] + [question])
    joined = norm(text)
    entities = [t for cat in ("drug", "test", "condition", "allergen", "equipment") for t in lexicon["categories"][cat] if has_term(joined, t)]
    return {"allowed_intents": ctx["allowed_intents"], "facts": [{"id": f["id"], "value": f["value"], "disclosed": bool(f.get("disclosed")), "disclosable_now": bool(f.get("disclosable_now"))} for f in ctx["facts"]],
            "allowed_entities": entities, "numbers": numbers(text), "dose_text": ctx.get("dose_text", [])}


class Dialogue:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout
        self.lexicon = json.loads(LEXICON.read_text(encoding="utf-8"))
        self.cache: dict[tuple, Reply] = {}

    def reply(self, ctx: dict, question: str, role: str = "patient", style: str = "plain", age: int = 50, sex: str = "M", authored: dict[str, str] | None = None,
              fallback_lines: dict[str, str] | None = None, asked_fact: str | None = None) -> Reply:
        authored, fallback_lines = authored or {}, fallback_lines or {}
        shown = [f for f in ctx["facts"] if f.get("disclosable_now")]
        if sum(c.isalpha() for c in question) < 2:                       # silence, a cough or recogniser noise: no model call, a request to say it again (p1-ai-language-s13)
            intent = "cannot_recall" if role == "patient" else ctx["allowed_intents"][0]
            return Reply(authored.get("clarify", CLARIFY) if role == "patient" else fallback_lines.get(intent, CLARIFY), intent, [], "fallback", ["EMPTY_INPUT"])
        key = (role, ctx.get("case", ""), tuple(f["id"] for f in shown), question.strip().lower())
        if key in self.cache:
            r = self.cache[key]
            return Reply(r.text, r.intent, r.fact_ids, "cache", r.reasons)
        if self.client is None:
            return self._fallback(role, ctx, shown, asked_fact, authored, fallback_lines, ["NO_MODEL"])
        vctx = validator_context(ctx, question, self.lexicon)
        facts_text = "\n".join(f"- {f['id']}: {f['value']}" for f in shown) or "- (none)"
        turns = "\n".join(f"{t.get('who', '?')}: {t.get('text', '')}" for t in ctx.get("last_turns", [])[-4:]) or "(none)"
        variables = ({"style": style, "age": str(age), "sex_word": "woman" if sex == "F" else "man", "intents": ", ".join(ctx["allowed_intents"]), "facts": facts_text, "turns": turns, "question": question}
                     if role == "patient" else {"role": role, "intents": ", ".join(ctx["allowed_intents"]), "facts": facts_text, "turns": turns, "question": question})

        def check(v) -> list[str]:
            res = validate({"kind": "utterance", "role": role, **v}, vctx, self.lexicon)
            return [f"{c}: the answer was refused for this reason" for c in res.codes]
        r = gateway.run(self.client, "dialogue.patient" if role == "patient" else "dialogue.staff", variables, validate=check, schema=schema_for(ctx["allowed_intents"], [f["id"] for f in shown]),
                        model=self.model, timeout=self.timeout, retries=1, task="dialogue_" + role)
        if r.ok:
            out = Reply(r.value["text"].strip(), r.value["intent"], r.value["fact_ids"], "model", [], r.audit)
            self.cache[key] = out
            return out
        return self._fallback(role, ctx, shown, asked_fact, authored, fallback_lines, [d.split(":")[0] for d in r.audit["defects"]] or [r.audit["error"] or "REJECTED"], r.audit)

    def team(self, ctx: dict, staff_ctxs: dict[str, dict], question: str, style: str = "plain", age: int = 50, sex: str = "M", authored: dict[str, str] | None = None,
             staff_lines: dict[str, dict[str, str]] | None = None, asked_fact: str | None = None) -> dict[str, Reply]:
        """One trainee turn answered by the patient and the staff (the three voices): the patient's words are written from the earned facts by `reply` (grounding validator, authored fallback), and the staff
        CHOOSE their intents from the roles' closed lists in ONE call (`pick_many`) and speak the approved line of the intent (`staff_lines[role][intent]`), so nothing a model writes is spoken for staff.
        Two calls, not three: measured against a single combined call (patient and staff in one answer), the two separate prompts kept the patient's answers more often on the right fact and staff intents
        more often correct (docs/research/cloud_model_routing.md), at about 0.6 s more. A team member who is not being spoken to chooses silence and is left out. Returns {"patient": Reply, role: Reply, ...}."""
        out = {"patient": self.reply(ctx, question, "patient", style, age, sex, authored, None, asked_fact)}
        audit = out["patient"].audit or {}
        if staff_ctxs and audit.get("cloud") and not audit.get("fell_back_from"):
            # The staff are chosen by the cloud model only. The local model cannot be trusted to stay silent when it is not its turn (measured: it answered for the wrong role and spoke on every turn), so after an
            # outage, a usage limit, an authored answer or a local patient answer the staff do not speak: the patient answers alone, and nobody is made to speak a line that no good choice produced.
            for role, r in self.pick_many(staff_ctxs, question, staff_lines or {}).items():
                if r.intent != SILENT and r.source != "fallback" and (r.audit or {}).get("cloud"):
                    out[role] = r
        return out

    def pick(self, ctx: dict, question: str, role: str, fallback_lines: dict[str, str]) -> Reply:
        """A staff reply that is CHOSEN, not written: the model names which of the allowed intents fits (a one-word answer, so it is fast) and the words are the approved line of that intent
        (`fallback_lines`, the pre-rendered voice lines), so nothing the model writes is ever spoken. Measured (tools/ai/eval_multiagent.py --staff-pick) as the way three characters can answer within
        the budget on one model. A model that is down or answers badly gives the first allowed intent's line, as `reply` does."""
        intents = ctx["allowed_intents"]
        turns = "\n".join(f"{t.get('who', '?')}: {t.get('text', '')}" for t in ctx.get("last_turns", [])[-4:]) or "(none)"
        if self.client is None:
            return self._fallback(role, ctx, [], None, {}, fallback_lines, ["NO_MODEL"])
        r = gateway.run(self.client, "dialogue.staff_pick", {"role": role, "intents": ", ".join(intents), "turns": turns, "question": question},
                        schema={"type": "object", "additionalProperties": False, "required": ["intent"], "properties": {"intent": {"type": "string", "enum": intents}}}, model=self.model,
                        timeout=self.timeout, retries=1, task="dialogue_pick_" + role)
        if r.ok:
            intent = r.value["intent"]
            return Reply(fallback_lines.get(intent, CLARIFY), intent, [], "model", [], r.audit)
        return self._fallback(role, ctx, [], None, {}, fallback_lines, [d.split(":")[0] for d in r.audit["defects"]] or [r.audit["error"] or "REJECTED"], r.audit)

    def pick_many(self, ctxs: dict[str, dict], question: str, fallback_lines: dict[str, dict[str, str]]) -> dict[str, Reply]:
        """Several staff replies from ONE model call: one answer holds an intent for every role, each from that role's own allowed list, and each is spoken as the approved line of its intent (`fallback_lines[role]`).
        One short decode instead of one per character is what brings a patient and two staff within the budget on a single model (tools/ai/eval_multiagent.py --staff-pick-together). A role the model
        answers badly for, or a model that is down, gets the first allowed intent's line, as `pick` does."""
        roles = {r: c["allowed_intents"] for r, c in ctxs.items()}
        turns = "\n".join(f"{t.get('who', '?')}: {t.get('text', '')}" for c in list(ctxs.values())[:1] for t in c.get("last_turns", [])[-4:]) or "(none)"

        def fallback(why):
            return {r: self._fallback(r, ctxs[r], [], None, {}, fallback_lines.get(r, {}), why) for r in roles}
        if self.client is None:
            return fallback(["NO_MODEL"])
        schema = {"type": "object", "additionalProperties": False, "required": list(roles), "properties": {r: {"type": "string", "enum": [*intents, SILENT]} for r, intents in roles.items()}}
        r = gateway.run(self.client, "dialogue.staff_pick_many", {"roles": "\n".join(f"{name}: {', '.join(intents)}" for name, intents in roles.items()), "turns": turns, "question": question}, schema=schema,
                        model=self.model, timeout=self.timeout, retries=1, task="dialogue_pick_many")
        if not r.ok:
            return fallback([d.split(":")[0] for d in r.audit["defects"]] or [r.audit["error"] or "REJECTED"])
        return {name: Reply("" if r.value[name] == SILENT else fallback_lines.get(name, {}).get(r.value[name], CLARIFY), r.value[name], [], "model", [], r.audit) for name in roles}

    def _fallback(self, role, ctx, shown, asked_fact, authored, fallback_lines, reasons, audit=None) -> Reply:
        if role == "patient":
            if asked_fact and asked_fact in authored and any(f["id"] == asked_fact for f in shown):
                return Reply(authored[asked_fact], "answer_question", [asked_fact], "fallback", reasons, audit or {})
            return Reply(authored.get("cannot_recall", CANNOT_RECALL), "cannot_recall", [], "fallback", reasons, audit or {})
        intent = ctx["allowed_intents"][0]
        return Reply(fallback_lines.get(intent, CLARIFY), intent, [], "fallback", reasons, audit or {})
