"""Quest sky: converts the 8k Radiance HDR panorama into a small LDR PNG. The Quest camera has HDR off, so everything above 1.0 is clamped on screen anyway; clamping and sRGB-encoding here
gives the same picture from a texture that is a fraction of the size (2048 x 1024 ASTC instead of 4096 x 2048 RGB9E5).
  uv run --project tools python unity/Tools/Environment/hdr_to_ldr.py [exposure]"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

SRC = Path(__file__).resolve().parents[2] / "Assets/Textures/Environment/Sky/city_skyline_8k.hdr"
DST = Path(__file__).resolve().parents[2] / "Assets/Textures/Environment/Sky/city_skyline_ldr_2048.png"


def read_hdr(path):
    with open(path, "rb") as f:
        while True:
            line = f.readline()
            if line.startswith((b"-Y", b"+Y")):
                parts = line.split(); h, w = int(parts[1]), int(parts[3]); break
        data = f.read()
    img = np.zeros((h, w, 4), np.uint8); pos = 0
    for y in range(h):
        if data[pos] != 2 or data[pos + 1] != 2 or data[pos + 2] & 0x80:
            raise ValueError("only new-style RLE scanlines are supported")
        pos += 4
        for c in range(4):
            x = 0
            while x < w:
                n = data[pos]; pos += 1
                if n > 128:
                    n -= 128; img[y, x:x + n, c] = data[pos]; pos += 1
                else:
                    img[y, x:x + n, c] = np.frombuffer(data, np.uint8, n, pos); pos += n
                x += n
    e = img[..., 3].astype(np.float32)
    scale = np.where(e > 0, np.ldexp(1.0, (e - 136).astype(np.int32)), 0.0)[..., None]
    return img[..., :3].astype(np.float32) * scale


exposure = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
lin = np.clip(read_hdr(SRC) * exposure, 0, 1)
srgb = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)
im = Image.fromarray((srgb * 255 + 0.5).astype(np.uint8)).resize((2048, 1024), Image.LANCZOS)
im.save(DST, optimize=True)
print("wrote", DST, DST.stat().st_size, "bytes")
