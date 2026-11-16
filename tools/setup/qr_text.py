"""A small QR code maker for the demonstration laptop's address (P3-NET-04), so the address can be read with a phone or the headset's passthrough camera instead of typed.

Byte mode, error correction level L, versions 1 to 5 (up to 106 bytes: an address such as https://192.168.100.200:8000 needs version 2). No dependency: it follows ISO/IEC 18004 (finder, timing and alignment
patterns, Reed-Solomon over GF(256) with 0x11D, the BCH-coded format bits, the eight masks with the usual penalty rules 1, 2 and 4). `render()` prints it for a dark terminal: light modules are drawn, so the
dark ones show the background, with the four-module quiet zone the standard asks for."""
from __future__ import annotations

# (data codewords, error-correction codewords) of the single block at level L; alignment pattern centre for versions 2 to 5
CAPACITY = {1: (19, 7), 2: (34, 10), 3: (55, 15), 4: (80, 20), 5: (108, 26)}
ALIGN = {2: 18, 3: 22, 4: 26, 5: 30}
FORMAT_L = 1          # level L in the format bits


def _gf_mul(x: int, y: int) -> int:
    z = 0
    for i in reversed(range(8)):
        z = (z << 1) ^ ((z >> 7) * 0x11D)
        z ^= ((y >> i) & 1) * x
    return z


def _rs_divisor(degree: int) -> list[int]:
    result = [0] * (degree - 1) + [1]
    root = 1
    for _ in range(degree):
        for j in range(len(result)):
            result[j] = _gf_mul(result[j], root)
            if j + 1 < len(result):
                result[j] ^= result[j + 1]
        root = _gf_mul(root, 0x02)
    return result


def rs_remainder(data: list[int], degree: int) -> list[int]:
    divisor = _rs_divisor(degree)
    result = [0] * degree
    for b in data:
        factor = b ^ result.pop(0)
        result.append(0)
        for i, coef in enumerate(divisor):
            result[i] ^= _gf_mul(coef, factor)
    return result


def codewords(text: str) -> tuple[int, list[int]]:
    """The smallest version that holds the text, and its data plus error-correction codewords."""
    raw = text.encode("utf-8")
    for version, (data_len, ec_len) in CAPACITY.items():
        if 4 + 8 + 8 * len(raw) <= data_len * 8:
            break
    else:
        raise ValueError(f"too long for a version 5 code: {len(raw)} bytes")
    bits = [0, 1, 0, 0] + [(len(raw) >> i) & 1 for i in reversed(range(8))]
    for b in raw:
        bits += [(b >> i) & 1 for i in reversed(range(8))]
    bits += [0] * min(4, data_len * 8 - len(bits))
    bits += [0] * (-len(bits) % 8)
    data = [int("".join(map(str, bits[i:i + 8])), 2) for i in range(0, len(bits), 8)]
    pad = 0xEC
    while len(data) < data_len:
        data.append(pad)
        pad ^= 0xEC ^ 0x11
    return version, data + rs_remainder(data, ec_len)


def format_bits(mask: int) -> int:
    data = FORMAT_L << 3 | mask
    rem = data
    for _ in range(10):
        rem = (rem << 1) ^ ((rem >> 9) * 0x537)
    return (data << 10 | rem) ^ 0x5412


class _Grid:
    def __init__(self, version: int):
        self.size = 17 + 4 * version
        self.dark = [[False] * self.size for _ in range(self.size)]
        self.fixed = [[False] * self.size for _ in range(self.size)]

    def put(self, x: int, y: int, dark: bool) -> None:
        self.dark[y][x], self.fixed[y][x] = dark, True

    def functions(self, version: int) -> None:
        n = self.size
        for i in range(n):
            self.put(6, i, i % 2 == 0)
            self.put(i, 6, i % 2 == 0)
        for cx, cy in ((3, 3), (n - 4, 3), (3, n - 4)):
            for dy in range(-4, 5):
                for dx in range(-4, 5):
                    if 0 <= cx + dx < n and 0 <= cy + dy < n:
                        self.put(cx + dx, cy + dy, max(abs(dx), abs(dy)) not in (2, 4))
        if version in ALIGN:
            c = ALIGN[version]
            for dy in range(-2, 3):
                for dx in range(-2, 3):
                    self.put(c + dx, c + dy, max(abs(dx), abs(dy)) != 1)
        self.format(0)

    def format(self, mask: int) -> None:
        bits, n = format_bits(mask), self.size
        bit = lambda i: (bits >> i) & 1 == 1  # noqa: E731
        for i in range(6):
            self.put(8, i, bit(i))
        self.put(8, 7, bit(6))
        self.put(8, 8, bit(7))
        self.put(7, 8, bit(8))
        for i in range(9, 15):
            self.put(14 - i, 8, bit(i))
        for i in range(8):
            self.put(n - 1 - i, 8, bit(i))
        for i in range(8, 15):
            self.put(8, n - 15 + i, bit(i))
        self.put(8, n - 8, True)                                  # the dark module

    def place(self, words: list[int]) -> None:
        n, i, right = self.size, 0, self.size - 1
        while right >= 1:
            if right == 6:
                right = 5
            for vert in range(n):
                for j in range(2):
                    x = right - j
                    y = n - 1 - vert if ((right + 1) & 2) == 0 else vert
                    if not self.fixed[y][x] and i < len(words) * 8:
                        self.dark[y][x] = (words[i >> 3] >> (7 - (i & 7))) & 1 == 1
                        i += 1
            right -= 2

    def masked(self, mask: int) -> list[list[bool]]:
        test = [lambda x, y: (x + y) % 2 == 0, lambda x, y: y % 2 == 0, lambda x, y: x % 3 == 0, lambda x, y: (x + y) % 3 == 0,
                lambda x, y: (x // 3 + y // 2) % 2 == 0, lambda x, y: x * y % 2 + x * y % 3 == 0, lambda x, y: (x * y % 2 + x * y % 3) % 2 == 0,
                lambda x, y: ((x + y) % 2 + x * y % 3) % 2 == 0][mask]
        return [[self.dark[y][x] ^ (not self.fixed[y][x] and test(x, y)) for x in range(self.size)] for y in range(self.size)]


def _penalty(m: list[list[bool]]) -> int:
    n, score = len(m), 0
    for lines in (m, [list(col) for col in zip(*m)]):
        for line in lines:
            run = 1
            for a, b in zip(line, line[1:]):
                if a == b:
                    run += 1
                else:
                    score += run - 2 if run >= 5 else 0
                    run = 1
            score += run - 2 if run >= 5 else 0
    score += 3 * sum(1 for y in range(n - 1) for x in range(n - 1) if m[y][x] == m[y][x + 1] == m[y + 1][x] == m[y + 1][x + 1])
    dark = sum(map(sum, m))
    score += 10 * (abs(dark * 20 - n * n * 10) // (n * n))
    return score


def encode(text: str) -> list[list[bool]]:
    """The module matrix (True = dark), rows top to bottom."""
    version, words = codewords(text)
    best = None
    for mask in range(8):
        g = _Grid(version)
        g.functions(version)
        g.place(words)
        g.format(mask)
        m = g.masked(mask)
        p = _penalty(m)
        if best is None or p < best[0]:
            best = (p, m)
    return best[1]


def render(matrix: list[list[bool]], unicode: bool = True, quiet: int = 4) -> str:
    """For a dark terminal: light modules are drawn (two characters per module), dark ones are left as the background."""
    light = "██" if unicode else "##"
    n = len(matrix)
    rows = []
    for y in range(-quiet, n + quiet):
        rows.append("".join(light if not (0 <= y < n and 0 <= x < n and matrix[y][x]) else "  " for x in range(-quiet, n + quiet)))
    return "\n".join(rows)
