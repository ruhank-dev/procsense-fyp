"""The scenario quality judge (feature A9, capability A9.c2; stage LG-12): a language model scores the free text of a case against its facts. ADVISORY ONLY: it never approves, rejects or changes a case.

    scores = Judge(client).score(case)         # Scores(coherence, plausibility, persona, reading, comment) or None if the model could not be asked

It reads the same facts and lines as the text critic (server/generation/critic.py) and scores four things from 1 to 5. A low score is an advisory signal beside the deterministic metrics (server/generation/quality.py);
the pipeline does not use it to serve or refuse a package. The plan's acceptance needs a human-rated calibration set (Spearman with human ratings at least 0.6, EX-15), which does not exist, so the judge is measured only
against seeded defects (tools/ai/eval_judge.py) and its gate stays unset: it is built, advisory and unvalidated.
"""
from __future__ import annotations

from dataclasses import dataclass

try:
    from generation.critic import facts_of, lines_of
    from llm import gateway
except ImportError:
    from server.generation.critic import facts_of, lines_of
    from server.llm import gateway

DIMENSIONS = ("coherence", "plausibility", "persona", "reading")
SCHEMA = {"type": "object", "required": [*DIMENSIONS, "comment"], "additionalProperties": False,
          "properties": {**{d: {"type": "integer", "minimum": 1, "maximum": 5} for d in DIMENSIONS}, "comment": {"type": "string", "maxLength": 240}}}


@dataclass
class Scores:
    coherence: int
    plausibility: int
    persona: int
    reading: int
    comment: str

    def lowest(self) -> int:
        return min(getattr(self, d) for d in DIMENSIONS)


class Judge:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout

    def score(self, case: dict) -> Scores | None:
        facts, lines = facts_of(case), lines_of(case)
        r = gateway.run(self.client, "judge.scenario", {"facts": "\n".join(f"- {f}" for f in facts), "lines": "\n".join(f"{k}: {v}" for k, v in lines.items())}, schema=SCHEMA, model=self.model, timeout=self.timeout, retries=0, task="judge")
        if not r.ok:
            return None
        return Scores(**{d: int(r.value[d]) for d in DIMENSIONS}, comment=str(r.value["comment"]))
