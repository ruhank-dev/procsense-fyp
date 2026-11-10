"""Tests .githooks/pre-push in a throw-away repository, never in the real one. Standard library only.

Usage: uv run --project tools tools/ci/test_pre_push_hook.py      (task ci runs it too)
A stub 'task' stands in for the real `task ci`, so each case controls whether "the checks" pass or fail.
"""
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HOOK = ROOT / ".githooks" / "pre-push"
sys.stdout.reconfigure(encoding="utf-8")

STUB = '#!/bin/sh\necho "task $*" >> "$TASK_STUB_LOG"\nexit "${TASK_STUB_EXIT:-0}"\n'
OK_STUB = "#!/bin/sh\nexit 0\n"
# Fake 'gh': counts the runs in GH_STUB_RUNS ("sha:event:conclusion ...") that match the --commit and --event it was asked for and that succeeded, like
# `gh run list --commit --event --status success --json conclusion --jq length`. GH_STUB_MODE=error makes it fail like an API error. No network.
GH_STUB = """#!/bin/sh
echo "gh $*" >> "$GH_STUB_LOG"
[ "${GH_STUB_MODE:-}" = "error" ] && { echo "gh: HTTP 500" >&2; exit 1; }
event=""; commit=""; prev=""
for a in "$@"; do case "$prev" in --event) event=$a ;; --commit) commit=$a ;; esac; prev=$a; done
n=0
for r in $GH_STUB_RUNS; do
  rsha=${r%%:*}; rest=${r#*:}; rev=${rest%%:*}; rcon=${rest#*:}
  if [ "$rsha" = "$commit" ] && [ "$rev" = "$event" ] && [ "$rcon" = success ]; then n=$((n + 1)); fi
done
echo "$n"
"""



def system_dirs() -> list[str]:
    if os.name == "nt":  # Git for Windows needs its cmd, mingw64 and usr folders to run hooks
        exec_path = subprocess.run(["git", "--exec-path"], capture_output=True, text=True).stdout.strip()
        root = Path(exec_path).parents[2]
        return [str(root / "cmd"), str(root / "mingw64" / "bin"), str(root / "usr" / "bin")]
    return ["/usr/bin", "/bin"]


class Lab:
    def __init__(self, base: Path):
        self.base = base
        self.bin = base / "bin"
        self.bin.mkdir()
        self.log = base / "task.log"
        for name, body in (("task", STUB), ("dotnet", OK_STUB), ("uv", OK_STUB)):
            self.stub(name, body)
        # A minimal PATH (stubs first, then only the system/Git folders), so a developer's real task/dotnet/uv never leak into the test.
        self.env = {**os.environ, "PATH": os.pathsep.join([str(self.bin)] + system_dirs()), "TASK_STUB_LOG": str(self.log),
                    "GIT_CONFIG_GLOBAL": str(base / "gitconfig"), "GIT_TERMINAL_PROMPT": "0", "GH_STUB_LOG": str(base / "gh.log")}
        self.remote = base / "remote.git"
        self.git("init", "--bare", "-b", "main", str(self.remote), cwd=base)
        self.work = self.clone("work")
        (self.work / ".githooks").mkdir()
        shutil.copy(HOOK, self.work / ".githooks" / "pre-push")
        self.git("config", "core.hooksPath", ".githooks", cwd=self.work)
        self.git("add", ".githooks", cwd=self.work)  # the real project tracks its hook, so the clean-tree check must see it as committed
        self.commit(self.work, "README.md", "first", "initial")
        self.git("-c", "core.hooksPath=/dev/null", "push", "-u", "origin", "main", cwd=self.work)

    def stub(self, name: str, body: str) -> None:
        p = self.bin / name
        p.write_text(body, encoding="utf-8", newline="\n")
        p.chmod(0o755)

    def git(self, *args, cwd=None, env=None, check=True):
        p = subprocess.run(["git", *args], cwd=cwd or self.work, env=env or self.env, capture_output=True, text=True, encoding="utf-8", errors="replace")
        if check and p.returncode != 0:
            raise RuntimeError(f"git {' '.join(args)} failed: {p.stderr}")
        return p

    def clone(self, name: str) -> Path:
        path = self.base / name
        self.git("clone", str(self.remote), str(path), cwd=self.base)
        self.git("config", "user.email", "t@example.com", cwd=path)
        self.git("config", "user.name", "Tester", cwd=path)
        return path

    def commit(self, repo: Path, file: str, text: str, message: str) -> None:
        (repo / file).write_text(text, encoding="utf-8", newline="\n")
        self.git("add", file, cwd=repo)
        self.git("commit", "-m", message, cwd=repo)

    def push(self, *args, stub_exit=0, extra_env=None, cwd=None):
        self.log.write_text("", encoding="utf-8")
        env = {**self.env, "TASK_STUB_EXIT": str(stub_exit), **(extra_env or {})}
        p = self.git("push", *args, cwd=cwd, env=env, check=False)
        calls = len([l for l in self.log.read_text(encoding="utf-8").splitlines() if l.strip()])
        return p.returncode, p.stdout + p.stderr, calls

    def remote_main(self) -> str:
        return self.git("rev-parse", "main", cwd=self.remote).stdout.strip()

    def head(self, repo=None) -> str:
        return self.git("rev-parse", "HEAD", cwd=repo or self.work).stdout.strip()


def run_cases(lab: Lab) -> list[tuple[str, bool, str]]:
    results = []

    def case(name, ok, detail=""):
        results.append((name, bool(ok), detail))

    # 1. feature branch: no checks, push works
    lab.git("switch", "-c", "feature/a")
    lab.commit(lab.work, "a.txt", "a", "feature a")
    rc, out, calls = lab.push("-u", "origin", "feature/a")
    case("feature-branch push passes straight through (no checks run)", rc == 0 and calls == 0, out)

    # 2. main with passing checks (fast-forward)
    lab.git("switch", "main")
    lab.git("merge", "--ff-only", "feature/a")
    rc, out, calls = lab.push("origin", "main")
    case("push to main with passing checks is allowed and runs task ci once", rc == 0 and calls == 1 and lab.remote_main() == lab.head(), out)

    # 3. failing checks block
    lab.commit(lab.work, "b.txt", "b", "b")
    before = lab.remote_main()
    rc, out, calls = lab.push("origin", "main", stub_exit=1)
    case("push to main with failing checks is blocked and main is unchanged", rc != 0 and "task ci' failed" in out and lab.remote_main() == before, out)

    # 4. no env-var bypass
    rc, out, calls = lab.push("origin", "main", stub_exit=1, extra_env={"PROCSENSE_ALLOW_MAIN_PUSH": "1"})
    case("the old PROCSENSE_ALLOW_MAIN_PUSH bypass no longer works", rc != 0 and lab.remote_main() == before, out)

    # 5. dirty tracked file
    (lab.work / "b.txt").write_text("changed", encoding="utf-8", newline="\n")
    rc, out, calls = lab.push("origin", "main")
    case("modified files block the push to main (no checks run)", rc != 0 and calls == 0 and "uncommitted" in out, out)
    lab.git("checkout", "--", "b.txt")

    # 6. untracked file
    (lab.work / "new.txt").write_text("x", encoding="utf-8", newline="\n")
    rc, out, calls = lab.push("origin", "main")
    case("untracked files block the push to main", rc != 0 and calls == 0 and "untracked" in out, out)
    (lab.work / "new.txt").unlink()

    # 7. pushing a commit that is not checked out
    lab.git("switch", "feature/a")
    lab.commit(lab.work, "c.txt", "c", "c on feature")
    lab.git("switch", "main")
    rc, out, calls = lab.push("origin", "feature/a:main")
    case("pushing feature:main while another branch is checked out is blocked", rc != 0 and calls == 0 and "not the one checked out" in out, out)

    # 8. HEAD:main from a feature branch that contains origin/main
    lab.git("switch", "feature/a")
    lab.git("merge", "main")
    rc, out, calls = lab.push("origin", "HEAD:main")
    case("HEAD:main from a feature branch runs the checks and is allowed when they pass", rc == 0 and calls == 1 and lab.remote_main() == lab.head(), out)

    # 9. stale local main: remote moved
    other = lab.clone("other")
    lab.commit(other, "o.txt", "o", "from the other clone")
    lab.git("-c", "core.hooksPath=/dev/null", "push", "origin", "main", cwd=other)
    lab.git("switch", "main")
    lab.commit(lab.work, "d.txt", "d", "d")
    rc, out, calls = lab.push("origin", "main")
    case("pushing when main moved on GitHub is blocked with a fetch/merge instruction", rc != 0 and calls == 0 and "git fetch origin" in out, out)

    # 10. reconcile and push
    lab.git("fetch", "origin")
    lab.git("merge", "origin/main", "-m", "merge origin/main")
    rc, out, calls = lab.push("origin", "main")
    case("after fetching and merging, the push to main passes", rc == 0 and calls == 1 and lab.remote_main() == lab.head(), out)

    # 11. force-push
    lab.git("reset", "--hard", "HEAD~1")
    lab.commit(lab.work, "e.txt", "e", "diverging commit")
    before = lab.remote_main()
    rc, out, calls = lab.push("--force", "origin", "main")
    case("force-push to main is blocked", rc != 0 and calls == 0 and lab.remote_main() == before and "not a fast-forward" in out, out)
    lab.git("reset", "--hard", "origin/main")

    # 12. delete main
    rc, out, calls = lab.push("origin", ":main")
    case("deleting main is blocked", rc != 0 and "deleting" in out and lab.remote_main() == before, out)

    # 13. several refs in one push: checks run once
    lab.git("switch", "-c", "feature/b")
    lab.commit(lab.work, "f.txt", "f", "f")
    lab.git("switch", "main")
    lab.git("merge", "--ff-only", "feature/b")
    rc, out, calls = lab.push("origin", "main", "feature/b")
    case("pushing main and a feature branch together runs the checks once", rc == 0 and calls == 1, out)

    # 14. tags pass
    lab.git("tag", "v0.0.0-test")
    rc, out, calls = lab.push("origin", "v0.0.0-test")
    case("tag pushes pass straight through", rc == 0 and calls == 0, out)

    # 14b. the exact sequence from docs/TEAM_GIT_WORKFLOW.md section D, with two teammates racing
    lab.git("switch", "main")
    lab.git("fetch", "origin", "--prune")
    lab.git("merge", "--ff-only", "origin/main")
    a, b = lab.work, lab.clone("bob")
    lab.git("config", "core.hooksPath", ".githooks", cwd=b)
    lab.git("switch", "-c", "sannan47/x", "--no-track", "origin/main", cwd=a)
    lab.commit(a, "sx.txt", "x", "sannan work")
    lab.git("switch", "-c", "myt1x54/y", "--no-track", "origin/main", cwd=b)
    lab.commit(b, "my.txt", "y", "myt work")
    ok = True
    for repo, branch in ((a, "sannan47/x"), (b, "myt1x54/y")):  # both publish their branch first (steps 2-6)
        lab.git("fetch", "origin", "--prune", cwd=repo)
        lab.git("merge", "origin/main", cwd=repo)
        rc, out, calls = lab.push("-u", "origin", "HEAD", cwd=repo)
        ok = ok and rc == 0 and calls == 0
    lab.git("switch", "main", cwd=a)  # Sannan publishes first (steps 7-9)
    lab.git("merge", "--ff-only", "origin/main", cwd=a)
    lab.git("merge", "--ff-only", "sannan47/x", cwd=a)
    rc, out, calls = lab.push("origin", "main", cwd=a)
    ok = ok and rc == 0 and calls == 1
    lab.git("switch", "main", cwd=b)  # Myt1x54's computer does not know main moved: his local fast-forward works...
    lab.git("merge", "--ff-only", "myt1x54/y", cwd=b)
    before = lab.remote_main()
    rc, out, calls = lab.push("origin", "main", cwd=b)  # ...but the push is refused (by git, or by the hook), as the guide says
    ok = ok and rc != 0 and calls == 0 and lab.remote_main() == before and ("non-fast-forward" in out or "fetch first" in out or "git fetch origin" in out)
    lab.git("switch", "myt1x54/y", cwd=b)  # recovery from the guide: back to the branch, fetch, merge, check, fast-forward main
    lab.git("fetch", "origin", "--prune", cwd=b)
    lab.git("merge", "origin/main", cwd=b)
    lab.git("switch", "main", cwd=b)
    lab.git("merge", "--ff-only", "myt1x54/y", cwd=b)
    rc, out, calls = lab.push("origin", "main", cwd=b)
    ok = ok and rc == 0 and calls == 1 and lab.remote_main() == lab.head(b)
    lab.git("switch", "main")  # bring the first clone back in line with GitHub for the remaining cases
    lab.git("fetch", "origin", "--prune")
    lab.git("merge", "--ff-only", "origin/main")
    case("the guide's sequence works for two teammates racing: the stale push is blocked, the documented recovery then succeeds", ok, out)

    # 15. missing tool
    lab.commit(lab.work, "g.txt", "g", "g")
    (lab.bin / "task").unlink()
    before = lab.remote_main()
    rc, out, calls = lab.push("origin", "main")
    case("a missing 'task' blocks the push with an install hint", rc != 0 and "'task' is not installed" in out and lab.remote_main() == before, out)
    lab.stub("task", STUB)

    # 16. --no-verify is the one documented escape, and it really bypasses (the hook is not tamper-proof)
    rc, out, calls = lab.push("--no-verify", "origin", "main", stub_exit=1)
    case("'git push --no-verify' bypasses the hook (documented limitation: local hooks are not enforcement)", rc == 0 and calls == 0, out)

    # 17-25: the remote-green shortcut (a successful full 'workflow_dispatch' run of ci.yml on the exact commit). Fake gh, no network, no real push to GitHub.
    lab.git("remote", "set-url", "origin", "https://github.com/test/repo.git")   # what the hook reads to find the repository ...
    lab.git("remote", "set-url", "--push", "origin", str(lab.remote))             # ... while the push itself still goes to the local bare repo

    def next_main_commit(name):
        lab.commit(lab.work, name + ".txt", name, name)
        return lab.head()

    def gh_push(sha, runs, mode="", install_gh=True, stub_exit=0, args=("origin", "main")):
        if install_gh:
            lab.stub("gh", GH_STUB)
        elif (lab.bin / "gh").exists():
            (lab.bin / "gh").unlink()
        (lab.base / "gh.log").write_text("", encoding="utf-8")
        rc, out, calls = lab.push(*args, stub_exit=stub_exit, extra_env={"GH_STUB_RUNS": runs.replace("HEAD", sha), "GH_STUB_MODE": mode})
        task_log = lab.log.read_text(encoding="utf-8").strip().splitlines()
        gh_log = (lab.base / "gh.log").read_text(encoding="utf-8")
        return rc, out, task_log, gh_log

    full_ci = ["task ci"]

    # 17. green full run on this exact commit: fast local checks instead of task ci
    sha = next_main_commit("g1"); before = lab.remote_main()
    rc, out, tl, gl = gh_push(sha, "HEAD:workflow_dispatch:success")
    fast = ["task check"]
    case("a successful full workflow_dispatch run for this exact commit: only 'task check' runs, 'task ci' does not, and the push is allowed",
         rc == 0 and tl == fast and lab.remote_main() == sha and "--repo test/repo" in gl and "--workflow ci.yml" in gl and f"--commit {sha}" in gl
         and "--event workflow_dispatch" in gl and "--status success" in gl, f"{out}\n{tl}\n{gl}")

    # 18. no run at all
    sha = next_main_commit("g2")
    rc, out, tl, gl = gh_push(sha, "")
    case("no GitHub run for the commit falls back to the full 'task ci'", rc == 0 and tl == full_ci, f"{out}\n{tl}")

    # 19. only a push-triggered (change-aware, possibly partial) run
    sha = next_main_commit("g3")
    rc, out, tl, gl = gh_push(sha, "HEAD:push:success")
    case("a green push-triggered run (it may have skipped jobs) does not count: full 'task ci' runs", rc == 0 and tl == full_ci, f"{out}\n{tl}")

    # 20. a successful run, but for another commit
    sha = next_main_commit("g4")
    rc, out, tl, gl = gh_push(sha, "0123456789abcdef0123456789abcdef01234567:workflow_dispatch:success")
    case("a green full run of a different commit does not count: full 'task ci' runs", rc == 0 and tl == full_ci, f"{out}\n{tl}")

    # 21. gh missing
    sha = next_main_commit("g5")
    rc, out, tl, gl = gh_push(sha, "HEAD:workflow_dispatch:success", install_gh=False)
    case("gh not installed falls back to the full 'task ci'", rc == 0 and tl == full_ci, f"{out}\n{tl}")

    # 22. API error / not logged in
    sha = next_main_commit("g6")
    rc, out, tl, gl = gh_push(sha, "HEAD:workflow_dispatch:success", mode="error")
    case("a gh/API error falls back to the full 'task ci'", rc == 0 and tl == full_ci, f"{out}\n{tl}")

    # 23. origin is not a GitHub repository
    sha = next_main_commit("g7")
    lab.git("remote", "set-url", "origin", str(lab.remote))
    rc, out, tl, gl = gh_push(sha, "HEAD:workflow_dispatch:success")
    case("an origin that is not on GitHub falls back to the full 'task ci' (gh is not even asked)", rc == 0 and tl == full_ci and gl == "", f"{out}\n{tl}\n{gl}")
    lab.git("remote", "set-url", "origin", "https://github.com/test/repo.git")

    # 24. green run, but the fast local checks fail
    sha = next_main_commit("g8"); before = lab.remote_main()
    rc, out, tl, gl = gh_push(sha, "HEAD:workflow_dispatch:success", stub_exit=1)
    case("with a green remote run, a failing fast local check still blocks the push and main is unchanged", rc != 0 and "was not updated" in out and lab.remote_main() == before and "task ci" not in tl, f"{out}\n{tl}")

    # 25. a brand-new main on the remote (zero remote sha): never trust the shortcut
    fresh = lab.base / "fresh.git"
    lab.git("init", "--bare", "-b", "main", str(fresh), cwd=lab.base)
    lab.git("remote", "set-url", "--push", "origin", str(fresh))
    rc, out, tl, gl = gh_push(sha, "HEAD:workflow_dispatch:success")
    case("creating main on a remote that has none falls back to the full 'task ci' even with a green run", rc == 0 and tl == full_ci and gl == "", f"{out}\n{tl}\n{gl}")
    lab.git("remote", "set-url", "--push", "origin", str(lab.remote))

    # 26. no bypass variable in the hook, and a failing 'task ci' still blocks on the fallback path
    sha = next_main_commit("g9"); before = lab.remote_main()
    rc, out, tl, gl = gh_push(sha, "", stub_exit=1)
    case("on the fallback path a failing 'task ci' still blocks and main is unchanged", rc != 0 and "task ci' failed" in out and lab.remote_main() == before and tl == full_ci, f"{out}\n{tl}")
    return results


def main() -> int:
    if not HOOK.exists():
        print("missing .githooks/pre-push")
        return 1
    with tempfile.TemporaryDirectory() as tmp:
        results = run_cases(Lab(Path(tmp)))
        failed = [r for r in results if not r[1]]
        for name, ok, detail in results:
            print(f"{'PASS' if ok else 'FAIL'}  {name}")
            if not ok:
                print("        " + detail.strip().replace("\n", "\n        ")[:600])
        print(f"\n{len(results) - len(failed)}/{len(results)} hook cases passed")
        return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
