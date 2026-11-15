"""The laptop checklist helpers of the networked Quest demonstration (P3-NET-13, P3-NET-16)."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "setup"))
import demo_laptop  # noqa: E402


class DemoLaptop(unittest.TestCase):
    def test_route_models_are_collected_once_each(self):
        names = demo_laptop.model_names()
        self.assertIn("gemma4:31b-cloud", names)
        self.assertEqual(len(names), len(set(names)))

    def test_missing_models_accepts_the_cloud_relay_under_its_plain_name(self):
        wanted = ["gemma4:31b-cloud", "qwen3.5:4b-q4_K_M"]
        self.assertEqual(demo_laptop.missing_models(wanted, ["gemma4:31b", "qwen3.5:4b-q4_K_M"]), [])
        self.assertEqual(demo_laptop.missing_models(wanted, ["gemma4:31b-cloud"]), ["qwen3.5:4b-q4_K_M"])
        self.assertEqual(demo_laptop.missing_models(wanted, []), wanted)

    def test_lan_addresses_are_private_only(self):
        for ip in demo_laptop.lan_addresses():
            self.assertTrue(ip.startswith(("10.", "192.168.", "172.")), ip)

    def test_the_address_qr_code_is_the_checked_matrix(self):
        """P3-NET-04. The matrix was decoded back to the address with an independent reader (OpenCV QRCodeDetector) when the encoder was written; the hash keeps it so."""
        import hashlib
        import qr_text
        m = qr_text.encode("http://192.168.1.20:8000")
        self.assertEqual(len(m), 25)                                                       # version 2
        self.assertEqual(hashlib.sha256("".join("1" if v else "0" for r in m for v in r).encode()).hexdigest(), "96f1e0ddbc07ec2f710655eb2b5d0d40cf6aa1834a5f785f658189cac91db1aa")
        self.assertEqual(qr_text.format_bits(0), 0b111011111000100)                       # level L, mask 0 (ISO/IEC 18004 table C.1)
        self.assertEqual([len(qr_text.encode(t)) for t in ("A", "https://192.168.100.200:8000", "x" * 106)], [21, 25, 37])
        with self.assertRaises(ValueError):
            qr_text.encode("x" * 107)

    def test_the_qr_code_decodes_when_a_reader_is_installed(self):
        try:
            import cv2
            import numpy as np
        except ImportError:
            self.skipTest("OpenCV is not installed here; the matrix hash above still holds the checked code")
        import qr_text
        for text in ("http://192.168.1.20:8000", "https://10.0.0.5:8443"):
            m = qr_text.encode(text)
            n = len(m)
            img = np.full((n + 8, n + 8), 255, np.uint8)
            for y in range(n):
                for x in range(n):
                    if m[y][x]:
                        img[y + 4, x + 4] = 0
            img = cv2.resize(img, ((n + 8) * 10, (n + 8) * 10), interpolation=cv2.INTER_NEAREST)
            self.assertEqual(cv2.QRCodeDetector().detectAndDecode(img)[0], text)

    def test_the_printed_qr_has_the_quiet_zone_and_an_ascii_form(self):
        art = demo_laptop.address_qr("http://192.168.1.20:8000")
        rows = art.split("\n")
        self.assertEqual(len(rows), 25 + 8)
        import qr_text
        ascii_rows = qr_text.render(qr_text.encode("http://192.168.1.20:8000"), unicode=False).split("\n")
        self.assertEqual(ascii_rows[0], "##" * 33)
        self.assertTrue(all(set(r) <= {"#", " "} for r in ascii_rows))


if __name__ == "__main__":
    unittest.main()
