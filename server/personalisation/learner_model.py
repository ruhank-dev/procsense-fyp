"""The learner model: what a trainee has shown, and how sure we are (p1-personalisation-s02, features D67 and A11).

    m = LearnerModel(tables)
    m.update(evidence)                       # one SessionEvidence (evidence.py), in order; cheap
    m.mastery("b14")                         # Mastery(mean, low, high, sessions, status)
    m.predict("b14", tier="P1")              # chance the next item of that behaviour is done right

For every behaviour a Beta distribution of "done right": each session adds its credits to the successes and its penalties to the failures (a critical or major error counts for more, as the scoring rubric says),
and before each update the evidence already held fades a little (forgetting factor FORGET per session, so recent sessions count most). The posterior mean and an 80 percent interval (normal approximation of the Beta)
say how sure the model is: with little evidence the interval is wide and no claim is made. Status: `unknown` (no evidence), `hypothesis` (a low mean on fewer than MIN_SESSIONS sessions: never acted on as a fact),
`weakness` (a low mean AND the whole interval below WEAK_BELOW, on at least MIN_SESSIONS sessions with evidence), `strength` (the interval above STRONG_ABOVE), otherwise `developing`. One bad session
therefore cannot make a weakness: it makes a hypothesis. The model is arithmetic, not a trained neural model; its use is justified only by predicting the next session better than the Elo rating the engine already keeps
(tools/personalisation/simulate.py and the tests). Difficulty enters the prediction the same way as in Elo: a case that is `d` rating points harder lowers the odds by d/400 on the base-10 logit scale.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field

PRIOR = (1.0, 1.0)
FORGET = 0.9
MIN_SESSIONS = 3
WEAK_BELOW = 0.6
STRONG_ABOVE = 0.75
Z80 = 1.2816
SEVERITY_WEIGHT = {"minor": 1.0, "major": 2.0, "critical": 3.0}
DEFAULT_OFFSETS = {"P0": 0.0, "P1": 100.0, "P2": 200.0}


@dataclass
class Mastery:
    mean: float
    low: float
    high: float
    sessions: int
    status: str
    evidence: float                      # the weight of evidence held (successes plus failures after fading)


@dataclass
class _Beta:
    a: float = PRIOR[0]
    b: float = PRIOR[1]
    sessions: int = 0


def _logit(p: float) -> float:
    p = min(max(p, 1e-6), 1 - 1e-6)
    return math.log(p / (1 - p))


def _sigmoid(x: float) -> float:
    return 1 / (1 + math.exp(-x))


class LearnerModel:
    def __init__(self, tier_offsets: dict[str, float] | None = None, forget: float = FORGET):
        self.beta: dict[str, _Beta] = {}
        self.offsets = tier_offsets or DEFAULT_OFFSETS
        self.forget = forget
        self.sessions = 0

    # ---- learning -----------------------------------------------------------------------------------------
    def update(self, evidence: dict) -> None:
        """One finished session. Credits are successes; each error counts as `SEVERITY_WEIGHT` failures. A behaviour the session did not touch fades, but its sessions count does not move."""
        self.sessions += 1
        touched = {c["behaviour"]: c for c in evidence["competencies"]}
        severe: dict[str, float] = {}
        for e in evidence["errors"]:
            severe[e["behaviour"]] = severe.get(e["behaviour"], 0.0) + SEVERITY_WEIGHT[e["severity"]] - 1.0      # the extra weight beyond one failure
        for name, b in self.beta.items():
            b.a = PRIOR[0] + (b.a - PRIOR[0]) * self.forget
            b.b = PRIOR[1] + (b.b - PRIOR[1]) * self.forget
        for name, c in touched.items():
            b = self.beta.setdefault(name, _Beta())
            b.a += c["credits"]
            b.b += c["penalties"] + severe.get(name, 0.0)
            b.sessions += 1

    # ---- reading ------------------------------------------------------------------------------------------
    def mastery(self, behaviour: str) -> Mastery:
        b = self.beta.get(behaviour)
        if b is None or b.sessions == 0:
            return Mastery(PRIOR[0] / sum(PRIOR), 0.0, 1.0, 0, "unknown", 0.0)
        n = b.a + b.b
        mean = b.a / n
        sd = math.sqrt(b.a * b.b / (n * n * (n + 1)))
        low, high = max(0.0, mean - Z80 * sd), min(1.0, mean + Z80 * sd)
        if b.sessions >= MIN_SESSIONS and high < WEAK_BELOW:
            status = "weakness"
        elif b.sessions >= MIN_SESSIONS and low > STRONG_ABOVE:
            status = "strength"
        elif mean < WEAK_BELOW:
            status = "hypothesis"
        else:
            status = "developing"
        return Mastery(round(mean, 4), round(low, 4), round(high, 4), b.sessions, status, round(n - sum(PRIOR), 3))

    def weaknesses(self) -> list[str]:
        """Behaviours with status `weakness`, lowest mean first (ties by name)."""
        return [n for _, n in sorted((self.mastery(n).mean, n) for n in self.beta if self.mastery(n).status == "weakness")]

    def hypotheses(self) -> list[str]:
        return [n for _, n in sorted((self.mastery(n).mean, n) for n in self.beta if self.mastery(n).status == "hypothesis")]

    def predict(self, behaviour: str, tier: str = "P0") -> float:
        """The chance the next item of this behaviour is done right on a case of this tier: the posterior mean moved by the tier's offset on the Elo logit scale."""
        m = self.mastery(behaviour).mean
        return _sigmoid(_logit(m) - self.offsets.get(tier, 0.0) * math.log(10) / 400.0)

    def snapshot(self) -> dict:
        return {n: self.mastery(n).__dict__ for n in sorted(self.beta)}


@dataclass
class EloBaseline:
    """The engine's rating (src/ProcSense.Core/Difficulty): K 16, the score of a session is credits/(credits+penalties), the expectation is against the case's tier rating. Used to compare predictions."""
    k: float = 16.0
    start: float = 1000.0
    offsets: dict[str, float] = field(default_factory=lambda: dict(DEFAULT_OFFSETS))
    rating: dict[str, float] = field(default_factory=dict)

    def predict(self, behaviour: str, tier: str = "P0") -> float:
        r = self.rating.get(behaviour, self.start)
        return 1.0 / (1.0 + 10 ** (((self.start + self.offsets.get(tier, 0.0)) - r) / 400.0))

    def update(self, evidence: dict) -> None:
        tier = evidence["tier"]
        for c in sorted(evidence["competencies"], key=lambda c: c["behaviour"]):
            total = c["credits"] + c["penalties"]
            if total == 0:
                continue
            r = self.rating.get(c["behaviour"], self.start)
            self.rating[c["behaviour"]] = r + self.k * (c["credits"] / total - self.predict(c["behaviour"], tier))
