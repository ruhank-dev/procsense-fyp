"""Corner plants of the extended corridor: two tall kentia-style palms in grey brushed-metal planters and two large ficus trees in terracotta planters.

Same conventions as build_reference_props (metres, Z up, front faces -Y, one mesh per material named EQ_<asset>_M_<Material>); the foliage cards reuse that module's palm frond cards and solid leaf blades.
Run from the repo root:
  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_corridor_plants.py
Outputs under unity/Logs/CorridorPlants (git-ignored): .blend, .fbx, validation.json. Dimensions are illustrative, not a product specification.
"""
import json
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_reference_props as ref  # noqa: E402
import prop_kit as pk  # noqa: E402
from prop_kit import lathe, tube  # noqa: E402

OUT = HERE.parents[1] / "Logs" / "CorridorPlants"


def metal_planter(radius, height):
    """Round brushed-metal planter: tapered body, rolled rim, soil."""
    lathe("planter", [(radius * .86, 0.0), (radius * .90, .02), (radius, height * .93), (radius * 1.04, height), (radius * .93, height), (radius * .90, height - .02), (0.0, height - .02)], (0, 0, 0), "PlanterGrey", "Z", 56)
    lathe("soil", [(.0, height - .015), (radius * .92, height - .015), (radius * .92, height - .005), (.0, height - .005)], (0, 0, 0), "Soil", "Z", 40)
    return height


def clay_planter(radius, height):
    """Terracotta planter: flared body, thick rim, shallow saucer, soil."""
    lathe("pot", [(radius * .62, .035), (radius * .66, .04), (radius * .96, height * .92), (radius * 1.08, height * .92), (radius * 1.08, height), (radius * .96, height), (radius * .90, height - .03), (0.0, height - .03)], (0, 0, 0), "Terracotta", "Z", 56)
    lathe("saucer", [(radius * .66, 0.0), (radius * .88, 0.0), (radius * .94, .03), (radius * .86, .035), (radius * .62, .035)], (0, 0, 0), "Terracotta", "Z", 48)
    lathe("soil", [(.0, height - .03), (radius * .90, height - .03), (radius * .90, height - .02), (.0, height - .02)], (0, 0, 0), "Soil", "Z", 40)
    return height


def tall_palm(seed, trunks):
    rnd = random.Random(seed)
    top = metal_planter(.27, .62)
    for k, (tx, ty, ht, lean) in enumerate(trunks):
        pts = [(0, 0, top - .02), (tx * .35, ty * .35, top + ht * .35), (tx, ty, top + ht * .86)]
        tube(f"trunk{k}", pts, .014 if k == 0 else .011, "Stem", .06, 8)
        crown = Vector(pts[-1]); n = 11 if k == 0 else 8
        for j in range(n):
            yaw = j * 360 / n + rnd.uniform(-14, 14) + k * 31
            L = rnd.uniform(.95, 1.30) * (1.0 if k == 0 else .86)
            ref.card(f"frond{k}_{j}", tuple(crown), yaw, rnd.uniform(46, 84), L, L * .62, rnd.uniform(.32, .60), "PalmFrond", rnd.uniform(-18, 18))


def palm_a():
    tall_palm(21, [(0, 0, 1.55, 4), (.12, .09, 1.22, 12), (-.13, -.07, 1.02, -10), (.04, -.15, .82, 8), (-.08, .14, .70, -6)])


def palm_b():
    tall_palm(34, [(0, 0, 1.72, -3), (-.11, .08, 1.30, -12), (.14, -.06, 1.08, 11), (-.03, -.16, .86, -4), (.09, .13, .72, 7)])


def ficus(seed):
    rnd = random.Random(seed)
    top = clay_planter(.26, .52)
    base = Vector((0, 0, top - .02))
    tube("trunk", [tuple(base), (.01, .005, top + .45), (-.015, .01, top + .92)], .026, "Stem", .08, 10)
    branches = []
    for b in range(6):
        ang = b * 60 + rnd.uniform(-15, 15); h = top + .55 + b * .12 + rnd.uniform(-.04, .04)
        r = .18 + .05 * (b % 3)
        end = Vector((math.cos(math.radians(ang)) * r, math.sin(math.radians(ang)) * r, h + .30 + rnd.uniform(0, .18)))
        tube(f"branch{b}", [(0, 0, h - .12), (end.x * .5, end.y * .5, h + .05), tuple(end)], .009, "Stem", .08, 6)
        branches.append(end)
    branches.append(Vector((0, 0, top + 1.18)))
    n = 0
    for end in branches:
        for j in range(18):
            yaw = rnd.uniform(0, 360); up = rnd.uniform(-10, 70)
            p = end + Vector((rnd.uniform(-.06, .06), rnd.uniform(-.06, .06), rnd.uniform(-.10, .06)))
            ref.blade(f"leaf{n}", tuple(p), yaw, up, rnd.uniform(.20, .30), rnd.uniform(.10, .15), rnd.uniform(.03, .07), "Leaf" if n % 3 else "LeafLight", thick=.0016, seg=3)
            n += 1


def ficus_a():
    ficus(5)


def ficus_b():
    ficus(17)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    jobs = [("corr_palm_a", palm_a), ("corr_palm_b", palm_b), ("corr_ficus_a", ficus_a), ("corr_ficus_b", ficus_b)]
    results = [pk.export(n, f, OUT, tri_budget=60000, allow_open=("PalmFrond", "PothosLeaf")) for n, f in jobs]
    (OUT / "validation.json").write_text(json.dumps(results, indent=2) + "\n")
    print("CORRIDOR PLANTS", json.dumps([(r["name"], r["triangles"]) for r in results]))


assert bpy.app.version[:3] == (4, 5, 14)
if __name__ == "__main__":
    main()
