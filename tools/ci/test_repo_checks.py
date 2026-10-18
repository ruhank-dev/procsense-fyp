"""Tests for the secret patterns of tools/ci/repo_checks.py (p1-backend-s07): a pasted key must be caught, a placeholder must not.

    uv run --project tools tools/ci/test_repo_checks.py
CI runs it as "secret scan catches pasted keys". The example strings are built from pieces so this file does not contain a real-looking key itself.
"""
import hashlib
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import repo_checks  # noqa: E402


def caught(text: str) -> list[str]:
    return [why for pattern, why in repo_checks.SECRET_PATTERNS if re.search(pattern, text)]


class SecretPatterns(unittest.TestCase):
    def test_a_supabase_secret_key_is_caught(self):
        self.assertIn("Supabase secret key", caught("SUPABASE_SECRET_KEY=" + "sb_" + "secret_" + "A1b2C3d4E5f6G7h8I9j0"))

    def test_the_publishable_key_is_not_a_secret(self):
        self.assertEqual(caught("SUPABASE_PUBLISHABLE_KEY=" + "sb_" + "publishable_" + "A1b2C3d4E5f6G7h8I9j0"), [])

    def test_a_database_address_with_a_real_password_is_caught(self):
        url = "postgresql+psycopg://" + "postgres.abcdefghijklmnop" + ":" + "Tr0ub4dor3xyz" + "@aws-0-ap-southeast-1.pooler.supabase.com:6543/postgres"
        self.assertIn("database address with a password in it", caught(url))

    def test_a_placeholder_password_is_fine(self):
        for pw in ("[YOUR-PASSWORD]", "YOUR-PASSWORD", "<password>", "${DB_PASSWORD}", "REPLACE_ME"):
            url = "postgresql://postgres.PROJECT-REF:" + pw + "@HOST:6543/postgres"
            self.assertEqual(caught(url), [], pw)

    def test_a_throwaway_database_on_this_computer_is_fine(self):
        for host in ("localhost:54329", "127.0.0.1:5432"):
            self.assertEqual(caught("postgresql+psycopg://postgres:" + "postgres" + "@" + host + "/procsense_test"), [], host)

    def test_a_signed_token_is_caught(self):
        token = "ey" + "JhbGciOiJIUzI1NiJ9" + "." + "ey" + "Jyb2xlIjoic2VydmljZV9yb2xlIn0" + "." + "abcdefghij1234567890"
        self.assertIn("signed token (JWT)", caught(token))

    def test_the_server_notes_and_env_example_are_scanned(self):
        # the scan includes server/*.md and server/.env.example, which the older suffix list skipped
        src = (Path(__file__).resolve().parent / "repo_checks.py").read_text(encoding="utf-8")
        self.assertIn('rel.startswith("server/")', src)
        self.assertIn('".example"', src)

    def test_the_real_repository_is_clean(self):
        self.assertEqual(repo_checks.check_hygiene(), [])


class LfsSizeRule(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.git("init", "--quiet")
        self.file = self.root / "large mesh.asset"
        self.file.write_bytes(b"a" * 5_000_001)
        self.root_patch = patch.object(repo_checks, "ROOT", self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)

    def git(self, *args, data=None):
        return subprocess.run(["git", *args], cwd=self.root, input=data, capture_output=True, check=True).stdout

    def stage(self, *, attribute=True, pointer=True):
        if attribute:
            (self.root / ".gitattributes").write_text("*.asset filter=lfs diff=lfs merge=lfs -text\n", encoding="utf-8")
        content = self.file.read_bytes()
        if pointer:
            content = ("version https://git-lfs.github.com/spec/v1\noid sha256:" + hashlib.sha256(content).hexdigest()
                       + "\nsize " + str(len(content)) + "\n").encode("ascii")
        oid = self.git("hash-object", "-w", "--stdin", data=content).decode().strip()
        self.git("update-index", "--add", "--cacheinfo", "100644", oid, self.file.name)

    def test_large_lfs_file_with_spaces_passes(self):
        self.stage()
        self.assertEqual(repo_checks.check_hygiene(), [])

    def test_attribute_without_an_indexed_pointer_still_fails(self):
        self.stage(pointer=False)
        self.assertTrue(any("larger than 5 MB" in message for message in repo_checks.check_hygiene()))

    def test_pointer_without_lfs_configuration_still_fails(self):
        self.stage(attribute=False)
        self.assertFalse(repo_checks.stored_in_lfs(self.file))

    def test_changed_local_bytes_do_not_match_an_old_pointer(self):
        self.stage()
        self.file.write_bytes(b"b" * 5_000_001)
        self.assertFalse(repo_checks.stored_in_lfs(self.file))

    def test_lfs_does_not_exempt_secret_scanning(self):
        self.file = self.root / "large.cs"
        self.file.write_bytes(b"a" * 5_000_001 + b"\n" + ("sb_" + "secret_" + "A1b2C3d4E5f6G7h8I9j0").encode())
        self.stage()
        (self.root / ".gitattributes").write_text("*.cs filter=lfs diff=lfs merge=lfs -text\n", encoding="utf-8")
        self.assertTrue(repo_checks.stored_in_lfs(self.file))
        self.assertTrue(any("Supabase secret key" in message for message in repo_checks.check_hygiene()))


if __name__ == "__main__":
    unittest.main(verbosity=1)
