"""What a person is asked to agree to (p1-backend-s12). The text is versioned: when it changes, everyone is asked again. These are design texts for a qualified person to review before any real customer (DT-127);
they describe what the service really does, and they do not claim legal compliance."""
from __future__ import annotations

CONSENT_VERSION = "2026-10-14.1"

CONSENT_TEXTS = {
    "results": {
        "required": True,
        "title": "Storing my training results",
        "text": (
            "ProcSense stores what I do in training sessions: the actions I take in the simulator, my scores and my reports, linked to my account. "
            "My organisation's supervisors and administrators can see these results, including my display name. My email address stays with the sign-in service and is not shown with my results. "
            "Results are kept for the period my organisation chose (24 months unless it chose another) and then the stored session log is removed. "
            "I can look at who opened my results, download everything held about me, or delete my account at any time from the Privacy page. "
            "All patients in the cases are made up; no real patient information is involved. I need to agree to this to have my sessions stored."
        ),
    },
    "research": {
        "required": False,
        "title": "Using my results in research (optional)",
        "text": (
            "ProcSense may use my sessions in studies that evaluate and improve the training. In those studies my name and email address are never included; "
            "only a code that cannot be traced to me without my organisation's records. Saying no changes nothing about my access, my results or what my supervisors see. I can change my mind at any time."
        ),
    },
}
