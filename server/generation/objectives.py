"""Learning objectives and the difficulty target (p1-live-scenario-generation-s01, stage LG-02).

    from generation.objectives import Tables, Ratings, derive

A Python mirror of the rating rules in src/ProcSense.Core/Difficulty/Difficulty.cs: one rating per behaviour per trainee (pseudonym), Elo with K 16 against the case's tier rating, the score of a session in a behaviour
being credits / (credits + penalties); no adaptation before 3 sessions. Both implementations are run on tests/vectors/difficulty_vectors.json so they cannot drift apart.
The objectives are chosen by these rules and not by a model: the choice must be explainable and give the same answer for the same history.
"""
from __future__ import annotations

import json
import math
from dataclasses import dataclass, field
from pathlib import Path

from . import contracts

ROOT = Path(__file__).resolve().parents[2]
DIMENSIONS = ROOT / "content" / "difficulty" / "dimensions.json"
TOP = 3


@dataclass(frozen=True)
class Dimension:
    id: str
    label: str
    weak_behaviours: tuple[str, ...]
    tag: str


@dataclass(frozen=True)
class Tables:
    k: float
    min_sessions: int
    start: float
    tier_offsets: dict[str, float]
    dimensions: tuple[Dimension, ...]
    explanations: dict[str, str]

    @staticmethod
    def load(path: Path = DIMENSIONS) -> "Tables":
        d = json.loads(path.read_text(encoding="utf-8"))
        return Tables(float(d["k_factor"]), int(d["min_sessions"]), float(d["start_rating"]), {k: float(v) for k, v in d["tier_offsets"].items()},
                      tuple(Dimension(x["id"], x["label"], tuple(x["weak_behaviours"]), x["tag"]) for x in d["dimensions"]), dict(d["explanations"]))

    def case_rating(self, tier: str) -> float:
        return self.start + self.tier_offsets.get(tier, 0.0)

    def for_behaviour(self, behaviour: str) -> Dimension | None:
        return next((d for d in self.dimensions if behaviour in d.weak_behaviours), None)


@dataclass
class SessionRecord:
    """One finished session: the case's tier and, per behaviour, the credits earned and the penalties taken (the competencies of the debrief facts)."""
    case: str
    tier: str
    behaviours: dict[str, tuple[int, int]]


@dataclass
class Ratings:
    tables: Tables
    rating: dict[str, float] = field(default_factory=dict)
    rated: dict[str, int] = field(default_factory=dict)
    sessions: int = 0

    def of(self, behaviour: str) -> float:
        return self.rating.get(behaviour, self.tables.start)

    def add(self, s: SessionRecord) -> None:
        self.sessions += 1
        case_rating = self.tables.case_rating(s.tier)
        for behaviour in sorted(s.behaviours):                      # ordinal order, as in the engine
            credits, penalties = s.behaviours[behaviour]
            total = credits + penalties
            if total == 0:
                continue
            score = credits / total
            r = self.of(behaviour)
            expected = 1.0 / (1.0 + math.pow(10, (case_rating - r) / 400.0))
            self.rating[behaviour] = r + self.tables.k * (score - expected)
            self.rated[behaviour] = self.rated.get(behaviour, 0) + 1

    @staticmethod
    def from_history(history: list[SessionRecord], tables: Tables) -> "Ratings":
        r = Ratings(tables)
        for s in history:
            r.add(s)
        return r

    def weakest(self, n: int = TOP) -> list[str]:
        """The rated behaviours, lowest rating first (ties by name), as the selector orders them."""
        return sorted(self.rated, key=lambda b: (self.of(b), b))[:n]


def derive(request: dict, history: list[SessionRecord], tables: Tables, recent_fingerprints: list[str] | None = None) -> dict:
    """Objectives v1 for an accepted request (one that has a seed and a request id) and the trainee's finished sessions in order."""
    ratings = Ratings.from_history(history, tables)
    focus = [f for f in request.get("focus", [])]
    recent_spines = [s.case for s in history[-3:]]
    if focus:
        basis = "focus_requested"
        behaviours = [b for b in focus if tables.for_behaviour(b)] or [b for d in tables.dimensions if d.id in focus for b in d.weak_behaviours[:1]]
    elif ratings.sessions < tables.min_sessions:
        basis, behaviours = "default_objectives", []
    else:
        basis, behaviours = "ratings", ratings.weakest()
    competencies = [{"behaviour": b, "rating": round(ratings.of(b), 3), "times_rated": ratings.rated.get(b, 0)} for b in behaviours][:TOP]
    dims: list[str] = []
    for b in behaviours:
        d = tables.for_behaviour(b)
        if d and d.id not in dims:
            dims.append(d.id)
    for f in focus:
        if any(d.id == f for d in tables.dimensions) and f not in dims:
            dims.append(f)
    # the target: each dimension's rating is the lowest rating among its behaviours that were rated, else the start rating; the dimensions to exercise are pulled one step toward the case tier above
    vector = {}
    for d in tables.dimensions:
        rated = [ratings.of(b) for b in d.weak_behaviours if b in ratings.rated]
        vector[d.id] = round(min(rated) if rated else tables.start, 3)
    if basis == "default_objectives":
        explanation = tables.explanations["default"].replace("{case}", "the standard first case for this family").replace("{min_sessions}", str(tables.min_sessions))
    elif competencies:
        d0 = tables.for_behaviour(competencies[0]["behaviour"])
        explanation = tables.explanations["adapted"].replace("{case}", "the next case").replace("{behaviour}", competencies[0]["behaviour"]).replace("{rating}", str(round(competencies[0]["rating"])))\
            .replace("{dimension}", d0.label if d0 else "the requested focus")
    else:
        explanation = "The requested focus names no behaviour or dimension this version knows; the standard objectives are used."
    objectives = {"version": 1, "request_id": request["request_id"], "scenario": request["scenario"], "level": request["level"], "seed": request["seed"], "basis": basis, "sessions_rated": ratings.sessions,
                  "competencies": competencies, "dimensions": dims[:TOP], "difficulty_vector": vector,
                  "exclusions": {"spines": list(dict.fromkeys(recent_spines)), "fingerprints": list(recent_fingerprints or [])[-20:]}, "explanation": explanation}
    contracts.validate("objectives", objectives)
    return objectives
