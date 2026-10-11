"""The Stage B checks, one module per check, in the order they run (p0-case-library s04, guide B-M2).

Every check is `check(card, ctx) -> list[Defect]`. A Defect says which check found it (1 to 11), a code (for example RESULTS_VS_STATE) and the detail.
Checks run in order and every defect is collected for the report (fail fast on the order, never lose information).
"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Defect:
    check: int
    code: str
    detail: str

    def as_dict(self) -> dict:
        return {"check": self.check, "code": self.code, "detail": self.detail}
