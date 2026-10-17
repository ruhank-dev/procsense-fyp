"""Practice-mode explanation of an opened result (p2-ai-learning-analytics-s05, feature B22): a teaching aid grounded in the result's own row and approved report.

    text = Interpreter(client).explain(result_row, report_text, mode="practice")       # an Explanation, or None: nothing is shown

The explanation is shown only in practice mode (never in assessment, where it would be help). The model is given the result row (test, value, unit, reference range, flag, comment) and, for a picture, the
approved report text, and writes at most two sentences saying what the result means for this patient in plain words. The answer passes the same grounding rules as everything else: the numbers must be those
of the row or report, no medicine, condition or equipment may appear that the row and report do not hold, no dose, no prescribing phrase. If it fails once it is retried with the reasons; after that nothing
is shown (the result stays as it was: the plain row). Template fallback: the row itself.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

try:
    from llm import gateway
    from validation.validator import PRESCRIBE, content_words, has_term, norm, numbers
except ImportError:
    from server.llm import gateway
    from server.validation.validator import PRESCRIBE, content_words, has_term, norm, numbers

ROOT = Path(__file__).resolve().parents[2]


@dataclass
class Explanation:
    text: str
    source: str            # "model"


def defects(text: str, row: dict, report: str, lexicon: dict) -> list[str]:
    allowed_text = " ".join([str(row.get(k, "")) for k in ("test", "value", "unit", "normal_range", "flag", "comment")] + [report])
    out = []
    if any(n not in numbers(allowed_text) for n in numbers(text)):
        out.append("UNGROUNDED_NUMBER: a number that is not in the result or its report")
    allowed = norm(allowed_text)
    for cat in ("drug", "test", "condition", "allergen", "equipment"):
        for term in lexicon["categories"][cat]:
            if has_term(norm(text), term) and not has_term(allowed, term):
                out.append(f"OFF_CARD_ENTITY: {term}")
    if PRESCRIBE.search(text):
        out.append("PRESCRIBE_PHRASING: do not tell anyone to give or prescribe anything")
    if len(text) > 400:
        out.append("LENGTH: at most two short sentences")
    return out


class Interpreter:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout
        self.lexicon = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))

    def explain(self, row: dict, report: str = "", mode: str = "practice") -> Explanation | None:
        if mode != "practice" or self.client is None:
            return None
        facts = "; ".join(f"{k}: {row[k]}" for k in ("test", "value", "unit", "normal_range", "flag", "comment") if row.get(k)) + (f". Report: {report}" if report else "")
        r = gateway.run(self.client, "analytics.explain_result", {"result": facts}, validate=lambda v: defects(v["text"], row, report, self.lexicon), model=self.model, timeout=self.timeout, retries=1, task="explain_result")
        return Explanation(r.value["text"].strip(), "model") if r.ok else None
