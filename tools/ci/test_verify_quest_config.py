"""The Quest config verifier (tools/unity/verify_quest_config.py): the real project passes with no FAIL, and a regressed setting
(MSAA 4, XR performance settings off, Quest 1 re-enabled) turns its item into FAIL."""
import importlib.util
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def load():
    spec = importlib.util.spec_from_file_location("verify_quest_config", ROOT / "tools" / "unity" / "verify_quest_config.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class VerifyQuestConfigTests(unittest.TestCase):
    def run_with(self, edits: dict[str, tuple[str, str]]):
        mod = load()
        real = mod.read

        def fake(rel: str) -> str:
            text = real(rel)
            if rel in edits:
                old, new = edits[rel]
                self.assertIn(old, text, f"{rel}: test edit target not found")
                text = text.replace(old, new, 1)
            return text

        mod.read = fake
        mod.HEAD = False
        mod.results.clear()
        code = mod.main()
        return code, {item: status for status, item, _ in mod.results}

    def test_the_project_has_no_fail(self):
        code, items = self.run_with({})
        self.assertEqual(0, code, [i for i, s in items.items() if s == "FAIL"])
        self.assertGreaterEqual(len(items), 25)

    def test_msaa_4_fails(self):
        code, items = self.run_with({"unity/Assets/Settings/URP_Quest_Android.asset": ("m_MSAA: 1", "m_MSAA: 4")})
        self.assertEqual(1, code)
        self.assertEqual("FAIL", items["MSAA 1 (Android URP; Medium quality AA 0)"])

    def test_performance_settings_off_fails(self):
        p = "unity/Assets/XR/Settings/OpenXR Package Settings.asset"
        mod = load()
        text = mod.read(p)
        i = text.index("m_Name: XrPerformanceSettingsFeature Android")
        j = text.index("m_enabled: 1", i)
        segment = text[i:j + len("m_enabled: 1")]
        code, items = self.run_with({p: (segment, segment.replace("m_enabled: 1", "m_enabled: 0"))})
        self.assertEqual(1, code)
        self.assertEqual("FAIL", items["XR performance settings (SustainedHigh hint)"])

    def test_ffr_high_fails(self):
        p = "unity/Assets/Scripts/Views/QuestPerformanceProfile.cs"
        code, items = self.run_with({p: ("const float Foveation = 0.5f;", "const float Foveation = 1f;")})
        self.assertEqual(1, code)
        self.assertEqual("FAIL", items["FFR on (Medium), subsampled layout"])

    def test_first_overhaul_scale_fails(self):
        p = "unity/Assets/Scripts/Views/QuestPerformanceProfile.cs"
        code, items = self.run_with({p: ("const float StartScale = 0.9f;", "const float StartScale = 0.8f;")})
        self.assertEqual("FAIL", items["eye buffer 1.0 / start 0.9 / dynamic 0.85-1.0, driver installs itself"])

    def test_frame_timing_for_diagnostic_only_warns(self):
        p = "unity/Assets/Editor/Setup/BuildScript.cs"
        code, items = self.run_with({p: ("                PlayerSettings.enableFrameTimingStats = true;", "                if (v.Diagnostic) PlayerSettings.enableFrameTimingStats = true;")})
        self.assertEqual("WARN", items["dynamic resolution GPU time in release and diagnostic APKs"])

    def test_hero_texture_6x6_fails(self):
        p = "unity/Assets/Textures/Hospital/FinalLook/gown_albedo.png.meta"
        code, items = self.run_with({p: ("textureFormat: 48", "textureFormat: 50")})
        self.assertEqual("FAIL", items["close-up textures ASTC 4x4/5x5 (patient, gown, bed, monitor)"])

    def test_quest_1_enabled_fails(self):
        p = "unity/Assets/XR/Settings/OpenXR Package Settings.asset"
        code, items = self.run_with({p: ("manifestName: quest\n    enabled: 0", "manifestName: quest\n    enabled: 1")})
        self.assertEqual("FAIL", items["Meta Quest feature on; Quest 2 on, Quest 1 off"])


if __name__ == "__main__":
    unittest.main()
