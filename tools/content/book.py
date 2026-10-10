"""The walkthrough as pages of normalised text, for the verbatim check.

Every content row that comes from the book keeps a `quote` (the exact words it was read from) and a `page`. The verbatim check
(tools/content/verbatim.py) confirms the quote is on that page, and that every number in the row is inside the quote.

The markdown conversion flattened the tables, so the check works on normalised text: escapes such as \\~ \\> \\< \\( \\- removed, runs of
whitespace and line breaks collapsed, page footers dropped. Nothing else is changed: no rewording, no number fixes.
"""
from __future__ import annotations

import re
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WALKTHROUGH = ROOT / "docs" / "context" / "ProcSense_Medical_Walkthrough_v5_1.md"

FOOTER = re.compile(r"^\*Sources cited at point of use and in Part H .*? Page (\d+)\*\s*$")
SKIP = re.compile(r"^(\*ProcSense Medical Walkthrough v5\.1|---\s*$)")


def normalise(text: str) -> str:
    text = re.sub(r"\\([\\~><()\[\]\-.*_#+!|`])", r"\1", text)   # markdown escapes the converter added
    text = text.replace(" ", " ")
    return re.sub(r"\s+", " ", text).strip()


@lru_cache(maxsize=1)
def pages() -> dict[int, str]:
    """Page number -> normalised text of that page. Text before a footer belongs to the page that footer names."""
    out: dict[int, list[str]] = {}
    pending: list[str] = []
    for line in WALKTHROUGH.read_text(encoding="utf-8").splitlines():
        m = FOOTER.match(line.strip())
        if m:
            out.setdefault(int(m.group(1)), []).extend(pending)
            pending = []
        elif not SKIP.match(line.strip()):
            pending.append(line)
    return {n: normalise(" ".join(parts)) for n, parts in out.items()}


def page_text(first: int, last: int | None = None) -> str:
    """Text of pages first..last (a quote may run across a page break)."""
    last = last or first
    p = pages()
    return " ".join(p.get(n, "") for n in range(first, last + 1))


def find_page(quote: str) -> list[int]:
    """Pages whose text contains the quote (normalised). Used to fill the page column, then re-checked."""
    q = normalise(quote)
    return [n for n, t in sorted(pages().items()) if q in t]
