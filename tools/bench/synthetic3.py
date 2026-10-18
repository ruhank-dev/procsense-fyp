"""The THIRD synthetic set (tests/classifier_synthetic_v3): a fresh, frozen hold-out of 300 sentences for judging a classifier change that was made AFTER the v2 test split had been scored once.

    uv run --project tools tools/bench/synthetic3.py sources|judge J1 --split test|judge J2 --split test|agreement|freeze|check

It is built and judged exactly like version 2 (tools/bench/synthetic2.py, whose functions it reuses with different constants): three new simulated author profiles (F1 calm plain-speaking nurse, F2 telegraphic
on-call doctor, F3 wordy hesitant clinician), two new scenario groups (G9 hip fracture and trauma, G10 eye day case), five sentences for each of the 20 labels per profile, two AI judge configurations of one local
model, contested rows settled by the builder. It has only a test split. It must not repeat any sentence of the old template set, version 1 or version 2 (checked). Like the others it is model-written, AI-judged
and NOT independent, and it is scored once.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import synthetic2 as S  # noqa: E402

S.OUT = ROOT / "tests" / "classifier_synthetic_v3"
S.SPLITS = ("test",)
S.PROFILES = {"F1": ("test", "calm plain-speaking ward nurse: contractions, short, Scottish-flavoured endearments"), "F2": ("test", "telegraphic on-call doctor: shorthand, fragments, no articles"),
              "F3": ("test", "wordy hesitant clinician: fillers, indirect requests, long sentences")}
S.GROUPS = {"test": {"G9", "G10"}}
S.PER_CELL = {"test": 5}
S.PREFIX = {"test": "V3F"}


def earlier() -> list[str]:
    texts = [json.loads(line)["text"] for n in ("train", "dev", "test") for line in (ROOT / "tests" / "classifier" / f"{n}.jsonl").read_text(encoding="utf-8").splitlines() if line.strip()]
    for folder in ("classifier_synthetic", "classifier_synthetic_v2"):
        texts += [json.loads(line)["text"] for n in ("train", "dev", "test") for line in (ROOT / "tests" / folder / f"{n}.jsonl").read_text(encoding="utf-8").splitlines() if line.strip()]
    return texts


S.EXTERNAL = earlier()

if __name__ == "__main__":
    sys.exit(S.main(sys.argv[1:]))
