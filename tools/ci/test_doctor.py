"""`task doctor` (tools/doctor.py): reads the wanted versions from the repository, reads versions out of tool output, and never crashes when a tool is missing."""
import sys
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import doctor  # noqa: E402


class Doctor(unittest.TestCase):
    def test_wanted_versions_come_from_the_repository(self):
        self.assertRegex(doctor.wanted_dotnet(), r"^\d+\.\d+\.\d+$")
        self.assertRegex(doctor.wanted_unity(), r"^\d+\.\d+\.\d+f\d+$")
        self.assertRegex(doctor.wanted_blender(), r"^\d+\.\d+(\.\d+)?$")

    def test_versions_are_read_from_tool_output(self):
        self.assertEqual(doctor.first_version("git version 2.51.0.windows.1"), "2.51.0")
        self.assertEqual(doctor.first_version("uv 0.13.0 (abc 2026-09-01)"), "0.13.0")
        self.assertEqual(doctor.first_version("Blender 4.5.14 LTS"), "4.5.14")
        self.assertEqual(doctor.first_version("nothing here"), "")
        self.assertEqual(doctor.major("9.0.318"), 9)
        self.assertEqual(doctor.major(""), -1)

    def test_a_machine_with_nothing_installed_reports_missing_and_fails(self):
        with mock.patch.object(doctor.shutil, "which", return_value=None), \
             mock.patch.object(doctor, "run", return_value=""), \
             mock.patch.object(doctor, "find_unity", return_value=None), \
             mock.patch.object(doctor, "find_blender", return_value=None):
            rows, ok = doctor.check()
        self.assertFalse(ok)
        status = {r[1]: r[0] for r in rows}
        for tool in ("Git", ".NET SDK", "uv", "Task", "Unity", "Blender", "adb", "pre-push hook"):
            self.assertEqual(status[tool], "MISSING", tool)
        self.assertTrue(all(r[5] for r in rows if r[0] != "OK"), "every missing tool says how to fix it")

    def test_an_old_dotnet_is_flagged(self):
        def fake_run(*cmd):
            return {"dotnet": "8.0.100", "git": "git version 2.45.0", "uv": "uv 0.5.1", "task": "Task version: v3.40.0"}.get(cmd[0], "")
        with mock.patch.object(doctor.shutil, "which", return_value="/usr/bin/x"), \
             mock.patch.object(doctor, "run", side_effect=fake_run), \
             mock.patch.object(doctor, "find_unity", return_value=None), \
             mock.patch.object(doctor, "find_blender", return_value=None):
            rows, ok = doctor.check()
        self.assertEqual({r[1]: r[0] for r in rows}[".NET SDK"], "OLD")
        self.assertFalse(ok)


if __name__ == "__main__":
    unittest.main(verbosity=1)
