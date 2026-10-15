"""`task repo-backup` (tools/repo_backup.py): the bundle holds every branch, a clone of it gives the same commits back, old bundles are pruned, and a folder inside the repository is refused."""
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import repo_backup  # noqa: E402


def git(*args, cwd):
    return subprocess.run(["git", "-c", "user.name=t", "-c", "user.email=t@example.test", *args], cwd=cwd, check=True, capture_output=True, text=True).stdout.strip()


class RepoBackup(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.base = Path(self.tmp.name)
        self.repo = self.base / "repo"
        self.repo.mkdir()
        git("init", "-q", "-b", "main", cwd=self.repo)
        (self.repo / "a.txt").write_text("one")
        git("add", "a.txt", cwd=self.repo)
        git("commit", "-q", "-m", "first", cwd=self.repo)
        git("switch", "-q", "-c", "feature", cwd=self.repo)
        (self.repo / "b.txt").write_text("two")
        git("add", "b.txt", cwd=self.repo)
        git("commit", "-q", "-m", "second", cwd=self.repo)
        git("tag", "v1", cwd=self.repo)

    def tearDown(self):
        self.tmp.cleanup()

    def test_the_bundle_restores_every_branch_and_tag(self):
        out = self.base / "backups"
        target, refs = repo_backup.backup(out, keep=5, fetch=False, repo=self.repo)
        self.assertTrue(target.exists())
        self.assertGreaterEqual(refs, 3)            # main, feature, v1
        clone = self.base / "clone"
        git("clone", "-q", str(target), str(clone), cwd=self.base)
        self.assertEqual(git("rev-parse", "origin/feature", cwd=clone), git("rev-parse", "feature", cwd=self.repo))
        self.assertEqual(git("rev-parse", "v1", cwd=clone), git("rev-parse", "v1", cwd=self.repo))

    def test_only_the_newest_bundles_are_kept(self):
        out = self.base / "backups"
        start = datetime(2026, 10, 15, tzinfo=timezone.utc)
        for i in range(4):
            repo_backup.backup(out, keep=2, fetch=False, repo=self.repo, now=start + timedelta(minutes=i))
        names = sorted(p.name for p in out.glob("*.bundle"))
        self.assertEqual(names, ["procsense-repo-20261015-000200.bundle", "procsense-repo-20261015-000300.bundle"])

    def test_a_folder_inside_the_repository_is_refused(self):
        with self.assertRaisesRegex(RuntimeError, "outside the repository"):
            repo_backup.backup(self.repo / "backups", keep=1, fetch=False, repo=self.repo)


if __name__ == "__main__":
    unittest.main(verbosity=1)
