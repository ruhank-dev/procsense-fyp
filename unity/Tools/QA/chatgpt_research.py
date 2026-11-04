"""Text-only research questions to ChatGPT through the project's authorised `chatgpt_plan` adapter (same integration as chatgpt_review.py).

  python unity/Tools/QA/chatgpt_research.py <topic-name> "<question>"

The answer is stored verbatim under docs/research/chatgpt/<topic>.md. ChatGPT cannot browse reliably, so every URL it names is a lead to verify, never a source of truth.
"""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "docs" / "3d-blender-unity-skills"))
from integrations.adapters import make_adapter  # noqa: E402

SYSTEM = ("You are a senior technical artist advising a Unity 6 URP hospital VR training project (desktop Enhanced URP plus Quest 2). "
          "Recommend only free assets whose licence allows redistribution in a public repository (CC0 preferred). Name the exact asset, author, URL and licence, "
          "say plainly when you are unsure a URL or licence is right, and never invent assets.")

topic, question = sys.argv[1], sys.argv[2]
adapter = make_adapter("chatgpt_plan")
text = adapter._respond([{"role": "user", "content": [{"type": "input_text", "text": question}]}], SYSTEM)
out = ROOT / "docs" / "research" / "chatgpt"; out.mkdir(parents=True, exist_ok=True)
(out / f"{topic}.md").write_text(f"# ChatGPT research: {topic}\n\nQuestion: {question}\n\n{text}\n", encoding="utf-8")
print(text[:6000])
