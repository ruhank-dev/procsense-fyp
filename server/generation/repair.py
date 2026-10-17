"""Repair, regeneration or rejection (p1-live-scenario-generation-s08, stage LG-12).

    strategy = choose(defects, already_tried)        # the cheapest fix that can cure the first defect
    Budget(rounds=3, seconds=20).spent()             # a candidate that runs out is rejected, not served

A deterministic table maps a defect code to strategies from the cheapest to the dearest: redraw the free numbers, choose the pictures again, swap the variable blocks, swap the spine. A narrative
defect would also try a model re-ask; there is no narrative model step yet (s04), so a narrative defect goes to block and spine swaps. A repaired candidate is never trusted: the pipeline
(pipeline.py) repeats every check, from the first, on what the repair produced. At most 3 rounds and the time budget; after that the caller serves a pooled package or a library case (FB-03).
Every attempt is returned for the provenance record.
"""
from __future__ import annotations

import time
from dataclasses import dataclass, field

# cheapest first
REDRAW_NUMBERS, RESELECT_MEDIA, SWAP_BLOCKS, SWAP_SPINE = "redraw_numbers", "reselect_media", "swap_blocks", "swap_spine"
ORDER = (REDRAW_NUMBERS, RESELECT_MEDIA, SWAP_BLOCKS, SWAP_SPINE)

TABLE: dict[str, tuple[str, ...]] = {
    # numbers that disagree with each other or with the story
    "VR-CM-01": (REDRAW_NUMBERS, RESELECT_MEDIA, SWAP_BLOCKS), "VR-CM-02": (RESELECT_MEDIA, REDRAW_NUMBERS, SWAP_SPINE), "VR-CM-06": (REDRAW_NUMBERS, SWAP_SPINE),
    "VR-CM-08": (REDRAW_NUMBERS, SWAP_BLOCKS, SWAP_SPINE),
    # pictures
    "MEDIA": (RESELECT_MEDIA, SWAP_SPINE), "VR-CM-03": (RESELECT_MEDIA, SWAP_SPINE), "VR-CM-04": (RESELECT_MEDIA, SWAP_SPINE),
    # the patient's own blocks
    "VR-CM-07": (SWAP_BLOCKS, SWAP_SPINE), "Q_PERSONA": (SWAP_BLOCKS, SWAP_SPINE), "Q_COHERENCE": (SWAP_BLOCKS, SWAP_SPINE), "Q_READING": (SWAP_BLOCKS, SWAP_SPINE),
    "VR-CM-05": (SWAP_SPINE,),
    # the spine itself
    "Q_TEACHABLE": (SWAP_SPINE,), "Q_DISCOVERABLE": (SWAP_SPINE,), "Q_DIFFICULTY": (SWAP_SPINE,), "Q_COVERAGE": (SWAP_SPINE,), "Q_FAIRNESS": (SWAP_SPINE,),
    "LOW_NOVELTY": (SWAP_BLOCKS, SWAP_SPINE), "CRITIC": (SWAP_BLOCKS, SWAP_SPINE), "SIDE_VS_EXAM": (SWAP_BLOCKS, SWAP_SPINE), "SIDE_VS_OPERATION": (SWAP_BLOCKS, SWAP_SPINE), "PAIN_DENIED": (SWAP_BLOCKS, SWAP_SPINE), "UNLOCKED_NUMBER": (RESELECT_MEDIA, SWAP_SPINE),
}
DEFAULT = (SWAP_BLOCKS, SWAP_SPINE)                     # a Stage B or schema defect with no entry: change the blocks, then the story


def choose(defects: list[dict], tried: list[str] | None = None) -> str | None:
    """The first strategy not yet tried for the first defect that has one left; None when every strategy for every defect has been tried (reject)."""
    done = set(tried or [])
    for d in defects:
        for s in TABLE.get(d["code"], DEFAULT):
            if s not in done:
                return s
    return None


@dataclass
class Budget:
    rounds: int = 3
    seconds: float = 20.0
    started: float = field(default_factory=time.monotonic)
    used: int = 0

    def spent(self) -> bool:
        return self.used >= self.rounds or time.monotonic() - self.started > self.seconds

    def take(self) -> None:
        self.used += 1


def kwargs_for(strategy: str, objectives: dict, spine: str, round_no: int) -> tuple[dict, dict]:
    """(objective changes, build_skeleton keyword arguments) that carry out a strategy. Streams are named by round so a repair never repeats an earlier draw."""
    salt = 1_000_003 * round_no
    if strategy == REDRAW_NUMBERS:
        return {}, {"force_spine": spine, "numeric_seed": objectives["seed"] + salt}
    if strategy == RESELECT_MEDIA:
        return {}, {"force_spine": spine, "media_seed": objectives["seed"] + salt}
    if strategy == SWAP_BLOCKS:
        return {"seed": objectives["seed"] + 7919 * round_no}, {"force_spine": spine}
    if strategy == SWAP_SPINE:
        ex = dict(objectives["exclusions"])
        ex["spines"] = list(dict.fromkeys([*ex["spines"], spine]))
        return {"exclusions": ex, "seed": objectives["seed"] + 104729 * round_no}, {}
    raise ValueError(strategy)
