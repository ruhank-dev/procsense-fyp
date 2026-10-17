"""The prompt registry (OFF-09, p1-ai-language-s02): every prompt a model is given is a file in prompts/, with a version and a hash, so any output can be traced to the exact words that produced it.

    p = get("compose.narrative")                         # the latest version; get("compose.narrative", 1) for a given one
    system, user = p.render(patient="...", facts="...")  # {{name}} slots; a missing or an extra slot is an error
    p.schema                                             # the JSON Schema the answer must satisfy (None for free text)

prompts/registry.json lists the prompts; a prompt file has a system part and a user part separated by a line `=== user ===`. Editing a prompt file without a new version and a new hash entry is caught
by `check()` (run in CI): a released prompt is immutable, a change is a new version. The registry keeps no secrets and no patient data; slots are filled at call time and the filled text is never stored here
(the audit keeps a hash of it).
"""
from __future__ import annotations

import hashlib
import json
import re
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROMPTS = ROOT / "prompts"
REGISTRY = PROMPTS / "registry.json"
SPLIT = "=== user ==="
SLOT = re.compile(r"\{\{([a-z_][a-z0-9_]*)\}\}")
ROLES = ("live", "batch")


class PromptError(ValueError):
    pass


@dataclass(frozen=True)
class Prompt:
    id: str
    version: int
    role: str                      # "live" (4B, thinking off) or "batch" (9B)
    system: str
    user: str
    variables: tuple[str, ...]
    schema: dict | None
    num_predict: int
    think: bool
    sha256: str
    gateway: bool = False           # may be called through the AI gateway (it also needs a validator in validators.py)

    @property
    def key(self) -> str:
        return f"{self.id}@{self.version}"

    def render(self, **values: str) -> tuple[str, str]:
        extra = set(values) - set(self.variables)
        missing = set(self.variables) - set(values)
        if extra or missing:
            raise PromptError(f"{self.key}: slots given but not in the prompt {sorted(extra)}; slots in the prompt but not given {sorted(missing)}")
        fill = lambda text: SLOT.sub(lambda m: str(values[m.group(1)]), text)        # noqa: E731
        return fill(self.system), fill(self.user)


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def _parse(path: Path) -> tuple[str, str]:
    text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
    if SPLIT not in text:
        raise PromptError(f"{path.name}: no '{SPLIT}' line between the system part and the user part")
    system, user = text.split(f"\n{SPLIT}\n", 1) if f"\n{SPLIT}\n" in text else text.split(SPLIT, 1)
    return system.strip(), user.strip()


@lru_cache
def registry() -> dict:
    return json.loads(REGISTRY.read_text(encoding="utf-8"))


def get(prompt_id: str, version: int | None = None) -> Prompt:
    entries = [e for e in registry()["prompts"] if e["id"] == prompt_id]
    if not entries:
        raise PromptError(f"no prompt {prompt_id}")
    e = max(entries, key=lambda x: x["version"]) if version is None else next((x for x in entries if x["version"] == version), None)
    if e is None:
        raise PromptError(f"no version {version} of {prompt_id}")
    path = PROMPTS / e["file"]
    if file_hash(path) != e["sha256"]:
        raise PromptError(f"{e['file']} changed without a new version: a released prompt is immutable (its hash is in prompts/registry.json)")
    system, user = _parse(path)
    schema = json.loads((ROOT / e["schema"]).read_text(encoding="utf-8")) if e.get("schema") else None
    slots = tuple(dict.fromkeys(SLOT.findall(system + "\n" + user)))
    if set(slots) != set(e["variables"]):
        raise PromptError(f"{e['file']}: the slots in the text {sorted(slots)} are not the variables the registry lists {sorted(e['variables'])}")
    return Prompt(e["id"], e["version"], e["role"], system, user, tuple(e["variables"]), schema, int(e.get("num_predict", 400)), bool(e.get("think", False)), e["sha256"], bool(e.get("gateway", False)))


def check() -> list[str]:
    """Problems with the registry; empty when every prompt file is registered, hashed, parseable, and every registered file exists."""
    problems: list[str] = []
    reg = registry()
    seen: set[tuple[str, int]] = set()
    for e in reg["prompts"]:
        key = (e["id"], e["version"])
        if key in seen:
            problems.append(f"{e['id']} version {e['version']} is listed twice")
        seen.add(key)
        if e["role"] not in ROLES:
            problems.append(f"{e['id']}: role {e['role']} is not one of {ROLES}")
        try:
            get(e["id"], e["version"])
        except (PromptError, OSError, ValueError) as err:
            problems.append(str(err))
    listed = {e["file"] for e in reg["prompts"]}
    for p in PROMPTS.rglob("*.txt"):
        if p.relative_to(PROMPTS).as_posix() not in listed:
            problems.append(f"prompts/{p.relative_to(PROMPTS).as_posix()} is not in the registry")
    return problems
