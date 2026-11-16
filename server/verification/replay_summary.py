"""What the dashboard shows about hints and the verdict, taken from the server's own replay (P3-API-06), never from numbers the headset sends.

Once the engine has replayed a session and reproduced its log hash, the log is the engine's record: every accepted `request_hint` in it is a hint the trainee was given. Hints cost points in the examination only
(ResultSummary.HintsCharged in the Core: the assessment charges, practice does not). The verdict and end state are the replay's facts."""
from __future__ import annotations

import json

HINT_VERB = "request_hint"          # ProcSense.Core ExamControl.RequestHint
KEY = "server_replay"               # where the summary is kept inside the stored report


def summarise(log_text: str, mode: str, facts: dict) -> dict:
    hints = 0
    for line in log_text.split("\n"):
        if line:
            entry = json.loads(line)
            if entry.get("verb") == HINT_VERB and entry.get("accepted") is True:
                hints += 1
    return {"hints_used": hints, "hints_charged": hints if mode == "assessment" else 0, "verdict": facts.get("result"), "end_state": facts.get("end_state"), "source": "server_replay"}


def read(report: dict | None) -> dict:
    """The stored summary, or an empty one for a session verified before the summary existed (the dashboard then shows no hint count rather than a guessed zero)."""
    found = (report or {}).get(KEY)
    return found if isinstance(found, dict) else {}
