"""The packs sampler lives in server/generation/packs.py because the hosted service uses it live; this module only makes it importable from the tools (tests and builders)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "server"))
from generation.packs import *  # noqa: E402,F401,F403
from generation.packs import LabPack, PACKS, load, quantile_curve, inverse_cdf, cdf, Draw, MIN_DATA_MASS, QUANTILE_POINTS  # noqa: E402,F401
