"""File names of the feature plans (docs/plans/features): a plan whose feature is built and tested by the team carries the "✅ " prefix, like a ticked step file (until 27 Oct 2026 the prefix was "[x] ").

    plan_name("A1", "Complete")  ->  "✅ A1.md";   plan_name("A9", "Partly built")  ->  "A9.md"
    plan_link("A1", "Complete", "../plans/features")  ->  "../plans/features/%E2%9C%85%20A1.md"

The prefix is derived, never typed: a feature is ticked when tools/plans/feature_matrix.py derives "Complete", "Engineering complete" or "Engineering complete, gated off" (built and tested, its switch OFF until human evidence exists: the plan's checklist says so) for it (everything the team can build and test without a headset is done; the headset
checks that remain are Sannan's entries in docs/testing/SANNAN_EXTERNAL_TESTING.md and are listed inside the plan). A feature that is built but gated off, partly built, owned by others or deferred is not ticked.
tools/plans/pipeline_docs.py writes the files under these names and removes the other spelling.
"""
from urllib.parse import quote

TICKED = ("Complete", "Engineering complete", "Engineering complete, gated off")
PREFIX = "✅ "
LEGACY_PREFIXES = ("[x] ",)                 # the spelling before 27 Oct 2026: still understood, and removed when the plan is rewritten under the current name


def ticked(result: str) -> bool:
    return result in TICKED


def plan_name(fid: str, result: str) -> str:
    return f"{PREFIX if ticked(result) else ''}{fid}.md"


def plan_link(fid: str, result: str, base: str) -> str:
    return f"{base}/{quote(plan_name(fid, result))}"


def feature_id(file_name: str) -> str:
    """'✅ A1.md', the legacy '[x] A1.md' and 'A1.md' all give 'A1'."""
    stem = file_name[:-3] if file_name.endswith(".md") else file_name
    for p in (PREFIX,) + LEGACY_PREFIXES:
        if stem.startswith(p):
            return stem[len(p):]
    return stem
