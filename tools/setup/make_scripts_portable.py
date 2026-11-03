"""One-off migration (3 Nov 2026): makes the PowerShell and shell helpers under unity/Tools independent of one machine.

Replaces the hard-coded repository path, the scratch unity_run.ps1 location and the Unity.exe path by $Repo / $PSScriptRoot / $UnityExe, and prepends a two-line prelude.
Run from the repository root:  python -I tools/setup/make_scripts_portable.py
"""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPO_LIT = "C:\\Users\\RUHAN\\Downloads\\procsense-fyp-main\\procsense-fyp-main"
UNITY_LIT = "C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.10f1\\Editor\\Unity.exe"
SCRATCH_RE = re.compile(r"C:\\Users\\RUHAN\\AppData\\Local\\Temp\\claude\\[^'\"]*?\\scratchpad\\unity_run\.ps1")
PRELUDE = ("$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\\..\\..')).Path\r\n"
           "$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { '" + UNITY_LIT + "' }\r\n")


def fix(text: str) -> str:
    text = SCRATCH_RE.sub(lambda m: "$PSScriptRoot\\unity_run.ps1", text)
    # single-quoted literals: '<repo>' or '<repo>\x\y'  ->  (Join-Path $Repo 'x\y')
    text = re.sub(r"'" + re.escape(REPO_LIT) + r"\\([^']*)'", lambda m: f"(Join-Path $Repo '{m.group(1)}')", text)
    text = re.sub(r"'" + re.escape(REPO_LIT) + r"'", "$Repo", text)
    # double-quoted / bare occurrences  ->  $Repo
    text = text.replace(REPO_LIT, "$Repo")
    text = text.replace("'" + UNITY_LIT + "'", "$UnityExe").replace(UNITY_LIT, "$UnityExe")
    text = text.replace("'$PSScriptRoot\\unity_run.ps1'", "(Join-Path $PSScriptRoot 'unity_run.ps1')")
    return text


def main() -> None:
    changed = 0
    for p in sorted(list((ROOT / "unity" / "Tools").rglob("*.ps1")) + list((ROOT / "unity" / "Tools").rglob("*.sh"))):
        raw = p.read_bytes().decode("utf-8", errors="replace"); crlf = "\r\n" in raw
        text = raw.replace("\r\n", "\n")
        if "RUHAN" not in text and "6000.5.10f1" not in text:
            continue
        new = fix(text)
        if p.suffix == ".ps1" and "$Repo" in new and "$Repo = (Resolve-Path" not in new:
            lines = new.split("\n"); i = 0
            while i < len(lines) and (lines[i].startswith("#") or not lines[i].strip()):
                i += 1
            if i < len(lines) and lines[i].lower().startswith("param("):                 # keep param() as the first statement
                i += 1
            lines[i:i] = PRELUDE.replace("\r\n", "\n").rstrip("\n").split("\n")
            new = "\n".join(lines)
        if new != text:
            p.write_bytes((new.replace("\n", "\r\n") if crlf or p.suffix == ".ps1" else new).encode("utf-8")); changed += 1; print("fixed", p.relative_to(ROOT))
    print(changed, "files changed")


if __name__ == "__main__":
    main()
