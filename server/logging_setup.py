"""No personal data or secret in any log line (p1-backend-s12). Every log message is cleaned as it is made: email addresses, sign-in tokens, the headset tokens and the service's own keys are replaced.
This is a safety net: the code does not log them in the first place, and a test checks both."""
from __future__ import annotations

import logging
import re

PATTERNS = [
    (re.compile(r"[A-Za-z0-9._%+'-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"), "[email]"),
    (re.compile(r"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]*"), "[token]"),
    (re.compile(r"\bps[dr]_[A-Za-z0-9_-]{20,}"), "[token]"),
    (re.compile(r"\bsb_(secret|publishable)_[A-Za-z0-9_-]+"), "[key]"),
    (re.compile(r"(?i)(bearer\s+)[A-Za-z0-9._~+/=-]{12,}"), r"\1[token]"),
]


def clean(text: str) -> str:
    for pattern, replacement in PATTERNS:
        text = pattern.sub(replacement, text)
    return text


_installed = False


def install() -> None:
    """Makes every log record clean its own message. Safe to call more than once."""
    global _installed
    if _installed:
        return
    previous = logging.getLogRecordFactory()

    def factory(*args, **kwargs):
        record = previous(*args, **kwargs)
        original = record.getMessage

        def get_message() -> str:
            return clean(original())
        record.getMessage = get_message   # type: ignore[method-assign]
        return record

    logging.setLogRecordFactory(factory)
    _installed = True
