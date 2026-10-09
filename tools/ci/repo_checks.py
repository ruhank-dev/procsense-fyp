"""Project-rule checks that no compiler or unit test covers. Standard library only.

Usage (from repo root):  task check     or     uv run --project tools tools/ci/repo_checks.py [check ...]
Checks: core-rules, hygiene, docs-links, deferred-register, prompt-history, steps, team-guards
Exit code 0 = all pass, 1 = at least one failure. Add a new check here (and a row in docs/CI.md) whenever a new rule appears.
"""
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[2]
sys.stdout.reconfigure(encoding="utf-8")


def tracked_files() -> list[Path]:
    """Files git knows about (so ignored build output never causes noise)."""
    out = subprocess.run(["git", "ls-files", "-z"], cwd=ROOT, capture_output=True, check=True).stdout.decode("utf-8")
    return [ROOT / p for p in out.split("\0") if p and (ROOT / p).exists()]


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8", errors="replace")


# --- core-rules: determinism and engine-independence rules for src/ProcSense.Core (Implementation Guide B-R11) ---
CORE_RULES = [
    (r"\bDateTime\.(Now|UtcNow|Today)\b", "wall clock: use SimClock"),
    (r"\bnew\s+(System\.)?Random\s*\(", "System.Random: use SimRandom streams"),
    (r"\bGuid\.NewGuid\b", "random GUIDs break replay"),
    (r"\bUnityEngine\b", "Core must not reference Unity"),
    (r"\bEnvironment\.GetEnvironmentVariable\b", "settings must not read environment variables directly"),
    (r"\bStopwatch\b", "real elapsed time breaks determinism"),
    (r"\bTask\.Delay\b|\bThread\.Sleep\b", "real waiting breaks determinism"),
    (r"\.GetHashCode\s*\(\s*\)", "string hash codes differ per process: use a stable hash"),
    (r"\bConsole\.", "Core does not print; the CLI does"),
]


def check_core_rules() -> list[str]:
    problems = []
    for p in tracked_files():
        rel = p.relative_to(ROOT).as_posix()
        if not (rel.startswith("src/ProcSense.Core/") and p.suffix == ".cs"):
            continue
        for n, line in enumerate(read(p).splitlines(), 1):
            code = line.split("//", 1)[0]
            for pattern, why in CORE_RULES:
                if re.search(pattern, code) and "ci-allow" not in line:
                    problems.append(f"{rel}:{n}: {why}")
    return problems


# --- hygiene: files that must never be committed ---
SECRET_PATTERNS = [
    (r"-----BEGIN (RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----", "private key"),
    (r"\bghp_[A-Za-z0-9]{36}\b|\bgithub_pat_[A-Za-z0-9_]{50,}\b", "GitHub token"),
    (r"\bAKIA[0-9A-Z]{16}\b", "AWS access key"),
    (r"\bxox[baprs]-[A-Za-z0-9-]{10,}\b", "Slack token"),
    (r"(?i)\b(password|passwd|api[_-]?key|secret)\b\s*[:=]\s*[\"'][^\"'\s]{6,}[\"']", "hard-coded credential"),
]
FORBIDDEN_PATHS = [
    (r"(^|/)(bin|obj)/", "build output"),
    (r"(^|/)\.venv/", "virtual environment"),
    (r"(^|/)\.env($|\.)", "environment file (use .env.example)"),
    (r"\.session\.jsonl$", "recorded session output"),
    (r"^(result|bad)[^/]*\.json$", "CLI test output in the repo root"),
]
TEXT_SUFFIXES = {".cs", ".py", ".yml", ".yaml", ".json", ".csproj", ".toml", ".sln", ".props", ".ps1", ".sh"}


def check_hygiene() -> list[str]:
    problems = []
    for p in tracked_files():
        rel = p.relative_to(ROOT).as_posix()
        for pattern, why in FORBIDDEN_PATHS:
            if re.search(pattern, rel) and not rel.endswith(".env.example"):
                problems.append(f"{rel}: {why} must not be committed")
        if p.stat().st_size > 5_000_000 and p.suffix.lower() not in {".png", ".jpg", ".fbx", ".blend", ".wav", ".ogg", ".pdf"}:
            problems.append(f"{rel}: larger than 5 MB (use Git LFS or rebuild it from a script)")
        if p.suffix in TEXT_SUFFIXES and not rel.startswith(("tools/uv.lock", "tools/ci/")):
            text = read(p)
            for pattern, why in SECRET_PATTERNS:
                if re.search(pattern, text):
                    problems.append(f"{rel}: looks like a {why}")
    return problems


# --- docs-links: every relative Markdown link must point at something that exists ---
LINK = re.compile(r"(?<!\!)\[[^\]\n]*\]\(([^)\s]+)\)")


def check_docs_links() -> list[str]:
    problems = []
    for p in tracked_files():
        if p.suffix != ".md" or p.relative_to(ROOT).as_posix().startswith("docs/context/"):
            continue  # docs/context holds verbatim conversions of the original documents
        for n, line in enumerate(read(p).splitlines(), 1):
            for target in LINK.findall(line):
                if re.match(r"^(https?:|mailto:|#)", target):
                    continue
                path = unquote(target.split("#", 1)[0])
                if path and not (p.parent / path).exists():
                    problems.append(f"{p.relative_to(ROOT).as_posix()}:{n}: broken link -> {target}")
    return problems


# --- deferred-register: docs/deferred-tasks.md stays well-formed ---
def check_deferred_register() -> list[str]:
    f = ROOT / "docs" / "deferred-tasks.md"
    if not f.exists():
        return ["docs/deferred-tasks.md is missing"]
    problems, seen = [], set()
    allowed = {"Pending", "In progress", "Completed", "Blocked"}
    for n, line in enumerate(read(f).splitlines(), 1):
        m = re.match(r"^\| (DT-\d{3}) \|", line)
        if not m:
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if m.group(1) in seen:
            problems.append(f"docs/deferred-tasks.md:{n}: duplicate id {m.group(1)}")
        seen.add(m.group(1))
        if len(cells) != 6:
            problems.append(f"docs/deferred-tasks.md:{n}: {m.group(1)} has {len(cells)} columns, expected 6")
        elif cells[2].replace("*", "") not in allowed:
            problems.append(f"docs/deferred-tasks.md:{n}: {m.group(1)} has status '{cells[2]}', expected one of {sorted(allowed)}")
    if not seen:
        problems.append("docs/deferred-tasks.md: no DT-xxx rows found")
    return problems


# --- prompt-history: entries are numbered 1..N without gaps and each has its text block ---
def check_prompt_history() -> list[str]:
    f = ROOT / "docs" / "previous_prompt.md"
    if not f.exists():
        return ["docs/previous_prompt.md is missing"]
    text = read(f)
    problems = []
    body = text.split("---", 1)[-1]
    # An entry heading is "## N. <date>" followed by the "_Session:" line; headings inside a pasted prompt do not count.
    numbers = [int(x) for x in re.findall(r"^## (\d+)\. [^\n]*\n_Session:", body, flags=re.M)]
    if numbers != list(range(1, len(numbers) + 1)):
        problems.append(f"docs/previous_prompt.md: entries are not numbered 1..N in order (found {numbers[:5]}...)")
    if len(re.findall(r"^``````text$", body, flags=re.M)) != len(numbers):
        problems.append("docs/previous_prompt.md: every entry needs exactly one ``````text block")
    return problems


# --- steps: step files are named, titled and assigned consistently ---
def check_steps() -> list[str]:
    problems = []
    for p in sorted((ROOT / "docs" / "plans").glob("p*/p*-*/steps/*.md")):
        name = p.name.removeprefix("✅ ")
        sid = re.match(r"(p[0-2]-[a-z0-9-]+?-s\d{2})-", name)
        first = read(p).splitlines()[0] if read(p) else ""
        if not sid:
            problems.append(f"{p.name}: file name does not start with <tier>-<phase>-sNN-")
        elif not first.startswith(f"# {sid.group(1)} "):
            problems.append(f"{p.name}: first line must start with '# {sid.group(1)} —'")
    return problems


# --- team-guards: the files that protect main from teammates must not be deleted by accident ---
TEAM_GUARDS = [
    "CONTRIBUTING.md", "docs/TEAM_GIT_WORKFLOW.md", "docs/CI.md", ".github/branch-protection.json", ".github/dependabot.yml",
    ".github/workflows/ci.yml", ".github/workflows/release.yml", ".github/workflows/main-guard.yml",
    ".githooks/pre-push", "tools/ci/test_pre_push_hook.py", "tools/ci/setup_check.py",
]


def check_team_guards() -> list[str]:
    # On disk, not "tracked": CI checks out only committed files, so a missing commit shows up there.
    problems = [f"{g}: missing (it protects main; see docs/CI.md)" for g in TEAM_GUARDS if not (ROOT / g).exists()]
    hook = ROOT / ".githooks" / "pre-push"
    if hook.exists():
        text = read(hook)
        if "task ci" not in text:
            problems.append(".githooks/pre-push: no longer runs 'task ci'")
        if re.search(r"ALLOW_MAIN_PUSH|SKIP_CHECKS|PROCSENSE_SKIP", text):
            problems.append(".githooks/pre-push: contains a casual bypass variable (the only escape is the documented 'git push --no-verify')")
    ci = ROOT / ".github" / "workflows" / "ci.yml"
    if ci.exists() and not re.search(r"push:\s*\n\s*branches:\s*\[main\]", read(ci)):
        problems.append(".github/workflows/ci.yml: must run on pushes to main")
    return problems


CHECKS = {
    "core-rules": check_core_rules,
    "hygiene": check_hygiene,
    "docs-links": check_docs_links,
    "deferred-register": check_deferred_register,
    "prompt-history": check_prompt_history,
    "steps": check_steps,
    "team-guards": check_team_guards,
}


def main(argv: list[str]) -> int:
    names = argv or list(CHECKS)
    unknown = [n for n in names if n not in CHECKS]
    if unknown:
        print(f"unknown check(s): {unknown}; available: {list(CHECKS)}")
        return 2
    failed = False
    for name in names:
        problems = CHECKS[name]()
        if problems:
            failed = True
            print(f"FAIL  {name} ({len(problems)})")
            for p in problems[:50]:
                print(f"        {p}")
            if len(problems) > 50:
                print(f"        ... and {len(problems) - 50} more")
        else:
            print(f"PASS  {name}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
