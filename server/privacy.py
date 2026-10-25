"""What a person is asked to agree to (p1-backend-s12). The text is versioned: when it changes, everyone is asked again. These are design texts for a qualified person to review before any real customer (DT-127);
they describe what the service really does, and they do not claim legal compliance."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

CONSENT_VERSION = "2026-10-25.1"        # the version of the notice and the questions below; legal documents carry the same version (content/legal)

LEGAL_DIR = Path(__file__).resolve().parents[1] / "content" / "legal"
DOCUMENTS = ("terms", "privacy_notice")


def legal_document(name: str) -> dict:
    """A legal document (Terms and Conditions, Privacy Notice) as sections of paragraphs, with its version and the hash of exactly what is shown."""
    if name not in DOCUMENTS:
        raise KeyError(name)
    raw = (LEGAL_DIR / (name + ".json")).read_bytes()
    doc = json.loads(raw.decode("utf-8"))
    doc["sha256"] = hashlib.sha256(raw.replace(b"\r\n", b"\n")).hexdigest()
    return doc


def text_hash(kind: str) -> str:
    """The fingerprint of what the person was shown when they answered this question: the question's own text and, for the questions that point to a document, that document."""
    parts = [CONSENT_VERSION, kind, CONSENT_TEXTS[kind]["text"]]
    for name in CONSENT_TEXTS[kind].get("documents", ()):
        parts.append(legal_document(name)["sha256"])
    return hashlib.sha256("\n".join(parts).encode("utf-8")).hexdigest()


CONSENT_TEXTS = {
    "terms": {
        "required": True,
        "documents": ("terms",),
        "title": "Terms and Conditions",
        "text": "I have read the Terms and Conditions and I accept them. They let me use ProcSense. They are not my consent to any optional use of my data: those are separate questions below.",
    },
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
    "cloud": {
        "required": False,
        "documents": ("privacy_notice",),
        "title": "Cloud AI processing of what I say (optional)",
        "text": (
            "When I speak to the simulator, my voice is turned into text on the ProcSense service and the recording is not kept. If I agree here, the text of what I said, up to four earlier lines of the conversation, "
            "the facts the invented patient may say at that moment, the list of available simulator actions and an identifier of the training scenario are sent over the internet to Ollama Cloud (Ollama Inc., who may process it in the United States), "
            "which runs the Gemma 4 31B language model, so that my spoken questions are understood and the patient and staff can answer. My name, email address, account identifiers and the audio are not sent. "
            "Ollama states that it processes this content transiently, does not store it beyond the time needed to answer, and does not use it to train models; it gives no fixed retention period and does not say where its models run, and I have read the Privacy Notice, which gives the details and what could not be verified. "
            "If I do not agree, or later withdraw, the same features use a smaller model on the ProcSense service and nothing I say is sent to Ollama; some features are simpler. My results and scores are not affected. "
            "I can withdraw at any time on the Privacy page; that stops new transmissions but cannot recall what was already processed."
        ),
    },
}
